using System.Collections.Generic;
using AquaMai.Config.Attributes;
using AquaMai.Config.Types;
using AquaMai.Core;
using AquaMai.Core.Helpers;
using AquaMai.Mods.Tweaks.TimeSaving;
using HarmonyLib;
using Mai2.Mai2Cue;
using MAI2.Util;
using MAI2System;
using Main;
using Manager;
using MelonLoader;
using Process;

namespace AquaMai.Mods.UX;

[ConfigSection(
    name: "一键登录与登出",
    en: "One key to proceed to music select (during entry) or end current PC (during music select).",
    zh: "一键跳过登录过程直接进入选歌界面，或在选歌界面直接结束本局游戏")]
public class OneKeyEntryEnd
{
    [ConfigEntry(name: "按键")]
    public static readonly KeyCodeOrName key = KeyCodeOrName.Service;

    [ConfigEntry(name: "长按")]
    public static readonly bool longPress = true;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameMainObject), "Update")]
    public static void OnGameMainObjectUpdate()
    {
        if (!KeyListener.GetKeyDownOrLongPress(key, longPress)) return;
        MelonLogger.Msg("[QuickSkip] Activated");
        DoQuickSkip();
    }

    public static void DoQuickSkip()
    {
        var traverse = Traverse.Create(SharedInstances.ProcessDataContainer.processManager);
        var processList = traverse.Field("_processList").GetValue<LinkedList<ProcessManager.ProcessControle>>();

        ProcessBase processToRelease = null;

        foreach (ProcessManager.ProcessControle process in processList)
        {
#if DEBUG
            MelonLogger.Msg($"[QuickSkip] {process.Process}");
#endif
            switch (process.Process.ToString())
            {
                // After login
                case "Process.ModeSelect.ModeSelectProcess":
                case "Process.LoginBonus.LoginBonusProcess":
                case "Process.RegionalSelectProcess":
                case "Process.CharacterSelectProcess":
                // Typo in Assembly-CSharp
                case "Process.CharacterSelectProces":
                case "Process.TicketSelect.TicketSelectProcess":
                    GameManager.IsNormalMode = true;
                    processToRelease = process.Process;
                    break;

                case "Process.MusicSelectProcess":
                    // Skip to save
                    SoundManager.PreviewEnd();
                    SoundManager.PlayBGM(Cue.BGM_COLLECTION, 2);
                    if (ConfigLoader.Config.GetSectionState(typeof(ExitToSave)).Enabled)
                    {
                        SharedInstances.ProcessDataContainer.processManager.AddProcess(new FadeProcess(SharedInstances.ProcessDataContainer, process.Process,
                            new DataSaveProcess(SharedInstances.ProcessDataContainer)));
                        // Fix crash
                        SharedInstances.ProcessDataContainer.processManager.PrepareTimer(0, 0, false, null, false);
                    }
                    else
                    {
                        SharedInstances.ProcessDataContainer.processManager.AddProcess(new FadeProcess(SharedInstances.ProcessDataContainer, process.Process,
                            new UnlockMusicProcess(SharedInstances.ProcessDataContainer)));
                    }
                    break;
            }
        }

        if (processToRelease != null)
        {
            GameManager.SetMaxTrack();
            SharedInstances.ProcessDataContainer.processManager.AddProcess(new FadeProcess(SharedInstances.ProcessDataContainer, processToRelease,
                new MusicSelectProcess(SharedInstances.ProcessDataContainer)));
        }
    }
}