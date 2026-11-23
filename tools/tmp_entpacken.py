#!/usr/bin/env python3
# Legt die TextMeshPro-Grundausstattung (Schrift, Shader, Einstellungen) nach Assets/TextMesh Pro,
# wie es Unity beim ersten Oeffnen nach Rueckfrage tut. Nur fuer den automatischen Build auf
# GitHub: Dort fragt niemand. Lokal nicht noetig, und die Dateien werden nicht eingecheckt.
# Quelle ist das Paket com.unity.textmeshpro aus der Unity-Registry, Version wie in manifest.json.
import io
import json
import os
import sys
import tarfile
import urllib.request

WURZEL = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ZIEL = os.path.join(WURZEL, "Assets", "TextMesh Pro")
INHALT = "package/Package Resources/TMP Essential Resources.unitypackage"


def main():
    if os.path.isdir(ZIEL):
        print("TextMesh Pro ist schon da.")
        return
    with open(os.path.join(WURZEL, "Packages", "manifest.json"), encoding="utf-8") as f:
        version = json.load(f)["dependencies"]["com.unity.textmeshpro"]
    with urllib.request.urlopen("https://packages.unity.com/com.unity.textmeshpro") as antwort:
        adresse = json.load(antwort)["versions"][version]["dist"]["tarball"]
    with urllib.request.urlopen(adresse) as antwort:
        paket = tarfile.open(fileobj=io.BytesIO(antwort.read()), mode="r:gz")
    einheit = tarfile.open(fileobj=io.BytesIO(paket.extractfile(INHALT).read()), mode="r:gz")
    teile = {}
    for eintrag in einheit.getmembers():
        if not eintrag.isfile():
            continue
        guid, _, name = eintrag.name.lstrip("./").partition("/")
        teile.setdefault(guid, {})[name] = einheit.extractfile(eintrag).read()
    anzahl = 0
    for guid, dateien in teile.items():
        pfad = dateien["pathname"].decode("utf-8").splitlines()[0].strip()
        # Nur nach Assets/TextMesh Pro, nichts anderes anfassen.
        voll = os.path.normpath(os.path.join(WURZEL, pfad))
        if not voll.startswith(ZIEL):
            sys.exit("Unerwarteter Pfad im Paket: " + pfad)
        if "asset" in dateien:
            os.makedirs(os.path.dirname(voll), exist_ok=True)
            with open(voll, "wb") as f:
                f.write(dateien["asset"])
            anzahl += 1
        else:
            os.makedirs(voll, exist_ok=True)
        if "asset.meta" in dateien:
            with open(voll + ".meta", "wb") as f:
                f.write(dateien["asset.meta"])
    print("TextMesh Pro %s: %d Dateien nach Assets/TextMesh Pro" % (version, anzahl))


if __name__ == "__main__":
    main()
