using AquaMai.Config.Attributes;
using AquaMai.Core.Attributes;
using HarmonyLib;
using Manager;

namespace AquaMai.Mods.Utils;

[ConfigSection("自由模式时间修改", "Freedom Timer Modification")]
public static class FreedomTimer
{
    [ConfigEntry("秒数")]
    public static long seconds = 600;

    [ConfigEntry("无限时间")]
    public static bool infinityTime = false;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.GetFreedomStartTime))]
    public static bool GetFreedomStartTime(ref long __result)
    {
        __result = seconds * 1000;
        return false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.IsFreedomTimerPause), MethodType.Getter)]
    [EnableIf(nameof(infinityTime))]
    public static void IsFreedomTimerPause(ref bool __result)
    {
        __result = true;
    }
}