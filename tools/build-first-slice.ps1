[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$EditorPath)

$ErrorActionPreference = 'Stop'
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../unity'))
$logPath = Join-Path $projectRoot 'Logs/first-slice-build.log'
$apkPath = Join-Path $projectRoot 'Builds/Android/expedition-combat-prototype.apk'
$started = Get-Date
$arguments = @(
    '-batchmode', '-nographics', '-quit',
    '-projectPath', ('"{0}"' -f $projectRoot),
    '-buildTarget', 'Android',
    '-executeMethod', 'Expedition.EditorTools.PrototypeSetup.BuildAndroid',
    '-logFile', ('"{0}"' -f $logPath)
)
Write-Host "Building Android APK from $projectRoot"
Write-Host "Build log: $logPath"
$process = Start-Process -FilePath $EditorPath -ArgumentList ($arguments -join ' ') `
    -WorkingDirectory $projectRoot -WindowStyle Hidden -PassThru
try {
    $null = $process.Handle
    $process.WaitForExit()
    $exitCode = $process.ExitCode
}
finally { $process.Dispose() }
if (-not (Test-Path -LiteralPath $logPath)) { throw "Unity produced no build log (exit $exitCode)." }
$log = Get-Content -LiteralPath $logPath -Raw
if ($exitCode -ne 0 -or $log -notmatch 'Test APK created: Builds/Android/expedition-combat-prototype.apk') {
    Select-String -LiteralPath $logPath -Pattern 'error CS\d+|BuildFailedException|Android build failed|No valid Unity Editor license|Build completed with a result' |
        Select-Object -Last 12 | ForEach-Object { Write-Host $_.Line }
    throw "Android build did not succeed (Unity exit $exitCode). See $logPath"
}
$apk = Get-Item -LiteralPath $apkPath
if ($apk.LastWriteTime -lt $started.AddSeconds(-2)) { throw "APK was not refreshed: $apkPath" }
Write-Host "PASS: $($apk.FullName) ($($apk.Length) bytes; $($apk.LastWriteTime))"
