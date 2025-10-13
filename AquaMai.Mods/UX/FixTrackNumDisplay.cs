using System;
using AquaMai.Config.Attributes;
using HarmonyLib;
using Manager;
using MelonLoader;
using Monitor;
using UI;
using UnityEngine;

namespace AquaMai.Mods.UX;

[ConfigSection(
    name: "两位数曲目修复",
    en: "Make track number in top right corner display two digits",
    zh: "让右上角的当前曲目数字可以显示两位数")]
public class FixTrackNumDisplay
{
    [ConfigEntry(
        name: "显示格式",
        en: "Track number display style: 0=Left-padded (_5), 1=Zero-padded (05), 2=Right-padded (5_)",
        zh: "曲目数字显示格式: 0=左侧补空格(_5), 1=左侧补零(05), 2=右侧补空格(5_)")]
    private static readonly int TrackNumDisplayStyle = 0; // Default 0

    private static Sprite[] customSprites;
    private static bool isInitialized = false;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CommonMonitor), "SetTrackCount")]
    public static void SetTrackCountPostfix(MultipleImage ____trackCountObject)
    {
        if (isInitialized) return;

        try {
            customSprites = new Sprite[3];
            var trackSprites = ____trackCountObject.MultiSprites; // 0: Blue, 1: Green, 2: Red

            for (int i = 0; i < 3; i++) {
                int w = trackSprites[i].texture.width;
                int h = trackSprites[i].texture.height;

                // 使用RenderTexture创建可读的原始贴图
                var renderTexture = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
                var previous = RenderTexture.active;
                RenderTexture.active = renderTexture;
                Graphics.Blit(trackSprites[i].texture, renderTexture);
                var ori_copy = new Texture2D(w, h, TextureFormat.RGBA32, false);
                ori_copy.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                ori_copy.Apply();
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(renderTexture);

                // 新建贴图，拓宽30像素
                Texture2D newTex = new Texture2D(w+30, h, ori_copy.format, false);
                newTex.filterMode = ori_copy.filterMode;
                // 从192像素处分割原始贴图, 复制到新贴图左右两侧
                Color[] leftColors = ori_copy.GetPixels(0, 0, 192, h);
                newTex.SetPixels(0, 0, 192, h, leftColors);
                Color[] rightColors = ori_copy.GetPixels(192, 0, w-192, h);
                newTex.SetPixels(192+30, 0, w-192, h, rightColors);
                // 将原始像素的182-192复制3次，填充新贴图中间的30像素空白
                Color[] repeatSection = ori_copy.GetPixels(182, 0, 10, h);
                newTex.SetPixels(192, 0, 10, h, repeatSection);
                newTex.SetPixels(202, 0, 10, h, repeatSection);
                newTex.SetPixels(212, 0, 10, h, repeatSection);

                // 应用修改
                newTex.Apply();
                customSprites[i] = Sprite.Create(newTex, new Rect(0, 0, w+30, h), new Vector2(0.5f, 0.5f));
                UnityEngine.Object.Destroy(ori_copy);
            }
            MelonLogger.Msg($"[FixTrackNumDisplay] Initialized");
            isInitialized = true;

        } catch (Exception e) {MelonLogger.Msg($"[FixTrackNumDisplay] {e}");}
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(CommonMonitor), "SetTrackCount")]
    public static bool SetTrackCountPrefix(uint currentTrackNum, uint maxTrackNum, 
                                            MultipleImage ____trackCountObject,
                                            SpriteCounter ____trackCountText,
                                            SpriteCounter ____trackDenominatortText,
                                            GameObject ____trackMaskImage,
                                            Color[] ____trackColor)
    {
        if (maxTrackNum < 10) return true;
        if (!isInitialized) return true;
        if (currentTrackNum < 1) return true;
        if (GameManager.IsFreedomMode) return true;

        try {
            // 在前两局进行调整
            if (currentTrackNum < 3) {
                // 右边增加30像素, 匹配新的贴图尺寸
                var rectTransform = ____trackCountObject.GetComponent<RectTransform>();
                Vector2 sizeDelta = rectTransform.sizeDelta;
                if (sizeDelta.x != 290) rectTransform.sizeDelta = new Vector2(290f, sizeDelta.y);
                // 显示位置增加到两位
                if (____trackCountText.FrameList.Count != 2) ____trackCountText.AddFormatFream();
                if (____trackDenominatortText.FrameList.Count != 2) ____trackDenominatortText.AddFormatFream();
                // 统一设置文字尺寸
                ____trackCountText.FrameList[0].DefaultScale = new Vector2(34, 40);
                ____trackCountText.FrameList[1].DefaultScale = new Vector2(34, 40);
                ____trackDenominatortText.FrameList[0].DefaultScale = new Vector2(34, 40);
                ____trackDenominatortText.FrameList[1].DefaultScale = new Vector2(34, 40);
                ____trackCountText.FrameList[0].Scale = 0.4f;
                ____trackCountText.FrameList[1].Scale = 0.4f;
                ____trackDenominatortText.FrameList[0].Scale = 0.3f;
                ____trackDenominatortText.FrameList[1].Scale = 0.3f;
            }

            var curStr = currentTrackNum.ToString();
            var maxStr = maxTrackNum.ToString();

            // 根据配置选择显示格式
            curStr = TrackNumDisplayStyle switch {
                0 => curStr.PadLeft(2),      //左侧空格补位
                1 => curStr.PadLeft(2, '0'), //左侧零补位
                2 => curStr.PadRight(2),     //右侧空格补位
                _ => curStr.PadLeft(2),      //默认 case 0
            };

            // 默认文字位置
            ____trackCountText.FrameList[0].RelativePosition = new Vector2(5, 0);
            ____trackCountText.FrameList[1].RelativePosition = new Vector2(-7, 0);
            ____trackDenominatortText.FrameList[0].RelativePosition = new Vector2(33, 0);
            ____trackDenominatortText.FrameList[1].RelativePosition = new Vector2(16, 0);

            // 数字1的位置需要额外调整，以保证视觉上与其它数字间距一致
            // currentTrackNum 第一位
            if (curStr[0] == '1' && curStr[1] != ' ')
                ____trackCountText.FrameList[0].RelativePosition = new Vector2(5+2, 0);
            // currentTrackNum 第二位
            if (curStr[1] == '1' && curStr[0] != ' ')
                ____trackCountText.FrameList[1].RelativePosition = new Vector2(-7-2, 0);
            // maxTrackNum 第一位
            if (maxStr[0] == '1')
                ____trackDenominatortText.FrameList[0].RelativePosition = new Vector2(33+2, 0);                
            // maxTrackNum 第二位
            if (maxStr[1] == '1')
                ____trackDenominatortText.FrameList[1].RelativePosition = new Vector2(16-2, 0);

            // 复制原版处理非自由模式的逻辑
            var trackColorID = 0;
            ____trackMaskImage.SetActive(value: false);
            ____trackCountText.ChangeText(curStr); // 使用自定义文本
            ____trackDenominatortText.ChangeText(maxStr);
            trackColorID = (maxTrackNum - currentTrackNum) switch
            {
                0u => 2, 
                1u => 1, 
                _ => 0, 
            };
            ____trackCountObject.sprite = customSprites[trackColorID]; // 使用自定义贴图
            ____trackCountText.SetColor(____trackColor[trackColorID]);
            ____trackDenominatortText.SetColor(____trackColor[trackColorID]);
            return false;
            
        } catch (Exception e) {
            MelonLogger.Msg($"[FixTrackNumDisplay] {e}");
            return true;
        }
    }
}
