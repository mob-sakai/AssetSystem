using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using UnityEngine.Networking;
using Object = UnityEngine.Object;
using System.Text;

namespace Mobcast.Coffee.AssetSystem
{
	public class AssetManager : MonoSingleton<AssetManager>
	{

		#if UNITY_STANDALONE_OSX
		const string Platform = "OSX";
		#elif UNITY_EDITOR_WIN
		const string Platform = "Windows";
		#elif UNITY_ANDROID
		const string Platform = "Android";
		#elif UNITY_IOS || UNITY_IPHONE
		const string Platform = "iOS";
		#elif UNITY_WEBGL
		const string Platform = "WebGL";
		#else
		const string Platform = "Unknown";
		#endif

		/// <summary>
		/// AssetBundleManifest object which can be used to load the dependecies
		/// and check suitable assetBundle variants.
		/// </summary>
		public static AssetBundleManifest Manifest { set; get; }

		/// <summary>
		/// The base downloading url which is used to generate the full
		/// downloading url with the assetBundle names.
		/// </summary>
		public static string SourceURL { set; get; }

		public static Dictionary<string, AssetBundle> m_LoadedAssetBundles = new Dictionary<string, AssetBundle>();
		//		static Dictionary<string, string> m_DownloadingErrors = new Dictionary<string, string>();
		//		static List<string> m_DownloadingBundles = new List<string>();
		public static List<AssetOperation> m_InProgressOperations = new List<AssetOperation>();
		//		public static Dictionary<string, string[]> m_Dependencies = new Dictionary<string, string[]>();
		public static Dictionary<string, UnityEngine.Object> m_RuntimeCache = new Dictionary<string, UnityEngine.Object>();
		public static Hash128 m_ManifestHash = new Hash128();
		public static Dictionary<string, HashSet<string>> m_Depended = new Dictionary<string, HashSet<string>>();
		static HashSet<string> m_Unloadable = new HashSet<string>();


		public static StringBuilder errorLog = new StringBuilder();


		static bool SimulateAssetBundleInEditor;


		IEnumerator Start()
		{
			yield return new WaitUntil(() => Caching.ready);
			m_ManifestHash = Hash128.Parse(PlayerPrefs.GetString("AssetBundleManifestHash"));

			//TODO: 最後に利用したまニフェkストをロード.
			if (Caching.IsVersionCached(Platform, m_ManifestHash))
			{
				yield return StartCoroutine(UpdateManifest(m_ManifestHash));
			}
			yield break;
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
					operation.OnComplete();
					if (!string.IsNullOrEmpty(operation.error))
						errorLog.AppendLine(operation.error);
				}
			}

			foreach (var assetBundleName in m_Unloadable)
				UnloadAssetBundleInternal(assetBundleName);
			m_Unloadable.Clear();
		}

		/// <summary>
		/// Sets base downloading URL to a web URL. The directory pointed to by this URL
		/// on the web-server should have the same structure as the AssetBundles directory
		/// in the demo project root.
		/// </summary>
		/// <example>For example, AssetBundles/iOS/xyz-scene must map to
		/// absolutePath/iOS/xyz-scene.
		/// <example>
		public static void SetURL(string absolutePath)
		{
			if (!absolutePath.EndsWith("/"))
				absolutePath += "/";

			SourceURL = absolutePath + Platform + "/";
		}


		/// <summary>
		/// Retrieves an asset bundle that has previously been requested via LoadAssetBundle.
		/// Returns null if the asset bundle or one of its dependencies have not been downloaded yet.
		/// </summary>
		static public bool TryGetAssetBundle(string assetBundleName, out AssetBundle bundle)
		{
			return m_LoadedAssetBundles.TryGetValue(assetBundleName, out bundle);
		}


		/// <summary>
		/// Preloads the asset bundle.
		/// </summary>
		/// <returns>The asset bundle.</returns>
		public static BundlePreLoadOperation PreDownload()
		{
			if (!Manifest)
			{
//				Log(LogType.Error, "Please initialize AssetBundleManifest by calling AssetManager.Initialize()");
				return null;
			}

			foreach (var name in Manifest.GetAllAssetBundles())
			{
				var hash = Manifest.GetAssetBundleHash(name);
				bool cached = Caching.IsVersionCached(name, hash);
				if (!cached)
				{
					LoadAssetBundle(name, false);
				}
			}

			var operation = new BundlePreLoadOperation(m_InProgressOperations.OfType<BundleLoadOperation>().ToList());
			m_InProgressOperations.Add(operation);

			return operation;
		}

		// Starts the download of the asset bundle identified by the given name, and asset bundles
		// that this asset bundle depends on.
		static protected void LoadAssetBundle(string assetBundleName, bool isLoadingAssetBundleManifest)
		{
			#if UNITY_EDITOR
			// If we're in Editor simulation mode, we don't have to really load the assetBundle and its dependencies.
			if (SimulateAssetBundleInEditor)
				return;
			#endif

			Debug.LogFormat("LoadAssetBundle {0}, {1}", assetBundleName, isLoadingAssetBundleManifest);

			if (!isLoadingAssetBundleManifest && !Manifest)
			{
				//					Log(LogType.Error, "Please initialize AssetBundleManifest by calling AssetBundleManager.Initialize()");
				return;
			}

			// Check if the assetBundle has already been processed.
			LoadAssetBundleInternal(assetBundleName, isLoadingAssetBundleManifest);

			if (Manifest)
			{
				foreach (var dependency in Manifest.GetAllDependencies(assetBundleName))
				{
					AddDepend(dependency, assetBundleName);
					LoadAssetBundleInternal(dependency, false);
				}
			}
		}


		// Sets up download operation for the given asset bundle if it's not downloaded already.
		static protected void LoadAssetBundleInternal(string assetBundleName, bool isLoadingAssetBundleManifest)
		{
			Debug.LogFormat("LoadAssetBundleInternal {0}, {1}", assetBundleName, m_LoadedAssetBundles.ContainsKey(assetBundleName));


			// Already loaded.
			AssetBundle bundle = null;
			m_LoadedAssetBundles.TryGetValue(assetBundleName, out bundle);
			if (bundle != null)
			{
				return;
			}

			//TODO: 既にロードしていないか確認する

			string url = SourceURL + assetBundleName;

			UnityWebRequest request = null;
			if (isLoadingAssetBundleManifest)
			{
				// If hash is not zero, manifest will be cached. Otherwise, always manifest will be downloaded.
				Debug.LogFormat("LoadingAssetBundleManifest: {0}, {1} (cached:{2})", url, m_ManifestHash, Caching.IsVersionCached(assetBundleName, m_ManifestHash));
				request = UnityWebRequest.GetAssetBundle(url, m_ManifestHash, 0);
			}
			else
			{
				request = UnityWebRequest.GetAssetBundle(url, Manifest.GetAssetBundleHash(assetBundleName), 0);
			}
			m_InProgressOperations.Add(new BundleLoadOperation(request));
		}

		static protected void UnloadAssetBundle(string assetBundleName)
		{
			if (Manifest)
			{
				foreach (var dependency in Manifest.GetAllDependencies(assetBundleName))
				{
					SubDepend(dependency, assetBundleName);
				}
			}
		}

		static protected void UnloadAssetBundleInternal(string assetBundleName)
		{
			m_Depended.Remove(assetBundleName);

			AssetBundle bundle;
			m_LoadedAssetBundles.TryGetValue(assetBundleName, out bundle);
			m_LoadedAssetBundles.Remove(assetBundleName);
			if (bundle)
			{
				bundle.Unload(false);
				Debug.Log(assetBundleName + " has been unloaded successfully");
			}
		}




		/// <summary>
		/// Starts a load operation for an asset from Resources.
		/// </summary>
		static public AssetLoadOperation LoadAssetAsync<T>(string assetName, System.Action<T> onLoad = null) where T:UnityEngine.Object
		{
			if (onLoad != null)
				return LoadAssetAsync("", assetName, typeof(T), obj => onLoad(obj as T));
			else
				return LoadAssetAsync("", assetName, typeof(T));
		}

		/// <summary>
		/// Starts a load operation for an asset from Resources.
		/// </summary>
		static public AssetLoadOperation LoadAssetAsync(string assetName, System.Type type, System.Action<UnityEngine.Object> onLoad = null)
		{
			if (onLoad != null)
				return LoadAssetAsync("", assetName, type, onLoad);
			else
				return LoadAssetAsync("", assetName, type);
		}

		/// <summary>
		/// Starts a load operation for an asset from the given asset bundle.
		/// </summary>
		static public AssetLoadOperation LoadAssetAsync<T>(string assetBundleName, string assetName, System.Action<T> onLoad = null) where T:UnityEngine.Object
		{
			if (onLoad != null)
				return LoadAssetAsync(assetBundleName, assetName, typeof(T), obj => onLoad(obj as T));
			else
				return LoadAssetAsync(assetBundleName, assetName, typeof(T));
		}

		/// <summary>
		/// Starts a load operation for an asset from the given asset bundle.
		/// </summary>
		static public AssetLoadOperation LoadAssetAsync(string assetBundleName, string assetName, System.Type type, System.Action<UnityEngine.Object> onLoad = null)
		{
			
//				assetBundleName = RemapVariantName(assetBundleName);
			string operationId = AssetLoadOperation.GetId(assetBundleName, assetName, type);

			// Operation has not been cached. Need load the assetbundle.
			if (!string.IsNullOrEmpty(assetBundleName) && !m_RuntimeCache.ContainsKey(operationId))
				LoadAssetBundle(assetBundleName, type == typeof(AssetBundleManifest));

			// Search same operation in progress to merge load complete callbacks.
			AssetLoadOperation operation = null;
			foreach (var progressOperation in m_InProgressOperations)
			{
				var op = progressOperation as AssetLoadOperation;
				if (op != null && op.id == operationId)
				{
					operation = op;
					break;
				}
			}

			// When no same operation in progress, create new operation.
			if (operation == null)
			{
				operation = new AssetLoadOperation(assetBundleName, assetName, type);
				m_InProgressOperations.Add(operation);
			}

			// Add load complete callback.
			if (onLoad != null)
			{
				operation.onComplete += () => onLoad(m_RuntimeCache.ContainsKey(operationId) ? m_RuntimeCache[operationId] : null);
			}

			return operation;
		}

		/// <summary>
		/// Update manifest.
		/// Compare manifests and delete old cached bundles.
		/// Starts download of manifest asset bundle.
		/// Returns the manifest asset bundle downolad operation object.
		/// </summary>
		/// <param name="manifest">Asset bundle manifest.</param>
		static void UpdateManifest(AssetBundleManifest manifest)
		{
			Debug.Log("update manifest. " + m_ManifestHash);
			var oldManifest = Manifest;
			Manifest = manifest;

			if (!Manifest)
			{
				Debug.LogError("Failed to update manifest.");
				return;
			}

			if (oldManifest)
			{
				Debug.Log("manifest gap check.");
			
				var oldBundles = new HashSet<string>(oldManifest.GetAllAssetBundles());
				var newBundles = new HashSet<string>(manifest.GetAllAssetBundles());

				Debug.Log("old.");
				oldBundles.Select(x => x + ":" + oldManifest.GetAssetBundleHash(x) + "\n").LogDump();
				Debug.Log("new.");
				newBundles.Select(x => x + ":" + manifest.GetAssetBundleHash(x) + "\n").LogDump();

				foreach (var name in oldBundles)
				{
					var oldHash = oldManifest.GetAssetBundleHash(name);

					// The bundle has removed or changed. Need to delete cached bundle.
					if (!newBundles.Contains(name) || oldHash != manifest.GetAssetBundleHash(name))
						ClearCachedAssetBundle(name, oldHash);
				}
			}
			PlayerPrefs.SetString("AssetBundleManifestHash", m_ManifestHash.ToString());
		}

		/// <summary>
		/// Starts download of manifest asset bundle.
		/// Returns the manifest asset bundle downolad operation object.
		/// </summary>
		/// <param name="hash">Hash for manifest.</param>
		/// <param name="onComplete">callback.</param>
		static public AssetLoadOperation UpdateManifest(Hash128 hash = default(Hash128))
		{

			#if UNITY_EDITOR
			// If we're in Editor simulation mode, we don't need the manifest assetBundle.
			if (SimulateAssetBundleInEditor)
				return null;
			#endif

			ClearRuntimeCacheAll();
			ClearOperationsAll();
			UnloadAssetbundlesAll();

			if (hash != default(Hash128))
				m_ManifestHash = hash;

			return LoadAssetAsync<AssetBundleManifest>(Platform, "assetbundlemanifest", UpdateManifest);
		}

		/// <summary>
		/// Delete cached asset bundle.
		/// </summary>
		/// <param name="assetBundleName">AssetBundle name.</param>
		/// <param name="hash">hash.</param>
		public static void ClearCachedAssetBundle(string assetBundleName, Hash128 hash)
		{
			UnloadAssetBundleInternal(assetBundleName);
			if (Caching.IsVersionCached(assetBundleName, hash))
			{
				Debug.LogFormat("Delete assetbundle {0}, hash {1}", assetBundleName, hash);
				var request = UnityWebRequest.GetAssetBundle(assetBundleName, hash, uint.MaxValue);
				request.Send();
				request.Abort();
			}
		}

		/// <summary>
		/// Delete all cached asset bundle.
		/// </summary>
		static public void ClearCachedAssetBundleAll()
		{
			Caching.CleanCache();

			if (!Manifest)
				return;
			
			foreach (var bundleName in Manifest.GetAllAssetBundles())
			{
				ClearCachedAssetBundle(bundleName, Manifest.GetAssetBundleHash(bundleName));
			}
		}

		/// <summary>
		/// Unloads the assetbundle all.
		/// </summary>
		public static void UnloadAssetbundlesAll()
		{
			foreach (var assetBundleName in new List<string>(m_LoadedAssetBundles.Keys))
				UnloadAssetBundleInternal(assetBundleName);
			m_LoadedAssetBundles.Clear();
			m_Depended.Clear();
		}

		/// <summary>
		/// Clears the runtime cache.
		/// </summary>
		public static void ClearRuntimeCacheAll()
		{
			m_RuntimeCache.Clear();
		}

		/// <summary>
		/// Clears the operations.
		/// </summary>
		public static void ClearOperationsAll()
		{
//			m_DownloadingErrors.Clear();
			m_InProgressOperations.ForEach(op => op.OnCancel());
			m_InProgressOperations.Clear();
		}

		/// <summary>
		/// Clears the operations.
		/// </summary>
		public static void ClearAll()
		{
			errorLog.Length = 0;
			ClearRuntimeCacheAll();
			ClearOperationsAll();
			UnloadAssetbundlesAll();
			ClearCachedAssetBundleAll();
			Resources.UnloadUnusedAssets();
		}

		public static void AddDepend(string assetBundleName, string dependedId)
		{
			if (string.IsNullOrEmpty(assetBundleName))
				return;
				
			HashSet<string> depended;
			if (!m_Depended.TryGetValue(assetBundleName, out depended))
			{
				if (string.IsNullOrEmpty(dependedId))
				{
					m_Unloadable.Add(assetBundleName);
					return;
				}
	
				depended = new HashSet<string>();
				m_Depended.Add(assetBundleName, depended);
			}
			if (!string.IsNullOrEmpty(dependedId))
				depended.Add(dependedId);
				
			UpdateDepend(assetBundleName, depended);
		}

	
		public static void SubDepend(string assetBundleName, string dependedId)
		{
			if (string.IsNullOrEmpty(assetBundleName))
				return;
				
			HashSet<string> depended;
			if (m_Depended.TryGetValue(assetBundleName, out depended))
			{
				if (!string.IsNullOrEmpty(dependedId))
					depended.Remove(dependedId);
			}
			UpdateDepend(assetBundleName, depended);
		}

		public static void UpdateDepend(string assetBundleName, HashSet<string> depended)
		{
			if (depended != null && 0 < depended.Count)
			{
				m_Unloadable.Remove(assetBundleName);
			}
			else
			{
				m_Unloadable.Add(assetBundleName);
				m_Depended.Remove(assetBundleName);
			}
		}
	}
}