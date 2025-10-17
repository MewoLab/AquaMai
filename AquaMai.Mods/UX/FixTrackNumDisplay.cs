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
    en: "Make track number in top right corner display two digits (in Normal mode)",
    zh: "让右上角的当前曲目数字可以显示两位数（在普通模式中）")]
public class FixTrackNumDisplay
{
    private static Sprite[] customSprites;
    private static bool isInitialized = false;

    // 记录原始数据，以便恢复到原版显示
    private static Sprite[] originalSprites;
    private static Vector2 originalSizeDelta;
    private static Vector2 originalTextDefaultScale1;
    private static Vector2 originalTextDefaultScale2;
    private static float originalTextScale1;
    private static float originalTextScale2;
    private static Vector2 originalTextPos1;
    private static Vector2 originalTextPos2;


    [HarmonyPostfix]
    [HarmonyPatch(typeof(CommonMonitor), "SetTrackCount")]
    public static void SetTrackCountPostfix(uint maxTrackNum,
                                            MultipleImage ____trackCountObject,
                                            SpriteCounter ____trackCountText,
                                            SpriteCounter ____trackDenominatortText)
    {
        if (isInitialized) return;

        try {
            // 记录原始贴图尺寸
            var rectTransform = ____trackCountObject.GetComponent<RectTransform>();
            originalSizeDelta = rectTransform.sizeDelta;
            // 记录原始文字尺寸
            originalTextDefaultScale1 = ____trackCountText.FrameList[0].DefaultScale;
            originalTextDefaultScale2 = ____trackDenominatortText.FrameList[0].DefaultScale;
            originalTextScale1 = ____trackCountText.FrameList[0].Scale;
            originalTextScale2 = ____trackDenominatortText.FrameList[0].Scale;
            // 记录原始文字位置
            originalTextPos1 = ____trackCountText.FrameList[0].RelativePosition;
            originalTextPos2 = ____trackDenominatortText.FrameList[0].RelativePosition;


            customSprites = new Sprite[3];
            var trackSprites = ____trackCountObject.MultiSprites; // 0: Blue, 1: Green, 2: Red

            // 记录原始贴图
            originalSprites = new Sprite[3];
            for (int i = 0; i < 3; i++) {
                originalSprites[i] = trackSprites[i];
            }

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
        if (!isInitialized) return true;
        if (currentTrackNum < 1) return true;
        if (!GameManager.IsNormalMode) return true; // 仅在普通模式下生效



        // 当 currentTrackNum 和 maxTrackNum 都 <10 时，恢复到原版显示
        if (currentTrackNum < 10 && maxTrackNum < 10)
        {
            // 先做一个基础判断，看看是否已经是原本的样子
            if (____trackCountText.FrameList.Count == 1 && ____trackDenominatortText.FrameList.Count == 1) return true;

            try
            {
                // 恢复原版贴图
                var trackSprites = ____trackCountObject.MultiSprites;
                for (int i = 0; i < 3; i++)
                {
                    trackSprites[i] = originalSprites[i];
                }
                // 恢复原版贴图尺寸
                var rectTransform = ____trackCountObject.GetComponent<RectTransform>();
                Vector2 sizeDelta = rectTransform.sizeDelta;
                if (sizeDelta.x != originalSizeDelta.x) rectTransform.sizeDelta = originalSizeDelta;
                // 恢复原版文字位数
                if (____trackCountText.FrameList.Count != 1)
                {
                    while (____trackCountText.FrameList.Count > 1)
                        ____trackCountText.RemoveFormatFrame();
                }
                if (____trackDenominatortText.FrameList.Count != 1)
                {
                    while (____trackDenominatortText.FrameList.Count > 1)
                        ____trackDenominatortText.RemoveFormatFrame();
                }
                // 恢复原版文字尺寸
                ____trackCountText.FrameList[0].DefaultScale = originalTextDefaultScale1;
                ____trackDenominatortText.FrameList[0].DefaultScale = originalTextDefaultScale2;
                ____trackCountText.FrameList[0].Scale = originalTextScale1;
                ____trackDenominatortText.FrameList[0].Scale = originalTextScale2;
                // 恢复原版文字位置
                ____trackCountText.FrameList[0].RelativePosition = originalTextPos1;
                ____trackDenominatortText.FrameList[0].RelativePosition = originalTextPos2;
            }
            catch (Exception e)
            {
                MelonLogger.Msg($"[FixTrackNumDisplay] Reset to origin error: {e}");
            }
            return true; // 使用原版逻辑
        }



        // 现在是 currentTrackNum 或 maxTrackNum >= 10，需要使用自定义显示
        try
        {
            // 先做一个基础判断，看看是否需要改变样式
            if (____trackCountText.FrameList.Count == 1 || ____trackDenominatortText.FrameList.Count == 1)
            {
                // 右边增加30像素, 匹配新的贴图尺寸
                var rectTransform = ____trackCountObject.GetComponent<RectTransform>();
                Vector2 sizeDelta = rectTransform.sizeDelta;
                var newX = originalSizeDelta.x + 30;
                if (sizeDelta.x != newX) rectTransform.sizeDelta = new Vector2(newX, sizeDelta.y);
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

            // 统一使用左侧空格补位
            curStr = curStr.PadLeft(2);

            // 默认文字位置
            ____trackCountText.FrameList[0].RelativePosition = new Vector2(5, 0);
            ____trackCountText.FrameList[1].RelativePosition = new Vector2(-7, 0);
            ____trackDenominatortText.FrameList[0].RelativePosition = new Vector2(33, 0);
            ____trackDenominatortText.FrameList[1].RelativePosition = new Vector2(16, 0);

            // 数字1的位置需要额外调整，以保证视觉上与其它数字间距一致
            // currentTrackNum 第一位
            if (curStr[0] == '1' && curStr[1] != ' ')
                ____trackCountText.FrameList[0].RelativePosition = new Vector2(5 + 2, 0);
            // currentTrackNum 第二位
            if (curStr[1] == '1' && curStr[0] != ' ')
                ____trackCountText.FrameList[1].RelativePosition = new Vector2(-7 - 2, 0);
            // maxTrackNum 第一位
            if (maxStr[0] == '1')
                ____trackDenominatortText.FrameList[0].RelativePosition = new Vector2(33 + 2, 0);
            // maxTrackNum 第二位
            if (maxStr[1] == '1')
                ____trackDenominatortText.FrameList[1].RelativePosition = new Vector2(16 - 2, 0);

            // 复制原版处理普通模式的逻辑
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

        }
        catch (Exception e)
        {
            MelonLogger.Msg($"[FixTrackNumDisplay] {e}");
            return true;
        }
    }
}
