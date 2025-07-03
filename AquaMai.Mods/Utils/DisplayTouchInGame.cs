using System;
using System.Collections.Generic;
using AquaMai.Config.Attributes;
using AquaMai.Config.Types;
using AquaMai.Core.Helpers;
using AquaMai.Core.Resources;
using AquaMai.Mods.UX.PracticeMode;
using HarmonyLib;
using Manager;
using AquaMai.Core.Environment;
using Monitor;
using Process;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AquaMai.Mods.Utils;

[ConfigSection(
    zh: "在游戏过程中在副屏显示触摸输入，可用于调试吃和蹭的问题")]
public static class DisplayTouchInGame
{
    private static GameObject prefab;
    private static GameObject[] canvasGameObjects = new GameObject[2];
    private static TextMeshProUGUI tmp;
    private static TextMeshProUGUI[] tmps = new TextMeshProUGUI[2];

    [ConfigEntry(
        zh: "将上屏改成白底",
        en: "Display white background on top screen")]
    public static bool whiteBackground = false;

    [ConfigEntry(
        zh: "默认显示。关了的话，可以用按键切换显示")]
    public static bool defaultOn = true;

    [ConfigEntry(zh: "按键切换显示")]
    public static readonly KeyCodeOrName key = KeyCodeOrName.None;
    [ConfigEntry] private static readonly bool longPress = false;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CommonMonitor), "HideDebugInfoText")]
    public static void CommonMonitorInitialize(TextMeshProUGUI ____romVersionText)
    {
        tmp = ____romVersionText;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MusicSelectProcess), nameof(MusicSelectProcess.OnUpdate))]
    public static void OnMusicSelectProcessUpdate()
    {
        if (!KeyListener.GetKeyDownOrLongPress(key, longPress)) return;
        defaultOn = !defaultOn;
        MessageHelper.ShowMessage(defaultOn ? Locale.NextPlayShowTouchDisplay : Locale.NextPlayHideTouchDisplay);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameProcess), nameof(GameProcess.OnUpdate))]
    public static void OnGameProcessUpdate()
    {
        if (whiteBackground && defaultOn)
        {
            foreach (var t in tmps)
            {
                t.text = $"{TimeSpan.FromMilliseconds(PracticeMode.CurrentPlayMsec):mm\\:ss\\.fff}";
            }
        }
        if (!KeyListener.GetKeyDownOrLongPress(key, longPress)) return;
        defaultOn = !defaultOn;
        foreach (var go in canvasGameObjects)
        {
            if (go != null)
            {
                go.SetActive(defaultOn);
            }
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(InputManager), nameof(InputManager.RegisterMouseTouchPanel))]
    public static void RegisterMouseTouchPanel(int target, MouseTouchPanel targetMouseTouchPanel)
    {
        if (target != 0)
            return;
        prefab = targetMouseTouchPanel.gameObject;
        MelonLogger.Msg("[DisplayTouchInGame] RegisterMouseTouchPanel");
        MelonLogger.Msg(prefab);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameProcess), nameof(GameProcess.OnStart))]
    public static void OnGameStart(GameMonitor[] ____monitors)
    {
        MelonLogger.Msg("[DisplayTouchInGame] OnGameStart");
        if (prefab == null)
        {
            MelonLogger.Error("[DisplayTouchInGame] prefab is null");
            return;
        }

        for (int i = 0; i < 2; i++)
        {
            var sub = ____monitors[i].gameObject.transform.Find("Canvas/Sub");
            if (sub == null)
            {
                MelonLogger.Error($"[DisplayTouchInGame] sub is null for monitor {i}");
                continue;
            }
            var canvas = new GameObject("[AquaMai] DisplayTouchInGame");
            canvas.transform.SetParent(sub, false);
            canvas.SetActive(defaultOn);
            canvasGameObjects[i] = canvas;

            if (whiteBackground)
            {
                var rect = canvas.AddComponent<RectTransform>();
                rect.sizeDelta = new Vector2(1080, 450);
                rect.localPosition = new Vector3(0f, 0f, 0.1f);
                var img = canvas.AddComponent<Image>();
                img.color = Color.white;

                var t = Object.Instantiate(tmp, canvas.transform, false);
                t.text = "";
                t.transform.localPosition = new Vector3(-500f, 0f, 0f);
                t.transform.localScale = Vector3.one * 2;
                tmps[i] = t;
            }

            var buttons = new GameObject("Buttons");
            buttons.transform.SetParent(canvas.transform, false);
            buttons.transform.localPosition = new Vector3(0f, 0f, 0.2f);
            buttons.transform.localScale = Vector3.one * 450 / 1080f;

            var touchPanel = Object.Instantiate(prefab, canvas.transform, false);
            Object.Destroy(touchPanel.GetComponent<MouseTouchPanel>());
            foreach (Transform item in touchPanel.transform)
            {
                Object.Destroy(item.GetComponent<MeshButton>());
                Object.Destroy(item.GetComponent<Collider>());
                if (item.name.StartsWith("A"))
                {
                    var btn = Object.Instantiate(item, buttons.transform, false);
                    btn.name = item.name;
                }
                var customGraphic = item.GetComponent<CustomGraphic>();
                customGraphic.color = Color.blue;
                var tmp = item.GetComponentInChildren<TextMeshProUGUI>();
                tmp.color = Color.black;
            }
            touchPanel.transform.localScale = Vector3.one * 0.95f * 450 / 1080f;
            touchPanel.transform.localPosition = new Vector3(0f, 0f, 0.3f);
            var touchDisplay = touchPanel.AddComponent<Display>();
            touchDisplay.player = i;
            var buttonDisplay = buttons.AddComponent<Display>();
            buttonDisplay.player = i;
            buttonDisplay.isButton = true;
        }
    }

    private class Display : MonoBehaviour
    {
        public int player;
        public bool isButton;

        private List<CustomGraphic> _buttonList;
        private Color _offTouchCol = new Color(0f, 0f, 1f);
        private Color _onTouchCol = new Color(1f, 0f, 0f);

        private void Start()
        {
            if (isButton)
            {
                _offTouchCol = Color.clear;
            }
            _buttonList = new List<CustomGraphic>();
            foreach (Transform item in transform)
            {
                CustomGraphic component = item.GetComponent<CustomGraphic>();
                _buttonList.Add(component);
            }
        }

        private void OnGUI()
        {
            foreach (CustomGraphic graphic in _buttonList)
            {
                if (!Enum.TryParse(graphic.name, out InputManager.TouchPanelArea button)) return;
                if (isButton)
                {
                    graphic.color = (InputManager.GetButtonPush(player, (InputManager.ButtonSetting)button) ? _onTouchCol : _offTouchCol);
                }
                else
                {
                    graphic.color = (InputManager.GetTouchPanelAreaPush(player, button) ? _onTouchCol : _offTouchCol);
                }
            }
        }
    }
}
