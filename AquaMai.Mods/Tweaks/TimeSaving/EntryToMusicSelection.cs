using AquaMai.Config.Attributes;
using DB;
using HarmonyLib;
using MAI2.Util;
using Manager;
using Monitor;
using Monitor.ModeSelect;
using Process;
using Process.Information;

namespace AquaMai.Mods.Tweaks.TimeSaving;

[ConfigSection(
    en: "Directly enter the song selection screen after login.",
    zh: "登录完成后直接进入选歌界面")]
public class EntryToMusicSelection
{
    private static int[] _timers = new int[2];

    private static CommonValue[] _volumeFadeIns = { new CommonValue(), new CommonValue() };

    private static readonly bool[] _volumeFadeInState = new bool[2];

    /*
     * Highly experimental, may well break some stuff
     * Works by overriding the info screen (where it shows new events and stuff)
     * to directly exit to the music selection screen, skipping character and
     * event selection, among others
     */
    [HarmonyPrefix]
    [HarmonyPatch(typeof(InformationProcess), "OnUpdate")]
    public static bool OnUpdate(InformationProcess __instance, ProcessDataContainer ___container)
    {
        GameManager.SetMaxTrack();
        ___container.processManager.AddProcess(new MusicSelectProcess(___container));
        ___container.processManager.ReleaseProcess(__instance);
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(MapResultMonitor), "Initialize")]
    public static void MapResultMonitorPreInitialize(int monIndex)
    {
        var userData = Singleton<UserDataManager>.Instance.GetUserData(monIndex);
        var index = userData.MapList.FindIndex((m) => m.ID == userData.Detail.SelectMapID);
        if (index >= 0) return;
        userData.MapList.Clear();
    }

    // Gradually increase headphone volume
    [HarmonyPrefix]
    [HarmonyPatch(typeof(MusicSelectProcess), "OnUpdate")]
    public static void MusicSelectProcessOnUpdate()
    {
        for (var i = 0; i < 2; i++)
        {
            if (_volumeFadeInState[i] && _timers[i] == 0) continue;

            if (_timers[i] > 0)
            {
                _timers[i]--;
            }

            if (_volumeFadeInState[i])
            {
                if (_timers[i] == 0)
                {
                    SoundManager.SetHeadPhoneVolume(i, _volumeFadeIns[i].end);
                }
                else if (!_volumeFadeIns[i].UpdateValue())
                {
                    SoundManager.SetHeadPhoneVolume(i, _volumeFadeIns[i].current);
                }
            }
            else
            {
                var userData = UserDataManager.Instance.GetUserData(i);
                if (!userData.IsEntry) continue;
                var value = userData.Option.HeadPhoneVolume.GetValue();
                if (GameManager.IsSelectContinue[i])
                {
                    _volumeFadeIns[i].start = value;
                    _volumeFadeIns[i].current = value;
                }
                else
                {
                    _volumeFadeIns[i].start = 0.05f;
                    _volumeFadeIns[i].current = 0.05f;
                }

                _volumeFadeIns[i].end = value;
                _volumeFadeIns[i].diff = (_volumeFadeIns[i].end - _volumeFadeIns[i].start) / 90f;
                _timers[i] = 90;
                _volumeFadeInState[i] = true;
            }
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameOverProcess), "OnStart")]
    public static void GameOverProcessOnStart()
    {
        for (var i = 0; i < 2; i++)
        {
            _volumeFadeInState[i] = false;
        }
    }
}
