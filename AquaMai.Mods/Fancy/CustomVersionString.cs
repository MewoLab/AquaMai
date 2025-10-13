using AquaMai.Config.Attributes;
using HarmonyLib;

namespace AquaMai.Mods.Fancy;

[ConfigSection(
    name: "自定义版本文本",
    en: "Set the version string displayed at the top-right corner of the screen.",
    zh: "把右上角的版本更改为自定义文本")]
public class CustomVersionString
{
    [ConfigEntry(name: "版本文本")]
    private static readonly string versionString = "";

    /*
     * Patch displayVersionString Property Getter
     */
    [HarmonyPrefix]
    [HarmonyPatch(typeof(MAI2System.Config), "displayVersionString", MethodType.Getter)]
    public static bool GetDisplayVersionString(ref string __result)
    {
        if (string.IsNullOrEmpty(versionString))
        {
            return true;
        }

        __result = versionString;
        // Return false to block the original method
        return false;
    }
}
