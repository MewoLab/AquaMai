using AquaMai.Config.Attributes;
using HarmonyLib;
using Process.AdvertiseCommercial;

namespace AquaMai.Mods.Fancy;

[ConfigSection(
    "无限闲置视频时长",
    defaultOn: true,
    en: "Ignores 5 mins total timeout on commercial screen.",
    zh: "忽略闲置时的视频广告画面的5分钟总时长限制")]
public class IgnoreCommercialTimeout
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(AdvertiseCommercialProcess), "CheckTotalTimeOut")]
    public static bool PreCheckTotalTimeOut()
    {
        return false;
    }
}
