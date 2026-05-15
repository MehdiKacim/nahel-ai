# install.ps1
param(
    [string]$InstallPath = "C:\Tools\ollamock"
)

# Check if built
$serviceExe = "publish/service/Ollamock.Service.exe"
$appExe = "publish/app/Ollamock.App.exe"

if (-not (Test-Path $serviceExe)) {
    Write-Host "Not built. Running build first..." -ForegroundColor Yellow
    .\scripts\build.ps1
}

# Create directories
New-Item -ItemType Directory -Force -Path "$InstallPath/service" | Out-Null
New-Item -ItemType Directory -Force -Path "$InstallPath/app" | Out-Null
New-Item -ItemType Directory -Force -Path "$InstallPath/logs" | Out-Null
New-Item -ItemType Directory -Force -Path "$InstallPath/metrics" | Out-Null
New-Item -ItemType Directory -Force -Path "$InstallPath/backups" | Out-Null

# Copy service
Copy-Item "publish/service/*" "$InstallPath/service" -Recurse -Force

# Copy app
Copy-Item "publish/app/*" "$InstallPath/app" -Recurse -Force

# Install service
$serviceName = "OllamockService"
$servicePath = "$InstallPath/service/Ollamock.Service.exe"

if (Get-Service $serviceName -ErrorAction SilentlyContinue) {
    Stop-Service $serviceName -Force
    sc.exe delete $serviceName
    Start-Sleep 2
}

New-Service -Name $serviceName `
    -BinaryPathName "`"$servicePath`"" `
    -DisplayName "Ollamock Service" `
    -Description "Ollamock AI runtime gateway and launcher" `
    -StartupType Automatic

Start-Service $serviceName

# Create shortcut
$WshShell = New-Object -comObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut("$env:APPDATA\Microsoft\Windows\Start Menu\Programs\Ollamock.lnk")
$Shortcut.TargetPath = "$InstallPath/app/Ollamock.App.exe"
$Shortcut.IconLocation = "$InstallPath/app/Ollamock.App.exe,0"
$Shortcut.Save()

# Create desktop shortcut
$DesktopShortcut = $WshShell.CreateShortcut("$env:USERPROFILE\Desktop\Ollamock.lnk")
$DesktopShortcut.TargetPath = "$InstallPath/app/Ollamock.App.exe"
$DesktopShortcut.IconLocation = "$InstallPath/app/Ollamock.App.exe,0"
$DesktopShortcut.Save()

Write-Host "" 
Write-Host "Ollamock installed!" -ForegroundColor Green
Write-Host "Service: $serviceName (running)" -ForegroundColor Cyan
Write-Host "App: $InstallPath/app/Ollamock.App.exe" -ForegroundColor Cyan
Write-Host "API: http://localhost:11434" -ForegroundColor Cyan
Write-Host "Admin: http://localhost:11434/admin" -ForegroundColor Cyan
Write-Host "Tray: Right-click Ollamock icon in system tray" -ForegroundColor Cyan
