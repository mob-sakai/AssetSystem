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

	public abstract class AssetOperation : IEnumerator
	{
		public object Current { get { return null; } }

		public string error { get; protected set; }

		public virtual float progress { get; }


		public event System.Action onComplete = ()=>{};

		public bool MoveNext()
		{
			return !IsDone();
		}

		public void Reset()
		{
		}

		public virtual bool Update()
		{
			return !IsDone();
		}

		abstract public bool IsDone();

		public virtual void OnComplete()
		{
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
			onComplete = null;
		}
	}

	public class BundleLoadOperation : AssetOperation
	{
		public AssetBundle assetBundle { get; protected set; }

		public override float progress { get { return m_request != null ? m_request.downloadProgress : 1; } }

		UnityWebRequest m_request;

		public BundleLoadOperation(UnityWebRequest request)
		{
			if (request == null || !(request.downloadHandler is DownloadHandlerAssetBundle))
				throw new System.ArgumentNullException("request");

			m_request = request;
			m_request.Send();
		}

		public override bool IsDone()
		{
			return m_request == null || m_request.isDone;
		}

		public override void OnComplete()
		{
			Debug.Log("BundleLoadOperation OnComplete");
			if (!string.IsNullOrEmpty(m_request.error))
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
					AssetManager.AddDepend (System.IO.Path.GetFileName(m_request.url), null);

				}
			}
			AssetManager.m_LoadedAssetBundles.Add(System.IO.Path.GetFileName(m_request.url), assetBundle);

			m_request.Dispose();
			m_request = null;

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



	public class AssetLoadOperation : AssetOperation
	{
		protected string m_AssetBundleName;
		protected string m_AssetName;
//		protected string m_DownloadingError;
		protected System.Type m_Type;
		protected AsyncOperation m_Request = null;
		public UnityWebRequest m_WebRequest = null;

		/// <summary>Load asset complete callback.</summary>
//		public System.Action<Object> onComplete;

		/// <summary>Operation identifier.</summary>
		public string id { get; protected set; }

		public static string GetId(string bundleName, string assetName, System.Type type)
		{
			return string.Format("{0}.{1}.{2}", bundleName, assetName, type.Name);
		}

		public AssetLoadOperation(string bundleName, string assetName, System.Type type)
		{
			//			m_RuntimeCache = runtimeCache;
			m_AssetBundleName = bundleName;
			m_AssetName = assetName;
			m_Type = type;
			id = GetId(m_AssetBundleName, m_AssetName, m_Type);


			// Asset in Web or StreamingAssets.
			if (assetName.Contains("://"))
			{
				if(m_Type == typeof(Texture))
					m_WebRequest = UnityWebRequest.GetTexture(assetName, true);
				else if(m_Type == typeof(AudioClip))
					m_WebRequest = UnityWebRequest.GetAudioClip(assetName, AudioType.MPEG);
				
				m_Request = m_WebRequest.Send();
			}
			// Asset in Resources.
			else if (string.IsNullOrEmpty(m_AssetBundleName))
			{
				m_Request = Resources.LoadAsync(assetName, type);
			}
		}
	
//		public AssetLoadOperation(UnityWebRequest request, System.Type type)
//		{
//			m_WebRequest = request;
//			m_AssetBundleName = "";
//			m_AssetName = request.url;
//			m_Type = type;
//			id = GetId("", m_AssetName, m_Type);
//		}
//
//		public AssetLoadOperation(string assetName, ResourceRequest request, System.Type type)
//		{
//			m_Request = request;
//			m_AssetBundleName = "";
//			m_AssetName = assetName;
//			m_Type = type;
//			id = GetId("", m_AssetName, m_Type);
//		}
		// Asset is in Web or StreamingAssets.
		//				if (assetName.Contains("://"))
		//				{
		//						operation = new AssetLoadOperation(assetName, UnityWebRequest.GetTexture(assetName, true), type, m_RuntimeCache,
		//						dl => (dl as DownloadHandlerTexture).texture);
		//				}

		public T GetAsset<T>() where T:Object
		{
			// Operation has been cached.
			Object obj = null;
			if (AssetManager.m_RuntimeCache.TryGetValue(id, out obj))
			{
				return obj as T;
			}

			return null;
		}

		public override float progress { get { return m_Request != null ? m_Request.progress : 0; } }

		// Returns true if more Update calls are required.
		public override bool Update()
		{
			Debug.Log(id + " Update.");

			// Operation has been cached.
			if (AssetManager.m_RuntimeCache.ContainsKey(id))
				return false;

//			if (m_WebRequest != null)
//			{
//				return m_WebRequest.isDone;
//			}
//			else 
			if (m_Request == null && !string.IsNullOrEmpty(m_AssetBundleName))
			{
				AssetBundle bundle;
				if (AssetManager.m_LoadedAssetBundles.TryGetValue(m_AssetBundleName, out bundle))
				{
					if (!bundle)
					{
						error = "error : bundle loading error";
						Debug.LogError(error);
						return false;
					}
					else
					{
//						Debug.Log(bundle + " contains.");
						bundle.GetAllAssetNames().LogDump();
						m_Request = bundle.LoadAssetAsync(m_AssetName, m_Type);

						AssetManager.AddDepend (m_AssetBundleName, id);
						//			AssetBundleManager.AddDepend(m_AssetBundleName, id);
					}
				}
//				Debug.Log(bundle + " bundle??.");
			} 

			return !IsDone();
		}

		public override bool IsDone()
		{
			Debug.Log(id + " IsDone.");

			// Operation has been cached.
			if (AssetManager.m_RuntimeCache.ContainsKey(id))
				return true;

			//			// Return if meeting downloading error.
			//			// m_DownloadingError might come from the dependency downloading.
			if (!string.IsNullOrEmpty(error))
				return true;

//			if (m_WebRequest != null)
//				return m_WebRequest.isDone;

			return m_Request != null && m_Request.isDone;
		}

		public override void OnComplete()
		{
			Debug.Log(id + " OnComplete.");
			//			AssetBundleManager.SubDepend(m_AssetBundleName, id);

			if (m_WebRequest != null)
			{
				if (m_WebRequest.isDone && string.IsNullOrEmpty( m_WebRequest.error))
				{
					if(m_WebRequest.downloadHandler is DownloadHandlerTexture)
						AssetManager.m_RuntimeCache[id] = (m_WebRequest.downloadHandler as DownloadHandlerTexture).texture;
					else if(m_WebRequest.downloadHandler is DownloadHandlerAudioClip)
						AssetManager.m_RuntimeCache[id] = (m_WebRequest.downloadHandler as DownloadHandlerAudioClip).audioClip;
//					else if(m_WebRequest.downloadHandler is DownloadHandler)
//						AssetManager.m_RuntimeCache[id] = TextAsset.(m_WebRequest.downloadHandler as DownloadHandler).text;

				}

				if (!m_WebRequest.isDone)
					m_WebRequest.Abort();
				m_WebRequest.Dispose();
				m_WebRequest = null;
			}
			else if (m_Request is ResourceRequest)
			{
				if(m_Request.isDone)
					AssetManager.m_RuntimeCache[id] = (m_Request as ResourceRequest).asset;
			}
			else if (m_Request is AssetBundleRequest)
			{
				AssetManager.SubDepend (m_AssetBundleName, id);
				if(m_Request.isDone)
					AssetManager.m_RuntimeCache[id] = (m_Request as AssetBundleRequest).asset;
			}
			base.OnComplete();
		}

		public override void OnCancel()
		{
			AssetManager.SubDepend (m_AssetBundleName, id);

			error = "operation has been canceled";

			if (m_WebRequest != null)
			{
				if (!m_WebRequest.isDone)
					m_WebRequest.Abort();
				m_WebRequest.Dispose();
				m_WebRequest = null;
			}
			base.OnCancel();
		}



//		public override void OnCancel()
//		{
//			base.OnCancel();
//		}
//		public override void OnCancel()
//		{
//			//			AssetBundleManager.SubDepend(m_AssetBundleName, id);
//			onComplete = null;
//		}
	}


	public class BundlePreLoadOperation : AssetOperation
	{
		readonly List<BundleLoadOperation> m_Operations;

		public BundlePreLoadOperation(List<BundleLoadOperation> operations)
		{
			m_Operations = operations;
		}

		public override float progress
		{
			get
			{
				if (m_Operations.Count == 0)
					return 1;

				float rate = 0;
				for (int i = 0; i < m_Operations.Count; i++)
				{
					var op = m_Operations[i];
					if (op == null || op.IsDone())
					{
						rate += 1;
					}
					else
					{
						rate += op.progress;
					}
				}
				return rate /= m_Operations.Count;
			}
		}

		public override bool IsDone()
		{
			for (int i = 0; i < m_Operations.Count; i++)
			{
				if (m_Operations[i].IsDone() == false)
					return false;
			}
			return true;
		}

		public override void OnComplete()
		{
			StringBuilder sb = new StringBuilder();
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
	}
}