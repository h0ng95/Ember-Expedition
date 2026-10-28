param([int]$FirstSeed=49501,[int]$Count=4,[string]$ReportPath='.local-tools/v047/fixed-party.json',
    [ValidateSet('current','v046')][string]$Rules='current',[switch]$ComparePrevious)
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskReport=[IO.Path]::GetFullPath((Join-Path $taskRoot $ReportPath))
New-Item -ItemType Directory -Force -Path (Split-Path $taskReport) | Out-Null
$taskBinary=Join-Path (Split-Path $taskReport) ('curve-'+[Guid]::NewGuid().ToString('N')+'.exe')
$taskSources=@(Get-ChildItem (Join-Path $taskRoot 'unity/Assets/Scripts/Core/*.cs') | ForEach-Object FullName)
$taskSources+=@((Join-Path $taskRoot 'tests/ExpeditionRun/HybridBalanceProbe.cs'),(Join-Path $PSScriptRoot 'Program.cs'))
& (Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe') /nologo /target:exe /optimize+ /r:System.Web.Extensions.dll /main:DifficultyCurveProbe "/out:$taskBinary" $taskSources
if($LASTEXITCODE -ne 0){throw 'Difficulty probe compilation failed'}
& $taskBinary (Join-Path $taskRoot 'unity/Assets/Resources/Content/unit-catalog-v010.json') $taskReport $FirstSeed $Count $Rules $(if($ComparePrevious){'compare'}else{'single'})
if($LASTEXITCODE -ne 0){throw 'Difficulty probe failed'}
