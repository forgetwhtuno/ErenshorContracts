param(
    [string]$GameDir = ""
)
$ErrorActionPreference = "Stop"

if (-not $GameDir) {
    throw "Pass -GameDir pointing at the Erenshor install folder (contains Erenshor.exe)."
}
$dll = Join-Path $GameDir "plugins\ErenshorContracts.dll"
if (Test-Path $dll) {
    Remove-Item -Force $dll
    Write-Host "Removed Erenshor Contracts plugin file." -ForegroundColor Green
}
else {
    Write-Host "Erenshor Contracts plugin file was not present."
}
Write-Host "Saved contract state under plugins\config\ErenshorContracts is intentionally left in place." -ForegroundColor Yellow
