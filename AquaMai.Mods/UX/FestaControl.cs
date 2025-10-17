using AquaMai.Config.Attributes;
using AquaMai.Core.Attributes;
using HarmonyLib;
using Manager;
using Process;

namespace AquaMai.Mods.UX;

[ConfigSection(
    name: "Festa 开关",
    zh: "控制 “Festa” 模式 UI 的显示。如不开启，则不会更改设置，由 Event 或者服务器控制",
    en: "Control the display of “Festa” mode UI. If not enabled, the settings will not be changed, and Event or server will control it")]
[EnableGameVersion(26000)]
public static class FestaControl
{
    [ConfigEntry(
        name: "启用 Festa",
        zh: "是否显示 “Festa” 模式 UI",
        en: "Whether to display “Festa” mode UI")]
    public static readonly bool isFesta = false;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(FestaManager), "Initalize")]
    public static void Hook(FestaManager __instance)
    {
        Traverse.Create(__instance).Property<bool>("isOpenFesta").Value = isFesta;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GetPresentProcess), "CreateFestaBorderRewardListOpenFesta")]
    public static bool CreateFestaBorderRewardListOpenFesta()
    {
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(FestaManager), "CanAttendFesta")]
    public static bool CanAttendFesta(ref bool __result)
    {
        __result = false;
        return false;
    }
}