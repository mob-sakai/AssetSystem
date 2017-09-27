using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using UnityEngine.Networking;
using Object = UnityEngine.Object;
using System.Text;
using System.Text.RegularExpressions;

namespace Mobcast.Coffee.AssetSystem
{

	public class AssetManager : MonoSingleton<AssetManager>
	{
		public const string kLog = "[AssetManager] ";

#if UNITY_STANDALONE_OSX
		public const string Platform = "OSX";
#elif UNITY_STANDALONE_WIN
		public const string Platform = "Windows";
#elif UNITY_ANDROID
		public const string Platform = "Android";
#elif UNITY_IOS || UNITY_IPHONE
		public const string Platform = "iOS";
#elif UNITY_WEBGL
		public const string Platform = "WebGL";
#else
		public const string Platform = "Unknown";
#endif

		/// <summary>
		/// AssetBundleManifest object which can be used to load the dependecies
		/// and check suitable assetBundle variants.
		/// </summary>
		public static AssetBundleManifest manifest { set; get; }

		/// <summary>
		/// The base downloading url which is used to generate the full
		/// downloading url with the assetBundle names.
		/// </summary>
		public static string patchServerURL { get; set; }

		public static Patch patch { get; private set; }

		public static Dictionary<string, AssetBundle> m_LoadedAssetBundles = new Dictionary<string, AssetBundle>();
		public static List<AssetOperation> m_InProgressOperations = new List<AssetOperation>();
		public static Dictionary<string, UnityEngine.Object> m_RuntimeCache = new Dictionary<string, UnityEngine.Object>();
		public static Dictionary<string, HashSet<string>> m_Depended = new Dictionary<string, HashSet<string>>();
		static HashSet<string> m_Unloadable = new HashSet<string>();

		public static StringBuilder errorLog = new StringBuilder();

		public static PatchList patchList = new PatchList();



		#if UNITY_EDITOR
		public const string MenuText_Root = "Coffee/AsssetSystem";
		public const string MenuText_SimulationMode = MenuText_Root + "/AssetBundle Mode/Simulation (Editor)";
		public const string MenuText_LocalServerMode = MenuText_Root + "/AssetBundle Mode/In Local Server (Editor)";
		public const string MenuText_StreamingAssets = MenuText_Root + "/AssetBundle Mode/In StreamingAssets";
		public const string MenuText_BuildAssetBundle = MenuText_Root + "/Build AssetBundle (Uncompressed)";

		public static bool isSimulationMode { get { return UnityEditor.Menu.GetChecked(AssetManager.MenuText_SimulationMode); } }

		public static bool isLocalServerMode { get { return UnityEditor.Menu.GetChecked(AssetManager.MenuText_LocalServerMode); } }

		//		public static bool isStreamingAssetsMode { get { return UnityEditor.Menu.GetChecked(AssetManager.MenuText_StreamingAssets); } }
		#endif

		public override string ToString()
		{
			var assetbundleNames = AssetManager.manifest ? AssetManager.manifest.GetAllAssetBundles() : new string[0];
			var downloadedCount = AssetManager.manifest ? assetbundleNames.Count(x => Caching.IsVersionCached(x, AssetManager.manifest.GetAssetBundleHash(x))) : 0;

			return string.Format("{0}\nパッチサーバーURL : {6}\n現在のパッチ : {7}\nディスク使用量 : {1}\nランタイムキャッシュ : {2}\nロード済みアセットバンドル : {3}\nダウンロード済みアセットバンドル{4}/{5}",
				kLog,
				Caching.spaceOccupied,
				AssetManager.m_RuntimeCache.Count,
				AssetManager.m_LoadedAssetBundles.Count,
				downloadedCount,
				assetbundleNames.Length,
				patchServerURL,
				patch
			);
		}

		IEnumerator Start()
		{
			yield return new WaitUntil(() => Caching.ready);

			#if UNITY_EDITOR
			if (isLocalServerMode)
			{
				EnableLocalServerMode();
				yield break;
			}
			else if (isSimulationMode)
			{
				EnableSimulationMode();
				yield break;
			}

			#endif

			patch = JsonUtility.FromJson<Patch>(PlayerPrefs.GetString("AssetManager_Patch", "{}"));

			// 最後に利用したパッチのマニフェストファイルはダウンロード済み？
			// Is the last used patch cached?
			if (Caching.IsVersionCached(Platform, Hash128.Parse(patch.commitHash)))
			{
				yield return StartCoroutine(SetPatch(patch));
			}
			yield break;
		}

#if UNITY_EDITOR
		/// <summary>
		/// ローカルサーバーモードに設定します.
		/// Sets the local server mode.
		/// </summary>
		public static void EnableLocalServerMode()
		{
			// ローカルパッチサーバを起動
			// Start local patch server.
			if (!isLocalServerMode)
				UnityEditor.EditorApplication.ExecuteMenuItem(MenuText_LocalServerMode);

			patchServerURL = "http://localhost:7888/";
			patch = new Patch(){ comment = "LocalServerMode", commitHash = "" };
			Debug.LogWarningFormat("{0}ローカルサーバーモードに設定しました", kLog);
		}

		/// <summary>
		/// シミュレーションモードに設定します.
		/// Sets the simulation mode.
		/// </summary>
		public static void EnableSimulationMode()
		{
			SetPatchServerURL("SimulationMode");
			patch = new Patch(){ comment = "SimulationMode", commitHash = "" };
			Debug.LogWarningFormat("{0}シミュレーションモードに設定しました", kLog);

			UnityEditor.Menu.SetChecked(AssetManager.MenuText_SimulationMode, true);
		}
#endif

		void Update()
		{
			// 実行中のオペレーションを全て更新処理する.
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
					// オペレーション完了. 終了コールバックを実行.
					// The operation is completed.
					m_InProgressOperations.RemoveAt(i);
					operation.OnComplete();
					if (!string.IsNullOrEmpty(operation.error))
						errorLog.AppendLine(operation.error);
				}
			}

			//アンロード可能なアセットバンドルをアンロード
			// Unload unused asset bundles.
			if (0 < m_Unloadable.Count)
			{
				var array = m_Unloadable.ToArray();
				m_Unloadable.Clear();

				foreach (var assetBundleName in array)
					UnloadAssetBundleInternal(assetBundleName);
			}
		}

		public static bool TryGetBundle(string name, out AssetBundle bundle)
		{
			bundle = null;

			// 依存しているアセットバンドルを全てロード済みか.
			// TODO: cache dependancy!
			// Loaded all dependancy.
			if (!manifest || manifest.GetAllDependencies(name).All(m_LoadedAssetBundles.ContainsKey))
			{

				// 自分自身をロード済みか.
				// Loaded itself.
				return m_LoadedAssetBundles.TryGetValue(name, out bundle);
			}
			return false;
		}

		/// <summary>
		/// Sets base downloading URL to a web URL. The directory pointed to by this URL
		/// on the web-server should have the same structure as the AssetBundles directory
		/// in the demo project root.
		/// </summary>
		public static void SetPatchServerURL(string url)
		{
			#if UNITY_EDITOR
			//ローカルサーバ起動中の場合、ローカルサーバを停止
			if (UnityEditor.Menu.GetChecked(AssetManager.MenuText_LocalServerMode))
			{
				UnityEditor.EditorApplication.ExecuteMenuItem(AssetManager.MenuText_LocalServerMode);
			}

			UnityEditor.Menu.SetChecked(AssetManager.MenuText_SimulationMode, false);
			#endif

			if (!url.EndsWith("/"))
				url += "/";

			patchServerURL = url;
		}

		/// <summary>
		/// StreamingAssetsをパッチサーバに設定します.
		/// Set patch server URL to StreamingAssets.
		/// </summary>
		public static void SetPatchServerURLToStreamingAssets()
		{
		#if UNITY_EDITOR
		SetPatchServerURL("file://" + System.IO.Path.Combine(Application.streamingAssetsPath, "AssetBundles"));
		#else
		SetPatchServerURL(System.IO.Path.Combine(Application.streamingAssetsPath, "AssetBundles"));
		#endif
			patch = new Patch(){ comment = "StreamingAssets", commitHash = "" };
			Debug.LogWarningFormat("{0}StreamingAssetsモードに設定しました", kLog);
		}

		/// <summary>
		/// Preloads the asset bundle.
		/// </summary>
		/// <returns>The asset bundle.</returns>
		public static BundlePreLoadOperation PreDownload(Func<string,bool> predicate, Action onComplete)
		{
			#if UNITY_EDITOR
			if (isSimulationMode)
			{
				Debug.LogWarning("PreDownload AssetBundle in Sumilation Mode");

				var dummyOperation = new BundlePreLoadOperation(m_InProgressOperations.OfType<BundleLoadOperation>().ToList());
				m_InProgressOperations.Add(dummyOperation);
				return dummyOperation;
			}
			#endif

			if (!manifest)
			{
				Debug.LogWarning("no manifest");
				return null;
			}

			IEnumerable<string> bundleNams = predicate != null
			? manifest.GetAllAssetBundles().Where(predicate)
			: manifest.GetAllAssetBundles();

			foreach (var name in bundleNams)
			{
				var hash = manifest.GetAssetBundleHash(name);
				bool cached = Caching.IsVersionCached(name, hash);
				if (!cached)
				{
					LoadAssetBundle(name, false);
				}
			}

			var operation = new BundlePreLoadOperation(m_InProgressOperations.OfType<BundleLoadOperation>().ToList());
			if (onComplete != null)
				operation.onComplete += onComplete;
			m_InProgressOperations.Add(operation);

			return operation;
		}

		/// <summary>
		/// Preloads the asset bundle.
		/// </summary>
		/// <returns>The asset bundle.</returns>
		public static BundlePreLoadOperation PreDownload()
		{
			return PreDownload(null, null);
		}


		// Starts the download of the asset bundle identified by the given name, and asset bundles
		// that this asset bundle depends on.
		static public BundleLoadOperation LoadAssetBundle(string assetBundleName)
		{
			return LoadAssetBundle(assetBundleName, false);
		}

		// Starts the download of the asset bundle identified by the given name, and asset bundles
		// that this asset bundle depends on.
		static protected BundleLoadOperation LoadAssetBundle(string assetBundleName, bool isLoadingAssetBundleManifest)
		{
			string operationId = BundleLoadOperation.GetId(assetBundleName);

#if UNITY_EDITOR
			if (isSimulationMode)
			{
				return new BundleLoadOperation(assetBundleName);
			}
#endif


			// Already loaded.
			AssetBundle bundle = null;
			m_LoadedAssetBundles.TryGetValue(assetBundleName, out bundle);
			if (bundle != null)
			{
				Debug.LogFormat("LoadAssetBundle {0}, is loaded already.", assetBundleName);
				return new BundleLoadOperation(operationId);
			}
			m_LoadedAssetBundles.Remove(assetBundleName);

			Debug.LogFormat("LoadAssetBundle {0}, manifest ? {1}", assetBundleName, isLoadingAssetBundleManifest);

			// Search same operation in progress to merge load complete callbacks.
			// Check if the assetBundle has already been processed.
			foreach (var progressOperation in m_InProgressOperations)
			{
				var op = progressOperation as BundleLoadOperation;
				if (op != null && op.id == operationId)
				{
					return op;
				}
			}

			if (!isLoadingAssetBundleManifest)
			{
				if (!manifest)
				{
					Debug.LogError("Please initialize AssetBundleManifest by calling AssetBundleManager.Initialize()");
					return null;
				}

				foreach (var dependency in manifest.GetAllDependencies(assetBundleName))
				{
					AddDepend(dependency, assetBundleName);
					LoadAssetBundle(dependency, false);
				}
			}
			return LoadAssetBundleInternal(assetBundleName, isLoadingAssetBundleManifest);
		}


		// Sets up download operation for the given asset bundle if it's not downloaded already.
		static protected BundleLoadOperation LoadAssetBundleInternal(string assetBundleName, bool isLoadingAssetBundleManifest)
		{

			string url = string.IsNullOrEmpty(patch.commitHash)
			? patchServerURL + Platform + "/" + assetBundleName
			: patchServerURL + patch.commitHash + "/" + Platform + "/" + assetBundleName;
		
			#if UNITY_EDITOR
			if (isLocalServerMode)
			{
				url = patchServerURL + Platform + "/" + assetBundleName;
			}
			#endif

		
			UnityWebRequest request = null;
			if (isLoadingAssetBundleManifest)
			{
				var hash = Hash128.Parse(patch.commitHash);
				// If hash is not zero, manifest will be cached. Otherwise, always manifest will be downloaded.
				Debug.LogFormat("LoadingAssetBundleManifest: {0}, {1} (cached:{2})", url, hash, Caching.IsVersionCached(assetBundleName, hash));
				request = UnityWebRequest.GetAssetBundle(url, hash, 0);
			}
			else
			{
				request = UnityWebRequest.GetAssetBundle(url, manifest.GetAssetBundleHash(assetBundleName), 0);
			}
			var op = new BundleLoadOperation(assetBundleName, request);
			m_InProgressOperations.Add(op);
			return op;
		}

		/// <summary>
		/// アンロードする
		/// </summary>
		/// <param name="assetBundleName">Asset bundle name.</param>
		static protected void UnloadAssetBundleInternal(string assetBundleName, bool checkDependancy = true)
		{
			if (checkDependancy && manifest)
			{
				foreach (var dependency in manifest.GetAllDependencies(assetBundleName))
				{
					SubDepend(dependency, assetBundleName);
				}
			}
			m_Depended.Remove(assetBundleName);

			AssetBundle bundle;
			m_LoadedAssetBundles.TryGetValue(assetBundleName, out bundle);
			m_LoadedAssetBundles.Remove(assetBundleName);
			if (bundle)
			{
				bundle.Unload(!checkDependancy);
//				bundle.Unload(true);
				Debug.Log(assetBundleName + " has been unloaded successfully");
			}
		}

		static Regex s_RegAb = new Regex("^ab://(.*)/(.*)$");


		/// <summary>
		/// Starts a load operation for an asset from Resources.
		/// </summary>
		static public AssetLoadOperation LoadAssetAsync<T>(string assetName, System.Action<T> onLoad = null) where T:UnityEngine.Object
		{
			if (assetName.StartsWith("ab://"))
			{
				var match = s_RegAb.Match(assetName);
				return LoadAssetAsync(match.Groups[1].Value, match.Groups[2].Value, typeof(T), obj => onLoad(obj as T));
			}

			return onLoad != null
				? LoadAssetAsync("", assetName, typeof(T), obj => onLoad(obj as T))
				: LoadAssetAsync("", assetName, typeof(T));
		}

		/// <summary>
		/// Starts a load operation for an asset from Resources.
		/// </summary>
		static public AssetLoadOperation LoadAssetAsync(string assetName, System.Type type, System.Action<UnityEngine.Object> onLoad = null)
		{
			if (assetName.StartsWith("ab://"))
			{
				var match = s_RegAb.Match(assetName);
				return LoadAssetAsync(match.Groups[1].Value, match.Groups[2].Value, type, onLoad);
			}

			return onLoad != null
				? LoadAssetAsync("", assetName, type, onLoad)
				: LoadAssetAsync("", assetName, type);
		}

		/// <summary>
		/// Starts a load operation for an asset from the given asset bundle.
		/// </summary>
		static public AssetLoadOperation LoadAssetAsync<T>(string assetBundleName, string assetName, System.Action<T> onLoad = null) where T:UnityEngine.Object
		{
			return onLoad != null
				? LoadAssetAsync(assetBundleName, assetName, typeof(T), obj => onLoad(obj as T))
				: LoadAssetAsync(assetBundleName, assetName, typeof(T));
		}

		/// <summary>
		/// Starts a load operation for an asset from the given asset bundle.
		/// </summary>
		static public AssetLoadOperation LoadAssetAsync(string assetBundleName, string assetName, System.Type type, System.Action<UnityEngine.Object> onLoad = null)
		{
			if (type != typeof(AssetBundleManifest))
			{
				assetBundleName = assetBundleName.ToLower();
			}
		
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
				operation.onComplete += () =>
				{
					Debug.LogFormat("oncomplete  {0} {1}", operationId, m_RuntimeCache.ContainsKey(operationId));
					onLoad(m_RuntimeCache.ContainsKey(operationId) ? m_RuntimeCache[operationId] : null);
				};
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
		static void SetPatch(AssetBundleManifest manifest)
		{
//			Debug.Assert(!isSumilationMode);

			Debug.Log("update manifest. " + patch.commitHash);
			var oldManifest = manifest;
			AssetManager.manifest = manifest;

			if (!manifest)
			{
				Debug.LogError("Failed to update manifest.");
				return;
			}

			if (oldManifest)
			{
				Debug.Log("manifest gap check.");
			
				var oldBundles = new HashSet<string>(oldManifest.GetAllAssetBundles());
				var newBundles = new HashSet<string>(manifest.GetAllAssetBundles());

				foreach (var name in oldBundles)
				{
					var oldHash = oldManifest.GetAssetBundleHash(name);

					// The bundle has removed or changed. Need to delete cached bundle.
					if (!newBundles.Contains(name) || oldHash != manifest.GetAssetBundleHash(name))
						ClearCachedAssetBundle(name, oldHash);
				}
			}
			PlayerPrefs.SetString("AssetManager_Patch", JsonUtility.ToJson(patch));
		}

		/// <summary>
		/// Starts download of manifest asset bundle.
		/// Returns the manifest asset bundle downolad operation object.
		/// </summary>
		/// <param name="hash">Hash for manifest.</param>
		/// <param name="onComplete">callback.</param>
		static public AssetLoadOperation SetPatch(Patch patchHash)
		{
			patch = patchHash;
			ClearRuntimeCacheAll();
			ClearOperationsAll();
			UnloadAssetbundlesAll();

			#if UNITY_EDITOR
			if (isSimulationMode)
				return null;
			#endif

			return LoadAssetAsync<AssetBundleManifest>(Platform, "assetbundlemanifest", SetPatch);
		}

		static public AssetLoadOperation UpdatePatchList(string url, Action<PatchList> onComplete = null)
		{
		Debug.Log("パッチリストの更新　実行");

			return LoadAssetAsync<PlainObject>(url, txt =>
				{
				Debug.Log("パッチリストの更新　ロード完了 " + txt + "," + (txt ? txt.text : "{}"));
					patchList = new PatchList();

					try
					{
						JsonUtility.FromJsonOverwrite(txt ? txt.text : "{}", patchList);
					}
					catch (Exception e)
					{
						Debug.LogException(e);
					}

					patch = patchList.leatestPatch;
					onComplete(patchList);
				});
		}


		/// <summary>
		/// Delete cached asset bundle.
		/// </summary>
		/// <param name="assetBundleName">AssetBundle name.</param>
		/// <param name="hash">hash.</param>
		static void ClearCachedAssetBundle(string assetBundleName, Hash128 hash)
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
#if UNITY_2017_1_OR_NEWER
			Caching.ClearCache();
#else
			Caching.CleanCache();
#endif

			if (!manifest)
				return;
			
			foreach (var bundleName in manifest.GetAllAssetBundles())
			{
				ClearCachedAssetBundle(bundleName, manifest.GetAssetBundleHash(bundleName));
			}
		}

		/// <summary>
		/// Unloads the assetbundle all.
		/// </summary>
		static void UnloadAssetbundlesAll()
		{
			foreach (var assetBundleName in new List<string>(m_LoadedAssetBundles.Keys))
				UnloadAssetBundleInternal(assetBundleName, false);
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
		static void ClearOperationsAll()
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

		static void UpdateDepend(string assetBundleName, HashSet<string> depended)
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