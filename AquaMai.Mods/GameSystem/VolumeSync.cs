using System;
using System.Collections;
using System.Linq;
using AquaMai.Config.Attributes;
using HarmonyLib;
using Manager;
using MelonLoader;
using Type = System.Type;

namespace AquaMai.Mods.GameSystem;

[ConfigSection(
    zh: """
        将1P耳机音量同步给游戏主音量（外放音量）
        注意：若此功能与八声道同时开启，会导致内置耳机声道的音量被缩放两次，推荐在仅使用1P且不开启八声道时使用,
        """,
    en: """
        Sync 1P headphone volume to game master volume
        Note: Enabling with 8ch will double-scale internal headphone volume. Recommended to use this only in 1P without 8ch.
        """)]
public static class VolumeSync
{
    public static class PlayerObjHelper
    {
        public static void Init()
        {
            var playerObjType = typeof(SoundCtrl).GetNestedType("PlayerObj", AccessTools.all);

            UnboundIsReady = MethodInvoker.GetHandler(
                AccessTools.DeclaredMethod(playerObjType, "IsReady", [])
            );

            UnboundSetAisac = MethodInvoker.GetHandler(
                AccessTools.DeclaredMethod(playerObjType, "SetAisac", [typeof(int), typeof(float)])
            );

            UnboundTargetIDAccessor = AccessTools.FieldRefAccess<int>(playerObjType, "TargetID");
            UnboundNeedUpdateAccessor = AccessTools.FieldRefAccess<bool>(playerObjType, "NeedUpdate");
            UnboundPlayerDictAccessor = AccessTools.FieldRefAccess<SoundCtrl, IDictionary>("_players");
        }

        public static FastInvokeHandler UnboundIsReady;
        public static FastInvokeHandler UnboundSetAisac;

        public static AccessTools.FieldRef<object, int> UnboundTargetIDAccessor;
        public static AccessTools.FieldRef<object, bool> UnboundNeedUpdateAccessor;
        public static AccessTools.FieldRef<SoundCtrl, IDictionary> UnboundPlayerDictAccessor;
    }

    private static float[] _playerVolumes;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(SoundCtrl), nameof(SoundCtrl.Initialize))]
    public static void SoundCtrl_Initialize_Prefix(SoundCtrl __instance, SoundCtrl.InitParam param)
    {
        MelonLogger.Msg("VolumeSync - Initializing");
        __instance._masterVolume = 0.05f; // Default headphone volume

        // Initialization
        try
        {
            PlayerObjHelper.Init();
            _playerVolumes = new float[param.PlayerNum];
            for (var i = 0; i < param.PlayerNum; i++)
            {
                _playerVolumes[i] = 1f;
            }
        }
        catch (Exception e)
        {
            MelonLogger.Error(e);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SoundCtrl), nameof(SoundCtrl.SetMasterVolume))]
    public static void SoundCtrl_SetMasterVolume_Postfix(SoundCtrl __instance, float[] ____headPhoneVolume,
        float volume)
    {
        __instance._masterVolume = __instance.Adjust0_1(volume * ____headPhoneVolume[0]);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SoundCtrl), nameof(SoundCtrl.ResetMasterVolume))]
    public static void SoundCtrl_ResetMasterVolume_Postfix(SoundCtrl __instance, float[] ____headPhoneVolume)
    {
        __instance._masterVolume = __instance.Adjust0_1(____headPhoneVolume[0]);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SoundCtrl), nameof(SoundCtrl.SetHeadPhoneVolume))]
    public static void SoundCtrl_SetHeadPhoneVolume_Postfix(SoundCtrl __instance, int targerID, float volume)
    {
        if (targerID != 0) return;

        __instance._masterVolume = __instance.Adjust0_1(volume);

        var playerDict = PlayerObjHelper.UnboundPlayerDictAccessor(__instance);
        foreach (DictionaryEntry pair in playerDict)
        {
            var player = pair.Value; // SoundCtrl.PlayerObj
            var key = (int)pair.Key;
            var newVolume = __instance.Adjust0_1(__instance._masterVolume * _playerVolumes[key]);

            if (!(bool)PlayerObjHelper.UnboundIsReady(player)) continue;

            switch (PlayerObjHelper.UnboundTargetIDAccessor(player))
            {
                case 0:
                    PlayerObjHelper.UnboundSetAisac(player, 4, newVolume);
                    break;
                case 1:
                    PlayerObjHelper.UnboundSetAisac(player, 5, newVolume);
                    break;
                case 2:
                    PlayerObjHelper.UnboundSetAisac(player, 4, newVolume);
                    PlayerObjHelper.UnboundSetAisac(player, 5, newVolume);
                    break;
            }

            PlayerObjHelper.UnboundNeedUpdateAccessor(player) = true;
        }

        // The code above is supposed to do following things,
        // but SoundCtrl.PlayerObj is a private type so we need these unbound method bullshit.
        // By the way, MethodInvoker is an IL-based delegate, which is faster than invoking MethodInfo every time
        // (Traverse only cache MethodInfo, invoking is still using MethodInfo.Invoke)
        // ================================================================================
        // foreach (KeyValuePair<int, SoundCtrl.PlayerObj> player in this._players)
        // {
        //     if (player.Value.IsReady())
        //     {
        //         switch (player.Value.TargetID)
        //         {
        //             case 0:
        //                 player.Value.SetAisac(4, volume);
        //                 break;
        //             case 1:
        //                 player.Value.SetAisac(5, volume);
        //                 break;
        //             case 2:
        //                 player.Value.SetAisac(4, volume);
        //                 player.Value.SetAisac(5, volume);
        //                 break;
        //         }
        //         player.Value.NeedUpdate = true;
        //     }
        // }
        // ================================================================================
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(SoundCtrl), nameof(SoundCtrl.Play))]
    public static void SoundCtrl_Play_Prefix(SoundCtrl __instance, SoundCtrl.PlaySetting setting)
    {
        _playerVolumes[setting.PlayerID] = (setting.Volume < 0f) ? 1.0f : setting.Volume;

        if (setting.PlayerID != 3 && setting.PlayerID != 4 && setting.PlayerID != 5)
        {
            return;
        }

        // PlayerID 3 ~ 5 are not controlled by master volume, so we need a extra scaling
        // MelonLogger.Msg($"scaling volume: {__instance._masterVolume}");
        setting.Volume *= __instance._masterVolume;
    }
}