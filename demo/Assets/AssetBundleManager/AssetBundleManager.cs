using UnityEngine;
#if UNITY_5_4_OR_NEWER
using UnityEngine.Networking;
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using System.Collections;
using System.Collections.Generic;

/*  The AssetBundle Manager provides a High-Level API for working with AssetBundles. 
    The AssetBundle Manager will take care of loading AssetBundles and their associated 
    Asset Dependencies.
        Initialize()
            Initializes the AssetBundle manifest object.
        LoadAssetAsync()
            Loads a given asset from a given AssetBundle and handles all the dependencies.
        LoadLevelAsync()
            Loads a given scene from a given AssetBundle and handles all the dependencies.
        LoadDependencies()
            Loads all the dependent AssetBundles for a given AssetBundle.
        BaseDownloadingURL
            Sets the base downloading url which is used for automatic downloading dependencies.
        SimulateAssetBundleInEditor
            Sets Simulation Mode in the Editor.
        Variants
            Sets the active variant.
        RemapVariantName()
            Resolves the correct AssetBundle according to the active variant.
*/

namespace AssetBundles
{
    /// <summary>
    /// Loaded assetBundle contains the references count which can be used to
    /// unload dependent assetBundles automatically.
    /// </summary>
    public class LoadedAssetBundle
    {
        public AssetBundle m_AssetBundle;
        internal event Action unload;

        internal void OnUnload()
        {
            m_AssetBundle.Unload(false);
            if (unload != null)
                unload();
        }

        public LoadedAssetBundle(AssetBundle assetBundle)
        {
            m_AssetBundle = assetBundle;
        }
    }

    /// <summary>
    /// Class takes care of loading assetBundle and its dependencies
    /// automatically, loading variants automatically.
    /// </summary>
    public class AssetBundleManager : MonoBehaviour
    {
        public enum LogMode { All, JustErrors };
        public enum LogType { Info, Warning, Error };

        static LogMode m_LogMode = LogMode.All;
        static string m_BaseDownloadingURL = "";
        static string[] m_ActiveVariants =  {};
        static AssetBundleManifest m_AssetBundleManifest = null;

#if UNITY_EDITOR
        static int m_SimulateAssetBundleInEditor = -1;
        const string kSimulateAssetBundles = "SimulateAssetBundles";
#endif

        static Dictionary<string, LoadedAssetBundle> m_LoadedAssetBundles = new Dictionary<string, LoadedAssetBundle>();
        static Dictionary<string, string> m_DownloadingErrors = new Dictionary<string, string>();
        static List<string> m_DownloadingBundles = new List<string>();
        static List<AssetBundleLoadOperation> m_InProgressOperations = new List<AssetBundleLoadOperation>();
        static Dictionary<string, string[]> m_Dependencies = new Dictionary<string, string[]>();
		static Dictionary<string, UnityEngine.Object> m_RuntimeCache = new Dictionary<string, UnityEngine.Object>();
		static Hash128 m_ManifestHash = new Hash128 ();
		static Dictionary<string, HashSet<string>> m_Depended = new Dictionary<string, HashSet<string>> ();
		static HashSet<string> m_Unloadable = new HashSet<string> ();

		/// <summary>
		/// Gets the in progress operations.
		/// </summary>
		public static List<AssetBundleLoadOperation> InProgressOperations { get { return m_InProgressOperations; } }


        public static LogMode logMode
        {
            get { return m_LogMode; }
            set { m_LogMode = value; }
        }

        /// <summary>
        /// The base downloading url which is used to generate the full
        /// downloading url with the assetBundle names.
        /// </summary>
        public static string BaseDownloadingURL
        {
            get { return m_BaseDownloadingURL; }
            set { m_BaseDownloadingURL = value; }
        }

        public delegate string OverrideBaseDownloadingURLDelegate(string bundleName);

        /// <summary>
        /// Implements per-bundle base downloading URL override.
        /// The subscribers must return null values for unknown bundle names;
        /// </summary>
        public static event OverrideBaseDownloadingURLDelegate overrideBaseDownloadingURL;

        /// <summary>
        /// Variants which is used to define the active variants.
        /// </summary>
        public static string[] ActiveVariants
        {
            get { return m_ActiveVariants; }
            set { m_ActiveVariants = value; }
        }

        /// <summary>
        /// AssetBundleManifest object which can be used to load the dependecies
        /// and check suitable assetBundle variants.
        /// </summary>
        public static AssetBundleManifest AssetBundleManifestObject
        {
			get {return m_AssetBundleManifest; }
            set {m_AssetBundleManifest = value; }
        }

        private static void Log(LogType logType, string text)
        {
            if (logType == LogType.Error)
                Debug.LogError("[AssetBundleManager] " + text);
            else if (m_LogMode == LogMode.All && logType == LogType.Warning)
                Debug.LogWarning("[AssetBundleManager] " + text);
            else if (m_LogMode == LogMode.All)
                Debug.Log("[AssetBundleManager] " + text);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Flag to indicate if we want to simulate assetBundles in Editor without building them actually.
        /// </summary>
        public static bool SimulateAssetBundleInEditor
        {
            get
            {
                if (m_SimulateAssetBundleInEditor == -1)
                    m_SimulateAssetBundleInEditor = EditorPrefs.GetBool(kSimulateAssetBundles, true) ? 1 : 0;

                return m_SimulateAssetBundleInEditor != 0;
            }
            set
            {
                int newValue = value ? 1 : 0;
                if (newValue != m_SimulateAssetBundleInEditor)
                {
                    m_SimulateAssetBundleInEditor = newValue;
                    EditorPrefs.SetBool(kSimulateAssetBundles, value);
                }
            }
        }
#endif

        private static string GetStreamingAssetsPath()
        {
            if (Application.isEditor)
                return "file://" +  System.Environment.CurrentDirectory.Replace("\\", "/"); // Use the build output folder directly.
            else if (Application.isWebPlayer)
                return System.IO.Path.GetDirectoryName(Application.absoluteURL).Replace("\\", "/") + "/StreamingAssets";
            else if (Application.isMobilePlatform || Application.isConsolePlatform)
                return Application.streamingAssetsPath;
            else // For standalone player.
                return "file://" +  Application.streamingAssetsPath;
        }

        /// <summary>
        /// Sets base downloading URL to a directory relative to the streaming assets directory.
        /// Asset bundles are loaded from a local directory.
        /// </summary>
        public static void SetSourceAssetBundleDirectory(string relativePath)
        {
            BaseDownloadingURL = GetStreamingAssetsPath() + relativePath;
        }

        /// <summary>
        /// Sets base downloading URL to a web URL. The directory pointed to by this URL
        /// on the web-server should have the same structure as the AssetBundles directory
        /// in the demo project root.
        /// </summary>
        /// <example>For example, AssetBundles/iOS/xyz-scene must map to
        /// absolutePath/iOS/xyz-scene.
        /// <example>
        public static void SetSourceAssetBundleURL(string absolutePath)
        {
            if (absolutePath.StartsWith("/"))
            {
                absolutePath = "file://" + absolutePath;
            }
            if (!absolutePath.EndsWith("/"))
            {
                absolutePath += "/";
            }

            BaseDownloadingURL = absolutePath + Utility.GetPlatformName() + "/";
        }

        /// <summary>
        /// Sets base downloading URL to a local development server URL.
        /// </summary>
        public static void SetDevelopmentAssetBundleServer()
        {
#if UNITY_EDITOR
            // If we're in Editor simulation mode, we don't have to setup a download URL
            if (SimulateAssetBundleInEditor)
                return;
#endif

            TextAsset urlFile = Resources.Load("AssetBundleServerURL") as TextAsset;
            string url = (urlFile != null) ? urlFile.text.Trim() : null;
            if (url == null || url.Length == 0)
            {
                Log(LogType.Error, "Development Server URL could not be found.");
            }
            else
            {
                AssetBundleManager.SetSourceAssetBundleURL(url);
            }
        }

        /// <summary>
        /// Retrieves an asset bundle that has previously been requested via LoadAssetBundle.
        /// Returns null if the asset bundle or one of its dependencies have not been downloaded yet.
        /// </summary>
        static public LoadedAssetBundle GetLoadedAssetBundle(string assetBundleName, out string error)
        {
            if (m_DownloadingErrors.TryGetValue(assetBundleName, out error))
                return null;

            LoadedAssetBundle bundle = null;
            m_LoadedAssetBundles.TryGetValue(assetBundleName, out bundle);
            if (bundle == null)
                return null;

            // No dependencies are recorded, only the bundle itself is required.
            string[] dependencies = null;
            if (!m_Dependencies.TryGetValue(assetBundleName, out dependencies))
                return bundle;

            // Make sure all dependencies are loaded
            foreach (var dependency in dependencies)
            {
                if (m_DownloadingErrors.TryGetValue(dependency, out error))
                    return null;

                // Wait all the dependent assetBundles being loaded.
                LoadedAssetBundle dependentBundle;
                m_LoadedAssetBundles.TryGetValue(dependency, out dependentBundle);
                if (dependentBundle == null)
                    return null;
            }

            return bundle;
        }

        /// <summary>
        /// Returns true if certain asset bundle has been downloaded without checking
        /// whether the dependencies have been loaded.
        /// </summary>
        static public bool IsAssetBundleDownloaded(string assetBundleName)
        {
            return m_LoadedAssetBundles.ContainsKey(assetBundleName);
        }


		/// <summary>
		/// シングルトンインスタンスを取得します.
		/// シーン上にインスタンスが存在しない場合、自動的に生成されます.
		/// </summary>
		public static AssetManager instance
		{
			get
			{
				if (_instance == null)
				{
					//シーン内のインスタンスを検索します.
					_instance = GameObject.FindObjectOfType<AssetManager>();

					//シーン内にインスタンスが存在しない場合、自動的に生成します.
					if (_instance == null)
					{
						_instance = new GameObject(typeof(AssetManager).Name).AddComponent<AssetManager>();
					}

					//インスタンスをアクティブにします.
					_instance.enabled = true;
					_instance.gameObject.SetActive(true);
				}

				return _instance;
			}
		}

		static AssetManager _instance;

		/// <summary>
		/// コンポーネントの生成コールバック.
		/// インスタンスが生成された時に、コールされます
		/// </summary>
		protected virtual void Awake()
		{
			//初めてのインスタンスは、シングルトンインスタンスとして登録.
			if (_instance == null)
				_instance = GetComponent<AssetManager>();

			//複数のシングルトンインスタンスは許可しません.
			if (_instance != this)
			{
				Debug.LogErrorFormat(this, "Multiple {0} is not allowed. please fix it.", typeof(AssetManager).Name);
				enabled = false;
				return;
			}

			DontDestroyOnLoad(gameObject);
			AssetBundleDownloadOperation.onComplete = OnCompleteAssetBundleDownloadOperation;

#if UNITY_EDITOR
			Log(LogType.Info, "Simulation Mode: " + (SimulateAssetBundleInEditor ? "Enabled" : "Disabled"));
#endif
		}

		/// <summary>
		/// コンポーネントの破棄コールバック.
		/// インスタンスが破棄された時にコールされます.
		/// </summary>
		protected virtual void OnDestroy()
		{
			//自身がシングルトンインスタンスの場合、登録を解除します.
			if (_instance == this)
				_instance = null;
		}

		/// <summary>
		/// Preloads the asset bundle.
		/// </summary>
		/// <returns>The asset bundle.</returns>
		public static AssetBundlePreDownloadOperation PreloadAssetBundle(System.Action<AssetBundlePreDownloadOperation> onComplete = null)
		{
			if (!m_AssetBundleManifest)
			{
				Log(LogType.Error, "Please initialize AssetBundleManifest by calling AssetManager.Initialize()");
				return null;
			}
            
			foreach (var name in m_AssetBundleManifest.GetAllAssetBundles()) {
				var hash = m_AssetBundleManifest.GetAssetBundleHash (name);
				bool cached = Caching.IsVersionCached (name, hash);
				Debug.LogFormat ("name:{0}, hash:{1}, cached:{2}", name, hash, cached);
				if (!cached)
				{
					AssetManager.LoadAssetBundle (name);
				}
			}

			var operations = new List<AssetBundleDownloadOperation> ();
			foreach (var op in AssetManager.InProgressOperations)
			{
				if (op is AssetBundleDownloadOperation)
					operations.Add (op as AssetBundleDownloadOperation);
			}

			var operation = new AssetBundlePreDownloadOperation (operations);
			if(onComplete != null)
				operation.onComplete += onComplete;
			
			m_InProgressOperations.Add (operation);

			return operation;
		}

        // Temporarily work around a il2cpp bug
		static public void LoadAssetBundle(string assetBundleName)
        {
            LoadAssetBundle(assetBundleName, false);
        }
            
        // Starts the download of the asset bundle identified by the given name, and asset bundles
        // that this asset bundle depends on.
        static protected void LoadAssetBundle(string assetBundleName, bool isLoadingAssetBundleManifest)
        {
            Log(LogType.Info, "Loading Asset Bundle " + (isLoadingAssetBundleManifest ? "Manifest: " : ": ") + assetBundleName);

#if UNITY_EDITOR
            // If we're in Editor simulation mode, we don't have to really load the assetBundle and its dependencies.
            if (SimulateAssetBundleInEditor)
                return;
#endif

            if (!isLoadingAssetBundleManifest)
            {
                if (m_AssetBundleManifest == null)
                {
                    Log(LogType.Error, "Please initialize AssetBundleManifest by calling AssetBundleManager.Initialize()");
                    return;
                }
            }

            // Check if the assetBundle has already been processed.
            bool isAlreadyProcessed = LoadAssetBundleInternal(assetBundleName, isLoadingAssetBundleManifest);

            // Load dependencies.
            if (!isAlreadyProcessed && !isLoadingAssetBundleManifest)
                LoadDependencies(assetBundleName);
        }

        // Returns base downloading URL for the given asset bundle.
        // This URL may be overridden on per-bundle basis via overrideBaseDownloadingURL event.
        protected static string GetAssetBundleBaseDownloadingURL(string bundleName)
        {
            if (overrideBaseDownloadingURL != null)
            {
                foreach (OverrideBaseDownloadingURLDelegate method in overrideBaseDownloadingURL.GetInvocationList())
                {
                    string res = method(bundleName);
                    if (res != null)
                        return res;
                }
            }
            return m_BaseDownloadingURL;
        }

        // Checks who is responsible for determination of the correct asset bundle variant
        // that should be loaded on this platform. 
        //
        // On most platforms, this is done by the AssetBundleManager itself. However, on
        // certain platforms (iOS at the moment) it's possible that an external asset bundle
        // variant resolution mechanism is used. In these cases, we use base asset bundle 
        // name (without the variant tag) as the bundle identifier. The platform-specific 
        // code is responsible for correctly loading the bundle.
        static protected bool UsesExternalBundleVariantResolutionMechanism(string baseAssetBundleName)
        {
#if ENABLE_IOS_APP_SLICING
            var url = GetAssetBundleBaseDownloadingURL(baseAssetBundleName);
            if (url.ToLower().StartsWith("res://") ||
                url.ToLower().StartsWith("odr://"))
                return true;
#endif
            return false;
        }

        // Remaps the asset bundle name to the best fitting asset bundle variant.
        static protected string RemapVariantName(string assetBundleName)
        {
            string[] bundlesWithVariant = m_AssetBundleManifest.GetAllAssetBundlesWithVariant();

            // Get base bundle name
            string baseName = assetBundleName.Split('.')[0];

            if (UsesExternalBundleVariantResolutionMechanism(baseName))
                return baseName;

            int bestFit = int.MaxValue;
            int bestFitIndex = -1;
            // Loop all the assetBundles with variant to find the best fit variant assetBundle.
            for (int i = 0; i < bundlesWithVariant.Length; i++)
            {
                string[] curSplit = bundlesWithVariant[i].Split('.');
                string curBaseName = curSplit[0];
                string curVariant = curSplit[1];

                if (curBaseName != baseName)
                    continue;

                int found = System.Array.IndexOf(m_ActiveVariants, curVariant);

                // If there is no active variant found. We still want to use the first
                if (found == -1)
                    found = int.MaxValue - 1;

                if (found < bestFit)
                {
                    bestFit = found;
                    bestFitIndex = i;
                }
            }

            if (bestFit == int.MaxValue - 1)
            {
                Log(LogType.Warning, "Ambigious asset bundle variant chosen because there was no matching active variant: " + bundlesWithVariant[bestFitIndex]);
            }

            if (bestFitIndex != -1)
            {
                return bundlesWithVariant[bestFitIndex];
            }
            else
            {
                return assetBundleName;
            }
        }

		public static void AddDepend(string assetBundleName, string dependedId)
		{
			if (string.IsNullOrEmpty (assetBundleName))
				return;
			
			HashSet<string> depended;
			if(!m_Depended.TryGetValue(assetBundleName, out depended))
			{
				if (string.IsNullOrEmpty (dependedId))
				{
					m_Unloadable.Add (assetBundleName);
					return;
				}

				depended = new HashSet<string> ();
				m_Depended.Add (assetBundleName, depended);
			}
			if(!string.IsNullOrEmpty(dependedId))
				depended.Add (dependedId);
			
			UpdateDepend (assetBundleName, dependedId, depended);
		}


		public static void SubDepend(string assetBundleName, string dependedId)
		{
			if (string.IsNullOrEmpty (assetBundleName))
				return;
			
			HashSet<string> depended;
			if(m_Depended.TryGetValue(assetBundleName, out depended))
			{
				if(!string.IsNullOrEmpty(dependedId))
					depended.Remove (dependedId);
			}
			UpdateDepend (assetBundleName, dependedId, depended);
		}

		public static void UpdateDepend(string assetBundleName, string dependedId, HashSet<string> depended)
		{
			if (depended != null && 0 < depended.Count)
			{
				m_Unloadable.Remove (assetBundleName);
			}
			else
			{
				m_Unloadable.Add (assetBundleName);
				m_Depended.Remove (assetBundleName);
			}
		}

        // Sets up download operation for the given asset bundle if it's not downloaded already.
        static protected bool LoadAssetBundleInternal(string assetBundleName, bool isLoadingAssetBundleManifest)
        {
            // Already loaded.
            LoadedAssetBundle bundle = null;
            m_LoadedAssetBundles.TryGetValue(assetBundleName, out bundle);
            if (bundle != null)
            {
                return true;
            }

            // @TODO: Do we need to consider the referenced count of WWWs?
            // In the demo, we never have duplicate WWWs as we wait LoadAssetAsync()/LoadLevelAsync() to be finished before calling another LoadAssetAsync()/LoadLevelAsync().
            // But in the real case, users can call LoadAssetAsync()/LoadLevelAsync() several times then wait them to be finished which might have duplicate WWWs.
            if (m_DownloadingBundles.Contains(assetBundleName))
                return true;

            string bundleBaseDownloadingURL = GetAssetBundleBaseDownloadingURL(assetBundleName);

            if (bundleBaseDownloadingURL.ToLower().StartsWith("odr://"))
            {
#if ENABLE_IOS_ON_DEMAND_RESOURCES
                Log(LogType.Info, "Requesting bundle " + assetBundleName + " through ODR");
                m_InProgressOperations.Add(new AssetBundleDownloadFromODROperation(assetBundleName));
#else
                new ApplicationException("Can't load bundle " + assetBundleName + " through ODR: this Unity version or build target doesn't support it.");
#endif
            }
            else if (bundleBaseDownloadingURL.ToLower().StartsWith("res://"))
            {
#if ENABLE_IOS_APP_SLICING
                Log(LogType.Info, "Requesting bundle " + assetBundleName + " through asset catalog");
                m_InProgressOperations.Add(new AssetBundleOpenFromAssetCatalogOperation(assetBundleName));
#else
                new ApplicationException("Can't load bundle " + assetBundleName + " through asset catalog: this Unity version or build target doesn't support it.");
#endif
            }
            else
            {
                if (!bundleBaseDownloadingURL.EndsWith("/"))
                {
                    bundleBaseDownloadingURL += "/";
                }

                string url = bundleBaseDownloadingURL + assetBundleName;

#if UNITY_5_4_OR_NEWER
                // If url refers to a file in StreamingAssets, use AssetBundle.LoadFromFileAsync to load.
                // UnityWebRequest also is able to load from there, but we use the former API because:
                // - UnityWebRequest under Android OS fails to load StreamingAssets files (at least Unity5.50 or less)
                // - or UnityWebRequest anyway internally calls AssetBundle.LoadFromFileAsync for StreamingAssets files
                if (url.StartsWith(Application.streamingAssetsPath)) {
                    m_InProgressOperations.Add(new AssetBundleDownloadFileOperation(assetBundleName, url));
                } else {
                    UnityWebRequest request = null;
                    if (isLoadingAssetBundleManifest) {
						// If hash is not zero, manifest will be cached. Otherwise, always manifest will be downloaded.
						Debug.LogFormat("LoadingAssetBundleManifest: {0}, {1} (cached:{2})", url, m_ManifestHash, Caching.IsVersionCached(assetBundleName, m_ManifestHash));
						request = m_ManifestHash != default(Hash128) ? UnityWebRequest.GetAssetBundle(url, m_ManifestHash, 0)
							: UnityWebRequest.GetAssetBundle(url);
                    } else {
                        request = UnityWebRequest.GetAssetBundle(url, m_AssetBundleManifest.GetAssetBundleHash(assetBundleName), 0);
                    }
                    m_InProgressOperations.Add(new AssetBundleDownloadWebRequestOperation(assetBundleName, request));
                }
#else
                WWW download = null;
                if (isLoadingAssetBundleManifest) {

					// If hash is not zero, manifest will be cached. Otherwise, always manifest will be downloaded.
					Debug.LogFormat("LoadingAssetBundleManifest: {0}, {1} (cached:{2})", url, m_ManifestHash, Caching.IsVersionCached(assetBundleName, m_ManifestHash));
					download = m_ManifestHash != default(Hash128) ? WWW.LoadFromCacheOrDownload(url, m_ManifestHash, 0)
							: WWW(url);
                    // For manifest assetbundle, always download it as we don't have hash for it.
                    download = new WWW(url);
                } else {
                    download = WWW.LoadFromCacheOrDownload(url, m_AssetBundleManifest.GetAssetBundleHash(assetBundleName), 0);
                }
                m_InProgressOperations.Add(new AssetBundleDownloadFromWebOperation(assetBundleName, download));
#endif
            }
            m_DownloadingBundles.Add(assetBundleName);

            return false;
        }

        // Where we get all the dependencies and load them all.
        static protected void LoadDependencies(string assetBundleName)
        {
            if (m_AssetBundleManifest == null)
            {
                Log(LogType.Error, "Please initialize AssetBundleManifest by calling AssetBundleManager.Initialize()");
                return;
            }

            // Get dependecies from the AssetBundleManifest object..
            string[] dependencies = m_AssetBundleManifest.GetAllDependencies(assetBundleName);
            if (dependencies.Length == 0)
                return;

            for (int i = 0; i < dependencies.Length; i++)
                dependencies[i] = RemapVariantName(dependencies[i]);

            // Record and load all dependencies.
            m_Dependencies.Add(assetBundleName, dependencies);
			foreach (var dependency in dependencies)
			{
				AddDepend (dependency, assetBundleName);
				LoadAssetBundleInternal (dependency, false);
			}
        }

        /// <summary>
        /// Unloads assetbundle and its dependencies.
        /// </summary>
        static public void UnloadAssetBundle(string assetBundleName)
        {
#if UNITY_EDITOR
            // If we're in Editor simulation mode, we don't have to load the manifest assetBundle.
            if (SimulateAssetBundleInEditor)
                return;
#endif
            assetBundleName = RemapVariantName(assetBundleName);

            UnloadDependencies(assetBundleName);
        }

        static protected void UnloadDependencies(string assetBundleName)
        {
            string[] dependencies = null;
            if (!m_Dependencies.TryGetValue(assetBundleName, out dependencies))
                return;

            // Loop dependencies.
            foreach (var dependency in dependencies)
            {
				SubDepend (dependency, assetBundleName);
            }

            m_Dependencies.Remove(assetBundleName);
        }

        static protected void UnloadAssetBundleInternal(string assetBundleName)
        {
            string error;
            LoadedAssetBundle bundle = GetLoadedAssetBundle(assetBundleName, out error);
            if (bundle == null)
                return;

            bundle.OnUnload();
            m_LoadedAssetBundles.Remove(assetBundleName);

            Log(LogType.Info, assetBundleName + " has been unloaded successfully");
        }

        void Update()
        {
            // Update all in progress operations
            for (int i = 0; i < m_InProgressOperations.Count;)
            {
                var operation = m_InProgressOperations[i];
                if (operation.Update())
                {
                    i++;
                }
                else
                {
                    m_InProgressOperations.RemoveAt(i);
					operation.OnComplete ();
                }
            }

			foreach(var assetBundleName in m_Unloadable)
				UnloadAssetBundleInternal (assetBundleName);
			m_Unloadable.Clear ();
        }

		static void OnCompleteAssetBundleDownloadOperation(AssetBundleDownloadOperation download)
        {
            if (download.error == null)
			{
                m_LoadedAssetBundles.Add(download.assetBundleName, download.assetBundle);
				AddDepend (download.assetBundleName, null);
			}
            else
            {
                string msg = string.Format("Failed downloading bundle {0} from {1}: {2}",
                        download.assetBundleName, download.GetSourceURL(), download.error);
                m_DownloadingErrors.Add(download.assetBundleName, msg);
            }

            m_DownloadingBundles.Remove(download.assetBundleName);
        }

		/// <summary>
		/// Starts a load operation for an asset from the given asset bundle.
		/// </summary>
		static public AssetBundleLoadAssetOperation LoadAssetAsync<T>(string assetBundleName, string assetName, System.Action<T> onLoad = null) where T:UnityEngine.Object
		{
			if(onLoad != null)
				return LoadAssetAsync (assetBundleName, assetName, typeof(T), obj => onLoad (obj as T));
			else
				return LoadAssetAsync (assetBundleName, assetName, typeof(T));
		}

        /// <summary>
        /// Starts a load operation for an asset from the given asset bundle.
        /// </summary>
		static public AssetBundleLoadAssetOperation LoadAssetAsync(string assetBundleName, string assetName, System.Type type, System.Action<UnityEngine.Object> onLoad = null)
        {
            Log(LogType.Info, "Loading " + assetName + " from " + assetBundleName + " bundle");

#if UNITY_EDITOR
            if (SimulateAssetBundleInEditor)
            {
                string[] assetPaths = AssetDatabase.GetAssetPathsFromAssetBundleAndAssetName(assetBundleName, assetName);
                if (assetPaths.Length == 0)
                {
                    Log(LogType.Error, "There is no asset with name \"" + assetName + "\" in " + assetBundleName);
                    return null;
                }

                // @TODO: Now we only get the main object from the first asset. Should consider type also.
                UnityEngine.Object target = AssetDatabase.LoadMainAssetAtPath(assetPaths[0]);
				AssetBundleLoadAssetOperationSimulation operation = new AssetBundleLoadAssetOperationSimulation(target);

				// Load complete callback.
				if (onLoad != null) {
					onLoad(target);
				}
				return operation;
            }
            else
#endif
            {
                assetBundleName = RemapVariantName(assetBundleName);
				string operationId = AssetBundleLoadAssetOperationFull.GetId (assetBundleName, assetName, type);

				// Operation has not been cached. Need load the assetbundle.
				if(!m_RuntimeCache.ContainsKey(operationId))
                	LoadAssetBundle(assetBundleName);

				// Search same operation in progress to merge load complete callbacks.
				AssetBundleLoadAssetOperationFull operation = null;
				foreach (var progressOperation in m_InProgressOperations) {
					var op = progressOperation as AssetBundleLoadAssetOperationFull;
					if (op != null && op.id == operationId) {
						operation = op;
						break;
					}
				}

				// When no same operation in progress, create new operation.
				if(operation == null)
				{
					operation = new AssetBundleLoadAssetOperationFull(assetBundleName, assetName, type, m_RuntimeCache);
					m_InProgressOperations.Add(operation);
				}

				// Add load complete callback.
				if (onLoad != null) {
					operation.onComplete += onLoad;
				}

				return operation;
            }
        }

        /// <summary>
        /// Starts a load operation for a level from the given asset bundle.
        /// </summary>
        static public AssetBundleLoadOperation LoadLevelAsync(string assetBundleName, string levelName, bool isAdditive)
        {
            Log(LogType.Info, "Loading " + levelName + " from " + assetBundleName + " bundle");

            AssetBundleLoadOperation operation = null;
#if UNITY_EDITOR
            if (SimulateAssetBundleInEditor)
            {
                operation = new AssetBundleLoadLevelSimulationOperation(assetBundleName, levelName, isAdditive);
            }
            else
#endif
            {
                assetBundleName = RemapVariantName(assetBundleName);
                LoadAssetBundle(assetBundleName);
                operation = new AssetBundleLoadLevelOperation(assetBundleName, levelName, isAdditive);

                m_InProgressOperations.Add(operation);
            }

            return operation;
        }

		/// <summary>
		/// Starts a load operation for an asset from Resources.
		/// </summary>
		static public AssetBundleLoadAssetOperationFull LoadAssetAsync<T>(string assetName, System.Action<T> onLoad = null) where T:UnityEngine.Object
		{
			if(onLoad != null)
				return LoadAssetAsync (assetName, typeof(T), obj => onLoad (obj as T));
			else
				return LoadAssetAsync (assetName, typeof(T));
		}

		/// <summary>
		/// Starts a load operation for an asset from Resources.
		/// </summary>
		static public AssetBundleLoadAssetOperationFull LoadAssetAsync(string assetName, System.Type type, System.Action<UnityEngine.Object> onLoad = null)
		{
			string operationId = LoadAssetOperation.GetId (assetName, type);
			Log(LogType.Info, "Loading " + assetName + ", " + operationId);

			// Search same operation in progress to merge load complete callbacks.
			AssetBundleLoadAssetOperationFull operation = null;
			foreach (var progressOperation in m_InProgressOperations) {
				var op = progressOperation as AssetBundleLoadAssetOperationFull;
				if (op != null && op.id == operationId) {
					operation = op;
					break;
				}
			}

			// When no same operation in progress, create new operation.
			if(operation == null)
			{
				// Asset is in Web or StreamingAssets.
				if (assetName.Contains ("://"))
				{
					operation = new LoadAssetWebRequestOperation(assetName, UnityWebRequest.GetTexture(assetName, true), type, m_RuntimeCache,
						dl =>(dl as DownloadHandlerTexture).texture);
				}
				else
					operation = new LoadAssetOperation(assetName, type, m_RuntimeCache);

				m_InProgressOperations.Add(operation);
			}

			// Add load complete callback.
			if (onLoad != null) {
				operation.onComplete += onLoad;
			}

			return operation;
		}

		/// <summary>
		/// Delete cached asset bundle.
		/// </summary>
		/// <param name="assetBundleName">AssetBundle name.</param>
		static public void ClearCachedAssetBundle(string assetBundleName)
		{
			if (!m_AssetBundleManifest)
			{
				Debug.LogError ("Please initialize AssetBundleManifest by calling AssetBundleManager.Initialize()");
				return;
			}

			ClearCachedAssetBundle (assetBundleName, m_AssetBundleManifest.GetAssetBundleHash (assetBundleName));
		}

		/// <summary>
		/// Delete cached asset bundle.
		/// </summary>
		/// <param name="assetBundleName">AssetBundle name.</param>
		/// <param name="hash">hash.</param>
		static public void ClearCachedAssetBundle(string assetBundleName, Hash128 hash)
		{
			UnloadAssetBundle (assetBundleName);
			if (Caching.IsVersionCached (assetBundleName, hash))
			{
				Debug.LogFormat ("Delete assetbundle {0}, hash {1}", assetBundleName, hash);
#if UNITY_5_4_OR_NEWER
				UnityWebRequest.GetAssetBundle (assetBundleName, hash, uint.MaxValue).Send();
#else
				// Although error log comes out, there is no problem.
				WWW.LoadFromCacheOrDownload (assetBundleName, hash, uint.MaxValue);
#endif
			}
		}

		/// <summary>
		/// Delete all cached asset bundle.
		/// </summary>
		static public void ClearCachedAssetBundleAll()
		{
			Caching.CleanCache ();

			if (!m_AssetBundleManifest)
				return;
			
			foreach (var bundleName in m_AssetBundleManifest.GetAllAssetBundles())
				ClearCachedAssetBundle (bundleName, m_AssetBundleManifest.GetAssetBundleHash(bundleName));
		}

		/// <summary>
		/// Update manifest.
		/// Compare manifests and delete old cached bundles.
		/// Starts download of manifest asset bundle.
		/// Returns the manifest asset bundle downolad operation object.
		/// </summary>
		/// <param name="manifest">Asset bundle manifest.</param>
		public static bool UpdateManifest (AssetBundleManifest manifest)
		{
			var oldManifest = m_AssetBundleManifest;
			m_AssetBundleManifest = manifest;

			if (!m_AssetBundleManifest)
			{
				Debug.LogError ("Failed to update manifest.");
				return false;
			}

			if (!oldManifest)
				return true;
			
			var oldBundles = new HashSet<string>( oldManifest.GetAllAssetBundles() );
			var newBundles = new HashSet<string>( manifest.GetAllAssetBundles() );

			foreach (var name in oldBundles) {
				var oldHash = oldManifest.GetAssetBundleHash (name);

				// The bundle has removed or changed. Need to delete cached bundle.
				if (!newBundles.Contains (name) || oldHash != manifest.GetAssetBundleHash (name))
					ClearCachedAssetBundle (name, oldHash);
			}
			return true;
		}

		/// <summary>
		/// Starts download of manifest asset bundle.
		/// Returns the manifest asset bundle downolad operation object.
		/// </summary>
		/// <param name="hash">Hash for manifest.</param>
		/// <param name="onComplete">callback.</param>
		static public AssetBundleLoadManifestOperation UpdateManifest(Hash128 hash = default(Hash128), System.Action<AssetBundleLoadManifestOperation> onComplete = null)
		{
			return UpdateManifest(Utility.GetPlatformName(), hash, onComplete);
		}

		/// <summary>
		/// Starts download of manifest asset bundle.
		/// Returns the manifest asset bundle downolad operation object.
		/// </summary>
		/// <param name="manifestAssetBundleName">Manifest asset bundle name.</param>
		/// <param name="hash">Hash for manifest.</param>
		/// <param name="onComplete">callback.</param>
		static public AssetBundleLoadManifestOperation UpdateManifest(string manifestAssetBundleName, Hash128 hash = default(Hash128), System.Action<AssetBundleLoadManifestOperation> onComplete = null)
		{
			#if UNITY_EDITOR
			// If we're in Editor simulation mode, we don't need the manifest assetBundle.
			if (SimulateAssetBundleInEditor)
				return null;
			#endif


			ClearRuntimeCacheAll ();
			ClearOperationsAll ();
			UnloadAssetbundlesAll ();

			if(hash != default(Hash128))
				m_ManifestHash = hash;
			LoadAssetBundle(manifestAssetBundleName, true);
			var operation = new AssetBundleLoadManifestOperation(manifestAssetBundleName);
			if(onComplete != null)
				operation.onComplete += _ =>onComplete(operation);
			
			m_InProgressOperations.Add(operation);
			return operation;
		}

		public static System.Text.StringBuilder GenerateReportText()
		{
			System.Text.StringBuilder sb = new System.Text.StringBuilder ();

			sb.AppendFormat ("[{0} Report] manifestHash:{1}, spaceOccupied:{2}, RuntimeCached:{3}, LoadedBundles:{4}, InProgress:{5}, DownloadingBundles:{6}\n",
				typeof(AssetBundleManager).Name,
				m_ManifestHash,
				Caching.spaceOccupied,
				m_RuntimeCache.Count,
				m_LoadedAssetBundles.Count,
				m_InProgressOperations.Count,
				m_DownloadingBundles.Count
			);

			if (0 < InProgressOperations.Count)
			{
				sb.Append ("\n[InProgress]\n");
				foreach(var op in m_InProgressOperations)
					sb.AppendFormat ("  {0}\n",op.GetType().Name);
			}

			if (0 < m_DownloadingBundles.Count)
			{
				sb.Append ("\n[DownloadingBundles]\n");
				foreach(var name in m_DownloadingBundles)
					sb.AppendFormat ("  {0}\n", name);
			}

			if (0 < m_Depended.Count)
			{
				sb.Append ("\n[Depended] ");
				foreach(var key in m_Depended.Keys)
				{
					sb.AppendFormat ("\n{0}: ",key);
					foreach(var id in m_Depended[key])
						sb.AppendFormat ("{0}: ,", id);
				}
			}

			if (0 < m_DownloadingErrors.Count)
			{
				sb.Append ("\n[DownloadingErrors] ");
				foreach(var error in m_DownloadingErrors)
					sb.AppendFormat ("\n{0}: ", error);
			}

			if(m_AssetBundleManifest)
			{
				var bundles = m_AssetBundleManifest.GetAllAssetBundles ();

				System.Text.StringBuilder sbCached = new System.Text.StringBuilder ();
				System.Text.StringBuilder sbNotCached = new System.Text.StringBuilder ();
				int cached = 0;
				foreach(var ab in bundles)
				{
					var hash = m_AssetBundleManifest.GetAssetBundleHash (ab);
					string summary = string.Format ("  {0} [{1}]", ab, hash.ToString ().Substring (0, 4));
					if (Caching.IsVersionCached (ab, hash)) {
						sbCached.AppendLine (summary);
						cached++;
					} else {
						sbNotCached.AppendLine (summary);
					}
				}

				sb.AppendFormat ("\n[Manifest] AllAssetBundles:{0} cached:{1}\n", bundles.Length, cached);
				sb.AppendFormat("\n[Cached]\n{0}", sbCached);
				sb.AppendFormat("\n[NotCached]\n{0}", sbNotCached);
			}

			return sb;
		}

		/// <summary>
		/// Unloads the assetbundle all.
		/// </summary>
		public static void UnloadAssetbundlesAll()
		{
			foreach(var assetBundleName in new List<string>(m_LoadedAssetBundles.Keys))
				UnloadAssetBundleInternal (assetBundleName);
			m_LoadedAssetBundles.Clear ();
		}

		/// <summary>
		/// Clears the runtime cache.
		/// </summary>
		public static void ClearRuntimeCacheAll()
		{
			m_RuntimeCache.Clear ();
		}

		/// <summary>
		/// Clears the operations.
		/// </summary>
		public static void ClearOperationsAll()
		{
			m_DownloadingErrors.Clear ();
			m_InProgressOperations.ForEach (op => op.OnCancel());
			m_InProgressOperations.Clear ();
		}

		/// <summary>
		/// Clears the operations.
		/// </summary>
		public static void ClearAll()
		{
			ClearRuntimeCacheAll ();
			ClearOperationsAll ();
			UnloadAssetbundlesAll ();
			ClearCachedAssetBundleAll ();
			Resources.UnloadUnusedAssets ();
		}
	} // End of AssetBundleManager.
}
