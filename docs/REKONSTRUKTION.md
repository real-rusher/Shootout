# Shootout: Rekonstruktion

Wie **Shootout** aus dem Build zurueckgewonnen wurde, wie das Original funktioniert und welche Fehler bekannt sind. Shootout ist ein kleiner LAN-Multiplayer-Shooter in Unity, den Schueler fuer die Schulrechner gebaut haben. Vorlage war ein gemoddeter Build (`Shootout_modded1.1`, Entwicklername im Build: „CubexDev“, Build vom Dezember 2024). Quellcode und Unity-Projekt lagen nicht vor. Alles hier wurde aus dem Build zurueckgewonnen: Code per ILSpy dekompiliert, Assets und Szenendaten mit UnityPy ausgelesen.

Lies diese Datei komplett, bevor du Code aenderst. Die Regeln fuer die Arbeit im Repo stehen in `CLAUDE.md`.

## Feste Regeln

1. **Das Easter-Egg-Foto bleibt draussen.** Im Original hing in der Info-Raum-Map ein verfremdetes Foto echter Personen (Sprite `InfoEasterEgg` auf dem GameObject `Square` mit SpriteRenderer). Es ist aus allen Dateien hier entfernt: kein Bild, kein Eintrag in `docs/hierarchy.txt`, kein Mesh im GLB. Im Map-Vorschaubild `Assets/Sprites/InfoRaum.png` war es seitlich angeschnitten zu sehen, diese Stelle ist uebermalt. Nicht wieder einbauen, auch nicht als Platzhalter, und keine anderen Fotos echter Personen ins Spiel holen.
2. **Keine Zugangsdaten ins Projekt.** Der Original-Build enthielt in `GSTU_Config` (Google-Sheets-Plugin) einen Google-API-Key und ein Refresh-Token. Beides ist hier nicht enthalten. Falls jemand das Plugin wieder einbaut: Schluessel nie committen.
3. **Code-Konventionen:** Kommentare und Commit-Nachrichten auf Deutsch, ohne Umlaute (ae, oe, ue, ss).
4. Antworten an den Besitzer: kurz, auf Deutsch, ohne Gedankenstriche.

## Ordner

| Pfad | Inhalt |
|---|---|
| `Assets/Scripts/` | Spielcode, bereinigt und kompilierbar (siehe unten). Hier wird weitergearbeitet. |
| `docs/dekompiliert_roh/` | Unveraenderte ILSpy-Ausgabe der Spielskripte, nur als Referenz. Enthaelt den vom Netcode-Compiler erzeugten RPC-Code und `OnlineLobbies.cs`. |
| `Assets/Models/*.glb` | Maps, Spieler und Laser als glTF-Binaerdateien mit Hierarchie und Materialfarben. |
| `Assets/Sprites/*.png` | UI-Grafiken: Fadenkreuz, Hitmarker, Icons, Map-Vorschaubilder. |
| `Assets/Input/Shootout.inputactions` | Input-System-Asset (Maps „Player“ und „UI“), aus dem Build rekonstruiert. |
| `docs/hierarchy.txt` | Komplette Szenen- und Prefab-Hierarchie mit Komponenten und Layern. |
| `docs/ui_layout.json` | Ganzes UI (Canvas): RectTransforms, Farben, Sprites, Texte, Schriftgroessen und welche Methode jeder Button aufruft. |
| `docs/ui_layout_PlayerRowPrefab.json` | Prefab einer Scoreboard-Zeile. |
| `docs/werte.json` | Alle Tuning-Werte aus Prefabs und Szene, Spawn-Zonen, Lichter, Layer, Netzwerk-Einstellungen. |

## Technik des Originals

- **Unity 2021.3.4f1**, Built-in Render Pipeline, Standard-Shader mit einfarbigen Materialien, keine Texturen auf den Modellen.
- **Pakete:** Netcode for GameObjects 1.x (genaue Version liess sich nicht auslesen, Stil passt zu 1.0.x), Unity Transport, Input System 1.3, TextMeshPro, Multiplayer Tools. ProBuilder ist eingebunden, wird im Code aber nicht genutzt.
- **Fremdcode, der nicht uebernommen wurde:** GoogleSheetsToUnity (Plugin fuer eine Online-Lobbyliste, im Spiel nur halb angefangen: `OnlineLobbies.cs` liest eine Tabelle und loggt sie, mehr nicht).
- **Kein Audio.** Der Build enthaelt keine einzige AudioClip-Datei.
- Nur eine Szene (`SampleScene`).

## Spielkonzept

Jeder gegen jeden in einem nachgebauten Raum. Ein Treffer mit dem Laser toetet sofort. Kein Rundenende, keine Teams. Gezaehlt werden Kills und Deaths.

- **Steuerung:** WASD oder Pfeiltasten, Maus zum Umsehen, Linksklick schiessen, Leertaste springen, Escape fuer Pause. Gamepad ist in den Input Actions mit drin.
- **Waffe:** Hitscan-Raycast aus der Kamera mit unendlicher Reichweite. Sichtbar ist nur ein blauer Strahl (`Laser`-Prefab), der 0,3 s stehen bleibt und vom `Laserpointer` an der Kamera zum Trefferpunkt geht.
- **Tod:** Kamera zoomt langsam heraus, Death-Screen „Killed by [Name]“ mit „(press space to respawn)“. Der Spieler wird schon beim Tod an einen zufaelligen Spawnpunkt teleportiert und ist unsichtbar, bis er Leertaste drueckt.
- **HUD:** Fadenkreuz, Hitmarker (0,5 s), „Kills: x“ und „Deaths: x“, Abklingbalken. Im Pause-Menue: Lobbycode, Button zum Kopieren, Optionen, Verlassen und das Scoreboard (sortiert nach Kills, dann wenigen Deaths, mit K/D).
- **Einstellungen:** Mausempfindlichkeit (Slider, Standard 3) und Name aendern funktionieren. „Show FPS“, „Windowed/Fullscreen“ und der Sound-Tab haben im Original **keine Funktion** (keine Events verdrahtet).

### Maps

| chosenMap | Name im Menue | Prefab-Feld | GameObject-Wurzel | GLB |
|---|---|---|---|---|
| 1 | Zimmer (`BedRoomBtn`) | `Manager.oldMapPrefab` | `Shooter-Map` | `Map_Zimmer.glb` |
| 2 | Info-Raum (`InfoRoomBtn`) | `Manager.newMapPrefab` | `MessyRoomparts` (Skalierung 0,8) | `Map_InfoRaum.glb` |

Achtung, die GameObject-Namen sind vertauscht und irrefuehrend. Massgeblich ist die Tabelle.

**Spawn-Zonen:** Die BoxCollider (Trigger) direkt an der Map-Wurzel sind die Spawn-Bereiche. `Spawnlocation.convertCollToBox` liest deren `bounds` und waehlt einen zufaelligen Punkt in einer zufaelligen Box. Werte in `docs/werte.json`.

### Wichtige Tuning-Werte

| Wert | Original |
|---|---|
| Laufgeschwindigkeit | 10 m/s |
| Sprunggeschwindigkeit | 20 (bei Schwerkraft 9,81 × mass 4 ergibt das etwa 5 m Sprunghoehe) |
| Bodenbeschleunigung / Luftanteil | 10 / 0,2 |
| Jump-Buffer (prejumpTime) | 0,1 s |
| Abklingzeit Laser (Prefab) | 0,7 s, wird durch den Mod aber ignoriert |
| Death-Zoom | Geschwindigkeit 3, Distanz 3 |
| Netcode Tickrate | 60 |
| Port | 7777 (UDP) |

## Architektur

Alles haengt in einer Szene. Singletons ueber `static Instance`.

- **`Manager`** (auf „Game Manager“): Spielzustand (`Lobby`, `Game`, `PausedGame`, `Settings`), wechselt Input-Action-Maps und Cursor, startet Host/Client, instanziert beide Maps beim Start und aktiviert die gewaehlte.
- **`ConnectionManager`**: Startet NetworkManager als Host oder Client. Haelt `chosenMap` als NetworkVariable. Wartet nach Verbindung auf die Map, baut Spawn-Boxen und spawnt den eigenen Spieler.
- **`PlayersManager`**: Liste aller Spieler, macht Namen eindeutig („Name #1“).
- **`Playermanager`** (Player-Prefab): NetworkVariables fuer Name, Kills, Deaths, isDead (alle vom Owner schreibbar). Tod, Respawn, Sichtbarkeit.
- **`playermovment`**, **`playerlooking`**, **`playershooting`**: Bewegung (CharacterController), Maus-Look plus Death-Zoom, Schiessen plus RPCs.
- **`NetworkClientTransform`**: NetworkTransform mit Client-Autoritaet.
- **`UIManager`**: Menue als Stapel (`continueBranch`/`revertBranch`), Panels werden ueber GameObject-Namen gefunden. Namen in der Hierarchie also nicht umbenennen, ohne den Code anzupassen.
- **`UIGameManager`**: HUD, Pause, Death-Screen, Scoreboard.
- **`SettingsManager`**, **`playerListRow`**, **`LaserEffect`**, **`connectingAnim`**, **`ValidateUserInput`**, **`Spawnlocation`**: siehe Namen.

### Netzwerkfluss

1. **LAN-Host:** Eigene IPv4 wird ermittelt, Host startet auf Port 7777. Der „Lobbycode“ ist nur die **letzte Zahl der IP** (zum Beispiel `143`).
2. **LAN-Join:** Der Client setzt die ersten drei Zahlen seiner eigenen IP vor den Code. Funktioniert also nur im selben /24-Netz.
3. **Global:** Host holt seine oeffentliche IP von `api64.ipify.org` (kann IPv6 liefern), der Client gibt die volle IP ein. Braucht Portfreigabe, im Schulnetz praktisch nie nutzbar.
4. **Treffer:** Der Schuetze macht den Raycast lokal, erhoeht seine Kills selbst und schickt `playerShotServerRPC(shooter, victim)` am getroffenen Spieler. Der Server verteilt per `receiveShotClientRPC`, der Owner des Opfers erhoeht Deaths und stirbt. Spieler werden ueber **Namen als String** identifiziert.
5. Remote-Spieler kommen auf Layer 8 (`Enemy`), ihr Koerper auf Layer 9 (`EnemyBody`). Der eigene Spieler liegt auf Layer 2 (`Ignore Raycast`), deshalb trifft man sich nicht selbst.

## Was in `Assets/Scripts/` gegenueber dem Rohcode geaendert ist

Nur Aufraeumarbeit, Verhalten ist gleich:

- Vom Netcode-IL-Postprocessor erzeugter Code entfernt (`__getTypeName`, `__rpc_handler_*`, RPC-Tabelle, `__rpc_exec_stage`-Pruefungen). Die RPC-Methoden in `playershooting.cs` stehen wieder als normale `[ServerRpc]`/`[ClientRpc]`-Methoden da.
- `Delegate.Combine(...)` durch `+=` ersetzt, `gamestate != 0` durch `!= Manager.GameState.Lobby`.
- `OnlineLobbies.cs` und `OnSpreedSheetLoaded.cs` weggelassen (haengen am Google-Sheets-Plugin).
- Geprueft: Die Skripte kompilieren gegen die Unity-, Netcode-, Input-System- und TextMeshPro-DLLs aus dem Build. In einem echten Unity-Editor wurde noch nicht getestet.

## Bekannte Fehler und Schwaechen (Reihenfolge = Wichtigkeit)

1. **Mod:** In `playershooting.checkForShoot` steht `_timeSinceShot >= 0.01f` statt vermutlich `>= coolDown`. Ergebnis: Dauerfeuer, der Abklingbalken ist praktisch nutzlos.
2. **Endlosschleife:** `ConnectionManager.waitForChosenMap` beginnt mit `while (chosenMap.Value == 0) { }` ohne `yield`. Ist der Wert beim Client noch 0, friert das Spiel ein. Muss `yield return null` in der Schleife bekommen.
3. **Keine Server-Autoritaet:** Treffer, Kills und Deaths entscheidet jeder Client selbst. Trivial cheatbar. Fuer eine neue Version: Treffer auf dem Server pruefen, NetworkVariables nur vom Server schreiben lassen.
4. **Spieler ueber Namen statt ueber NetworkObjectId** identifiziert. Namensaenderung waehrend des Spiels kann Treffer falsch zuordnen.
5. **Singleton-Fehler:** In allen `Awake`-Methoden steht `Destroy(Instance)` statt `Destroy(gameObject)`. Es wird also das alte Objekt zerstoert (und auch nur die Komponente).
6. `playermovment._isDead` und `playerlooking._isDead` lesen immer den Zustand des **eigenen** Spielers, auch auf fremden Spielerobjekten.
7. `playershooting.playerHit` vergleicht `playershooting` mit `Playermanager`, das ist nie gleich. Funktioniert nur wegen der Layer.
8. `PlayersManager.getUniqueName` vergleicht mit `GameObject.name`, nicht mit dem Netzwerknamen.
9. Event-Abos in `Playermanager.Awake` und `OnEnable` werden nie wieder entfernt.
10. Timeouts passen nicht zusammen: `waitForConnection` gibt nach 5 s auf, `connectingAnim` nach 10 s.
11. K/D im Scoreboard wird ungerundet als Float angezeigt.
12. Show-FPS, Fenstermodus und Sound-Tab ohne Funktion, kein Audio.

Bei einer zweiten Durchsicht des Codes zusaetzlich gefunden:

13. **Ein Tod zaehlt mehrfach.** Durch das Dauerfeuer (Punkt 1) trifft der Schuetze dasselbe Opfer mehrmals, bevor dessen `isDead` bei ihm ankommt. Niemand prueft, ob das Opfer schon tot ist. Kills und Deaths steigen mehrfach, `spawn()` und `ownerPlayerDead` laufen mehrfach.
14. **Global-Host scheitert vermutlich immer.** `GlobalconnectAsHost` bindet den Transport an die oeffentliche IP. Hinter einem Router liegt die auf keiner Netzwerkkarte. Liefert ipify nichts, wird `chosenMap` trotzdem gesetzt, obwohl kein Host laeuft.
15. **Falscher Lobbycode moeglich.** `getLocalIPAdress` nimmt die erste IPv4-Adresse. Mit VirtualBox, Hyper-V oder VPN ist das der falsche Adapter.
16. **Blickrichtung nach Respawn zufaellig.** `Playermanager.spawn` setzt `LookRotation(-(0, y, 0))`, also senkrecht. `playerlooking.lookTowardsCenter` liest daraus einen sinnlosen Winkel. Gemeint war vermutlich der Blick zur Mapmitte.
17. **Spawnpunkte ungeprueft.** Zufaellig in der Box (auch in der Hoehe), ohne Pruefung auf andere Spieler in der Naehe.
18. **Fremde Spieler werden lokal bewegt.** `playermovment.Update` ruft `UpdateGravity` und damit `controller.Move` auch auf fremden Spielerobjekten auf, gegen die NetworkTransform-Synchronisation.
19. **AudioListener an jeder Spielerkamera.** Faellt ohne Audio nicht auf. Sobald Sound dazukommt, bei fremden Spielern abschalten.
20. **Kein Laser ins Leere.** Der Strahl wird nur gezeigt, wenn der Raycast etwas trifft.

## Unity-Projekt wieder aufsetzen

Im Repo liegt schon das Geruest: `Packages/manifest.json` (alle Pakete, dazu
ParrelSync), `ProjectSettings/ProjectVersion.txt` (2021.3.4f1), die Layer 8 =
`Enemy` und 9 = `EnemyBody` in `ProjectSettings/TagManager.asset`, Skripte,
Modelle, Sprites und Input Actions unter `Assets/`. Szene und Prefabs baut ein
Editor-Skript aus den Daten in `docs/` (`Assets/Editor/SzeneAufbauen.cs`).

1. Ordner im Unity Hub mit *Add project from disk* hinzufuegen und mit 2021.3.4f1 oeffnen.
   Die Pakete laedt Unity beim ersten Oeffnen selbst. Fragt Unity, ob das neue Input
   System aktiviert werden soll: **Yes** (Unity startet neu).
2. Menue **Shootout > Szene und Prefabs aufbauen**. Fehlen die TextMeshPro Essentials,
   importiert das Skript sie und bittet, das Menue noch einmal aufzurufen.
3. Ergebnis: `Assets/Scenes/SampleScene.unity` (auch in den Build Settings), Prefabs
   `player`, `Laser`, `Row`, `Map_Zimmer`, `Map_InfoRaum` in `Assets/Prefabs/`, Netz und
   Material des Lasers in `Assets/Erzeugt/`, `Assets/DefaultNetworkPrefabs.asset`.
   Alles mit den `.meta`-Dateien committen.

Was das Skript aus den Daten uebernimmt: das ganze UI mit Positionen, Farben, Texten,
Sprites und allen 28 Knopf-Aufrufen, alle Tuning-Werte, Spawn-Zonen, Lichtstaerken,
Layer, Verdrahtung der Skriptfelder. Was es **schaetzt**, weil der Build es nicht hergibt
(im Code mit `GESCHAETZT` markiert): Position der Lichter und der Menue-Kamera, Dicke
des Laserstrahls, Lage des Laserpointers an der Kamera, Groesse des Namensschilds,
Bezugsaufloesung des Canvas (1920 x 1080). Nicht nachgebaut: die Blink-Animationen der
Texte ("Press space ..."), die vier BoxCollider am Game Manager (im Code ungenutzt) und
der Inhalt des Scrollbereichs in den Einstellungen.

Ein zweiter Aufruf baut alles neu und ueberschreibt Aenderungen von Hand. Sobald die
Szene im Repo ist, wird sie im Editor weiter bearbeitet und das Skript nicht mehr gebraucht.

**Alternative zum Skript:** Mit AssetRipper (Windows) laesst sich aus dem Original-Build ein komplettes Unity-Projekt exportieren. Danach unbedingt: GameObject `Square` aus dem Info-Raum-Prefab loeschen, Sprite und Textur `InfoEasterEgg` loeschen, `GSTU_Config` leeren oder loeschen. Erst dann Szene und Prefabs in dieses Repo uebernehmen. Die Skripte von AssetRipper durch die aus `Assets/Scripts/` ersetzen, dabei die `.meta`-Dateien von AssetRipper behalten, sonst verlieren Szene und Prefabs ihre Skript-Verweise. `tools/pruefe_verbotenes.sh` blockiert den Commit, falls `InfoEasterEgg` oder `GSTU_Config` noch irgendwo stehen.

## Grenzen der Rekonstruktion

- GLBs sind ein eigener Export aus den Mesh- und Transformdaten. Achsen wurden von Unity (linkshaendig) nach glTF umgerechnet, glTFast rechnet beim Import zurueck. Masstab und Ausrichtung nach dem Import kurz pruefen.
- Materialien sind nur Grundfarbe plus Emission, Normalen und UVs fehlen im Export (Unity berechnet Normalen beim Import neu).
- Die UI-Schrift ist die TMP-Standardschrift LiberationSans SDF.
- Die NGO-Version des Originals ist unbekannt. Im Manifest steht 1.14.1, die neueste 1.x fuer Unity 2021.3. Falls der alte Code damit Fehler wirft, auf 1.0.2 zurueckgehen.
