using AquaMai.Config.Attributes;
using HarmonyLib;
using MAI2.Util;
using Manager;
using Monitor;
using Process;
using UI;
using UnityEngine;

namespace AquaMai.Mods.UX;

[ConfigSection(
    name: "歌曲快速结束",
    en: "Show a \"skip\" button like AstroDX after the notes end.",
    zh: "音符结束之后显示像 AstroDX 一样的「跳过」按钮")]
public class QuickEndPlay
{
    private static int _timer;
    private static QuickEndPlayButtonController[] ui = new QuickEndPlayButtonController[2];

    [HarmonyPatch(typeof(GameProcess), "OnStart")]
    [HarmonyPostfix]
    public static void GameProcessPostStart(GameMonitor[] ____monitors)
    {
        _timer = 0;
        for (int i = 0; i < 2; i++)
        {
            var main = Traverse.Create(____monitors[i]).Field<CanvasGroup>("Main").Value;
            var uiButtons = new GameObject();
            uiButtons.transform.SetParent(main.transform, false);
            ui[i] = uiButtons.AddComponent<QuickEndPlayButtonController>();
        }
    }

    [HarmonyPatch(typeof(GameProcess), "OnUpdate")]
    [HarmonyPostfix]
    public static void GameProcessPostUpdate(GameProcess __instance, Message[] ____message, ProcessDataContainer ___container, byte ____sequence)
    {
        switch (____sequence)
        {
            case 9:
                _timer = 0;
                break;
            case > 4:
                _timer++;
                if (_timer == 60)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        ui[i].Initialize(i);
                    }
                }

                break;
            default:
                _timer = 0;
                break;
        }

        if (_timer > 60 && (InputManager.GetTouchPanelAreaDown(InputManager.TouchPanelArea.A4) || InputManager.GetButtonDown(0, InputManager.ButtonSetting.Button04)))
        {
            var traverse = Traverse.Create(__instance);
            ___container.processManager.SendMessage(____message[0]);
            Singleton<GamePlayManager>.Instance.SetSyncResult(0);
            traverse.Method("SetRelease").GetValue();
        }
    }

    private class QuickEndPlayButtonController : ButtonControllerBase
    {
        public override void Initialize(int monitorIndex)
        {
            InitPositions();
            base.Initialize(monitorIndex);
            var param = GetFlatButtonParam(FlatButtonType.Skip);
            CommonButtons = new CommonButtonObject[_positions.Length];
            CommonButtons[3] = Instantiate(CommonPrefab.GetFlatButtonObject(), _positions[3]);
            CommonButtons[3].Initialize(MonitorIndex, InputManager.ButtonSetting.Button04, param.LedColor);
            CommonButtons[3].SetSymbol(param.Image, isFlip: false);
            CommonButtons[3].SetSE(param.Cue);
            SetVisible(true, 3);
        }

        private void InitPositions()
        {
            _positions = new Transform[8];
            var btn4 = new GameObject();
            var rectTransform = btn4.AddComponent<RectTransform>();
            btn4.transform.SetParent(transform, false);
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0);
            rectTransform.anchoredPosition = new Vector2(223, -563);
            rectTransform.sizeDelta = new Vector2(351, 212);
            rectTransform.rotation = Quaternion.Euler(0, 0, 22.5f);
            rectTransform.localScale = new Vector3(1, 1, 1);
            _positions[3] = rectTransform;
        }
    }
}