using System.Reflection;
using MelonLoader;

namespace AquaMai.MelonLoader;

public class AquaMai : MelonMod
{
    public override void OnInitializeMelon()
    {
        Common.AquaMai.Bootstrap(new Common.BootstrapOptions
        {
            CurrentAssembly = Assembly.GetExecutingAssembly(),
            Harmony = HarmonyInstance,
            MsgStringAction = MelonLogger.Msg,
            MsgObjectAction = MelonLogger.Msg,
            ErrorStringAction = MelonLogger.Error,
            ErrorObjectAction = MelonLogger.Error,
            WarningStringAction = MelonLogger.Warning,
            WarningObjectAction = MelonLogger.Warning,
        });
    }

    public override void OnGUI()
    {
        Common.AquaMai.OnGUI();
    }
}
