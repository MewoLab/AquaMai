using AquaMai.Config.Attributes;
using AquaMai.Core.Attributes;
using HarmonyLib;
using Monitor;

namespace AquaMai.Mods.Fancy;

[ConfigSection(
    en: "Set the \"CREDIT(S)\" string displayed at the middle and top-right corner of the screen.",
    zh: "自定义中间和右上角的 \"CREDIT(S)\"（可用点数）文本")]
public class CustomCreditsString
{
    [ConfigEntry(
        en: "Custom string to replace the \"CREDIT(S)\". Empty for a blank string.",
        zh: "用于替换 \"CREDIT(S)\" 的自定义文本，留空则为空白"
    )]
    private static readonly string creditsString = "";

    [ConfigEntry(
        en: "Hide the number of credits after the \"CREDITS(S)\" string.",
        zh: "隐藏 \"CREDIT(S)\" 后的可用点数数量"
    )]
    private static readonly bool hideCreditsNumber = true;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(CreditController), "Initialize")]
    public static void Initialize(ref string ____freePlayText, ref string ___CreditText)
    {
        ____freePlayText = creditsString;
        ___CreditText = hideCreditsNumber
            ? creditsString
            : creditsString.TrimEnd() + "  "; // Original: "CREDIT(S)  "
    }

    [EnableIf(nameof(hideCreditsNumber))]
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CreditController), "SetCredits")]
    public static void PostSetCredits(TMPro.TextMeshProUGUI ____creditText, string ___CreditText)
    {
        ____creditText.text = ___CreditText.TrimEnd();
    }
}
