using AquaMai.Config.Attributes;
using UnityEngine;

namespace AquaMai.Mods.Tweaks;

[ConfigSection(
    name: "锁定帧率",
    en: """
        Force the frame rate limit to 60 FPS and disable vSync.
        Do not use if your game has no issues. Suggest to set the refresh rate of your display instead.
        """,
    zh: """
        强制设置帧率上限为 60 帧并关闭垂直同步
        如果你的游戏没有问题，请不要使用。建议直接设置显示器的刷新率
        """)]
public class LockFrameRate
{
    [ConfigEntry(
        name: "目标帧率",
        zh: "目标帧率，不建议修改。除非你知道你在做什么")]
    public static readonly int targetFrameRate = 60;

    public static void OnBeforePatch()
    {
        Application.targetFrameRate = targetFrameRate;
        QualitySettings.vSyncCount = 0;
    }
}