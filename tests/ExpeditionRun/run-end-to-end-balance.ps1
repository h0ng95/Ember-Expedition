param([int]$FirstSeed = 1, [int]$Count = 20,
    [string]$ReportPath = '.local-tools/v033-end-to-end-before.json',
    [ValidateSet('TargetFirst', 'FrontFirst', 'Balanced')][string]$GrowthOrder = 'Balanced',
    [ValidateSet('all', 'none', 'early', 'delayed', 'selective')][string]$Spending = 'all',
    [ValidateSet('all', 'final', 'H-01', 'H-02', 'H-03', 'H-04', 'H-05', 'H-06')][string]$Policy = 'all')
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$binary = Join-Path ([IO.Path]::GetTempPath()) ('EndToEndBalance-' + [Guid]::NewGuid().ToString('N') + '.exe')
try {
    $core = Get-ChildItem (Join-Path $projectRoot 'unity/Assets/Scripts/Core/Catalog*.cs') | ForEach-Object FullName
    & $compiler /nologo /target:exe /optimize+ /r:System.Web.Extensions.dll /main:EndToEndBalanceProbe "/out:$binary" $core `
        (Join-Path $projectRoot 'unity/Assets/Scripts/Core/ExpeditionRun.cs') `
        (Join-Path $PSScriptRoot 'HybridBalanceProbe.cs') `
        (Join-Path $PSScriptRoot 'EndToEndBalanceProbe.cs')
    if ($LASTEXITCODE -ne 0) { throw 'End-to-end balance compilation failed.' }
    $report = Join-Path $projectRoot $ReportPath
    New-Item -ItemType Directory -Force -Path (Split-Path $report) | Out-Null
    & $binary (Join-Path $projectRoot 'unity/Assets/Resources/Content/unit-catalog-v010.json') $report $FirstSeed $Count $GrowthOrder $Spending $Policy
    if ($LASTEXITCODE -ne 0) { throw 'End-to-end balance simulation failed.' }
}
finally { if (Test-Path -LiteralPath $binary) { Remove-Item -LiteralPath $binary } }
