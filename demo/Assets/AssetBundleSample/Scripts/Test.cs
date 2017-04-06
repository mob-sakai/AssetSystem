using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using AssetBundles;


public class Test : MonoBehaviour
{
    public const string AssetBundlesOutputPath = "/AssetBundles/";
    public string assetBundleName;
    public string assetName;

	public string resourceDomain = "https://s3-ap-northeast-1.amazonaws.com/patch.s3.sand.mbl.mobcast.io/";
	public string resourceVersion = "0cbe0f6edd536caea5cf5651cd4bf922cb9110ac";


	public RawImage image;

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
		AssetBundleManager.m_CacheAssetBundleManifest = true;
		var request = AssetBundleManager.Initialize();
	}


	public void UpdateManifest()
	{
		AssetBundleManager.m_RuntimeCache.Clear ();
		AssetBundleManager.SetSourceAssetBundleURL(resourceDomain + resourceVersion);
		AssetBundleManager.UpdateManifest();
	}

	public void LoadAll()
	{
		string baseUrl = AssetBundleManager.BaseDownloadingURL;

		var manifest = AssetBundleManager.AssetBundleManifestObject;
		foreach (var name in manifest.GetAllAssetBundles()) {
			Debug.LogFormat ("url:{0}, hash:{1}, cached:{2}", baseUrl + name, manifest.GetAssetBundleHash (name), Caching.IsVersionCached (baseUrl + name, manifest.GetAssetBundleHash (name)));

//			if (!Caching.IsVersionCached (baseUrl + name, manifest.GetAssetBundleHash (name))) {
				AssetBundleManager.LoadAssetBundle (name);
//			}
		}
	}


	public void Load()
	{
		AssetBundleManager.LoadAssetAsync<Texture2D>(assetBundleName, assetName, obj => image.texture = obj);
	}

	public void ClearAll()
	{
		string baseUrl = AssetBundleManager.BaseDownloadingURL;

		var manifest = AssetBundleManager.AssetBundleManifestObject;
		foreach (var name in manifest.GetAllAssetBundles()) {
			Debug.LogFormat ("url:{0}, hash:{1}, cached:{2}", baseUrl + name, manifest.GetAssetBundleHash (name), Caching.IsVersionCached (baseUrl + name, manifest.GetAssetBundleHash (name)));

			if (Caching.IsVersionCached (baseUrl + name, manifest.GetAssetBundleHash (name))) {
				Debug.Log ("Delete! " + name);
				WWW.LoadFromCacheOrDownload (baseUrl + name, manifest.GetAssetBundleHash (name), uint.MaxValue);
			}
		}

		Caching.CleanCache ();
	}


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
