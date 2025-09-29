using AquaMai.Config.Attributes;
using HarmonyLib;
using Manager;
using System;
using MelonLoader;

namespace AquaMai.Mods.GameSystem;

[ConfigSection(
    zh: "音频独占与八声道设置",
    en: "Audio Exclusive and 8-Channel Settings")]
public static class Sound
{
    [ConfigEntry(
        zh: "是否启用音频独占",
        en: "Enable Audio Exclusive")]
    private readonly static bool enableExclusive = false;

    [ConfigEntry(
        zh: "是否启用八声道",
        en: "Enable 8-Channel")]
    private readonly static bool enable8Channel = false;
    
    [ConfigEntry(
        zh: """
            将1P耳机音量同步给游戏主音量（外放音量）
            注意：Maimai处理耳机音量的方式未知，若此功能与八声道同时开启，可能导致耳机音量被缩放两次，推荐在仅使用1P且不开启八声道时使用,
            """,
        en: """
            Sync 1P headphone volume to game master volume
            Maimai's headphone volume handling is unclear. Enabling with 8ch may double-scale headphone volume. It is recommended to use this only in 1P without 8ch.
            """)]
    private readonly static bool sync1pVolumeToMaster = false;

    [ConfigEntry(
        en: "Music Volume.",
        zh: "乐曲音量")]
    private readonly static float musicVolume = 1.0f;

    private static CriAtomUserExtension.AudioClientShareMode AudioShareMode => enableExclusive ? CriAtomUserExtension.AudioClientShareMode.Exclusive : CriAtomUserExtension.AudioClientShareMode.Shared;

    private const ushort wBitsPerSample = 32;
    private const uint nSamplesPerSec = 48000u;
    private static ushort nChannels => enable8Channel ? (ushort)8 : (ushort)2;
    private static ushort nBlockAlign => (ushort)(wBitsPerSample / 8 * nChannels);
    private static uint nAvgBytesPerSec => nSamplesPerSec * nBlockAlign;

    private static CriAtomUserExtension.WaveFormatExtensible CreateFormat() =>
        new()
        {
            Format = new CriAtomUserExtension.WaveFormatEx
            {
                wFormatTag = 65534,
                nSamplesPerSec = nSamplesPerSec,
                wBitsPerSample = wBitsPerSample,
                cbSize = 22,
                nChannels = nChannels,
                nBlockAlign = nBlockAlign,
                nAvgBytesPerSec = nAvgBytesPerSec
            },
            Samples = new CriAtomUserExtension.Samples
            {
                wValidBitsPerSample = 24,
            },
            dwChannelMask = enable8Channel ? 1599u : 3u,
            SubFormat = new Guid("00000001-0000-0010-8000-00aa00389b71")
        };

    [HarmonyPrefix]
    // Original typo
    [HarmonyPatch(typeof(WasapiExclusive), "Intialize")]
    public static bool InitializePrefix()
    {
        CriAtomUserExtension.SetAudioClientShareMode(AudioShareMode);
        CriAtomUserExtension.SetAudioBufferTime(160000uL);
        var format = CreateFormat();
        CriAtomUserExtension.SetAudioClientFormat(ref format);
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(SoundManager), "Play")]
    public static void PlayPrefix(SoundManager.AcbID acbID,
        SoundManager.PlayerID playerID,
        int cueID,
        bool prepare,
        int target,
        int startTime,
        ref float volume)
    {
        if (acbID == SoundManager.AcbID.Music && playerID == SoundManager.PlayerID.Music)
        {
            volume = musicVolume;
        }
    }
    
    /*
     * 将1P耳机音量同步给游戏主音量（外放音量）
     * Sync 1P headphone volume to game master volume
     *
     * 注意：Maimai处理耳机音量的方式未知，若此功能与八声道同时开启，可能导致耳机音量被缩放两次，
     *       推荐在仅使用1P且不开启八声道时使用
     * Note: Maimai's headphone volume handling is unclear. Enabling with 8ch may double-scale headphone volume.
     *       It is recommended to use this only in 1P without 8ch.
     */
    [HarmonyPrefix]
    [HarmonyPatch(typeof(SoundCtrl), nameof(SoundCtrl.Initialize))]
    public static void SoundCtrl_Initialize_Prefix(SoundCtrl __instance)
    {
        __instance._masterVolume = 0.05f; // Default headphone volume
        // MelonLogger.Msg("master volume initialized to 0.05");
    }
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(SoundCtrl), nameof(SoundCtrl.SetMasterVolume))]
    public static void SoundCtrl_SetMasterVolume_Postfix(SoundCtrl __instance, float[] ____headPhoneVolume, float volume)
    {
        __instance._masterVolume = __instance.Adjust0_1(volume * ____headPhoneVolume[0]);
        // MelonLogger.Msg($"master volume set to {__instance._masterVolume}");
    }
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(SoundCtrl), nameof(SoundCtrl.ResetMasterVolume))]
    public static void SoundCtrl_ResetMasterVolume_Postfix(SoundCtrl __instance, float[] ____headPhoneVolume)
    {
        __instance._masterVolume = __instance.Adjust0_1(____headPhoneVolume[0]);
        // MelonLogger.Msg($"master volume reset to {__instance._masterVolume}");
    }
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(SoundCtrl), nameof(SoundCtrl.SetHeadPhoneVolume))]
    public static void SoundCtrl_SetHeadPhoneVolume_Postfix(SoundCtrl __instance, int targerID, float volume)
    {
        // MelonLogger.Msg($"setting headphone volume : target {targerID}, volume {volume}");
        if (targerID == 0)
        {
            __instance._masterVolume = __instance.Adjust0_1(volume);
            // MelonLogger.Msg($"master volume set to {__instance._masterVolume}");
            // fixme)) 由于每个播放通道的master音量只在切换音频文件时重设，目前调节耳机音量不能及时反映到master
            // 需要在这里加一段调整所有通道音量的代码，但这样就需要记录开始播放时的volume参数
        }
    }
    
    [HarmonyPrefix]
    [HarmonyPatch(typeof(SoundCtrl), nameof(SoundCtrl.Play))]
    public static void SoundCtrl_Play_Prefix(SoundCtrl __instance, SoundCtrl.PlaySetting setting)
    {
        // fixme)) 在这里记录每个文件开始播放时的volume参数
        if (setting.PlayerID != 3 && setting.PlayerID != 4 && setting.PlayerID != 5)
        {
            return;
        }
        // PlayerID 3 ~ 5 are not controlled by master volume, so we need a extra scaling
        // MelonLogger.Msg($"scaling volume: {__instance._masterVolume}");
        setting.Volume *= __instance._masterVolume;
    }
    
    
}
