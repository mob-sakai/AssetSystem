AssetSystem
===

## Overview

A system that loads and manages assets.



## Requirement

* Unity5.4+
* No other SDK



## 読込み可能なもの

* AssetBundle
* StreamingAssets
* Resources
* Web(Texture/Audio/Text/Bytes)



## Usage

* Download AssetSystem.unitypackage and install to your project.
* From the menu, click `Coffee > AssetSystem > Setup`
* Use in your script.
```cs
// 
AssetManager.SetResourceDomainURL(resourceDomain);
AssetManager.SetResourceVersion(resourceVersion);
AssetManager.PreDownload ();
AssetManager.LoadAssetAsync<Sprite>(assetBundleName, assetName, obj => image.sprite = obj);
AssetManager.LoadAssetAsync<Texture> ("https://s3-ap-northeast-1.amazonaws.com/patch.s3.sand.mbl.mobcast.io/image/shop/order/BNR_order_0000.png", img => rawimage.texture = img);
AssetManager.UpdateResourceVersions("https://s3-ap-northeast-1.amazonaws.com/patch.s3.sand.mbl.mobcast.io/deploy/history.json");
```


## Construct AssetBundle Deliverly Service

In order to deliver asset bundles efficiently, not only storage services but also CDN (Contents Deliverly Network) are necessary.
CDN is a network that deliverlies content from storage services to end users, and it distributes the deliverly load of large files.
AWS (Amazon Web Service) provides S3 as a storage service and CloudFront as a CDN.

The CDN has a publicly accessible endpoint, from which directories can be constructed hierarchically.
When using AssetManager, place the AssetBundles as follows.  
`https://{CDN Endpoint}/{Patch Id}/{Platform}/{AssetBundles}`

An example is as follows.  
`https://patch.akamaized.net/5eb46/Android/tutorial.assetbundle`

| Parameter		| Description														|
|:----------	|:----------------------------------------------------------------	|
| CDN Endpoint	| Endpoint URL of CDN.												|
| Patch Id		| A unique identifier for the patch, such as a revision hash (Git).	|
| Platform		| Android, iOS, WebGL ...											|
| AssetBundles	| All AssetBundles in the patch.									|




## Patch

Patch is a **full collection** of AssetBundles in a specific revision.  
AssetBundleManifest is responsible for the history management for each AssetBundle.  
In most cases, storage service capacity is not a problem.  

`Asset(Texture, AudioClip, Prefab...) ∈ AssetBundle ∈ Patch`



## Patch Management without API Server

パッチ履歴を利用すると、APIサーバーなしでパッチの管理ができます。
パッチ履歴をjsonファイルで次のように記述し、CDNで配信してください。


```cs
{
  "patchList": [
    {
      "buildTime": 1501579242,
      "comment": "New Stage Available!",
      "deployTime": 1501579242,
      "commitHash": "5eb46"
    },
    ...
  ]
}
```

| Parameter		| Description														|
|:-------------	|:-----------------------------------------------------------------	|
| buildTime		| UNIX Time of the patch built.										|
| comment		| Comment for the patch.											|
| deployTime	| UNIX Time of the patch deployed.									|
| commitHash	| A unique identifier for the patch, such as a revision hash (Git).	|


スクリプトから次のように利用します.  
1. `AssetManager.GetPatchHistory` によって、パッチ履歴を取得します. これは、最新のPatch Idを取得するために
```cs
// Get patch history.
AssetManager.GetPatchHistory("https://patch.akamaized.net/PatchHistory.json");

// Set leatest patch to use.
AssetManager.SetPatch(AssetManager.leatestPatch);

// Download AssetBundles beforehand.
AssetManager.Preload();
```





## AssetBundleManagerとの相違

### 同仕様
* オペレーション処理
* シミュレーションモード
* アセットバンドルのキャッシングはUnity標準(Caching)

### 機能追加
* マニフェストによるアセットバンドルのバージョニング
* ランタイムキャッシュ
    * 一度ロードしたアセットは、ランタイムキャッシュされます。
    * キャッシュのクリアも可能です。
    * 同じ名前の場合でも、型が違う場合は別オブジェクトとして判定されます。
* プレロード（事前にダウンロード）
* キャッシュクリア
    * 弱いランタイムキャッシュクリア
    * キャッシュクリア
    * 強いランタイムキャッシュクリア
* Resources、StreamingAssets、Web(Texture/Audio/Text/Bytes)からのロード
* リソースバージョン(json)
* Etagによるキャッシュ付きダウンロード

### オミット
* ローカルサーバ
* Variant
* iOSのリソースメソッド
* シーンのロード

## アセットバンドル運用
* インフラチームのCDSシステムに則る
* 05.インフラ資料 - 発表資料 - 20160721 - CDNについて
* 05.インフラ資料 - システム : パッチ ビルド/デプロイ
* https://s3-ap-northeast-1.amazonaws.com/patch.s3.sand.mbl.mobcast.io/$version/$platform/$platform.manifest
* リビジョンコミット毎に１つのリソースバージョンがフルで入っている
* CDSドメイン　ドメインURLを入力
* バージョン　アセットバンドルビルド　ハッシュ値
* プレロード　事前にダウンロード




## Screenshot




## Release Notes

### ver.0.3.0:

* ギャップチェック
* ランタイムキャッシュバリデーション

### ver.0.2.0:

* StreamingAssetsモードの追加


### ver.0.1.0:

* Load asset 