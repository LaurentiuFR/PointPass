# Shared by disposable validation projects; the real project's manifest is managed by Unity.
param([hashtable]$Dependencies, [string]$RepositoryRoot)
$manifest = Get-Content "$RepositoryRoot/Packages/manifest.json" -Raw | ConvertFrom-Json
foreach ($property in $manifest.dependencies.PSObject.Properties) {
    if ($property.Name.StartsWith('com.unity.modules.')) { $Dependencies[$property.Name] = $property.Value }
}
foreach ($packageName in @('com.unity.inputsystem', 'com.unity.ugui', 'com.unity.netcode.gameobjects',
    'com.unity.transport', 'com.unity.collections', 'com.unity.burst', 'com.unity.mathematics',
    'com.unity.nuget.mono-cecil', 'com.unity.test-framework', 'com.unity.test-framework.performance')) {
    $package = @(Get-ChildItem "$RepositoryRoot/Library/PackageCache" -Directory -Filter "$packageName@*")
    if ($package.Count -ne 1) { throw "Expected one locally resolved $packageName package. Open PointPass in Unity first." }
    $Dependencies[$packageName] = 'file:' + $package[0].FullName.Replace('\', '/')
}
