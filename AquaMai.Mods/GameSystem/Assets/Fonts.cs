using System.Collections.Generic;
using System.Linq;
using AquaMai.Config.Attributes;
using AquaMai.Core.Helpers;
using HarmonyLib;
using MelonLoader;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace AquaMai.Mods.GameSystem.Assets;

[ConfigSection(
    name: "自定义字体",
    en: "Use custom font(s) as fallback or fully replace the original game font.",
    zh: "使用自定义字体作为回退（解决中文字形缺失问题），或完全替换游戏原字体",
    defaultOn: true)]
public class Fonts
{
    [ConfigEntry(
        name: "字体路径",
        en: """
            Font path(s).
            Use semicolon to separate multiple paths for a fallback chain.
            Microsoft YaHei Bold by default.
            """,
        zh: """
            使用分号分隔多个路径以构成 Fallback 链
            默认为微软雅黑 Bold
            """)]
    private static readonly string paths = "%SYSTEMROOT%/Fonts/msyhbd.ttc";

    [ConfigEntry(
        name: "作为回退字体",
        en: "Add custom font(s) as fallback, use original game font when possible.",
        zh: "将自定义字体作为游戏原字体的回退，尽可能使用游戏原字体")]
    private static readonly bool addAsFallback = true;

    private static List<TMP_FontAsset> fontAssets = [];
    private static readonly List<TMP_FontAsset> processedFonts = [];

    public static void OnBeforePatch()
    {
        var paths = Fonts.paths
            .Split(';')
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(FileSystem.ResolvePath);
        var fonts = paths
            .Select(p =>
            {
                var font = new Font(p);
                if (font == null)
                {
                    MelonLogger.Warning($"[Fonts] Font not found: {p}");
                }
                return font;
            })
            .Where(f => f != null);
        fontAssets = fonts
            .Select(f => TMP_FontAsset.CreateFontAsset(f, 90, 9, GlyphRenderMode.SDFAA, 8192, 8192))
            .ToList();
        
        fontAssets.ForEach(f => f.ReadFontAssetDefinition());

        if (fontAssets.Count == 0)
        {
            MelonLogger.Warning("[Fonts] No font loaded.");
        }
        // else if (addAsFallback)
        // {
        //     fallbackFontAssets = fontAssets;
        // }
        // else
        // {
        //     replacementFontAsset = fontAssets[0];
        //     fallbackFontAssets = fontAssets.Skip(1).ToList();
        // }
    }

    [HarmonyPatch(typeof(TextMeshProUGUI), "Awake")]
    [HarmonyPostfix]
    public static void PostAwake(TextMeshProUGUI __instance)
    {
        if (fontAssets.Count == 0) return;
        if (processedFonts.Contains(__instance.font)) return;

        if (!addAsFallback)
        {
            __instance.font.ClearFontAssetData();
        }
        if (fontAssets.Count > 0)
        {
            ProcessFallback(__instance);
        }

        processedFonts.Add(__instance.font);
    }

//     private static void ProcessReplacement(TextMeshProUGUI __instance)
//     {
// # if DEBUG
//         MelonLogger.Msg($"{__instance.font.name} {__instance.text}");
// # endif
//
//         var materialOrigin = __instance.fontMaterial;
//         __instance.font = replacementFontAsset;
//
// # if DEBUG
//         MelonLogger.Msg($"shaderKeywords {materialOrigin.shaderKeywords.Join()} {__instance.fontMaterial.shaderKeywords.Join()}");
// # endif
//         // if (materialOrigin != null)
//         // {
//         //     materialOrigin.SetTexture(ShaderUtilities.ID_MainTex, replacementFontAsset.atlasTexture);
//         //     __instance.fontMaterial = materialOrigin;
//         // }
//     }

    private static void ProcessFallback(TextMeshProUGUI __instance)
    {
        foreach (var fontAsset in fontAssets)
        {
            __instance.font.fallbackFontAssetTable.Add(fontAsset);
        }
    }
}
