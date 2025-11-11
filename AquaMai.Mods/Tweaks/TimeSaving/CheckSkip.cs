using AquaMai.Config.Attributes;
using HarmonyLib;
using Process;

namespace AquaMai.Mods.Tweaks.TimeSaving;

[ConfigSection(
    name: "跳过启动检查",
    en: "Skip check to speed up the game boot process.",
    zh: """
        在自检界面，有一些检查需要等待检查完毕后，才会进入下一阶段
        开了以下选项之后会将检查过程丢入后台进行，但可能存在部分副作用
        """)]
public class CheckSkip
{
    [ConfigEntry(
        name: "跳过摄像头检查",
        en: "Skip camera check.",
        zh: """
            若在后台检查完毕前过早启动摄像头会导致摄像头临时黑屏
            """)]
    private static readonly bool skipStartupCameraCheck = false;

    [ConfigEntry(
        name: "跳过启动网络检查",
        en: "Skip network check.",
        zh: """
            若在后台检查完毕前过早进入会进入临时灰网
            """)]
    private static readonly bool skipPowerOnNetworkCheck = false;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(StartupProcess), "OnUpdate")]
    public static void PreStartupUpdate(ref byte ____state)
    {
        if (skipStartupCameraCheck && ____state == 2)
        {
            ____state = 3;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PowerOnProcess), "OnUpdate")]
    public static void PrePowerOnUpdate(ref byte ____state)
    {
        if (skipPowerOnNetworkCheck && ____state == 6)
        {
            ____state = 7;
        }
    }
}
