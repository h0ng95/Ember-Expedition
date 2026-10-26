param([int]$FirstSeed=46001, [int]$Count=20,
    [ValidateSet('current','v045')][string]$Rules='current',
    [string]$Output='.local-tools/research-matrix')
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskOutput=[IO.Path]::GetFullPath((Join-Path $taskRoot $Output))
New-Item -ItemType Directory -Force -Path $taskOutput | Out-Null
$taskBinary=Join-Path $taskOutput ('matrix-'+[Guid]::NewGuid().ToString('N')+'.exe')
$taskSources=@(Get-ChildItem (Join-Path $taskRoot 'unity/Assets/Scripts/Core/*.cs') | ForEach-Object FullName)
$taskSources+=@((Join-Path $taskRoot 'tests/ExpeditionRun/HybridBalanceProbe.cs'),(Join-Path $taskRoot 'tests/ExpeditionRun/EndToEndBalanceProbe.cs'))
& (Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe') /nologo /target:exe /optimize+ /r:System.Web.Extensions.dll /main:EndToEndBalanceProbe "/out:$taskBinary" $taskSources
if($LASTEXITCODE -ne 0){throw 'Matrix compilation failed'}
Push-Location $taskRoot
try { & node (Join-Path $PSScriptRoot 'matrix.cjs') $taskBinary $taskOutput $FirstSeed $Count $Rules
    if($LASTEXITCODE -ne 0){throw 'Matrix simulation failed'} }
finally {Pop-Location}
