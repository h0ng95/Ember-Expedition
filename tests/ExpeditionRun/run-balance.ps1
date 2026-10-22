param([int]$FirstSeed = 1, [int]$Count = 100,
    [string]$ReportPath = 'docs/design-v0.14/balance-training.json')
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$binary = Join-Path ([IO.Path]::GetTempPath()) ('ExpeditionBalance-' + [Guid]::NewGuid().ToString('N') + '.exe')
try {
    $core = Get-ChildItem (Join-Path $projectRoot 'unity/Assets/Scripts/Core/Catalog*.cs') | ForEach-Object FullName
    & $compiler /nologo /target:exe /optimize+ "/out:$binary" $core `
        (Join-Path $projectRoot 'unity/Assets/Scripts/Core/ExpeditionRun.cs') (Join-Path $PSScriptRoot 'BalanceProbe.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Balance compilation failed.' }
    $report = Join-Path $projectRoot $ReportPath
    New-Item -ItemType Directory -Force -Path (Split-Path $report) | Out-Null
    & $binary (Join-Path $projectRoot 'unity/Assets/Resources/Content/unit-catalog-v010.json') $report $FirstSeed $Count
    if ($LASTEXITCODE -ne 0) { throw 'Balance simulation failed.' }
}
finally { if (Test-Path -LiteralPath $binary) { Remove-Item -LiteralPath $binary } }
