using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using Mobcast.Coffee.AssetSystem;

namespace Mobcast.Coffee.AssetSystem
{
	public class Demo_AssetSystem : MonoBehaviour
	{
		const string CloudServerURL = "https://s3-ap-northeast-1.amazonaws.com/patch.s3.sand.lip.mobcast.io/__AssetSystemDemo/";

		[Header("概要")]
		public LayoutGroup layoutGroup;
		public Text textSummary;

		//	[Header("サーバー")]
		//	public Toggle toggleServerLocal;
		//	public Toggle toggleServerSimulation;
		//	public Toggle toggleServerCloud;
		//	public Toggle toggleServerStreamingAssets;

		[Header("パッチ")]
		public GameObject prefabPatch;
		public Slider sliderProgress;
		public Text textCurrentPatch;

		[Header("インジケータ")]
		public GameObject goDownloading;
		public GameObject goLoading;
		public GameObject goBusy;

		[Header("ロード")]
		public RawImage rawimage;


		IEnumerator Start()
		{
			yield return new WaitUntil(() => AssetManager.ready);
			textCurrentPatch.text = AssetManager.patch.ToString();

			// パッチURLを設定.
			if (!AssetManager.isStreamingAssetsMode)
				AssetManager.SetPatchServerURL(CloudServerURL);

			InvokeRepeating("UpdateSummary", 0, 1);
		}

		void Update()
		{
			goDownloading.SetActive(AssetManager.m_InProgressOperations.Any(op => op is BundleLoadOperation));
			goLoading.SetActive(AssetManager.m_InProgressOperations.Any(op => op is AssetLoadOperation));
			goBusy.SetActive(AssetManager.m_InProgressOperations.Any());
		}

		void UpdateSummary()
		{
			textSummary.text = AssetManager.instance.ToString();
			layoutGroup.SetLayoutVertical();
		}


		/// <summary>
		/// 最新のパッチに更新.
		/// </summary>
		public void OnClick_UpdatePatchList()
		{
			sliderProgress.value = 0;
			AssetManager.SetPatchLatest(CloudServerURL + "deploy/history.json");
		}

		/// <summary>
		/// プリロードを実行します
		/// </summary>
		public void OnClick_PreloadPatch()
		{
			var op = AssetManager.PreDownload();
			StartCoroutine(Co_PreloadProgress(op));
		}

		IEnumerator Co_PreloadProgress(AssetOperation op)
		{
			if (op == null)
				yield break;
		
			while (op.keepWaiting)
			{
				sliderProgress.value = op.progress;
				yield return null;
			}
			sliderProgress.value = 1;
			yield break;
		}

		/// <summary>
		/// パッチをクリア
		/// </summary>
		public void OnClick_ClearPatch()
		{
			AssetManager.ClearCachedAssetBundleAll();
			sliderProgress.value = 0;

		}

		/// <summary>
		/// ランタイムキャッシュをクリア
		/// </summary>
		public void OnClick_ClearAssetCache()
		{
			AssetManager.ClearRuntimeCacheAll();
		}

		public void OnClick_LoadImage(string path)
		{
			AssetManager.LoadAssetAsync<Texture2D>(path, img => rawimage.texture = img);
		}
	}
}