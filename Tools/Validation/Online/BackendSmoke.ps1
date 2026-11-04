param([int]$Port = 15841)
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$apiDll = Join-Path $taskRoot 'Backend/PointPass.Api/bin/Release/net10.0/PointPass.Api.dll'
if (!(Test-Path -LiteralPath $apiDll)) { throw 'Build Backend/PointPass.Api first.' }
$logRoot = Join-Path $taskRoot 'Logs/BackendSmoke'
[IO.Directory]::CreateDirectory($logRoot) | Out-Null
$process = $null
$original = @{}
foreach ($name in @('PointPass__WorkerKey','ConnectionStrings__PointPass')) {
    $original[$name] = [Environment]::GetEnvironmentVariable($name)
}
try {
    $bytes = New-Object byte[] 32
    [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    $env:PointPass__WorkerKey = [Convert]::ToBase64String($bytes)
    $env:ConnectionStrings__PointPass = 'Host=127.0.0.1;Port=1;Database=pointpass_validation;Username=unused;Timeout=1'
    $process = Start-Process -FilePath 'dotnet' -ArgumentList @($apiDll,'--urls',"http://127.0.0.1:$Port") -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $logRoot 'stdout.txt') -RedirectStandardError (Join-Path $logRoot 'stderr.txt')
    Add-Type -AssemblyName System.Net.Http
    $http = [Net.Http.HttpClient]::new()
    $http.Timeout = [TimeSpan]::FromSeconds(3)
    $http.BaseAddress = [Uri]"http://127.0.0.1:$Port"
    $ready = $false
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        try {
            $response = $http.GetAsync('/health').GetAwaiter().GetResult()
            $ready = [int]$response.StatusCode -eq 200
            $response.Dispose()
            if ($ready) { break }
        } catch { Start-Sleep -Milliseconds 100 }
    }
    if (!$ready) { throw 'Smoke-test API failed to start; inspect ignored Logs/BackendSmoke.' }
    $cases = @(
        @{Method='GET';Path='/v1/profile';Status=401},
        @{Method='GET';Path='/v1/profile?playerId=someoneelse';Status=400},
        @{Method='POST';Path='/internal/worker/heartbeat';Body='{"instanceId":"20000000-0000-0000-0000-000000000001"}';Status=403},
        @{Method='POST';Path='/v1/guests/session';Body='{"credential":null}';Status=400},
        @{Method='POST';Path='/v1/guests/session';Body='{"credential":"a","credential":"b"}';Status=400},
        @{Method='POST';Path='/v1/room/join';Body='{"gameVersion":"x","position":99}';Status=400},
        @{Method='POST';Path='/v1/guests/session';Body=('x' * 9000);Status=413}
    )
    foreach ($case in $cases) {
        if ($case.Method -eq 'GET') { $response = $http.GetAsync($case.Path).GetAwaiter().GetResult() }
        else {
            $body = [Net.Http.StringContent]::new($case.Body,[Text.Encoding]::UTF8,'application/json')
            try { $response = $http.PostAsync($case.Path,$body).GetAwaiter().GetResult() } finally { $body.Dispose() }
        }
        try {
            $actual = [int]$response.StatusCode
            if ($actual -ne $case.Status) { throw "HTTP smoke check failed: $($case.Method) $($case.Path) returned $actual, expected $($case.Status)." }
            Write-Output "PASS $($case.Method) $($case.Path): $actual"
        } finally { $response.Dispose() }
    }
    Write-Output 'Completed seven HTTP security checks. Database and gameplay were not tested by this suite.'
} finally {
    if ($null -ne $process -and !$process.HasExited) { Stop-Process -Id $process.Id }
    if ($null -ne $http) { $http.Dispose() }
    foreach ($name in $original.Keys) { [Environment]::SetEnvironmentVariable($name,$original[$name]) }
}
