$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$testRoot = Join-Path $projectRoot ('.local-tools/cloud-account-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null
$binary = Join-Path $testRoot 'CloudAccount.exe'
$core = Get-ChildItem (Join-Path $projectRoot 'unity/Assets/Scripts/Core/*.cs') | ForEach-Object FullName
& $compiler /nologo /target:exe /optimize+ /r:System.Web.Extensions.dll "/out:$binary" $core (Join-Path $PSScriptRoot 'Program.cs')
if ($LASTEXITCODE -ne 0) { throw 'Cloud account compilation failed.' }
& $binary
if ($LASTEXITCODE -ne 0) { throw 'Cloud account tests failed.' }
