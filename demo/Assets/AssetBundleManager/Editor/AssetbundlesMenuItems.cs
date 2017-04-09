using UnityEngine;
using UnityEditor;
using System.Collections;

namespace AssetBundles
{
    public class AssetBundlesMenuItems
    {
        const string kSimulationMode = "Assets/AssetBundles/Simulation Mode";

        [MenuItem(kSimulationMode)]
        public static void ToggleSimulationMode()
        {
            AssetManager.SimulateAssetBundleInEditor = !AssetManager.SimulateAssetBundleInEditor;
        }

        [MenuItem(kSimulationMode, true)]
        public static bool ToggleSimulationModeValidate()
        {
            Menu.SetChecked(kSimulationMode, AssetManager.SimulateAssetBundleInEditor);
            return true;
        }

        [MenuItem("Assets/AssetBundles/Build AssetBundles")]
        static public void BuildAssetBundles()
        {
            BuildScript.BuildAssetBundles();
        }

        [MenuItem ("Assets/AssetBundles/Build Player (for use with engine code stripping)")]
        static public void BuildPlayer ()
        {
            BuildScript.BuildPlayer();
        }
    }
}
