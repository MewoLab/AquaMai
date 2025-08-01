using System.Collections.Generic;
using System.Reflection;
using AquaMai.Config.Attributes;
using AquaMai.Config.Types;
using AquaMai.Core;
using AquaMai.Core.Helpers;
using AquaMai.Core.Resources;
using AquaMai.Mods.GameSystem;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;

namespace AquaMai.Mods.Utils;

[ConfigSection(zh: """
                   屏幕位置调整。适用于手台对不齐的情况，可以分别调整每个屏幕区域的位置
                   在游戏中按键开启调整模式
                   目前开启后不支持使用鼠标模拟触摸
                   """)]
public class ScreenPositionAdjust
{
    [ConfigEntry(zh: "上屏紧贴着下屏", en: "Top screen is tightly attached to the bottom screen")]
    public static readonly bool compactMode = false;
    [ConfigEntry(zh: "进入调整模式的按键", en: "Key to enter adjustment mode")]
    public static readonly KeyCodeOrName adjustKey = KeyCodeOrName.F10;

    private static GameObject root;
    // main1P sub1P main2P sub2P
    private static Transform[] images = new Transform[4];
    private static Camera[] cameras = new Camera[4];
    private static RenderTexture[] renderTextures = new RenderTexture[4];

    /// <summary>
    /// 避免一帧延迟
    /// </summary>
    private class CameraUpdater : MonoBehaviour
    {
        void OnPreRender()
        {
            foreach (var camera in cameras)
            {
                if (camera == null)
                {
                    continue;
                }
                camera.Render();
            }
        }
    }

    private static float GetSizeFactor()
    {
        return Screen.height / (compactMode ? 1080f + 450f : 1920f);
    }


    /// <summary>
    /// 调整大小时重新调整 texture 大小，让显示质量最好
    /// </summary>
    private class DynamicRenderTextureResizer : MonoBehaviour
    {
        private int lastHeight;

        void Start()
        {
            lastHeight = Screen.height;
        }

        void Update()
        {
            if (Screen.height == lastHeight) return;
#if DEBUG
            MelonLogger.Msg("[ScreenPositionAdjust] Screen height changed, resizing render textures.");
#endif
            lastHeight = Screen.height;
            for (int i = 0; i < renderTextures.Length; i++)
            {
                if (renderTextures[i] != null)
                {
                    renderTextures[i].Release();
                    renderTextures[i].width = (int)(1080 * GetSizeFactor());
                    renderTextures[i].height = (int)(((i % 2 == 0) ? 1080 : 450) * GetSizeFactor());
                    renderTextures[i].Create();
                }
            }
        }
    }

    private static float[] offsetX = new float[4];
    private static float[] offsetY = new float[4];

    private class AdjustController : MonoBehaviour
    {
        private int index = -1;
        private float speed = 1f;

        private void OnGUI()
        {
            if (index == -1) return;
            var rect = new Rect(0, 0, GuiSizes.FontSize * 50, GuiSizes.FontSize * 15);

            var player = index < 2 ? "1P" : "2P";
            var sub = index % 2 == 0 ? "Main" : "Sub";

            var labelStyle = GUI.skin.GetStyle("label");
            labelStyle.fontSize = GuiSizes.FontSize * 2;
            labelStyle.alignment = TextAnchor.MiddleLeft;
            GUI.Box(rect, "");
            GUI.Label(rect, string.Format(Locale.ScreenPositionAdjustTip, $"{player} {sub}", speed, adjustKey));
        }

        private void Update()
        {
            if (KeyListener.GetKeyDown(adjustKey))
            {
                index++;
                if (index > 3)
                {
                    index = -1;
                }
                if (index == -1)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        PlayerPrefs.SetFloat($"AquaMaiScreenPositionAdjust-x:{i}", offsetX[i]);
                        PlayerPrefs.SetFloat($"AquaMaiScreenPositionAdjust-y:{i}", offsetY[i]);
                    }
                    PlayerPrefs.Save();
                    return;
                }
            }
            if (index == -1) return;
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                offsetX[index] -= speed;
                images[index].localPosition -= new Vector3(speed, 0, 0);
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                offsetX[index] += speed;
                images[index].localPosition += new Vector3(speed, 0, 0);
            }
            else if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                offsetY[index] += speed;
                images[index].localPosition += new Vector3(0, speed, 0);
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                offsetY[index] -= speed;
                images[index].localPosition -= new Vector3(0, speed, 0);
            }
            else if (Input.GetKeyDown(KeyCode.LeftBracket))
            {
                speed /= 2f;
            }
            else if (Input.GetKeyDown(KeyCode.RightBracket))
            {
                speed *= 2f;
            }
        }
    }

    public static IEnumerable<MethodBase> TargetMethods()
    {
        var lateInitialize = AccessTools.Method(typeof(Main.GameMain), "LateInitialize", [typeof(MonoBehaviour), typeof(Transform), typeof(Transform)]);
        if (lateInitialize is not null) return [lateInitialize];
        return [AccessTools.Method(typeof(Main.GameMain), "Initialize", [typeof(MonoBehaviour), typeof(Transform), typeof(Transform)])];
    }

    public static void Prefix(Transform left, Transform right)
    {
        root = new GameObject("[AquaMai] ScreenPositionAdjust Display", [typeof(Canvas)]);
        root.transform.position = new Vector3(11451, 19198, 0);
        var canvas = root.GetComponent<Canvas>();
        canvas.GetComponent<RectTransform>().sizeDelta = new Vector2(2160, 1920);
        canvas.renderMode = RenderMode.WorldSpace;
        root.AddComponent<CanvasScaler>();
        root.AddComponent<GraphicRaycaster>();
        root.AddComponent<DynamicRenderTextureResizer>();
        root.AddComponent<AdjustController>();
        Camera.main.gameObject.AddComponent<CameraUpdater>();
        Camera.main.transform.position = new Vector3(ConfigLoader.Config.GetSectionState(typeof(SinglePlayer)).Enabled ? 11451 - 540 : 11451, 19198, -800);

        var compactDelta = 0;
        if (compactMode)
        {
            Camera.main.orthographicSize = 540 + 225;
            // 960 - 540 - 225 = 195
            compactDelta = 195;
        }

        // init camera
        for (int i = 0; i < 4; i++)
        {
            var player = i < 2 ? "1P" : "2P";
            var sub = i % 2 == 0 ? "Main" : "Sub";
            var texture = new RenderTexture((int)(1080 * GetSizeFactor()), (int)((sub == "Main" ? 1080 : 450) * GetSizeFactor()), 24, RenderTextureFormat.RGB111110Float)
            {
                useMipMap = false,
                autoGenerateMips = false,
                depth = 0,
                antiAliasing = 1,
            };

            var camera = new GameObject($"[AquaMai] ScreenPositionAdjust Camera {sub} {player}").AddComponent<Camera>();
            camera.transform.parent = player == "1P" ? left : right;
            camera.enabled = false;
            camera.targetTexture = texture;
            camera.cullingMask = Camera.main.cullingMask;
            camera.backgroundColor = Color.black;
            camera.orthographic = true;
            camera.orthographicSize = sub == "Main" ? 540 : 225;
            camera.transform.localPosition = new Vector3(0, sub == "Main" ? -420 : 735, -800);
            cameras[i] = camera;

            var image = new GameObject($"[AquaMai] ScreenPositionAdjust Image {sub} {player}").AddComponent<UnityEngine.UI.RawImage>();
            image.transform.parent = root.transform;
            image.texture = texture;
            image.rectTransform.sizeDelta = new Vector2(1080, sub == "Main" ? 1080 : 450);
            image.transform.localPosition = new Vector3(player == "1P" ? -540 : 540, sub == "Main" ? -420 + compactDelta : 735 - compactDelta, 0);
            images[i] = image.transform;

            offsetX[i] = PlayerPrefs.GetFloat($"AquaMaiScreenPositionAdjust-x:{i}", 0);
            offsetY[i] = PlayerPrefs.GetFloat($"AquaMaiScreenPositionAdjust-y:{i}", 0);
            images[i].localPosition += new Vector3(offsetX[i], offsetY[i], 0);
        }
    }
}