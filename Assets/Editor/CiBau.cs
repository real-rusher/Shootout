using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Automatischer Windows-Build auf GitHub (.github/workflows/spiel_bauen.yml, GameCI).
// Baut zuerst Szene und Prefabs wie das Menue, dann das Spiel. Lokal braucht man das nicht.
public static class CiBau
{
	const string SZENE = "Assets/Scenes/SampleScene.unity";

	public static void Bauen()
	{
		try
		{
			EinstellungenSetzen();
			SzeneAufbauen.AufbauenOhneFragen();
			string ziel = Argument("-customBuildPath") ?? "build/StandaloneWindows64/Shootout.exe";
			var optionen = new BuildPlayerOptions
			{
				scenes = new[] { SZENE },
				locationPathName = ziel,
				target = BuildTarget.StandaloneWindows64,
				options = BuildOptions.None,
			};
			BuildReport bericht = BuildPipeline.BuildPlayer(optionen);
			Debug.Log("Shootout-Build: " + bericht.summary.result + ", " + bericht.summary.totalSize / (1024 * 1024) + " MB, " + ziel);
			EditorApplication.Exit(bericht.summary.result == BuildResult.Succeeded ? 0 : 1);
		}
		catch (Exception fehler)
		{
			Debug.LogException(fehler);
			EditorApplication.Exit(1);
		}
	}

	// Im Repo liegen keine ProjectSettings ausser TagManager und Version. Auf dem Build-Server
	// faengt Unity deshalb mit Standardwerten an: Hier das Noetige setzen.
	static void EinstellungenSetzen()
	{
		PlayerSettings.productName = "Shootout";
		PlayerSettings.companyName = "Shootout";
		PlayerSettings.runInBackground = true;
		PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
		PlayerSettings.resizableWindow = true;
		// Eingabe: neues Input System (1). Im Editor fragt Unity danach, hier nicht.
		var einstellungen = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
		if (einstellungen != null && einstellungen.Length > 0)
		{
			var so = new SerializedObject(einstellungen[0]);
			var eingabe = so.FindProperty("activeInputHandler");
			if (eingabe != null)
			{
				eingabe.intValue = 1;
				so.ApplyModifiedPropertiesWithoutUndo();
			}
		}
		AssetDatabase.SaveAssets();
	}

	static string Argument(string name)
	{
		string[] argumente = Environment.GetCommandLineArgs();
		for (int i = 0; i < argumente.Length - 1; i++)
		{
			if (argumente[i] == name)
			{
				return argumente[i + 1];
			}
		}
		return null;
	}
}
