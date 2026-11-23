# Hinweise fuer KI-Assistenten (Claude, Jules, Copilot ...)

Shootout: ein kleiner LAN-Mehrspieler-Shooter in Unity (2021.3, Netcode for
GameObjects 1.x), urspruenglich von Schuelern gebaut. Quellcode und Projekt
waren verloren, alles wurde aus dem Build zurueckgewonnen. Aufbau, Technik und
bekannte Fehler stehen in `README.md` und `docs/`. Lies das, bevor du Code aenderst.

## Feste Regeln

1. **Das Easter-Egg-Foto bleibt draussen.** Im Original hing in der
   Info-Raum-Map ein verfremdetes Foto echter Personen (Sprite `InfoEasterEgg`
   auf dem GameObject `Square`). Nicht wieder einbauen, auch nicht als
   Platzhalter, und keine anderen Fotos echter Personen ins Spiel holen.
2. **Keine Zugangsdaten ins Repo.** Das Google-Sheets-Plugin (`GSTU_Config`)
   enthielt einen API-Key und ein Refresh-Token. Schluessel nie committen.
   `tools/pruefe_verbotenes.sh` prueft beide Regeln als pre-commit-Hook und in
   GitHub Actions. Nie abschalten oder umgehen.
3. Kommentare, neue Namen und Commit-Nachrichten auf **Deutsch, ohne Umlaute**
   (ae, oe, ue, ss). Im Spiel angezeigter Text darf Umlaute haben.
4. Antworten an den Besitzer: kurz, auf Deutsch, ohne Gedankenstriche.

## Arbeitsweise

* `main` ist der spielbare Stand. Gearbeitet wird auf eigenen Branches, die
  per Pull Request nach `main` kommen.
* Kleine, gezielte Aenderungen, eine Sache pro Branch. Nichts nebenbei
  umbauen oder umbenennen. Vorhandene Namen (`playermovment`, `Playermanager`
  ...) bleiben, auch wenn sie seltsam sind: Szene und Prefabs haengen daran,
  und `UIManager` findet Panels ueber GameObject-Namen.
* Bei nicht offensichtlichen Entscheidungen ein Kommentar mit dem Grund.

## Was eine KI hier kann und was nicht

* In der Cloud-Umgebung gibt es **kein Unity**. Code kann geschrieben, aber
  nicht kompiliert oder gestartet werden. Der Besitzer testet im Editor.
  Deshalb im Pull Request immer sagen, was ungetestet ist und wie man es testet.
* **Szenen, Prefabs und `.asset`-Dateien nicht von Hand im YAML aendern.**
  Stattdessen eine Klick-Anleitung fuer den Editor schreiben (welches
  GameObject, welche Komponente, welcher Wert). Ausnahme: reine Zahlenwerte,
  wenn klar ist, welches Feld es ist.
* Neue Skripte bekommen ihre `.meta`-Datei von Unity. Keine `.meta`-Dateien
  erfinden, keine vorhandenen loeschen (sonst verlieren Szenen ihre Verweise).

## Wo was liegt

* Spielcode des Originals in `Assets/Scripts/` (Namen bleiben), Neues in
  `Assets/Scripts/Spiel/` (Waffen, Teams, Runde, Bots, Kopfbild, Map-Effekte) und
  `Assets/Scripts/UI/`.
* Zahlen zum Einstellen: Waffen in `Spiel/Waffen.cs`, Bots oben in `Spiel/BotSteuerung.cs`,
  Lobby-Standards in `Spiel/LobbyWahl.cs`.
* Szene und Prefabs erzeugt `Assets/Editor/SzeneAufbauen*.cs` (Teile: Grundaufbau,
  `.Erweiterung` fuer Bots, Kopfbild und neues UI, `.DDay` fuer die D-Day-Map, `.Karten` fuer
  die Maps 4 bis 6, `.Grafik` fuer die Grafikstufe "Hoch", `.Ultra` fuer "Ultra", `.Landschaft`
  fuer Gelaende und Meer um die Maps, `.DDayModelle` fuer Boote, Panzer, Sperren, Bunker). Eigene Shader in `Assets/Shaders/`. Neues UI steht
  in `Assets/Editor/UiErweiterung.json` (gleiches Format wie `docs/ui_layout.json`).
* Fertiges Spiel baut GitHub (`.github/workflows/spiel_bauen.yml`, `Editor/CiBau.cs`), Anleitung
  in `docs/SPIEL_BAUEN.md`. Neue Schritte im Szenenaufbau gehoeren in `AufbauSchritte`, damit
  Menue und Build dasselbe tun.
* "Lokal" heisst: der Mensch an diesem PC (`Playermanager.istLokal`). Bots gehoeren dem Host
  (`IsOwner`), sind aber nicht lokal. Neuer Code, der nur fuer den eigenen Spieler gilt,
  fragt `istLokal`, nicht `IsOwner`.

## Version und Netz

* Jede Aenderung bekommt eine neue Version: `Spielversion.VERSION` in
  `Assets/Scripts/Netz/Spielversion.cs` erhoehen und im `README.md` die Zeile
  "Aktuell: Version" anpassen und oben in der Versionstabelle eine Zeile
  ergaenzen.
* Host und Client muessen dieselbe Version haben, der Host lehnt sonst ab.
  Was das Netz betrifft (RPCs, NetworkVariables, Spawns), in der
  Versionszeile ausdruecklich sagen.
* Treffer, Kills und Deaths sollen langfristig der Server entscheiden. Neuen
  Code nicht auf Client-Autoritaet aufbauen.

## Pruefen vor dem Pull Request

```
sh tools/pruefe_verbotenes.sh --alle   # endet mit "FEHLER: keine"
```

Dazu im Unity-Editor (macht der Besitzer): Konsole ohne Fehler, Host und
Client mit zwei Instanzen (ParrelSync) verbinden, einmal schiessen und sterben.
