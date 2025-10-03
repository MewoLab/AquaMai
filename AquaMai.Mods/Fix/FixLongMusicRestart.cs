using System.Diagnostics;
using System.Linq;
using AquaMai.Config.Attributes;
using AquaMai.Core.Attributes;
using HarmonyLib;
using MAI2.Util;
using Manager;
using MelonLoader;
using Process;

namespace AquaMai.Mods.Fix;

[EnableGameVersion(25500)]
[ConfigSection(name: "Long Music 重开修复",
    zh: "修复 Long Music 重开时重复扣除 Track 数",
    exampleHidden: true, defaultOn: true)]
public class FixLongMusicRestart
{
    private static bool isThisGameRestart = false;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameProcess), "OnStart")]
    public static void PreGameProcessStart()
    {
        isThisGameRestart = Singleton<GamePlayManager>.Instance.IsQuickRetry();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameProcess), "OnStart")]
    public static void PostGameProcessStart()
    {
        isThisGameRestart = false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(DataManager), nameof(DataManager.IsLong))]
    public static bool PatchIsLong(ref bool __result)
    {
        if (!isThisGameRestart)
        {
            return true;
        }
        var stackTrace = new StackTrace();
        var stackFrames = stackTrace.GetFrames();
        if (stackFrames.All(it => it.GetMethod().DeclaringType.Name != "GameProcess"))
        {
#if DEBUG
            MelonLogger.Msg("[FixLongMusicRestart] IsLong called outside GameProcess, returning true.");
#endif
            return true;
        }
        __result = false;
        return false;
    }
}