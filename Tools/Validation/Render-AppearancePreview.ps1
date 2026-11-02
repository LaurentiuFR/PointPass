param(
    [string]$UnityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.3.27f2/Editor/Unity.exe',
    [switch]$VerifyMenus,
    [switch]$VerifyOffline,
    [switch]$ReviewMatch
)
$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$previewRoot = Join-Path $repositoryRoot 'Logs/AppearancePreview'
$reportRoot = Join-Path $repositoryRoot 'Logs/AppearanceVerification'
New-Item -ItemType Directory -Force -Path "$previewRoot/Assets/Scripts", "$previewRoot/Assets/Editor", "$previewRoot/Assets/Resources/Art", "$previewRoot/Packages", "$previewRoot/ProjectSettings", $reportRoot | Out-Null
Get-ChildItem "$repositoryRoot/Assets/_Project/Scripts" -Recurse -Filter '*.cs' | Copy-Item -Destination "$previewRoot/Assets/Scripts"
Get-ChildItem "$repositoryRoot/Assets/_Project/Resources/Art" -File |
    Where-Object { $_.Extension -in @('.png', '.shader', '.cginc') } | Copy-Item -Destination "$previewRoot/Assets/Resources/Art"
Copy-Item "$PSScriptRoot/AppearancePreview.cs" "$previewRoot/Assets/Editor/AppearancePreview.cs"
Copy-Item "$PSScriptRoot/MenuPlayModeVerification.cs" "$previewRoot/Assets/Editor/MenuPlayModeVerification.cs"
Copy-Item "$PSScriptRoot/MatchVisualReview.cs" "$previewRoot/Assets/Editor/MatchVisualReview.cs"
Copy-Item "$PSScriptRoot/OfflineFeaturesPlayMode.cs" "$previewRoot/Assets/Editor/OfflineFeaturesPlayMode.cs"
Copy-Item "$PSScriptRoot/ReplayValidation.cs" "$previewRoot/Assets/Editor/ReplayValidation.cs"
Copy-Item "$repositoryRoot/ProjectSettings/ProjectVersion.txt" "$previewRoot/ProjectSettings/ProjectVersion.txt"
$dependencies = @{}
foreach ($module in @('physics2d','ui','imgui','audio','imageconversion','jsonserialize')) { $dependencies["com.unity.modules.$module"] = '1.0.0' }
foreach ($name in @('com.unity.inputsystem','com.unity.ugui')) {
    $package = @(Get-ChildItem "$repositoryRoot/Library/PackageCache" -Directory -Filter "$name@*")
    if ($package.Count -ne 1) { throw "Expected one locally cached $name package." }
    $dependencies[$name] = 'file:' + $package[0].FullName.Replace('\','/')
}
@{ dependencies = $dependencies } | ConvertTo-Json | Set-Content "$previewRoot/Packages/manifest.json"
$graphics = if ($VerifyMenus -and !$ReviewMatch) { '-nographics' } else { '-force-d3d11' }
$method = if ($ReviewMatch) { 'PointPass.Validation.MatchVisualReview.Run' } elseif ($VerifyOffline) { 'PointPass.Validation.OfflineFeaturesPlayMode.Run' } elseif ($VerifyMenus) { 'PointPass.Validation.MenuPlayModeVerification.Run' } else { 'PointPass.Validation.AppearancePreview.Run' }
$logName = if ($ReviewMatch) { 'match-review.log' } elseif ($VerifyOffline) { 'offline-playmode.log' } elseif ($VerifyMenus) { 'menu-playmode.log' } else { 'render.log' }
$process = Start-Process -FilePath $UnityEditor -WindowStyle Hidden -PassThru -ArgumentList @(
    '-batchmode',$graphics,'-projectPath',('"'+$previewRoot+'"'),
    '-executeMethod',$method,'-logFile',('"'+"$reportRoot/$logName"+'"')
)
$process.WaitForExit()
if ($process.ExitCode -ne 0) { throw "Unity verification failed; inspect $reportRoot/$logName" }
if ($VerifyOffline) { Get-Content "$repositoryRoot/Logs/OfflineVerification/playmode-tests.txt" }
elseif ($VerifyMenus) { Get-Content "$reportRoot/playmode-tests.txt" }
else { Write-Output "Rendered previews in $reportRoot" }
