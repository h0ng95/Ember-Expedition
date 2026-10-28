param([int]$FirstSeed=51101,[int]$Count=4,[string]$Output='.local-tools/v051-balance')
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskOutput=[IO.Path]::GetFullPath((Join-Path $taskRoot $Output))
New-Item -ItemType Directory -Force -Path $taskOutput | Out-Null
$taskSources=@(Get-ChildItem (Join-Path $taskRoot 'unity/Assets/Scripts/Core/*.cs') | ForEach-Object FullName)
$taskSources+=@((Join-Path $taskRoot 'tests/ExpeditionRun/HybridBalanceProbe.cs'),(Join-Path $PSScriptRoot 'Program.cs'))
$taskBinary=Join-Path $taskOutput 'boss-gate.exe'
& (Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe') /nologo /target:exe /optimize+ /r:System.Web.Extensions.dll /main:BossGateProbe "/out:$taskBinary" $taskSources
if($LASTEXITCODE -ne 0){throw 'Boss gate compilation failed'}
& $taskBinary (Join-Path $taskRoot 'unity/Assets/Resources/Content/unit-catalog-v010.json') (Join-Path $taskOutput 'results.json') $FirstSeed $Count
if($LASTEXITCODE -ne 0){throw 'Boss gate simulation failed'}
