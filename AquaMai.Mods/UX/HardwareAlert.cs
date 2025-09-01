using System.Diagnostics;
using AquaMai.Config.Attributes;
using AquaMai.Core.Resources;
using HarmonyLib;
using IO;
using MAI2.Util;
using Main;
using Manager;
using Process;
using Process.Error;
using UnityEngine;

namespace AquaMai.Mods.UX;

[ConfigSection(
    defaultOn: true,
    zh: "自定义硬件警告，可配置在指定硬件自检失败时阻止游戏启动并显示报错画面")]
public class HardwareAlert : MonoBehaviour
{
    private static HardwareAlert display;
    private Stopwatch stopwatch = new Stopwatch();

    // [HarmonyPostfix]
    // [HarmonyPatch(typeof(StartupProcess), "OnUpdate")]
    // public static void OnPostUpdate(StartupProcess __instance)
    // {
    //     var tv = Traverse.Create(__instance);
    //     if (tv.Field("_state").GetValue<byte>() > 0)
    //     {
    //         if (SingletonStateMachine<AmManager, AmManager.EState>.Instance.NewTouchPanel[1].Status ==
    //             NewTouchPanel.StatusEnum.Error)
    //         {
    //             var go = new GameObject("硬件提示组件");
    //             display = go.AddComponent<HardwareAlert>();
    //         }
    //     }
    // }

    private static int errorCode;
    private static string errorMessage;
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(StartupProcess), "OnUpdate")]
    public static void OnPostUpdate(StartupProcess __instance)
    {
        if (AMDaemon.Error.Number > 0)
        {
            var tv = Traverse.Create(__instance);
            var ctn = tv.Field("container").GetValue<ProcessDataContainer>();
            ctn.processManager.AddProcess((ProcessBase) new ErrorProcess(ctn));
            ctn.processManager.ReleaseProcess((ProcessBase) __instance);
            GameManager.IsErrorMode = true;
            // errorCode = AMDaemon.Error.Number;
            // errorMessage = AMDaemon.Error.Message;
            // var go = new GameObject("硬件提示组件");
            // display = go.AddComponent<HardwareAlert>();
        }
    }

    private void Start()
    {
        stopwatch.Start();
    }

    private void OnGUI()
    {
        if (stopwatch.ElapsedMilliseconds < 2000)
        {
            return;
        }
        GUIStyle styleTitle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 35,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true
        };
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 25,
            alignment = TextAnchor.MiddleLeft,
            wordWrap = true
        };

        GUI.Label(new Rect(50, 50, Screen.width - 50, Screen.height - 500), "AMDaemon Error", styleTitle);
        GUI.Label(new Rect(50, 50, Screen.width - 50, Screen.height - 50), $"出现错误：{errorCode}, Message: {errorMessage}", style);
    }
}