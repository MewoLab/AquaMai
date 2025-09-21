using System.Collections.Generic;
using System.Linq;
using AquaMai.Core.Attributes;
using AquaMai.Config.Attributes;
using AquaMai.Core.Helpers;
using AquaMai.Core.Resources;
using DB;
using HarmonyLib;
using JetBrains.Annotations;
using MAI2.Util;
using Manager;
using Manager.MaiStudio;
using Manager.UserDatas;
using MelonLoader;
using Monitor;
using Process;
using UnityEngine;

namespace AquaMai.Mods.UX;

[EnableGameVersion(23500)]
[ConfigSection(
    en: "Show detail of selected song in music selection screen.",
    zh: "选歌界面显示选择的歌曲的详情")]
public class SelectionDetail
{
    [ConfigEntry(
        en: "Show friend battle target achievement",
        zh: "显示友人对战目标分数")]
    private static readonly bool showBattleAchievement = false;

    private static readonly Window[] window = new Window[2];
    private static MusicSelectProcess.MusicSelectData SelectData { get; set; }
    private static readonly int[] difficulty = new int[2];
    [CanBeNull] private static UserGhost userGhost;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MusicSelectMonitor), "UpdateRivalScore")]
    public static void ScrollUpdate(MusicSelectProcess ____musicSelect, MusicSelectMonitor __instance)
    {
        int player;
        if (__instance == ____musicSelect.MonitorArray[0])
        {
            player = 0;
        }
        else if (__instance == ____musicSelect.MonitorArray[1])
        {
            player = 1;
        }
        else
        {
            return;
        }

        if (window[player] != null)
        {
            window[player].Close();
        }

        var userData = Singleton<UserDataManager>.Instance.GetUserData(player);
        if (!userData.IsEntry) return;

        if (____musicSelect.IsRandomIndex()) return;

        SelectData = ____musicSelect.GetMusic(0);
        if (SelectData == null) return;
        difficulty[player] = ____musicSelect.GetDifficulty(player);
        var ghostTarget = ____musicSelect.GetCombineMusic(0).musicSelectData[(int)____musicSelect.ScoreType].GhostTarget;
        userGhost = Singleton<GhostManager>.Instance.GetGhostToEnum(ghostTarget);

        window[player] = player == 0 ? __instance.gameObject.AddComponent<P1Window>() : __instance.gameObject.AddComponent<P2Window>();
    }

    private class P1Window : Window
    {
        protected override int player => 0;
    }

    private class P2Window : Window
    {
        protected override int player => 1;
    }

    private abstract class Window : MonoBehaviour
    {
        protected abstract int player { get; }
        private UserData userData => Singleton<UserDataManager>.Instance.GetUserData(player);

        public void OnGUI()
        {
            var dataToShow = new List<string>();
            dataToShow.Add($"ID: {SelectData.MusicData.name.id}");
            dataToShow.Add(MusicDirHelper.LookupPath(SelectData.MusicData.name.id).Split('/').Reverse().ToArray()[3]);
            if (SelectData.MusicData.genreName is not null) // SelectData.MusicData.genreName.str may not correct
                dataToShow.Add(Singleton<DataManager>.Instance.GetMusicGenre(SelectData.MusicData.genreName.id)?.genreName);
            if (SelectData.MusicData.AddVersion is not null)
                dataToShow.Add(Singleton<DataManager>.Instance.GetMusicVersion(SelectData.MusicData.AddVersion.id)?.genreName);
            var notesData = SelectData.MusicData.notesData[difficulty[player]];
            dataToShow.Add($"{notesData?.level}.{notesData?.levelDecimal}");

            if (userGhost != null)
            {
                dataToShow.Add(string.Format(Locale.UserGhostAchievement, $"{userGhost.Achievement / 10000m:0.0000}"));
            }

            var rate = CalcB50(SelectData.MusicData, difficulty[player]);
            if (rate > 0)
            {
                dataToShow.Add(string.Format(Locale.RatingUpWhenSSSp, rate));
            }
            else
            {
                rate = CalcB50(SelectData.MusicData, difficulty[player], true);
                if (rate > 0)
                {
                    dataToShow.Add(string.Format(Locale.RatingUpWhenAP, rate));
                }
            }

            var playCount = Shim.GetUserScoreList(userData)[difficulty[player]].FirstOrDefault(it => it.id == SelectData.MusicData.name.id)?.playcount ?? 0;
            if (playCount > 0)
            {
                dataToShow.Add(string.Format(Locale.PlayCount, playCount));
            }


            var width = GuiSizes.FontSize * 15f;
            var x = GuiSizes.PlayerCenter - width / 2f + GuiSizes.PlayerWidth * player;
            var y = Screen.height * 0.87f;

            var labelStyle = GUI.skin.GetStyle("label");
            labelStyle.fontSize = GuiSizes.FontSize;
            labelStyle.alignment = TextAnchor.MiddleCenter;

            GUI.Box(new Rect(x, y, width, dataToShow.Count * GuiSizes.LabelHeight + 2 * GuiSizes.Margin), "");
            for (var i = 0; i < dataToShow.Count; i++)
            {
                GUI.Label(new Rect(x, y + GuiSizes.Margin + i * GuiSizes.LabelHeight, width, GuiSizes.LabelHeight), dataToShow[i]);
            }
        }

        private int CalcB50(MusicData musicData, int difficulty, bool ap = false)
        {
            var musicId = musicData.name.id;
            var aimRate = ap ? Shim.CreateUserRate(musicId, difficulty, 1010000, (uint)musicData.version, PlayComboflagID.AllPerfectPlus) :
                Shim.CreateUserRate(musicId, difficulty, 1005000, (uint)musicData.version, PlayComboflagID.None);
            var list = aimRate.OldFlag ? userData.RatingList.RatingList : userData.RatingList.NewRatingList;
            var maxCount = aimRate.OldFlag ? 35 : 15;
            uint userLowRate = 0;
            if (list.Count == maxCount)
            {
                var rate = list.Last();
                userLowRate = rate.SingleRate;
            }

            var userSongRate = list.FirstOrDefault(it => it.MusicId == musicId && it.Level == difficulty);
            if (!userSongRate.Equals(default(UserRate))) userLowRate = userSongRate.SingleRate;

            return (int)aimRate.SingleRate - (int)userLowRate;
        }

        public void Close()
        {
            Destroy(this);
        }
    }
}