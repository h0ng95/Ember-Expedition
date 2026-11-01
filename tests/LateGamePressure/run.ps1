param([int]$FirstSeed=68101,[int]$Count=2,[string]$Output='.local-tools/v068-lategame',[switch]$ControlledOnly)
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskOutput=[IO.Path]::GetFullPath((Join-Path $taskRoot $Output))
New-Item -ItemType Directory -Force -Path $taskOutput | Out-Null
$taskSources=@(Get-ChildItem (Join-Path $taskRoot 'unity/Assets/Scripts/Core/*.cs') | ForEach-Object FullName)
$taskSources+=@((Join-Path $taskRoot 'tests/ExpeditionRun/HybridBalanceProbe.cs'),(Join-Path $PSScriptRoot 'Program.cs'))
$taskBinary=Join-Path $taskOutput 'late-pressure.exe'
& (Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe') /nologo /target:exe /optimize+ /r:System.Web.Extensions.dll /main:LateGamePressureProbe "/out:$taskBinary" $taskSources
if($LASTEXITCODE -ne 0){throw 'Late-game probe compilation failed'}
& $taskBinary (Join-Path $taskRoot 'unity/Assets/Resources/Content/unit-catalog-v010.json') $taskOutput $FirstSeed $Count $(if($ControlledOnly){'controlled'}else{'all'})
if($LASTEXITCODE -ne 0){throw 'Late-game probe failed'}
