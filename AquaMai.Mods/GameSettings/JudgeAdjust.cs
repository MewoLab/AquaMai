﻿using System.Threading;
using AquaMai.Config.Attributes;
using HarmonyLib;
using IO;
using Manager.UserDatas;

namespace AquaMai.Mods.GameSettings;

[ConfigSection(
    en: "Globally adjust A/B judgment (unit same as in-game options) or increase touch delay.",
    zh: "全局调整 A/B 判（单位和游戏里一样）或增加触摸延迟")]
public class JudgeAdjust
{
    [ConfigEntry(
        en: "Adjust A judgment.",
        zh: "调整 A 判")]
    private static readonly double a = 0;

    [ConfigEntry(
        en: "Adjust B judgment.",
        zh: "调整 B 判")]
    private static readonly double b = 0;

    [ConfigEntry(
        en: "Increase touch delay.",
        zh: "增加触摸延迟（不建议使用）")]
    private static readonly uint touchDelay = 0;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UserOption), "GetAdjustMSec")]
    public static void GetAdjustMSec(ref float __result)
    {
        __result += (float)(a * 16.666666d);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UserOption), "GetJudgeTimingFrame")]
    public static void GetJudgeTimingFrame(ref float __result)
    {
        __result += (float)b;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NewTouchPanel), "Recv")]
    public static void NewTouchPanelRecv()
    {
        if (touchDelay <= 0) return;
        Thread.Sleep((int)touchDelay);
    }
}
