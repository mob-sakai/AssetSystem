using UnityEngine;
using System.Collections.Generic;
#if UNITY_5_3 || UNITY_5_3_OR_NEWER
using UnityEngine.SceneManagement;
#endif
#if UNITY_5_4_OR_NEWER
using UnityEngine.Networking;
#endif
#if ENABLE_IOS_ON_DEMAND_RESOURCES
using UnityEngine.iOS;
#endif
using System.Collections;
using Object = UnityEngine.Object;

namespace AssetBundles
{
    public abstract class AssetBundleLoadOperation : IEnumerator
    {
        public object Current
        {
            get
            {
                return null;
            }
        }

        public bool MoveNext()
        {
            return !IsDone();
        }

        public void Reset()
        {
        }

        abstract public bool Update();

        abstract public bool IsDone();

		protected virtual void OnComplete(){}

		public virtual void Cancel(){}
    }

    public abstract class AssetBundleDownloadOperation : AssetBundleLoadOperation
    {
		public static System.Action<AssetBundleDownloadOperation> onComplete;

        bool done;

        public string assetBundleName { get; private set; }
        public LoadedAssetBundle assetBundle { get; protected set; }
        public string error { get; protected set; }

        protected abstract bool downloadIsDone { get; }

        public override bool Update()
        {
            if (!done && downloadIsDone)
            {
				OnComplete();
                done = true;
            }

            return !done;
        }

        public override bool IsDone()
        {
            return done;
        }

		protected override void OnComplete ()
		{
			if(onComplete != null)
				onComplete(this);
		}

        public abstract string GetSourceURL();

        public AssetBundleDownloadOperation(string assetBundleName)
        {
            this.assetBundleName = assetBundleName;
        }
    }

#if ENABLE_IOS_ON_DEMAND_RESOURCES
    // Read asset bundle asynchronously from iOS / tvOS asset catalog that is downloaded
    // using on demand resources functionality.
    public class AssetBundleDownloadFromODROperation : AssetBundleDownloadOperation
    {
        OnDemandResourcesRequest request;

        public AssetBundleDownloadFromODROperation(string assetBundleName)
            : base(assetBundleName)
        {
            // Work around Xcode crash when opening Resources tab when a 
            // resource name contains slash character
            request = OnDemandResources.PreloadAsync(new string[] { assetBundleName.Replace('/', '>') });
        }

        protected override bool downloadIsDone { get { return (request == null) || request.isDone; } }

        public override string GetSourceURL()
        {
            return "odr://" + assetBundleName;
        }

		protected override void OnComplete()
        {
            error = request.error;
            if (error != null)
                return;

            var path = "res://" + assetBundleName;
#if UNITY_5_3 || UNITY_5_3_OR_NEWER
            var bundle = AssetBundle.LoadFromFile(path);
#else
            var bundle = AssetBundle.CreateFromFile(path);
#endif
            if (bundle == null)
            {
                error = string.Format("Failed to load {0}", path);
                request.Dispose();
            }
            else
            {
                assetBundle = new LoadedAssetBundle(bundle);
                // At the time of unload request is already set to null, so capture it to local variable.
                var localRequest = request;
                // Dispose of request only when bundle is unloaded to keep the ODR pin alive.
                assetBundle.unload += () =>
                {
                    localRequest.Dispose();
                };
            }

            request = null;

			base.onComplete(this);
        }

		public override void Cancel ()
		{
			if (request != null)
			{
				request.Dispose ();
				request = null;
			}
		}
	}
#endif

#if ENABLE_IOS_APP_SLICING
    // Read asset bundle synchronously from an iOS / tvOS asset catalog
    public class AssetBundleOpenFromAssetCatalogOperation : AssetBundleDownloadOperation
    {
        public AssetBundleOpenFromAssetCatalogOperation(string assetBundleName)
            : base(assetBundleName)
        {
            var path = "res://" + assetBundleName;
#if UNITY_5_3 || UNITY_5_3_OR_NEWER
            var bundle = AssetBundle.LoadFromFile(path);
#else
            var bundle = AssetBundle.CreateFromFile(path);
#endif
            if (bundle == null)
                error = string.Format("Failed to load {0}", path);
            else
                assetBundle = new LoadedAssetBundle(bundle);
        }

        protected override bool downloadIsDone { get { return true; } }

        public override string GetSourceURL()
        {
            return "res://" + assetBundleName;
        }
    }
#endif

    public class AssetBundleDownloadFromWebOperation : AssetBundleDownloadOperation
    {
        WWW m_WWW;
        string m_Url;

        public AssetBundleDownloadFromWebOperation(string assetBundleName, WWW www)
            : base(assetBundleName)
        {
            if (www == null)
                throw new System.ArgumentNullException("www");
            m_Url = www.url;
            this.m_WWW = www;
        }

        protected override bool downloadIsDone { get { return (m_WWW == null) || m_WWW.isDone; } }

		protected override void OnComplete()
        {
            error = m_WWW.error;
            if (!string.IsNullOrEmpty(error))
                return;

            AssetBundle bundle = m_WWW.assetBundle;
            if (bundle == null)
                error = string.Format("{0} is not a valid asset bundle.", assetBundleName);
            else
                assetBundle = new LoadedAssetBundle(m_WWW.assetBundle);

            m_WWW.Dispose();
            m_WWW = null;

			base.OnComplete ();
        }

		public override void Cancel ()
		{
			if (m_WWW != null)
			{
				m_WWW.Dispose ();
				m_WWW = null;
			}
		}

        public override string GetSourceURL()
        {
            return m_Url;
        }
    }

#if UNITY_5_4_OR_NEWER
    public class AssetBundleDownloadWebRequestOperation : AssetBundleDownloadOperation
    {
        UnityWebRequest m_request;
        AsyncOperation m_Operation;
        string m_Url;

        public AssetBundleDownloadWebRequestOperation(string assetBundleName, UnityWebRequest request)
            : base(assetBundleName)
        {
            if (request == null || !(request.downloadHandler is DownloadHandlerAssetBundle))
                throw new System.ArgumentNullException("request");
            m_Url = request.url;
            m_request = request;
            m_Operation = request.Send();
        }

        protected override bool downloadIsDone { get { return (m_Operation == null) || m_Operation.isDone; } }

		protected override void OnComplete()
        {
            error = m_request.error;
            if (!string.IsNullOrEmpty(error))
                return;

            var handler = m_request.downloadHandler as DownloadHandlerAssetBundle;
            AssetBundle bundle = handler.assetBundle;
            if (bundle == null)
                error = string.Format("{0} is not a valid asset bundle.", assetBundleName);
            else
                assetBundle = new LoadedAssetBundle(bundle);

            m_request.Dispose();
            m_request = null;
            m_Operation = null;

			base.OnComplete ();
        }

		public override void Cancel ()
		{
			if (m_request != null)
			{
				if (m_request.isDone)
					m_request.Abort ();
				m_request.Dispose ();
				m_request = null;
			}
			m_Operation = null;
		}

        public override string GetSourceURL()
        {
            return m_Url;
        }
    }

    public class AssetBundleDownloadFileOperation : AssetBundleDownloadOperation
    {
        AssetBundleCreateRequest m_Operation;
        string m_Url;

        public AssetBundleDownloadFileOperation(string assetBundleName, string url, uint crc = 0, ulong offset = 0)
            : base(assetBundleName)
        {
            m_Operation = AssetBundle.LoadFromFileAsync(url, crc, offset);
            m_Url = url;
        }

        protected override bool downloadIsDone { get { return (m_Operation == null) || m_Operation.isDone; } }

		protected override void OnComplete()
        {
            AssetBundle bundle = m_Operation.assetBundle;
            if (bundle == null) {
                error = string.Format("failed to load assetBundle {0}.", assetBundleName);
                return;
            }

            if (bundle == null)
                error = string.Format("{0} is not a valid asset bundle.", assetBundleName);
            else
                assetBundle = new LoadedAssetBundle(bundle);
            m_Operation = null;
        }

        public override string GetSourceURL()
        {
            return m_Url;
        }
    }
#endif

#if UNITY_EDITOR
    public class AssetBundleLoadLevelSimulationOperation : AssetBundleLoadOperation
    {
        AsyncOperation m_Operation = null;

        public AssetBundleLoadLevelSimulationOperation(string assetBundleName, string levelName, bool isAdditive)
        {
            string[] levelPaths = UnityEditor.AssetDatabase.GetAssetPathsFromAssetBundleAndAssetName(assetBundleName, levelName);
            if (levelPaths.Length == 0)
            {
                // TODO: The error needs to differentiate that an asset bundle name doesn't exist
                //        from that there right scene does not exist in the asset bundle...

                Debug.LogError("There is no scene with name \"" + levelName + "\" in " + assetBundleName);
                return;
            }

            if (isAdditive)
                m_Operation = UnityEditor.EditorApplication.LoadLevelAdditiveAsyncInPlayMode(levelPaths[0]);
            else
                m_Operation = UnityEditor.EditorApplication.LoadLevelAsyncInPlayMode(levelPaths[0]);
        }

        public override bool Update()
        {
            return false;
        }

        public override bool IsDone()
        {
            return m_Operation == null || m_Operation.isDone;
        }
    }
#endif

    public class AssetBundleLoadLevelOperation : AssetBundleLoadOperation
    {
        protected string                m_AssetBundleName;
        protected string                m_LevelName;
        protected bool                  m_IsAdditive;
        protected string                m_DownloadingError;
        protected AsyncOperation        m_Request;

        public AssetBundleLoadLevelOperation(string assetbundleName, string levelName, bool isAdditive)
        {
            m_AssetBundleName = assetbundleName;
            m_LevelName = levelName;
            m_IsAdditive = isAdditive;
        }

        public override bool Update()
        {
            if (m_Request != null)
                return false;

            LoadedAssetBundle bundle = AssetBundleManager.GetLoadedAssetBundle(m_AssetBundleName, out m_DownloadingError);
            if (bundle != null)
            {
#if UNITY_5_3 || UNITY_5_3_OR_NEWER
                m_Request = SceneManager.LoadSceneAsync(m_LevelName, m_IsAdditive ? LoadSceneMode.Additive : LoadSceneMode.Single);
#else
                if (m_IsAdditive)
                    m_Request = Application.LoadLevelAdditiveAsync(m_LevelName);
                else
                    m_Request = Application.LoadLevelAsync(m_LevelName);
#endif
                return false;
            }
            else
                return true;
        }

        public override bool IsDone()
        {
            // Return if meeting downloading error.
            // m_DownloadingError might come from the dependency downloading.
            if (m_Request == null && m_DownloadingError != null)
            {
                Debug.LogError(m_DownloadingError);
                return true;
            }

            return m_Request != null && m_Request.isDone;
        }
    }

    public abstract class AssetBundleLoadAssetOperation : AssetBundleLoadOperation
    {
        public abstract T GetAsset<T>() where T: Object;
    }

    public class AssetBundleLoadAssetOperationSimulation : AssetBundleLoadAssetOperation
    {
        Object                          m_SimulatedObject;

        public AssetBundleLoadAssetOperationSimulation(Object simulatedObject)
        {
            m_SimulatedObject = simulatedObject;
        }

        public override T GetAsset<T>()
        {
            return m_SimulatedObject as T;
        }

        public override bool Update()
        {
            return false;
        }

        public override bool IsDone()
        {
            return true;
        }
    }

    public class AssetBundleLoadAssetOperationFull : AssetBundleLoadAssetOperation
    {
        protected string                m_AssetBundleName;
        protected string                m_AssetName;
        protected string                m_DownloadingError;
        protected System.Type           m_Type;
        protected AssetBundleRequest    m_Request = null;
		protected Dictionary<string, Object> m_RuntimeCache;

		/// <summary>Load asset complete callback.</summary>
		public System.Action<Object> onComplete;

		/// <summary>Operation identifier.</summary>
		public string id { get; protected set; }

		public static string GetId(string bundleName, string assetName, System.Type type)
		{
			return string.Format ("{0}.{1}.{2}", bundleName, assetName, type.Name);
		}

		public AssetBundleLoadAssetOperationFull(string bundleName, string assetName, System.Type type, Dictionary<string, Object> runtimeCache)
        {
			m_RuntimeCache = runtimeCache;
            m_AssetBundleName = bundleName;
            m_AssetName = assetName;
            m_Type = type;
			id = GetId( bundleName, assetName, type);
        }

        public override T GetAsset<T>()
		{
			// Operation has been cached.
			Object obj = null;
			if (m_RuntimeCache != null && m_RuntimeCache.TryGetValue (id, out obj)) {
				return obj as T;
			}

			// Loading request has done.
			if (m_Request != null && m_Request.isDone) {
				return m_Request.asset as T;
			}
			
			return null;
        }

        // Returns true if more Update calls are required.
        public override bool Update()
		{
			// Operation has been cached.
			if (m_RuntimeCache != null && m_RuntimeCache.ContainsKey (id))
				return false;
			
			if (m_Request == null) {
				LoadedAssetBundle bundle = AssetBundleManager.GetLoadedAssetBundle (m_AssetBundleName, out m_DownloadingError);
				if (bundle != null) {
					// TODO: When asset bundle download fails this throws an exception...
					m_Request = bundle.m_AssetBundle.LoadAssetAsync (m_AssetName, m_Type);

					// Not found specified asset in bundle.
					if (m_Request == null) {
						m_DownloadingError = string.Format("There is no asset with name {0}({1}) in {2}", m_AssetName, m_Type.Name, m_AssetBundleName );
						return false;
					}
				}
			} 

			// Loading error has occered.
			if (m_DownloadingError != null) {
				Debug.LogError(m_DownloadingError);
			}

			// Load complete.
			if(m_Request.isDone) {
				OnComplete ();
			}

			return !IsDone ();
        }

        public override bool IsDone()
        {
			// Operation has been cached.
			if (m_RuntimeCache != null && m_RuntimeCache.ContainsKey (id))
				return true;
			
            // Return if meeting downloading error.
            // m_DownloadingError might come from the dependency downloading.
            if (m_DownloadingError != null)
            {
                return true;
            }

            return m_Request != null && m_Request.isDone;
        }

		protected override void OnComplete ()
		{
			Object asset = GetAsset<Object> ();

			// Cache the asset.
			if (m_RuntimeCache != null && asset)
				m_RuntimeCache [id] = asset;

			if (onComplete == null)
				return;

			foreach (System.Action<Object> action in onComplete.GetInvocationList()) {
				try{
					action (asset);
				}catch(System.Exception ex){
					Debug.LogException(ex);
				}
			}
			onComplete = null;
		}

		public override void Cancel ()
		{
		}
    }

    public class AssetBundleLoadManifestOperation : AssetBundleLoadAssetOperationFull
    {
        public AssetBundleLoadManifestOperation(string bundleName)
			: base(bundleName, "AssetBundleManifest", typeof(AssetBundleManifest), null)
        {
        }
    }
}
