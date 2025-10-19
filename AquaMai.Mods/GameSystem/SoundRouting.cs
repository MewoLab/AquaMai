using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AquaMai.Config.Attributes;
using AquaMai.Config.Types;
using HarmonyLib;
using MelonLoader;

namespace AquaMai.Mods.GameSystem;

[ConfigSection(
    name: "8Ch 音频路由重映射",
    en: """
        Redirect each channel in 8Ch to specific channel.
        Mixing is allowed. Use "None" to mute a channel.
        (NOTE: For 2Ch setups, please route the channels you want to P1SpeakerLeft/Right and other channels to None.)
        """,
    zh: """
        将 8Ch 的各个声道重新映射到指定的声道
        可进行混合，可使用 "None" 以禁用一个声道
        （注：对于 2Ch 用户，请将需要的声道路由到 P1SpeakerLeft/Right，其他声道路由到 None）
        """)]
public static class SoundRouting
{
    [ConfigEntry]
    private static readonly SoundChannel routeP1SpeakerLeftTo = SoundChannel.P1SpeakerLeft;

    [ConfigEntry]
    private static readonly SoundChannel routeP1SpeakerRightTo = SoundChannel.P1SpeakerRight;

    [ConfigEntry]
    private static readonly SoundChannel routeP1HeadphoneLeftTo = SoundChannel.P1HeadphoneLeft;

    [ConfigEntry]
    private static readonly SoundChannel routeP1HeadphoneRightTo = SoundChannel.P1HeadphoneRight;

    [ConfigEntry]
    private static readonly SoundChannel routeP2SpeakerLeftTo = SoundChannel.P2SpeakerLeft;

    [ConfigEntry]
    private static readonly SoundChannel routeP2SpeakerRightTo = SoundChannel.P2SpeakerRight;

    [ConfigEntry]
    private static readonly SoundChannel routeP2HeadphoneLeftTo = SoundChannel.P2HeadphoneLeft;

    [ConfigEntry]
    private static readonly SoundChannel routeP2HeadphoneRightTo = SoundChannel.P2HeadphoneRight;

    private const int PARAMETER_MATRIX_SIZE = 8 * 8 * 4; // 8 input * 8 output * 4 sizeof(float BE)

    private static byte[] GenerateParameterMatrix(SoundChannel routeLeftTo, SoundChannel routeRightTo)
    {
        var matrix8x8 = new byte[PARAMETER_MATRIX_SIZE];
        int index = 0;
        for (int i = 0; i < 2; i++) // 2ch (stereo) input
        {
            for (int j = 0; j < 8; j++) // 8ch output
            {
                if (i == 0 && j == (int)routeLeftTo || i == 1 && j == (int)routeRightTo)
                {
                    // 1.0f in Big endian
                    // TODO: anyone need to control the volume in routing?
                    matrix8x8[index++] = 63;
                    matrix8x8[index++] = 128;
                    matrix8x8[index++] = 0;
                    matrix8x8[index++] = 0;
                }
                else
                {
                    matrix8x8[index++] = 0;
                    matrix8x8[index++] = 0;
                    matrix8x8[index++] = 0;
                    matrix8x8[index++] = 0;
                }
            }
        }
        return matrix8x8;
    }

    private static byte[] GetOriginalParameters()
    {
        var memoryStream = new MemoryStream();
        memoryStream.Write(GenerateParameterMatrix(SoundChannel.P1SpeakerLeft, SoundChannel.P1SpeakerRight), 0, PARAMETER_MATRIX_SIZE);
        memoryStream.Write(GenerateParameterMatrix(SoundChannel.P2SpeakerLeft, SoundChannel.P2SpeakerRight), 0, PARAMETER_MATRIX_SIZE);
        memoryStream.Write(GenerateParameterMatrix(SoundChannel.P1HeadphoneLeft, SoundChannel.P1HeadphoneRight), 0, PARAMETER_MATRIX_SIZE);
        memoryStream.Write(GenerateParameterMatrix(SoundChannel.P2HeadphoneLeft, SoundChannel.P2HeadphoneRight), 0, PARAMETER_MATRIX_SIZE);
        return memoryStream.ToArray();
    }

    private static byte[] GenerateParameters()
    {
        var memoryStream = new MemoryStream();
        memoryStream.Write(GenerateParameterMatrix(routeP1SpeakerLeftTo, routeP1SpeakerRightTo), 0, PARAMETER_MATRIX_SIZE);
        memoryStream.Write(GenerateParameterMatrix(routeP2SpeakerLeftTo, routeP2SpeakerRightTo), 0, PARAMETER_MATRIX_SIZE);
        memoryStream.Write(GenerateParameterMatrix(routeP1HeadphoneLeftTo, routeP1HeadphoneRightTo), 0, PARAMETER_MATRIX_SIZE);
        memoryStream.Write(GenerateParameterMatrix(routeP2HeadphoneLeftTo, routeP2HeadphoneRightTo), 0, PARAMETER_MATRIX_SIZE);
        return memoryStream.ToArray();
    }

    // Naive O(n*m) search but enough
    private static int FindBytesInBytes(byte[] haystack, byte[] needle)
    {
        for (int i = 0; i < haystack.Length; i++) if (haystack.Skip(i).Take(needle.Length).SequenceEqual(needle)) return i;
        return -1;
    }

    private static byte[] RewriteAcf(byte[] acfData)
    {
        var originalParameters = GetOriginalParameters();
        int i = FindBytesInBytes(acfData, originalParameters);
        if (i == -1)
        {
            MelonLogger.Error("[SoundRouting] Failed to find original parameters in ACF data");
            return acfData;
        }
        var newParameters = GenerateParameters();
        var newAcfData = new byte[acfData.Length];
        Array.Copy(acfData, 0, newAcfData, 0, i);
        Array.Copy(newParameters, 0, newAcfData, i, newParameters.Length);
        Array.Copy(acfData, i + originalParameters.Length, newAcfData, i + newParameters.Length, acfData.Length - i - originalParameters.Length);
        return newAcfData;
    }

    private static readonly Dictionary<string, byte[]> _patchedAcfDataCache = [];

    [HarmonyPrefix]
    [HarmonyPatch(typeof(CriAtomEx), "RegisterAcf", [typeof(CriFsBinder), typeof(string)])]
    public static bool PreRegisterAcf(string acfPath)
    {
        try
        {
            if (!_patchedAcfDataCache.ContainsKey(acfPath))
            {
                var acfData = File.ReadAllBytes(acfPath);
                _patchedAcfDataCache[acfPath] = RewriteAcf(acfData);
            }
            CriAtomEx.RegisterAcf(_patchedAcfDataCache[acfPath]);
        }
        catch (Exception e)
        {
            MelonLogger.Error("[SoundRouting] Failed to rewrite ACF data", e);
        }
        return false;
    }
}
