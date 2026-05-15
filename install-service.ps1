# install-service.ps1
param(
    [string]$InstallPath = "C:\Tools\ollama-bridge",
    [string]$SourcePath = ".\publish"
)

if (-not (Test-Path $SourcePath)) {
    Write-Host "Publication..."
    dotnet publish -c Release -r win-x64 --self-contained -o $SourcePath
}

New-Item -ItemType Directory -Force -Path $InstallPath | Out-Null
Copy-Item "$SourcePath\*" $InstallPath -Recurse -Force

$ollamaHost = [Environment]::GetEnvironmentVariable("OLLAMA_HOST", "User")
if ($ollamaHost -ne "http://localhost:11436") {
    Write-Host ""
    Write-Host "ATTENTION: Configurez Ollama natif sur :11436" -ForegroundColor Yellow
    Write-Host "  [Environment]::SetEnvironmentVariable('OLLAMA_HOST', 'http://localhost:11436', 'User')" -ForegroundColor Cyan
    Write-Host "  Puis redemarrez Ollama" -ForegroundColor Cyan
    Write-Host ""
}

$serviceName = "OllamaOpenVINOBridge"
$exe = "$InstallPath\OllamaBridge.exe"

if (Get-Service $serviceName -ErrorAction SilentlyContinue) {
    Stop-Service $serviceName -Force
    sc.exe delete $serviceName
    Start-Sleep 2
}

New-Service -Name $serviceName `
    -BinaryPathName "`"$exe`"" `
    -DisplayName "Ollama OpenVINO Bridge" `
    -Description "Proxy multi-backend Ollama -> OpenVINO (LLM/Vision/Embed)" `
    -StartupType Automatic

Start-Service $serviceName
Write-Host "Service $serviceName installe et demarre" -ForegroundColor Green
Write-Host ""
Write-Host "Cockpit: http://localhost:11434/admin" -ForegroundColor Cyan
Write-Host "API:     http://localhost:11434/api/generate" -ForegroundColor Cyan
Write-Host "Config:  $InstallPath\appsettings.json" -ForegroundColor Cyan
