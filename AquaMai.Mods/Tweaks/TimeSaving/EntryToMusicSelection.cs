using System.Collections;
using AquaMai.Config.Attributes;
using AquaMai.Core.Helpers;
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
        SharedInstances.GameMainObject.StartCoroutine(GraduallyIncreaseHeadphoneVolumeCoroutine());
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
    private static IEnumerator GraduallyIncreaseHeadphoneVolumeCoroutine()
    {
        CommonValue[] _volumeFadeIns = [null, null];

        for (var i = 0; i < 2; i++)
        {
            var userData = UserDataManager.Instance.GetUserData(i);
            if (!userData.IsEntry) continue;
            _volumeFadeIns[i] = new CommonValue();
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
        }

        yield return null;


        for (var timer = 90; timer >= 0; timer--)
        {
            for (var i = 0; i < 2; i++)
            {
                if (_volumeFadeIns[i] == null) continue;
                if (timer == 0)
                {
                    SoundManager.SetHeadPhoneVolume(i, _volumeFadeIns[i].end);
                }
                else if (!_volumeFadeIns[i].UpdateValue())
                {
                    SoundManager.SetHeadPhoneVolume(i, _volumeFadeIns[i].current);
                }
            }

            yield return null;
        }
    }
}