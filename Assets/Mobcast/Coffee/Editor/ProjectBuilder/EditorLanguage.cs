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


		public static void Initialize()
		{
			using (var stream = GetStream())
			{
				UpdateSupportedLanguages(stream);
				UpdateTerms(stream);
			}
		}

		static StreamReader GetStream()
		{
			var csvPath = AssetDatabase.FindAssets("t:TextAsset " + Path.GetFileNameWithoutExtension(s_CsvFileName))
				.Select(guid => AssetDatabase.GUIDToAssetPath(guid))
				.FirstOrDefault(path => Path.GetFileName(path) == s_CsvFileName);

			if (string.IsNullOrEmpty(csvPath))
			{
				UnityEngine.Debug.LogWarningFormat("Language file '{0}' is not found in project.", s_CsvFileName);
				return null;
			}

			return new StreamReader(csvPath, Encoding.UTF8);
		}

		public static string Get(string key)
		{
			string ret;
			return s_Terms.TryGetValue(key, out ret) ? ret : key;
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
				if (strs.Length <= languageId || strs[0].Length == 0)
					continue;

				s_Terms[strs[0]] = strs[languageId];
			}
		}

		/// <summary>
		/// Adds the language to menu.
		/// Use for IHasCustomMenu.AddItemsToMenu
		/// </summary>
		public static void AddItemsToMenu(GenericMenu menu)
		{
			using (var stream = GetStream())
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
		}
	}
}