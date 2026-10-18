$ErrorActionPreference = 'Stop'
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    throw 'This smoke runner requires the Windows .NET Framework C# compiler.'
}
$executable = Join-Path ([System.IO.Path]::GetTempPath()) ('ExpeditionCombatSmoke-' + [Guid]::NewGuid().ToString('N') + '.exe')
try {
    & $compiler /nologo /target:exe /optimize+ "/out:$executable" (Join-Path $projectRoot 'unity/Assets/Scripts/Core/CombatSimulation.cs') (Join-Path $PSScriptRoot 'Program.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Combat smoke compilation failed.' }
    & $executable
    if ($LASTEXITCODE -ne 0) { throw 'Combat smoke verification failed.' }
}
finally {
    if (Test-Path -LiteralPath $executable) { Remove-Item -LiteralPath $executable }
}
