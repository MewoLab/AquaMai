using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using AMDaemon;
using AquaMai.Config.Attributes;
using AquaMai.Config.Types;
using HarmonyLib;
using HidLibrary;
using MelonLoader;
using UnityEngine;

namespace AquaMai.Mods.GameSystem;

[ConfigSection(
    name: "ADX HID 输入",
    en: "Input using ADX HID firmware (do not enable if you are not using ADX's HID firmware, be sure to delete the existing HID related DLL when enabled)",
    zh: "使用 ADX HID 固件的自定义输入（如果你没有使用 ADX 的 HID 固件，请不要启用。启用时请务必删除现有 HID 相关 DLL）")]
public class AdxHidInput
{
    private static HidDevice[] adxController = new HidDevice[2];
    private static byte[,] inputBuf = new byte[2, 32];

    private static void HidInputThread(int p)
    {
        while (true)
        {
            if (adxController[p] == null) continue;
            var report1P = adxController[p].Read();
            if (report1P.Status != HidDeviceData.ReadStatus.Success || report1P.Data.Length <= 13) continue;
            for (int i = 0; i < 14; i++)
            {
                inputBuf[p, i] = report1P.Data[i];
            }
        }
    }

    public static void OnBeforePatch()
    {
        adxController[0] = HidDevices.Enumerate(0x2E3C, 0x5750).FirstOrDefault();
        adxController[1] = HidDevices.Enumerate(0x2E4C, 0x5750).FirstOrDefault();

        if (adxController[0] == null)
        {
            MelonLogger.Msg("[HidInput] Open HID 1P Failed");
        }
        else
        {
            MelonLogger.Msg("[HidInput] Open HID 1P OK");
        }

        if (adxController[1] == null)
        {
            MelonLogger.Msg("[HidInput] Open HID 2P Failed");
        }
        else
        {
            MelonLogger.Msg("[HidInput] Open HID 2P OK");
        }

        for (int i = 0; i < 2; i++)
        {
            if (adxController[i] == null) continue;
            var p = i;
            Thread hidThread = new Thread(() => HidInputThread(p));
            hidThread.Start();
        }
    }

    [ConfigEntry(name: "按钮 1（向上的三角键）")]
    private static readonly AdxKeyMap button1 = AdxKeyMap.Select1P;

    [ConfigEntry(name: "按钮 2（三角键中间的圆形按键）")]
    private static readonly AdxKeyMap button2 = AdxKeyMap.Service;

    [ConfigEntry(name: "按钮 3（向下的三角键）")]
    private static readonly AdxKeyMap button3 = AdxKeyMap.Select2P;

    [ConfigEntry(name: "按钮 4（最下方的圆形按键）")]
    private static readonly AdxKeyMap button4 = AdxKeyMap.Test;

    private static bool GetPushedByButton(int playerNo, InputId inputId)
    {
        var current = inputId.Value switch
        {
            "test" => AdxKeyMap.Test,
            "service" => AdxKeyMap.Service,
            "select" when playerNo == 0 => AdxKeyMap.Select1P,
            "select" when playerNo == 1 => AdxKeyMap.Select2P,
            _ => AdxKeyMap.None,
        };

        AdxKeyMap[] arr = [button1, button2, button3, button4];
        if (current != AdxKeyMap.None)
        {
            for (int i = 0; i < 4; i++)
            {
                if (arr[i] != current) continue;
                var keyIndex = 10 + i;
                if (inputBuf[0, keyIndex] == 1 || inputBuf[1, keyIndex] == 1)
                {
                    return true;
                }
            }
            return false;
        }

        return inputId.Value switch
        {
            "button_01" => inputBuf[playerNo, 5] == 1,
            "button_02" => inputBuf[playerNo, 4] == 1,
            "button_03" => inputBuf[playerNo, 3] == 1,
            "button_04" => inputBuf[playerNo, 2] == 1,
            "button_05" => inputBuf[playerNo, 9] == 1,
            "button_06" => inputBuf[playerNo, 8] == 1,
            "button_07" => inputBuf[playerNo, 7] == 1,
            "button_08" => inputBuf[playerNo, 6] == 1,
            _ => false,
        };
    }

    [HarmonyPatch]
    public static class Hook
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            var jvsSwitch = typeof(IO.Jvs).GetNestedType("JvsSwitch", BindingFlags.NonPublic | BindingFlags.Public);
            return [jvsSwitch.GetMethod("Execute")];
        }

        public static bool Prefix(
            int ____playerNo,
            InputId ____inputId,
            ref bool ____isStateOnOld2,
            ref bool ____isStateOnOld,
            ref bool ____isStateOn,
            ref bool ____isTriggerOn,
            ref bool ____isTriggerOff,
            KeyCode ____subKey)
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