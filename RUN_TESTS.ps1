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
    (Join-Path $ScriptRoot "src\ContractRewardAuthorityPolicy.cs") `
    (Join-Path $ScriptRoot "src\ContractEnemyEligibilityPolicy.cs") `
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
