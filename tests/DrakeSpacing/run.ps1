param([string]$ReportPath='.local-tools/v052/spacing-checks.json')
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskDir=Join-Path $taskRoot '.local-tools/v050'
New-Item -ItemType Directory -Force -Path $taskDir | Out-Null
$taskSources=@(Get-ChildItem (Join-Path $taskRoot 'unity/Assets/Scripts/Core/*.cs') | ForEach-Object FullName)
$taskSources+=Join-Path $PSScriptRoot 'Program.cs'
& "$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319/csc.exe" /nologo /target:exe /optimize+ /r:System.Web.Extensions.dll "/out:$taskDir/drake-tests.exe" $taskSources
if($LASTEXITCODE -ne 0){throw 'Compile failed'}
& "$taskDir/drake-tests.exe" (Join-Path $taskRoot 'unity/Assets/Resources/Content/unit-catalog-v010.json') (Join-Path $taskRoot 'tests/DrakeSpacing/qa-roster-v048.json') (Join-Path $taskRoot $ReportPath)
if($LASTEXITCODE -ne 0){throw 'Drake spacing regression failed'}
