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
	/// <summary>
	/// Asset operation.
	/// </summary>
	public abstract class AssetOperation : CustomYieldInstruction
	{
//		public object Current { get { return null; } }

		/// <summary>Operation identifier.</summary>
		public string id { get; protected set; }

		/// <summary>Error message.</summary>
		public string error { get; protected set; }

		/// <summary>What's the operation's progress. (Read Only).</summary>
		public float progress { get; protected set; }

		public event System.Action onComplete = () => { };

		public virtual bool Update()
		{
			return keepWaiting;
		}

//		abstract public bool IsDone();

		public virtual void OnComplete()
		{
			progress = 1;
			try
			{
				onComplete();
			}
			catch (System.Exception ex)
			{
				Debug.LogException(ex);
			}
			finally
			{
				onComplete = null;
			}
		}

		public virtual void OnCancel()
		{
			error = "operation has been canceled";
			onComplete = null;
		}
	}

	/// <summary>
	/// バンドルロードオペレーション
	/// </summary>
	public class BundleLoadOperation : AssetOperation
	{
		public AssetBundle assetBundle { get; protected set; }

//		public override float progress { get { return m_request != null ? m_request.downloadProgress : 1; } }

		UnityWebRequest m_request;


		public static string GetId(string bundleName)
		{
			return string.Format("ab://{0}", bundleName);
		}

		public BundleLoadOperation(string bundleName)
		{
			id = GetId(bundleName);
		}

		public BundleLoadOperation(string bundleName, UnityWebRequest request)
		{
			id = GetId(bundleName);
			m_request = request;
			m_request.Send();
//			id = System.IO.Path.GetFileName(m_request.url);
		}

		public override bool keepWaiting
		{
			get { return m_request != null && !m_request.isDone; }
		}

		public override bool Update()
		{
			progress = m_request != null ? m_request.downloadProgress : 1;


			return base.Update();
		}

		public override void OnComplete()
		{
			Debug.Log("BundleLoadOperation OnComplete" + id);


			if (m_request == null)
			{
			}
			else if (!string.IsNullOrEmpty(m_request.error))
			{
				error = m_request.error;
				Debug.LogError(error);
			}
			else
			{
				var handler = m_request.downloadHandler as DownloadHandlerAssetBundle;
				if (handler == null || handler.assetBundle == null)
				{
					error = string.Format("{0} is not a valid asset bundle.", m_request.url);
					Debug.LogError(error);
				}
				else
				{
					assetBundle = handler.assetBundle;
					AssetManager.AddDepend(System.IO.Path.GetFileName(m_request.url), null);
				}
			}

			if (m_request != null)
			{
				AssetManager.m_LoadedAssetBundles.Add (System.IO.Path.GetFileName (m_request.url), assetBundle);
				m_request.Dispose ();
				m_request = null;
			}

			base.OnComplete();
		}

		public override void OnCancel()
		{
			error = "operation has been canceled";

			if (m_request != null)
			{
				if (!m_request.isDone)
					m_request.Abort();
				m_request.Dispose();
				m_request = null;
			}
			base.OnCancel();
		}
	}


	/// <summary>
	/// アセットロードオペレーション
	/// </summary>
	public class AssetLoadOperation : AssetOperation
	{
		protected string m_AssetBundleName;
		protected string m_AssetName;
		protected System.Type m_Type;
		protected AsyncOperation m_Request = null;
		protected UnityWebRequest m_WebRequest = null;
		protected Object m_Object;

		public static string GetId(string bundleName, string assetName, System.Type type)
		{
			if(!string.IsNullOrEmpty(bundleName))
				return string.Format("ab://{0}/{1}({2})", bundleName, assetName, type.Name);
			else if(assetName.Contains("://"))
				return string.Format("{0}({1})", assetName, type.Name);
			else
				return string.Format("resources://{0}({1})", assetName, type.Name);
		}

		public AssetLoadOperation(string bundleName, string assetName, System.Type type)
		{
			m_AssetBundleName = bundleName;
			m_AssetName = assetName;
			m_Type = type;
			id = GetId(m_AssetBundleName, m_AssetName, m_Type);
			if (AssetManager.m_RuntimeCache.TryGetValue(id, out m_Object))
				return;

			AssetManager.AddDepend(m_AssetBundleName, id);

#if UNITY_EDITOR
			// Simulation mode (only in editor).
			if (AssetManager.isSimulationMode && !assetName.Contains("://"))
			{
				var pathes = !string.IsNullOrEmpty(bundleName)
					? UnityEditor.AssetDatabase.GetAssetPathsFromAssetBundleAndAssetName(bundleName, assetName)
					: UnityEditor.AssetDatabase.FindAssets(string.Format("t:{0} {1}", type.Name, System.IO.Path.GetFileName(assetName)))
						.Select(guid=>UnityEditor.AssetDatabase.GUIDToAssetPath(guid))
						.ToArray();
				
				var asset = pathes.Select(x => UnityEditor.AssetDatabase.LoadAssetAtPath(x, type)).FirstOrDefault();
				if (asset)
				{
					m_Object = asset;
				}
				else
				{
					error = "error : loading error : " + id;
				}
				progress = 1f;
				return;
			}
#endif

			// Asset in Web or StreamingAssets.
			if (assetName.Contains("://"))
			{
				if (m_Type == typeof(Texture2D))
				{
					m_WebRequest = UnityWebRequest.Get(assetName);
					m_WebRequest.SetCacheable(new CacheableDownloadHandlerTexture(m_WebRequest, new byte[256 * 1024]));
//#if UNITY_2017_1_OR_NEWER
//					m_WebRequest = UnityWebRequestTexture.GetTexture(assetName, true);
//#else
//					m_WebRequest = UnityWebRequest.GetTexture(assetName, true);
//#endif
				}
				else if (m_Type == typeof(AudioClip))
				{
#if UNITY_2017_1_OR_NEWER
					m_WebRequest = UnityWebRequestMultimedia.GetAudioClip(assetName, AudioType.MPEG);
#else
					m_WebRequest = UnityWebRequest.GetAudioClip(assetName, AudioType.MPEG);
#endif
				}
				else
				{
					m_WebRequest = UnityWebRequest.Get(assetName);
				}

				m_Request = m_WebRequest.Send();
			}
			// Asset in Resources.
			else if (string.IsNullOrEmpty(m_AssetBundleName))
			{
				m_Request = Resources.LoadAsync(assetName, type);
			}
		}

		public T GetAsset<T>() where T : Object
		{
			return m_Object as T;
//			// Operation has been cached.
//			Object obj = null;
//			return AssetManager.m_RuntimeCache.TryGetValue(id, out obj) ? obj as T : null;
		}

		// Returns true if more Update calls are required.
		public override bool Update()
		{
			// Return if meeting downloading error.
			if (!string.IsNullOrEmpty(error))
				return false;

			// .
			if (m_Object)
				return false;
			

			progress = m_Request != null ? m_Request.progress : 0;

			if (m_Request == null && !string.IsNullOrEmpty(m_AssetBundleName))
			{
				AssetBundle bundle;

//				if (AssetManager.Manifest && !AssetManager.Manifest.GetAllDependencies( m_AssetBundleName ).All(AssetManager.m_LoadedAssetBundles.ContainsKey))
//				{
//					return true;
//				}
//				if (AssetManager.m_LoadedAssetBundles.TryGetValue(m_AssetBundleName, out bundle))
				if (AssetManager.TryGetBundle(m_AssetBundleName, out bundle))
				{
					if (!bundle)
					{
						error = "error : bundle loading error XXX";
						return false;
					}
					else
					{
						m_Request = bundle.LoadAssetWithSubAssetsAsync(m_AssetName, m_Type);
					}
				}
			}

			return keepWaiting;
		}

		/// <summary>Indicates if coroutine should be kept suspended.</summary>
		public override bool keepWaiting
		{
			get
			{
				// .
				if (m_Object)
					return false;

				// Return if meeting downloading error.
				if (!string.IsNullOrEmpty(error))
					return false;

				return m_Request == null || !m_Request.isDone;
			}
		}

		public override void OnComplete()
		{
			Debug.Log(id + " OnComplete. " + m_Request);

			AssetManager.SubDepend(m_AssetBundleName, id);


			// Asset in Web or StreamingAssets.
			if (m_WebRequest != null)
			{
				Debug.Log(" m_WebRequest. " + m_WebRequest.isDone + ", " + m_WebRequest.error);
				error = m_WebRequest.error;

				if (m_WebRequest.isDone && string.IsNullOrEmpty(error))
				{
					Object asset = null;
					if (m_Type == typeof(Texture2D))
						asset = (m_WebRequest.downloadHandler as CacheableDownloadHandlerTexture).texture;
					else if (m_Type == typeof(AudioClip))
						asset = (m_WebRequest.downloadHandler as DownloadHandlerAudioClip).audioClip;
					else
					{
						Debug.Log("data; " + m_WebRequest.downloadHandler.text);
						asset = PlainObject.Create(m_WebRequest.downloadHandler.data);
					}
					m_Object = asset;
				}

				if (!m_WebRequest.isDone)
					m_WebRequest.Abort();
				m_WebRequest.Dispose();
				m_WebRequest = null;
			}
			// Asset in Resources.
			else if (m_Request is ResourceRequest)
			{
				Debug.Log(" ResourceRequest. " + (m_Request as ResourceRequest).asset);
				if ((m_Request as ResourceRequest).asset)
					m_Object = (m_Request as ResourceRequest).asset;
				else
					error = " not found " + id;
			}
			// Asset in AssetBundle.
			else if (m_Request is AssetBundleRequest)
			{
				Debug.Log(" AssetBundleRequest. " + (m_Request as AssetBundleRequest).asset);
				if ((m_Request as AssetBundleRequest).asset)
					m_Object = (m_Request as AssetBundleRequest).asset;
				else
					error = " not found " + id;
			}

			if (!string.IsNullOrEmpty(error))
				Debug.LogError(error);

			base.OnComplete();
		}

		public override void OnCancel()
		{
			AssetManager.SubDepend(m_AssetBundleName, id);

			if (m_WebRequest != null)
			{
				if (!m_WebRequest.isDone)
					m_WebRequest.Abort();
				m_WebRequest.Dispose();
				m_WebRequest = null;
			}
			base.OnCancel();
		}
	}

	/// <summary>
	/// アセットバンドル事前ロードオペレーション
	/// </summary>
	public class BundlePreLoadOperation : AssetOperation
	{
		static StringBuilder sb = new StringBuilder();

		readonly List<BundleLoadOperation> m_Operations;

		public BundlePreLoadOperation(List<BundleLoadOperation> operations)
		{
			m_Operations = operations;
		}

		public override bool Update()
		{
			if (m_Operations.Count == 0)
				return false;

			float rate = 0;
			for (int i = 0; i < m_Operations.Count; i++)
			{
				var op = m_Operations[i];
				if (op == null || !op.keepWaiting)
				{
					rate += 1;
				}
				else
				{
					rate += op.progress;
				}
			}
			progress = rate / m_Operations.Count;
			return base.Update();
		}

		public override bool keepWaiting
		{
			get
			{
				for (int i = 0; i < m_Operations.Count; i++)
				{
					if (m_Operations[i].keepWaiting)
						return true;
				}
				return false;
			}
		}

		public override void OnComplete()
		{
			progress = 1;
			sb.Length = 0;
			foreach (var op in m_Operations)
			{
				if (!string.IsNullOrEmpty(op.error))
				{
					sb.AppendLine(op.error);
				}
			}
			error = sb.ToString();
			base.OnComplete();
		}

		public override void OnCancel()
		{
			m_Operations.Clear();
			base.OnCancel();
		}
	}
}