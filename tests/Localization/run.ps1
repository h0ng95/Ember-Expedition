$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$testRoot = Join-Path $projectRoot ('.local-tools/localization-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null
$binary = Join-Path $testRoot 'Localization.exe'
$core = Get-ChildItem (Join-Path $projectRoot 'unity/Assets/Scripts/Core/*.cs') | ForEach-Object FullName
& $compiler /nologo /target:exe /optimize+ /r:System.Web.Extensions.dll "/out:$binary" $core (Join-Path $PSScriptRoot 'Program.cs')
if ($LASTEXITCODE -ne 0) { throw 'Localization test compilation failed.' }
& $binary (Join-Path $projectRoot 'unity/Assets/Resources/Content/unit-catalog-v010.json') (Join-Path $projectRoot 'content/localization/messages.json')
if ($LASTEXITCODE -ne 0) { throw 'Localization tests failed.' }
