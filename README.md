# Shootout

Aktuell: Version 0.4.3

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
* **Aussehen:** Bild vom PC (jedes Format, das Windows oeffnen kann) auf die vordere Kopfhaelfte legen, mit Vorschau.
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
| Escape | Pause, Scoreboard, schliesst Einstellungen |
| Einstellungen > Steuerung | Third Person ueber die rechte Schulter |
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
| 0.4.3 | D-Day-Meer: ein einziges Wassernetz bis zum Horizont (innen 4 m, aussen immer groeber), Wellen nur im nahen Bereich; das zweite ferne Netz (0.4.0) war nicht zu sehen und ist entfernt. Nebel im fertigen Spiel: Nebel-Varianten der Shader werden behalten (Unity hatte sie beim Bauen weggelassen). Landungsboote liegen auf dem Grund, im Boot wird kein Wasser gezeichnet und man gilt dort nicht als im Wasser (`Spiel/WasserAusschnitt.cs`). Kein neuer Netzcode. Szene neu aufbauen. |
| 0.4.2 | Helligkeit: Sonne 1,0, Aufhelllicht 0,3, Umgebungslicht und Nachbearbeitung (Belichtung, Bloom) zurueckgenommen, Gras glaenzt weniger, helle Texturen dunkler (vorher ueberstrahlt). Wasser dunkler und deckender. Schatten mit festen Kacheln (StableFit), kein Wandern mehr. Info-Raum hell: viel Umgebungslicht, 20 Lampen, Deckenlicht mit schwachen Schatten, Raumhuelle wirft keinen Schatten. Bunker: Scharten 1,1 bis 2,3 m (Augenhoehe 1,88), Hang davor wieder frei. Landungsboote schwimmen (Deck ueber den Wellen). Himmelsinseln: neue Inseln mit unregelmaessiger Grasdecke, Felszapfen mit Schichten, Wurzeln. Bots weichen beim Festhaengen eine Weile aus, nach dreimal ein Umweg. Teams heissen ausserhalb von D-Day Gruen und Blau. Zielfernrohr auch oben und unten schwarz. Ultra-Gras weiter sichtbar und mit Schatten. Kein neuer Netzcode. Szene neu aufbauen. |
| 0.4.1 | Behoben: Der Aufbau brach beim fernen Meer ab (Ordner Erzeugt/Landschaft fehlte noch). Ordner werden jetzt beim Speichern immer angelegt. Sonst wie 0.4.0. Szene neu aufbauen. |
| 0.4.0 | Grosser Grafik-Umbau, kein neuer Netzcode. Schatten: Umgebungslicht fest (im fertigen Spiel waren Schatten schwarz), eigener Himmel fuer Spiegelungen, Schatten in jeder Stufe, Figuren werfen Schatten. Waffen: Gewehr und Sniper naeher und mittiger, Strahl aus der Muendung des Modells. Sniper: Rechtsklick Zoom an/aus, Mausrad vier Stufen. Info-Raum: Lichtstreifen an der Decke, Deckenlicht mit weichen Schatten. Relief fuer alle Mauern und Boeden (Shader `Shootout/Weltraster`), neue Texturen fuer Sandstein, Schalbeton, Estrich. Tunnel: Betonrippen, Sockel, Kabeltrassen, Pfuetzen, Roste. Wuestenstadt: Fenster mit Sims und Laeden, Holzbalken, Zinnen, Pflanzen, Waescheleinen, Laternen, Staub. Landschaft um die Maps (`Editor/SzeneAufbauen.Landschaft.cs`): Kueste, Huegel und Berge um D-Day, Meer bis zum Horizont, Duenen und Felsberge um die Wuestenstadt, Gipfel und Wolkenmeer bei den Himmelsinseln. D-Day (`Editor/SzeneAufbauen.DDayModelle.cs`): Landungsboote nach LCVP-Vorbild, Panzerwrack, Panzerigel aus Doppel-T-Traegern, Holzrampen mit Minen, Stacheldraht, einzelne Sandsaecke, begehbare Bunker mit Schiessscharten und Schutzwand. Ultra-Gras neu: dichter Teppich, Windwellen, Trampelpfade, die sich langsam erholen (Shader `Hidden/Shootout/Trampeln`). Szene neu aufbauen. |
| 0.3.0 | Schatten: Nur die Hauptsonne wirft Schatten, in "Hoch" bekam auch das Aufhelllicht welche (Schatten aus der falschen Richtung, v. a. Wuestenstadt). Sonne am Himmel fest auf die Hauptsonne. Wasser: neuer Shader mit laufenden Wellen und Kraeuselung pro Pixel, helle Kaemme, feineres Netz (4 m) und hoehere Duenung, auch in der normalen Stufe. D-Day-Boden: Farbtextur 1024, Detailtextur mit Koernung, Steinchen und Relief. Ultra-Gras: ganze gruene Flaeche bedeckt (Raster 0,5 m), nah dicht, hinten duenner, Windboeen, weicht Spielern und Bots aus (`Spiel/GrasDruck.cs`). Kein neuer Netzcode. Szene neu aufbauen. |
| 0.2.9 | Automatischer Windows-Build auf GitHub (`.github/workflows/spiel_bauen.yml`, `Editor/CiBau.cs`, `tools/tmp_entpacken.py`): fertige `Shootout_Windows.zip` unter Releases und auf dem Branch `fertiges-spiel`. Einrichten: `docs/SPIEL_BAUEN.md`. Spiel selbst unveraendert. |
| 0.2.8 | Neue Grafikstufe "Ultra" (Einstellungen > Video, schliesst "Hoch" ein, `Editor/SzeneAufbauen.Ultra.cs`): Baeume aus Aesten und Blattbueschen, Buesche und Palmkronen mit Wind (Shader `Shootout/Laub`), einzelne Grashalme per Instancing (`Spiel/GrasFeld.cs`, Shader `Shootout/Gras`), Feuer aus Flammen-Daumenkino mit Kern, Funken und dichtem Rauch, Wasserspritzer bei Schuessen ins Wasser, beim Hineinspringen und Waten (`Spiel/UltraEffekte.cs`, `Spiel/WasserSpuren.cs`), Muendungsfeuer mit Licht, Waffenmodelle fuer Gewehr, SMG und Sniper statt des Rohrs (`Spiel/WaffenModell.cs`), Spiegelungssonden pro Map, feinere Texturen, Deferred Rendering mit Bildschirm-Spiegelungen. Kollider bleiben in allen Stufen gleich. Kein neuer Netzcode: Die Waffe anderer Spieler kommt aus dem bestehenden Schuss-RPC. Szene neu aufbauen. |
| 0.2.7 | Tunnel: Taschenlampe an jeder Figur (`Spiel/Taschenlampe.cs`, an `KartenRegeln.taschenlampe`), die eigene mit Schatten, die anderen sieht man auch. Himmelsinseln: Felsen ragten als graue Kuppeln ohne Kollider ueber die Inseln, jetzt darunter und fest. Neue Maps: Zylinder und Kugeln mit Netz-Kollider (passt bei ungleicher Skalierung), Markisen und Marktdaecher fest, keine Kisten mehr in Spawnbereichen, Eckpfosten im Tunnel, Absturz unter -20 m laesst auch in Wuestenstadt und Tunnel neu erscheinen. Szene neu aufbauen. |
| 0.2.6 | Behoben: Ohne das Paket "Post Processing" brach das Uebersetzen ab (und der Aufbau lief mit altem Code, Fehler "dDayKnopf"). Das Paket wird jetzt nur ueber Reflexion angesprochen: Fehlt es, laeuft alles, nur "Krasse Grafik" ohne Nachbearbeitung, mit Hinweis in der Konsole. Szene neu aufbauen. |
| 0.2.5 | **Netz:** Map-Nummern 4 bis 6 (Wuestenstadt, Tunnel, Himmelsinseln) im bestehenden `chosenMap`, Host und Client brauchen dieselbe Version. Drei neue Maps (`Editor/SzeneAufbauen.Karten.cs`, Shader `Shootout/Weltraster`): Wuestenstadt (eigener Grundriss mit Markt, Palasthof, Turm, Daechern), Tunnel (Gangnetz mit Halle, flackernden Lampen), Himmelsinseln (Bruecken, Sprungfelder, Absturz = neu erscheinen). Host-Fenster mit sechs Map-Knoepfen. Unter Wasser: Schleier, Nebel, gedaempfter Ton, Wasseroberflaeche von unten sichtbar. Einstellungen > Video "Krasse Grafik": Nachbearbeitung (Paket Post Processing: Bloom, Umgebungsverdeckung, ACES, Vignette, SMAA), weiche Schatten, eigener Wasser-Shader. Neues Paket, Unity laedt es beim Oeffnen. Szene neu aufbauen. |
| 0.2.4 | Knoepfe im Pausemenue (Verlassen, Einstellungen, Kopieren) reagierten nicht: Die unsichtbare Flaeche der Spielerliste fing die Klicks ab. Jetzt fangen nur Bedienelemente und deckende Fensterhintergruende Klicks, geprueft per Simulation in 15 Bildschirmzustaenden. Kopieren des Lobbycodes zeigt "kopiert", der Lobbycode-Text kopiert beim Anklicken. Szene neu aufbauen. |
| 0.2.3 | **Netz:** Team wechseln im Pausemenue (`teamWechselnServerRpc`, `aufNeueSeiteClientRpc`), Teamzuteilung bei Gleichstand zufaellig (vorher war der Host immer Alliierter). Third Person ueber die rechte Schulter (Einstellungen > Steuerung), Schuesse zielen dann auf das Fadenkreuz der Schulterkamera. Alle Knoepfe geprueft: Zurueck-Knoepfe in allen Lobby-Fenstern, "press escape to continue" und "press space to respawn" anklickbar, Escape schliesst die Einstellungen, Quit beendet im Editor Play, Fenster/Vollbild mit Hinweis "nur im fertigen Spiel". Szene neu aufbauen. |
| 0.2.2 | Kopfbild nimmt jedes Format, das Windows oeffnen kann (WebP, GIF, BMP, TIFF, ICO, HEIC und AVIF mit Store-Erweiterung): Was Unity nicht liest, wandelt Windows ueber PowerShell und WIC in PNG um (`UI/Bildumwandlung.cs`). Uebertragen wird weiter nur PNG oder JPG. |
| 0.2.1 | **Netz:** Kopfbild mit Lage (Hoehe, Groesse, Breite) in `bildBeginnen...Rpc`, Kamera-Drehung lokal und nur Neigung (Waffe anderer Spieler drehte sich seitlich nicht mit). Farben der Maps wie im Original (glTFast hatte sie aufgehellt), Ladebildschirm mit Shader-Vorbereitung, im Editor keine Hintergrund-Uebersetzung der Shader mehr (kein Blau). Kopfbild pro Spielinstallation (Klone teilen es nicht mehr), Lage einstellbar, Meldung nennt das echte Format (WebP als .png). Treffer- und Kill-Anzeige deutlicher mit Kill-Ton, rote Explosion beim Tod. Bots: gehen auf Gegner zu, nehmen auf D-Day die Schluchten, mit Einschuss-Waffen ungenauer und langsamer, Sniper seltener. Szene neu aufbauen. |
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
