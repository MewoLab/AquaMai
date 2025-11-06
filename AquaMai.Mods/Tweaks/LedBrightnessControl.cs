using AquaMai.Config.Attributes;
using AquaMai.Core.Attributes;
using HarmonyLib;
using Mecha;
using UnityEngine;

namespace AquaMai.Mods.Tweaks;

[ConfigSection(
    name: "LED亮度控制",
    en: "Control LED brightness for button and cabinet lights",
    zh: "控制按键和框体灯的亮度")]
[EnableImplicitlyIf(nameof(shouldEnableImplicitly))]
public class LedBrightnessControl
{
    public static bool shouldEnableImplicitly = false;
    [ConfigEntry(
        name: "1P按键亮度",
        en: "Button Brightness 1P (0.0 - 1.0)",
        zh: "(0.0 - 1.0)")]
    public static float button1p = 1.0f;

    [ConfigEntry(
        name: "2P按键亮度",
        en: "Button Brightness 2P (0.0 - 1.0)",
        zh: "(0.0 - 1.0)")]
    public static float button2p = 1.0f;

    [ConfigEntry(
        name: "1P框体灯亮度",
        en: "Cabinet Brightness 1P (0.0 - 1.0)",
        zh: "(0.0 - 1.0)")]
    public static float cabinet1p = 1.0f;

    [ConfigEntry(
        name: "2P框体灯亮度",
        en: "Cabinet Brightness 2P (0.0 - 1.0)",
        zh: "(0.0 - 1.0)")]
    public static float cabinet2p = 1.0f;

    private static Color32 ApplyBrightness(Color32 originalColor, float brightness)
    {
        brightness = Mathf.Clamp01(brightness);

        var newColor = new Color32(
            (byte)(originalColor.r * brightness),
            (byte)(originalColor.g * brightness),
            (byte)(originalColor.b * brightness),
            originalColor.a
        );

        return newColor;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Bd15070_4IF), "_setColor")]
    public static bool SetColorPrefix(byte ledPos, ref Color32 color, Bd15070_4IF.InitParam ____initParam)
    {
        float brightness;
        if (ledPos > 7)
        {
            brightness = ____initParam.index == 0 ? cabinet1p : cabinet2p;
        }
        else
        {
            brightness = ____initParam.index == 0 ? button1p : button2p;
        }

        color = ApplyBrightness(color, brightness);
        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Bd15070_4IF), "_setColorMulti")]
    public static bool SetColorMultiPrefix(ref Color32 color, Bd15070_4IF.InitParam ____initParam)
    {
        color = ApplyBrightness(color, ____initParam.index == 0 ? button1p : button2p);
        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Bd15070_4IF), "_setColorMultiFade")]
    public static bool SetColorMultiFadePrefix(ref Color32 color, Bd15070_4IF.InitParam ____initParam)
    {
        color = ApplyBrightness(color, ____initParam.index == 0 ? button1p : button2p);
        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Bd15070_4IF), "_setColorMultiFet")]
    public static bool SetColorMultiFetPrefix(ref Color32 color, Bd15070_4IF.InitParam ____initParam)
    {
        color = ApplyBrightness(color, ____initParam.index == 0 ? button1p : button2p);
        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Bd15070_4IF), "_setColorFet")]
    public static bool SetColorFetPrefix(byte ledPos, ref byte color, Bd15070_4IF.InitParam ____initParam)
    {
        float brightness;
        // 机身 LED - 8
        // 画面外周 - 9
        // 侧盖 - 10
        if (ledPos > 7)
        {
            brightness = ____initParam.index == 0 ? cabinet1p : cabinet2p;
        }
        else
        {
            brightness = ____initParam.index == 0 ? button1p : button2p;
        }

        color = (byte)(color * brightness);
        return true;
    }
}