# Gemeinsame adb-Helfer fuer die Android-Tests (dot-sourcing: . .\Tools\Android\adb.ps1)
$global:Adb = $env:GLAMOUR_ADB
if (-not $global:Adb) { $global:Adb = (Get-ChildItem 'C:\Program Files\Unity\Hub\Editor' -Directory | Sort-Object Name -Descending | % { Join-Path $_.FullName 'Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe' } | ? { Test-Path $_ } | Select-Object -First 1) }
if (-not $global:Adb) { $global:Adb = "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe" }
$global:Pkg = 'de.domezosware.glamourgames.unityandroid'
# Zielgeraet (Standard: Pixel 10 per WLAN-Debugging), ueberschreibbar mit GLAMOUR_SERIAL
$global:Serial = if ($env:GLAMOUR_SERIAL) { $env:GLAMOUR_SERIAL } else { 'adb-57241FDCR0042J-AKTDfX._adb-tls-connect._tcp' }

function Adb { & $global:Adb -s $global:Serial @args }
function Start-Glamour([string]$UnityArgs = '') {
    Adb shell input keyevent KEYCODE_WAKEUP | Out-Null; Adb shell am force-stop $global:Pkg | Out-Null; Adb logcat -c | Out-Null
    $act = (Adb shell cmd package resolve-activity --brief -c android.intent.category.LAUNCHER $global:Pkg | Select-Object -Last 1).Trim()
    if ($UnityArgs) { Adb shell am start -n $act --es unity "$UnityArgs" | Out-Null } else { Adb shell am start -n $act | Out-Null }
}
# Bildschirmfoto vom Geraet (binaer-sicher ueber cmd)
function Get-Shot([string]$Path) { cmd /c "`"$global:Adb`" exec-out screencap -p > `"$Path`"" }
# Ansichts-Geometrie aus dem Log: Pixel = ox + x*scale, oy + y*scale
function Get-View {
    $l = (Adb logcat -d -s Unity:I | Select-String 'Ansicht:' | Select-Object -Last 1).Line
    if ($l -match 'scale=([\d.,]+) ox=(-?[\d.,]+) oy=(-?[\d.,]+)') { [pscustomobject]@{ Scale=[double]($Matches[1] -replace ',','.'); Ox=[double]($Matches[2] -replace ',','.'); Oy=[double]($Matches[3] -replace ',','.') } }
}
# Tippen auf Designkoordinaten (1600x900-Raum)
function Tap-Design([double]$x, [double]$y) { $v = Get-View; if (-not $v) { throw 'Keine Ansicht im Log' }; Adb shell input tap ([int]($v.Ox + $x * $v.Scale)) ([int]($v.Oy + $y * $v.Scale)) }
function Swipe-Design([double]$x1,[double]$y1,[double]$x2,[double]$y2,[int]$ms=200) { $v = Get-View; Adb shell input swipe ([int]($v.Ox+$x1*$v.Scale)) ([int]($v.Oy+$y1*$v.Scale)) ([int]($v.Ox+$x2*$v.Scale)) ([int]($v.Oy+$y2*$v.Scale)) $ms }
