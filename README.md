# Shootout

Aktuell: Version 0.1.3

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

Naechste Schritte:

1. Menue ausfuehren, Ergebnis committen, Spiel mit zwei Instanzen (ParrelSync) zum Laufen bringen.
2. Fehler beheben, die das Spiel kaputt machen: Endlosschleife, Abklingzeit, Mehrfachkills, Singletons.
3. Treffer auf dem Server pruefen, Spieler ueber NetworkObjectId statt Namen erkennen.
4. Neue Features (Sound, Runden, Teams, Maps).

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
2. **Unity 2021.3.4f1** ueber den Unity Hub installieren (Archiv:
   unity.com/releases/editor/archive), im Hub *Add project from disk* und den Ordner waehlen.
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
       driver = 'C:/Program Files/Unity/Hub/Editor/2021.3.4f1/Editor/Data/Tools/UnityYAMLMerge.exe' merge -p %O %B %A %A
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
| 0.1.3 | **Netz:** Online-Runden ueber Unity Relay (Host Global / Join Global mit Join-Code), Pakete `com.unity.services.relay` 1.2.0 und `com.unity.services.authentication` 2.7.4. Die alte Global-Verbindung ueber die oeffentliche IP ist raus. LAN unveraendert, nach einer Online-Runde stellt `ConnectionManager` den Transport zurueck auf direktes UDP. Braucht einmal die Einrichtung unter "Online spielen". |
| 0.1.2 | Editor-Skript `Assets/Editor/SzeneAufbauen.cs` (Menue Shootout > Szene und Prefabs aufbauen): baut Szene, UI und die Prefabs `player`, `Laser`, `Row` und beide Maps aus `docs/`. Ungetestet in Unity, aber gegen TextMeshPro und Input System kompiliert. **Netz:** Szene und Player-Prefab entstehen neu, Host und Client brauchen denselben Stand. |
| 0.1.1 | Inhalt der Rekonstruktion uebernommen: Skripte, Modelle, Sprites, Input Actions, `docs/`. Unity-Geruest: `Packages/manifest.json` (Netcode 1.14.1, Input System 1.7.0, glTFast, ParrelSync), Unity 2021.3.4f1, Layer `Enemy` und `EnemyBody`. Acht weitere bekannte Fehler in `docs/REKONSTRUKTION.md`. |
| 0.1.0 | Repo eingerichtet: `.gitignore`, `.gitattributes` mit LFS und Unity-Merge, Pruefung auf Foto und Schluessel (Hook und GitHub Actions), `CLAUDE.md`. **Netz:** Versionspruefung beim Verbinden (`Spielversion.cs`, noch ungetestet, kommt auf das NetworkManager-Objekt). |
