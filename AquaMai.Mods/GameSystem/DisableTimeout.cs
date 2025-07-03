using System.Diagnostics;
using System.Linq;
using AquaMai.Config.Attributes;
using AquaMai.Core.Attributes;
using HarmonyLib;
using Manager;
using AquaMai.Core.Environment;
using Monitor;
using Process;
using Process.Entry.State;
using Process.ModeSelect;
using UnityEngine.Playables;

namespace AquaMai.Mods.GameSystem;

[ConfigSection(
    en: "Disable timers (hidden and set to 65535 seconds).",
    zh: "去除并隐藏游戏中的倒计时")]
public class DisableTimeout
{
    [ConfigEntry(
        en: "Disable game start timer.",
        zh: "也移除刷卡和选择模式界面的倒计时")]
    private static readonly bool inGameStart = true;

    [ConfigEntry(
        en: "Hide the timer display.",
        zh: "隐藏计时器")]
    private static readonly bool hideTimer = true;

    private static bool CheckInGameStart()
    {
        if (inGameStart) return false;
        var stackTrace = new StackTrace();
        var stackFrames = stackTrace.GetFrames();
        var names = stackFrames.Select(it => it.GetMethod().DeclaringType.Name).ToArray();
# if DEBUG
        MelonLogger.Msg(names.Join());
# endif
        return names.Contains("EntryProcess") || names.Contains("ModeSelectProcess");
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TimerController), "PrepareTimer")]
    public static void PrePrepareTimer(ref int second)
    {
        if (CheckInGameStart()) return;
        second = 65535;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TimerController), "PrepareTimer")]
    public static void PostPrepareTimer(TimerController __instance)
    {
        if (hideTimer) return;
        if (CheckInGameStart()) return;
        Traverse.Create(__instance).Property<bool>("IsInfinity").Value = true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(CommonTimer), "SetVisible")]
    public static void CommonTimerSetVisible(ref bool isVisible)
    {
        if (CheckInGameStart()) return;
        if (!hideTimer) return;
        isVisible = false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(EntryProcess), "DecrementTimerSecond")]
    [EnableIf(nameof(inGameStart))]
    public static bool EntryProcessDecrementTimerSecond(ContextEntry ____context)
    {
        SoundManager.PlaySE(Mai2.Mai2Cue.Cue.SE_SYS_SKIP, 0);
        ____context.SetState(StateType.DoneEntry);
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ModeSelectProcess), "UpdateInput")]
    [EnableIf(nameof(inGameStart))]
    public static bool ModeSelectProcessUpdateInput(ModeSelectProcess __instance)
    {
        if (!InputManager.GetMonitorButtonDown(InputManager.ButtonSetting.Button05)) return true;
        __instance.TimeSkipButtonAnim(InputManager.ButtonSetting.Button05);
        SoundManager.PlaySE(Mai2.Mai2Cue.Cue.SE_SYS_SKIP, 0);
        Traverse.Create(__instance).Method("TimeUp").GetValue();
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(PhotoEditProcess), "MainMenuUpdate")]
    public static void PhotoEditProcess(PhotoEditMonitor[] ____monitors, PhotoEditProcess __instance)
    {
        if (!InputManager.GetMonitorButtonDown(InputManager.ButtonSetting.Button04)) return;
        SoundManager.PlaySE(Mai2.Mai2Cue.Cue.SE_SYS_SKIP, 0);
        for (var i = 0; i < 2; i++)
        {
            try
            {
                ____monitors[i].SetButtonPressed(InputManager.ButtonSetting.Button04);
            }
            catch
            {
                // ignored
            }
        }

        Traverse.Create(__instance).Method("OnTimeUp").GetValue();
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
}
