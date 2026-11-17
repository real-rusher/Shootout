# Shootout

Aktuell: Version 0.1.0

Kleiner LAN-Mehrspieler-Shooter in Unity: jeder gegen jeden, ein Lasertreffer
toetet sofort, zwei Maps (Zimmer und Info-Raum). Das Original haben Schueler
fuer die Schulrechner gebaut (Entwicklername im Build: "CubexDev"). Dieses Repo
ist die Weiterentwicklung. Der Quellcode lag nicht mehr vor und wurde aus dem
Build zurueckgewonnen.

Regeln fuer alle, die hier mitarbeiten (auch KI-Assistenten): `CLAUDE.md`.

## Aufbau

Das Unity-Projekt liegt direkt im Wurzelordner:

| Pfad | Inhalt |
|---|---|
| `Assets/` | Spiel: Szenen, Skripte, Prefabs, Modelle, Sprites |
| `Packages/`, `ProjectSettings/` | Unity-Pakete und Projekteinstellungen |
| `docs/` | Rekonstruktion: Hierarchie, UI-Layout, Tuning-Werte |
| `tools/pruefe_verbotenes.sh` | Prueft, dass kein Foto und keine Zugangsdaten im Repo landen |
| `.githooks/` | pre-commit-Hook, der diese Pruefung vor jedem Commit startet |

`Library/`, `Temp/`, `Logs/` und Builds erzeugt Unity selbst, sie sind nicht im Repo.

## Einrichten (einmal pro Rechner)

1. **Git LFS** installieren (bei Git fuer Windows und GitHub Desktop schon dabei), dann:
   ```
   git lfs install
   git clone https://github.com/real-rusher/Shootout.git
   cd Shootout
   git config core.hooksPath .githooks
   ```
2. **Unity 2021.3.4f1** ueber den Unity Hub installieren (Archiv:
   unity.com/releases/editor/archive) und den Ordner als Projekt oeffnen.
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
5. **Mehrspieler im Editor testen:** Paket ParrelSync installieren
   (*Package Manager > + > Add package from git URL*:
   `https://github.com/VeriorPies/ParrelSync.git?path=/ParrelSync`).
   Damit laufen Host und Client gleichzeitig auf einem PC.

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

## Versionen

Die neueste Version steht oben. "Netz" heisst: Host und Client muessen diese Version beide haben.

| Version | Aenderung |
|---|---|
| 0.1.0 | Repo eingerichtet: `.gitignore`, `.gitattributes` mit LFS und Unity-Merge, Pruefung auf Foto und Schluessel (Hook und GitHub Actions), `CLAUDE.md`. **Netz:** Versionspruefung beim Verbinden (`Spielversion.cs`, noch ungetestet, kommt auf das NetworkManager-Objekt). |
