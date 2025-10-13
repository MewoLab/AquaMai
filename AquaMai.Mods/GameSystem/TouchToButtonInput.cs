using System.Collections.Generic;
using System.Reflection;
using AquaMai.Config.Attributes;
using HarmonyLib;
using Process;
using static Manager.InputManager;

namespace AquaMai.Mods.GameSystem;

[ConfigSection(
    name: "触摸转按键",
    en: "Map touch actions to buttons.",
    zh: "映射触摸操作至实体按键")]
public class TouchToButtonInput
{
    private static bool _isPlaying = false;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameProcess), "OnStart")]
    public static void OnGameProcessStart(GameProcess __instance)
    {
        _isPlaying = true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameProcess), "OnRelease")]
    public static void OnGameProcessRelease(GameProcess __instance)
    {
        _isPlaying = false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TestModeProcess), "OnStart")]
    public static void OnTestModeProcessStart()
    {
        _isPlaying = true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TestModeProcess), "OnRelease")]
    public static void OnTestModeProcessRelease()
    {
        _isPlaying = false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Manager.InputManager), "GetButtonDown")]
    public static void GetButtonDown(ref bool __result, int monitorId, ButtonSetting button)
    {
        if (_isPlaying || __result) return;
        if (button.ToString().StartsWith("Button"))
        {
            __result = GetTouchPanelAreaDown(monitorId, (TouchPanelArea)button);
        }
        else if (button.ToString().Equals("Select"))
        {
            __result = GetTouchPanelAreaLongPush(monitorId, TouchPanelArea.C1, 500L) || GetTouchPanelAreaLongPush(monitorId, TouchPanelArea.C2, 500L);
        }
    }

    [HarmonyPatch]
    public static class GetMonitorButtonDown
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(Manager.InputManager), "GetMonitorButtonDown", [typeof(int).MakeByRefType(), typeof(ButtonSetting)]);
        }

        public static void Postfix(ref bool __result, ref int monitorId, ButtonSetting button)
        {
            if (_isPlaying || __result) return;
            for (int i = 0; i < 2; i++)
            {
                if (button.ToString().StartsWith("Button"))
                {
                    __result = GetTouchPanelAreaDown(i, (TouchPanelArea)button);
                }
                else if (button.ToString().Equals("Select"))
                {
                    __result = GetTouchPanelAreaLongPush(i, TouchPanelArea.C1, 500L) || GetTouchPanelAreaLongPush(i, TouchPanelArea.C2, 500L);
                }

                if (__result)
                {
                    monitorId = i;
                    break;
                }
            }
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Manager.InputManager), "GetButtonPush")]
    public static void GetButtonPush(ref bool __result, int monitorId, ButtonSetting button)
    {
        if (_isPlaying || __result) return;
        if (button.ToString().StartsWith("Button")) __result = GetTouchPanelAreaPush(monitorId, (TouchPanelArea)button);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Manager.InputManager), "GetButtonLongPush")]
    public static void GetButtonLongPush(ref bool __result, int monitorId, ButtonSetting button, long msec)
    {
        if (_isPlaying || __result) return;
        if (button.ToString().StartsWith("Button")) __result = GetTouchPanelAreaLongPush(monitorId, (TouchPanelArea)button, msec);
    }
}