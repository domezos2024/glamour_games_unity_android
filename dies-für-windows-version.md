# Vier-Spieler-Bluetooth: Übertragung auf die Windows-Version

Dieses Dokument wird **laufend** mit der Android-Umsetzung (Repo `glamour_games_unity_android`, Branch `feature/vier-spieler-bluetooth`) fortgeschrieben.
Ziel: dieselben Änderungen in `glamour_games_unity` (Windows) nachziehen. Jeder Schritt ist abhakbar.

Grundregel: Dateien unter `Assets/Glamour/Scripts/Core/Net/` und die Spiel-Logik sind in beiden Repos praktisch identisch. Android-spezifisch sind nur `AndroidBt.cs`, `GlamourBt.java`, Manifest/Build. Windows-spezifisch ist nur `WinBt.cs`.
Pfade unten sind relativ zum Repo-Wurzelverzeichnis. Android-Repo: `E:\AndroidStudioProjects\glamour_games_unity_android`.

## Status der Android-Umsetzung

- [x] Branch `feature/vier-spieler-bluetooth` angelegt
- [x] Netzwerkkern (Wire, Transport-Schnittstelle, Session) – Abschnitt 1
- [x] Mehr-Peer-Transporte Android (`GlamourBt.java`, `AndroidBt.cs`) und Windows-Entwurf (`WinBt.cs`) – Abschnitt 2
- [x] Link-Fassade, Scene-Basis, App-Overlay – Abschnitt 3
- [x] Registry mit Sitzzahlen, Lobby – Abschnitte 4 und 5
- [x] Spiele Memory, Kniffel, Snake, Blackjack, Poker auf 2–4 Sitze – Abschnitt 6
- [x] Vorschau-/Testwerkzeuge (Mehrprozess-Test, Konsolentests) – Abschnitte 7 und 8
- [ ] Echter Test mit 4 Geräten (Android: nur 1 Handy + PC vorhanden, daher offen) – Abschnitt 11

## 0. Vorbereitung (Windows-Repo)

- [ ] Eigenen Branch anlegen: `git checkout -b feature/vier-spieler-bluetooth`
- [ ] Build-Stand festhalten (Tag oder Commit-Hash notieren), `.utmp/` nicht anfassen
- [ ] Unity-Version prüfen (muss C# 9 / Unity 6000.3 sein; keine Features über C# 9: kein `file-scoped namespace`, kein `record struct`, keine `global using`)
- [ ] Beide Repos gleich halten: Skripte aus dem Android-Repo nach `glamour_games_unity` kopieren (nur `AndroidBt.cs`, `GlamourBt.java`, Android-Manifest bleiben außen vor)
- [ ] Empfohlen: die Dateien per Diff übertragen: im Android-Repo `git diff main -- Assets/Glamour/Scripts > windows.patch`, im Windows-Repo `git apply --3way windows.patch`, danach Konflikte lösen (Windows-spezifische Stellen: `WinBt.cs`, Plattform-Defines)

## 1. Netzwerkkern (identisch kopieren)

Neue Dateien aus dem Android-Repo 1:1 nach `Assets/Glamour/Scripts/Core/Net/` kopieren (inkl. `.meta`, sonst vergibt Unity neue GUIDs):

- [ ] `Wire.cs` – Nachrichten-Umschlag `GL2|sessionId|messageId|senderId|seat|type|seq|ack|rev|payload...`, Prozent-Kodierung für `% | CR LF`, `WireUtil` (Namen begrenzen, Zufalls-IDs)
- [ ] `Transport.cs` – `ISessionTransport` (mehrere Verbindungen), `IBtTransport`, `TransportEvent`, `BtErr`, `BtDev`, `IClock`, Fehlertexte; `BtDev` aus der alten `Link.cs` entfernen (sonst doppelt definiert)
- [ ] `Session.cs` – Sitzungslogik (Roster, Sitzplätze, Intent/Delta/Snapshot, Ping, Reconnect-Fenster, Resume-Token, Statistik)
- [ ] Prüfen: keine Unity-Abhängigkeit in diesen drei Dateien (sie werden auch in den Konsolentests kompiliert)

Wichtige Entwurfsentscheidungen (nicht ändern, sonst laufen Android und Windows auseinander):

- Stern-Topologie, **Host ist alleinige Autorität** (Sitzvergabe, Zufall, Revision, Validierung)
- Gäste senden nur **Intents** (mit Folgenummer, vom Host quittiert, bei Bedarf wiederholt)
- Host verteilt **Deltas** mit fortlaufender Revision; bei Lücke fordert der Gast einen **Snapshot** an
- Private Daten (Pokerkarten) nur per `EmitTo(seat, …)`, nie im Broadcast
- Identität = zufällige `playerId` + kurzlebiger `resumeToken`, **nie** die Bluetooth-Adresse
- Protokollversion `GL2`; ältere Clients (`HELLO|…`) werden abgelehnt
- Reconnect-Fenster 45 s (konfigurierbar in `SessionConfig.ReconnectWindow`), danach Abbruch der Partie
- Dienst-UUID unverändert `7a3c2f5e-9b1d-4e8a-a6f2-3d5c8b9e1f42` (muss auf Android und Windows gleich sein)

## 2. Transport

### 2a. Android (zur Referenz, nichts zu tun)

- Java-Plugin `Assets/Plugins/Android/GlamourBt.java`: Mehr-Peer (`startHost`, Accept-Schleife bis `maxPeers` = 3, `connect`, `send(peer,line)`, `close(peer)`, `poll()` mit Ereignissen `C|peer|name|addr`, `L|peer|line`, `X|peer|code|text`)
- Fehlercodes (gleich in C#-`BtErr`): 1 Berechtigung, 2 Bluetooth aus, 3 kein Adapter, 4 Verbindung fehlgeschlagen, 5 Zeitüberschreitung, 6 Socket beendet, 7 Lesefehler, 8 Schreibfehler, 9 voll, 10 unbekannt

### 2b. Windows (`Core/Net/WinBt.cs`)

- [ ] `WinBt` auf `IBtTransport` / `ISessionTransport` umstellen (Datei aus dem Android-Repo übernehmen; sie enthält bereits die Mehr-Peer-Fassung)
- [ ] `listen(s, 3)` und **Accept-Schleife** statt einmaligem `accept` (Server-Socket bleibt offen, auch im Spiel, für Wiederverbindung)
- [ ] Pro Verbindung eigener Lese-Thread, Peer-Nummern ab 1, nie wiederverwenden
- [ ] Schreiben pro Peer mit eigener Sperre; `Close(peer)` erst nach dem Leeren der Sendewarteschlange
- [ ] Lese-/Schreibfehler als `TransportEvent.Close(peer, BtErr.…)` melden
- [ ] Ereignisse über `ConcurrentQueue<TransportEvent>` an den Spiel-Thread übergeben (`Poll`)
- [ ] SDP-Anmeldung unverändert lassen (fertiger Datensatz `BTH_SET_SERVICE`; nur Dienstklasse liefert WSAEINVAL 10022), erst beim Stoppen abmelden
- [ ] `Paired()`, `Scan()`, `LocalName`, `EnsurePermission()` (Windows: immer true) ergänzen
- [ ] Mehr als ein gleichzeitiger Client unter Windows mit echtem Adapter testen (Adapter-Limit prüfen und dokumentieren)
- [ ] Unity-Windows-Build starten und prüfen, dass `WinBt.cs` kompiliert (im Android-Repo wird sie nur mit Windows-Defines kompiliert, also dort **nicht** compilegeprüft)

## 3. Fassade, Szenenbasis, App

### 3a. `Core/Net/Link.cs` (Fassade)

- [ ] Datei aus dem Android-Repo übernehmen; die alte Link-Logik (HELLO/Relay) ersetzen
- [ ] Statische Eigenschaften: `T` (Transport), `Sess` (Session; **nicht** `S`, kollidiert mit der Sound-Enum), `IsHost`, `Connected`, `InGame`, `Frozen`, `MySeat`, `Count`, `Name(seat)`, `PeerName`, `MyName`, `Seed`, `HostDriven`
- [ ] API: `Init()`, `Host()`, `Join(addr)`, `Start(GameInfo)`, `SetReady(bool)`, `Kick(seat)`, `Update()`, `Intent(...)`, `Emit(...)`, `EmitTo(seat, ...)`, `Net(...)` (Relay für Altspiele), `BackToMenu()`, `LeaveGame()`, `DrawFrozen(canvas, time)`
- [ ] Plattform-Transport in `Init()`: `#if UNITY_ANDROID && !UNITY_EDITOR` → `AndroidBt`, `#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN` → `WinBt`
- [ ] Nachrichten werden in einer Warteschlange (max. 4000) gehalten und der laufenden Szene zugestellt, sobald deren `OppKey` zum Spiel passt

### 3b. `Core/Scene.cs`

- [ ] `virtual bool Seated => false;` (Sitzspiel: `Act`/`NetIntent`/`NetDelta`; sonst alter Relay-Pfad `Net`/`NetRecv`)
- [ ] `int MySeat`, `int SeatCount`
- [ ] `protected void Act(kind, params object[])`, `Emit(...)`, `EmitTo(seat, ...)`
- [ ] Hooks: `NetIntent(seat, kind, args)`, `NetDelta(kind, args, priv)`, `NetSnapshot(seat)`, `NetApplySnapshot(blob)`, `NetLost()`
- [ ] `Leave()`: bei `(Remote || Seated) && Link.InGame` Host → `Link.BackToMenu()`, Gast → `Link.LeaveGame()` (außer `HostDriven`)
- [ ] `PName(i)`: bei Sitzspielen `Link.Name(i)`

### 3c. `Core/App.cs`

- [ ] Frame-Schleife: `bool frozen = Link.Frozen && (cur.Seated || cur.Remote); if (!frozen) cur.BaseUpdate(dt); … if (frozen) Link.DrawFrozen(canvas, cur.Time);` (Pause-Overlay bei Verbindungsverlust, Spiel-Zeit steht still)
- [ ] `Link.Update()` pro Frame aufrufen (Poll des Transports und Zustellung)

### 3d. `Core/Opponents.cs`

- [ ] Anzeige „Bluetooth · Name“ bei mehr als 2 Spielern als „Bluetooth · N Spieler“

## 4. Registry (`Games/Menu.cs`)

- [ ] `GameInfo(id, name, sub, col, make, minSeats = 2, maxSeats = 2)` – feste Spiel-IDs gleich `OppKey`: `memory`, `ttt`, `c4`, `ships`, `snake`, `kniffel`, `nim`, `slot`, `bj`, `poker`
- [ ] `Registry.ById(id)`; Start übergibt die **ID**, nicht den Index
- [ ] Sitzzahlen: Memory 2–4, Kniffel 2–4, Snake 2–4, Blackjack 2–4, Poker 2–4; TicTacToe, Vier gewinnt, Schiffe versenken, Nim, Slot bleiben 2 (Relay-Modus)
- [ ] Menü: `Launch` → `Link.Start(Registry.All[i])` bei verbundener Sitzung; Oberzeile zeigt „N Spieler verbunden“

## 5. Lobby (`Core/Net/BtLobby.cs`)

- [ ] Datei übernehmen: vier Spielerkarten (Name, Farbe, Bereit-Status, Verbindungsstatus), Bereit-Knopf, Host: Kick und Start, „x/4 verbunden“, Gerätelisten (gekoppelte PCs/Handys, grün = bietet Glamour-Dienst an)
- [ ] Texte mit Zeilenumbruch immer als zwei `Gfx.Text`-Aufrufe zeichnen (`\n` wird als „?“ dargestellt)
- [ ] Start erst, wenn Mindestzahl bereit und alle dieselbe Protokollversion/denselben Spieldatensatz haben (macht `Session`)
- [ ] Fehlertexte aus `NetText.Of(BtErr)` und `NetText.Of(NetError)` anzeigen

## 6. Spiele

Allgemeines Muster für jedes Sitzspiel:

- `public override bool Seated => Remote;`
- Alle Eingaben (auch die des Hosts) laufen über `Act(kind, args)`
- Host: `NetIntent(seat, kind, args)` prüft Sitz/Phase/Zug und führt aus, danach `Emit(kind, …)` (der Host überspringt in `NetDelta` seine eigenen Änderungen: `if (Link.IsHost) return;`)
- Gäste: `NetDelta` wendet an; `NetSnapshot(seat)` / `NetApplySnapshot(blob)` für Nachzügler und Wiederverbindung
- Zufall (Mischen, Würfeln, Spawn) **nur** beim Host; Gäste bekommen Ergebnisse
- Namen auf ca. 8–14 Zeichen kürzen, sonst überlaufen Ergebnisleisten bei 4 Spielern

### 6a. Memory (`Games/Memory.cs`)

- [ ] 2–4 Sitze, Karten mischt der Host, Zug-Reihenfolge reihum, `NetIntent("flip")`, `NetDelta`, `ApplyFlip`, `BuildCards`
- [ ] Farben je Sitz (`SeatCol`), Punkte-Boxen `boxN` je Spielerzahl

### 6b. Kniffel (`Games/Kniffel.cs`)

- [ ] Würfel würfelt der Host; Haltezustand pro Sitz; Eingaben werden gepuffert (`pend`/`ApplyPending`) solange der Host animiert (`hostBusy`)
- [ ] Wertungstabelle mit `tableN` Spalten; Bonus „x/63“ nur bei `tableN <= 2` zeichnen (sonst Überlappung)

### 6c. Snake (`Games/Snake.cs`)

- [ ] Host rechnet die Ticks (`NetTick`), verteilt Positionen/Futter (`NetPayload`), Ergebnis `NetResult`; Eingabe als Richtungs-Intent
- [ ] Ergebnisleiste mit gekürzten Namen

### 6d. Blackjack (`Games/Blackjack.cs`)

- [ ] `Hand[]` dynamisch (`Setup(n)`), Positionen `PXi(i)` je Spielerzahl (2: links/rechts, 3–4: gleichmäßig), Einsätze pro Sitz
- [ ] Host führt Schlitten, Bank und Auszahlung; Deltas `card`, `reveal`, `sync`; die verdeckte Bankkarte kennt nur der Host (Snapshot und Sync verraten sie nicht)
- [ ] Intents: `bet`, `deal`, `hit`, `stand`, `double`, `round`, `refill`, `reset`; `LocalAct(kind)` für lokalen Modus (ohne Netz) / Remote
- [ ] Solo/gegen Computer bleibt wie bisher

### 6e. Poker (`Games/Poker.cs`)

- [ ] Alle festen 3er-Annahmen entfernt: `Layouts` für 2/3/4 Sitze (`Seat`-Eigenschaft, `SY(i)`), `Next`, `Apply`, Showdown mit Side-Pots und Restchips reihum, Button/Blinds (Heads-up-Regel bleibt), Farben `SeatCol`
- [ ] Netzmodus nur Menschen (2–4); Host führt `HandCo()` und wartet auf Intent `mv` (fold/call/raise, Prüfung Sitz = `toAct`, Raise nur wenn `Legal(seat).canRaise`)
- [ ] Öffentlicher Zustand `pub` (17 Kopffelder + 12 Felder je Spieler; Reihenfolge siehe `PubArgs()`), geht nach **jeder** Zustandsänderung an alle
- [ ] **Private Karten:** Hole Cards nur per `EmitTo(seat, "hole", …)`; andere Sitze sehen Platzhalter (`Known=false`), echte Karten fremder Sitze erscheinen erst im `pub`, wenn `showAll && !Folded`
- [ ] Snapshot: `pub` plus eigene Hole Cards des angefragten Sitzes
- [ ] Gast-Zug: `Act("mv", …)`; Sperre `mvSent` gegen Doppelklick; Host-Intents `hand` (nächste Hand, jeder Sitz) und `new` (nur Sitz 0 oder nach Spielende)
- [ ] Abwesenheitsregel: Spiel pausiert (Overlay), nach Ablauf des Reconnect-Fensters Abbruch der Partie
- [ ] Prüfung (siehe Abschnitt 7): jeder Gast kennt nur seine eigenen Karten bis zum Showdown

### 6f. Altspiele (TicTacToe, Vier gewinnt, Schiffe, Nim, Slot)

- [ ] Unverändert im Relay-Modus (`Net`/`NetRecv`, Host = Delta, Gast = Intent), nur 2 Sitze
- [ ] Bekannte Einschränkung: Nach einer Wiederverbindung liefert ihr `NetSnapshot` leer (`""`) – Zustand kann abweichen

## 7. Vorschau- und Mehrprozess-Test (ohne Unity, ohne Bluetooth)

Dateien: `Tools/Preview/Preview.cs`, `TcpBt.cs`, `FakeUnity.cs`, `preview.sh`. `TcpBt` ersetzt Bluetooth durch lokales TCP (mehrere Peers), alles andere (Session, Link, Szenen) ist der echte Code.

- [ ] `Preview.cs`/`TcpBt.cs` aus dem Android-Repo übernehmen (Tools-Ordner, nicht Teil des Unity-Builds)
- [ ] Bauen (Windows, PowerShell): Projekt `Preview.csproj` mit `Core\**\*.cs` (ohne `App.cs`, `Sfx.cs`), `Games\*.cs`, `FakeUnity.cs`, `Preview.cs`, `TcpBt.cs`; Zielplattform `net8.0`, `LangVersion 9.0`
- [ ] Umgebung: `$env:TMPDIR` auf einen Ordner mit `glamour_preview_cache\font.raw` und `img.raw` setzen (erzeugt `preview.sh` per Python aus den Atlas-PNGs) und `$env:GLAMOUR_ROOT` auf das Repo, sonst fehlen Schrift und Bilder
- [ ] Host starten: `dotnet Preview.dll --scene=<Index> --t=<Sek> --net=host:PORT:SITZE --script="..." --out=host.png`
- [ ] Gäste starten: `dotnet Preview.dll --scene=<Index> --t=<Sek> --net=join:PORT --script="..." --out=gast1.png` (je Gast ein Prozess)
- [ ] Szenenindex: 0 Memory, 1 TicTacToe, 2 Vier gewinnt, 3 Schiffe, 4 Snake, 5 Kniffel, 6 Nim, 7 Slot, 8 Blackjack, 9 Poker
- [ ] Skript: `c,x,y@t` Klick, `k,Taste@t` Taste, `s@t` Bildschirmfoto (`_0.png`, `_1.png` …); Koordinaten im 1600x900-Raum
- [ ] Prozesse mit `Start-Process` starten und mit `Wait-Process` warten (lange Läufe als Hintergrund-Job, sonst 120-s-Limit)
- [ ] `GLAMOUR_NETLOG=1` gibt Sitzungsereignisse aus; `GLAMOUR_DUMP=1` gibt für Poker aus, welche Hole Cards jeder Prozess kennt (`bekannt`/`verdeckt`)
- [ ] Erwartung Poker: Gast kennt nur den eigenen Sitz (`bekannt`), fremde Sitze `verdeckt[0,0]`; bei `showAll=True` sind alle `bekannt`
- [ ] Erwartung Blackjack: komplette Runde (Einsatz, Austeilen, Halten, Auszahlung) auf Host und Gast identisch
- [ ] Hinweis: In der Vorschau erscheinen Kartenmotive als weiße Flächen (Bild-Atlas), das beeinträchtigt die Logik nicht

## 8. Konsolentests (`Tools/Tests`)

- [ ] `Tools/Tests` aus dem Android-Repo kopieren (`MemTransport.cs`, `NetTests.cs`, `Program.cs`, `GlamourTests.csproj`)
- [ ] Ausführen: `cd Tools\Tests; dotnet run -c Release` – erwartet **195 Prüfungen, 0 Fehler**
- [ ] Inhalt: Wire-Kodierung, Beitritt/Kick/Ready/Start, Intents (Idempotenz, falscher Sitz, Wiederholung), Deltas/Snapshots, Lücken, Reconnect (Token, Fenster), Host-Ende, Spieler verlässt Partie, Verzögerung/Jitter/Duplikate
- [ ] `MemTransport` simuliert Latenz, Jitter, Duplikate, Verlust und harte Trennung
- [ ] Offen: Vertragstests für `WinBt` (echter Adapter) – nur auf dem PC mit Bluetooth möglich

## 9. Bauen, Installieren, Testen

### Android (Referenz)

- [ ] Signatur: Variablen aus `E:\Claude Projekte\android_signing\keystore.properties` setzen (`GLAMOUR_KEYSTORE`, `GLAMOUR_KEYSTORE_PASS`, `GLAMOUR_KEY_ALIAS`, `GLAMOUR_KEY_PASS`) – Inhalt nie ausgeben, nie ins Repo
- [ ] `Unity.exe -batchmode -quit -buildTarget Android -projectPath <Repo> -executeMethod GlamourGames.EditorTools.ProjectBootstrap.CiBuild -buildPath <Repo>\Builds\Android\GlamourGamesUnityAndroid.apk -logFile build.log`
- [ ] `Tools\Android\install_run.ps1` (installiert per `adb install -r -d` und startet die App); bei `INSTALL_FAILED_UPDATE_INCOMPATIBLE` war der Build nicht mit dem Release-Schlüssel signiert
- [ ] Gerät: Pixel 10 (USB `57241FDCR0042J`), `Tools\Android\adb.ps1` wählt es automatisch

### Windows

- [ ] Windows-Standalone-Build per Unity (`-buildTarget StandaloneWindows64`) oder Editor-Menü; beim ersten Start Bluetooth-Berechtigung/Adapter prüfen
- [ ] Windows als Host: SDP-Dienst anmelden, Handy verbindet sich; Windows als Gast: Verbindung zum Handy-Host
- [ ] Test PC <-> Handy: `Player.log` vor dem Start löschen, sonst liest man alte Einträge

## 10. Leistung

- [ ] Draw-Callbacks in der Vorschau laufen nur im letzten Bild: Animationen in `Draw`/`Modal.Extra` zeitbasiert rechnen
- [ ] Pro Frame keine Listen/Strings neu anlegen in Hot Paths (Netz-Poll, Zeichnen); `Link.Update()` leert die Warteschlange begrenzt
- [ ] Poker-KI nur für Solo/Hotseat; im Netzmodus keine KI-Rechnung
- [ ] Messen: Bildzeit auf Zielgerät und PC (Profiler), Messwerte hier eintragen: ____

## 11. Geräte-Matrix und Abnahme

- [ ] 2 Geräte (Android + Windows) – Lobby, alle Sitzspiele
- [ ] 3 Geräte – Memory, Kniffel, Snake, Blackjack, Poker
- [ ] 4 Geräte – alle fünf Sitzspiele; Wiederverbindung eines Gasts (Bluetooth aus/an) innerhalb 45 s
- [ ] Host-Verlust: sauberer Abbruch mit Meldung
- [ ] Hinweis (Android-Stand): Echte 4-Geräte-Tests nicht durchgeführt; Protokoll/Sitzlogik/Spiele sind per Simulation (195 Prüfungen) und Mehrprozess-Läufen (3–4 Prozesse) belegt


## Nachtrag 08.10.2026

- [ ] `Tools/Tests/TransportContract.cs` und `PokerTests.cs` übernehmen; `dotnet run -c Release` muss 251 Checks / 0 Fehler liefern
- [ ] `WinBt` gegen denselben Transport-Vertrag prüfen (echter Adapter, 3 Gäste)
- [ ] Offene Punkte siehe Abschnitt "Restliche offene Punkte" in `ChatGPT-Verbesserungen.md`
