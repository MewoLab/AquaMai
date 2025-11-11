using HarmonyLib;
using UnityEngine;
using AquaMai.Config.Attributes;

namespace AquaMai.Mods.GameSystem.Assets;

[ConfigSection(
	defaultOn: true,
    en: "Ignore .ab image resources that fail to load",
    zh: "忽略加载失败的 .ab 图片资源")]
public class IgnoreLoadFailedAssetBundle
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(AssetBundleManager), "OnSucess")]
    public static bool PatchOnSucess(string name, AssetBundle loadedAssetBundle)
    {
        if (loadedAssetBundle == null) return false;
        return true;
    }
}