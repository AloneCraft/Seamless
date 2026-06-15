param(
    [string]$ProjectRoot = ".",
    [ValidateSet("editmode", "playmode", "all")]
    [string]$TestPlatform = "editmode",
    [string]$UnityPath = "",
    [string]$ResultsDir = "TestResults"
)

$ErrorActionPreference = "Stop"

function Find-UnityExecutable {
    param([string]$ExplicitPath)

    if ($ExplicitPath -and (Test-Path -LiteralPath $ExplicitPath)) {
        return (Resolve-Path -LiteralPath $ExplicitPath).Path
    }

    $hubEditors = @(
        "C:\Program Files\Unity\Hub\Editor",
        "${env:ProgramFiles}\Unity\Hub\Editor"
    ) | Where-Object { $_ -and (Test-Path -LiteralPath $_) }

    foreach ($root in $hubEditors) {
        $candidates = Get-ChildItem -LiteralPath $root -Recurse -Filter Unity.exe -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match "\\Editor\\Unity\.exe$" } |
            Sort-Object FullName -Descending

        if ($candidates.Count -gt 0) {
            return $candidates[0].FullName
        }
    }

    throw "Unity.exe was not found. Pass -UnityPath explicitly."
}

function Invoke-UnityTestRun {
    param(
        [string]$UnityExe,
        [string]$ProjectPath,
        [string]$Platform,
        [string]$OutputDir
    )

    $resultPath = Join-Path $OutputDir "$Platform-results.xml"
    $logPath = Join-Path $OutputDir "$Platform.log"

    $args = @(
        "-batchmode",
        "-nographics",
        "-quit",
        "-projectPath", $ProjectPath,
        "-runTests",
        "-testPlatform", $Platform,
        "-testResults", $resultPath,
        "-logFile", $logPath
    )

    Write-Host "Running Unity $Platform tests..."
    Write-Host "$UnityExe $($args -join ' ')"

    $process = Start-Process -FilePath $UnityExe -ArgumentList $args -NoNewWindow -Wait -PassThru

    if ($process.ExitCode -ne 0) {
        Write-Error "Unity $Platform tests failed with exit code $($process.ExitCode). Log: $logPath Results: $resultPath"
    }

    Write-Host "Passed $Platform tests. Results: $resultPath Log: $logPath"
}

$resolvedRoot = (Resolve-Path -LiteralPath $ProjectRoot).Path
$resolvedResults = Join-Path $resolvedRoot $ResultsDir
$projectName = Split-Path -Leaf $resolvedRoot
$openUnityForProject = Get-Process Unity -ErrorAction SilentlyContinue |
    Where-Object { $_.MainWindowTitle -like "*$projectName*" }

if ($openUnityForProject) {
    $processList = ($openUnityForProject | ForEach-Object { "$($_.Id): $($_.MainWindowTitle)" }) -join "; "
    throw "Unity already has this project open. Close the Unity Editor before running batchmode tests. Processes: $processList"
}

if (-not (Test-Path -LiteralPath $resolvedResults)) {
    New-Item -ItemType Directory -Path $resolvedResults | Out-Null
}

$unityExePath = Find-UnityExecutable -ExplicitPath $UnityPath
$platforms = if ($TestPlatform -eq "all") {
    @("editmode", "playmode")
} else {
    @($TestPlatform)
}

foreach ($platform in $platforms) {
    Invoke-UnityTestRun -UnityExe $unityExePath -ProjectPath $resolvedRoot -Platform $platform -OutputDir $resolvedResults
}
