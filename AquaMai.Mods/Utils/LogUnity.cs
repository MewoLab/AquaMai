using AquaMai.Config.Attributes;
using MelonLoader;
using UnityEngine;

namespace AquaMai.Mods.Utils;

[ConfigSection(
    name: "Unity 日志",
    en: "Output Unity logs (for debugging purposes)",
    zh: "输出 Unity 日志（调试用）",
    exampleHidden: true)]
public static class LogUnity
{
    public static void OnBeforePatch()
    {
        Application.logMessageReceived += Log;
    }

    private static void Log(string msg, string stackTrace, LogType type)
    {
        MelonLogger.Msg("[Unity] " + msg);
        MelonLogger.Msg("[Unity] " + stackTrace);
    }
}