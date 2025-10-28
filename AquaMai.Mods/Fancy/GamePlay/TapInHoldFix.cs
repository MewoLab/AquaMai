using AquaMai.Config.Attributes;
using HarmonyLib;
using Monitor;

namespace AquaMai.Mods.Fancy.GamePlay;

[ConfigSection("Hold 夹 Tap",
    zh: "让普通模式也能像宴会场一样，在 Hold 音符期间正常判定其他音符，兼容特殊设计谱面（如飞行志）\n实验性功能，未测试对其他谱面的影响",
    en: "Allow normal mode to judge Tap notes normally during Hold notes, like in UTAGE. Compatible with specially designed charts\nExperimental feature, untested on other charts")]
public class TapInHoldFix
{
    [HarmonyPatch(typeof(NoteBase), "IsJudgeNote")]
    [HarmonyPrefix]
    public static bool IsJudgeNote(NoteBase __instance, ref bool __result)
    {
        __result = !__instance.IsEnd();
        return false;
    }
}