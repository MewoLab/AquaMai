using System.Diagnostics;
using System.Linq;
using AquaMai.Config.Attributes;
using AquaMai.Core.Attributes;
using HarmonyLib;
using Manager;
using MelonLoader;
using Monitor;
using Process;
using Process.Entry.State;
using Process.ModeSelect;
using UnityEngine.Playables;

namespace AquaMai.Mods.GameSystem;

[ConfigSection(
    name: "去除倒计时",
    en: "Disable timers (hidden and set to 65535 seconds).",
    zh: "去除并隐藏游戏中的倒计时")]
public class DisableTimeout
{
    [ConfigEntry(
        name: "游戏开始倒计时",
        en: "Disable game start timer.",
        zh: "也移除刷卡和选择模式界面的倒计时")]
    private static readonly bool inGameStart = true;

    [ConfigEntry(
        name: "照片编辑倒计时",
        en: "Disable timer in PhotoEditProcess, not recommended.",
        zh: "也移除 可以看一看游戏成绩哦 界面的倒计时，不推荐启用，会导致无法点击上传照片按钮")]
    private static readonly bool inPhotoEditProcess = false;

    [ConfigEntry(
        name: "隐藏计时器",
        en: "Hide the timer display.")]
    private static readonly bool hideTimer = true;

    private static bool ShouldNotEnable()
    {
        if (inGameStart && inPhotoEditProcess) return false;
        var stackTrace = new StackTrace();
        var stackFrames = stackTrace.GetFrames();
        var names = stackFrames.Select(it => it.GetMethod().DeclaringType.Name).ToArray();
# if DEBUG
        MelonLogger.Msg(names.Join());
# endif
        if (!inGameStart && (names.Contains("EntryProcess") || names.Contains("ModeSelectProcess"))) return true;
        if (!inPhotoEditProcess && names.Contains("PhotoEditProcess")) return true;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TimerController), "PrepareTimer")]
    public static void PrePrepareTimer(ref int second)
    {
        if (ShouldNotEnable()) return;
        second = 65535;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TimerController), "PrepareTimer")]
    public static void PostPrepareTimer(TimerController __instance)
    {
        if (hideTimer) return;
        if (ShouldNotEnable()) return;
        Traverse.Create(__instance).Property<bool>("IsInfinity").Value = true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(CommonTimer), "SetVisible")]
    public static void CommonTimerSetVisible(ref bool isVisible)
    {
        if (ShouldNotEnable()) return;
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
    [EnableIf(nameof(inPhotoEditProcess))]
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