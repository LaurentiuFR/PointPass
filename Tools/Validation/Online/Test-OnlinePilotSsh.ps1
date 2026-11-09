[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SshKeyPath,
    [Parameter(Mandatory)][string]$KnownHostsFile,
    [ValidateSet('62.238.115.207')][string]$ServerAddress = '62.238.115.207',
    [string]$Player = "$PSScriptRoot/../../../Builds/OnlinePilot/Client/PointPass.exe",
    [string]$BlueProfile,
    [string]$RedProfile,
    [string]$ReportDirectory = "$PSScriptRoot/../../../Logs/OnlinePilotVerification/Clients",
    [switch]$FullMatch,
    [switch]$ReuseProfiles
)

$ErrorActionPreference = 'Stop'
$expectedFingerprint = 'SHA256:cinqGMWTldXWlxt9FWNy62f5GcFvvb+00N1/ti71Tv0'
$runId = [Guid]::NewGuid().ToString('N').Substring(0, 12)
if (!$BlueProfile) { $BlueProfile = if ($ReuseProfiles) { 'pilot-blue' } else { "pilot-b-$runId" } }
if (!$RedProfile) { $RedProfile = if ($ReuseProfiles) { 'pilot-red' } else { "pilot-r-$runId" } }
foreach ($profile in @($BlueProfile, $RedProfile)) {
    if ($profile -cnotmatch '\A[A-Za-z0-9-]{1,24}\z') { throw 'Invalid pilot profile name.' }
}
if ($BlueProfile -eq $RedProfile) { throw 'Choose two different pilot profiles.' }

$playerPath = (Get-Item -LiteralPath $Player).FullName
$identityPath = (Get-Item -LiteralPath $SshKeyPath).FullName
$pinPath = (Get-Item -LiteralPath $KnownHostsFile).FullName
$ssh = (Get-Command ssh.exe -CommandType Application).Source
$sshKeygen = (Get-Command ssh-keygen.exe -CommandType Application).Source

function Start-PrivateProcess([string]$Executable, [string[]]$Arguments) {
    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $Executable
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    $startInfo.RedirectStandardInput = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.StandardInputEncoding = [Text.UTF8Encoding]::new($false)
    foreach ($argument in $Arguments) { $startInfo.ArgumentList.Add($argument) }
    $child = [Diagnostics.Process]::new()
    $child.StartInfo = $startInfo
    if (!$child.Start()) {
        $child.Dispose()
        throw 'Could not start the private child process.'
    }
    return $child
}

function Invoke-PrivateProcess([string]$Executable, [string[]]$Arguments, [string]$InputText = '') {
    $child = Start-PrivateProcess $Executable $Arguments
    try {
        $stdout = $child.StandardOutput.ReadToEndAsync()
        $stderr = $child.StandardError.ReadToEndAsync()
        if ($InputText.Length) { $child.StandardInput.Write($InputText) }
        $child.StandardInput.Close()
        if (!$child.WaitForExit(20000) -or !$stdout.Wait(2000) -or !$stderr.Wait(2000)) {
            throw 'A private child process timed out.'
        }
        return [pscustomobject]@{ ExitCode = $child.ExitCode; Output = $stdout.GetAwaiter().GetResult() }
    }
    finally {
        if (!$child.HasExited) { $child.Kill() }
        $child.Dispose()
    }
}

function Assert-HostPin([string]$FingerprintOutput) {
    $matchesForEd25519 = [regex]::Matches($FingerprintOutput, '(?m)^\S+[ \t]+ED25519[ \t]+(SHA256:[A-Za-z0-9+/=]+)[ \t]*\r?$')
    if ($matchesForEd25519.Count -ne 1 -or $matchesForEd25519[0].Groups[1].Value -cne $expectedFingerprint) {
        throw 'The known_hosts file must contain exactly the required Ed25519 host pin for this server.'
    }
}

# FileShare.Read lets OpenSSH read the pin and prevents writes or replacement during the run.
$pinFile = [IO.File]::OpenRead($pinPath)
$lease = $null
$invitationHashes = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::Ordinal)
try {
    $pinLookup = Invoke-PrivateProcess $sshKeygen @('-F', $ServerAddress, '-l', '-E', 'sha256', '-f', $pinPath)
    if ($pinLookup.ExitCode -ne 0) { throw 'The required server entry could not be read from known_hosts.' }
    Assert-HostPin $pinLookup.Output

    $knownHostsOption = 'UserKnownHostsFile="' + $pinPath.Replace('\', '/') + '"'
    $sshArguments = @('-F', 'none', '-T', '-i', $identityPath,
        '-o', 'BatchMode=yes', '-o', 'IdentitiesOnly=yes', '-o', 'StrictHostKeyChecking=yes',
        '-o', 'HostKeyAlgorithms=ssh-ed25519', '-o', 'KexAlgorithms=curve25519-sha256',
        '-o', $knownHostsOption, '-o', 'GlobalKnownHostsFile=NUL', '-o', 'UpdateHostKeys=no',
        '-o', 'ConnectTimeout=10', '-o', 'ServerAliveInterval=15', '-o', 'ServerAliveCountMax=2',
        "root@$ServerAddress")
    $leaseCommand = @'
set -eu
exec 9>>/run/lock/pointpass-pilot-verification.lock
flock -n 9 || exit 73
systemctl is-active --quiet pointpass-api.service || exit 76
systemctl is-active --quiet pointpass-match.service || exit 76
for outbox in /var/lib/pointpass-match/pending-result.json /var/lib/pointpass-match/pending-result.json.tmp; do
    if [ -e "$outbox" ] || [ -L "$outbox" ]; then exit 77; fi
done
open_matches=$(runuser -u postgres -- env PGCONNECT_TIMEOUT=5 PGOPTIONS='-c statement_timeout=5000' psql -XAtqw -v ON_ERROR_STOP=1 -d pointpass -c "SELECT count(*) FROM matches WHERE state IN ('waiting','active')")
[ "$open_matches" = 0 ] || exit 75
worker_invocation=$(systemctl show -p InvocationID --value pointpass-match.service)
# Use recent samples from this worker instance when available. A fresh worker can proceed before its first sample.
recent_peers=$(journalctl -u pointpass-match.service "_SYSTEMD_INVOCATION_ID=$worker_invocation" --since '1 minute ago' -o cat --no-pager | sed -n 's/.*\[PointPass Worker\] metrics peers=\([0-9][0-9]*\) .*/\1/p' | tail -n 1)
[ -z "$recent_peers" ] || [ "$recent_peers" = 0 ] || exit 78
printf '%s\n' POINTPASS_READY
cat >/dev/null
'@
    $lease = Start-PrivateProcess $ssh ($sshArguments + $leaseCommand.Replace("`r", ''))
    $leaseErrors = $lease.StandardError.ReadToEndAsync()
    $ready = $lease.StandardOutput.ReadLineAsync()
    if (!$ready.Wait(20000)) { throw 'The pilot lock and idle-room check timed out.' }
    if ($ready.GetAwaiter().GetResult() -cne 'POINTPASS_READY') {
        if ($lease.WaitForExit(1000)) {
            if ($lease.ExitCode -eq 73) { throw 'Another pilot verification run holds the server lock.' }
            if ($lease.ExitCode -eq 75) { throw 'A waiting or active match already exists.' }
            if ($lease.ExitCode -eq 76) { throw 'The API and match worker must both be active.' }
            if ($lease.ExitCode -eq 77) { throw 'The match worker has a pending result outbox.' }
            if ($lease.ExitCode -eq 78) { throw 'The latest worker sample reports connected peers.' }
        }
        throw 'The server lock and idle-room check failed.'
    }
    $leaseOutput = $lease.StandardOutput.ReadToEndAsync()

    $provider = {
        param([string]$Role)
        if ($lease.HasExited) { throw 'The pilot verification lock was lost.' }
        if ($Role -cnotin @($BlueProfile, $RedProfile)) { throw 'Unexpected invitation profile.' }
        if ($ReuseProfiles) { return '' }
        if ($invitationHashes.ContainsKey($Role)) { throw 'An invitation was already issued for this profile.' }

        $response = $null
        $code = $null
        $bytes = $null
        $hash = $null
        try {
            $response = Invoke-PrivateProcess $ssh ($sshArguments + "/opt/pointpass/bin/create-invite.sh --pipe $Role")
            if ($response.ExitCode -ne 0) { throw 'The private invitation command failed.' }
            if ($response.Output -cnotmatch '\A[A-Za-z0-9_-]{43}\r?\n?\z') { throw 'The private invitation response was invalid.' }
            $code = $response.Output.TrimEnd("`r", "`n")
            $bytes = [Text.Encoding]::ASCII.GetBytes($code)
            $hash = [Security.Cryptography.SHA256]::HashData($bytes)
            $invitationHashes.Add($Role, [Convert]::ToHexString($hash).ToLowerInvariant())
            if ($lease.HasExited) { throw 'The pilot verification lock was lost during invitation creation.' }
            return $code
        }
        finally {
            if ($null -ne $bytes) { [Array]::Clear($bytes, 0, $bytes.Length) }
            if ($null -ne $hash) { [Array]::Clear($hash, 0, $hash.Length) }
            $code = $null
            $response = $null
        }
    }

    $gameplayFailure = $null
    try {
        & "$PSScriptRoot/Test-OnlinePilot.ps1" -Player $playerPath -ReportDirectory $ReportDirectory `
            -BlueProfile $BlueProfile -RedProfile $RedProfile -InvitationProvider $provider -FullMatch:$FullMatch
        if ($lease.HasExited) { throw 'The pilot verification lock was lost before the match test completed.' }
    }
    catch { $gameplayFailure = $_ }

    if ($invitationHashes.Count) {
        try {
            $hashSql = foreach ($digest in $invitationHashes.Values) {
                if ($digest -cnotmatch '\A[0-9a-f]{64}\z') { throw 'Invalid private audit digest.' }
                "decode('$digest', 'hex')"
            }
            $sql = "SELECT count(*), count(*) FILTER (WHERE used = 1 AND maximum_uses = 1) FROM invitations WHERE code_hash IN ($($hashSql -join ', '));`n"
            # This session suppresses statement logging and accepts its SQL through stdin.
            $auditCommand = "runuser -u postgres -- env PGCONNECT_TIMEOUT=5 PGOPTIONS='-c default_transaction_read_only=on -c statement_timeout=5000 -c log_statement=none -c log_min_duration_statement=-1 -c log_duration=off -c log_min_error_statement=panic' psql -XAtqw -v ON_ERROR_STOP=1 -d pointpass"
            $audit = Invoke-PrivateProcess $ssh ($sshArguments + $auditCommand) $sql
            $counts = [regex]::Match($audit.Output, '\A([0-9]+)\|([0-9]+)\s*\z')
            if ($audit.ExitCode -ne 0 -or !$counts.Success) { throw 'Could not verify invitation consumption.' }
            $found = [int]$counts.Groups[1].Value
            $consumed = [int]$counts.Groups[2].Value
            Write-Output "Invitation records found $found. Consumed $consumed of $($invitationHashes.Count)."
            if ($found -ne $invitationHashes.Count -or $consumed -ne $invitationHashes.Count) { throw 'The invitation consumption check did not pass.' }
        }
        catch {
            if (!$gameplayFailure) { throw }
            Write-Warning 'Invitation consumption could not be confirmed after the gameplay failure.'
        }
        finally {
            $sql = $null
            $hashSql = $null
            $digest = $null
        }
    }
    if ($gameplayFailure) { throw $gameplayFailure }
}
finally {
    $invitationHashes.Clear()
    try {
        if ($null -ne $lease) {
            try { $lease.StandardInput.Close() } catch [IO.IOException] { }
            try {
                if (!$lease.WaitForExit(5000)) { $lease.Kill() }
            }
            finally { $lease.Dispose() }
        }
    }
    finally { $pinFile.Dispose() }
}
