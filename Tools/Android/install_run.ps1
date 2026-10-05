# Installiert die APK auf dem verbundenen Geraet und startet die App.
#   .\Tools\Android\install_run.ps1 [-Apk Pfad] [-UnityArgs "--scene=3 --skip=2"]
param([string]$Apk = "$PSScriptRoot\..\..\Builds\Android\GlamourGamesUnityAndroid.apk", [string]$UnityArgs = '')
. "$PSScriptRoot\adb.ps1"
Adb install -r -d $Apk
Start-Glamour $UnityArgs
