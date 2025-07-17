using DB;
using HarmonyLib;
using Manager;
using AquaMai.Core.Environment;
using Process;
using UnityEngine;

namespace AquaMai.Core.Helpers;

public class MessageHelper
{
    private static IGenericManager _genericManager = null;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ProcessManager), "SetMessageManager")]
    private static void OnSetMessageManager(IGenericManager genericManager)
    {
        _genericManager = genericManager;
    }

    public static void ShowMessage(string message, WindowSizeID size = WindowSizeID.Middle, string title = null, Sprite sprite = null, bool retain = false)
    {
        if (_genericManager is null)
        {
            MelonLogger.Error($"[MessageHelper] Unable to show message: `{message}` GenericManager is null");
            return;
        }

        _genericManager.Enqueue(0, retain ? WindowMessageID.CollectionCategorySelectAnnounce : WindowMessageID.CollectionAttentionEmptyFavorite, new WindowParam()
        {
            hideTitle = title is null,
            replaceTitle = true,
            title = title,
            replaceText = true,
            text = message,
            changeSize = true,
            sizeID = size,
            directSprite = sprite is not null,
            sprite = sprite,
        });
    }
}
