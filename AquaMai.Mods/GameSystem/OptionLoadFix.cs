using System.Collections.Generic;
using System.Linq;
using AMDaemon;
using AquaMai.Config.Attributes;
using HarmonyLib;
using Manager;
using UnityEngine;

namespace AquaMai.Mods.GameSystem;

[ConfigSection(
    name: "Opt 加载修复",
    en: "When loading Opt in StreamingAssets, not only load those starting with A, but also those starting with the letter corresponding to the version",
    zh: "在 StreamingAssets 加载 Opt 时，不仅加载 A 开头的，也加载版本对应字母开头的",
    defaultOn: true)]
public static class OptionLoadFix
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(AppImage), "OptionMountRootPath", MethodType.Getter)]
    public static bool AppImageOptionMountRootPath(ref string __result)
    {
        __result = Application.streamingAssetsPath;

        return false;
    }
    
    // 如果 optDir 也是 A 开头的话，结果会重复，需要去重
    [HarmonyPostfix]
    [HarmonyPatch(typeof(DataManager), "GetDirs")]
    public static void DataManagerGetDirs(ref List<string> dirs)
    {
        dirs = dirs.Distinct().ToList();
    }
}
