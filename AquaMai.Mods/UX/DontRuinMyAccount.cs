using System;
using System.Reflection;
using AquaMai.Config.Attributes;
using AquaMai.Core.Attributes;
using AquaMai.Core.Helpers;
using AquaMai.Core.Resources;
using DB;
using HarmonyLib;
using MAI2.Util;
using Manager;
using Manager.UserDatas;
using MelonLoader;
using Monitor;
using Process;
using UnityEngine;

namespace AquaMai.Mods.UX;

[ConfigSection(
    zh: "AutoPlay 时不保存成绩",
    en: "Do not save scores when AutoPlay is used",
    defaultOn: true)]
// 收编自 https://github.com/Starrah/DontRuinMyAccount/blob/master/Core.cs
public class DontRuinMyAccount
{
    [ConfigEntry(zh: "AutoPlay 激活后显示提示", en: "Show notice when AutoPlay is activated")]
    public static readonly bool showNotice = true;
    private static uint currentTrackNumber => GameManager.MusicTrackNumber;
    public static bool ignoreScore;
    private static UserScore oldScore;

    [HarmonyPatch(typeof(GameProcess), "OnUpdate")]
    [HarmonyPostfix]
    public static void OnUpdate()
    {
        if (GameManager.IsInGame && GameManager.IsAutoPlay() && !ignoreScore)
        {
            ignoreScore = true;
            MelonLogger.Msg("[DontRuinMyAccount] Autoplay triggered, will ignore this score.");
        }
    }

    [HarmonyPatch(typeof(GameProcess), "OnStart")]
    [HarmonyPostfix]
    [EnableIf(nameof(showNotice))]
    public static void OnStart(GameMonitor[] ____monitors)
    {
        ____monitors[0].gameObject.AddComponent<NoticeUI>();
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(UserData), "UpdateScore")]
    public static bool BeforeUpdateScore(int musicid, int difficulty, uint achive, uint romVersion)
    {
        if (ignoreScore)
        {
            MelonLogger.Msg("[DontRuinMyAccount] Prevented update DXRating: trackNo {0}, music {1}:{2}, achievement {3}", currentTrackNumber, musicid, difficulty, achive);
            return false;
        }
        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ResultProcess), "OnStart")]
    [HarmonyPriority(HarmonyLib.Priority.High)]
    public static bool BeforeResultProcessStart()
    {
        if (!ignoreScore)
        {
            return true;
        }
        var musicid = GameManager.SelectMusicID[0];
        var difficulty = GameManager.SelectDifficultyID[0];
        var userData = Singleton<UserDataManager>.Instance.GetUserData(0);
        // deepcopy
        oldScore = JsonUtility.FromJson<UserScore>(JsonUtility.ToJson(userData.ScoreDic[difficulty].GetValueSafe(musicid)));
        MelonLogger.Msg("[DontRuinMyAccount] Stored old score: trackNo {0}, music {1}:{2}, achievement {3}", currentTrackNumber, musicid, difficulty, oldScore?.achivement);
        return true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ResultProcess), "OnStart")]
    [HarmonyPriority(HarmonyLib.Priority.High)]
    public static void AfterResultProcessStart()
    {
        if (!ignoreScore)
        {
            return;
        }
        ignoreScore = false;
        var musicid = GameManager.SelectMusicID[0];
        var difficulty = GameManager.SelectDifficultyID[0];
        // current music playlog
        var score = Singleton<GamePlayManager>.Instance.GetGameScore(0, (int)currentTrackNumber - 1);
        // score.Achivement = 0; // Private setter, so reflection is essential
        typeof(GameScoreList).GetProperty("Achivement", BindingFlags.Public | BindingFlags.Instance)?.GetSetMethod(true)?.Invoke(score, [0m]);
        typeof(GameScoreList).GetProperty("ComboType", BindingFlags.Public | BindingFlags.Instance)?.GetSetMethod(true)?.Invoke(score, [PlayComboflagID.None]);
        typeof(GameScoreList).GetProperty("NowComboType", BindingFlags.Public | BindingFlags.Instance)?.GetSetMethod(true)?.Invoke(score, [PlayComboflagID.None]);
        score.SyncType = PlaySyncflagID.None;
        // user's all scores
        var userData = Singleton<UserDataManager>.Instance.GetUserData(0);
        var userScoreDict = userData.ScoreDic[difficulty];
        if (oldScore != null)
        {
            userScoreDict[musicid] = oldScore;
        }
        else
        {
            userScoreDict.Remove(musicid);
        }
        MelonLogger.Msg("[DontRuinMyAccount] Reset scores: trackNo {0}, music {1}:{2}, set current music playlog to 0.0000%, and userScoreDict[{1}:{2}] to {3}", currentTrackNumber,
            musicid, difficulty, oldScore?.achivement);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameProcess), nameof(GameProcess.OnStart))]
    public static void OnGameStart()
    {
        // For compatibility with QuickRetry
        ignoreScore = false;
    }

    private class NoticeUI : MonoBehaviour
    {
        public void OnGUI()
        {
            if (!ignoreScore) return;
            var y = Screen.height * .075f;
            var width = GuiSizes.FontSize * 20f;
            var x = GuiSizes.PlayerCenter + GuiSizes.PlayerWidth / 2f - width;
            var rect = new Rect(x, y, width, GuiSizes.LabelHeight * 2.5f);

            var labelStyle = GUI.skin.GetStyle("label");
            labelStyle.fontSize = (int)(GuiSizes.FontSize * 1.2);
            labelStyle.alignment = TextAnchor.MiddleCenter;

            GUI.Box(rect, "");
            GUI.Label(rect, GameManager.IsAutoPlay() ? Locale.AutoplayOn : Locale.AutoplayWasUsed);
        }
    }
}