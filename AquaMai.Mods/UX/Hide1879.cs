using HarmonyLib;
using AquaMai.Config.Attributes;
using System;
using System.Diagnostics;
using System.Linq;
using MelonLoader;
using Manager;

namespace AquaMai.Mods.UX;

[ConfigSection(
    name: "隐藏乱码曲",
    en: "Hide glitch Xaleid◆scopiX in normal mode",
    zh: "在正常模式中，隐藏乱码曲 Xaleid◆scopiX",
    defaultOn: true
)]
public class Hide1879
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(NotesListManager), "CreateNormalNotesList")]
    public static void CreateNormalNotesList_Postfix()
    {
        try
        {
            if (GameManager.IsKaleidxScopeMode) return;
            var stackTrace = new StackTrace();
            var stackFrames = stackTrace.GetFrames();
            if (!stackFrames.Select(it => it.GetMethod().DeclaringType.Name).Contains("MusicSelectProcess"))
            {
                return;
            }

            var dm = DataManager.Instance;
            if (dm == null) return;
            var musicInfo = dm.GetMusic(011879);
            if (musicInfo == null) return;

            var instance = NotesListManager.Instance;
            if (instance == null) return;
            var notesList = instance.GetNotesList();
            if (notesList != null && notesList.ContainsKey(011879))
            {
                notesList.Remove(011879);
            }
        }
        catch (Exception ex)
        {
            MelonLogger.Error($"[Hide 1879] Error: {ex}");
        }
    }
}