using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using AMDaemon;
using AquaMai.Config.Attributes;
using AquaMai.Config.Types;
using AquaMai.Core.Attributes;
using HarmonyLib;
using HidLibrary;
using MelonLoader;
using UnityEngine;
using EnableConditionOperator = AquaMai.Core.Attributes.EnableConditionOperator;

namespace AquaMai.Mods.GameSystem;

[ConfigSection(
    name: "ADX HID 输入",
    defaultOn: true,
    en: "Input using ADX HID firmware (If you are not using ADX's HID firmware, enabling this won't do anything)",
    zh: "使用 ADX HID 固件的自定义输入（没有 ADX 的话开了也不会加载，也没有坏处）")]
public class AdxHidInput
{
    private static HidDevice[] adxController = new HidDevice[2];
    private static byte[,] inputBuf = new byte[2, 32];
    private static double[] td = [0, 0];
    private static bool tdEnabled;

    private static void HidInputThread(int p)
    {
        while (true)
        {
            if (adxController[p] == null) return;
            var report1P = adxController[p].Read();
            if (report1P.Status != HidDeviceData.ReadStatus.Success || report1P.Data.Length <= 13) continue;
            for (int i = 0; i < 14; i++)
            {
                inputBuf[p, i] = report1P.Data[i];
            }
        }
    }

    private static void TdInit(int p)
    {
        adxController[p].OpenDevice();
        var arr = new byte[64];
        arr[0] = 71;
        adxController[p].WriteReportSync(new HidReport(64)
        {
            ReportId = 1,
            Data = arr,
        });
        Thread.Sleep(100);
        var rpt = adxController[p].ReadReportSync(1);
        if (rpt.Data[0] != 71)
        {
            MelonLogger.Msg($"[HidInput] TD Init {p} Failed");
            return;
        }
        if (rpt.Data[5] < 110) return;
        arr[0] = 0x73;
        adxController[p].WriteReportSync(new HidReport(64)
        {
            ReportId = 1,
            Data = arr,
        });
        Thread.Sleep(100);
        rpt = adxController[p].ReadReportSync(1);
        if (rpt.Data[0] != 0x73)
        {
            MelonLogger.Msg($"[HidInput] TD Init {p} Failed");
            return;
        }
        if (rpt.Data[2] == 0) return;
        td[p] = rpt.Data[2] * 0.25;
        tdEnabled = true;
        MelonLogger.Msg($"[HidInput] TD Init {p} OK, {td[p]} ms");
    }

    public static void OnBeforePatch(HarmonyLib.Harmony h)
    {
        adxController[0] = HidDevices.Enumerate(0x2E3C, [0x5750, 0x5767]).FirstOrDefault(it => !it.DevicePath.EndsWith("kbd"));
        adxController[1] = HidDevices.Enumerate(0x2E4C, 0x5750).Concat(HidDevices.Enumerate(0x2E3C, 0x5768)).FirstOrDefault(it => !it.DevicePath.EndsWith("kbd"));

        if (adxController[0] != null)
        {
            MelonLogger.Msg("[HidInput] Open HID 1P OK");
        }

        if (adxController[1] != null)
        {
            MelonLogger.Msg("[HidInput] Open HID 2P OK");
        }

        var inputEnabled = false;
        for (int i = 0; i < 2; i++)
        {
            if (adxController[i] == null) continue;
            TdInit(i);
            if (adxController[i].Attributes.ProductId is 0x5767 or 0x5768) continue;
            if (io4Compact) continue;
            inputEnabled = true;
            var p = i;
            Thread hidThread = new Thread(() => HidInputThread(p));
            hidThread.Start();
        }
        if (inputEnabled)
        {
            h.PatchAll(typeof(Hook));
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

    [ConfigEntry("IO4 兼容模式", zh: "如果你不知道这是什么，请勿开启", hideWhenDefault: true)]
    private static readonly bool io4Compact = false;

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

    private static readonly Dictionary<uint, Queue<TouchData>> _queue = new();
    private static readonly object _lockObject = new object();

    private struct TouchData
    {
        public ulong Data;
        public uint Counter;
        public DateTimeOffset Timestamp;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Manager.InputManager), "SetNewTouchPanel")]
    [EnableIf(nameof(tdEnabled))]
    public static bool SetNewTouchPanel(uint index, ref ulong inputData, ref uint counter, ref bool __result)
    {
        var d = td[index];
        if (d <= 0)
        {
            return true;
        }

        lock (_lockObject)
        {
            var currentTime = DateTimeOffset.UtcNow;
            var dequeueCount = 0;

            if (!_queue.ContainsKey(index))
            {
                _queue[index] = new Queue<TouchData>();
            }

            _queue[index].Enqueue(new TouchData
            {
                Data = inputData,
                Counter = counter,
                Timestamp = currentTime,
            });

            var ret = false;
            foreach (var data in _queue[index])
            {
                if ((currentTime - data.Timestamp).TotalMilliseconds < d) break;
                ret = true;
                dequeueCount++;

                inputData = data.Data;
                counter = data.Counter;
            }

            for (var i = 0; i < dequeueCount; i++)
            {
                _queue[index].Dequeue();
            }

            return ret;
        }
    }
}