# Testwerkzeug fuer ein virtuelles Display (scrcpy --new-display): funktioniert auch bei gesperrtem Telefon.
#   1) scrcpy dauerhaft starten (z.B. als Hintergrundjob):
#        $env:ADB = <adb.exe>; scrcpy --new-display=2424x1080/420 --start-app=de.domezosware.glamourgames.unityandroid --no-audio
#   2) . .\Tools\Android\vd.ps1 ; Vd-Init ; Vd-Shot name ; Vd-Tap 800 450 ; Vd-Back ; Vd-Perf
. "$PSScriptRoot\adb.ps1"
$global:ShotDir = if ($env:GLAMOUR_SHOTS) { $env:GLAMOUR_SHOTS } else { 'E:\Claude Projekte\glamour_shots' }
$global:Vd = $null

function Vd-Init {
    $sf = ((Adb shell 'dumpsys SurfaceFlinger --display-id') | Select-String 'scrcpy' | Select-Object -First 1).Line -replace '^Display (\d+).*', '$1'
    $disp = ((Adb shell 'dumpsys display') | Select-String '"scrcpy", displayId (\d+)' | Select-Object -First 1).Matches.Groups[1].Value
    if (-not $sf -or -not $disp) { throw 'Kein scrcpy-Display gefunden (scrcpy laeuft nicht?)' }
    # Standard: 2424x1080 ohne Aussparung -> Designraum 1600x900 mit Massstab 1.2, links/rechts je 252 px Rand
    $global:Vd = [pscustomobject]@{ Sf = $sf; Id = $disp; Scale = 1.2; Ox = 252.0; Oy = 0.0 }
    $global:Vd
}
function Vd-View { $v = Get-View; if ($v) { $global:Vd.Scale = $v.Scale; $global:Vd.Ox = $v.Ox; $global:Vd.Oy = $v.Oy } }
function Vd-Shot([string]$Name) { cmd /c "`"$global:Adb`" exec-out screencap -d $($global:Vd.Sf) -p > `"$global:ShotDir\$Name.png`""; "$Name.png" }
function Vd-Px([double]$x, [double]$y) { @([int]($global:Vd.Ox + $x * $global:Vd.Scale), [int]($global:Vd.Oy + $y * $global:Vd.Scale)) }
function Vd-Tap([double]$x, [double]$y) { $p = Vd-Px $x $y; Adb shell "input -d $($global:Vd.Id) tap $($p[0]) $($p[1])" }
function Vd-Swipe([double]$x1, [double]$y1, [double]$x2, [double]$y2, [int]$ms = 250) { $a = Vd-Px $x1 $y1; $b = Vd-Px $x2 $y2; Adb shell "input -d $($global:Vd.Id) swipe $($a[0]) $($a[1]) $($b[0]) $($b[1]) $ms" }
function Vd-Back { Adb shell "input -d $($global:Vd.Id) keyevent KEYCODE_BACK" }
function Vd-Key([string]$k) { Adb shell "input -d $($global:Vd.Id) keyevent $k" }
function Vd-Launch([string]$UnityArgs = '') {
    Adb shell am force-stop $global:Pkg | Out-Null; Adb logcat -c | Out-Null
    $act = (Adb shell cmd package resolve-activity --brief -c android.intent.category.LAUNCHER $global:Pkg | Select-Object -Last 1).Trim()
    if ($UnityArgs) { Adb shell am start --display $global:Vd.Id -n $act --es unity "'$UnityArgs'" | Out-Null } else { Adb shell am start --display $global:Vd.Id -n $act | Out-Null }
}
function Vd-Perf([int]$n = 3) { Adb logcat -d | Select-String 'App.Perf' | Select-Object -Last $n | % { ($_.Line -split 'App.Perf: ')[1] } }
function Vd-Log([string]$pattern, [int]$n = 10) { Adb logcat -d | Select-String $pattern | Select-Object -Last $n | % { ($_.Line -split ' I Unity   : ')[-1] -replace '^\[Glamour\] .*\\Scripts\\', '' } }
function Vd-Errors([int]$n = 10) { Adb logcat -d -b main,crash | Select-String -Pattern 'Exception|Error|FATAL|crash|NullRef' | Select-Object -Last $n | % { $_.Line.Substring([math]::Max(0,$_.Line.Length-200)) } }
