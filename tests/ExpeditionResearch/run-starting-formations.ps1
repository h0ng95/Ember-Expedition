$ErrorActionPreference = 'Stop'
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$binary = Join-Path ([System.IO.Path]::GetTempPath()) ('StartFormation-' + [Guid]::NewGuid().ToString('N') + '.exe')
try {
    $core = Get-ChildItem (Join-Path $projectRoot 'unity/Assets/Scripts/Core/Catalog*.cs') | ForEach-Object FullName
    & $compiler /nologo /target:exe /optimize+ "/out:$binary" $core `
        (Join-Path $projectRoot 'unity/Assets/Scripts/Core/ExpeditionRun.cs') `
        (Join-Path $PSScriptRoot 'StartFormationProbe.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Start formation probe compilation failed.' }
    & $binary (Join-Path $projectRoot 'unity/Assets/Resources/Content/unit-catalog-v010.json')
    if ($LASTEXITCODE -ne 0) { throw 'Start formation probe failed.' }
}
finally { if (Test-Path -LiteralPath $binary) { Remove-Item -LiteralPath $binary } }
