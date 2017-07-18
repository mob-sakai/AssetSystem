using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using Mobcast.Coffee.AssetSystem;

public class Test : MonoBehaviour
{
    public const string AssetBundlesOutputPath = "/AssetBundles/";
    public string assetBundleName;
    public string assetName;

	public string resourceDomain = "https://s3-ap-northeast-1.amazonaws.com/patch.s3.sand.mbl.mobcast.io/";
	public string resourceVersion = "e033f50907cc57b0b9b737a5ba1066b88a604d0e";
	public string manifestName = "Android";


	public Image image;

	public Text m_Log;
	public Text m_Progress;

	void Update()
	{
//		m_Log.text = AssetManager.GenerateReportText ().ToString ();
	}

    // Use this for initialization
//    IEnumerator Start()
//    {
////		AssetBundleManager.SetSourceAssetBundleURL(resourceDomain + resourceVersion);
////		var request = AssetBundleManager.Initialize();
////		if (request != null)
////			yield return StartCoroutine(request);
//
//
//
//
////        yield return StartCoroutine(Initialize());
////
////        // Load asset.
////        yield return StartCoroutine(InstantiateGameObjectAsync(assetBundleName, assetName));
//    }

	public void SetSourceAssetBundleURL()
	{
		AssetManager.SetURL(resourceDomain + resourceVersion);
	}

//	public void SetDevelopmentAssetBundleServer()
//	{
//		AssetBundleManager.SetDevelopmentAssetBundleServer ();
//	}

	public void InitializeXXX()
	{
		StartCoroutine (CoInitializeXXX());

//		AssetBundleManager.SetSourceAssetBundleURL(resourceDomain + resourceVersion);
//		AssetBundleManager.m_ManifestHash = Hash128.Parse (resourceVersion);
//		AssetBundleManager.UpdateManifest(Hash128.Parse (resourceVersion), op => Debug.LogFormat("UpdateManifest {0}", op));

//		AssetBundleManager.m_CacheAssetBundleManifest = true;
//		var request = AssetBundleManager.Initialize();
	}


	IEnumerator CoInitializeXXX()
	{
//		AssetBundleManager.SetSourceAssetBundleURL(resourceDomain + resourceVersion);
		var opUpdateManifest = AssetManager.UpdateManifest(Hash128.Parse (resourceVersion));

		yield return StartCoroutine (opUpdateManifest);
		if (!string.IsNullOrEmpty (opUpdateManifest.error))
		{
			Debug.LogError ("CoInitializeXXX is failed. Please try again.");
			yield break;
		}
		/*
		var op = AssetManager.PreloadAssetBundle ();

		// wait for finish.
		while(!op.isDone)
		{
			yield return null;
			m_Progress.text = string.Format ("{0:P1}, error:{1}%", op.progress, op.error);
		}
		*/
		yield break;
	}

	public void UpdateManifest()
	{
		AssetManager.UpdateManifest(Hash128.Parse (resourceVersion));
	}

	public void LoadAll()
	{
		AssetManager.PreDownload ();
//		StartCoroutine (CoLoadAll());

//
//		string baseUrl = AssetBundleManager.BaseDownloadingURL;
//
//		var manifest = AssetBundleManager.AssetBundleManifestObject;
//		foreach (var name in manifest.GetAllAssetBundles()) {
//			Debug.LogFormat ("url:{0}, hash:{1}, cached:{2}", baseUrl + name, manifest.GetAssetBundleHash (name), Caching.IsVersionCached (baseUrl + name, manifest.GetAssetBundleHash (name)));
//			if (!Caching.IsVersionCached (baseUrl + name, manifest.GetAssetBundleHash (name))) {
//				AssetBundleManager.LoadAssetBundle (name);
//			}
//
//
//		}
//
//		// download operation.
//		AssetBundleManager.InProgressOperations
//			.OfType<AssetBundleDownloadOperation> ()
//			.Sum (op => op.progress);
//

	}

	/*
	IEnumerator CoLoadAll()
	{
		var op = AssetManager.PreloadAssetBundle ();

		// wait for finish.
		while(!op.isDone)
		{
			yield return null;
			m_Progress.text = string.Format ("{0:P1}, error:{1}%", op.progress, op.error);
		}
		yield break;
	}*/


	public void Load()
	{
		AssetManager.LoadAssetAsync<Sprite>(assetBundleName, assetName, obj => image.sprite = obj);

//		StartCoroutine (CoLoad());
//		AssetBundleManager.LoadAssetAsync<Texture2D>(assetBundleName, assetName, obj => image.texture = obj);
	}

	IEnumerator CoLoad()
	{
		var op = AssetManager.LoadAssetAsync(assetBundleName, assetName, typeof(Sprite));

		yield return StartCoroutine (op);
		image.sprite = op.GetAsset<Sprite> ();
	}

	public void LoadFromResources()
	{
//		AssetManager.LoadAssetAsync<Sprite> (assetName, img => image.sprite = img);
	}

	public void LoadFromWeb()
	{
	}

	public void ClearAll()
	{
		AssetManager.ClearAll ();
	}
}



namespace System.Collections.Generic
{
	using System.Linq;
	public static class CollectionExtentions
	{
		public static string Dump<T1, T2>(this IEnumerable<T1> self, System.Func<T1,T2> selector)
		{
			return self.Select(selector).Dump();
		}

		public static string Dump<T>(this IEnumerable<T> self)
		{
			return self.Select(x => x != null ? x.ToString() : "null").Dump();
		}

		public static string Dump(this IEnumerable<string> self)
		{
			return !self.Any() ? "" : self.Aggregate(new System.Text.StringBuilder(), (a, b) => a.Append(b + ", "), x =>{ x.Length -= 2; return x.ToString();});
		}

		public static void LogDump<T>(this IEnumerable<T> self)
		{
			Debug.Log(self.Dump());
		}

		public static void LogDump<T>(this IEnumerable<T> self, string label)
		{
			if (string.IsNullOrEmpty (label))
				self.LogDump ();
			else
				Debug.LogFormat("{0}: {1}", label, self.Dump());
		}
	}
}
