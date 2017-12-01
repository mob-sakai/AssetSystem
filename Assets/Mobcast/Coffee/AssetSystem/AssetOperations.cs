using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using UnityEngine.Networking;
using Object = UnityEngine.Object;
using System.Text;
using System.IO;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Mobcast.Coffee.AssetSystem
{
	/// <summary>
	/// Asset operation.
	/// </summary>
	public abstract class AssetOperation : CustomYieldInstruction
	{
		protected const string kLog = "[AssetManager] ";

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
				error = ex.Message;
			}

			if (!string.IsNullOrEmpty(error))
				Debug.LogErrorFormat("{0} {1} エラー: {2}, {3}", kLog, GetType().Name, error, id);
			onComplete = null;
		}

		public virtual void OnCancel()
		{
			Debug.LogWarningFormat("{0} {1} キャンセルしました: {2}, {3}", kLog, GetType().Name, error, id);
			onComplete = null;
		}
	}

	/// <summary>
	/// バンドルロードオペレーション
	/// </summary>
	public class BundleLoadOperation : AssetOperation
	{
		public AssetBundle assetBundle { get; protected set; }

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
			if (m_request == null)
			{
			}
			else if (!string.IsNullOrEmpty(m_request.error))
			{
				error = string.Format("{0}, {1}", m_request.error, m_request.url);
			}
			else
			{
				var handler = m_request.downloadHandler as DownloadHandlerAssetBundle;
				if (handler == null || handler.assetBundle == null)
				{
					error = string.Format("無効なアセットバンドルです : {0}", m_request.url);
				}
				else
				{
					assetBundle = handler.assetBundle;
					AssetManager.AddDepend(assetBundle.name, null);
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

			// ランタイムキャッシュに存在すれば、そのまま利用
			if (AssetManager.m_RuntimeCache.TryGetValue(id, out m_Object))
			{
				if (m_Object)
					return;
				else
					AssetManager.m_RuntimeCache.Remove(id);
			}

			AssetManager.AddDepend(m_AssetBundleName, id);

#if UNITY_EDITOR
			// シミュレーションモード中. アセットバンドルはAssetDataBaseを利用してロード.
			// Simulation mode (only in editor).
			if (AssetManager.isSimulationMode && !assetName.Contains("://") && type != typeof(AssetBundleManifest))
			{
				// Resources からロードします.
				if(string.IsNullOrEmpty(bundleName))
				{
					var relativePath = string.Format("resources/{0}.", assetName).ToLower();
					m_Object = UnityEditor.AssetDatabase.FindAssets(string.Format("t:{0} {1}", type.Name, Path.GetFileName(assetName)))
						.Select(guid=>UnityEditor.AssetDatabase.GUIDToAssetPath(guid).ToLower())
						.Where(x=> x.Contains(relativePath))
						.Select(x => UnityEditor.AssetDatabase.LoadAssetAtPath(x, type))
						.FirstOrDefault();
				}
				// AssetBundle からロードします.
				else
				{
					m_Object = UnityEditor.AssetDatabase.GetAssetPathsFromAssetBundleAndAssetName(bundleName, assetName)
						.Select(x => UnityEditor.AssetDatabase.LoadAssetAtPath(x, type))
						.FirstOrDefault();
				}
				
				if (!m_Object)
				{
					if(string.IsNullOrEmpty(m_AssetBundleName))
						error = string.Format("アセット {1} が見つかりませんでした(シミュレーションモード)", m_AssetBundleName, m_AssetName);
					else
						error = string.Format("アセットバンドル {0} 内に、アセット {1} が見つかりませんでした(シミュレーションモード)", m_AssetBundleName, m_AssetName);
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

				if (AssetManager.TryGetBundle(m_AssetBundleName, out bundle))
				{
					// バンドルロードに失敗している
					if (!bundle)
					{
						error = string.Format("アセットバンドル {0} のロードに失敗しているため、アセット {1} をロードできません", m_AssetBundleName, m_AssetName);
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
			AssetManager.SubDepend(m_AssetBundleName, id);

			// Web、StreamingAssets、デバイス内からのロード
			// Asset in Web or StreamingAssets.
			if (m_WebRequest != null)
			{
				error = m_WebRequest.error;
				if (m_WebRequest.isDone && string.IsNullOrEmpty(error))
				{
					if (m_Type == typeof(Texture2D))
					{
						m_Object = (m_WebRequest.downloadHandler as CacheableDownloadHandlerTexture).texture;
					}
					else if (m_Type == typeof(AudioClip))
					{
						m_Object = (m_WebRequest.downloadHandler as DownloadHandlerAudioClip).audioClip;
					}
					else
					{
						m_Object = PlainObject.Create(m_WebRequest.downloadHandler.data);
					}
				}

				if (!m_WebRequest.isDone)
					m_WebRequest.Abort();
				m_WebRequest.Dispose();
				m_WebRequest = null;
			}
			// Resources内のアセットロード
			// Asset in Resources.
			else if (m_Request is ResourceRequest)
			{
				if ((m_Request as ResourceRequest).asset)
					m_Object = (m_Request as ResourceRequest).asset;
				else
					error = string.Format("Resources内に、アセット {1} が見つかりませんでした", m_AssetBundleName, m_AssetName);
			}
			// アセットバンドル内のアセットロード
			// Asset in AssetBundle.
			else if (m_Request is AssetBundleRequest)
			{
				if ((m_Request as AssetBundleRequest).asset)
					m_Object = (m_Request as AssetBundleRequest).asset;
				else
					error = string.Format("アセットバンドル {0} 内に、アセット {1} が見つかりませんでした", m_AssetBundleName, m_AssetName);
			}

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
					rate += Mathf.Clamp01(op.progress);
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



	/// <summary>
	/// シーンロードオペレーション
	/// </summary>
	public class SceneLoadOperation : AssetOperation
	{
		protected string m_AssetBundleName;
		protected string m_SceneName;
		protected System.Type m_Type;
		protected bool m_IsAdditive;
		protected AsyncOperation m_Request = null;

		public static string GetId(string bundleName, string sceneName)
		{
			if(!string.IsNullOrEmpty(bundleName))
				return string.Format("ab://{0}/{1}({2})", bundleName, sceneName, "Scene");
			else
				return string.Format("{0}({1})", sceneName, "Scene");
		}

		public SceneLoadOperation(string bundleName, string sceneName, bool isAdditive)
		{
			m_AssetBundleName = bundleName;
			m_SceneName = sceneName;
			m_IsAdditive = isAdditive;
			id = GetId(m_AssetBundleName, m_SceneName);

			AssetManager.AddDepend(m_AssetBundleName, id);

#if UNITY_EDITOR
			// シミュレーションモード中. アセットバンドルはAssetDatabaseを利用してロード.
			// Simulation mode (only in editor).
			if (AssetManager.isSimulationMode)
			{
				var filter = string.IsNullOrEmpty(bundleName) ? "t:Scene" : "t:Scene b:" + bundleName;
				var path = AssetDatabase.FindAssets(filter + " " + sceneName)
					.Select(AssetDatabase.GUIDToAssetPath)
					.FirstOrDefault(x=>Path.GetFileNameWithoutExtension(x) == sceneName);

				if (!string.IsNullOrEmpty(path))
				{
					m_Request = isAdditive
						? EditorApplication.LoadLevelAsyncInPlayMode(path)
						: EditorApplication.LoadLevelAdditiveAsyncInPlayMode(path);
				}
				else
				{
					if(string.IsNullOrEmpty(m_AssetBundleName))
						error = string.Format("シーン {0} が見つかりませんでした(シミュレーションモード)", m_SceneName);
					else
						error = string.Format("アセットバンドル {0} 内に、シーン {1} が見つかりませんでした(シミュレーションモード)", m_AssetBundleName, m_SceneName);
				}

				progress = 1f;
				return;
			}
#endif

			// アセットバンドルではない.
			if (string.IsNullOrEmpty(m_AssetBundleName))
			{
				m_Request = SceneManager.LoadSceneAsync(sceneName, isAdditive ? LoadSceneMode.Additive : LoadSceneMode.Single);
			}
		}

		// Returns true if more Update calls are required.
		public override bool Update()
		{
			// Return if meeting downloading error.
			if (!string.IsNullOrEmpty(error))
				return false;


			progress = m_Request != null ? m_Request.progress : 0;

			if (m_Request == null && !string.IsNullOrEmpty(m_AssetBundleName))
			{
				AssetBundle bundle;

				if (AssetManager.TryGetBundle(m_AssetBundleName, out bundle))
				{
					// バンドルロードに失敗している
					if (!bundle)
					{
						error = string.Format("アセットバンドル {0} のロードに失敗しているため、シーン {1} をロードできません", m_AssetBundleName, m_SceneName);
						return false;
					}
					else
					{
						m_Request = SceneManager.LoadSceneAsync(m_SceneName, m_IsAdditive ? LoadSceneMode.Additive : LoadSceneMode.Single);
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
				// Return if meeting downloading error.
				if (!string.IsNullOrEmpty(error))
					return false;

				return m_Request == null || !m_Request.isDone;
			}
		}

		public override void OnComplete()
		{
			AssetManager.SubDepend(m_AssetBundleName, id);
			base.OnComplete();
		}

		public override void OnCancel()
		{
			AssetManager.SubDepend(m_AssetBundleName, id);
			base.OnCancel();
		}
	}
}