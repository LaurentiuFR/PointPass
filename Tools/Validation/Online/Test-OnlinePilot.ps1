param(
    [string]$Player = "$PSScriptRoot/../../../Builds/OnlinePilot/Client/PointPass.exe",
    [string]$BlueInvitationFile = "$PSScriptRoot/../../../Logs/OnlinePilotVerification/Invitations/blue.txt",
    [string]$RedInvitationFile = "$PSScriptRoot/../../../Logs/OnlinePilotVerification/Invitations/red.txt",
    [string]$ReportDirectory = "$PSScriptRoot/../../../Logs/OnlinePilotVerification/Clients",
    [string]$BlueProfile = 'pilot-blue',
    [string]$RedProfile = 'pilot-red',
    [scriptblock]$InvitationProvider,
    [switch]$FullMatch,
    [switch]$KeepClients
)

# The built clients always use the authenticated internet endpoint configured in the
# game. This harness issues ordinary local inputs. There are no actor-placement,
# possession, time, score, server-credential or trusted-result fixture commands.
$ErrorActionPreference = 'Stop'
$playerPath = (Resolve-Path -LiteralPath $Player).Path
$reportRoot = [IO.Path]::GetFullPath($ReportDirectory)
$roles = @($BlueProfile, $RedProfile)
foreach ($role in $roles) {
    if ($role -notmatch '^[A-Za-z0-9-]{1,24}$') { throw 'Profiles must contain1–24ASCIIletters/digits/hyphens.' }
}
if ($BlueProfile -eq $RedProfile) { throw 'Use different guest profiles for the two clients.' }
if ($InvitationProvider -and ($PSBoundParameters.ContainsKey('BlueInvitationFile') -or $PSBoundParameters.ContainsKey('RedInvitationFile'))) {
    throw 'Select either the private in-memory invitation provider or legacy invitation files.'
}
$invitations = @{
    $BlueProfile = [IO.Path]::GetFullPath($BlueInvitationFile)
    $RedProfile = [IO.Path]::GetFullPath($RedInvitationFile)
}
New-Item -ItemType Directory -Force -Path $reportRoot | Out-Null
$sequences = @{ $BlueProfile = 0; $RedProfile = 0 }
$processes = @{}
$checks = [Collections.Generic.List[string]]::new()
$notes = [Collections.Generic.List[string]]::new()
$reportFile = Join-Path $reportRoot 'two-windows-clients.txt'
$started = Get-Date

function Read-State([string]$Role) {
    for ($attempt = 0; $attempt -lt 5; $attempt++) {
        try {
            $path = Join-Path $reportRoot "$Role-state.json"
            $stream = [IO.File]::Open($path, [IO.FileMode]::Open, [IO.FileAccess]::Read, ([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
            $reader = [IO.StreamReader]::new($stream)
            try { return ($reader.ReadToEnd() | ConvertFrom-Json) } finally { $reader.Dispose() }
        }
        catch { Start-Sleep -Milliseconds 15 }
    }
    return $null
}

function Send-Command([string]$Role, [string]$Action, [hashtable]$Values = @{}) {
    $sequences[$Role]++
    $command = @{ sequence = $sequences[$Role]; action = $Action }
    foreach ($key in $Values.Keys) { $command[$key] = $Values[$key] }
    $temporary = Join-Path $reportRoot "$Role-command.tmp"
    $target = Join-Path $reportRoot "$Role-command.json"
    [IO.File]::WriteAllText($temporary, ($command | ConvertTo-Json -Compress))
    $deadline = (Get-Date).AddSeconds(2)
    do {
        try { [IO.File]::Move($temporary, $target, $true); return }
        catch { if ((Get-Date) -ge $deadline) { throw }; Start-Sleep -Milliseconds 15 }
    } while ($true)
}

function Check([bool]$Condition, [string]$Description) {
    if (!$Condition) { throw $Description }
    $checks.Add("PASS $Description"); Write-Output "PASS $Description"
}

function Wait-For([scriptblock]$Condition, [string]$Description, [int]$Seconds = 20) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    do {
        if (& $Condition) { Check $true $Description; return }
        foreach ($observedRole in $roles) {
            if ($processes.ContainsKey($observedRole) -and $processes[$observedRole].HasExited) { throw "Client process ended unexpectedly: $observedRole" }
            $observedState = Read-State $observedRole
            if ($observedState -and $observedState.testError) { throw "Client test adapter error: $observedRole / $($observedState.testError)" }
        }
        Start-Sleep -Milliseconds 70
    } while ((Get-Date) -lt $deadline)
    $blue = Read-State $BlueProfile | ConvertTo-Json -Compress
    $red = Read-State $RedProfile | ConvertTo-Json -Compress
    throw "Timed out: $Description. Blue: $blue Red: $red"
}

function Start-Player([string]$Role) {
    $arguments = @('-pointpassOnlineTest', '-pointpassProfile', $Role,
        '-pointpassTestDirectory', $reportRoot,
        '-screen-width', '1280', '-screen-height', '720', '-screen-fullscreen', '0', '-force-d3d11',
        '-logFile', (Join-Path $reportRoot "$Role-player.log"))
    $invitation = $null
    try {
        if ($InvitationProvider) {
            # The provider must capture pinned SSH stdout in memory, verify its exit
            # status and return one code (or an empty line for a registered profile).
            # Neither provider output nor its exception details are logged here.
            try { $invitation = & $InvitationProvider $Role }
            catch { throw 'Private invitation provider failed; its response was not logged.' }
            if ($null -eq $invitation) { $invitation = '' }
            if ($invitation -isnot [string] -or ($invitation.Length -ne 0 -and $invitation -notmatch '^[A-Za-z0-9_-]{43}$')) {
                throw 'Private invitation provider returned an invalid response; its content was not logged.'
            }
            $arguments += '-pointpassInvitationStdin'
        }
        else { $arguments += @('-pointpassInvitationFile', $invitations[$Role]) }
        $start = [Diagnostics.ProcessStartInfo]::new()
        $start.FileName = $playerPath; $start.UseShellExecute = $false
        $start.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden; $start.CreateNoWindow = $true
        $start.RedirectStandardInput = [bool]$InvitationProvider
        foreach ($argument in $arguments) { $start.ArgumentList.Add($argument) }
        $process = [Diagnostics.Process]::new(); $process.StartInfo = $start
        if (!$process.Start()) { throw 'Could not start the test client.' }
        $processes[$Role] = $process
        if ($InvitationProvider) {
            try { $process.StandardInput.WriteLine($invitation); $process.StandardInput.Flush() }
            finally { $process.StandardInput.Close() }
        }
    }
    finally { $invitation = $null }
}

function Stop-Movement {
    foreach ($role in $roles) { Send-Command $role move @{ x = 0; y = 0; sprint = $false } }
    Start-Sleep -Milliseconds 350
}

function Position([string]$Role, $State) {
    if ($Role -eq $BlueProfile) { return $State.blue }
    return $State.red
}

function Drive-To([string]$Role, [double]$X, [double]$Y, [double]$Tolerance = 0.28, [int]$Seconds = 10) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    do {
        $state = Read-State $Role
        if (!$state -or !$state.playing) { Start-Sleep -Milliseconds 80; continue }
        $position = Position $Role $state
        $dx = $X - $position.x; $dy = $Y - $position.y
        $distance = [Math]::Sqrt($dx * $dx + $dy * $dy)
        if ($distance -le $Tolerance) { Send-Command $Role move @{ x = 0; y = 0 }; Start-Sleep -Milliseconds 300; return }
        # Ease approach to compensate for the intentionally interpolated client view.
        $magnitude = [Math]::Min(1.0, [Math]::Max(0.16, $distance * 0.55))
        Send-Command $Role move @{ x = [float]($dx / $distance * $magnitude); y = [float]($dy / $distance * $magnitude) }
        Start-Sleep -Milliseconds 100
    } while ((Get-Date) -lt $deadline)
    Send-Command $Role move @{ x = 0; y = 0 }
    throw "Could not approach waypoint using ordinary movement: $Role ($X,$Y)"
}

function Acquire-Ball([string]$Role, [int]$Slot, [int]$Seconds = 12) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    do {
        $state = Read-State $Role
        if (!$state -or !$state.playing) { Start-Sleep -Milliseconds 80; continue }
        if ($state.owner -eq $Slot) { Send-Command $Role move @{ x = 0; y = 0 }; Start-Sleep -Milliseconds 350; Check $true "$Role physically collects the shared ball"; return }
        $position = Position $Role $state
        $dx = $state.ball.x - $position.x; $dy = $state.ball.y - $position.y
        $distance = [Math]::Sqrt($dx * $dx + $dy * $dy)
        if ($distance -gt 0.01) {
            $magnitude = [Math]::Min(1.0, [Math]::Max(0.16, $distance * 0.55))
            Send-Command $Role move @{ x = [float]($dx / $distance * $magnitude); y = [float]($dy / $distance * $magnitude) }
        }
        Start-Sleep -Milliseconds 80
    } while ((Get-Date) -lt $deadline)
    Send-Command $Role move @{ x = 0; y = 0 }
    throw "Could not collect the ball using normal player movement: $Role"
}

function Aim-At([string]$Role, [double]$X, [double]$Y) {
    $state = Read-State $Role; $position = Position $Role $state
    Send-Command $Role aim @{ x = [float]($X - $position.x); y = [float]($Y - $position.y) }
    Start-Sleep -Milliseconds 400
}

function Charged-Kick([string]$Role, [string]$Type, [float]$Curve = 0) {
    Send-Command $Role ($Type + 'Down')
    Wait-For { (Read-State $Role).charge -ge 0.9 } "$Role receives authoritative charged $Type power" 6
    Send-Command $Role ($Type + 'Up') @{ curve = $Curve }
}

function Wait-Restart([int]$PreviousReset, [int]$BlueScore, [int]$RedScore) {
    Wait-For {
        $a = Read-State $BlueProfile; $b = Read-State $RedProfile
        $a.playing -and $b.playing -and $a.resetVersion -gt $PreviousReset -and $b.resetVersion -eq $a.resetVersion -and
            $a.blueScore -eq $BlueScore -and $b.blueScore -eq $BlueScore -and $a.redScore -eq $RedScore -and $b.redScore -eq $RedScore
    } 'Both clients observe the same kickoff and retained scores' 16
}

try {
    foreach ($role in $roles) {
        foreach ($suffix in @('-command.json', '-state.json', '-snapshots.csv')) {
            $path = Join-Path $reportRoot ($role + $suffix)
            if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path }
        }
    }
    Start-Player $BlueProfile
    Wait-For { $s = Read-State $BlueProfile; $s.hasSession -and !$s.busy -and $s.connected } 'First Windows client connects securely and waits for its opponent' 35
    Check (!(Read-State $BlueProfile).hasWorld) 'No local one-player match is independently simulated while waiting'
    Start-Player $RedProfile
    Wait-For { (Read-State $BlueProfile).playing -and (Read-State $RedProfile).playing } 'Two Windows clients reach the Linux-authoritative kickoff' 35
    Check ((Read-State $BlueProfile).localSlot -eq 0 -and (Read-State $RedProfile).localSlot -eq 1) 'Each authenticated client controls one opposing-team slot'
    foreach ($role in $roles) {
        $state = Read-State $role
        Check ($state.players -eq 2 -and $state.simulatedBodies -eq 0) "$role has two rendered footballers and no independently simulated ball/player physics"
    }
    Wait-For { $a = Read-State $BlueProfile; $b = Read-State $RedProfile; $a.hasPing -and $b.hasPing -and $a.packetsReceived -gt 0 -and $b.packetsReceived -gt 0 } 'Both clients measure real transport packets and round-trip latency'
    Wait-For { (Read-State $BlueProfile).snapshotHz -ge 20 -and (Read-State $RedProfile).snapshotHz -ge 20 } 'Both clients receive the 30Hz snapshot stream'

    $initial = Read-State $BlueProfile
    Send-Command $BlueProfile move @{ x = 0; y = 1 }
    Send-Command $RedProfile move @{ x = 0; y = -1 }
    Start-Sleep -Milliseconds 650; Stop-Movement
    $moved = Read-State $BlueProfile
    Check (($moved.blue.y - $initial.blue.y) -gt 1.5 -and ($initial.red.y - $moved.red.y) -gt 1.5) 'Blue and Red move independently through server-authoritative inputs'

    $walkStart = (Read-State $BlueProfile).blue.x
    Send-Command $BlueProfile move @{ x = 1; y = 0; sprint = $false }
    Start-Sleep -Milliseconds 550
    Send-Command $BlueProfile move @{ x = 0; y = 0 }; Start-Sleep -Milliseconds 350
    $walkDistance = (Read-State $BlueProfile).blue.x - $walkStart
    $sprintStart = (Read-State $BlueProfile).blue.x
    Send-Command $BlueProfile move @{ x = -1; y = 0; sprint = $true }
    Start-Sleep -Milliseconds 550
    Send-Command $BlueProfile move @{ x = 0; y = 0; sprint = $false }; Start-Sleep -Milliseconds 350
    $sprintDistance = $sprintStart - (Read-State $BlueProfile).blue.x
    Check ($sprintDistance -gt $walkDistance * 1.1) 'Held sprint increases authoritative movement speed'

    Drive-To $RedProfile 4 0
    Aim-At $RedProfile 0 0
    Acquire-Ball $BlueProfile 0
    $receiver = (Read-State $BlueProfile).red
    Aim-At $BlueProfile $receiver.x $receiver.y
    $beforePass = (Read-State $BlueProfile).blueKicks
    Charged-Kick $BlueProfile pass
    Wait-For { $s = Read-State $BlueProfile; $s.blueKicks -gt $beforePass -and $s.owner -eq 1 } 'A charged Blue pass physically reaches Red and transfers shared possession' 10
    Check ((Read-State $RedProfile).owner -eq 1) 'Both views agree Red owns the received pass'

    $receiver = (Read-State $RedProfile).blue
    Aim-At $RedProfile $receiver.x $receiver.y
    $beforePass = (Read-State $RedProfile).redKicks
    Charged-Kick $RedProfile pass
    Wait-For { $s = Read-State $BlueProfile; $s.redKicks -gt $beforePass -and $s.owner -eq 0 } 'A charged Red pass physically returns the ball to Blue' 10
    # LatestState intentionally leads the100ms interpolated possession used by
    # input eligibility. A real player sees the ball before pressing the button.
    Start-Sleep -Milliseconds 350

    # Cancellation is checked using authoritative charge and retained possession.
    Send-Command $BlueProfile shotDown
    Wait-For { (Read-State $BlueProfile).charge -gt 0.3 } 'Blue starts a server-side shot charge'
    $beforeCancel = (Read-State $BlueProfile).blueKicks
    Send-Command $BlueProfile cancel
    Wait-For { $s = Read-State $BlueProfile; $s.charging -eq 0 -and $s.owner -eq 0 } 'Cancel clears charge while retaining possession'
    Check ((Read-State $BlueProfile).blueKicks -eq $beforeCancel) 'Cancellation does not execute a kick'

    # The pass receiver was deliberately in the shooting lane. Clear that defender
    # through ordinary movement before checking a goal, rather than treating a
    # legitimate physical block/catch as a networking or scoring failure.
    Drive-To $RedProfile 4 3
    $beforeShot = Read-State $BlueProfile
    Aim-At $BlueProfile 10.3 0
    Charged-Kick $BlueProfile shot 0.16
    Wait-For { $s = Read-State $BlueProfile; $s.blueKicks -gt $beforeShot.blueKicks -and [Math]::Abs($s.ballCurve) -gt 0.03 } 'Blue executes a charged physical curved shot' 6
    Wait-For { $a = Read-State $BlueProfile; $b = Read-State $RedProfile; $a.blueScore -eq 1 -and $b.blueScore -eq 1 } 'Both clients receive the same Blue goal' 10
    Wait-Restart $beforeShot.resetVersion 1 0

    Drive-To $BlueProfile -4 3
    Acquire-Ball $RedProfile 1
    $beforeShot = Read-State $RedProfile
    Aim-At $RedProfile -10.3 0
    Charged-Kick $RedProfile shot -0.16
    Wait-For { $s = Read-State $RedProfile; $s.redKicks -gt $beforeShot.redKicks -and [Math]::Abs($s.ballCurve) -gt 0.03 } 'Red executes a charged physical curved shot' 6
    Wait-For { $a = Read-State $BlueProfile; $b = Read-State $RedProfile; $a.redScore -eq 1 -and $b.redScore -eq 1 } 'Both clients receive the same Red goal' 10
    Wait-Restart $beforeShot.resetVersion 1 1

    $beforeDisconnect = Read-State $BlueProfile
    Send-Command $RedProfile leave
    Wait-For { $s = Read-State $RedProfile; !$s.hasSession -and !$s.busy } 'Red cleanly leaves its encrypted session'
    Wait-For { !(Read-State $BlueProfile).playing } 'Authoritative match pauses for the disconnected participant'
    $pausedClock = (Read-State $BlueProfile).clock
    Start-Sleep -Milliseconds 900
    Check ([Math]::Abs((Read-State $BlueProfile).clock - $pausedClock) -lt 0.1) 'Match clock remains paused while awaiting reconnection'
    Send-Command $RedProfile join
    Wait-For { (Read-State $RedProfile).playing -and (Read-State $BlueProfile).playing } 'The same guest rejoins and resumes its authoritative match within30seconds' 20
    Check ((Read-State $RedProfile).localSlot -eq 1 -and (Read-State $RedProfile).blueScore -eq 1 -and (Read-State $RedProfile).redScore -eq 1) 'Reconnection retains team ownership and both scores'

    if ((Read-State $BlueProfile).rawValidationHook) {
        $beforeMalformed = (Read-State $BlueProfile).stateSequence
        Send-Command $BlueProfile rawMalformed @{ data = [Convert]::ToBase64String([byte[]]@(1, 2, 3)) }
        Wait-For { (Read-State $BlueProfile).stateSequence -gt $beforeMalformed + 10 -and (Read-State $RedProfile).playing } 'A malformed command does not crash or disrupt either client'
        $notes.Add('Malformed-command rejection counters must also be checked on the trusted server; continued playback alone is not proof of rejection.')
    }
    else { $notes.Add('PENDING Server malformed-command integration: this build has no development validation input hook.') }

    foreach ($role in $roles) { Send-Command $role capture }
    Start-Sleep -Milliseconds 400

    if ($FullMatch) {
        # Establish a non-draw score so the normal two90-second halves have a result.
        Drive-To $RedProfile 4 3
        Acquire-Ball $BlueProfile 0
        Aim-At $BlueProfile 10.3 0
        $beforeGoal = Read-State $BlueProfile
        Charged-Kick $BlueProfile shot 0
        Wait-For { $a = Read-State $BlueProfile; $b = Read-State $RedProfile; $a.blueScore -eq 2 -and $b.blueScore -eq 2 } 'Both clients observe Blue taking the lead' 10
        Wait-Restart $beforeGoal.resetVersion 2 1
        Drive-To $BlueProfile -5 3
        Drive-To $RedProfile 5 3
        Stop-Movement
        Wait-For { $a = Read-State $BlueProfile; $b = Read-State $RedProfile; $a.fullTime -and $b.fullTime -and $a.blueScore -eq 2 -and $b.redScore -eq 1 } 'The normal-duration authoritative match reaches the same full-time result on both clients' 230
        Wait-For { !(Read-State $BlueProfile).hasSession -and !(Read-State $RedProfile).hasSession } 'Both completed sessions return cleanly to the menu' 15
        $notes.Add('Verify this real result exists once in PostgreSQL/history. Client agreement alone does not establish result persistence.')
    }
    else { $notes.Add('PENDING Full normal-duration match/result test: rerun with -FullMatch. Match clocks are never accelerated by this harness.') }

    # Compare exact shared server-sequence records, avoiding interpolation/arrival differences.
    $blueRecords = @{}
    foreach ($row in (Import-Csv -LiteralPath (Join-Path $reportRoot "$BlueProfile-snapshots.csv"))) { $blueRecords[$row.sequence] = $row }
    $matching = 0
    foreach ($row in (Import-Csv -LiteralPath (Join-Path $reportRoot "$RedProfile-snapshots.csv"))) {
        if ($blueRecords.ContainsKey($row.sequence)) {
            $other = $blueRecords[$row.sequence]
            foreach ($field in @('reset','blueScore','redScore','clock','playing','owner','ballX','ballY','ballVx','ballVy','blueX','blueY','redX','redY','blueKicks','redKicks','curve')) {
                if ($row.$field -ne $other.$field) { throw "Authoritative snapshot mismatch at sequence $($row.sequence): $field" }
            }
            $matching++
        }
    }
    Check ($matching -ge 50) "$matching shared authoritative snapshots agree exactly across both actual Windows clients"
    $notes.Add('Transport security/certificate verification, API authorization/tickets/capacity, worker idle/active resources, second-PC tests and device input need their separate recorded checks.')
    $checks.Add("COMPLETE $($checks.Count) actual two-client gameplay checks; elapsed $([Math]::Round(((Get-Date)-$started).TotalSeconds,1)) seconds.")
}
catch {
    $checks.Add('FAIL ' + $_.Exception.Message)
    throw
}
finally {
    if (!$KeepClients) {
        foreach ($role in $roles) {
            if ($processes.ContainsKey($role) -and !$processes[$role].HasExited) {
                try { Send-Command $role quit } catch { }
            }
        }
        foreach ($role in $roles) {
            if ($processes.ContainsKey($role) -and !$processes[$role].HasExited) {
                if (!$processes[$role].WaitForExit(3000)) { Stop-Process -Id $processes[$role].Id }
            }
        }
    }
    [IO.File]::WriteAllLines($reportFile, @('Actual Windows clients through the dedicated internet server', "Started UTC: $($started.ToUniversalTime().ToString('O'))") + $checks.ToArray() + $notes.ToArray())
    Write-Output "Report: $reportFile"
}
