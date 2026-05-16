# build.ps1
param(
    [string]$Configuration = "Release"
)

Write-Host "Building Nahel..." -ForegroundColor Cyan

# Build solution
Write-Host "Building solution..." -ForegroundColor Yellow
dotnet build Nahel.sln -c $Configuration
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed." -ForegroundColor Red
    exit $LASTEXITCODE
}

# Publish CLI
Write-Host "Publishing CLI..." -ForegroundColor Yellow
dotnet publish src/Nahel.Cli/Nahel.Cli.csproj `
    -c $Configuration `
    -o publish/nahel

Write-Host "Build complete. Output in publish/nahel" -ForegroundColor Green
