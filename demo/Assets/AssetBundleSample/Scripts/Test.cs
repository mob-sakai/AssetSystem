using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using AssetBundles;
using System.Linq;
using System.Collections.Generic;

public class Test : MonoBehaviour
{
    public const string AssetBundlesOutputPath = "/AssetBundles/";
    public string assetBundleName;
    public string assetName;

	public string resourceDomain = "https://s3-ap-northeast-1.amazonaws.com/patch.s3.sand.mbl.mobcast.io/";
	public string resourceVersion = "0cbe0f6edd536caea5cf5651cd4bf922cb9110ac";


	public RawImage image;

	public Text m_Log;
	public Text m_Progress;

	void Update()
	{
		m_Log.text = AssetBundleManager.GenerateReportText ().ToString ();
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

	public void InitializeXXX()
	{
		AssetBundleManager.SetSourceAssetBundleURL(resourceDomain + resourceVersion);
		AssetBundleManager.m_ManifestHash = Hash128.Parse (resourceVersion);

//		AssetBundleManager.m_CacheAssetBundleManifest = true;
		var request = AssetBundleManager.Initialize();
	}


	public void UpdateManifest()
	{
//		AssetBundleManager.m_RuntimeCache.Clear ();
		AssetBundleManager.SetSourceAssetBundleURL(resourceDomain + resourceVersion);
		AssetBundleManager.m_ManifestHash = Hash128.Parse (resourceVersion);
		AssetBundleManager.UpdateManifest();
	}

	public void LoadAll()
	{
		StartCoroutine (CoLoadAll());

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

	IEnumerator CoLoadAll()
	{
		string baseUrl = AssetBundleManager.BaseDownloadingURL;

		var manifest = AssetBundleManager.AssetBundleManifestObject;
		foreach (var name in manifest.GetAllAssetBundles()) {
			Debug.LogFormat ("url:{0}, hash:{1}, cached:{2}", baseUrl + name, manifest.GetAssetBundleHash (name), Caching.IsVersionCached (baseUrl + name, manifest.GetAssetBundleHash (name)));
			if (!Caching.IsVersionCached (baseUrl + name, manifest.GetAssetBundleHash (name))) {
				AssetBundleManager.LoadAssetBundle (name);
			}
		}

		// download operation.
		List<AssetBundleDownloadOperation> downloads = AssetBundleManager.InProgressOperations
			.OfType<AssetBundleDownloadOperation> ()
			.ToList();

		// wait for finish.
		int count = downloads.Count;
		while(downloads.Any(x=>!x.IsDone()))
		{
			yield return null;
			float progress = downloads.Where (x => string.IsNullOrEmpty (x.error)).Sum (x=>x.progress);
			int succeed = downloads.Count (x => x.IsDone () && string.IsNullOrEmpty (x.error));
			m_Progress.text = string.Format ("{0}/{1} {2:P1}%", succeed, count, progress/count);
		}


		yield break;
	}


	public void Load()
	{
		StartCoroutine (CoLoad());
//		AssetBundleManager.LoadAssetAsync<Texture2D>(assetBundleName, assetName, obj => image.texture = obj);
	}

	IEnumerator CoLoad()
	{
		var op = AssetBundleManager.LoadAssetAsync(assetBundleName, assetName, typeof(Texture2D));

		yield return StartCoroutine (op);
		image.texture = op.GetAsset<Texture2D> ();
	}

	public void ClearAll()
	{
		AssetBundleManager.DeleteAssetBundleAll ();
//		string baseUrl = AssetBundleManager.BaseDownloadingURL;
//
//		var manifest = AssetBundleManager.AssetBundleManifestObject;
//		foreach (var name in manifest.GetAllAssetBundles()) {
//			Debug.LogFormat ("url:{0}, hash:{1}, cached:{2}", baseUrl + name, manifest.GetAssetBundleHash (name), Caching.IsVersionCached (baseUrl + name, manifest.GetAssetBundleHash (name)));
//
//			if (Caching.IsVersionCached (baseUrl + name, manifest.GetAssetBundleHash (name))) {
//				Debug.Log ("Delete! " + name);
//				WWW.LoadFromCacheOrDownload (baseUrl + name, manifest.GetAssetBundleHash (name), uint.MaxValue);
//			}
//		}
//
//		Caching.CleanCache ();
	}

	/*
	public void Check()
	{
		string platform = Utility.GetPlatformName ();
		string baseUrl = AssetBundleManager.BaseDownloadingURL;

		Debug.LogFormat ("manifest url:{0}, cached:{1}", baseUrl + platform, Caching.IsVersionCached (baseUrl + platform, 0));


		var manifest = AssetBundleManager.AssetBundleManifestObject;
		foreach (var name in manifest.GetAllAssetBundles()) {
			Debug.LogFormat ("url:{0}, hash:{1}, cached:{2}", baseUrl + name, manifest.GetAssetBundleHash (name), Caching.IsVersionCached (baseUrl + name, manifest.GetAssetBundleHash (name)));
		}



		Debug.LogFormat ("spaceOccupied {0}",
			Caching.spaceOccupied
		);
	}
	*/
//
//    // Initialize the downloading URL.
//    // eg. Development server / iOS ODR / web URL
//    void InitializeSourceURL()
//    {
//
//		/*
//		AssetBundleManager.SetDevelopmentAssetBundleServer();
//
//
//        // If ODR is available and enabled, then use it and let Xcode handle download requests.
//        #if ENABLE_IOS_ON_DEMAND_RESOURCES
//        if (UnityEngine.iOS.OnDemandResources.enabled)
//        {
//            AssetBundleManager.SetSourceAssetBundleURL("odr://");
//            return;
//        }
//        #endif
//        #if DEVELOPMENT_BUILD || UNITY_EDITOR
//        // With this code, when in-editor or using a development builds: Always use the AssetBundle Server
//        // (This is very dependent on the production workflow of the project.
//        //      Another approach would be to make this configurable in the standalone player.)
//        AssetBundleManager.SetDevelopmentAssetBundleServer();
//        return;
//        #else
//        // Use the following code if AssetBundles are embedded in the project for example via StreamingAssets folder etc:
//        AssetBundleManager.SetSourceAssetBundleURL(Application.dataPath + "/");
//        // Or customize the URL based on your deployment or configuration
//        //AssetBundleManager.SetSourceAssetBundleURL("http://www.MyWebsite/MyAssetBundles");
//        return;
//        #endif
//        */
//    }
//
//    // Initialize the downloading url and AssetBundleManifest object.
//    protected IEnumerator Initialize()
//    {
//        // Don't destroy this gameObject as we depend on it to run the loading script.
//        DontDestroyOnLoad(gameObject);
//
//        InitializeSourceURL();
//
//        // Initialize AssetBundleManifest which loads the AssetBundleManifest object.
//        var request = AssetBundleManager.Initialize();
//        if (request != null)
//            yield return StartCoroutine(request);
//    }
//
//    protected IEnumerator InstantiateGameObjectAsync(string assetBundleName, string assetName)
//    {
//        // This is simply to get the elapsed time for this phase of AssetLoading.
//        float startTime = Time.realtimeSinceStartup;
//
//        // Load asset from assetBundle.
//        AssetBundleLoadAssetOperation request = AssetBundleManager.LoadAssetAsync(assetBundleName, assetName, typeof(GameObject));
//        if (request == null)
//            yield break;
//        yield return StartCoroutine(request);
//
//        // Get the asset.
//        GameObject prefab = request.GetAsset<GameObject>();
//
//        if (prefab != null)
//            GameObject.Instantiate(prefab);
//
//        // Calculate and display the elapsed time.
//        float elapsedTime = Time.realtimeSinceStartup - startTime;
//        Debug.Log(assetName + (prefab == null ? " was not" : " was") + " loaded successfully in " + elapsedTime + " seconds");
//    }
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
