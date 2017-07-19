AssetSystem
===

## Overview

ゲーム内においてアセットを読み込むためのシステムです。

## 読込み可能なもの

すべて同じメソッドで読み込むことができます。

* AssetBundle
* StreamingAssets
* Resources
* Web(Texture/Audio/Text/Bytes)



## 使い方

```cs
AssetManager.SetResourceDomainURL(resourceDomain);
AssetManager.SetResourceVersion(resourceVersion);
AssetManager.PreDownload ();
AssetManager.LoadAssetAsync<Sprite>(assetBundleName, assetName, obj => image.sprite = obj);
AssetManager.LoadAssetAsync<Texture> ("https://s3-ap-northeast-1.amazonaws.com/patch.s3.sand.mbl.mobcast.io/image/shop/order/BNR_order_0000.png", img => rawimage.texture = img);
AssetManager.UpdateResourceVersions("https://s3-ap-northeast-1.amazonaws.com/patch.s3.sand.mbl.mobcast.io/deploy/history.json");
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

## Requirement

* Unity5.4+
* No other SDK



## Screenshot




## Release Notes

### ver.0.1.0:

