using System.Collections;
using System.Linq;
using AquaMai.Config.Attributes;
using AquaMai.Core.Attributes;
using AquaMai.Core.Helpers;
using AquaMai.Mods.Types;
using DB;
using HarmonyLib;
using JetBrains.Annotations;
using MelonLoader;
using MelonLoader.TinyJSON;
using Process;
using UnityEngine;
using UnityEngine.Networking;

namespace AquaMai.Mods.Enhancement;

[ConfigSection(
    defaultOn: true,
    exampleHidden: true,
    en: """
        Show announcement from compatible server
        (no side effects for other servers, no extra requests made)
        """,
    zh: """
        显示来自兼容服务端的公告
        （对其他服务器无副作用，不会发出额外请求）
        """)]
[EnableGameVersion(23000)]
public static class ServerAnnouncement
{
    private class ServerAnnouncementEntry : ConditionalMessage
    {
        [CanBeNull] public string title = null;
        [CanBeNull] public string announcement = null;
        [CanBeNull] public string imageUrl = null;
        public float imageSizeFactor = 9;
        public bool showOnIdle = false;
        public bool showOnUserLogin = false;
        public bool persistent = false;
    }

    private class ServerAnnouncementData
    {
        public ServerAnnouncementEntry[] entries = [];
    }

    private const string FieldName = "_aquaMaiServerAnnouncement";
    private static ServerAnnouncementEntry _announcement;
    private static Sprite _sprite;

    public static void OnBeforePatch()
    {
        NetPacketHook.OnNetPacketComplete += OnNetPacketComplete;
    }

    private static Variant OnNetPacketComplete(string api, Variant _, Variant response)
    {
        if (api != "GetGameSettingApi" || response is not ProxyObject obj) return null;
        var serverAnnouncementJson = obj.Keys.Contains(FieldName) ? obj[FieldName] : null;
        if (serverAnnouncementJson == null) return null;

        var serverAnnouncementData = serverAnnouncementJson.Make<ServerAnnouncementData>();
        ServerAnnouncementEntry chosenAnnouncement = null;
        foreach (var entry in serverAnnouncementData.entries)
        {
            if (entry.ShouldShow())
            {
                chosenAnnouncement = entry;
                break;
            }
        }

        if (chosenAnnouncement != null)
        {
            MelonLogger.Msg($"[ServerAnnouncement] {chosenAnnouncement.title}: {chosenAnnouncement.announcement}");
        }

        var oldImageUrl = _announcement?.imageUrl;
        var newImageUrl = chosenAnnouncement?.imageUrl;
        if (oldImageUrl != newImageUrl)
        {
            _sprite = null;
            if (!string.IsNullOrWhiteSpace(newImageUrl))
            {
                new GameObject("[AquaMai] ServerAnnouncement - DownloadTexture").AddComponent<DownloadTexture>();
            }
        }

        _announcement = chosenAnnouncement;

        return null;
    }

    private class DownloadTexture : MonoBehaviour
    {
        private void Start()
        {
            StartCoroutine(GetTexture());
        }

        private IEnumerator GetTexture()
        {
            var www = UnityWebRequestTexture.GetTexture(_announcement.imageUrl);
            yield return www.SendWebRequest();

            if (www.isNetworkError || www.isHttpError)
            {
                MelonLogger.Error($"[ServerAnnouncement] Failed to download image: {www.error}");
            }
            else
            {
                var texture = DownloadHandlerTexture.GetContent(www);
                _sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width / _announcement.imageSizeFactor);
#if DEBUG
                MelonLogger.Msg($"[ServerAnnouncement] Downloaded image success");
#endif
            }

            Destroy(gameObject);
        }
    }

    [HarmonyPatch(typeof(AdvertiseProcess), "OnStart")]
    [HarmonyPostfix]
    private static void AdvertiseProcessOnStart()
    {
        if (_announcement == null || !_announcement.showOnIdle) return;
        MessageHelper.ShowMessage(_announcement.announcement, title: _announcement.title, sprite: _sprite, size: WindowSizeID.LargeVerticalPostImage, retain: _announcement.persistent);
    }

    [HarmonyPatch(typeof(EntryProcess), "OnStart")]
    [HarmonyPostfix]
    private static void EntryProcessOnStart()
    {
        if (_announcement == null || !_announcement.showOnUserLogin) return;
        MessageHelper.ShowMessage(_announcement.announcement, title: _announcement.title, sprite: _sprite, size: WindowSizeID.LargeVerticalPostImage);
    }
}