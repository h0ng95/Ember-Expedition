param([int]$FirstSeed=49001,[int]$Count=4)
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Push-Location $taskRoot
try {
    & node (Join-Path $PSScriptRoot 'prepare-count-trial.cjs')
    if($LASTEXITCODE -ne 0){throw 'Trial preparation failed'}
    foreach($taskVariant in @('baseline','reinforced')) {
        $taskDir=Join-Path $taskRoot ".local-tools/v047/$taskVariant"
        $taskSources=@(Get-ChildItem "$taskDir/*.cs" | ForEach-Object FullName)
        $taskSources+=@((Join-Path $taskRoot 'tests/ExpeditionRun/HybridBalanceProbe.cs'),(Join-Path $taskRoot 'tests/ExpeditionRun/EndToEndBalanceProbe.cs'))
        & (Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe') /nologo /target:exe /optimize+ /r:System.Web.Extensions.dll /main:EndToEndBalanceProbe "/out:$taskDir.exe" $taskSources
        if($LASTEXITCODE -ne 0){throw "Trial compilation failed: $taskVariant"}
        & node tests/ResearchRegions/matrix.cjs "$taskDir.exe" ".local-tools/v047/trial-$taskVariant" $FirstSeed $Count current
        if($LASTEXITCODE -ne 0){throw "Trial failed: $taskVariant"}
    }
} finally {Pop-Location}
