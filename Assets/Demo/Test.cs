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

	[Header("サーバー")]
	public Text textServerURL;

	public Toggle toggleServerLocal;
	public Toggle toggleServerSimulation;
	public Toggle toggleServerCloud;
	public Toggle toggleServerStreamingAssets;

	[Header("パッチ")]
	public GameObject prefabPatch;
	public Slider sliderProgress;
	public Text textCurrentPatch;

	[Header("インジケータ")]
	public GameObject goDownloading;
	public GameObject goLoading;
	public GameObject goBusy;


	public const string AssetBundlesOutputPath = "/AssetBundles/";
	public string assetBundleName;
	public string assetName;

	public string resourceDomain = "https://s3-ap-northeast-1.amazonaws.com/patch.s3.sand.mbl.mobcast.io/";
	public string resourceVersion = "e033f50907cc57b0b9b737a5ba1066b88a604d0e";
	public string manifestName = "Android";


	public Image image;
	public RawImage rawimage;

	public Text m_Log;
	public Text m_Progress;


	public void SetSourceAssetBundleURL()
	{
		AssetManager.SetPatchServerURL(resourceDomain);
	}


	public void UpdateManifest()
	{
		AssetManager.SetPatch(AssetManager.patch);
	}

	public void LoadAll()
	{
		AssetManager.PreDownload();

	}

	public void LoadFromBundle()
	{
		AssetManager.AddDepend("common", "keep");
		AssetManager.LoadAssetAsync<GameObject>(assetBundleName, assetName, obj => GameObject.Instantiate(obj));
	}

	public void LoadFromResources()
	{
		AssetManager.LoadAssetAsync<Sprite>(assetName, obj => image.sprite = obj);
	}

	public void LoadFromWeb()
	{
		AssetManager.LoadAssetAsync<Texture2D>("https://s3-ap-northeast-1.amazonaws.com/patch.s3.sand.mbl.mobcast.io/image/shop/order/BNR_order_0001.png", img => rawimage.texture = img);
	}


	public void LoadVersions()
	{
		AssetManager.UpdatePatchList(AssetManager.patchServerURL + "history.json");
	}

	public void ClearAll()
	{
		AssetManager.ClearAll();
	}

	public void OnClick_SelectServer()
	{
		AssetManager.ClearAll();
		#if UNITY_EDITOR
		if (toggleServerLocal.isOn)
		{
			AssetManager.EnableLocalServerMode();
		}
		if (toggleServerSimulation.isOn)
		{
			AssetManager.EnableSimulationMode();
		}
		#endif
		if (toggleServerCloud.isOn)
		{
			AssetManager.SetPatchServerURL(CloudServerURL);
		}
		if (toggleServerStreamingAssets.isOn)
		{
			AssetManager.SetPatchServerURLToStreamingAssets();
		}
		textServerURL.text = AssetManager.patchServerURL;
		textCurrentPatch.text = AssetManager.patch.ToString();

		//パッチリストの更新.
		OnClick_UpdatePatchList();
	}

	void Start()
	{

		prefabPatch.SetActive(false);
		toggleServerCloud.isOn = true;

		toggleServerLocal.gameObject.SetActive(Application.isEditor);
		toggleServerSimulation.gameObject.SetActive(Application.isEditor);

		#if UNITY_EDITOR
		toggleServerLocal.isOn = AssetManager.isLocalServerMode;
		toggleServerSimulation.isOn = AssetManager.isSimulationMode;
		#endif

		OnClick_SelectServer();


		InvokeRepeating("UpdateSummary", 1, 1);
	}

	void UpdateSummary()
	{
		textSummary.text = AssetManager.instance.ToString();
	}

	void Update()
	{
		goDownloading.SetActive(AssetManager.m_InProgressOperations.Any(op => op is BundleLoadOperation));
		goLoading.SetActive(AssetManager.m_InProgressOperations.Any(op => op is AssetLoadOperation));
		goBusy.SetActive(AssetManager.m_InProgressOperations.Any());
	}

	/// <summary>
	/// パッチリストの更新
	/// </summary>
	public void OnClick_UpdatePatchList()
	{
		sliderProgress.value = 0;

		var patchParent = prefabPatch.transform.parent;
		for (int i = 1; i < patchParent.childCount; i++)
		{
			Destroy(patchParent.GetChild(i).gameObject);
		}

		string path = AssetManager.patchServerURL + "history.json";
		AssetManager.UpdatePatchList(path, list =>
			{
				if(list == null || list.patchList.Length == 0)
				{
					Debug.LogErrorFormat("パッチリストが存在しません : {0}", path);
					return;
				}

				foreach (var p in list.patchList)
				{
					AddPatchButton(p);
				}
			});
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

	public void OnClick_LoadTank(string path)
	{
		AssetManager.AddDepend("common", "keep");
		AssetManager.LoadAssetAsync<GameObject>(assetBundleName, assetName, obj =>
			{
				var go = Object.Instantiate(obj);
			});
	}
}
