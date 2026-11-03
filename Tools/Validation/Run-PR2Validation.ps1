param(
    [string]$UnityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.3.27f2/Editor/Unity.exe'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
# Unity clears the main project's Temp directory on startup. Keep the isolated project
# under ignored Logs so opening the real editor cannot delete a running validation.
$validationRoot = Join-Path $repositoryRoot 'Logs/PR2Validation'
$reportRoot = Join-Path $repositoryRoot 'Logs/PR2Verification'
New-Item -ItemType Directory -Force -Path "$validationRoot/Assets/Scripts", "$validationRoot/Assets/Editor", "$validationRoot/Packages", "$validationRoot/ProjectSettings", $reportRoot | Out-Null
New-Item -ItemType Directory -Force -Path "$validationRoot/Assets/Resources/Art" | Out-Null
Get-ChildItem "$repositoryRoot/Assets/_Project/Resources/Art" -File |
    Where-Object { $_.Extension -in @('.shader', '.cginc') } | Copy-Item -Destination "$validationRoot/Assets/Resources/Art"

# Copy source only. Unity generates metadata inside the disposable project.
# The editor-only harness does not enter Play Mode; include actor factories used by replay.
Get-ChildItem "$repositoryRoot/Assets/_Project/Scripts" -Recurse -Filter '*.cs' |
    Copy-Item -Destination "$validationRoot/Assets/Scripts"
Copy-Item "$PSScriptRoot/PR2Validation.cs" "$validationRoot/Assets/Editor/PR2Validation.cs"
Copy-Item "$PSScriptRoot/InputFeelValidation.cs" "$validationRoot/Assets/Editor/InputFeelValidation.cs"
Copy-Item "$PSScriptRoot/TurningValidation.cs" "$validationRoot/Assets/Editor/TurningValidation.cs"
Copy-Item "$PSScriptRoot/PresentationValidation.cs" "$validationRoot/Assets/Editor/PresentationValidation.cs"
Copy-Item "$PSScriptRoot/MenuValidation.cs" "$validationRoot/Assets/Editor/MenuValidation.cs"
Copy-Item "$PSScriptRoot/FlowAnimationValidation.cs" "$validationRoot/Assets/Editor/FlowAnimationValidation.cs"
Copy-Item "$PSScriptRoot/CurveValidation.cs" "$validationRoot/Assets/Editor/CurveValidation.cs"
Copy-Item "$PSScriptRoot/FlickValidation.cs" "$validationRoot/Assets/Editor/FlickValidation.cs"
Copy-Item "$PSScriptRoot/StadiumValidation.cs" "$validationRoot/Assets/Editor/StadiumValidation.cs"
Copy-Item "$PSScriptRoot/GoalPresentationValidation.cs" "$validationRoot/Assets/Editor/GoalPresentationValidation.cs"
Copy-Item "$PSScriptRoot/MatchDayValidation.cs" "$validationRoot/Assets/Editor/MatchDayValidation.cs"
Copy-Item "$PSScriptRoot/GoalMomentValidation.cs" "$validationRoot/Assets/Editor/GoalMomentValidation.cs"
Copy-Item "$PSScriptRoot/MenuSharpnessValidation.cs" "$validationRoot/Assets/Editor/MenuSharpnessValidation.cs"
Copy-Item "$PSScriptRoot/LightingValidation.cs" "$validationRoot/Assets/Editor/LightingValidation.cs"
Copy-Item "$PSScriptRoot/ReplayValidation.cs" "$validationRoot/Assets/Editor/ReplayValidation.cs"
foreach ($helper in @('LobbyValidation', 'PracticeValidation', 'ReplayControlsValidation', 'MobileCancellationValidation', 'AudioFeedbackValidation')) {
    Copy-Item "$PSScriptRoot/$helper.cs" "$validationRoot/Assets/Editor/$helper.cs"
}
Copy-Item "$repositoryRoot/ProjectSettings/ProjectVersion.txt" "$validationRoot/ProjectSettings/ProjectVersion.txt"

# Reuse locally resolved packages; no package downloads or main-project settings changes.
$dependencies = @{
    'com.unity.modules.physics2d' = '1.0.0'
    'com.unity.modules.ui' = '1.0.0'
    'com.unity.modules.imgui' = '1.0.0'
    'com.unity.modules.jsonserialize' = '1.0.0'
    'com.unity.modules.audio' = '1.0.0'
}
foreach ($packageName in @('com.unity.inputsystem', 'com.unity.ugui')) {
    $package = @(Get-ChildItem "$repositoryRoot/Library/PackageCache" -Directory -Filter "$packageName@*")
    if ($package.Count -ne 1) { throw "Open the main project in Unity first to resolve $packageName; expected one cached version." }
    $dependencies[$packageName] = 'file:' + $package[0].FullName.Replace('\', '/')
}
@{ dependencies = $dependencies } | ConvertTo-Json | Set-Content "$validationRoot/Packages/manifest.json"

$started = Get-Date
$process = Start-Process -FilePath $UnityEditor -WindowStyle Hidden -PassThru -ArgumentList @(
    '-batchmode', '-nographics', '-projectPath', ('"' + $validationRoot + '"'),
    '-executeMethod', 'PointPass.Validation.PR2Validation.Run', '-logFile', ('"' + "$reportRoot/unity-validation.log" + '"')
)
$process.WaitForExit()
$report = Get-Item "$reportRoot/engine-tests.txt" -ErrorAction SilentlyContinue
if (!$report -or $report.LastWriteTime -lt $started) { throw "No fresh test report. Inspect $reportRoot/unity-validation.log" }
Get-Content $report.FullName
if ($process.ExitCode -ne 0 -or (Select-String -Path $report.FullName -Pattern '^FAIL ' -Quiet)) {
    throw "Unity validation failed. Inspect $reportRoot/unity-validation.log"
}
