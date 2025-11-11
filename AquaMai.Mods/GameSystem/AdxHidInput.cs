using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AMDaemon;
using AquaMai.Config.Attributes;
using AquaMai.Config.Types;
using AquaMai.Core.Attributes;
using AquaMai.Core.Helpers;
using AquaMai.Mods.Tweaks;
using HarmonyLib;
using HidLibrary;
using MelonLoader;

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
        if (!LedBrightnessControl.shouldEnableImplicitly)
        {
            LedBrightnessControl.shouldEnableImplicitly = true;
            LedBrightnessControl.button1p *= 0.8f;
            LedBrightnessControl.button2p *= 0.8f;
            LedBrightnessControl.cabinet1p *= 0.8f;
            LedBrightnessControl.cabinet2p *= 0.8f;
        }
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

    public static void OnBeforeEnableCheck()
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
            JvsSwitchHook.RegisterButtonChecker(IsButtonPushed);
            JvsSwitchHook.RegisterAuxiliaryStateProvider(GetAuxiliaryState);
        }
    }

    private static bool IsButtonPushed(int playerNo, int buttonIndex1To8) => buttonIndex1To8 switch
    {
        1 => inputBuf[playerNo, 5] == 1,
        2 => inputBuf[playerNo, 4] == 1,
        3 => inputBuf[playerNo, 3] == 1,
        4 => inputBuf[playerNo, 2] == 1,
        5 => inputBuf[playerNo, 9] == 1,
        6 => inputBuf[playerNo, 8] == 1,
        7 => inputBuf[playerNo, 7] == 1,
        8 => inputBuf[playerNo, 6] == 1,
        _ => false,
    };

    [ConfigEntry(name: "按钮 1（向上的三角键）")]
    private static readonly IOKeyMap button1 = IOKeyMap.Select1P;

    [ConfigEntry(name: "按钮 2（三角键中间的圆形按键）")]
    private static readonly IOKeyMap button2 = IOKeyMap.Service;

    [ConfigEntry(name: "按钮 3（向下的三角键）")]
    private static readonly IOKeyMap button3 = IOKeyMap.Select2P;

    [ConfigEntry(name: "按钮 4（最下方的圆形按键）")]
    private static readonly IOKeyMap button4 = IOKeyMap.Test;

    [ConfigEntry("IO4 兼容模式", zh: "如果你不知道这是什么，请勿开启", hideWhenDefault: true)]
    private static readonly bool io4Compact = false;

    private static AuxiliaryState GetAuxiliaryState()
    {
        var auxiliaryState = new AuxiliaryState();
        Span<IOKeyMap> keyMaps = stackalloc IOKeyMap[4] { button1, button2, button3, button4 };
        for (int i = 0; i < 4; i++)
        {
            var keyIndex = 10 + i;
            var is1PPushed = inputBuf[0, keyIndex] == 1;
            var is2PPushed = inputBuf[1, keyIndex] == 1;
            switch (keyMaps[i])
            {
            case IOKeyMap.Select1P:
                auxiliaryState.select1P |= is1PPushed || is2PPushed;
                break;
            case IOKeyMap.Select2P:
                auxiliaryState.select2P |= is1PPushed || is2PPushed;
                break;
            case IOKeyMap.Select:
                auxiliaryState.select1P |= is1PPushed;
                auxiliaryState.select2P |= is2PPushed;
                break;
            case IOKeyMap.Service:
                auxiliaryState.service = is1PPushed || is2PPushed;
                break;
            case IOKeyMap.Test:
                auxiliaryState.test = is1PPushed || is2PPushed;
                break;
            }
        }
        return auxiliaryState;
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
