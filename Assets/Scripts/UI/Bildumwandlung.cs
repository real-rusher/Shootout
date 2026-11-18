using System;
using System.Diagnostics;
using System.Text;

// Wandelt Bilder, die Unity nicht lesen kann (WebP, BMP, GIF, TIFF, HEIC, AVIF, ICO ...), in PNG
// um. Unity kann nur PNG und JPG lesen. Windows bringt mit WIC eine Bildbibliothek mit, die fast
// alle Formate kennt (HEIC und AVIF mit den kostenlosen Erweiterungen aus dem Microsoft Store).
// Der Weg dorthin ist PowerShell mit WPF, das gibt es auf jedem Windows 10 und 11.
public static class Bildumwandlung
{
	public static bool Verfuegbar
	{
		get
		{
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
			return true;
#else
			return false;
#endif
		}
	}

	// Startet die Umwandlung im Hintergrund. Fertig, wenn der Prozess beendet ist und die
	// Ausgabedatei existiert.
	public static Process Starten(string eingabe, string ausgabe)
	{
		string skript =
			"$ErrorActionPreference = 'Stop'\n" +
			"Add-Type -AssemblyName PresentationCore\n" +
			"$quelle = [IO.File]::OpenRead(" + Text(eingabe) + ")\n" +
			"try {\n" +
			"  $decoder = [System.Windows.Media.Imaging.BitmapDecoder]::Create($quelle, [System.Windows.Media.Imaging.BitmapCreateOptions]::None, [System.Windows.Media.Imaging.BitmapCacheOption]::OnLoad)\n" +
			"  $encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder\n" +
			"  $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($decoder.Frames[0]))\n" +
			"  $ziel = [IO.File]::Create(" + Text(ausgabe) + ")\n" +
			"  try { $encoder.Save($ziel) } finally { $ziel.Close() }\n" +
			"} finally { $quelle.Close() }\n";
		// Als -EncodedCommand (UTF-16, Base64), damit Pfade mit Leerzeichen, Umlauten oder
		// Anfuehrungszeichen keine Probleme machen.
		string kodiert = Convert.ToBase64String(Encoding.Unicode.GetBytes(skript));
		ProcessStartInfo start = new ProcessStartInfo
		{
			FileName = "powershell.exe",
			Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + kodiert,
			CreateNoWindow = true,
			UseShellExecute = false,
			WindowStyle = ProcessWindowStyle.Hidden,
		};
		return Process.Start(start);
	}

	// PowerShell-Text in einfachen Anfuehrungszeichen, darin wird ' verdoppelt.
	private static string Text(string wert)
	{
		return "'" + wert.Replace("'", "''") + "'";
	}
}
