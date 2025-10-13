using System.Reflection;
using AquaMai.Config.Attributes;
using AquaMai.Core.Attributes;
using HarmonyLib;
using Manager;
using Monitor;
using Process;
using Process.Entry.State;
using Process.ModeSelect;
using UnityEngine.Playables;
using EnableConditionOperator = AquaMai.Core.Attributes.EnableConditionOperator;

namespace AquaMai.Mods.GameSystem;

[ConfigSection(
    name: "去除倒计时",
    en: "Disable timers (hidden and set to 65535 seconds).",
    zh: "去除并隐藏游戏中的倒计时")]
public class DisableTimeout
{
    [ConfigEntry(
        name: "移除续关倒计时",
        en: "Disable timer in ContinueProcess")]
    private static readonly bool inContinueProcess = false;

    [ConfigEntry(
        name: "移除开始倒计时",
        en: "Disable game start timer.",
        zh: "也移除刷卡和选择模式界面的倒计时")]
    private static readonly bool inGameStart = true;

    [ConfigEntry(
        name: "隐藏计时器",
        en: "Hide the timer display.")]
    private static readonly bool hideTimer = true;

    [ConfigEntry(
        name: "显示无穷符号",
        zh: "仅在 隐藏计时器 关闭时显示",
        en: "Show infinity symbol")]
    private static readonly bool showInfinity = true;

    [ConfigEntry(
        name: "快速跳过",
        zh: "在登录和选择模式界面，按一下跳过按钮就可以立即跳过",
        en: "Quickly skip entry and mode select")]
    private static readonly bool instanceSkip = false;

    private static bool shouldNotEnable = false;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TimerController), "PrepareTimer")]
    [EnableIf(nameof(hideTimer), EnableConditionOperator.Equal, false)]
    public static void PostPrepareTimer(TimerController __instance)
    {
        if (!showInfinity) return;
        if (shouldNotEnable) return;
        Traverse.Create(__instance).Property<bool>("IsInfinity").Value = true;
    }

    private static MethodBase _updateFreedomModeCounter = AccessTools.Method(typeof(TimerController), "UpdateFreedomModeCounter");

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TimerController), nameof(TimerController.UpdateTimer))]
    public static bool UpdateTimer(TimerController __instance, int ____countDownSecond, bool ____isTimeCounting)
    {
        if (shouldNotEnable) return true;
        if (____countDownSecond <= 0) return true;
        if (!____isTimeCounting) return true;
        if (GameManager.IsFreedomMode && GameManager.IsFreedomCountDown && !GameManager.IsFreedomTimerPause)
        {
            _updateFreedomModeCounter.Invoke(__instance, []);
        }
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(CommonTimer), "SetVisible")]
    [EnableIf(nameof(hideTimer))]
    public static void CommonTimerSetVisible(ref bool isVisible)
    {
        if (shouldNotEnable) return;
        isVisible = false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(EntryProcess), "DecrementTimerSecond")]
    [EnableIf(nameof(instanceSkip))]
    public static bool EntryProcessDecrementTimerSecond(ContextEntry ____context)
    {
        SoundManager.PlaySE(Mai2.Mai2Cue.Cue.SE_SYS_SKIP, 0);
        ____context.SetState(StateType.DoneEntry);
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ModeSelectProcess), "UpdateInput")]
    [EnableIf(nameof(instanceSkip))]
    public static bool ModeSelectProcessUpdateInput(ModeSelectProcess __instance)
    {
        if (!InputManager.GetMonitorButtonDown(InputManager.ButtonSetting.Button05)) return true;
        __instance.TimeSkipButtonAnim(InputManager.ButtonSetting.Button05);
        SoundManager.PlaySE(Mai2.Mai2Cue.Cue.SE_SYS_SKIP, 0);
        Traverse.Create(__instance).Method("TimeUp").GetValue();
        return false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ContinueMonitor), "Initialize")]
    public static void ContinueMonitorInitialize(PlayableDirector ____director)
    {
        if (____director != null)
        {
            ____director.extrapolationMode = DirectorWrapMode.Loop;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ContinueMonitor), "PlayContinue")]
    public static void ContinueMonitorPlayContinue(PlayableDirector ____director)
    {
        if (____director != null)
        {
            ____director.extrapolationMode = DirectorWrapMode.Hold;
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ContinueProcess), "OnStart")]
    [EnableIf(nameof(inContinueProcess), EnableConditionOperator.Equal, false)]
    public static void ContinueProcessOnStart()
    {
        shouldNotEnable = true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ContinueProcess), "OnRelease")]
    [EnableIf(nameof(inContinueProcess), EnableConditionOperator.Equal, false)]
    public static void ContinueProcessOnRelease()
    {
        shouldNotEnable = false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(EntryProcess), "OnStart")]
    [EnableIf(nameof(inGameStart), EnableConditionOperator.Equal, false)]
    public static void EntryProcessOnStart()
    {
        shouldNotEnable = true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(EntryProcess), "OnRelease")]
    [EnableIf(nameof(inGameStart), EnableConditionOperator.Equal, false)]
    public static void EntryProcessOnRelease()
    {
        shouldNotEnable = false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ModeSelectProcess), "OnStart")]
    [EnableIf(nameof(inGameStart), EnableConditionOperator.Equal, false)]
    public static void ModeSelectProcessOnStart()
    {
        shouldNotEnable = true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ModeSelectProcess), "OnRelease")]
    [EnableIf(nameof(inGameStart), EnableConditionOperator.Equal, false)]
    public static void ModeSelectProcessOnRelease()
    {
        shouldNotEnable = false;
    }
}