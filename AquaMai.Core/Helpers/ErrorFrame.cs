using System;
using HarmonyLib;
using Manager;
using MelonLoader;
using Monitor.Error;
using Process;
using Process.Error;
using TMPro;

namespace AquaMai.Core.Helpers;

public static class ErrorFrame
{
    private static int _customErrCode;
    private static string _customErrMsg;
    private static DateTime _customErrDate;
    
    public static void Show(ProcessBase process, int errCode, string errMsg)
    {
        _customErrCode = errCode;
        _customErrMsg = errMsg;
        _customErrDate = DateTime.Now;
        Show(process);
    }
    
    // Display the error frame with AMDaemon's original error message.
    public static void Show(ProcessBase process)
    {
        var tv = Traverse.Create(process);
        var ctn = tv.Field("container").GetValue<ProcessDataContainer>();
        ctn.processManager.AddProcess((ProcessBase) new ErrorProcess(ctn));
        ctn.processManager.ReleaseProcess(process);
        GameManager.IsErrorMode = true;
    }
    
    // patch the error monitor so that it can display custom error codes and messages.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ErrorMonitor), "Initialize", typeof(int), typeof(bool))]
    public static void PostInitialize(ErrorMonitor __instance)
    {
        var tv = Traverse.Create(__instance);
        if (_customErrCode == 0)
        {
            MelonLogger.Msg($"Displaying error frame with AMDaemon code {AMDaemon.Error.Number}: {AMDaemon.Error.Message}");
            return;
        }
        MelonLogger.Msg($"Displaying error frame with custom code {_customErrCode}: {_customErrMsg}");
        tv.Field("ErrorID").GetValue<TextMeshProUGUI>().text = _customErrCode.ToString().PadLeft(4, '0');
        tv.Field("ErrorMessage").GetValue<TextMeshProUGUI>().text = _customErrMsg;
        tv.Field("ErrorDate").GetValue<TextMeshProUGUI>().text = _customErrDate.ToString();
    }
    
}