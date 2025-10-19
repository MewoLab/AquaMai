using HarmonyLib;

using AquaMai.Config.Attributes;
using UnityEngine;

namespace AquaMai.Mods.GameSystem;

[ConfigSection(
    name: "耳机音量倍率",
    en: "Multiply 1P/2P headphone volume by a factor. Can decrease or increase, but not higher than original 20.",
    zh: "将 1P/2P 耳机音量乘以一个倍率，可以调低或调高，但不能高于原本的 20。")]
public static class HeadphoneVolumeMultiply
{
    [ConfigEntry]
    private static readonly float p1 = 1.0f;

    [ConfigEntry]
    private static readonly float p2 = 1.0f;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(CriAtomExPlayer), "SetAisacControl", [typeof(uint), typeof(float)])]
    public static void PreSetAisacControl(uint controlId, ref float value)
    {
        if (controlId == 2)
        {
            value *= p1;
            value = Mathf.Min(value, 1.0f);
        }
        else if (controlId == 3)
        {
            value *= p2;
            value = Mathf.Min(value, 1.0f);
        }
    }
}
