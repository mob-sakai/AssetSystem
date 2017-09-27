using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

//using Util = Mobcast.Coffee.Build.ProjectBuilderUtil;
using System.Reflection;
using UnityEditorInternal;
using System.IO;
using System;

namespace Mobcast.Coffee.Build
{
	/// <summary>
	/// プロジェクトビルダーエディタ.
	/// インスペクタをオーバーライドして、ビルド設定エディタを構成します.
	/// エディタから直接ビルドパイプラインを実行できます.
	/// </summary>
	internal class ProjectBuilderEditor : EditorWindow
	{
		public ProjectBuilder target;

		static GUIContent contentOpen;
		static ReorderableList roSceneList;
		static ReorderableList roBuilderList;

		static GUIStyle styleCommand;
		static GUIStyle styleSymbols;
		static string s_EndBasePropertyName = "";
		static string[] s_AvailableScenes;
		static List<ProjectBuilder> s_Builders;

		static readonly Dictionary<int, string> s_BundleOptions =
			System.Enum.GetValues(typeof(BuildAssetBundleOptions))
				.Cast<int>()
				.ToDictionary(x=>x,x=>System.Enum.GetName(typeof(BuildAssetBundleOptions),x));
		

		static readonly Dictionary<BuildTarget, IPlatformSettings> s_Platforms =
			typeof(ProjectBuilder).Assembly
				.GetTypes()
				.Where(x => x.IsPublic && !x.IsInterface && typeof(IPlatformSettings).IsAssignableFrom(x))
				.Select(x => Activator.CreateInstance(x) as IPlatformSettings)
				.OrderBy(x => x.platform)
				.ToDictionary(x=>x.platform);
		
		static readonly Dictionary<int, string> s_BuildTargets = s_Platforms
			.ToDictionary(x=>(int)x.Key, x=>x.Key.ToString());

		Vector2 scrollPosition;

		public static Texture GetPlatformIcon(ProjectBuilder builder)
		{
			return builder.buildApplication && s_Platforms.ContainsKey(builder.buildTarget)
				? s_Platforms[builder.buildTarget].icon
					: EditorGUIUtility.FindTexture("BuildSettings.Editor.Small");
		}

		[MenuItem("Coffee/Project BuilderX")]
		public static void OnOpenFromMenu()
		{
			EditorWindow.GetWindow<ProjectBuilderEditor>("Project Builder");
		}

		void Initialize()
		{
			if (styleCommand != null)
				return;

			styleSymbols = new GUIStyle(EditorStyles.textArea);
			styleSymbols.wordWrap = true;

			styleCommand = new GUIStyle(EditorStyles.textArea);
			styleCommand.stretchWidth = false;
			styleCommand.fontSize = 9;
			contentOpen = new GUIContent(EditorGUIUtility.FindTexture("project"));

			// Find end property in ProjectBuilder.
			var sp = new SerializedObject(ScriptableObject.CreateInstance<ProjectBuilder>()).GetIterator();
			sp.Next(true);
			while (sp.Next(false))
				s_EndBasePropertyName = sp.name;


			// Scene list.
			roSceneList = new ReorderableList(new List<ProjectBuilder.SceneSetting>(), typeof(ProjectBuilder.SceneSetting));
			roSceneList.drawElementCallback += (rect, index, isActive, isFocused) =>
				{
					var element = roSceneList.serializedProperty.GetArrayElementAtIndex(index);
					EditorGUI.PropertyField(new Rect(rect.x, rect.y, 16, rect.height-2), element.FindPropertyRelative("enable"), GUIContent.none);
					EditorGUIEx.TextFieldWithTemplate(new Rect(rect.x + 16, rect.y, rect.width - 16, rect.height-2), element.FindPropertyRelative("name"), GUIContent.none, s_AvailableScenes, false);
				};
			roSceneList.headerHeight = 0;
			roSceneList.elementHeight = 18;

			// Builder list.
			roBuilderList = new ReorderableList(s_Builders, typeof(ProjectBuilder));
			roBuilderList.onSelectCallback = (list) => Selection.activeObject = list.list[list.index] as ProjectBuilder;
			roBuilderList.onAddCallback += (list) => Util.CreateBuilderAsset();
			roBuilderList.onRemoveCallback += (list) =>
			{
				AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(list.list[list.index] as ProjectBuilder));
				AssetDatabase.Refresh();
				Util.Open();
			};
			roBuilderList.drawElementCallback += (rect, index, isActive, isFocused) =>
				{
					var b = roBuilderList.list[index] as ProjectBuilder;	//オブジェクト取得.

					GUI.DrawTexture(new Rect(rect.x, rect.y+2, 16, 16), GetPlatformIcon(b));
					GUI.Label(new Rect(rect.x + 16, rect.y + 2, rect.width - 16, rect.height-2), EditorGUIEx.GetContent(string.Format("{0} ({1})", b.name, b.productName)));
				};
			roBuilderList.headerHeight = 0;
			roBuilderList.draggable = false;
		}
		//---- ▲ GUIキャッシュ ▲ ----


		//-------------------------------
		//	Unityコールバック.
		//-------------------------------
		/// <summary>
		/// Raises the enable event.
		/// </summary>
		void OnEnable()
		{
			// Get all scenes in build from BuildSettings.
			s_AvailableScenes = EditorBuildSettings.scenes.Select(x => Path.GetFileName(x.path)).ToArray();

			// Get all builder assets in project.
			s_Builders = new List<ProjectBuilder>(
				Util.GetAssets<ProjectBuilder>()
					.OrderBy(b=>b.buildTarget)
			);

			SetBuilder(target ?? s_Builders.FirstOrDefault());

			Selection.selectionChanged += OnSelectionChanged;
		}

		void OnDisable()
		{
			Selection.selectionChanged -= OnSelectionChanged;
		}

		SerializedObject serializedObject;

		void SetBuilder(ProjectBuilder builder)
		{
			target = builder;
			serializedObject = null;
		}

		void OnSelectionChanged()
		{
			Debug.Log(Selection.activeObject);
			if (Selection.activeObject is ProjectBuilder)
			{
				target = Selection.activeObject as ProjectBuilder;
				serializedObject = null;
				Repaint();
			}
		}

		void OnGUI()
		{
			if (!target)
				return;

			serializedObject = serializedObject ?? new SerializedObject(target);
			using (var svs = new EditorGUILayout.ScrollViewScope(scrollPosition))
			{
				scrollPosition = svs.scrollPosition;
				OnInspectorGUI();
			}
		}

		/// <summary>
		/// Raises the inspector GU event.
		/// </summary>
		public void OnInspectorGUI()
		{
			Initialize();

			serializedObject.Update();
			var builder = target as ProjectBuilder;

			GUILayout.Label(EditorGUIUtility.ObjectContent(target, typeof(ProjectBuilder)));

			// Draw properties in custom project builder.
			DrawCustomProjectBuilder();

			var spBuildApplication = serializedObject.FindProperty ("buildApplication");
			var spBuildTarget = serializedObject.FindProperty ("buildTarget");
			using (new EditorGUIEx.GroupScope("Build Setting"))
			{
				EditorGUIEx.PropertyField(spBuildApplication);
				if (spBuildApplication.boolValue)
				{
					EditorGUIEx.PopupField(serializedObject.FindProperty("buildTarget"), s_BuildTargets);
					EditorGUIEx.PropertyField(serializedObject.FindProperty("companyName"));
					EditorGUIEx.PropertyField(serializedObject.FindProperty("productName"));
					EditorGUIEx.PropertyField(serializedObject.FindProperty("applicationIdentifier"));

					// Advanced Options
					GUILayout.Space(8);
					EditorGUILayout.LabelField("Advanced Options", EditorStyles.boldLabel);
					EditorGUIEx.PropertyField(serializedObject.FindProperty("developmentBuild"));
					EditorGUIEx.PropertyField(serializedObject.FindProperty("defineSymbols"), new GUIContent("Scripting Define Symbols"));

					// Scenes In Build.
					EditorGUILayout.LabelField("Enable/Disable Scenes In Build");
					roSceneList.serializedProperty = serializedObject.FindProperty("scenes");
					roSceneList.DoLayoutList();

					// Version.
					EditorGUILayout.LabelField("Version Settings", EditorStyles.boldLabel);
					EditorGUI.indentLevel++;
					EditorGUIEx.PropertyField(serializedObject.FindProperty("version"));

					// Internal version for the platform.
					switch ((BuildTarget)spBuildTarget.intValue)
					{
						case BuildTarget.Android:
							EditorGUIEx.PropertyField(serializedObject.FindProperty("versionCode"), EditorGUIEx.GetContent("Version Code"));
							break;
						case BuildTarget.iOS:
							EditorGUIEx.PropertyField(serializedObject.FindProperty("versionCode"), EditorGUIEx.GetContent("Build Number"));
							break;
					}
					EditorGUI.indentLevel--;
				}
			}

			// AssetBundle building.
			using (new EditorGUIEx.GroupScope("AssetBundle Build Setting"))
			{
				var spBuildAssetBundle = serializedObject.FindProperty ("buildAssetBundle");
				EditorGUIEx.PropertyField(spBuildAssetBundle);
				if (spBuildAssetBundle.boolValue)
				{
					EditorGUIEx.PropertyField(serializedObject.FindProperty("bundleOptions"));
					EditorGUIEx.PropertyField(serializedObject.FindProperty("copyToStreamingAssets"));
				}
			}


			// Drawer for target platform.
			var platfom = (BuildTarget)spBuildTarget.intValue;
			if (spBuildApplication.boolValue && s_Platforms.ContainsKey(platfom))
				s_Platforms[platfom].DrawSetting(serializedObject);

			// Control panel.
			DrawControlPanel();

			serializedObject.ApplyModifiedProperties();
		}


		//-------------------------------
		//	メソッド.
		//-------------------------------
		/// <summary>
		/// Draw all propertyies declared in Custom-ProjectBuilder.
		/// </summary>
		void DrawCustomProjectBuilder()
		{
			System.Type type = target.GetType();
			if (type == typeof(ProjectBuilder))
				return;

			GUI.backgroundColor = Color.green;
			using (new EditorGUIEx.GroupScope(type.Name))
			{
				GUI.backgroundColor = Color.white;
				var itr = serializedObject.GetIterator();

				// Skip properties declared in ProjectBuilder.
				itr.NextVisible(true);
				while (itr.NextVisible(false) && itr.name != s_EndBasePropertyName)
					;

				// Draw properties declared in Custom-ProjectBuilder.
				while (itr.NextVisible(false))
					EditorGUILayout.PropertyField(itr, EditorGUIEx.GetContent(itr.displayName), true);
			}
		}

		/// <summary>
		/// Control panel for builder.
		/// </summary>
		void DrawControlPanel()
		{
			var builder = target as ProjectBuilder;

			GUILayout.FlexibleSpace();
			using (new EditorGUILayout.VerticalScope("box"))
			{
				if (builder.buildApplication)
				{
					GUILayout.Label(new GUIContent(string.Format("Build {0} ver.{1} ({2})", builder.productName, builder.version, builder.versionCode), GetPlatformIcon(builder)), EditorStyles.largeLabel);
				}
				else if (builder.buildAssetBundle)
				{
					GUILayout.Label(new GUIContent(string.Format("Build {0} AssetBundles", AssetDatabase.GetAllAssetBundleNames().Length), GetPlatformIcon(builder)), EditorStyles.largeLabel);
				}

				using (new EditorGUILayout.HorizontalScope())
				{
					// Apply settings from current builder asset.
					if (GUILayout.Button(EditorGUIEx.GetContent("Apply Setting", EditorGUIUtility.FindTexture("vcs_check"))))
					{
						builder.DefineSymbol();
						builder.ApplySettings();
					}

					// Open PlayerSettings.
					if (GUILayout.Button(EditorGUIEx.GetContent("Player Setting", EditorGUIUtility.FindTexture("EditorSettings Icon")), GUILayout.Height(21), GUILayout.Width(110)))
					{
						EditorApplication.ExecuteMenuItem("Edit/Project Settings/Player");
					}
				}

				//ビルドターゲットが同じ場合のみビルド可能.
				EditorGUI.BeginDisabledGroup(builder.buildTarget != EditorUserBuildSettings.activeBuildTarget);

				using (new EditorGUILayout.HorizontalScope())
				{
					EditorGUI.BeginDisabledGroup(!builder.buildAssetBundle);
					// Build.
					if (GUILayout.Button(EditorGUIEx.GetContent("Build AssetBundles", EditorGUIUtility.FindTexture("buildsettings.editor.small")), "LargeButton"))
					{
						EditorApplication.delayCall += () => Util.StartBuild(builder, false, true);
					}

					// Open output.
					var r = EditorGUILayout.GetControlRect(false, GUILayout.Width(15));
					if (GUI.Button(new Rect(r.x - 2, r.y + 5, 20, 20), contentOpen, EditorStyles.label))
					{
						Directory.CreateDirectory(builder.bundleOutputPath);
						Util.RevealOutputInFinder(builder.bundleOutputPath);
					}
					EditorGUI.EndDisabledGroup();
				}

				using (new EditorGUILayout.HorizontalScope())
				{
					EditorGUI.BeginDisabledGroup(!builder.buildApplication);
					// Build.
					if (GUILayout.Button(EditorGUIEx.GetContent(string.Format("Build to '{0}'", builder.outputPath), EditorGUIUtility.FindTexture("preAudioPlayOff"), builder.outputFullPath), "LargeButton"))
					{
						EditorApplication.delayCall += () => Util.StartBuild(builder, false, false);
					}

					// Open output.
					var r = EditorGUILayout.GetControlRect(false, GUILayout.Width(15));
					if (GUI.Button(new Rect(r.x - 2, r.y + 5, 20, 20), contentOpen, EditorStyles.label))
						Util.RevealOutputInFinder(builder.outputFullPath);
					EditorGUI.EndDisabledGroup();
				}
				EditorGUI.EndDisabledGroup();

				// Build & Run.
				if (GUILayout.Button(EditorGUIEx.GetContent("Build & Run", EditorGUIUtility.FindTexture("preAudioPlayOn"), builder.outputFullPath), "LargeButton"))
				{
					EditorApplication.delayCall += () => Util.StartBuild(builder, true, false);
				}


				// Create custom builder script.
				if (Util.builderType == typeof(ProjectBuilder) && GUILayout.Button(EditorGUIEx.GetContent("Create Custom Project Builder Script")))
				{
					Util.CreateCustomProjectBuilder();
				}

				// Available builders.
				GUILayout.Space(10);
				GUILayout.Label(EditorGUIEx.GetContent("Available Project Builders"), EditorStyles.boldLabel);
				roBuilderList.list = s_Builders;
				roBuilderList.index = s_Builders.FindIndex(x => x == target);
				roBuilderList.DoLayoutList();
			}
		}
	}
}