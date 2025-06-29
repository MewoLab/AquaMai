using System;
using System.Reflection;
using AquaMai.Common;
using BepInEx;
using BepInEx.Logging;
using Manager;

namespace AquaMai.BepInEx;

[BepInPlugin(PluginName, BuildInfo.Name, BuildInfo.Version)]
public class Plugin : BaseUnityPlugin
{
    public const string PluginName = "net.aquadx.aquamai";

    public readonly static ManualLogSource LogSource = global::BepInEx.Logging.Logger.CreateLogSource(BuildInfo.Name);

    public void Awake()
    {
        var harmony = new HarmonyLib.Harmony(PluginName);

        Common.AquaMai.Bootstrap(new BootstrapOptions
        {
            CurrentAssembly = Assembly.GetExecutingAssembly(),
            Harmony = harmony,
            MsgStringAction = LogSource.LogMessage,
            MsgObjectAction = LogSource.LogMessage,
            ErrorStringAction = LogSource.LogError,
            ErrorObjectAction = LogSource.LogError,
            WarningStringAction = LogSource.LogWarning,
            WarningObjectAction = LogSource.LogWarning,
        });
    }

    public void OnGUI()
    {
        Common.AquaMai.OnGUI();
    }
}
