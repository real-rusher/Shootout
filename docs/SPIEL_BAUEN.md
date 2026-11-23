# Spiel automatisch bauen (GitHub)

Bei jeder neuen Version auf `main` oder `claude/ausbau` baut GitHub das Spiel fuer Windows
(`.github/workflows/spiel_bauen.yml`, mit GameCI). Danach liegt `Shootout_Windows.zip`

* unter **Releases** (rechts auf der Repo-Seite), eine pro Version,
* auf dem Branch **fertiges-spiel** (immer nur der neueste Build).

Wer spielen will: ZIP herunterladen, entpacken, `Shootout.exe` starten. Kein Unity, kein git.
Beim ersten Start die Firewall-Frage mit "Zulassen" beantworten, sonst geht LAN nicht.

## Einmal einrichten: Unity-Lizenz als Secrets

Unity braucht auch auf dem Server eine Lizenz. Die kostenlose Personal-Lizenz reicht. Die Werte
kommen als **Secrets** in die Repo-Einstellungen, nie in eine Datei im Repo.

1. Auf dem PC, auf dem Unity laeuft (mit Unity Hub angemeldet), die Datei
   `C:\ProgramData\Unity\Unity_lic.ulf` mit dem Editor oeffnen und den ganzen Inhalt kopieren.
2. Auf GitHub: Repo > **Settings** > **Secrets and variables** > **Actions** >
   **New repository secret**, drei Stueck:
   * `UNITY_LICENSE`: der kopierte Inhalt der .ulf-Datei
   * `UNITY_EMAIL`: E-Mail des Unity-Kontos
   * `UNITY_PASSWORD`: Passwort des Unity-Kontos (bei Anmeldung ueber Google vorher auf
     id.unity.com ein Passwort setzen)
3. **Actions** > **Spiel bauen** > **Run workflow** (Branch `claude/ausbau`). Der erste Lauf
   dauert 30 bis 60 Minuten, spaetere gehen schneller.

Ohne Secrets endet der Lauf gruen mit dem Hinweis "Unity-Lizenz fehlt".

## Was der Build macht

1. `tools/tmp_entpacken.py` legt die TextMeshPro-Grundausstattung an (lokal fragt Unity danach).
2. `CiBau.Bauen` (Assets/Editor) setzt Produktname und Input System, ruft den Szenenaufbau auf
   (wie das Menue "Shootout > Szene und Prefabs aufbauen") und baut `Shootout.exe`.
3. ZIP packen, als Release hochladen, Branch `fertiges-spiel` ersetzen.

Gebaut wird mit Unity 2021.3.45f1 (fuer f2 gibt es kein fertiges Abbild, gleiche Version).
