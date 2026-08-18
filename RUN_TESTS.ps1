$ErrorActionPreference = "Stop"
$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

function Find-Csc {
    foreach ($path in @(
        "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
        "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
    )) {
        if (Test-Path $path) { return $path }
    }
    throw "csc.exe not found. Install the .NET Framework Developer Pack or Visual Studio Build Tools."
}

$csc = Find-Csc
$out = Join-Path $env:TEMP "ErenshorContractsCoreTests.exe"
& $csc /nologo /target:exe /out:$out `
    (Join-Path $ScriptRoot "src\ContractModels.cs") `
    (Join-Path $ScriptRoot "src\ContractRewardPolicy.cs") `
    (Join-Path $ScriptRoot "src\ContractRewardConfigMigrationPolicy.cs") `
    (Join-Path $ScriptRoot "src\ContractRewardAuthorityPolicy.cs") `
    (Join-Path $ScriptRoot "src\ContractEnemyEligibilityPolicy.cs") `
    (Join-Path $ScriptRoot "src\ContractEnemyTargetPolicy.cs") `
    (Join-Path $ScriptRoot "src\ContractMobTargetPolicy.cs") `
    (Join-Path $ScriptRoot "src\ContractCombatPolicy.cs") `
    (Join-Path $ScriptRoot "src\ContractKillCreditPolicy.cs") `
    (Join-Path $ScriptRoot "src\ContractCore.cs") `
    (Join-Path $ScriptRoot "src\ContractStore.cs") `
    (Join-Path $ScriptRoot "src\ContractCharacterKey.cs") `
    (Join-Path $ScriptRoot "src\ContractBoardApi.cs") `
    (Join-Path $ScriptRoot "src\ContractJournalQueue.cs") `
    (Join-Path $ScriptRoot "tests\ContractCoreTests.cs") `
    (Join-Path $ScriptRoot "tests\ContractCombatPolicyTests.cs") `
    (Join-Path $ScriptRoot "tests\ContractCharacterKeyTests.cs") `
    (Join-Path $ScriptRoot "tests\ContractStoreTests.cs") `
    (Join-Path $ScriptRoot "tests\ContractJournalQueueTests.cs") `
    (Join-Path $ScriptRoot "tests\ContractBoardApiTests.cs") `
    (Join-Path $ScriptRoot "tests\ContractRewardPolicyTests.cs") `
    (Join-Path $ScriptRoot "tests\TestRunner.cs")
if ($LASTEXITCODE -ne 0) { throw "Contracts core tests did not compile." }
& $out
if ($LASTEXITCODE -ne 0) { throw "Contracts core tests failed." }

# Unity-free retained-UI visibility/fallback, action routing, strict bool mutation parsing, gesture cleanup, and normalized-position recovery policy.
$suiteUiOut = Join-Path $env:TEMP "ErenshorContracts.SuiteUiPolicyTests.exe"
& $csc /nologo /target:exe /out:$suiteUiOut `
    (Join-Path $ScriptRoot "src\SuiteUiPolicies.cs") `
    (Join-Path $ScriptRoot "tests\SuiteUiPolicyTests.cs")
if ($LASTEXITCODE -ne 0) { throw "Suite UI policy tests did not compile." }
& $suiteUiOut
if ($LASTEXITCODE -ne 0) { throw "Suite UI policy tests failed." }

# Current-assembly reward wiring is intentionally source-guarded here: native GameData cannot be
# constructed in the standalone deterministic runner, but the guards lock the proven typed calls,
# raid preflight, durable component ledger ordering, and one-time config migration into the suite.
$pluginSource = Get-Content (Join-Path $ScriptRoot "src\ErenshorContractsPlugin.cs") -Raw
$adapterSource = Get-Content (Join-Path $ScriptRoot "src\ContractNativeRewardAdapter.cs") -Raw
$migrationSource = Get-Content (Join-Path $ScriptRoot "src\ContractRewardConfigMigrationPolicy.cs") -Raw
$controlSource = Get-Content (Join-Path $ScriptRoot "src\ContractsControlApi.cs") -Raw
if ($adapterSource -notmatch 'GameData\.AddExperience\(plan\.XpAmount, false\)' -or $adapterSource -match 'MethodInfo|\.Invoke\(') { throw "Contracts reward wiring failed: direct XP call missing or reflection remains." }
if ($adapterSource -notmatch 'GameData\.PlayerInv\.UpdatePlayerInventory\(\)') { throw "Contracts reward wiring failed: native inventory refresh missing." }
if ($adapterSource -notmatch 'if \(GameData\.RaidActive\).*Finish or leave the raid before claiming this contract') { throw "Contracts reward wiring failed: whole-claim raid deferral missing." }
if ($pluginSource -notmatch 'TryPrepare\(candidate, ControlNativeXpRewardsEnabled' -or $pluginSource -notmatch 'if \(!ApplyGoldComponent\(occurrenceId, plan\)\) return;') { throw "Contracts reward wiring failed: preflight-before-Gold ordering missing." }
if ($pluginSource -notmatch 'PrepareRewardComponent[\s\S]*SaveNow\(\)[\s\S]*MarkRewardComponentApplying[\s\S]*SaveNow\(\)[\s\S]*TryGrantXp') { throw "Contracts reward wiring failed: XP durable component ordering missing." }
if ($migrationSource -notmatch 'CurrentSchema = 1' -or $pluginSource -notmatch 'MigrateRewardConfig\(\)[\s\S]*Config\.Save\(\)') { throw "Contracts reward wiring failed: one-time config migration persistence missing." }
if ($controlSource -notmatch 'GetRewardDiagnostics') { throw "Contracts reward wiring failed: reward diagnostics endpoint missing." }
Write-Host "PASS Contracts reward migration/wiring guards" -ForegroundColor Green
$launcherVisual = Get-Content (Join-Path $ScriptRoot "src\StandaloneLauncherVisual.cs") -Raw
$launcherSource = Get-Content (Join-Path $ScriptRoot "src\ContractLauncher.cs") -Raw
$windowSource = Get-Content (Join-Path $ScriptRoot "src\ContractBoardWindow.cs") -Raw
if ($launcherVisual -notmatch 'Width\s*=\s*154f' -or $launcherVisual -notmatch 'Height\s*=\s*32f' -or
    $launcherVisual -notmatch 'GripWidth\s*=\s*20f' -or $launcherVisual -notmatch '"GripDot"' -or
    $launcherSource -notmatch 'StyleGrip\(grip\)' -or $windowSource -notmatch 'AddVerticalChevron\(_collapseChevron, !_collapsed\)') {
    throw "Contracts Forgotten Roads launcher/chevron visual contract failed."
}
Write-Host "PASS Contracts Forgotten Roads launcher/chevron visual contract" -ForegroundColor Green
