# build.ps1
param(
    [string]$Configuration = "Release"
)

Write-Host "Building Ollamock..." -ForegroundColor Cyan

# Build Service
Write-Host "Building Service..." -ForegroundColor Yellow
dotnet publish Ollamock.Service/Ollamock.Service.csproj `
    -c $Configuration `
    -r win-x64 `
    --self-contained `
    -o publish/service

# Build App
Write-Host "Building App..." -ForegroundColor Yellow
dotnet publish Ollamock.App/Ollamock.App.csproj `
    -c $Configuration `
    -r win-x64 `
    --self-contained `
    -o publish/app

Write-Host "Build complete. Output in publish/" -ForegroundColor Green
