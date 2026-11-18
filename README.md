# Shootout

Aktuell: Version 0.2.0

Kleiner LAN-Mehrspieler-Shooter in Unity: jeder gegen jeden, ein Lasertreffer
toetet sofort, zwei Maps (Zimmer und Info-Raum). Das Original haben Schueler
fuer die Schulrechner gebaut (Entwicklername im Build: "CubexDev"). Dieses Repo
ist die Weiterentwicklung. Der Quellcode lag nicht mehr vor und wurde aus dem
Build zurueckgewonnen.

Regeln fuer alle, die hier mitarbeiten (auch KI-Assistenten): `CLAUDE.md`.
Wie das Original funktioniert und welche Fehler bekannt sind: `docs/REKONSTRUKTION.md`.

## Stand

Skripte, Modelle, Sprites, Input Actions, Pakete und Layer sind da. **Szene und Prefabs
erzeugt das Menue Shootout > Szene und Prefabs aufbauen** aus den Daten in `docs/`
(Einzelheiten in `docs/REKONSTRUKTION.md`). Die erzeugten Dateien sind noch nicht im
Repo, bis jemand das Menue einmal ausfuehrt und das Ergebnis committet.

Seit 0.2.0 dazugekommen (zum Ausprobieren, wie Bewegung und Spielgefuehl zu den Ideen passen):

* **Waffen:** Gewehr (wie im Original), SMG (schnell, streut, im Laufen mehr), Sniper (langsam,
  immer genau, Zoom wie bei Counter-Strike). Werte in `Assets/Scripts/Spiel/Waffen.cs`.
  Dazu 100 Leben, den Schaden prueft der Server.
* **Aussehen:** Bild vom PC (PNG, JPG) auf die vordere Kopfhaelfte legen, mit Vorschau.
  Andere Spieler sehen es, abschaltbar unter Einstellungen > Video.
* **Teams und Bots:** Beim Erstellen einer Lobby waehlbar. Alliierte gegen Verteidiger,
  Teampunkte oben. Bots fuellen die Runde auf die gewaehlte Spielerzahl auf.
* **D-Day-Map:** Strand mit Landungsbooten, Hang mit drei Schluchten als Wegen nach oben,
  Bunker, Graeben, Feuer, Wasser, Pflanzen. Wird vom Editor-Skript erzeugt.
* **Menue:** Einstellungs-Tabs, FPS-Anzeige, Fenster/Vollbild, Lautstaerke, Maus invertieren,
  alles wird gespeichert. Einfache Toene fuer Schuesse und Treffer.

### Steuerung

| Taste | Wirkung |
|---|---|
| WASD, Maus, Leertaste | Laufen, Umsehen, Springen (wie im Original) |
| Linksklick | Schiessen |
| 1, 2, 3 oder Mausrad | Gewehr, SMG, Sniper |
| Rechtsklick (Sniper) | Zoom Stufe 1, Stufe 2, aus |
| Escape | Pause, Scoreboard |
| Leertaste nach dem Tod | Neu spawnen |

Namen mit `!ad` schiessen mit jeder Waffe im Dauerfeuer (Dev-Funktion).

## Aufbau

Das Unity-Projekt liegt direkt im Wurzelordner:

| Pfad | Inhalt |
|---|---|
| `Assets/Scripts/` | Spielcode (bereinigt aus dem Build), `Netz/Spielversion.cs` neu |
| `Assets/Models/`, `Assets/Sprites/`, `Assets/Input/` | Maps, Spieler, Laser als GLB, UI-Grafiken, Input Actions |
| `Packages/`, `ProjectSettings/` | Unity-Pakete und Projekteinstellungen |
| `docs/REKONSTRUKTION.md` | Technik, Architektur, Netzwerkfluss, bekannte Fehler |
| `docs/hierarchy.txt`, `docs/*.json` | Szenen- und Prefab-Hierarchie, UI-Layout, Tuning-Werte |
| `docs/dekompiliert_roh/` | Unveraenderte Ausgabe des Decompilers, nur zum Nachschlagen |
| `tools/pruefe_verbotenes.sh` | Prueft, dass kein Foto und keine Zugangsdaten im Repo landen |
| `.githooks/` | pre-commit-Hook mit dieser Pruefung, dazu die Hooks von Git LFS |

`Library/`, `Temp/`, `Logs/` und Builds erzeugt Unity selbst, sie sind nicht im Repo.

## Einrichten (einmal pro Rechner)

1. **Git LFS** installieren (bei Git fuer Windows und GitHub Desktop schon dabei), dann:
   ```
   git lfs install
   git clone https://github.com/real-rusher/Shootout.git
   cd Shootout
   git config core.hooksPath .githooks
   ```
   Der letzte Befehl schaltet die Pruefung vor jedem Commit ein. In `.githooks/` liegen
   auch die Hooks von Git LFS, damit Bilder und Modelle danach weiter hochgeladen werden.
2. **Unity 2021.3.45f2** ueber den Unity Hub installieren (Eingabeaufforderung:
   `start unityhub://2021.3.45f2/88f88f591b2e`). Nicht 2021.3.4f1 nehmen, mit der das
   Original gebaut wurde: Unity meldet dafuer eine Sicherheitsluecke in gebauten Spielen,
   45f2 ist die behobene Version derselben Reihe. Danach im Hub *Add project from disk* und den Ordner waehlen.
   Beim ersten Oeffnen laedt Unity alle Pakete. Die Frage nach dem neuen Input System mit
   **Yes** beantworten.
3. In Unity pruefen: *Project Settings > Editor*: Asset Serialization = **Force Text**,
   Version Control = **Visible Meta Files**.
4. **Szenen-Merge** einrichten, damit Konflikte in Szenen und Prefabs meist von
   selbst aufgehen. In `.git/config` des Klons eintragen (Pfad an die eigene
   Unity-Installation anpassen):
   ```
   [merge "unityyamlmerge"]
       name = Unity SmartMerge
       driver = 'C:/Program Files/Unity/Hub/Editor/2021.3.45f2/Editor/Data/Tools/UnityYAMLMerge.exe' merge -p %O %B %A %A
       recursive = binary
   ```
5. **Mehrspieler im Editor testen:** ParrelSync ist schon im Manifest. Menue
   *ParrelSync > Clones Manager*, einen Klon anlegen und oeffnen. Dann laufen Host und
   Client gleichzeitig auf einem PC.

## Arbeitsablauf

1. Neuen Branch anlegen, eine Sache pro Branch.
2. Aendern, im Editor mit zwei Instanzen testen.
3. Version in `Assets/Scripts/Netz/Spielversion.cs` erhoehen und unten eintragen.
4. Pull Request nach `main`. GitHub Actions prueft dort noch einmal auf Foto und Schluessel.

Wenn etwas nicht geht: Unity-Konsole oder Logdatei mitschicken. Logdatei eines Builds
unter Windows: `%USERPROFILE%\AppData\LocalLow\<Firma>\<Spiel>\Player.log`.

## In der Schule spielen

* Windows-Build als ZIP mit Versionsnummer im Namen verteilen (USB-Stick, Netzlaufwerk).
* Alle muessen dieselbe Version haben, der Host lehnt sonst ab.
* Host und Client brauchen UDP-Port 7777 frei in der Firewall und muessen im selben
  /24-Netz sein (der Lobbycode ist die letzte Zahl der IP).
* In der Schule immer **Host Local / Join Local** nehmen. Dieser Weg ist von den Admins
  freigeschaltet und bleibt im Code unveraendert.

## Online spielen (ausserhalb der Schule)

**Host Global / Join Global** laeuft ueber Unity Relay: Host und Gaeste verbinden sich mit
einem Server von Unity, niemand braucht eine Portfreigabe. Der Host bekommt einen Join-Code
aus sechs Zeichen (im Pausemenue als Lobbycode, mit Kopierknopf), die anderen geben ihn bei
Join Global ein. Bis zu 10 Spieler pro Runde.

Einmal einrichten (Besitzer des Projekts):

1. Im Unity-Editor mit dem Unity-Konto anmelden.
2. *Edit > Project Settings > Services*: Projekt mit einer Unity-Cloud-Projekt-ID verknuepfen
   (neu anlegen oder vorhandene waehlen). Die ID landet in `ProjectSettings/ProjectSettings.asset`
   und ist kein Geheimnis, sie darf ins Repo.
3. Auf cloud.unity.com im Projekt **Relay** aktivieren (Bereich Multiplayer). Relay hat ein
   kostenloses Kontingent, die genauen Bedingungen stehen im Dashboard.
4. Neu bauen. Builds ohne verknuepftes Projekt zeigen bei Global "unable to connect" und
   schreiben die Ursache ins Log.

Ob Relay im Schulnetz durchkommt, haengt von der Firewall ab. Fuer die Schule ist LAN gedacht.

## Versionen

Die neueste Version steht oben. "Netz" heisst: Host und Client muessen diese Version beide haben.

| Version | Aenderung |
|---|---|
| 0.2.0 | **Netz:** Drei Waffen, Leben und Schaden (Server prueft Treffer, Schuetze per Objekt-ID), Teams (`Runde`, NetworkVariables `team`, `leben`), Bots (eigenes Prefab, laufen auf dem Host), Kopfbild (wird in Stuecken uebertragen), D-Day-Map (Map 3). Menue ausgebaut (Einstellungen, Aussehen, Host-Optionen), HUD mit Leben, Waffe, Teampunkten, FPS, Zielfernrohr. Behoben: Bugs 6, 7, 11, 16, 18, 19, 20 aus `docs/REKONSTRUKTION.md`, fehlende Maps nach Verbindungsabbruch, alte Asset-Kopien nach erneutem Aufbau (unsichtbarer Laser). LAN-Verbindungscode unveraendert. Szene neu aufbauen. |
| 0.1.8 | Dauerfeuer nur noch fuer Spieler mit `!ad` im Namen (Gross- und Kleinschreibung egal, Dev-Funktion). Alle anderen schiessen mit der Abklingzeit aus dem Prefab (0,7 s), wie vor dem Mod. Der Abklingbalken richtet sich nach der jeweils geltenden Zeit. |
| 0.1.7 | **Netz:** Laser der anderen Spieler wurden nicht oder falsch angezeigt (Treffer zaehlten trotzdem). `laserEffectServerRPC`/`laserEffectClientRPC` schicken jetzt den Trefferpunkt statt der Entfernung, jeder PC zeichnet vom Laserpointer des Schuetzen dorthin. Der Schuetze sieht seinen Laser sofort, ohne Umweg ueber den Server. |
| 0.1.6 | **Netz:** LAN-Beitritt ging nicht. Netcode 1.14 lauscht bei leerer "Server Listen Address" nur auf 127.0.0.1, das Original (Netcode 1.0.x) auf der LAN-IP. Das Editor-Skript setzt jetzt 0.0.0.0 (alle Adressen des PCs, Port weiter 7777). LAN-Code unveraendert. Dazu: Canvas skaliert nach der Breite, Lobbycode ueberlappt nicht mehr mit "(press escape to continue)". |
| 0.1.5 | Editor-Version 2021.3.45f2 statt 2021.3.4f1 (Unity meldet fuer 4f1 eine Sicherheitsluecke in gebauten Spielen). `ProjectVersion.txt` mit Revision, damit der Hub die Version erkennt. Spielcode unveraendert. |
| 0.1.4 | **Netz:** Ein Abschuss zaehlt nur noch einmal, auch bei Dauerfeuer. Der Server nimmt pro Leben nur den ersten Treffer an und schreibt erst dann dem Schuetzen den Kill gut (neues RPC `killBestaetigtClientRPC`, `playerShotServerRPC` bekommt `ServerRpcParams`). Der Hitmarker erscheint deshalb erst nach der Bestaetigung. Dauerfeuer selbst bleibt. |
| 0.1.3 | **Netz:** Online-Runden ueber Unity Relay (Host Global / Join Global mit Join-Code), Pakete `com.unity.services.relay` 1.2.0 und `com.unity.services.authentication` 2.7.4. Die alte Global-Verbindung ueber die oeffentliche IP ist raus. LAN unveraendert, nach einer Online-Runde stellt `ConnectionManager` den Transport zurueck auf direktes UDP. Braucht einmal die Einrichtung unter "Online spielen". |
| 0.1.2 | Editor-Skript `Assets/Editor/SzeneAufbauen.cs` (Menue Shootout > Szene und Prefabs aufbauen): baut Szene, UI und die Prefabs `player`, `Laser`, `Row` und beide Maps aus `docs/`. Ungetestet in Unity, aber gegen TextMeshPro und Input System kompiliert. **Netz:** Szene und Player-Prefab entstehen neu, Host und Client brauchen denselben Stand. |
| 0.1.1 | Inhalt der Rekonstruktion uebernommen: Skripte, Modelle, Sprites, Input Actions, `docs/`. Unity-Geruest: `Packages/manifest.json` (Netcode 1.14.1, Input System 1.7.0, glTFast, ParrelSync), Unity 2021.3.4f1, Layer `Enemy` und `EnemyBody`. Acht weitere bekannte Fehler in `docs/REKONSTRUKTION.md`. |
| 0.1.0 | Repo eingerichtet: `.gitignore`, `.gitattributes` mit LFS und Unity-Merge, Pruefung auf Foto und Schluessel (Hook und GitHub Actions), `CLAUDE.md`. **Netz:** Versionspruefung beim Verbinden (`Spielversion.cs`, noch ungetestet, kommt auf das NetworkManager-Objekt). |
