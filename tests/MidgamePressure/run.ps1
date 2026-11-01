param([int]$FirstSeed=67101,[int]$Count=2,[string]$Output='.local-tools/v067-pressure',
    [ValidateSet('Balanced','OffenseBalanced')][string]$GrowthOrder='Balanced',[switch]$SelectedOnly,[switch]$Production)
$ErrorActionPreference='Stop'
if($SelectedOnly -and $Production){throw 'Select one comparison mode'}
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskOutput=[IO.Path]::GetFullPath((Join-Path $taskRoot $Output))
New-Item -ItemType Directory -Force -Path $taskOutput | Out-Null
$taskSources=@(Get-ChildItem (Join-Path $taskRoot 'unity/Assets/Scripts/Core/*.cs') | ForEach-Object FullName)
$taskSources+=@((Join-Path $taskRoot 'tests/ExpeditionRun/HybridBalanceProbe.cs'),(Join-Path $PSScriptRoot 'Program.cs'))
$taskBinary=Join-Path $taskOutput 'midgame-pressure.exe'
& (Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe') /nologo /target:exe /optimize+ /r:System.Web.Extensions.dll /main:MidgamePressureProbe "/out:$taskBinary" $taskSources
if($LASTEXITCODE -ne 0){throw 'Midgame probe compilation failed'}
& $taskBinary (Join-Path $taskRoot 'unity/Assets/Resources/Content/unit-catalog-v010.json') (Join-Path $taskOutput 'results.json') $FirstSeed $Count $GrowthOrder $(if($Production){'production'}elseif($SelectedOnly){'selected'}else{'all'})
if($LASTEXITCODE -ne 0){throw 'Midgame probe failed'}
