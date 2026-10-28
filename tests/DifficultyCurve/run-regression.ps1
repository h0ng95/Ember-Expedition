$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskDir=Join-Path $taskRoot '.local-tools/v048'
New-Item -ItemType Directory -Force -Path $taskDir | Out-Null
$taskSources=@(Get-ChildItem (Join-Path $taskRoot 'unity/Assets/Scripts/Core/*.cs') | ForEach-Object FullName)
$taskSources+=Join-Path $PSScriptRoot 'RulesTests.cs'
& "$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319/csc.exe" /nologo /target:exe /optimize+ /r:System.Web.Extensions.dll "/out:$taskDir/rules-tests.exe" $taskSources
if($LASTEXITCODE -ne 0){throw 'Compile failed'}
& "$taskDir/rules-tests.exe" (Join-Path $taskRoot 'unity/Assets/Resources/Content/unit-catalog-v010.json') (Join-Path $taskRoot 'docs/design-v0.47/fixed-party.json')
if($LASTEXITCODE -ne 0){throw 'Difficulty regression failed'}
