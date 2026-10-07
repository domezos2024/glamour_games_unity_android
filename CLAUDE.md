# CLAUDE.md - glamour_games_unity_android

## Pfade
- Lokaler Klon auf dem PC: `E:\AndroidStudioProjects\glamour_games_unity_android` (vorher `E:\source\repos\glamour...`; nach Umzug Unity-Caches `Library\Bee`, `Library\BuildPlayerData` loeschen)

## Pflicht nach jeder Code-Aenderung
- APK auf dem PC bauen (Unity batchmode, siehe README "Bauen") und **immer** auf dem Geraet
  `adb-57241FDCR0042J-AKTDfX._adb-tls-connect._tcp` installieren und testen (`Tools\Android\install_run.ps1`, `Tools\Android\vd.ps1`).
- `Tools\Android\adb.ps1` waehlt das Pixel 10 automatisch: USB `57241FDCR0042J` oder WLAN `adb-57241FDCR0042J-AKTDfX._adb-tls-connect._tcp` (ueberschreibbar mit `GLAMOUR_SERIAL`). WLAN-Debugging faellt gelegentlich weg (mDNS findet nichts) -> USB nutzen.
- Die installierte App ist mit dem Release-Schluessel signiert; Debug-Builds scheitern mit `INSTALL_FAILED_UPDATE_INCOMPATIBLE`. Vor dem Build die Variablen aus `E:\Claude Projekte\android_signing\keystore.properties` setzen (Inhalt nie ausgeben, nie ins Repo).

## Ohne Unity pruefen (Cloud/Linux)
- `bash Tools/Preview/preview.sh --scene=<0..9|menu> --t=<s> --script="c,x,y@t" --out=bild.png` kompiliert alles ausser `App.cs`, `Sfx.cs`, `URP/` und rendert eine Szene.
- Draw-Callbacks laufen in der Vorschau nur im letzten Bild: Animationen in `Draw`/`Modal.Extra` zeitbasiert (geschlossen) rechnen, nicht per Bild-dt integrieren.

## Physik
- Gemeinsamer Kern: `Assets/Glamour/Scripts/Core/Physics.cs` (`Phys`, `Body`, `Rocker`, `Sinker`, `Chain`, `Glide`). Massstab je Szene in Pixel/Meter, `Phys.Gpx(pxPerM)` = echte Erdbeschleunigung.
