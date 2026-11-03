param(
    [string]$Player="$PSScriptRoot/../../Builds/Multiplayer/PointPass.exe",
    [int]$Port=7787,
    [int]$LatencyMs=0,
    [string]$ReportDirectory="$PSScriptRoot/../../Logs/PlayerFeatures/Multiplayer",
    [string]$Python=(Join-Path $env:USERPROFILE '.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe')
)
$ErrorActionPreference='Stop'
$playerPath=(Resolve-Path -LiteralPath $Player).Path
$reportRoot=[IO.Path]::GetFullPath($ReportDirectory)
New-Item -ItemType Directory -Path $reportRoot -Force | Out-Null
$sequence=@{host=0;client=0};$players=@{};$checks=[Collections.Generic.List[string]]::new();$proxy=$null
$clientPort=if($LatencyMs -gt 0){$Port+1}else{$Port}
function Send([string]$role,[string]$action,[hashtable]$values=@{}) {
    $sequence[$role]++;$value=@{sequence=$sequence[$role];action=$action}
    foreach($key in $values.Keys){$value[$key]=$values[$key]}
    $temp=Join-Path $reportRoot "$role-command.tmp"
    $value|ConvertTo-Json -Compress|Set-Content -LiteralPath $temp
    $deadline=(Get-Date).AddSeconds(4)
    do {
        try {[IO.File]::Move($temp,(Join-Path $reportRoot "$role-command.json"),$true);break}
        catch {if((Get-Date)-ge$deadline){throw};Start-Sleep -Milliseconds 20}
    }while($true)
    if($action-ne'quit') {
        do {if((State $role).appliedCommand-ge$sequence[$role]){return};Start-Sleep -Milliseconds 20}while((Get-Date)-lt$deadline)
        throw "Command acknowledgement timed out: $role $action"
    }
}
function State([string]$role) {
    try { $stream=[IO.File]::Open((Join-Path $reportRoot "$role-state.json"),'Open','Read',([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete));$reader=[IO.StreamReader]::new($stream)
        try{return ($reader.ReadToEnd()|ConvertFrom-Json)}finally{$reader.Dispose()} }catch{return $null}
}
function Check([bool]$okay,[string]$label) {if(!$okay){throw $label};$checks.Add("PASS $label");Write-Output "PASS $label"}
function Wait([scriptblock]$condition,[string]$label,[int]$seconds=20) {
    $deadline=(Get-Date).AddSeconds($seconds)
    do {if(&$condition){Check $true $label;return};Start-Sleep -Milliseconds 70}while((Get-Date)-lt $deadline)
    throw "Timed out: $label. Host: $(State host|ConvertTo-Json -Compress) Client: $(State client|ConvertTo-Json -Compress)"
}
function Start-Player([string]$role) {
    $mode=if($role-eq'host'){'--pp-host'}else{'--pp-client'};$listen=if($role-eq'host'){$Port}else{$clientPort}
    $arguments=@('--pp-network',$mode,'--pp-ip','127.0.0.1','--pp-port',"$listen",'--pp-test','--pp-test-role',$role,'--pp-test-dir',('"'+$reportRoot+'"'),'-screen-width','1280','-screen-height','720','-screen-fullscreen','0','-force-d3d11','-logFile',('"'+(Join-Path $reportRoot "$role-player.log")+'"'))
    $players[$role]=Start-Process -FilePath $playerPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
}
function Stop-Move {Send host move;Send client move;Start-Sleep -Milliseconds 220}
function Fixture([hashtable]$positions) {Stop-Move;Send host fixture $positions;Start-Sleep -Milliseconds (500+$LatencyMs*3)}
try {
    foreach($role in @('host','client')) {foreach($suffix in @('-command.json','-state.json','-snapshots.csv')) {$old=Join-Path $reportRoot ($role+$suffix);if(Test-Path -LiteralPath $old){Remove-Item -LiteralPath $old}}}
    if($LatencyMs-gt0) {
        $proxy=Start-Process -FilePath $Python -WindowStyle Hidden -PassThru -RedirectStandardError (Join-Path $reportRoot 'proxy-errors.log') -RedirectStandardOutput (Join-Path $reportRoot 'proxy-output.log') -ArgumentList @('-u',('"'+(Join-Path $PSScriptRoot 'LoopbackLatencyProxy.py')+'"'),'--listen',"$clientPort",'--server',"$Port",'--delay',"$LatencyMs",'--jitter','15','--loss','.01','--report',('"'+(Join-Path $reportRoot 'impairment.json')+'"'))
        Start-Sleep -Milliseconds 300
    }
    Start-Player host;Wait {(State host).status-like'Hosting*'} 'Host listens'
    Start-Player client;Wait {(State host).playing-and(State client).playing} 'Two real standalone players connect'
    Check ((State client).simulatedBodies-eq0) 'Client does not simulate its own ball or players'
    Wait {(State client).hasPing} 'Client measures live RTT'
    if($LatencyMs-gt0){Check ((State client).pingMs-ge($LatencyMs*1.2)) 'Transport impairment produces observable network delay'}

    Fixture @{blueX=0;blueY=0;redX=1.45;redY=0;ballX=.62;ballY=0}
    Wait {(State host).owner-eq0} 'Close foot contact collects possession'
    $before=(State host).redTackles
    Send client aimTap @{x=-1;y=0};Send client tackle
    Wait {(State host).redTackles-gt$before-and(State client).redTackles-gt$before} 'Remote tackle is resolved and replicated'
    Check ((State host).redTackleResult-eq1-and(State client).redTackleResult-eq1) 'Both clients report the same successful tackle'
    Wait {(State host).owner-ne0} 'Successful tackle physically releases the previous owner'

    Fixture @{blueX=0;blueY=0;redX=4;redY=0;ballX=.62;ballY=0}
    Wait {(State host).owner-eq0} 'Owner recollects through contact'
    $before=(State host).redTackles
    Send client aimTap @{x=-1;y=0};Send client tackleBurst
    Wait {(State host).redTackles-gt$before} 'Out-of-range attempt resolves'
    Check ((State host).redTackleResult-eq2-and(State host).owner-eq0) 'Miss leaves the opposing owner in possession'
    Start-Sleep -Milliseconds 300
    Check ((State host).redTackles-eq($before+1)) 'Repeated tackle commands cannot bypass recovery'

    Fixture @{blueX=0;blueY=0;redX=1.45;redY=0;ballX=.62;ballY=0}
    Wait {(State host).owner-eq0} 'Positioning fixture collects normally'
    $before=(State host).redTackles
    Send client aimTap @{x=1;y=0};Send client tackle
    Wait {(State host).redTackles-gt$before} 'Facing-away tackle resolves'
    Check ((State host).redTackleResult-eq2-and(State host).owner-eq0) 'Facing away cannot win possession'

    Fixture @{blueX=-2;blueY=-2;redX=3;redY=1;ballX=0;ballY=4}
    Send host resetProfile;Send client resetProfile
    $directions=@(@{x=1;y=0;sprint=$true},@{x=0;y=1;sprint=$true},@{x=-1;y=0;sprint=$true},@{x=0;y=-1;sprint=$true})
    for($i=0;$i-lt10;$i++){
        Send host move $directions[$i%4];Send client move $directions[($i+2)%4];Start-Sleep -Milliseconds 650
        if((State host).blueExhausted-and(State host).redExhausted){break}
    }
    Wait {(State host).blueExhausted-and(State host).redExhausted-and(State client).redExhausted} 'Both sprint budgets deplete under host authority'
    $s=State host
    Check ($s.blueStamina-lt.3-and$s.redStamina-lt.3) 'Both budgets reach exhaustion before meaningful recovery'
    Check ([Math]::Sqrt($s.blueVelocity.x*$s.blueVelocity.x+$s.blueVelocity.y*$s.blueVelocity.y)-le4.95) 'Exhaustion returns the player to normal running speed'
    Stop-Move
    Wait {(State host).blueStamina-gt.97-and(State host).redStamina-gt.97-and!(State host).redExhausted} 'Stamina refills after release and the brief recovery delay' 8

    Fixture @{blueX=0;blueY=0;redX=1.45;redY=0;ballX=.62;ballY=0}
    Wait {(State host).owner-eq0} 'Touch tackle fixture obtains the ball'
    $before=(State host).redTackles
    Send client aimTap @{x=-1;y=0}
    Send client touchTackleTap
    Wait {(State host).redTackles-gt$before} 'Touch tackle travels through HumanPlayerInput and the network command path'
    Send client humanOff
    Fixture @{blueX=6;blueY=0;redX=-5;redY=3;ballX=6.65;ballY=0}
    Wait {(State host).owner-eq0} 'Shot fixture collects the close ball'
    Send host shotDown;Start-Sleep -Milliseconds 900;Send host shotUp
    Wait {(State host).blueScore-eq1-and(State client).blueScore-eq1} 'Physical goal and score agree on both clients'
    Start-Sleep -Milliseconds 600
    (State host)|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $reportRoot 'busy-host-profile.json')
    (State client)|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $reportRoot 'busy-client-profile.json')
    # Readbacks and canvas-mode changes are test-only work, outside the gameplay timing sample.
    Send host capture;Send client capture
    Wait {(State host).playing-and(State client).playing} 'Goal celebration returns both players to the match'

    $hostRows=@{};foreach($row in (Import-Csv -LiteralPath (Join-Path $reportRoot 'host-snapshots.csv'))){$hostRows[$row.sequence]=$row}
    $matched=0
    foreach($row in (Import-Csv -LiteralPath (Join-Path $reportRoot 'client-snapshots.csv'))) {
        if(!$hostRows.ContainsKey($row.sequence)){continue};$expected=$hostRows[$row.sequence]
        foreach($key in $row.PSObject.Properties.Name){if($row.$key-ne$expected.$key){throw "Snapshot $($row.sequence) differs at $key"}};$matched++
    }
    Check ($matched-ge100) "Matching authoritative position, possession, stamina and tackle state in $matched snapshots"
    Send client disconnect;Wait {!(State client).hasSession-and(State host).players-eq0} 'Disconnect clears the shared world'
    Send client join @{slot=$clientPort};Wait {(State host).playing-and(State client).playing} 'Client rejoins with fresh ownership and stamina'
    Send host disconnect;Wait {!(State client).hasSession-and!(State host).hasSession} 'Host departure cleanly ends the client session'
    foreach($role in @('host','client')){Check (!(Select-String -LiteralPath (Join-Path $reportRoot "$role-player.log") -Pattern 'Exception:|NullReferenceException|\[Error\]' -Quiet)) "$role has no runtime exceptions"}
    $checks.Add('PASS TACKLING AND STAMINA MULTIPLAYER COMPLETE')
}catch{$checks.Add('FAIL '+$_.Exception.Message);throw}
finally {
    foreach($role in $players.Keys){if(!$players[$role].HasExited){Send $role quit}}
    Start-Sleep -Milliseconds 800
    foreach($process in $players.Values){if(!$process.HasExited){Stop-Process -Id $process.Id}}
    if($proxy-and!$proxy.HasExited){Stop-Process -Id $proxy.Id}
    $checks|Set-Content -LiteralPath (Join-Path $reportRoot 'tests.txt')
}
