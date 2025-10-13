using System.Linq;
using AquaMai.Config.Attributes;
using AquaMai.Core.Attributes;
using AquaMai.Core.Helpers;
using HarmonyLib;
using MAI2.Util;
using Manager;
using MelonLoader;

namespace AquaMai.Mods.GameSystem;

[ConfigSection(
    name: "重开计次",
    defaultOn: true,
    en: "Add play count when quick retry (regardless of trigger type).",
    zh: "在一键重开（无论何种触发方式）时，增加当前谱面的游玩次数")]
[EnableGameVersion(23000)]
public class AddPlayCountWhenQuickRetry
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(GamePlayManager), nameof(GamePlayManager.SetQuickRetryFrag))]
    public static void SetQuickRetryFrag(bool flag, bool ____isQuickRetry, GamePlayManager __instance)
    {
        if (!flag) return;
        if (____isQuickRetry) return;
        var userData = Singleton<UserDataManager>.Instance.GetUserData(0);
        for (int i = 0; i < 2; i++)
        {
            if (!userData.IsEntry) continue;
            MelonLogger.Msg($"[AddPlayCountWhenQuickRetry] Player {i} MusicID {GameManager.SelectMusicID[i]} Difficulty {GameManager.SelectDifficultyID[i]}");
            var score = Shim.GetUserScoreList(userData)[GameManager.SelectDifficultyID[i]].FirstOrDefault(it => it.id == GameManager.SelectMusicID[i]);
            if (score != null)
            {
                score.playcount++;
            }
        }
    }
}