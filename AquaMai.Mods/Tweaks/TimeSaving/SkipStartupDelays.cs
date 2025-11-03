using System.Diagnostics;
using AquaMai.Config.Attributes;
using HarmonyLib;
using Process;

namespace AquaMai.Mods.Tweaks.TimeSaving;

[ConfigSection(
    name: "跳过启动延迟",
    en: "Skip useless 2s delays to speed up the game boot process.",
    zh: """
        在自检界面，每个屏幕结束的时候都会等两秒才进入下一个屏幕，很浪费时间
        开了这个选项之后就不会等了
        """,
    defaultOn: true)]
public class SkipStartupDelays
{
    [ConfigEntry(
        name: "跳过摄像头检查",
        en: "Skip camera check to speed up the game boost process.",
        zh: """
            在自检界面，需要等待摄像头彻底检查完毕后才能进入
            开了这个会将检查过程丢入后台进行
            """)]
    private static readonly bool skipStartupCameraCheck = false;

    [ConfigEntry(
        name: "跳过启动网络检查",
        en: "Skip network check to speed up the game boost process.",
        zh: """
            在自检界面，需要等待网络彻底检查完毕后才能进入
            开了这个会将检查过程丢入后台进行
            """)]
    private static readonly bool skipPowerOnNetworkCheck = false;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(PowerOnProcess), "OnStart")]
    public static void PrePowerOnStart(ref float ____waitTime)
    {
        ____waitTime = 0f;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(StartupProcess), "OnUpdate")]
    public static void PreStartupUpdate(byte ____state, ref Stopwatch ___timer)
    {
        if (skipStartupCameraCheck && ____state == 2)
        {
            ____state = 3;
        }
        else if (____state == 8)
        {
            Traverse.Create(___timer).Field("elapsed").SetValue(2 * 10000000L);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PowerOnProcess), "OnUpdate")]
    public static void PreStartupUpdate(ref byte ____state)
    {
        if (skipPowerOnNetworkCheck && ____state == 6)
        {
            ____state = 7;
        }
    }
}
