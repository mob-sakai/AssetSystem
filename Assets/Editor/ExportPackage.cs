using UnityEditor;

namespace Mobcast.Coffee.AssetSystem
{
	public static class ExportPackage
	{
		const string kPackageName = "AssetSystem.unitypackage";
		static readonly string[] kAssetPathes = {
			"Assets/Mobcast/Coffee/AssetSystem",
		};

		[MenuItem ("Coffee/Export Package/" + kPackageName)]
		[InitializeOnLoadMethod]
		static void Export ()
		{
			if (EditorApplication.isPlayingOrWillChangePlaymode)
				return;
			
			AssetDatabase.ExportPackage (kAssetPathes, kPackageName, ExportPackageOptions.Recurse | ExportPackageOptions.Default);
			UnityEngine.Debug.Log ("Export successfully : " + kPackageName);
		}
	}
}