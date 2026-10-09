# Glamour Games – Analyse und Maßnahmenplan

## Kurzfazit

Die App besitzt bereits eine visuell anspruchsvolle Unity-6-/URP-Basis und funktionierenden Bluetooth-Mehrspieler für **zwei** Geräte. Eine Erweiterung auf vier Spieler ist kein einfacher Parameterwechsel: Transport, Lobby, Protokoll, Szenen-Basis und die einzelnen Spiele sind derzeit überwiegend auf genau einen Peer bzw. zwei Sitzplätze zugeschnitten.

Besonders deutlich: Android akzeptiert beim Hosten genau einen RFCOMM-Socket und schließt danach den Server-Socket (`Assets/Plugins/Android/GlamourBt.java`, Zeilen 83–95); `Link` beschreibt und modelliert „zwei Geräte“ und nur einen `PeerName` (`Assets/Glamour/Scripts/Core/Net/Link.cs`, Zeilen 39–50); die Lobby nennt konsequent „Mitspieler“ im Singular (`Assets/Glamour/Scripts/Core/Net/BtLobby.cs`).

## Tatsächliche Befunde

### Bestehende Stärken

- [x] Die vorhandene Basis erhalten: Unity 6000.3.25f1, URP 17.3, HDR, MSAA 4× und SRP Batcher sind konfiguriert (`Packages/manifest.json`, `ProjectSettings/ProjectVersion.txt`, `Assets/Glamour/Settings/GlamourURP.asset`).
- [x] Die adaptive Auflösung beibehalten und messen: `AdaptiveQuality` senkt den Render-Scale stufenweise von 1,0 auf 0,6, falls mehr als 20 % der Frames zu langsam sind (`Assets/Glamour/Scripts/URP/AdaptiveQuality.cs`). Stand: bleibt unverändert erhalten; Messung folgt unter 6.x.
- [x] Die etablierte Bluetooth-Abstraktion weiterverwenden: `IBtTransport`, Android-Java-Transport und Windows-Transport existieren bereits (`Assets/Glamour/Scripts/Core/Net/Link.cs`, `AndroidBt.cs`, `WinBt.cs`).
- [x] Bestehende reale Testhinweise als Ausgangspunkt nutzen, aber nicht als Vier-Spieler-Nachweis werten: README nennt einen Pixel-10↔Windows-Test und Zwei-Prozess-Tests, jedoch keine Tests mit mehr als zwei Teilnehmern (`README.md`, Abschnitt „Getestet“).

### Vier-Spieler-Hindernisse

- [x] Den Single-Peer-Transport ablösen: Android speichert nur `socket`, `out`, `connected`, `peer`; `accept()` wird nur einmal aufgerufen und der Server sofort geschlossen (`GlamourBt.java`, Zeilen 31–37, 83–125).
- [x] `Link` von globalem Ein-Peer-Zustand (`IsHost`, `PeerName`, `hello`, einer Nachrichtenwarteschlange) zu einer Sitzung mit Spieler-Liste umbauen (`Link.cs`, Zeilen 46–62, 73–122). _(erledigt: `Link` ist Fassade über `Session`; Roster, Sitze, Presence; 251 Konsolenchecks)_
- [x] Das zeilenbasierte, unversionierte Pipe-Protokoll (`HELLO`, `GO`, `MENU`, `BYE`, `G|spiel|art|…`) weiterentwickeln; derzeit fehlen Paket-ID, Sitzplatz, Sequenznummer, ACK, Zustandsrevision, Snapshot und Resume-Token (`Link.cs`, Zeilen 78–119).
- [x] Die Lobby von „Eröffnen/Beitreten für einen Mitspieler“ auf Host + 1–3 Gäste erweitern. Sie zeigt aktuell höchstens sieben gekoppelte Geräte, aber keinen Raum, keine Teilnehmerliste und keine Bereitschaft an (`BtLobby.cs`, Zeilen 13–68).
- [ ] Die Szene-Basis entkoppeln: `Scene.Remote` ist nur „Opponent.Remote“, `PName(int)` behandelt nur Platz 0/1, und `NetLost()` beendet das Spiel direkt (`Scene.cs`, Zeilen 37–68). Das verhindert Pause/Resume oder einen Ersatzspieler.

### Spiel-spezifische Grenzen

| Spiel | Repository-Befund | Konsequenz für 3–4 Personen |
| --- | --- | --- |
| Kniffel | Ergebnis, Namen, Farben und Scoreboard laufen mehrfach mit zwei Elementen bzw. `p < 2`; Bluetooth-Züge sind auf den Gegenpart ausgelegt (`Games/Kniffel.cs`, u. a. Zeilen 56, 138, 257–280). | Dynamische Spielerlisten, 3–4 Score-Spalten, zyklische Zugreihenfolge und hostautoritatives Würfeln/Eintragen nötig. |
| Blackjack | `Hand[] pl` hat zwei Hände; Positionsarray `PX` zwei Werte; Gast ist zwingend Sitz 1 (`Games/Blackjack.cs`, Zeilen 18–33, 58, 67, 103–109). | 2–4 Spieler plus Dealer, dynamisches Layout, Einsatz-/Zugphase pro Sitz und serverautoritatives Deck erforderlich. |
| Poker | Die Tabelle ist fest auf drei Plätze angelegt – in Bluetooth: Host, Gast, Computer; zahlreiche Schleifen nutzen `< 3` und Restchips mit Modulo 3 (`Games/Poker.cs`, Kommentar Zeilen 372–374 sowie z. B. 534, 542, 608, 649). | Zuerst Datenmodell und Tischlayout auf 2–4 menschliche Plätze umbauen, danach Side-Pots, Blinds und Geheimkarten pro Empfänger absichern. |
| Memory | Der Code bezeichnet den Bluetooth-Teilnehmer ausdrücklich als „Platz 2“ und nutzt eine 2-Spieler-Wertung (`Games/Memory.cs`, Kommentar Zeile 30). | Spielerreihenfolge und Punkte dynamisch gestalten; Kartenwahl nur vom aktiven Sitz akzeptieren. |
| Snake | Der Host berechnet die Simulation; der zweite Spieler sendet Richtungswechsel, Zustände werden gespiegelt (`Games/Snake.cs`, Zeilen 17–36). | Hostautoritativen Tick beibehalten, aber 2–4 Schlangen, Eingabepuffer je Sitz und Voll-Snapshots aufnehmen. |

### Grafik, UI/UX und Wartbarkeit

- [ ] Performance als Teil jeder visuellen Änderung prüfen: Android startet auf der Qualitätsstufe „Medium“ (`ProjectSettings/QualitySettings.asset`); URP nutzt HDR, Bloom laut README und MSAA 4×. Zusätzliche vier Spieler, Karten, Würfel und Partikel erhöhen daher die Last sichtbar.
- [ ] Große, gemischte Spiele schrittweise zerlegen: `Slot.cs` (847 Zeilen), `Poker.cs` (723), `Kniffel.cs` (507), `Battleship.cs` (500) und `Snake.cs` (366) enthalten jeweils Spielzustand, Eingaben, Animation/Rendering und teils Netzwerklogik zusammen. Das erschwert sichere Mehrspieleränderungen.
- [x] Automatisierte Tests ergänzen: Im Repository wurden keine Testdateien mit Test/Test-Namensmuster gefunden. Die vorhandenen `Tools/Preview`-Dateien sind dagegen ein sinnvoller Ansatzpunkt für deterministische Simulations- und Protokolltests. Stand: `Tools/Tests` (dotnet, 195 Prüfungen Netzkern grün); Spiellogik-Tests folgen.
- [x] UI für große Teilnehmerzahlen und kleine Querformat-Displays vorab als Layout-Regeln definieren. Das Design arbeitet auf einem festen 1600×900-Raum (`Scene.cs`, Zeile 7); vier Namen, Punktestände und Aktionshinweise benötigen responsive Bereiche statt weiterer fester Koordinaten. _(Layout-Regeln in docs/Mehrspieler-Spezifikation.md, Abschnitt 4)_

## Zielarchitektur für bis zu vier Bluetooth-Spieler

### Entscheidungen vor der Implementierung

- [x] **Topologie festlegen:** Ein Gerät ist alleiniger Host/Spielleiter; bis zu drei Gäste verbinden sich direkt per Bluetooth Classic/RFCOMM mit ihm. Der Host bleibt während einer Partie aktiv. Diese Stern-Topologie passt zum vorhandenen Host-Modell und vermeidet drei unterschiedliche Spielwahrheiten.
- [ ] **Kapazität offen kommunizieren:** Bluetooth Classic/RFCOMM kann je nach Android-Gerät/Adapter Verbindungsgrenzen haben. Deshalb vor dem Produktversprechen mit mindestens zwei Android-Modellen und Windows prüfen; bei instabilen drei Gastverbindungen eine LAN/Wi-Fi-Direct-Transportalternative als separaten Folgeentscheid dokumentieren.
- [x] **Autorität festlegen:** Der Host validiert alle Befehle, vergibt Sitzplätze, erzeugt Zufall, erhöht die Zustandsrevision und verteilt autoritative Deltas/Snapshots. Gäste senden ausschließlich Eingabeabsichten.
- [x] **Identität und Datenschutz:** Pro Sitzung eine zufällige `sessionId`, pro Spieler eine `playerId` und ein kurzlebiges `resumeToken`; Anzeigenamen auf Länge/Zeichensatz begrenzen. Niemals Bluetooth-Adresse als fachliche Identität oder Resume-Token verwenden.

### Zustands- und Protokollmodell

- [x] Einen transportunabhängigen `ISessionTransport` definieren, der mehrere Verbindungen abbildet (`StartHost`, `Connect`, `Peers`, `SendTo`, `Broadcast`, `Disconnect`). Die existierende `IBtTransport`-Schicht dient als Ausgangspunkt, wird aber nicht weiter mit Spiellogik vermischt.
- [x] Nachricht als versioniertes, serialisierbares Envelope modellieren, z. B. `{protocolVersion, sessionId, messageId, senderId, seat, type, sequence, ack, stateRevision, payload}`. JSON oder ein eindeutig längenpräfixiertes Binärformat wählen; das aktuelle `|`-Trennen mit Ersetzung durch `/` ist für wachsende Datenmodelle ungeeignet (`Link.cs`, Zeile 76).
- [x] Nachrichtentypen implementieren: `Hello`, `Welcome`, `JoinRequest`, `Roster`, `Ready`, `StartRequest`, `StartGame`, `Intent`, `StateDelta`, `StateSnapshot`, `Ack`, `Ping`, `Pong`, `Disconnect`, `ResumeRequest`, `ResumeAccepted`, `Error`.
- [x] Für jeden Spieler einen Eingabekanal mit monotoner Client-Sequenz führen. Doppelte, verspätete oder falsche-Sitz-Befehle idempotent verwerfen und mit der aktuellen Revision quittieren.
- [x] Bei Beitritt oder Wiederverbindung immer einen vollständigen Snapshot (Sitzliste, Spielphase, Random-/Deckzustand, Zug, Timer, sichtbarer Spielzustand) senden; danach nur Deltas mit fortlaufender Revision. _(erledigt: `NetSnapshot/NetApplySnapshot` in Memory, Kniffel, Snake, Blackjack, Poker; per Mehrprozess-Test verifiziert)_
- [x] Geheimnisse getrennt senden: Bei Poker dürfen Hole Cards nur an den jeweiligen Sitz gehen; der Broadcast enthält ausschließlich öffentliche Karten, Einsätze, Phasen und freigegebene Karten. _(Poker: EmitTo/"hole", Broadcast nur öffentlich; per Prozess-Dump geprüft)_

### Lobby, Host und Join

- [x] Einen Raumzustand `Discovering → Lobby → Starting → InGame → Paused/Reconnecting → Finished` einführen; damit Status nicht mehr aus Textpräfixen wie `StartsWith("wartet")` abgeleitet wird (`Link.cs`, Zeile 53).
- [x] Host-Ansicht: Spiel wählen, 2–4 Sitzplätze, Namen/Farben, Bereitschaft, Kick/Abbruch vor Start und eindeutige Kapazitätsanzeige „x/4 verbunden“.
- [x] Gast-Ansicht: gekoppelte Geräte, Verbindungsfortschritt, Teilnehmerliste, eigener Sitz, Bereitschaft und verständliche Fehlermeldung. Die vorhandenen Schritte zum Koppeln dürfen als Hilfe bestehen bleiben (`BtLobby.cs`, Zeilen 65–68). _(Vorschau mit 3 Prozessen: Teilnehmerliste, eigener Sitz („du“), Bereit-Umschalter, Status „Verbunden mit …“, Fehlermeldung „Host hat die Sitzung beendet“; Koppel-Hilfe bleibt)_
- [x] Start erst freigeben, wenn die konfigurierte Mindestzahl bereit ist und alle Clients dasselbe `protocolVersion`/denselben Spieldatensatz bestätigen.
- [x] Spielauswahl nicht mehr nur als Registry-Index versenden (`Link.Go`); eine stabile Spiel-ID sowie Sitzanzahl und Regelparameter übertragen. Registry-Reihenfolgen dürfen kein Netzwerkvertrag sein.

### Fehlerbehandlung und Reconnect

- [x] In jedem Transport Lese-/Schreibfehler, Socket-Ende, Berechtigungsfehler und Host-Verlust in strukturierte Fehlercodes überführen. Aktuell setzt Android bei Schreibfehler pauschal `getrennt` (`GlamourBt.java`, Zeilen 128–134).
- [x] Heartbeat (z. B. Ping/Pong) und Timeout einführen; Teilnehmer als `Reconnecting` markieren, nicht unmittelbar die Partie beenden.
- [x] Reconnect-Fenster, z. B. 30–60 Sekunden, als konfigurierbare Spielregel festlegen. Der Host behält den Sitz und akzeptiert Resume nur mit `sessionId`, `playerId` und `resumeToken`.
- [x] Je Spiel eine Abwesenheitsregel entscheiden und im UI anzeigen: pausieren (empfohlen für Kniffel/Memory/Poker), aussetzen oder KI übernehmen (nur wenn explizit vorgesehen). Keine verdeckte Aktion für einen getrennten Spieler durchführen. _(Regel für alle Sitzspiele: pausieren (Overlay mit Name und Restzeit), danach Abbruch; festgehalten in docs/Mehrspieler-Spezifikation.md)_
- [x] Bei Host-Verlust zunächst sauber beenden und Ergebnis/Abbruch anzeigen. Host-Migration erst nach stabilen Snapshots und belastbaren 4-Spieler-Tests als eigene Ausbaustufe planen.

## Nummerierter Umsetzungsplan

### 1. Absicherung und fachliche Spezifikation

- [x] 1.1 Einen Feature-Branch und einen reproduzierbaren Build-Stand festhalten; vorhandene `.utmp/`-Änderung nicht anfassen.
- [x] 1.2 Für jedes Zielspiel Regeln für zwei, drei und vier Menschen festschreiben: Mindestspieler, Sitzreihenfolge, Sieg, Unterbrechung und Abbruch. _(festgeschrieben in docs/Mehrspieler-Spezifikation.md, Abschnitt 1)_
- [x] 1.3 Eine Kompatibilitätsmatrix Android↔Android, Android↔Windows und Windows↔Windows mit zwei, drei und vier Teilnehmern anlegen. _(Matrix angelegt, Spalten mit Stand S/G in docs/Mehrspieler-Spezifikation.md; echte Geräteläufe offen)_
- [x] 1.4 Messziele definieren: Verbindungszeit, Reconnect-Erfolg, Paketverlust/Timeout, Host-FPS, Speicher und Spielzustands-Desyncs. _(Messziele definiert in docs/Mehrspieler-Spezifikation.md, Abschnitt 3; Messwerte offen)_

### 2. Reines Domänenmodell vom Rendering lösen

- [x] 2.1 `PlayerId`, `SeatId`, `PlayerState`, `GamePhase`, `SessionState` und einen dynamischen `IReadOnlyList<Player>`-Roster einführen. _(erledigt: `Seat`, `Presence`, `RoomState`, Roster in `Session`)_
- [ ] 2.2 Pro Spiel einen serialisierbaren, render-unabhängigen `GameState` und validierte `GameIntent`-Befehle definieren.
- [ ] 2.3 Zufall hostautoritativ und reproduzierbar machen; Seed allein genügt nicht als Reconnect-Grundlage, daher Zufalls-/Deckfortschritt im Snapshot halten.
- [ ] 2.4 Renderer/Unity-Szenen nur aus dem Domänenzustand ableiten; Animationen dürfen keine fachliche Wahrheit halten.

### 3. Mehr-Peer-Bluetooth-Transport und Sitzung bauen

- [x] 3.1 Android-Plugin von einem `BluetoothSocket` zu einer verwalteten Peer-Collection umbauen: weiter akzeptieren bis Kapazität 4, pro Socket Reader/Writer, thread-sichere Ereigniswarteschlange und kontrolliertes Schließen. Stand: `GlamourBt.java` kompiliert (javac, android-35); Geräte-Test folgt.
- [ ] 3.2 Windows-Transport mit derselben Mehr-Peer-Semantik anpassen und beide Plattformen gegen dieselben Transport-Vertragstests prüfen.
- [x] 3.3 `Link` in `SessionManager` aufteilen: Transport, Protokollcodec, Roster, Zustandsreplikation und Szenenadapter separieren.
- [x] 3.4 Versioniertes Envelope, ACK/Retry-Strategie, Begrenzung für Nachrichtengröße und Eingabevalidierung implementieren.
- [x] 3.5 Snapshot- und Delta-Replikation mit Revisionen implementieren; bei Lücke oder ungültigem Delta zwingend Snapshot nachfordern.

### 4. Lobby und Nutzerführung auf vier Spieler erweitern

- [x] 4.1 Neue Lobby mit Teilnehmerkarten, Sitznummer/Farbe, Bereitschaft, Kapazität und Host-Steuerung gestalten. Stand: Host-Ansicht auf dem Pixel 10 geprüft (1/4, Karten, Platz frei); Gast-Ansicht mit zweitem Gerät noch offen.
- [ ] 4.2 Verbinden, Beitreten, voll, inkompatible Version, Berechtigung fehlt, Bluetooth aus, Timeout und Reconnect als unterscheidbare, handlungsorientierte UI-Zustände darstellen.
- [x] 4.3 Im laufenden Spiel Netzstatus je Spieler zeigen, ohne Kernaktionen zu verdecken; Barrierefreiheit durch Farbe plus Symbol/Text sicherstellen. _(Link.DrawStatus: Streifen oben mit Name + online/wartet in Farbe, für Sitzspiele ab 3 Spielern; auf Gerät noch nicht gesehen)_
- [ ] 4.4 Querformat-Layouts für 2/3/4 Sitze definieren und bei Mindestauflösungen testen; keine zusätzlichen festen Zwei-Platz-Koordinaten einführen.

### 5. Spiele in sinnvoller Reihenfolge migrieren

- [x] 5.1 **Kniffel:** dynamische Scoreboard-Spalten, zyklische Turns, hostvalidiertes Würfeln/Halten/Eintragen, Snapshot der Würfel und aller Tabellenfelder. Geprüft: 3 Preview-Prozesse (Host + 2 Gäste) über TCP und die echte Sitzungslogik, Würfe/Eintragen identisch bei allen.
- [x] 5.2 **Memory:** dynamische Punktetafel, nur aktiver Sitz deckt auf, Host entscheidet Paar/Turn, Snapshot von Karten, offenem Paar, Punkten und Zug. Geprüft wie 5.1; Kartenlage nur beim Host, Symbole erst beim Aufdecken.
- [x] 5.3 **Snake:** bis zu vier Schlangen mit Sitzfarbe, Input-Puffer je Spieler, fester Host-Tick, regelmäßige Snapshots und clientseitige visuelle Glättung ohne lokale Simulation als Wahrheit. Geprüft: 4 Preview-Prozesse (Host + 3 Gäste), identisches Ergebnis auf allen; Zustand je Host-Schritt als Snapshot, Gäste simulieren nicht.
- [x] 5.4 **Blackjack:** `Hand[]`/Positionsarray dynamisch machen; Einsätze und Spielzüge pro aktivem Sitz; Deck und Dealer ausschließlich hostautoritativ; 2–4-Spieler-Tischlayout. _(umgesetzt: 2–4 Sitze, hostautoritativ; getestet mit 3 Prozessen, komplette Runde identisch auf allen Clients)_
- [x] 5.5 **Poker:** feste Drei-Platz-Annahmen zuerst eliminieren; 2–4 aktive Menschen, korrekte Blinds/Button/Side-Pots und private Hole-Card-Nachrichten; anschließend Abbruch-/Reconnectregeln testen. _(umgesetzt: 2–4 Sitze, Host rechnet Karten/Pot/Side-Pots, Hole Cards nur per EmitTo; getestet mit 3 und 4 Prozessen, Gäste kennen nur eigene Karten bis zum Showdown)_

### 6. Grafikqualität und Performance absichern

- [ ] 6.1 Für jede 2/3/4-Spieler-Szene Profiling auf Zielgeräten durchführen: Renderzeit, Main Thread, GC, Draw Calls, GPU-Speicher und Temperaturentwicklung.
- [ ] 6.2 Zusätzliche Karten, Chips, Würfel und Partikel poolen; Effekte bei vier Spielern über ein Qualitätsbudget drosseln statt die Lesbarkeit zu reduzieren.
- [ ] 6.3 Die bestehende adaptive Renderauflösung um ein sichtbares Qualitätsprofil ergänzen und bei Skalierung <1,0 Text-/Touch-Lesbarkeit testen.
- [ ] 6.4 Bloom/HDR/3D-Rigs je Szene gegen Messwerte justieren, nicht global auf Verdacht reduzieren.

### 7. Code-Cleanup und Qualitätssicherung

- [ ] 7.1 Die großen Spielszenen schrittweise in Regeln/State, Netzwerkadapter, Eingabe und Rendering zerlegen, beginnend mit Poker und Blackjack.
- [x] 7.2 Protokoll- und Spiellogik-Tests ohne Unity-Renderer ergänzen: Serialisierung, ungültige Befehle, Zugreihenfolge, Idempotenz, Snapshot/Delta und verdeckte Pokerdaten. _(erledigt: `Tools/Tests` – Wire, Session, Transport-Vertrag, Poker-Regeln; 251 Checks, 0 Fehler)_
- [x] 7.3 Simulierte Mehr-Peer-Integrationstests auf dem vorhandenen Preview-/TCP-Ansatz aufbauen: 1 Host + 1/2/3 Gäste, Verzögerung, Duplikate, Out-of-order und Disconnects. Umgesetzt als In-Memory-Simulation (`Tools/Tests/MemTransport.cs`, 1 Host + 1/2/3 Gäste, Verzögerung, Duplikate, Umordnung, Disconnects); TCP-Variante `Tools/Preview/TcpBt.cs` ist auf Mehr-Peer umgestellt.
- [ ] 7.4 Geräte-Regressionen durchführen: Android-Versionen ab dem dokumentierten Minimum API 26, echte Bluetooth-Adapter sowie Windows; Ergebnisse pro Spiel und Teilnehmerzahl protokollieren.
- [x] 7.5 Code-Review-Checkliste etablieren: keine festen Sitzanzahlen, keine globale Ein-Peer-Wahrheit, alle Eingaben validiert, keine Geheimdaten im Broadcast, jeder Zustand snapshotfähig. _(Checkliste in docs/Mehrspieler-Spezifikation.md, Abschnitt 5)_

## Abnahmekriterien

- [ ] Eine Lobby kann zuverlässig einen Host und ein bis drei Gäste zu einer Sitzung mit höchstens vier Personen verbinden.
- [x] Jeder Teilnehmer sieht denselben öffentlichen Zustand in Kniffel, Blackjack, Poker, Memory und Snake; der Host kann Desync durch Snapshot korrigieren. _(per Mehrprozess-Läufe über TCP mit echtem Session-/Spielcode belegt: Memory, Kniffel, Snake, Blackjack, Poker; echtes Bluetooth mit 3–4 Geräten nicht getestet)_
- [x] Private Pokerkarten sind ausschließlich beim zuständigen Spieler sichtbar und nicht im Broadcast enthalten. _(geprüft per Prozess-Dump: Gäste halten nur Platzhalter für fremde Karten)_
- [ ] Trennen eines Gasts führt zu verständlichem Status und regelkonformer Pause/Resume/Abbruch, nicht zu stillen, widersprüchlichen Spielständen.
- [ ] Ein erneuter Beitritt innerhalb des definierten Fensters stellt Spieler, Sitz und aktuellen Zustand wieder her.
- [ ] Der Host bleibt mit vier Teilnehmern innerhalb der vorab festgelegten FPS-/Speicherziele auf repräsentativen Android-Geräten.
- [ ] Alle automatisierten Protokoll-/Regeltests und die dokumentierte Geräte-Matrix bestehen.

## Verifikationsstand (Android, 08.10.2026)

- Geprüft: Unity-Build ohne Fehler, APK (Release-Signatur) auf Pixel 10 installiert, App startet, Blackjack/Poker (Hotseat) laufen, Lobby-Host-Ansicht; 195 Konsolentests grün; Mehrprozess-Läufe (3–4 Prozesse) für alle fünf Sitzspiele; Pokerkarten fremder Sitze in Gast-Prozessen nur als Platzhalter.
- Nicht geprüft (offen, absichtlich nicht abgehakt): echtes Bluetooth mit 3–4 Geräten, Geräte-Matrix (7.4), Profiling/Qualitätsprofil (Abschnitt 6), Windows-Transport mit echtem Adapter (3.2), Zerlegung der Großszenen (7.1), Domänenmodell (2.x), Reconnect eines Gasts mitten im Spiel auf Geräten.

### Restliche offene Punkte (Stand 08.10.2026, ehrlich)

Nicht abgehakt, weil ohne Hardware/Messung nicht nachweisbar:

- 3-4 echte Geräte per Bluetooth (Kapazität, Lobby mit 3 Gästen, Reconnect real, Akzeptanzkriterien Host/FPS)
- Geräte-Matrix API 26+, Windows-Adapter (7.4, 3.2: `WinBt` hat keinen Vertragstest mit echtem Adapter)
- Profiling/Qualitätsbudget (6.1-6.4, 38), Pooling von Effekten
- 2.2/2.4 und 7.1: Spiele halten Zustand weiterhin teils in der Szene; Zerlegung nur begonnen
- 4.2/4.4: Fehlertexte vorhanden (`Transport.cs`), eigene UI-Zustände und Mindestauflösungs-Test nicht vollständig geprüft
Abgehakt in dieser Runde: 21, 58, 2.1, 7.2.
