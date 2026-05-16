# install.ps1
param(
    [string]$InstallPath = "C:\Tools\nahel"
)

$cliExe = "publish/nahel/Nahel.Cli.exe"

# Build if needed
if (-not (Test-Path $cliExe)) {
    Write-Host "Not built. Running build first..." -ForegroundColor Yellow
    .\scripts\build.ps1
}

# Create directories
New-Item -ItemType Directory -Force -Path $InstallPath | Out-Null
New-Item -ItemType Directory -Force -Path "$InstallPath/logs" | Out-Null
New-Item -ItemType Directory -Force -Path "$InstallPath/backups" | Out-Null

# Copy CLI
Copy-Item "publish/nahel/*" $InstallPath -Recurse -Force

# Add to user PATH if not present
$currentPath = [Environment]::GetEnvironmentVariable("Path", "User")
if (-not ($currentPath -split ";" | ForEach-Object { $_.Trim() } | Where-Object { $_ -eq $InstallPath })) {
    [Environment]::SetEnvironmentVariable("Path", "$currentPath;$InstallPath", "User")
    $env:Path += ";$InstallPath"
    Write-Host "Added $InstallPath to user PATH" -ForegroundColor Yellow
}

# Create default config if not exists
$configPath = "$InstallPath/nahel.json"
if (-not (Test-Path $configPath)) {
    Copy-Item "src/Nahel.Cli/nahel.json" $configPath -Force
    Write-Host "Created default config: $configPath" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Nahel installed!" -ForegroundColor Green
Write-Host "Path: $InstallPath" -ForegroundColor Cyan
Write-Host "Run: nahel start" -ForegroundColor Cyan
Write-Host "Dashboard: http://localhost:11435/dashboard" -ForegroundColor Cyan
