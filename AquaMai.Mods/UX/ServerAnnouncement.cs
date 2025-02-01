using System.Collections;
using System.Linq;
using AquaMai.Config.Attributes;
using AquaMai.Core.Helpers;
using DB;
using HarmonyLib;
using JetBrains.Annotations;
using MelonLoader;
using MelonLoader.TinyJSON;
using Process;
using UnityEngine;
using UnityEngine.Networking;

namespace AquaMai.Mods.UX;

[ConfigSection(
    defaultOn: true,
    en: """
        Show announcement from the AquaDX server
        (no side effects for other servers, no extra requests made)
        """,
    zh: """
        显示来自 AquaDX 服务端的公告
        （对其他服务器无副作用，不会发出额外请求）
        """)]
public static class ServerAnnouncement
{
    private class ServerAnnouncementEntry
    {
        [CanBeNull] public string title = null;
        [CanBeNull] public string announcement = null;
        [CanBeNull] public string imageUrl = null;
        public bool showOnIdle = false;
        public bool showOnUserLogin = false;
        public string[] locales = [];
        [CanBeNull] public string minimumAquaMaiVersion = null;
        [CanBeNull] public string maximumAquaMaiVersion = null;
        public int minimumGameVersion = 0;
        public int maximumGameVersion = 0;
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
            if (ShouldShowAnnouncement(entry))
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

    private static bool ShouldShowAnnouncement(ServerAnnouncementEntry announcement)
    {
        if (announcement.locales != null && announcement.locales.Length != 0 && !announcement.locales.Contains(General.locale))
        {
            MelonLogger.Msg($"[ServerAnnouncement] Now showing announcement: Language {General.locale} not in {JSON.Dump(announcement.locales)}");
            return false;
        }

        var aquaMaiVersion = new System.Version(Core.BuildInfo.Version);
        if (announcement.minimumAquaMaiVersion != null && aquaMaiVersion < new System.Version(announcement.minimumAquaMaiVersion))
        {
            MelonLogger.Msg($"[ServerAnnouncement] Now showing announcement: AquaMai version {aquaMaiVersion} < {announcement.minimumAquaMaiVersion}");
            return false;
        }
        if (announcement.maximumAquaMaiVersion != null && aquaMaiVersion > new System.Version(announcement.maximumAquaMaiVersion))
        {
            MelonLogger.Msg($"[ServerAnnouncement] Now showing announcement: AquaMai version {aquaMaiVersion} > {announcement.maximumAquaMaiVersion}");
            return false;
        }
        
        var gameVersion = GameInfo.GameVersion;
        if (announcement.minimumGameVersion != 0 && gameVersion < announcement.minimumGameVersion)
        {
            MelonLogger.Msg($"[ServerAnnouncement] Now showing announcement: Game version {gameVersion} < {announcement.minimumGameVersion}");
            return false;
        }
        if (announcement.maximumGameVersion != 0 && gameVersion > announcement.maximumGameVersion)
        {
            MelonLogger.Msg($"[ServerAnnouncement] Now showing announcement: Game version {gameVersion} > {announcement.maximumGameVersion}");
            return false;
        }

        return true;
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
                _sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width / 4f);
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
        MessageHelper.ShowMessage(_announcement.announcement, title: _announcement.title, sprite: _sprite, size: WindowSizeID.LargeVerticalPostImage);
    }

    [HarmonyPatch(typeof(EntryProcess), "OnStart")]
    [HarmonyPostfix]
    private static void EntryProcessOnStart()
    {
        if (_announcement == null || !_announcement.showOnUserLogin) return;
        MessageHelper.ShowMessage(_announcement.announcement, title: _announcement.title, sprite: _sprite, size: WindowSizeID.LargeVerticalPostImage);
    }
}
