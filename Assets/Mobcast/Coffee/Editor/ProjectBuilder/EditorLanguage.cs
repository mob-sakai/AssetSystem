using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;

namespace Mobcast.Coffee.Build
{
	public static class EditorLanguage
	{
		static readonly Dictionary<string, GUIContent> s_Contents = new Dictionary<string, GUIContent>();
		static readonly string s_CsvFileName = typeof(EditorLanguage).FullName + ".csv";
		static readonly string s_SystemLanguage = Application.systemLanguage.ToString();

		static readonly List<string> s_SupportedLanguages = new List<string>();
		static readonly Dictionary<string, string> s_Terms = new Dictionary<string, string>();

		static string currentLanguage
		{
			get { return EditorPrefs.GetString(s_CsvFileName, s_SystemLanguage); }
			set
			{
				if (value == currentLanguage)
					return;
				else if (value == s_SystemLanguage)
					EditorPrefs.DeleteKey(s_CsvFileName);
				else
					EditorPrefs.SetString(s_CsvFileName, value);

				Initialize();
			}
		}

		static EditorLanguage()
		{
			Initialize ();
		}


		public static void Initialize()
		{
			s_Contents.Clear ();

			using (var stream = new StreamReader(GetLanguageFilePath (), Encoding.UTF8)) {
				UpdateSupportedLanguages (stream);
				UpdateTerms (stream);
			}
		}

		static string GetLanguageFilePath()
		{
			string csvPath = AssetDatabase.FindAssets("t:TextAsset " + Path.GetFileNameWithoutExtension(s_CsvFileName))
				.Select(guid => AssetDatabase.GUIDToAssetPath(guid))
				.FirstOrDefault(path => Path.GetFileName(path) == s_CsvFileName) ?? "";

			if(csvPath.Length == 0)
			{
				UnityEngine.Debug.LogWarningFormat("Language file '{0}' is not found in project.", s_CsvFileName);
			}
			return csvPath;
		}

//		static StreamReader GetStream()
//		{
//			var csvPath = GetLanguageFilePath ();
//
//			if (string.IsNullOrEmpty(csvPath))
//			{
//				UnityEngine.Debug.LogWarningFormat("Language file '{0}' is not found in project.", s_CsvFileName);
//				return null;
//			}
//
//			return new StreamReader(csvPath, Encoding.UTF8);
//		}

		public static string Get(string key, string defaultValue = null)
		{
			string ret;
			if (!string.IsNullOrEmpty (key) && s_Terms.TryGetValue (key, out ret))
				return ret;
			else if (defaultValue != null)
				return defaultValue;
			else
				return key;
		}


		public static GUIContent GetContent(string label, string tooltip="")
		{
			GUIContent c;
			if (!s_Contents.TryGetValue (label, out c))
			{
				c = new GUIContent (Get (label), Get (tooltip, ""));
				s_Contents.Add (label, c);
			}
			return c;
		}

		public static GUIContent GetContent(string label, Texture icon)
		{
			GUIContent c;
			if (!s_Contents.TryGetValue (label, out c))
			{
				c = new GUIContent (Get (label), icon);
				s_Contents.Add (label, c);
			}
			return c;
		}


		public static GUIContent GetContent(SerializedProperty property)
		{
			GUIContent c;
			return s_Contents.TryGetValue(property.name, out c)
					? c
					: GetContent (property.name, property.name+"_tooltip");
		}


		static void UpdateSupportedLanguages(StreamReader stream)
		{
			s_SupportedLanguages.Clear();
			if (stream == null || stream.EndOfStream)
				return;
		
			s_SupportedLanguages.AddRange(stream.ReadLine().Split('\t').Skip(1));
		}

		static void UpdateTerms(StreamReader stream)
		{
			s_Terms.Clear();
			if (stream == null || stream.EndOfStream || s_SupportedLanguages.Count == 0)
				return;

			int languageId = Mathf.Max(1, s_SupportedLanguages.IndexOf(currentLanguage) + 1);
			while (!stream.EndOfStream)
			{
				var strs = stream.ReadLine().Split('\t');
				if (strs[0].Length == 0)
					continue;

				s_Terms[strs[0]] = languageId < strs.Length ? strs[languageId] : strs[0];
			}
		}

		/// <summary>
		/// Adds the language to menu.
		/// Use for IHasCustomMenu.AddItemsToMenu
		/// </summary>
		public static void AddItemsToMenu(GenericMenu menu)
		{
			using (var stream = new StreamReader(GetLanguageFilePath (), Encoding.UTF8))
			{
				UpdateSupportedLanguages(stream);
			}

			var current = currentLanguage;
			menu.AddItem(new GUIContent("Language/Default (" + s_SystemLanguage + ")"), s_SystemLanguage == current, l => currentLanguage = (l as string), s_SystemLanguage);
			menu.AddSeparator("Language/");
			foreach (var lang in s_SupportedLanguages)
			{
				menu.AddItem(new GUIContent("Language/" + lang), lang == current, l => currentLanguage = (l as string), lang);
			}
			menu.AddSeparator("Language/");
			menu.AddItem(new GUIContent("Edit Language File"), false, EditLanguageFile);
		}

		public static void EditLanguageFile()
		{
			string path = GetLanguageFilePath ();
			if (string.IsNullOrEmpty (path))
				return;

//			using (var stream = new StreamWriter(path, true, Encoding.UTF8))
//			{
//				var keys = s_Contents.Keys
//					.Where (key => !string.IsNullOrEmpty (key) && !s_Terms.ContainsKey (key))
//					.Distinct ();
//
//				foreach (var key in keys)
//				{
//					stream.WriteLine (key);
//					stream.WriteLine (key+"_tooltip");
//				}
//			}
//			Initialize ();
//			EditorUtility.OpenWithDefaultApp (path);
		}
	}
}