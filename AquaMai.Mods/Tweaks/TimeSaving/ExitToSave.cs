using AquaMai.Config.Attributes;
using HarmonyLib;
using Process;

namespace AquaMai.Mods.Tweaks.TimeSaving;

[ConfigSection(
    name: "游戏结束直接登出",
    en: "Skip uploading photos and collectibles after the game ends and log out directly.",
    zh: "游戏结束后跳过上传照片和收藏品直接登出")]
public class ExitToSave
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(PhotoEditProcess), nameof(PhotoEditProcess.OnUpdate))]
    public static bool SkipPhotoEditProcess(ProcessDataContainer ___container, PhotoEditProcess __instance)
    {
        ___container.processManager.AddProcess(new DataSaveProcess(___container));
        ___container.processManager.ReleaseProcess(__instance);
        return false;
    }
}