using AquaMai.Config.Attributes;
using HarmonyLib;
using IO;
using Mecha;
using AquaMai.Core.Environment;
using Monitor;
using Process;

namespace AquaMai.Mods.UX;

[ConfigSection(
    en: "Disable button LED when not playing",
    zh: """
        在游戏闲置时关闭外键和框体的灯光
        “一闪一闪的 闪的我心发慌”
        """)]
public static class DisableLightOutGame
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Bd15070_4IF), nameof(Bd15070_4IF.SetColorFetAutoFade))]
    public static bool SetColorFetAutoFadePrefix()
    {
#if DEBUG
        MelonLogger.Msg("[DisableLightOutGame] 拦截 Bd15070_4IF.SetColorFetAutoFade");
#endif
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(AdvDemoMonitor), "BeatUpdate")]
    public static bool BeatUpdate()
    {
#if DEBUG
        MelonLogger.Msg("[DisableLightOutGame] 拦截 AdvDemoMonitor.BeatUpdate");
#endif
        return false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(AdvDemoProcess), "OnStart")]
    public static void AdvDemoProcessStart()
    {
        MechaManager.SetAllCuOff();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(AdvertiseProcess), "OnStart")]
    public static void AdvertiseProcessStart()
    {
        MechaManager.SetAllCuOff();
    }
}
