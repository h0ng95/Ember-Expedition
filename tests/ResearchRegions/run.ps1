$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$taskBinary = Join-Path $taskRoot '.local-tools/v046/ResearchRegions.exe'
New-Item -ItemType Directory -Force -Path (Split-Path $taskBinary) | Out-Null
$taskSources = Get-ChildItem (Join-Path $taskRoot 'unity/Assets/Scripts/Core/*.cs') | ForEach-Object FullName
& $taskCompiler /nologo /target:exe /optimize+ /r:System.Web.Extensions.dll "/out:$taskBinary" $taskSources (Join-Path $PSScriptRoot 'Program.cs')
if ($LASTEXITCODE -ne 0) { throw 'Compile failed' }
& $taskBinary (Join-Path $taskRoot 'unity/Assets/Resources/Content/unit-catalog-v010.json')
if ($LASTEXITCODE -ne 0) { throw 'Research region tests failed' }
