using System;
using System.Linq;
using System.Reflection;
using AMDaemon;
using AquaMai.Config.Attributes;
using AquaMai.Core.Helpers;
using AquaMai.Mods.GameSystem.MaimollerIO.Libs;
using HarmonyLib;
using Main;
using Manager;
using Mecha;
using UnityEngine;

namespace AquaMai.Mods.GameSystem.MaimollerIO;

[ConfigCollapseNamespace]
[ConfigSection(
    name: "Maimoller 的输入输出协议",
    en: "Input (buttons and touch) and output (LEDs) using Maimoller IO firmware",
    zh: "适配 Maimoller 的输入（按钮和触屏）和输出（LED）")]
public class MaimollerIO
{
    [ConfigEntry(
        name: "模式",
        en: "Mode: P1P2, P1, P2 (Default: P1P2, i.e. try to read two devices, one for 1P and one for 2P)",
        zh: "初始化模式：P1P2, P1, P2（默认为 P1P2，即尝试读取两个设备，分别作为 1P 和 2P）")]
    private static readonly MaimollerInitMode mode = MaimollerInitMode.P1P2;

    public static void OnBeforePatch()
    {
        Maimoller.Initialize(mode);
        JvsSwitchHook.AddKeyChecker(GetPushedByButton);
    }

    // NOTE: Coin button is not supported yet. AquaMai recommands setting fixed number of credits directly in the configuration.
    private static bool GetPushedByButton(int playerNo, InputId inputId) => ShouldEnableForPlayer(playerNo) && inputId.Value switch
    {
        "test" => _inputReports[playerNo].GetSwitchState(MaimollerInputReport.SwitchClass.SYSTEM, MaimollerInputReport.ButtonMask.TEST) != 0,
        "service" => _inputReports[playerNo].GetSwitchState(MaimollerInputReport.SwitchClass.SYSTEM, MaimollerInputReport.ButtonMask.SERVICE) != 0,
        "select" => _inputReports[playerNo].GetSwitchState(MaimollerInputReport.SwitchClass.SYSTEM, MaimollerInputReport.ButtonMask.SELECT) != 0,
        "button_01" => _inputReports[playerNo].GetSwitchState(MaimollerInputReport.SwitchClass.BUTTON, MaimollerInputReport.ButtonMask.BTN_1) != 0,
        "button_02" => _inputReports[playerNo].GetSwitchState(MaimollerInputReport.SwitchClass.BUTTON, MaimollerInputReport.ButtonMask.BTN_2) != 0,
        "button_03" => _inputReports[playerNo].GetSwitchState(MaimollerInputReport.SwitchClass.BUTTON, MaimollerInputReport.ButtonMask.BTN_3) != 0,
        "button_04" => _inputReports[playerNo].GetSwitchState(MaimollerInputReport.SwitchClass.BUTTON, MaimollerInputReport.ButtonMask.BTN_4) != 0,
        "button_05" => _inputReports[playerNo].GetSwitchState(MaimollerInputReport.SwitchClass.BUTTON, MaimollerInputReport.ButtonMask.BTN_5) != 0,
        "button_06" => _inputReports[playerNo].GetSwitchState(MaimollerInputReport.SwitchClass.BUTTON, MaimollerInputReport.ButtonMask.BTN_6) != 0,
        "button_07" => _inputReports[playerNo].GetSwitchState(MaimollerInputReport.SwitchClass.BUTTON, MaimollerInputReport.ButtonMask.BTN_7) != 0,
        "button_08" => _inputReports[playerNo].GetSwitchState(MaimollerInputReport.SwitchClass.BUTTON, MaimollerInputReport.ButtonMask.BTN_8) != 0,
        _ => false,
    };

    private static bool ShouldEnableForPlayer(int playerNo) => mode switch
    {
        MaimollerInitMode.P1P2 => playerNo == 0 || playerNo == 1,
        MaimollerInitMode.P1 => playerNo == 0,
        MaimollerInitMode.P2 => playerNo == 1,
        _ => false,
    };

    private static readonly MaimollerInputReport[] _inputReports = [.. Enumerable.Repeat(0, 2).Select(_ => new MaimollerInputReport())];
    private static readonly MaimollerOutputReport[] _outputReports = [.. Enumerable.Repeat(0, 2).Select(_ => new MaimollerOutputReport())];
    private static readonly MaimollerLedManager[] _ledManagers = [.. Enumerable.Repeat(0, 2).Select(i => new MaimollerLedManager(_outputReports[i]))];

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameMain), "Update")]
    public static void GameMainUpdatePrefix(bool ____isInitialize)
    {
        if (!____isInitialize) return;
        for (int i = 0; i < 2; i++)
        {
            if (!ShouldEnableForPlayer(i)) continue;
            Maimoller.Read(i, _inputReports[i]);
            Maimoller.Write(i, _outputReports[i]);
        }
    }

    private readonly static FieldInfo _tpNowField = typeof(InputManager).GetField("TpNow", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(InputManager), "UpdateTouchPanel")]
    public static void PreUpdateTouchPanel()
    {
        ulong[] tpNow = (ulong[])_tpNowField.GetValue(null);
        for (int i = 0; i < 2; i++)
        {
            if (!ShouldEnableForPlayer(i)) continue;
            ulong s = 0uL;
            s |= (ulong)_inputReports[i].GetSwitchState(MaimollerInputReport.SwitchClass.TOUCH_A, MaimollerInputReport.ButtonMask.ANY_PLAYER);
            s |= (ulong)_inputReports[i].GetSwitchState(MaimollerInputReport.SwitchClass.TOUCH_B, MaimollerInputReport.ButtonMask.ANY_PLAYER) << 8;
            s |= (ulong)_inputReports[i].GetSwitchState(MaimollerInputReport.SwitchClass.TOUCH_C, MaimollerInputReport.ButtonMask.ANY_TOUCH_C) << 16;
            s |= (ulong)_inputReports[i].GetSwitchState(MaimollerInputReport.SwitchClass.TOUCH_D, MaimollerInputReport.ButtonMask.ANY_PLAYER) << 18;
            s |= (ulong)_inputReports[i].GetSwitchState(MaimollerInputReport.SwitchClass.TOUCH_E, MaimollerInputReport.ButtonMask.ANY_PLAYER) << 26;
            tpNow[i] |= s;
        }
        _tpNowField.SetValue(null, tpNow);
    }

    [HarmonyPatch]
    public static class JvsOutputPwmPatch
    {
        public static MethodInfo TargetMethod() => typeof(IO.Jvs).GetNestedType("JvsOutputPwm", BindingFlags.NonPublic | BindingFlags.Public).GetMethod("Set");

        public static void Prefix(byte index, Color32 color) => _ledManagers[index].SetBillboardColor(color);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Bd15070_4IF), "PreExecute")]
    public static void PostPreExecute(Bd15070_4IF.InitParam ____initParam) =>
        _ledManagers[____initParam.index].PreExecute();

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Bd15070_4IF), "_setColor")]
    public static void Pre_setColor(byte ledPos, Color32 color, Bd15070_4IF.InitParam ____initParam) =>
        _ledManagers[____initParam.index].SetButtonColor(ledPos, color);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Bd15070_4IF), "_setColorMulti")]
    public static void Pre_setColorMulti(Color32 color, byte speed, Bd15070_4IF.InitParam ____initParam) =>
        _ledManagers[____initParam.index].SetButtonColor(-1, color);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Bd15070_4IF), "_setColorMultiFade")]
    public static void Pre_setColorMultiFade(Color32 color, byte speed, Bd15070_4IF.InitParam ____initParam) =>
        _ledManagers[____initParam.index].SetButtonColorFade(-1, color, GetByte2Msec(speed));

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Bd15070_4IF), "_setColorMultiFet")]
    public static void Pre_setColorMultiFet(Color32 color, Bd15070_4IF.InitParam ____initParam)
    {
        _ledManagers[____initParam.index].SetBodyIntensity(8, color.r);
        _ledManagers[____initParam.index].SetBodyIntensity(9, color.g);
        _ledManagers[____initParam.index].SetBodyIntensity(10, color.b);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Bd15070_4IF), "_setColorFet")]
    public static void Pre_setColorFet(byte ledPos, byte color, Bd15070_4IF.InitParam ____initParam) =>
        _ledManagers[____initParam.index].SetBodyIntensity(ledPos, color);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Bd15070_4IF), "_setLedAllOff")]
    public static void Pre_setLedAllOff(Bd15070_4IF.InitParam ____initParam)
    {
        _ledManagers[____initParam.index].SetBodyIntensity(-1, 0);
        _ledManagers[____initParam.index].SetButtonColor(-1, new Color32(0, 0, 0, byte.MaxValue));
    }

    private static long GetByte2Msec(byte speed) => (long)Math.Round(4095.0 / speed * 8.0);
}
