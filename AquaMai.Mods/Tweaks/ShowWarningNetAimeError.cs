using AquaMai.Config.Attributes;
using HarmonyLib;
using Manager;

namespace AquaMai.Mods.Tweaks;

[ConfigSection(
    en: "Show warning network icon if aime server has issue.",
    zh: "�����������������Aime��������ʾ���������鿪��")]
public class ShowWarningNetAimeError
{
    [HarmonyPatch(typeof(OperationManager), "NetIconStatus", MethodType.Getter)]
    [HarmonyPrefix]
    public static bool Prefix(ref bool __result)
    {
        if (!this.IsAliveServer || !this.IsAliveAime)
        {
            if (!this.WasDownloadSuccessOnce)
            {
                __result = OperationManager.NetStatus.Offline;
                return false;
            }
            __result = OperationManager.NetStatus.LimitedOnline;
            return false;
        }
        __result = OperationManager.NetStatus.Online;
        return false;
    }
}