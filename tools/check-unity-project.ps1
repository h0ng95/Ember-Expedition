[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$EditorPath
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $EditorPath -PathType Leaf)) {
    throw "Unity Editor executable not found: $EditorPath"
}
$editorFile = Get-Item -LiteralPath $EditorPath
$engineAssemblies = Join-Path $editorFile.DirectoryName 'Data/Managed/UnityEngine'
if ($editorFile.Name -ine 'Unity.exe' -or -not (Test-Path -LiteralPath $engineAssemblies -PathType Container)) {
    throw 'Use the installed Unity Editor Unity.exe, with Data/Managed/UnityEngine beside it. Unity Hub and the Unity CLI are not the Editor.'
}

$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../unity'))
$projectVersion = Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt'
if (-not (Test-Path -LiteralPath $projectVersion -PathType Leaf)) {
    throw "Unity project not found: $projectRoot"
}
$logDirectory = Join-Path $projectRoot 'Logs'
[System.IO.Directory]::CreateDirectory($logDirectory) | Out-Null
$logPath = Join-Path $logDirectory 'compile-check.log'

# Start-Process passes one command line to the executable; quote path arguments.
# These are validated Windows filesystem paths, which cannot contain double quotes.
$unityArguments = @(
    '-batchmode', '-nographics', '-quit',
    '-projectPath', ('"{0}"' -f $projectRoot),
    '-executeMethod', 'Expedition.EditorTools.PrototypeSetup.Configure',
    '-logFile', ('"{0}"' -f $logPath)
)
Write-Host "Checking Unity project: $projectRoot"
Write-Host "Log: $logPath"
$process = Start-Process -FilePath $editorFile.FullName -ArgumentList ($unityArguments -join ' ') `
    -WorkingDirectory $projectRoot -WindowStyle Hidden -Wait -PassThru

if (-not (Test-Path -LiteralPath $logPath -PathType Leaf)) {
    throw "Unity exited with code $($process.ExitCode) without creating its log. Open Unity Hub, sign in, activate an eligible Editor license, and verify this Editor can launch."
}
$logText = Get-Content -LiteralPath $logPath -Raw
$reportedErrors = [regex]::Matches($logText, '(?im)^.*(?:error CS\d+|Shader error|Aborting batchmode).*$')
$configured = $logText -match 'Expedition prototype ready\.'
if ($process.ExitCode -ne 0 -or $reportedErrors.Count -gt 0 -or -not $configured) {
    $reportedErrors | Select-Object -First 12 | ForEach-Object { Write-Host $_.Value }
    if ($logText -match '(?i)(no valid.*licen[cs]e|licen[cs].*(failed|failure|not found|not active|expired)|failed.*licen[cs]|entitlement.*(not found|missing|failed))') {
        Write-Host 'Unity reported a licensing problem. Open Unity Hub, sign in, and activate an eligible license for this Editor before retrying. This script does not activate or bypass licensing.'
    }
    throw "Unity check failed (exit $($process.ExitCode), reported errors $($reportedErrors.Count), setup completed $configured). Inspect $logPath"
}
Write-Host 'PASS: Unity batch import, C# compilation, and prototype setup completed.'
Write-Host 'This does not build an APK or verify rendered shaders, gameplay, touch input, or device performance.'
