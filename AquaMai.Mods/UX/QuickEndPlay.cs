using AquaMai.Config.Attributes;
using AquaMai.Core.Helpers;
using AquaMai.Core.Resources;
using HarmonyLib;
using MAI2.Util;
using Manager;
using Monitor;
using Process;
using UnityEngine;

namespace AquaMai.Mods.UX;

[ConfigSection(
    en: "Show a \"skip\" button like AstroDX after the notes end.",
    zh: "音符结束之后显示像 AstroDX 一样的「跳过」按钮")]
public class QuickEndPlay
{
    private static int _timer;

    [HarmonyPatch(typeof(GameProcess), "OnStart")]
    [HarmonyPostfix]
    public static void GameProcessPostStart(GameMonitor[] ____monitors)
    {
        _timer = 0;
        ____monitors[0].gameObject.AddComponent<Ui>();
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

    private class Ui : MonoBehaviour
    {
        //button position(x,y) topleft point
        public float topleftx = Screen.width / 2 - GuiSizes.PlayerWidth * 0.097f;
        public float toplefty = Screen.height - GuiSizes.PlayerWidth * 0.09f;
        //button size(w,h)
        public float width = GuiSizes.PlayerWidth * .50f;
        public float height = GuiSizes.PlayerWidth * .23f;

        public void OnGUI()
        {
            if (_timer < 60) return;

            // get button texture 4 -> SKIP
            Texture2D buttonTexture = ButtonControllerBase.GetFlatButtonParam(4).Image.texture;
            // set button background to transparent
            GUIStyle buttonBG = new GUIStyle(GUI.skin.button);
            buttonBG.normal.background = null;
            buttonBG.hover.background = null;
            buttonBG.active.background = null;
            // rotate -22.5°
            Vector2 topLeftPosition = new Vector2(topleftx, toplefty);
            GUIUtility.RotateAroundPivot((float)-22.5, topLeftPosition);
            // draw button
            GUI.Button(new Rect(topleftx, toplefty, width, height), buttonTexture, buttonBG);
        }
    }
}
