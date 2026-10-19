$ErrorActionPreference = 'Stop'
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$binary = Join-Path ([System.IO.Path]::GetTempPath()) ('CatalogBalance-' + [Guid]::NewGuid().ToString('N') + '.exe')
try {
    $core = Get-ChildItem (Join-Path $projectRoot 'unity/Assets/Scripts/Core/Catalog*.cs') | ForEach-Object FullName
    & $compiler /nologo /target:exe /optimize+ "/out:$binary" $core (Join-Path $PSScriptRoot 'BalanceProbe.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Balance probe compilation failed.' }
    $report = Join-Path $projectRoot 'docs/design-v0.10/balance-results-v010.json'
    New-Item -ItemType Directory -Force -Path (Split-Path $report) | Out-Null
    & $binary (Join-Path $projectRoot 'docs/design-v0.9/unit-stats-draft.json') `
        (Join-Path $projectRoot 'unity/Assets/Resources/Content/unit-catalog-v010.json') $report
    if ($LASTEXITCODE -ne 0) { throw 'Balance probe failed.' }
}
finally { if (Test-Path -LiteralPath $binary) { Remove-Item -LiteralPath $binary } }
