using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using Mobcast.Coffee.AssetSystem;

public class Test : MonoBehaviour
{
	const string CloudServerURL = "https://s3-ap-northeast-1.amazonaws.com/patch.s3.sand.lip.mobcast.io/";

	[Header("概要")]
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
//		prefabPatch.SetActive(false);

//		toggleServerCloud.isOn = true;
//
//		toggleServerLocal.gameObject.SetActive(Application.isEditor);
//		toggleServerSimulation.gameObject.SetActive(Application.isEditor);

		yield return new WaitUntil(()=>AssetManager.ready);
		textCurrentPatch.text = AssetManager.patch.ToString();

		OnClick_UpdatePatchList();

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
	}


	/// <summary>
	/// パッチリストの更新
	/// </summary>
	public void OnClick_UpdatePatchList()
	{
		sliderProgress.value = 0;
		/*
		if (toggleServerCloud.isOn)
		{
			var patchParent = prefabPatch.transform.parent;
			for (int i = 1; i < patchParent.childCount; i++)
			{
				Destroy(patchParent.GetChild(i).gameObject);
			}

			string path = AssetManager.patchServerURL + "history.json";
			AssetManager.UpdatePatchList(path, list =>
				{
					if (list == null || list.patchList.Length == 0)
					{
						Debug.LogErrorFormat("パッチリストが存在しません : {0}", path);
						AddPatchButton(new Patch(){ commitHash = "", comment = "デフォルト" });
						return;
					}

					foreach (var p in list.patchList)
					{
						AddPatchButton(p);
					}
				});
		}
		else
		{
			AssetManager.SetPatch(AssetManager.patch);
			textCurrentPatch.text = AssetManager.patch.ToString();
		}
		*/
	}


	void AddPatchButton(Patch p)
	{
		var go = Object.Instantiate(prefabPatch, prefabPatch.transform.parent);
		string summary = p.ToString();
		go.GetComponentInChildren<Button>().onClick.AddListener(() =>
			{
				AssetManager.SetPatch(p);
				textCurrentPatch.text = summary;
			});
		go.name = summary;
		go.GetComponentInChildren<Text>().text = summary;

		go.SetActive(true);
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
