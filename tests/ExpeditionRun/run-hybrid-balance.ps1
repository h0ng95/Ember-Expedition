param([int]$FirstSeed = 1, [int]$Count = 40,
    [string]$ReportPath = '',
    [ValidateSet('TargetFirst', 'FrontFirst')][string]$GrowthOrder = 'TargetFirst')
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$binary = Join-Path ([IO.Path]::GetTempPath()) ('HybridBalance-' + [Guid]::NewGuid().ToString('N') + '.exe')
try {
    $core = Get-ChildItem (Join-Path $projectRoot 'unity/Assets/Scripts/Core/Catalog*.cs') | ForEach-Object FullName
    & $compiler /nologo /target:exe /optimize+ /r:System.Web.Extensions.dll "/out:$binary" $core `
        (Join-Path $projectRoot 'unity/Assets/Scripts/Core/ExpeditionRun.cs') `
        (Join-Path $PSScriptRoot 'HybridBalanceProbe.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Hybrid balance compilation failed.' }
    if ([string]::IsNullOrWhiteSpace($ReportPath)) {
        $ReportPath = "docs/design-v0.27/balance-normal-$($GrowthOrder.ToLowerInvariant()).json"
    }
    $report = Join-Path $projectRoot $ReportPath
    New-Item -ItemType Directory -Force -Path (Split-Path $report) | Out-Null
    & $binary (Join-Path $projectRoot 'unity/Assets/Resources/Content/unit-catalog-v010.json') $report $FirstSeed $Count $GrowthOrder
    if ($LASTEXITCODE -ne 0) { throw 'Hybrid balance simulation failed.' }
}
finally { if (Test-Path -LiteralPath $binary) { Remove-Item -LiteralPath $binary } }
