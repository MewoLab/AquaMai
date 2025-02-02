using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using AMDaemon;
using AquaMai.Config.Attributes;
using HarmonyLib;
using HidLibrary;
using MelonLoader;
using UnityEngine;

namespace AquaMai.Mods.GameSystem;

[ConfigSection(
    en: "Input using ADX HID firmware (do not enable if you are not using ADX's HID firmware, be sure to delete the existing HID related DLL when enabled)",
    zh: "使用 ADX HID 固件的自定义输入（如果你没有使用 ADX 的 HID 固件，请不要启用。启用时请务必删除现有 HID 相关 DLL）")]
public class AdxHidInput
{
    private static HidDevice adxController1P = null;
    private static HidDevice adxController2P = null;
    private static byte[] inputBuf1P = new byte[32];
    private static byte[] inputBuf2P = new byte[32];

    private static void HidInputThread()
    {
        MelonLogger.Msg("[HidInput] =======    HID Input Start   =======");

        while (true)
        {
            if (adxController1P != null)
            {
                var report1P = adxController1P.Read();
                if (report1P.Status == HidDeviceData.ReadStatus.Success)
                    inputBuf1P = report1P.Data;
            }

            if (adxController2P != null)
            {
                var report2P = adxController2P.Read();
                if (report2P.Status == HidDeviceData.ReadStatus.Success)
                    inputBuf2P = report2P.Data;
            }
        }
    }

    public static void OnBeforePatch()
    {
        adxController1P = HidDevices.Enumerate(0x2E3C, 0x5750).FirstOrDefault();
        adxController2P = HidDevices.Enumerate(0x2E4C, 0x5750).FirstOrDefault();

        if (adxController1P == null)
        {
            MelonLogger.Msg("[HidInput] Open HID 1P Failed");
        }
        else
        {
            MelonLogger.Msg("[HidInput] Open HID 1P OK");
        }

        if (adxController2P == null)
        {
            MelonLogger.Msg("[HidInput] Open HID 2P Failed");
        }
        else
        {
            MelonLogger.Msg("[HidInput] Open HID 2P OK");
        }

        if (adxController1P != null || adxController2P != null)
        {
            Thread hidThread = new Thread(HidInputThread);
            hidThread.Start();
        }
    }

    private static bool GetPushedByButton(int playerNo, InputId inputId)
    {
        if (inputId.Value == "test") return inputBuf1P[13] == 1 || inputBuf2P[13] == 1;
        if (inputId.Value == "service") return inputBuf1P[11] == 1 || inputBuf2P[11] == 1;
        if (inputId.Value == "select" && playerNo == 0) return inputBuf1P[10] == 1 || inputBuf2P[10] == 1;
        if (inputId.Value == "select" && playerNo == 1) return inputBuf1P[12] == 1 || inputBuf2P[12] == 1;

        var buf = playerNo == 0 ? inputBuf1P : inputBuf2P;
        if (inputId.Value == "button_01") return buf[5] == 1;
        if (inputId.Value == "button_02") return buf[4] == 1;
        if (inputId.Value == "button_03") return buf[3] == 1;
        if (inputId.Value == "button_04") return buf[2] == 1;
        if (inputId.Value == "button_05") return buf[9] == 1;
        if (inputId.Value == "button_06") return buf[8] == 1;
        if (inputId.Value == "button_07") return buf[7] == 1;
        if (inputId.Value == "button_08") return buf[6] == 1;
        return false;
    }

    [HarmonyPatch]
    public static class Hook
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            var jvsSwitch = typeof(IO.Jvs).GetNestedType("JvsSwitch", BindingFlags.NonPublic | BindingFlags.Public);
            return [jvsSwitch.GetMethod("Execute")];
        }

        public static bool Prefix(int ____playerNo, InputId ____inputId, ref bool ____isStateOnOld2, ref bool ____isStateOnOld, ref bool ____isStateOn, ref bool ____isTriggerOn, ref bool ____isTriggerOff, KeyCode ____subKey)
        {
            var flag = GetPushedByButton(____playerNo, ____inputId);
            // 不影响键盘
            if (!flag) return true;

            var isStateOnOld2 = ____isStateOnOld;
            var isStateOnOld = ____isStateOn;

            if (isStateOnOld2 && !isStateOnOld)
            {
                return true;
            }

            ____isStateOn = true;
            ____isTriggerOn = !isStateOnOld;
            ____isTriggerOff = false;
            ____isStateOnOld2 = isStateOnOld2;
            ____isStateOnOld = isStateOnOld;
            return false;
        }
    }
}
