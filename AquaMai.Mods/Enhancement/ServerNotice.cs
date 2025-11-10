using System.Linq;
using AquaMai.Config.Attributes;
using AquaMai.Core.Attributes;
using AquaMai.Core.Helpers;
using AquaMai.Mods.Types;
using JetBrains.Annotations;
using MelonLoader;
using MelonLoader.TinyJSON;

namespace AquaMai.Mods.Enhancement;

[ConfigSection(
    defaultOn: true,
    exampleHidden: true,
    en: """
        Show extra information from compatible server
        (no side effects for other servers, no extra requests made)
        """,
    zh: """
        在用户登录之类的时候，显示来自兼容服务器的额外信息
        （对其他服务器无副作用，不会发出额外请求）
        """)]
[EnableGameVersion(23000)]
public class ServerNotice
{
    private const string FieldName = "_aquaMaiServerNotice";

    private class ServerNoticeEntry : ConditionalMessage
    {
        [CanBeNull] public string title = null;
        [CanBeNull] public string content = null;
    }

    private class ServerNoticeData
    {
        public ServerNoticeEntry[] entries = [];
    }

    public static void OnBeforePatch()
    {
        NetPacketHook.OnNetPacketComplete += OnNetPacketComplete;
    }

    private static Variant OnNetPacketComplete(string api, Variant request, Variant response)
    {
        if (response is not ProxyObject obj) return null;
        var serverNoticeJson = obj.Keys.Contains(FieldName) ? obj[FieldName] : null;
        if (serverNoticeJson == null) return null;

        var data = serverNoticeJson.Make<ServerNoticeData>();
        var entry = data?.entries.FirstOrDefault(it => it.ShouldShow());
        if (entry == null) return null;

        MelonLogger.Msg($"[ServerNotice] {entry.title}: {entry.content}");
        MessageHelper.ShowMessage(entry.content, title: entry.title);
        return null;
    }
}