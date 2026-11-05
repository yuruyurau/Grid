$ErrorActionPreference = "Stop"

$projectName = "Grid"
$binDir      = "bin"
$releaseDir  = "release"
$zipName     = "$projectName.zip"

Write-Host "=== Building ===" -ForegroundColor Cyan
& "$PSScriptRoot\build.ps1"
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed."
    exit $LASTEXITCODE
}

if (-not (Test-Path $releaseDir)) {
    New-Item -ItemType Directory -Path $releaseDir | Out-Null
}

$zipPath = Join-Path $releaseDir $zipName

if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

$items = @("$binDir\*")
if (Test-Path "LICENSE") {
    $items += "LICENSE"
    Write-Host "Included LICENSE" -ForegroundColor DarkGray
} else {
    Write-Warning "LICENSE not found (skipped)"
}

Write-Host "=== Creating zip ===" -ForegroundColor Cyan
Compress-Archive -Path $items -DestinationPath $zipPath -Force

Write-Host "Success: $zipPath" -ForegroundColor Green
