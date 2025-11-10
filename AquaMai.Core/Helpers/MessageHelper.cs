using DB;
using HarmonyLib;
using Manager;
using MelonLoader;
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

    public delegate void ShowMessageDelegate(string message, WindowSizeID size = WindowSizeID.Middle, string title = null, Sprite sprite = null, bool retain = false);

    public static ShowMessageDelegate ShowMessage = GameInfo.GameVersion > 22000
        ? _ShowMessage
        : (string message, WindowSizeID size = WindowSizeID.Middle, string title = null, Sprite sprite = null, bool retain = false) =>
        {
            MelonLogger.Warning($"[MessageHelper] ShowMessage is not supported in this game version. {title}: {message}");
        };

    private static void _ShowMessage(string message, WindowSizeID size = WindowSizeID.Middle, string title = null, Sprite sprite = null, bool retain = false)
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