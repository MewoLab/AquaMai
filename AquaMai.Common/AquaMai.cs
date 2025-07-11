using System;
using System.Reflection;
using System.Runtime.InteropServices;
using HarmonyLib;

namespace AquaMai.Common;

public class BootstrapOptions
{
    public required Assembly CurrentAssembly { get; set; }
    public required Harmony Harmony { get; set; }

    public required Action<string> MsgStringAction { get; set; }
    public required Action<object> MsgObjectAction { get; set; }
    public required Action<string> ErrorStringAction { get; set; }
    public required Action<object> ErrorObjectAction { get; set; }
    public required Action<string> WarningStringAction { get; set; }
    public required Action<object> WarningObjectAction { get; set; }
}

public class AquaMai
{
    public const string AQUAMAI_SAY = """
                                      如果你在 dnSpy / ILSpy 里看到了这行字，请从 resources 中解包 DLLs。
                                      If you see this line in dnSpy / ILSpy, please unpack the DLLs from resources.
                                      """;
    private static BootstrapOptions Options { get; set; }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleOutputCP(uint wCodePageID);

    private static void SetCoreBuildInfo(Assembly coreAssembly)
    {
        var coreBuildInfo = coreAssembly.GetType("AquaMai.Core.BuildInfo");
        var buildInfo = typeof(BuildInfo);
        foreach (var field in buildInfo.GetFields())
        {
            coreBuildInfo.GetField(field.Name)?.SetValue(null, field.GetValue(null));
        }
        coreBuildInfo.GetField("ModAssembly")?.SetValue(null, Options.CurrentAssembly);
    }

    private static void SetCoreLogger(Assembly coreAssembly)
    {
        var coreLogger = coreAssembly.GetType("AquaMai.Core.Environment.MelonLogger");
        coreLogger.GetField("MsgStringAction").SetValue(null, Options.MsgStringAction);
        coreLogger.GetField("MsgObjectAction").SetValue(null, Options.MsgObjectAction);
        coreLogger.GetField("ErrorStringAction").SetValue(null, Options.ErrorStringAction);
        coreLogger.GetField("ErrorObjectAction").SetValue(null, Options.ErrorObjectAction);
        coreLogger.GetField("WarningStringAction").SetValue(null, Options.WarningStringAction);
        coreLogger.GetField("WarningObjectAction").SetValue(null, Options.WarningObjectAction);
    }

    private static MethodInfo onGUIMethod;

    public static void EarlyStageLog(string message)
    {
        Options.MsgStringAction?.Invoke(message);
    }

    public static void Bootstrap(BootstrapOptions options)
    {
        Options = options;
        try
        {
            // Prevent Chinese characters from being garbled
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) SetConsoleOutputCP(65001);

            AssemblyLoader.LoadAssemblies();

            var modsAssembly = AssemblyLoader.GetAssembly(AssemblyLoader.AssemblyName.Mods);
            var coreAssembly = AssemblyLoader.GetAssembly(AssemblyLoader.AssemblyName.Core);
            SetCoreBuildInfo(coreAssembly);
            SetCoreLogger(coreAssembly);
            var startupMethod = coreAssembly.GetType("AquaMai.Core.Startup")
                                            .GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
            onGUIMethod = coreAssembly.GetType("AquaMai.Core.Startup")
                                    .GetMethod("OnGUI", BindingFlags.Public | BindingFlags.Static);

            startupMethod.Invoke(null, [modsAssembly, options.Harmony]);
        }
        catch (Exception ex)
        {
            Options.ErrorObjectAction?.Invoke(ex);
        }
    }

    public static void OnGUI()
    {
        try
        {
            onGUIMethod?.Invoke(null, []);
        }
        catch (Exception ex)
        {
            Options.ErrorObjectAction?.Invoke(ex);
        }
    }
}
