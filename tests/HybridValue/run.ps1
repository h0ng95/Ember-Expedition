param([string]$ReportPath = '.local-tools/v043-paired.json', [string]$KeepBinary = '')
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$binary = Join-Path ([IO.Path]::GetTempPath()) ('HybridValue-' + [Guid]::NewGuid().ToString('N') + '.exe')
try {
    $core = Get-ChildItem (Join-Path $projectRoot 'unity/Assets/Scripts/Core/*.cs') | ForEach-Object FullName
    & $compiler /nologo /target:exe /optimize+ /r:System.Web.Extensions.dll "/out:$binary" $core (Join-Path $PSScriptRoot 'Program.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Hybrid value compilation failed.' }
    if ($KeepBinary) { Copy-Item -LiteralPath $binary -Destination (Join-Path $projectRoot $KeepBinary) }
    $report = Join-Path $projectRoot $ReportPath
    New-Item -ItemType Directory -Force -Path (Split-Path $report) | Out-Null
    & $binary (Join-Path $projectRoot 'unity/Assets/Resources/Content/unit-catalog-v010.json') $report
    if ($LASTEXITCODE -ne 0) { throw 'Hybrid value experiment failed.' }
}
finally { if (Test-Path -LiteralPath $binary) { Remove-Item -LiteralPath $binary } }
