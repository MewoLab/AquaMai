using System;
using System.Linq;
using System.Reflection;
using AMDaemon;
using AquaMai.Config.Attributes;
using AquaMai.Core.Helpers;
using AquaMai.Mods.GameSystem.MaimollerIO.Libs;
using HarmonyLib;
using IO;
using Main;
using Manager;
using Mecha;
using MelonLoader;
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
        name: "启用 1P",
        en: "Enable 1P (If you mix Maimoller with other protocols, please disable the side that is not Maimoller)",
        zh: "启用 1P（如果混用 Maimoller 与其他协议，请对不是 Maimoller 的一侧禁用）")]
    private static readonly bool p1 = true;

    [ConfigEntry(
        name: "启用 2P",
        en: "Enable 2P (If you mix Maimoller with other protocols, please disable the side that is not Maimoller)",
        zh: "启用 2P（如果混用 Maimoller 与其他协议，请对不是 Maimoller 的一侧禁用）")]
    private static readonly bool p2 = true;

    private static bool ShouldEnableForPlayer(int playerNo) => playerNo switch
    {
        0 => p1,
        1 => p2,
        _ => false,
    };

    private static readonly MaimollerDevice[] _devices = [.. Enumerable.Range(0, 2).Select(i => new MaimollerDevice(i))];
    private static readonly MaimollerLedManager[] _ledManagers = [.. Enumerable.Range(0, 2).Select(i => new MaimollerLedManager(_devices[i].output))];

    public static void OnBeforePatch()
    {
        if (p1) _devices[0].Open();
        if (p2) _devices[1].Open();
        JvsSwitchHook.AddKeyChecker(GetPushedByButton);
    }

    // NOTE: Coin button is not supported yet. AquaMai recommands setting fixed number of credits directly in the configuration.
    private static bool GetPushedByButton(int playerNo, InputId inputId)
    {
        if (!ShouldEnableForPlayer(playerNo)) return false;
        return inputId.Value switch
        {
            "test" => _devices[playerNo].input.GetSwitchState(MaimollerInputReport.SwitchClass.SYSTEM, MaimollerInputReport.ButtonMask.TEST) != 0,
            "service" => _devices[playerNo].input.GetSwitchState(MaimollerInputReport.SwitchClass.SYSTEM, MaimollerInputReport.ButtonMask.SERVICE) != 0,
            "select" => _devices[playerNo].input.GetSwitchState(MaimollerInputReport.SwitchClass.SYSTEM, MaimollerInputReport.ButtonMask.SELECT) != 0,
            "button_01" => _devices[playerNo].input.GetSwitchState(MaimollerInputReport.SwitchClass.BUTTON, MaimollerInputReport.ButtonMask.BTN_1) != 0,
            "button_02" => _devices[playerNo].input.GetSwitchState(MaimollerInputReport.SwitchClass.BUTTON, MaimollerInputReport.ButtonMask.BTN_2) != 0,
            "button_03" => _devices[playerNo].input.GetSwitchState(MaimollerInputReport.SwitchClass.BUTTON, MaimollerInputReport.ButtonMask.BTN_3) != 0,
            "button_04" => _devices[playerNo].input.GetSwitchState(MaimollerInputReport.SwitchClass.BUTTON, MaimollerInputReport.ButtonMask.BTN_4) != 0,
            "button_05" => _devices[playerNo].input.GetSwitchState(MaimollerInputReport.SwitchClass.BUTTON, MaimollerInputReport.ButtonMask.BTN_5) != 0,
            "button_06" => _devices[playerNo].input.GetSwitchState(MaimollerInputReport.SwitchClass.BUTTON, MaimollerInputReport.ButtonMask.BTN_6) != 0,
            "button_07" => _devices[playerNo].input.GetSwitchState(MaimollerInputReport.SwitchClass.BUTTON, MaimollerInputReport.ButtonMask.BTN_7) != 0,
            "button_08" => _devices[playerNo].input.GetSwitchState(MaimollerInputReport.SwitchClass.BUTTON, MaimollerInputReport.ButtonMask.BTN_8) != 0,
            _ => false,
        };
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameMain), "Update")]
    public static void PreGameMainUpdate(bool ____isInitialize)
    {
        if (!____isInitialize) return;
        for (int i = 0; i < 2; i++)
        {
            if (!ShouldEnableForPlayer(i)) continue;
            _devices[i].Update();
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NewTouchPanel), "Execute")]
    public static bool PreNewTouchPanelExecute(uint ____monitorIndex, ref uint ____dataCounter)
    {
        int i = (int)____monitorIndex;
        if (!ShouldEnableForPlayer(i)) return true;
        ulong s = 0;
        s |= (ulong)_devices[i].input.GetSwitchState(MaimollerInputReport.SwitchClass.TOUCH_A, MaimollerInputReport.ButtonMask.ANY_PLAYER);
        s |= (ulong)_devices[i].input.GetSwitchState(MaimollerInputReport.SwitchClass.TOUCH_B, MaimollerInputReport.ButtonMask.ANY_PLAYER) << 8;
        s |= (ulong)_devices[i].input.GetSwitchState(MaimollerInputReport.SwitchClass.TOUCH_C, MaimollerInputReport.ButtonMask.ANY_TOUCH_C) << 16;
        s |= (ulong)_devices[i].input.GetSwitchState(MaimollerInputReport.SwitchClass.TOUCH_D, MaimollerInputReport.ButtonMask.ANY_PLAYER) << 18;
        s |= (ulong)_devices[i].input.GetSwitchState(MaimollerInputReport.SwitchClass.TOUCH_E, MaimollerInputReport.ButtonMask.ANY_PLAYER) << 26;
        InputManager.SetNewTouchPanel(____monitorIndex, s, ++____dataCounter);
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NewTouchPanel), "Start")]
    public static bool PreNewTouchPanelStart(uint ____monitorIndex, ref NewTouchPanel.StatusEnum ___Status, ref bool ____isRunning)
    {
        if (!ShouldEnableForPlayer((int)____monitorIndex)) return true;
        ___Status = NewTouchPanel.StatusEnum.Drive;
        ____isRunning = true;
        MelonLogger.Msg($"[MaimollerIO] NewTouchPanel Start {____monitorIndex + 1}P");
        return false;
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
