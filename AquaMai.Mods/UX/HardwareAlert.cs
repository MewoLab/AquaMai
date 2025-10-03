using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using AquaMai.Config.Attributes;
using AquaMai.Core.Helpers;
using AquaMai.Core.Resources;
using AquaMai.Mods.GameSystem;
using HarmonyLib;
using IO;
using MAI2.Util;
using MAI2System;
using Main;
using Manager;
using MelonLoader;
using Monitor.Error;
using Process;
using Process.Error;
using TMPro;
using UnityEngine;

namespace AquaMai.Mods.UX;

[ConfigSection(
    name: "自定义自检警告",
    en: "Custom hardware alert, you can configure to display an error frame upon hardware failure. Toggle the switches below to define the required hardware.",
    zh: "自定义硬件警告，可配置在指定硬件自检失败时阻止游戏启动并显示报错画面，开启下方的错误类型以配置需要关注的错误。")]
public class HardwareAlert
{
    [ConfigEntry(
        name: "原始语言显示",
        en: "If enabled, all the in-game hardware warnings will be displayed in game's original language, like Japanese for SDEZ. If you have used any translation pack, you should disable this setting.",
        zh: "如果启用，所有硬件警告将会使用游戏原本的语言显示，例如 SDEZ 就会用日文显示报错。如果你安装了任何汉化包，你应该关闭这个选项。")]
    private static readonly bool UseOriginalGameLanguage = true;
    [ConfigEntry(
        en: "1P Touch Sensor",
        name: "1P 触摸屏")]
    private static readonly bool TouchSensor_1P = false; // Error 3300, 3301
    [ConfigEntry(
        en: "2P Touch Sensor",
        name: "2P 触摸屏")]
    private static readonly bool TouchSensor_2P = false; // Error 3302, 3303
    [ConfigEntry(
        en: "1P LED",
        name: "1P LED")]
    private static readonly bool LED_1P = false; // custom 3400
    [ConfigEntry(
        en: "2P LED",
        name: "2P LED")]
    private static readonly bool LED_2P = false; // custom 3401
    [ConfigEntry(
        en: "Player Camera",
        name: "玩家摄像机")]
    private static readonly bool PlayerCamera = false;  // 3102
    [ConfigEntry(
        en: "DX Pass 1P",
        name: "DX Pass 1P")]
    private static readonly bool CodeReader_1P = false; // 3101
    [ConfigEntry(
        en: "DX Pass 2P",
        name: "DX Pass 2P")]
    private static readonly bool CodeReader_2P = false; // 3101
    [ConfigEntry(
        en: "WeChat QRCode Camera",
        name: "二维码扫描摄像头")]
    public static readonly bool ChimeCamera = false; // 3100

    private static readonly List<string> CameraTypeList = ["QRLeft", "QRRight", "Photo", "Chime"];
    private static SortedDictionary<CameraTypeEnumInner, int> _cameraIndex = [];
    private static bool _isInitialized = false;

    private enum CameraTypeEnumInner
    {
        QRLeft,
        QRRight,
        Photo,
        Chime,
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CameraManager), "CameraInitialize")]
    public static void PostCameraInitialize(CameraManager __instance)
    {
        if (_isInitialized)
        {
            return;
        }
        
        var curCamIdx = 0;
        foreach (var cameraTypeName in CameraTypeList)
        {
            if (Enum.TryParse<CameraTypeEnumInner>(cameraTypeName, out var cameraType))
            {
                MelonLogger.Msg($"[HardwareAlert] Identified camera type {cameraType} for current game version on idx {curCamIdx}");
                _cameraIndex[cameraType] = curCamIdx;
                curCamIdx++;
            }
        }

        _isInitialized = true;
    }
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(StartupProcess), "OnUpdate")]
    public static void OnPostUpdate(StartupProcess __instance)
    {
        // get current startup state
        var tv = Traverse.Create(__instance);
        // var state = tv.Field("_state").GetValue<byte>();
        var statusSubMsg = tv.Field("_statusSubMsg").GetValue<string[]>();
        
        // Touch sensor check
        // The built-in AMDaemon errors are not stable, and cannot be localized.
        // So we decided to use another approach to check it.
        if (TouchSensor_1P && statusSubMsg[0] == ConstParameter.TestString_Bad)
        {
            ErrorFrame.Show(__instance, 3300, FaultTouchSensor1P[GetLocale()]);
            return;
        }
        if (TouchSensor_2P && statusSubMsg[1] == ConstParameter.TestString_Bad)
        {
            ErrorFrame.Show(__instance, 3302, FaultTouchSensor2P[GetLocale()]);
            return;
        }

        // LED check
        if (LED_1P && statusSubMsg[2] == ConstParameter.TestString_Bad)
        {
            ErrorFrame.Show(__instance, 3400, FaultLED1P[GetLocale()]);
            return;
        }
        if (LED_2P && statusSubMsg[3] == ConstParameter.TestString_Bad)
        {
            ErrorFrame.Show(__instance, 3401, FaultLED2P[GetLocale()]);
            return;
        }
        
        // Camera Check
        if (CameraManager.IsReady)
        {
            var nCam = CameraManager.IsAvailableCameras.Length;

            var pcIdx = _cameraIndex[CameraTypeEnumInner.Photo];
            if (PlayerCamera && pcIdx < nCam && !CameraManager.IsAvailableCameras[pcIdx])
            {
                ErrorFrame.Show(__instance, 3102, FaultPlayerCamera[GetLocale()]);
                return;
            }

            var cr1PIdx = _cameraIndex[CameraTypeEnumInner.QRLeft];
            if (CodeReader_1P && cr1PIdx < nCam && !CameraManager.IsAvailableCameras[cr1PIdx])
            {
                ErrorFrame.Show(__instance, 3101, FaultQR1P[GetLocale()]);
                return;
            }

            var cr2PIdx = _cameraIndex[CameraTypeEnumInner.QRRight];
            if (CodeReader_2P && cr2PIdx < nCam && !CameraManager.IsAvailableCameras[cr2PIdx])
            {
                ErrorFrame.Show(__instance, 3101, FaultQR2P[GetLocale()]);
                return;
            }

            var chimeIdx = _cameraIndex[CameraTypeEnumInner.Chime];
            if (ChimeCamera && chimeIdx < nCam && !CameraManager.IsAvailableCameras[chimeIdx])
            {
                ErrorFrame.Show(__instance, 3100, FaultChime[GetLocale()]);
                return;
            }
        }
    }

    private static string GetLocale()
    {
        if (UseOriginalGameLanguage)
        {
            return GameVersionToLocale[GameInfo.GameId];
        }
        MelonLogger.Msg($"[HardwareAlert] Using locale '{Locale.Culture.TwoLetterISOLanguageName}'");
        // MelonLogger.Msg($"[HardwareAlert] Using locale '{Locale.Culture.Name}'");
        return Locale.Culture.TwoLetterISOLanguageName.Equals("zh",
            StringComparison.OrdinalIgnoreCase)
            ? "zh"
            : "en";
    }

    private static readonly Dictionary<string, string> GameVersionToLocale = new()
    {
        ["SDEZ"] = "jp",
        ["SDGA"] = "en",
        ["SDGB"] = "zh",
    };
    private static readonly Dictionary<string, string> FaultTouchSensor1P = new()
    {
        ["jp"] = "タッチセンサ（1P）はご利用いただけません",
        ["en"] = "Touch Sensor (1P) not available",
        ["zh"] = "触摸传感器（1P）不可用",
    };
    private static readonly Dictionary<string, string> FaultTouchSensor2P = new()
    {
        ["jp"] = "タッチセンサ（2P）はご利用いただけません",
        ["en"] = "Touch Sensor (2P) not available",
        ["zh"] = "触摸传感器（2P）不可用",
    };
    private static readonly Dictionary<string, string> FaultLED1P = new()
    {
        ["jp"] = "LED（1P）はご利用いただけません",
        ["en"] = "LED(1P) not available",
        ["zh"] = "LED（1P）不可用",
    };
    private static readonly Dictionary<string, string> FaultLED2P = new()
    {
        ["jp"] = "LED（2P）はご利用いただけません",
        ["en"] = "LED(2P) not available",
        ["zh"] = "LED（2P）不可用",
    };
    private static readonly Dictionary<string, string> FaultQR1P = new()
    {
        ["jp"] = "コードリーダー（1P）はご利用いただけません",
        ["en"] = "Code Reader (1P) not available",
        ["zh"] = "DX Pass 二维码相机（1P）不可用", // This thing does not exist...
    };
    private static readonly Dictionary<string, string> FaultQR2P = new()
    {
        ["jp"] = "コードリーダー（2P）はご利用いただけません",
        ["en"] = "Code Reader (2P) not available",
        ["zh"] = "DX Pass 二维码相机（2P）不可用", // This thing does not exist...
    };
    private static readonly Dictionary<string, string> FaultPlayerCamera = new()
    {
        ["jp"] = "プレイヤーカメラはご利用いただけません",
        ["en"] = "Player Camera not available",
        ["zh"] = "玩家相机不可用",
    };
    private static readonly Dictionary<string, string> FaultChime = new()
    {
        ["jp"] = "コードリーダーは（Chime）ご利用いただけません", // This thing does not exist...
        ["en"] = "Code Reader (Chime) not available", // This thing does not exist...
        ["zh"] = "二维码相机不可用",
    };
}