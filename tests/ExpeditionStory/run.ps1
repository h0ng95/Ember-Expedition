$ErrorActionPreference = 'Stop'
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$framework = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
$jsonLibrary = Join-Path $framework 'System.Web.Extensions.dll'
if (-not (Test-Path -LiteralPath $compiler) -or -not (Test-Path -LiteralPath $jsonLibrary)) {
    throw 'Windows .NET Framework C# compiler and System.Web.Extensions are required.'
}
$binary = Join-Path ([System.IO.Path]::GetTempPath()) ('ExpeditionStory-' + [Guid]::NewGuid().ToString('N') + '.exe')
try {
    $core = Get-ChildItem (Join-Path $projectRoot 'unity/Assets/Scripts/Core/Catalog*.cs') | ForEach-Object FullName
    & $compiler /nologo /target:exe /optimize+ "/out:$binary" "/r:$jsonLibrary" $core `
        (Join-Path $projectRoot 'unity/Assets/Scripts/Core/ExpeditionRun.cs') `
        (Join-Path $projectRoot 'unity/Assets/Scripts/Core/ExpeditionStory.cs') `
        (Join-Path $PSScriptRoot 'Program.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Expedition story test compilation failed.' }
    & $binary (Join-Path $projectRoot 'unity/Assets/Resources/Content/unit-catalog-v010.json')
    if ($LASTEXITCODE -ne 0) { throw 'Expedition story test failed.' }
}
finally { if (Test-Path -LiteralPath $binary) { Remove-Item -LiteralPath $binary } }
