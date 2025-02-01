using AquaMai.Config.Attributes;
using UnityEngine;

namespace AquaMai.Mods.Tweaks;

[ConfigSection(
    en: """
        Force the frame rate limit to 60 FPS and disable vSync.
        Do not use if your game has no issues.
        """,
    zh: """
        强制设置帧率上限为 60 帧并关闭垂直同步
        如果你的游戏没有问题，请不要使用
        """)]
public class LockFrameRate
{
    [ConfigEntry(
        zh: "目标帧率，不建议修改。除非你知道你在做什么")]
    public static readonly int targetFrameRate = 60;

    public static void OnBeforePatch()
    {
        Application.targetFrameRate = targetFrameRate;
        QualitySettings.vSyncCount = 0;
    }
}