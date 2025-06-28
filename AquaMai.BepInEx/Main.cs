using System;
using System.Reflection;
using System.Runtime.InteropServices;
using BepInEx;
using BepInEx.Logging;
using Manager;

namespace AquaMai;

[BepInPlugin("net.aquadx.aquamai", BuildInfo.Name, BuildInfo.GitVersion)]
public class AquaMai : BaseUnityPlugin
{
    public const string AQUAMAI_SAY = """
                                      如果你在 dnSpy / ILSpy 里看到了这行字，请从 resources 中解包 DLLs。
                                      If you see this line in dnSpy / ILSpy, please unpack the DLLs from resources.
                                      """;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleOutputCP(uint wCodePageID);

    private void SetCoreBuildInfo(Assembly coreAssembly)
    {
        var coreBuildInfo = coreAssembly.GetType("AquaMai.Core.BuildInfo");
        var buildInfo = typeof(BuildInfo);
        foreach (var field in buildInfo.GetFields())
        {
            coreBuildInfo.GetField(field.Name)?.SetValue(null, field.GetValue(null));
        }
        coreBuildInfo.GetField("ModAssembly")?.SetValue(null, Assembly.GetExecutingAssembly());
    }

    public readonly static ManualLogSource LogSource = BepInEx.Logging.Logger.CreateLogSource(BuildInfo.Name);
    private void SetCoreLogger(Assembly coreAssembly)
    {
        var coreLogger = coreAssembly.GetType("AquaMai.Core.Environment.MelonLogger");
        coreLogger.GetField("MsgStringAction").SetValue(null, (Action<string>)LogSource.LogMessage);
        coreLogger.GetField("MsgObjectAction").SetValue(null, (Action<object>)LogSource.LogMessage);
        coreLogger.GetField("ErrorStringAction").SetValue(null, (Action<string>)LogSource.LogError);
        coreLogger.GetField("ErrorObjectAction").SetValue(null, (Action<object>)LogSource.LogError);
        coreLogger.GetField("WarningStringAction").SetValue(null, (Action<string>)LogSource.LogWarning);
        coreLogger.GetField("WarningObjectAction").SetValue(null, (Action<object>)LogSource.LogWarning);
    }

    private static MethodInfo onGUIMethod;

    public void Awake()
    {
        var harmony = new HarmonyLib.Harmony("net.aquadx.aquamai");

        // Early initialization of AMDaemon.NET
        try
        {
            AmManager.Instance.Initialize();
            harmony.PatchAll(typeof(AquaMai));
        }
        catch (Exception ex)
        {
            LogSource.LogError($"AMDaemon.NET early initialization failed: {ex.Message}");
            return;
        }

        // Prevent Chinese characters from being garbled
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) SetConsoleOutputCP(65001);

        AssemblyLoader.LoadAssemblies();

        var modsAssembly = AssemblyLoader.GetAssembly(AssemblyLoader.AssemblyName.Mods);
        var coreAssembly = AssemblyLoader.GetAssembly(AssemblyLoader.AssemblyName.Core);
        SetCoreBuildInfo(coreAssembly);
        SetCoreLogger(coreAssembly);
        coreAssembly.GetType("AquaMai.Core.Startup")
                    .GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static)
                    .Invoke(null, [modsAssembly, harmony]);
        onGUIMethod = coreAssembly.GetType("AquaMai.Core.Startup")
                                  .GetMethod("OnGUI", BindingFlags.Public | BindingFlags.Static);
    }

    public void OnGUI()
    {
        onGUIMethod?.Invoke(null, []);
    }

    [HarmonyLib.HarmonyPatch(typeof(AmManager), "Initialize")]
    [HarmonyLib.HarmonyPrefix]
    public static bool PreAmManagerInitialize()
    {
        return false; // Prevent AMDaemon.NET from initializing again
    }
}
