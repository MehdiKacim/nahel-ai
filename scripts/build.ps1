#requires -Version 5.1
<#
.SYNOPSIS
    Build script for Nahel — assembles a self-contained build/ directory.
.DESCRIPTION
    1. Publishes the CLI as a single-file self-contained executable.
    2. Places nahel.exe + nahel.json at build/ root.
    3. Puts debug symbols and native satellites into build/core/.
    4. Copies the OVGenAI Python bridge into build/backends/ovgenai/ and creates a venv.
    5. Copies OVMS backend into build/backends/ovms/.
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [switch]$SkipEngine
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$buildDir = Join-Path $repoRoot "build"

Write-Host "========================================"
Write-Host "Nahel Build Script"
Write-Host "========================================"

# ---------------------------------------------------------------------------
# 1. Verify .NET SDK
# ---------------------------------------------------------------------------
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    Write-Error "dotnet SDK not found. Install from https://dotnet.microsoft.com/download"
    exit 1
}
Write-Host "[1/6] dotnet SDK found: $(dotnet --version)"

# ---------------------------------------------------------------------------
# 2. Publish CLI as single-file self-contained
# ---------------------------------------------------------------------------
Write-Host "[2/6] Publishing CLI (single-file, self-contained)..."
$cliProj = Join-Path $repoRoot "src\Nahel.Cli\Nahel.Cli.csproj"
$publishDir = Join-Path $repoRoot "src\Nahel.Cli\bin\$Configuration\net8.0\$RuntimeIdentifier\publish"

# Clean previous publish to avoid stale files
if (Test-Path $publishDir) {
    Remove-Item -Recurse -Force $publishDir
}

& dotnet publish $cliProj `
    -c $Configuration `
    -r $RuntimeIdentifier `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    --verbosity minimal

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed."
    exit 1
}

if (-not (Test-Path $publishDir)) {
    Write-Error "Publish output not found at $publishDir"
    exit 1
}

# ---------------------------------------------------------------------------
# 3. Prepare build/ directory
# ---------------------------------------------------------------------------
Write-Host "[3/6] Preparing build directory..."
# Kill any running nahel processes that may lock the build directory
Get-Process -Name "nahel" -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process -Name "Nahel.Cli" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

# Ensure build dir exists
if (-not (Test-Path $buildDir)) {
    New-Item -ItemType Directory -Path $buildDir -Force | Out-Null
}

# Remove old build artifacts (preserve backends/ and models/)
Get-ChildItem -Path $buildDir | Where-Object {
    $_.Name -ine "backends" -and $_.Name -ine "models" -and $_.Name -ine "nahel.json"
} | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

# Clean old backend DLLs at backend roots (preserve bin/ subdirs)
$backendsDir = Join-Path $buildDir "backends"
if (Test-Path $backendsDir) {
    foreach ($beDir in Get-ChildItem -Path $backendsDir -Directory -ErrorAction SilentlyContinue) {
        Get-ChildItem -Path $beDir.FullName -File -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
    }
}

# Copy published output
Copy-Item -Path "$publishDir\*" -Destination $buildDir -Recurse -Force

# Create core/ for satellites and symbols
$coreDir = Join-Path $buildDir "core"
New-Item -ItemType Directory -Path $coreDir -Force | Out-Null

# Create models/ if missing
$modelsDir = Join-Path $buildDir "models"
if (-not (Test-Path $modelsDir)) {
    New-Item -ItemType Directory -Path $modelsDir -Force | Out-Null
}

# Move everything except the main exe and config to core/
$exeSource = Join-Path $buildDir "Nahel.Cli.exe"
$exeDest   = Join-Path $buildDir "nahel.exe"
if (Test-Path $exeSource) {
    Move-Item -Path $exeSource -Destination $exeDest -Force
}

$items = Get-ChildItem -Path $buildDir
foreach ($item in $items) {
    if ($item.Name -ieq "nahel.exe") { continue }
    if ($item.Name -ieq "nahel.json") { continue }
    if ($item.Name -ieq "core") { continue }
    if ($item.Name -ieq "backends") { continue }
    if ($item.Name -ieq "models") { continue }
    Move-Item -Path $item.FullName -Destination $coreDir -Force
}

Write-Host "  Executable: build\nahel.exe"
Write-Host "  Satellites: build\core\"

# ---------------------------------------------------------------------------
# 4. Build backend plugins and copy DLLs
# ---------------------------------------------------------------------------
Write-Host "[4/6] Building backend plugins..."
$backendConfigs = @(
    @{ Name="ovgenai"; Proj="backends\Nahel.Backend.OVGenAI\Nahel.Backend.OVGenAI.csproj"; Dll="Nahel.Backend.OVGenAI.dll" },
    @{ Name="ovms";    Proj="backends\Nahel.Backend.OVMS\Nahel.Backend.OVMS.csproj";       Dll="Nahel.Backend.OVMS.dll" }
)

foreach ($be in $backendConfigs) {
    $beProj = Join-Path $repoRoot $be.Proj
    $beDir = Split-Path (Join-Path $repoRoot $be.Proj) -Parent
    $beBinDir = Join-Path $beDir "bin\$Configuration\net8.0"

    & dotnet build $beProj -c $Configuration --verbosity minimal
    if ($LASTEXITCODE -ne 0) { Write-Error "Failed to build $($be.Name) backend."; exit 1 }

    $beDest = Join-Path $buildDir "backends\$($be.Name)"
    New-Item -ItemType Directory -Path $beDest -Force | Out-Null
    Copy-Item -Path (Join-Path $beBinDir $be.Dll) -Destination $beDest -Force
    Write-Host "  Copied $($be.Dll) -> build\backends\$($be.Name)\"
}

# ---------------------------------------------------------------------------
# 5. Setup OVGenAI backend (Python venv + bridge)
# ---------------------------------------------------------------------------
Write-Host "[5/6] Setting up OVGenAI backend..."
$ovgenaiDir = Join-Path $buildDir "backends\ovgenai"
$ovgenaiBinDir = Join-Path $ovgenaiDir "bin"
New-Item -ItemType Directory -Path $ovgenaiBinDir -Force | Out-Null

# Copy bridge scripts (bridge is still Python, but called by the C# wrapper)
$bridgeSource = Join-Path $repoRoot "backends\Nahel.Backend.OVGenAI\Bridge"
$bridgeDest = Join-Path $ovgenaiBinDir "bridge"
Copy-Item -Path $bridgeSource -Destination $bridgeDest -Recurse -Force

# Create Python venv (reuse if exists)
$python = Get-Command python -ErrorAction SilentlyContinue
if (-not $python) {
    Write-Error "Python not found. Please install Python 3.10+ and ensure it's on PATH."
    exit 1
}

$pythonExe = Join-Path $ovgenaiBinDir "Scripts\python.exe"
if (-not (Test-Path $pythonExe)) {
    Write-Host "  Creating Python venv in $ovgenaiBinDir ..."
    & python -m venv $ovgenaiBinDir
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to create Python venv."
        exit 1
    }
    $pythonExe = Join-Path $ovgenaiBinDir "Scripts\python.exe"
    if (-not (Test-Path $pythonExe)) {
        Write-Error "python not found in newly created venv."
        exit 1
    }
} else {
    Write-Host "  Reusing existing Python venv in $ovgenaiBinDir"
}

Write-Host "  Installing Python dependencies (this may take a while)..."
& $pythonExe -m pip install --quiet --upgrade pip
& $pythonExe -m pip install --quiet fastapi uvicorn transformers huggingface-hub jinja2

# openvino-genai is often only available on the nightly index for Windows
Write-Host "  Installing openvino-genai (nightly index)..."
& $pythonExe -m pip install --quiet --pre -U openvino-genai --extra-index-url https://storage.openvinotoolkit.org/simple/wheels/nightly --trusted-host storage.openvinotoolkit.org

# ---------------------------------------------------------------------------
# 6. Download and extract OVMS backend from GitHub releases
# ---------------------------------------------------------------------------
if (-not $SkipEngine) {
    Write-Host "[6/6] Downloading OVMS backend..."
    $ovmsDest = Join-Path $buildDir "backends\ovms"
    $ovmsBinDir = Join-Path $ovmsDest "bin"
    New-Item -ItemType Directory -Path $ovmsBinDir -Force | Out-Null

    $releaseApi = "https://api.github.com/repos/openvinotoolkit/model_server/releases/latest"
    try {
        $release = Invoke-RestMethod -Uri $releaseApi -Headers @{"Accept"="application/vnd.github.v3+json"} -TimeoutSec 30
    } catch {
        Write-Error "Failed to query OVMS GitHub releases: $($_.Exception.Message)"
        exit 1
    }

    $winAsset = $release.assets | Where-Object { $_.name -match "windows" -and $_.name -match "\.zip$" } | Select-Object -First 1
    if (-not $winAsset) {
        Write-Error "No Windows ZIP asset found in latest OVMS release ($($release.tag_name))."
        exit 1
    }

    $zipPath = Join-Path $env:TEMP $winAsset.name
    Write-Host "  Downloading $($winAsset.name) ($([math]::Round($winAsset.size/1MB, 1)) MB)..."
    try {
        Invoke-WebRequest -Uri $winAsset.browser_download_url -OutFile $zipPath -UseBasicParsing -TimeoutSec 300
    } catch {
        Write-Error "Failed to download OVMS: $($_.Exception.Message)"
        exit 1
    }

    # Extract to temp, then move contents into bin/
    $extractTemp = Join-Path $env:TEMP "ovms_extract_$([System.IO.Path]::GetRandomFileName())"
    Write-Host "  Extracting to $ovmsBinDir ..."
    Expand-Archive -Path $zipPath -DestinationPath $extractTemp -Force
    Remove-Item $zipPath -Force

    # The ZIP root usually contains an 'ovms' folder; copy its contents into bin/
    $extractedOvmsDir = Join-Path $extractTemp "ovms"
    if (Test-Path $extractedOvmsDir) {
        if (Test-Path $ovmsBinDir) {
            Remove-Item -Path $ovmsBinDir -Recurse -Force
        }
        New-Item -ItemType Directory -Path $ovmsBinDir -Force | Out-Null
        Copy-Item -Path "$extractedOvmsDir\*" -Destination $ovmsBinDir -Recurse -Force
    } else {
        if (Test-Path $ovmsBinDir) {
            Remove-Item -Path $ovmsBinDir -Recurse -Force
        }
        New-Item -ItemType Directory -Path $ovmsBinDir -Force | Out-Null
        Copy-Item -Path "$extractTemp\*" -Destination $ovmsBinDir -Recurse -Force
    }
    Remove-Item $extractTemp -Recurse -Force
    Write-Host "  OVMS extracted to build\backends\ovms\bin"
} else {
    Write-Host "[6/6] Skipping engine download (--skip-engine)"
}

# ---------------------------------------------------------------------------
# 7. Copy or generate default nahel.json
# ---------------------------------------------------------------------------
$repoConfig = Join-Path $repoRoot "nahel.json"
$nahelConfigPath = Join-Path $buildDir "nahel.json"
if (Test-Path $nahelConfigPath) {
    Write-Host "  Preserving existing build\nahel.json"
} elseif (Test-Path $repoConfig) {
    Copy-Item -Path $repoConfig -Destination $nahelConfigPath -Force
    Write-Host "  Copied nahel.json from repo root"
} else {
    $nahelConfig = @{
        server = @{
            host = "127.0.0.1"
            port = 11435
            apiKey = "local"
        }
        models = @{}
    } | ConvertTo-Json -Depth 4
    Set-Content -Path $nahelConfigPath -Value $nahelConfig -Encoding UTF8
    Write-Host "  Generated default nahel.json"
}

# ---------------------------------------------------------------------------
# Done
# ---------------------------------------------------------------------------
Write-Host ""
Write-Host "Build complete. Directory: $buildDir"
Write-Host "Run: .\build\nahel.exe start"
