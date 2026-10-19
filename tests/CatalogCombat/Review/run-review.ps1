$ErrorActionPreference = 'Stop'
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'Windows .NET Framework C# compiler required.' }
$sources = Get-ChildItem (Join-Path $projectRoot 'unity/Assets/Scripts/Core/Catalog*.cs') | ForEach-Object FullName
$catalog = Join-Path $projectRoot 'unity/Assets/Resources/Content/unit-catalog-v010.json'
foreach ($name in @('RosterReview', 'BattleReview', 'EdgeReview', 'TimingReview')) {
    $binary = Join-Path ([System.IO.Path]::GetTempPath()) ('AstraCatalog-' + [Guid]::NewGuid().ToString('N') + '.exe')
    try {
        & $compiler /nologo /target:exe /optimize+ "/out:$binary" $sources (Join-Path $PSScriptRoot ($name + '.cs'))
        if ($LASTEXITCODE -ne 0) { throw "$name compilation failed. Previous executables were not run." }
        & $binary $catalog
        if ($LASTEXITCODE -ne 0) { throw "$name failed." }
    }
    finally {
        if (Test-Path -LiteralPath $binary) { Remove-Item -LiteralPath $binary }
    }
}
