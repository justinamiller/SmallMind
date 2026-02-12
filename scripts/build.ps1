# Build script for SmallMind
# PowerShell script for Windows

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "SmallMind Build Script" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

# Restore dependencies
Write-Host ""
Write-Host "Restoring dependencies..." -ForegroundColor Yellow
dotnet restore SmallMind.sln

if ($LASTEXITCODE -ne 0) {
    Write-Host "=========================================" -ForegroundColor Red
    Write-Host "❌ Restore failed!" -ForegroundColor Red
    Write-Host "=========================================" -ForegroundColor Red
    exit $LASTEXITCODE
}

# Build in Release configuration
Write-Host ""
Write-Host "Building SmallMind.sln in Release configuration..." -ForegroundColor Yellow
dotnet build SmallMind.sln -c Release --no-restore

if ($LASTEXITCODE -ne 0) {
    Write-Host "=========================================" -ForegroundColor Red
    Write-Host "❌ Build failed!" -ForegroundColor Red
    Write-Host "=========================================" -ForegroundColor Red
    exit $LASTEXITCODE
}

# Run tests if they exist
Write-Host ""
Write-Host "Running tests..." -ForegroundColor Yellow
dotnet test SmallMind.sln -c Release --no-build --verbosity minimal

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "=========================================" -ForegroundColor Red
    Write-Host "❌ Tests failed!" -ForegroundColor Red
    Write-Host "=========================================" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "=========================================" -ForegroundColor Green
Write-Host "✅ Build and tests completed successfully!" -ForegroundColor Green
Write-Host "=========================================" -ForegroundColor Green
