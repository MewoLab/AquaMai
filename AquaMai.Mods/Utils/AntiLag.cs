using System.Runtime.InteropServices;
using AquaMai.Config.Attributes;
using HarmonyLib;
using Main;
using AquaMai.Core.Environment;
using UnityEngine;

namespace AquaMai.Mods.Utils;

[ConfigSection(
    en: "Some tricks to prevent the system from lagging",
    zh: "奇妙的防掉帧，如果你有莫名其妙的掉帧，可以试试这个")]
public class AntiLag : MonoBehaviour
{
    [ConfigEntry(zh: "游戏未取得焦点时也运行")]
    private static readonly bool activateWhileBackground = false;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameMainObject), "Awake")]
    public static void OnGameMainObjectAwake()
    {
        var go = new GameObject("妙妙防掉帧");
        go.AddComponent<AntiLag>();
    }

    private void Awake()
    {
        InvokeRepeating(nameof(OnTimer), 10f, 10f);
    }

    [DllImport("user32.dll", EntryPoint = "keybd_event", SetLastError = true)]
    private static extern void keybd_event(uint bVk, byte bScan, uint dwFlags, uint dwExtraInfo);

    const int KEYEVENTF_KEYDOWN = 0x0000;
    const int KEYEVENTF_KEYUP = 0x0002;
    const int CTRL = 17;

    private void OnTimer()
    {
        if (!Application.isFocused && !activateWhileBackground) return;
#if DEBUG
        MelonLogger.Msg("[AntiLag] Trigger");
#endif
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        keybd_event(CTRL, 0, KEYEVENTF_KEYDOWN, 0);
        keybd_event(CTRL, 0, KEYEVENTF_KEYUP, 0);
    }
}
