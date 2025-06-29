using System;
using System.Reflection;
using AquaMai.Common;
using BepInEx;
using BepInEx.Logging;
using Manager;

namespace AquaMai.BepInEx;

[BepInPlugin(PluginName, BuildInfo.Name, BuildInfo.GitVersion)]
public class Plugin : BaseUnityPlugin
{
    public const string PluginName = "net.aquadx.aquamai";

    public readonly static ManualLogSource LogSource = global::BepInEx.Logging.Logger.CreateLogSource(BuildInfo.Name);

    public void Awake()
    {
        var harmony = new HarmonyLib.Harmony(PluginName);

        // Early initialization of AMDaemon.NET
        try
        {
            AmManager.Instance.Initialize();
            harmony.PatchAll(typeof(Plugin));
        }
        catch (Exception ex)
        {
            LogSource.LogError($"AMDaemon.NET early initialization failed: {ex.Message}");
            return;
        }

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

    [HarmonyLib.HarmonyPatch(typeof(AmManager), "Initialize")]
    [HarmonyLib.HarmonyPrefix]
    public static bool PreAmManagerInitialize()
    {
        return false; // Prevent AMDaemon.NET from initializing again
    }
}
