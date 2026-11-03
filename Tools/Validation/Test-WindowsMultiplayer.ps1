param(
    [string]$Player = "$PSScriptRoot/../../Builds/Multiplayer/PointPass.exe",
    [int]$Port = 7777,
    [string]$ReportDirectory = "$PSScriptRoot/../../Logs/NetworkVerification/TwoPlayers"
)
$ErrorActionPreference = 'Stop'
$playerPath = (Resolve-Path -LiteralPath $Player).Path
$reportRoot = [IO.Path]::GetFullPath($ReportDirectory)
New-Item -ItemType Directory -Force -Path $reportRoot | Out-Null
$sequences = @{ host = 0; client = 0; extra = 0 }
$processes = @{}
$checks = [Collections.Generic.List[string]]::new()

function Send-Command([string]$Role, [string]$Action, [hashtable]$Values = @{}) {
    $sequences[$Role]++
    $command = @{ sequence = $sequences[$Role]; action = $Action }
    foreach ($key in $Values.Keys) { $command[$key] = $Values[$key] }
    $temporary = Join-Path $reportRoot "$Role-command.tmp"
    $command | ConvertTo-Json -Compress | Set-Content -LiteralPath $temporary
    $deadline = (Get-Date).AddSeconds(3)
    do {
        try {
            [IO.File]::Move($temporary, (Join-Path $reportRoot "$Role-command.json"), $true)
            break
        }
        catch {
            if ((Get-Date) -ge $deadline) { throw }
            Start-Sleep -Milliseconds 20
        }
    } while ($true)
    if ($Action -ne 'quit') {
        $deadline = (Get-Date).AddSeconds(5)
        do {
            if ((Read-State $Role).appliedCommand -ge $sequences[$Role]) { return }
            Start-Sleep -Milliseconds 30
        } while ((Get-Date) -lt $deadline)
        throw "Command $Action was not applied by $Role"
    }
}
function Read-State([string]$Role) {
    for ($attempt = 0; $attempt -lt 8; $attempt++) {
        try {
            $stream = [IO.File]::Open((Join-Path $reportRoot "$Role-state.json"), [IO.FileMode]::Open, [IO.FileAccess]::Read, ([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
            $reader = [IO.StreamReader]::new($stream)
            try { return ($reader.ReadToEnd() | ConvertFrom-Json) } finally { $reader.Dispose() }
        }
        catch { Start-Sleep -Milliseconds 10 }
    }
    return $null
}
function Wait-For([scriptblock]$Condition, [string]$Description, [int]$Seconds = 20) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    do {
        if (& $Condition) { $checks.Add("PASS $Description"); Write-Output "PASS $Description"; return }
        Start-Sleep -Milliseconds 100
    } while ((Get-Date) -lt $deadline)
    throw "Timed out: $Description. Host: $(Read-State host | ConvertTo-Json -Compress) Client: $(Read-State client | ConvertTo-Json -Compress)"
}
function Check([bool]$Condition, [string]$Description) {
    if (!$Condition) { throw $Description }
    $checks.Add("PASS $Description"); Write-Output "PASS $Description"
}
function Start-Player([string]$Role, [string]$Mode) {
    $arguments = @('--pp-network', $Mode, '--pp-port', "$Port", '--pp-ip', '127.0.0.1', '--pp-test',
        '--pp-test-role', $Role, '--pp-test-dir', ('"' + $reportRoot + '"'),
        '-screen-width', '1280', '-screen-height', '720', '-screen-fullscreen', '0', '-force-d3d11',
        '-logFile', ('"' + (Join-Path $reportRoot "$Role-player.log") + '"'))
    $processes[$Role] = Start-Process -FilePath $playerPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
}
function Stop-Movement {
    Send-Command host move @{ x = 0; y = 0 }
    Send-Command client move @{ x = 0; y = 0 }
    Start-Sleep -Milliseconds 300
}
function Fixture([hashtable]$Values) {
    Stop-Movement
    Send-Command host fixture $Values
    Start-Sleep -Milliseconds 450
}

try {
    # Each run owns its command/state/log names. Remove only these test files, never source/assets.
    foreach ($role in @('host', 'client', 'extra')) {
        foreach ($suffix in @('-command.json', '-state.json', '-snapshots.csv')) {
            $old = Join-Path $reportRoot ($role + $suffix)
            if (Test-Path -LiteralPath $old) { Remove-Item -LiteralPath $old }
        }
    }
    Start-Player host '--pp-host'
    Wait-For { (Read-State host).status -like 'Hosting*' } 'Host listens without starting a one-player match'
    Start-Player client '--pp-client'
    Wait-For { (Read-State host).playing -and (Read-State client).playing } 'Two real Windows processes connect and reach kickoff'
    Check ((Read-State host).players -eq 2 -and (Read-State client).players -eq 2) 'Exactly two footballers exist in each view'
    Check ((Read-State client).simulatedBodies -eq 0) 'Client has no independently simulated player or ball physics'
    Wait-For { (Read-State host).metricsConnected -and (Read-State client).metricsConnected -and (Read-State host).hasPing -and (Read-State client).hasPing } 'Both peers measure live round-trip ping and transport diagnostics'
    foreach ($role in @('host', 'client')) {
        $metrics = Read-State $role
        Check ($metrics.fps -gt 0 -and $metrics.pingMs -ge 0 -and $metrics.pingMs -lt 10000 -and $metrics.jitterMs -ge 0) "$role reports finite FPS, ping and jitter"
        Check ($metrics.packetsSent -gt 0 -and $metrics.packetsReceived -gt 0 -and $metrics.packetLossPercent -ge 0) "$role reports real transport packet counters and estimated loss"
    }
    Wait-For { (Read-State client).snapshotHz -gt 5 -and (Read-State client).downloadKbps -gt 0 -and (Read-State host).uploadKbps -gt 0 } 'Snapshot rate and bandwidth counters advance during live gameplay'
    Send-Command client diagnosticsCheck
    Wait-For { (Read-State client).diagnosticsSelfCheck } 'Diagnostics handles known gaps, duplicate/reordered samples, sequence wrap, RTT variation and reset'
    $start = Read-State host
    Send-Command host move @{ x = 0; y = 1 }
    Send-Command client move @{ x = 0; y = -1 }
    Start-Sleep -Milliseconds 800
    Stop-Movement
    $moved = Read-State host
    Check (($moved.blue.y - $start.blue.y) -gt 2 -and ($start.red.y - $moved.red.y) -gt 2) 'Host and client independently move their own opposing footballers'
    foreach ($role in @('host', 'client')) {
        $team = if ($role -eq 'host') { 'blue' } else { 'red' }
        $direction = if ($role -eq 'host') { 1 } else { -1 }
        Fixture @{ blueX = -5; blueY = -3; redX = 5; redY = 3; ballX = 0; ballY = 4 }
        $walkStart = (Read-State host).$team.x
        Send-Command $role move @{ x = $direction; y = 0; sprint = $false }
        Start-Sleep -Milliseconds 700
        $velocity=(Read-State host).($team+'Velocity')
        $walkSpeed=[Math]::Sqrt($velocity.x*$velocity.x+$velocity.y*$velocity.y)
        Send-Command $role move @{ x = 0; y = 0 }
        $walkDistance = [Math]::Abs((Read-State host).$team.x - $walkStart)
        Fixture @{ blueX = -5; blueY = -3; redX = 5; redY = 3; ballX = 0; ballY = 4 }
        $sprintStart = (Read-State host).$team.x
        Send-Command $role move @{ x = $direction; y = 0; sprint = $true }
        Start-Sleep -Milliseconds 700
        $velocity=(Read-State host).($team+'Velocity')
        $sprintSpeed=[Math]::Sqrt($velocity.x*$velocity.x+$velocity.y*$velocity.y)
        Send-Command $role move @{ x = 0; y = 0 }
        $sprintDistance = [Math]::Abs((Read-State host).$team.x - $sprintStart)
        Check ($walkSpeed -gt 4 -and $sprintSpeed -gt $walkSpeed * 1.18) "$role held sprint preserves its authoritative speed advantage"
    }
    Fixture @{ blueX = -2; blueY = 0; redX = 1.5; redY = 0; ballX = -1.35; ballY = 0 }
    Wait-For { (Read-State host).owner -eq 0 -and (Read-State client).owner -eq 0 } 'Both peers agree that blue possesses the ball'
    $redKicks = (Read-State host).redKicks
    Send-Command client shotDown
    Start-Sleep -Milliseconds 200
    Send-Command client shotUp @{ curve = 1 }
    Start-Sleep -Milliseconds 200
    Check ((Read-State host).redKicks -eq $redKicks -and (Read-State host).owner -eq 0) 'Server rejects a client kick without possession'
    Send-Command host passDown
    Start-Sleep -Milliseconds 400
    Send-Command host passUp
    Wait-For { (Read-State host).blueKicks -gt 0 } 'Host charged pass executes through existing physical kicker'
    Wait-For { (Read-State host).owner -eq 1 -and (Read-State client).owner -eq 1 } 'Physical pass reaches client and shared possession changes'
    $redKicks = (Read-State host).redKicks
    Send-Command client passDown
    Wait-For { (Read-State host).charging -eq 1 -and (Read-State client).charge -gt 0.25 } 'Client charge is measured and replicated by the host'
    Send-Command client cancel
    Wait-For { (Read-State host).charging -eq 0 -and (Read-State client).charging -eq 0 } 'Client cancellation clears the authoritative charge'
    Send-Command client passUp
    Start-Sleep -Milliseconds 200
    Check ((Read-State host).redKicks -eq $redKicks -and (Read-State host).owner -eq 1) 'Cancelled client release retains possession without a delayed kick'
    Send-Command client passDown
    Start-Sleep -Milliseconds 350
    Send-Command client passUp
    Wait-For { (Read-State host).redKicks -gt $redKicks } 'Client charged pass executes on host'
    Wait-For { (Read-State host).owner -eq 0 -and (Read-State client).owner -eq 0 } 'Return pass is physically received by blue'
    foreach ($inputCase in @('touchShot', 'keyShot', 'touchPass')) {
        # Keep isolated input kicks outside the goal mouth: faster passes must not start
        # a legitimate goal celebration/reset in the middle of the next fixture.
        Fixture @{ blueX = 5; blueY = -3; redX = 0; redY = 3; ballX = -0.65; ballY = 3 }
        Wait-For { (Read-State client).owner -eq 1 } "Client has possession for $inputCase human-input regression"
        $redKicks = (Read-State host).redKicks
        if ($inputCase -eq 'touchShot') {
            Send-Command client cancelBurst
            Start-Sleep -Milliseconds 150
            Send-Command client touchShotTap
        }
        else {
            Send-Command client ($inputCase + 'Down')
            Start-Sleep -Milliseconds 450
            Send-Command client ($inputCase + 'Up')
        }
        Wait-For { (Read-State host).redKicks -gt $redKicks } "Red $inputCase executes through HumanPlayerInput (including release before snapshot acknowledgement)"
        Send-Command client humanOff
        Start-Sleep -Milliseconds 200
    }
    # Collect in the new facing direction: a sharp turn away from an already owned
    # ball may now correctly release it instead of magnetically bringing it around.
    Fixture @{ blueX = 5; blueY = 3; redX = 0; redY = 0; ballX = 0; ballY = 0.62 }
    Send-Command client aimTap @{ x = 0; y = 1 }
    Wait-For { (Read-State host).owner -eq 1 -and (Read-State client).owner -eq 1 } 'Both peers observe collection in the retained facing direction'
    $redKicks = (Read-State host).redKicks
    Send-Command client shotDown
    Start-Sleep -Milliseconds 350
    Send-Command client shotUp @{ curve = 0 }
    Wait-For { (Read-State host).redKicks -gt $redKicks -and (Read-State host).ballVelocity.y -gt 5 -and [Math]::Abs((Read-State host).ballVelocity.x) -lt 0.5 } 'Sub-heartbeat client direction tap preserves exact kick aim without movement'
    Send-Command client aimTap @{ x = -1; y = 0 }
    Start-Sleep -Milliseconds 120
    Fixture @{ blueX = 5; blueY = 3; redX = 0; redY = 0; ballX = -0.65; ballY = 0 }
    Wait-For { (Read-State host).owner -eq 1 } 'Client acquires shooting fixture through normal pickup'
    $redKicks = (Read-State host).redKicks
    Send-Command client shotDown
    Start-Sleep -Milliseconds 1100
    Send-Command client shotUp @{ curve = 1 }
    Wait-For { (Read-State host).redKicks -gt $redKicks -and [Math]::Abs((Read-State host).ballCurve) -gt 0.1 } 'Client charged curve shot bends the authoritative physical ball'
    Start-Sleep -Milliseconds 250
    Check ([Math]::Abs((Read-State host).ballVelocity.y) -gt 1) 'Curve changes travel direction after strike'
    Fixture @{ blueX = 6; blueY = 0; redX = -5; redY = 3; ballX = 6.65; ballY = 0 }
    Wait-For { (Read-State host).owner -eq 0 } 'Scoring fixture uses normal pickup'
    Send-Command host shotDown
    Start-Sleep -Milliseconds 1000
    Send-Command host shotUp @{ curve = 0 }
    Wait-For { (Read-State host).blueScore -eq 1 -and (Read-State client).blueScore -eq 1 } 'Physical shot crosses goal and scores identically on both peers'
    $goal = Read-State host
    Start-Sleep -Milliseconds 450
    Check ([Math]::Abs((Read-State host).clock - $goal.clock) -lt 0.01) 'Host clock stops during goal celebration'
    Send-Command host capture
    Send-Command client capture
    Wait-For { (Read-State host).resetVersion -gt $goal.resetVersion -and (Read-State client).resetVersion -gt $goal.resetVersion -and (Read-State client).playing } 'Goal celebration ends in the same kickoff reset'
    $reset = Read-State host
    Check ([Math]::Abs($reset.ball.x) -lt 0.01 -and [Math]::Abs($reset.ball.y) -lt 0.01 -and $reset.owner -eq -1) 'Restart clears ball momentum and possession'
    Start-Player extra '--pp-client'
    Wait-For { (Read-State extra).status -like '*Server full*' } 'Third connection receives server-full feedback'
    Send-Command extra quit
    Send-Command client disconnect
    Wait-For { (Read-State host).players -eq 0 -and (Read-State host).status -like '*waiting*' -and !(Read-State client).hasSession -and !(Read-State client).busy } 'Client departure removes the match and leaves host reusable'
    Send-Command client join @{ slot = $Port }
    Wait-For { (Read-State host).playing -and (Read-State client).playing -and (Read-State host).blueScore -eq 0 } 'Same client rejoins a clean match without duplicate actors'
    Send-Command host disconnect
    Wait-For { !(Read-State host).hasSession -and !(Read-State host).busy -and !(Read-State client).hasSession -and !(Read-State client).busy -and (Read-State client).players -eq 0 } 'Host departure ends client match cleanly'
    Send-Command host host @{ slot = $Port }
    Wait-For { (Read-State host).status -like 'Hosting*' } 'Hosting again reuses the port after clean shutdown'
    Send-Command client join @{ slot = $Port }
    Wait-For { (Read-State host).playing -and (Read-State client).playing } 'Both peers reconnect after full session shutdown'
    Send-Command host restart
    Wait-For { (Read-State client).resetVersion -ge 2 -and (Read-State client).playing } 'Explicit host restart replicates to client'

    $hostRows = Import-Csv -LiteralPath (Join-Path $reportRoot 'host-snapshots.csv')
    $clientRows = Import-Csv -LiteralPath (Join-Path $reportRoot 'client-snapshots.csv')
    # Sequence numbers restart for each connection; compare the first full session only.
    $hostBySequence = @{}
    foreach ($row in $hostRows) { if ($hostBySequence.ContainsKey($row.sequence)) { break }; $hostBySequence[$row.sequence] = $row }
    $matched = 0
    $previousSequence = 0
    foreach ($row in $clientRows) {
        if ([int]$row.sequence -le $previousSequence) { break }
        $previousSequence = [int]$row.sequence
        if (!$hostBySequence.ContainsKey($row.sequence)) { continue }
        $expected = $hostBySequence[$row.sequence]
        foreach ($property in $row.PSObject.Properties.Name) {
            if ($row.$property -ne $expected.$property) { throw "Snapshot $($row.sequence) differs at $property" }
        }
        $matched++
    }
    Check ($matched -ge 100) "Identical authoritative snapshots across $matched matching sequence numbers (ball, players, ownership, score, clock, kicks, curve)"
    foreach ($role in @('host', 'client')) {
        $errors = Select-String -LiteralPath (Join-Path $reportRoot "$role-player.log") -Pattern 'Exception:|NullReferenceException|\[Error\]' -Quiet
        Check (!$errors) "$role player log contains no runtime exceptions"
    }
    $checks.Add('PASS TWO-INSTANCE MULTIPLAYER VERIFICATION COMPLETE')
}
catch {
    $checks.Add('FAIL ' + $_.Exception.Message)
    throw
}
finally {
    foreach ($role in $processes.Keys) {
        if (!$processes[$role].HasExited) { Send-Command $role quit }
    }
    Start-Sleep -Milliseconds 800
    foreach ($process in $processes.Values) {
        if (!$process.HasExited) { Stop-Process -Id $process.Id }
    }
    $checks | Set-Content -LiteralPath (Join-Path $reportRoot 'tests.txt')
}
