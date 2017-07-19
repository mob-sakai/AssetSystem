using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using Mobcast.Coffee.AssetSystem;

public class Test : MonoBehaviour
{
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
		AssetManager.SetDomainURL(resourceDomain);
	}


	public void UpdateManifest()
	{
		AssetManager.SetVersion(resourceVersion);
	}

	public void LoadAll()
	{
		AssetManager.PreDownload ();

	}



	public void LoadFromBundle()
	{
		AssetManager.LoadAssetAsync<Sprite>(assetBundleName, assetName, obj => image.sprite = obj);
	}

	public void LoadFromResources()
	{
		AssetManager.LoadAssetAsync<Sprite>(assetName, obj => image.sprite = obj);
	}

	public void LoadFromWeb()
	{
		AssetManager.LoadAssetAsync<Texture> ("https://s3-ap-northeast-1.amazonaws.com/patch.s3.sand.mbl.mobcast.io/image/shop/order/BNR_order_0000.png", img => rawimage.texture = img);
	}


	public void LoadVersions()
	{
		AssetManager.UpdateResourceVersions("https://s3-ap-northeast-1.amazonaws.com/patch.s3.sand.mbl.mobcast.io/deploy/history.json");
	}

	public void ClearAll()
	{
		AssetManager.ClearAll ();
	}
}



