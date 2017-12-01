using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Mobcast.Coffee.AssetSystem;
using System.Text;
using System.Diagnostics;
using System.IO;
using System;
using EditorOption = Mobcast.Coffee.AssetSystem.EditorOption;

namespace Mobcast.Coffee.AssetSystem
{
	/// <summary>
	/// AssetManager Menu.
	/// </summary>
	static class AssetManagerMenu
	{
		const string MenuText_Root = "Coffee/AsssetSystem";
		const string MenuText_SimulationMode = MenuText_Root + "/AssetBundle Mode/Simulation (Editor)";
		const string MenuText_LocalServerMode = MenuText_Root + "/AssetBundle Mode/In Local Server (Editor)";
		const string MenuText_StreamingAssets = MenuText_Root + "/AssetBundle Mode/In StreamingAssets";
		const string MenuText_BuildAssetBundle = MenuText_Root + "/Build AssetBundle (Uncompressed)";

		static EditorOption editorOption { get { return EditorOption.instance; } }

		/// <summary>
		/// Raises the initialize on load method event.
		/// </summary>
		[InitializeOnLoadMethod]
		static void OnInitializeOnLoadMethod()
		{
		}

		[MenuItem(MenuText_SimulationMode)]
		static void ToggleSimulationMode()
		{
			if (Application.isPlaying)
				return;

			editorOption.mode = (editorOption.mode == EditorOption.Mode.Simulation) ? EditorOption.Mode.None : EditorOption.Mode.Simulation;
			if (IsLocalServerRunning())
				KillRunningAssetBundleServer();

			Valid();
		}

		[MenuItem(MenuText_LocalServerMode, true)]
		static bool Valid()
		{
			Menu.SetChecked(MenuText_SimulationMode, editorOption.mode == EditorOption.Mode.Simulation);
			Menu.SetChecked(MenuText_LocalServerMode, editorOption.mode == EditorOption.Mode.LocalServer);
			return true;
		}

		[MenuItem(MenuText_LocalServerMode)]
		static void ToggleLocalServerMode()
		{
			if (Application.isPlaying)
				return;
			
			BuildAssetBundle();

			editorOption.mode = (editorOption.mode == EditorOption.Mode.LocalServer) ? EditorOption.Mode.None : EditorOption.Mode.LocalServer;
			if (editorOption.mode == EditorOption.Mode.LocalServer)
				Run();
			else
				KillRunningAssetBundleServer();

			Valid();
		}

		[MenuItem(MenuText_BuildAssetBundle)]
		static void BuildAssetBundle()
		{
			if (Application.isPlaying)
				return;

			string path = "AssetBundles/" + AssetManager.Platform;
			BuildAssetBundleOptions op = BuildAssetBundleOptions.DeterministicAssetBundle
			                             | BuildAssetBundleOptions.UncompressedAssetBundle;
			
			if (!Directory.Exists(path))
				Directory.CreateDirectory(path);
			
			BuildPipeline.BuildAssetBundles(path, op, EditorUserBuildSettings.activeBuildTarget);
		}

		static bool IsLocalServerRunning()
		{
			try
			{
				return editorOption.localServerProcessId != 0 && !Process.GetProcessById(editorOption.localServerProcessId).HasExited;
			}
			catch
			{
				return false;
			}
		}

		static void KillRunningAssetBundleServer()
		{
			// Kill the last time we ran
			try
			{
				if (editorOption.localServerProcessId == 0)
					return;

				var lastProcess = Process.GetProcessById(editorOption.localServerProcessId);
				lastProcess.Kill();
				editorOption.localServerProcessId = 0;
			}
			catch
			{
			}
		}

		static void Run()
		{
			#if UNITY_EDITOR_OSX
			#if UNITY_5_4_OR_NEWER
			string frameWorksFolder = Path.Combine(EditorApplication.applicationPath, "Contents");
			#else
			string frameWorksFolder = Path.Combine(EditorApplication.applicationPath, "Contents/Frameworks");
			#endif
			#else
			string frameWorksFolder = Path.Combine(Path.GetDirectoryName(EditorApplication.applicationPath), "Data");
			#endif

			string serverExePath = AssetDatabase.FindAssets("AssetServer")
				.Select(x => AssetDatabase.GUIDToAssetPath(x))
				.First(x => Path.GetFileName(x) == "AssetServer.exe");

			string assetBundlesDirectory = Path.Combine(Environment.CurrentDirectory, "AssetBundles");

			if (!Directory.Exists(assetBundlesDirectory))
				Directory.CreateDirectory(assetBundlesDirectory);


			KillRunningAssetBundleServer();

			var monodistribution = Path.Combine(frameWorksFolder, "MonoBleedingEdge");
			var monoexe = Path.Combine(Path.Combine(monodistribution, "bin"), "mono");// new []{ monodistribution, "bin", "mono" }.Aggregate(Path.Combine);
			var args = string.Format("'{0}' '{1}' {2}",
				           Path.GetFullPath(serverExePath),
				           assetBundlesDirectory,
				           Process.GetCurrentProcess().Id
			           );


			var startInfo = new ProcessStartInfo
			{
				Arguments = args,
				CreateNoWindow = true,
				FileName = monoexe,
				RedirectStandardError = true,
				RedirectStandardOutput = true,
				WorkingDirectory = assetBundlesDirectory,
				UseShellExecute = false
			};

			startInfo.EnvironmentVariables["MONO_PATH"] = Path.Combine(monoexe, "2.0");
			startInfo.EnvironmentVariables["MONO_CFG_DIR"] = Path.Combine(monodistribution, "etc");

			Process launchProcess = Process.Start(startInfo);
			if (launchProcess == null || launchProcess.HasExited == true || launchProcess.Id == 0)
			{
				//Unable to start process
				Log(UnityEngine.Debug.LogError, "Unable Start AssetBundleServer process");
			}
			else
			{
				//We seem to have launched, let's save the PID
				editorOption.localServerProcessId = launchProcess.Id;

				//Add process callback.
				launchProcess.OutputDataReceived += (sender, e) => Log(UnityEngine.Debug.Log, e.Data);
				launchProcess.ErrorDataReceived += (sender, e) => Log(UnityEngine.Debug.LogError, e.Data);
				launchProcess.Exited += (sender, e) => Log(UnityEngine.Debug.Log, "Exit");

				launchProcess.BeginOutputReadLine();
				launchProcess.BeginErrorReadLine();
				launchProcess.EnableRaisingEvents = true;
			}
		}

		static void Log(Action<string> action, string message)
		{
			if (string.IsNullOrEmpty(message))
				return;
			action("<color=orange>[AssetBundleServer]</color> " + message);
		}

	}

	/// <summary>
	/// Asset manager editor.
	/// </summary>
	[CustomEditor(typeof(AssetManager), true)]
	public class AssetManagerEditor : Editor
	{
		public override void OnInspectorGUI()
		{
			base.OnInspectorGUI();

			using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
			{
				GUILayout.Label(string.Format("Space Occupied : {0}", Caching.spaceOccupied));


				GUILayout.Label(string.Format("ランタイムキャッシュ : {0}", AssetManager.m_RuntimeCache.Count));
				GUILayout.Label(string.Format("ロード済み : {0}", AssetManager.m_LoadedAssetBundles.Count));
				if (AssetManager.manifest)
				{
					var m = AssetManager.manifest;
					var ar = AssetManager.manifest.GetAllAssetBundles();
					var count = ar.Count(x => Caching.IsVersionCached(x, m.GetAssetBundleHash(x)));
					GUILayout.Label(string.Format("キャッシュ : {0}/{1}", count, ar.Length));
				}
			}


			GUILayout.Space(20);
			GUILayout.Label("依存関係", EditorStyles.boldLabel);
			EditorGUILayout.TextArea(
				AssetManager.m_Depended
				.Select(p => string.Format("{0} : {1}", p.Key, p.Value.Aggregate(new StringBuilder(), (a, b) => a.AppendFormat("{0}, ", b), a => a.ToString())))
					.Aggregate(new StringBuilder(), (a, b) => a.AppendLine(b), a => a.ToString())
			);


			GUILayout.Space(20);
			GUILayout.Label("ジョブリスト", EditorStyles.boldLabel);
			using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
			{
				foreach (var op in AssetManager.m_InProgressOperations)
				{
					GUILayout.Label(string.Format("{0}", op.ToString()));
				}
			}
			GUILayout.Space(20);
			GUILayout.Label("エラーログ", EditorStyles.boldLabel);
			EditorGUILayout.TextArea(AssetManager.errorLog.ToString());


			GUILayout.Space(20);
			GUILayout.Label("パッチ履歴", EditorStyles.boldLabel);
			using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
			{
				foreach (var patch in AssetManager.history.patchList)
				{
					var date = new System.DateTime(1970, 1, 1).AddSeconds(patch.deployTime).ToLocalTime().ToString("MM/dd hh:mm");
					EditorGUILayout.LabelField(string.Format("{0} [{1}] {2}", date, patch.commitHash.Substring(0, 4), patch.comment));
				}
			}


			GUILayout.Space(20);
			if (Application.isPlaying)
				Repaint();

			serializedObject.ApplyModifiedProperties();
		}
	}
}