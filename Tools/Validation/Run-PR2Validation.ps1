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

# A reused isolated project can contain scripts copied from a different branch.
# Synchronize only these two generated, flat directories before copying this branch.
$generatedDirectories = @(
    (Resolve-Path -LiteralPath "$validationRoot/Assets/Scripts").Path,
    (Resolve-Path -LiteralPath "$validationRoot/Assets/Editor").Path
)
function Remove-StaleValidationScripts([string]$Directory, [string[]]$ExpectedNames) {
    $resolvedDirectory = (Resolve-Path -LiteralPath $Directory).Path
    if ($resolvedDirectory -notin $generatedDirectories) { throw 'Refusing to clean outside the isolated validation directories.' }
    $prefix = $resolvedDirectory.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    foreach ($script in Get-ChildItem -LiteralPath $resolvedDirectory -File -Filter '*.cs') {
        if ($script.Name -in $ExpectedNames) { continue }
        foreach ($candidate in @($script.FullName, ($script.FullName + '.meta'))) {
            if (!(Test-Path -LiteralPath $candidate -PathType Leaf)) { continue }
            $resolvedFile = (Resolve-Path -LiteralPath $candidate).Path
            if (!$resolvedFile.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase) -or
                [IO.Path]::GetDirectoryName($resolvedFile) -ne $resolvedDirectory) {
                throw 'Refusing to remove a validation file outside its checked directory.'
            }
            Remove-Item -LiteralPath $resolvedFile
        }
    }
}
$sourceFiles = @(Get-ChildItem "$repositoryRoot/Assets/_Project/Scripts" -Recurse -Filter '*.cs' -File)
$helpers = @('PR2Validation', 'InputFeelValidation', 'TurningValidation', 'PresentationValidation',
    'MenuValidation', 'FlowAnimationValidation', 'CurveValidation', 'FlickValidation', 'StadiumValidation',
    'GoalPresentationValidation', 'MatchDayValidation', 'GoalMomentValidation', 'MenuSharpnessValidation',
    'LightingValidation', 'ReplayValidation', 'LobbyValidation', 'PracticeValidation',
    'ReplayControlsValidation', 'MobileCancellationValidation', 'AudioFeedbackValidation')
Remove-StaleValidationScripts $generatedDirectories[0] $sourceFiles.Name
Remove-StaleValidationScripts $generatedDirectories[1] @($helpers | ForEach-Object { $_ + '.cs' })

# Copy source only. Unity generates metadata inside the disposable project.
# The editor-only harness does not enter Play Mode; include actor factories used by replay.
$sourceFiles | Copy-Item -Destination "$validationRoot/Assets/Scripts"
foreach ($helper in $helpers) {
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
& "$PSScriptRoot/Use-CachedRuntimePackages.ps1" -Dependencies $dependencies -RepositoryRoot $repositoryRoot
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
