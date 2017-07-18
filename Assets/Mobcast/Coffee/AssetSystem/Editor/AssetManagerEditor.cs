using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Mobcast.Coffee.AssetSystem;

namespace Mobcast.Coffee.AssetSystem
{
	/// <summary>
	/// UIマネージャのエディタ.
	/// </summary>
	[CustomEditor(typeof(AssetManager))]
	public class UIManagerEditor : Editor
	{
		public override void OnInspectorGUI()
		{
			base.OnInspectorGUI();

			var current = target as AssetManager;
			 
			//			UIManager manager = target as UIManager;

			using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
			{
				EditorGUILayout.TextField("URL", AssetManager.SourceURL);
				EditorGUILayout.TextField("ハッシュ", AssetManager.m_ManifestHash.ToString());
				GUILayout.Label(string.Format("Space Occupied : {0}", Caching.spaceOccupied));


				GUILayout.Label(string.Format("ランタイムキャッシュ : {0}", AssetManager.m_RuntimeCache.Count));
				GUILayout.Label(string.Format("ロード済み : {0}", AssetManager.m_LoadedAssetBundles.Count));
				if (AssetManager.Manifest)
				{
					var m = AssetManager.Manifest;
					var ar = AssetManager.Manifest.GetAllAssetBundles();
					var count = ar.Count(x=>Caching.IsVersionCached(x, m.GetAssetBundleHash(x)));
					GUILayout.Label(string.Format("キャッシュ : {0}/{1}", count, ar.Length));
				}
			}


			GUILayout.Space(20);
			GUILayout.Label("依存関係", EditorStyles.boldLabel);
			EditorGUILayout.TextArea(AssetManager.m_Depended.Select(p=>string.Format("{0} -> {1}\n",p.Key, p.Value.Dump())).Dump());


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
			if (Application.isPlaying)
				Repaint();
		}
	}
}