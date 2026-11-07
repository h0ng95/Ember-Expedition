param([string]$DotnetPath)
$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (!$DotnetPath) {
    $localSdk=Join-Path $projectRoot '.local-tools/dotnet/dotnet.exe'
    $DotnetPath=if(Test-Path -LiteralPath $localSdk){$localSdk}else{'dotnet'}
}
Push-Location $projectRoot
try {
    & $DotnetPath run --project tests/RankedRuns/RankedRuns.csproj
    if($LASTEXITCODE -ne 0){throw 'Ranked replay tests failed.'}
} finally {Pop-Location}
