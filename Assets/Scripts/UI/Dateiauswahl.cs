using System;
using System.Runtime.InteropServices;

// Windows-Dialog "Datei oeffnen", nur fuer Bilder. Unity hat im fertigen Spiel keinen
// eigenen Dateidialog, deshalb direkt ueber die Windows-Bibliothek comdlg32.
public static class Dateiauswahl
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

	// Gibt den gewaehlten Pfad zurueck oder null (abgebrochen oder nicht unter Windows).
	public static string BildWaehlen(string titel)
	{
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
		OpenFileName dialog = new OpenFileName();
		dialog.structSize = Marshal.SizeOf(dialog);
		// Alles, was Windows als Bild oeffnen kann. Was Unity nicht liest, wandelt Bildumwandlung um.
		dialog.filter = "Bilder\0*.png;*.jpg;*.jpeg;*.jfif;*.bmp;*.dib;*.gif;*.tif;*.tiff;*.ico;*.webp;*.heic;*.heif;*.avif;*.jxr;*.wdp;*.dds\0Alle Dateien\0*.*\0\0";
		dialog.filterIndex = 1;
		dialog.file = new string('\0', 1024);
		dialog.maxFile = dialog.file.Length;
		dialog.fileTitle = new string('\0', 256);
		dialog.maxFileTitle = dialog.fileTitle.Length;
		dialog.title = titel;
		dialog.flags = OFN_EXPLORER | OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_NOCHANGEDIR;
		if (GetOpenFileName(dialog))
		{
			string pfad = dialog.file;
			int ende = pfad.IndexOf('\0');
			return ende >= 0 ? pfad.Substring(0, ende) : pfad;
		}
		return null;
#else
		return null;
#endif
	}

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
	private const int OFN_EXPLORER = 0x00080000;

	private const int OFN_FILEMUSTEXIST = 0x00001000;

	private const int OFN_PATHMUSTEXIST = 0x00000800;

	private const int OFN_NOCHANGEDIR = 0x00000008;

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	private class OpenFileName
	{
		public int structSize;
		public IntPtr dlgOwner = IntPtr.Zero;
		public IntPtr instance = IntPtr.Zero;
		public string filter;
		public string customFilter;
		public int maxCustFilter;
		public int filterIndex;
		public string file;
		public int maxFile;
		public string fileTitle;
		public int maxFileTitle;
		public string initialDir;
		public string title;
		public int flags;
		public short fileOffset;
		public short fileExtension;
		public string defExt;
		public IntPtr custData = IntPtr.Zero;
		public IntPtr hook = IntPtr.Zero;
		public string templateName;
		public IntPtr reservedPtr = IntPtr.Zero;
		public int reservedInt;
		public int flagsEx;
	}

	[DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern bool GetOpenFileName([In, Out] OpenFileName dialog);
#endif
}
