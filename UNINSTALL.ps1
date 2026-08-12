param(
    [string]$BepInExRoot = ""
)
$ErrorActionPreference = "Stop"

if (-not $BepInExRoot) {
    throw "Pass -BepInExRoot pointing at the profile/root that contains BepInEx."
}
$pluginDir = Join-Path $BepInExRoot "BepInEx\plugins\ErenshorContracts"
if (Test-Path $pluginDir) {
    Remove-Item -Recurse -Force $pluginDir
    Write-Host "Removed Erenshor Contracts plugin files." -ForegroundColor Green
}
else {
    Write-Host "Erenshor Contracts plugin folder was not present."
}
Write-Host "Saved contract state under BepInEx\config\ErenshorContracts is intentionally left in place." -ForegroundColor Yellow
