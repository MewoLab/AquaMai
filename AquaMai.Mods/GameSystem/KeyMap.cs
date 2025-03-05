using System.Reflection;
using AMDaemon;
using AquaMai.Config.Attributes;
using AquaMai.Config.Types;
using AquaMai.Core.Attributes;
using HarmonyLib;
using UnityEngine;

namespace AquaMai.Mods.GameSystem;

[ConfigSection(
    en: """
        Enable or disable IO4 and DebugInput. Configure the key mapping for DebugInput.
        DebugInput works independently of IO4 (IO4-compatible board / segatools IO4 emulation).
        (You should enable at least one input source, unless you use other input solutions like AdxHidInput.)
        """,
    zh: """
        启用或禁用 IO4 和 DebugInput。配置 DebugInput 的按键映射。
        DebugInput 与 IO4（兼容 IO4 板 / segatools IO4 模拟）独立工作。
        （你应该至少启用一个输入源，除非你使用其他输入方案如 AdxHidInput。）
        """,
    defaultOn: true)]
public class KeyMap
{
    [ConfigEntry(
        en: """
            Disable IO4 (IO4-compatible board / segatools IO4 emulation) input.
            With IO4 input disabled, your IO4-compatible board or segatools IO4 emulation is ignored.
            """,
        zh: """
            禁用 IO4（兼容 IO4 板 / segatools IO4 模拟）输入。
            在禁用 IO4 输入后，你的兼容 IO4 板或 segatools IO4 模拟将被忽略。
            """)]
    private static readonly bool disableIO4 = false;

    [ConfigEntry(
        en: """
            Disable DebugInput. The key mapping below will not work.
            With DebugInput disabled, you'll need a IO4-compatible board, segatools IO4 emulation or other custom input solutions to play.
            You may want to configure IO4 emulation key mapping in segatools.ini's [io4] and [button] section.
            """,
        zh: """
            禁用 DebugInput，下列按键映射将不起作用。
            在禁用 DebugInput 后，你需要兼容 IO4 板、segatools IO4 模拟或其他自定义输入方案才能游玩。
            如果使用 IO4 模拟，你可以在 segatools.ini 的 [io4] 和 [button] 部分配置按键映射。
            """)]
    public static readonly bool disableDebugInput = false; // Implemented in AquaMai.Mods/Fix/Common.cs

    [EnableIf(nameof(disableIO4))]
    [HarmonyPatch("IO.Jvs+JvsSwitch", ".ctor", MethodType.Constructor, [typeof(int), typeof(string), typeof(KeyCode), typeof(bool), typeof(bool)])]
    [HarmonyPrefix]
    public static void PreJvsSwitchConstructor(ref bool invert)
    {
        invert = false;
    }

    [EnableIf(nameof(disableIO4))]
    [HarmonyPatch(typeof(SwitchInput), "get_IsOn")]
    [HarmonyPrefix]
    public static bool PreGetIsOn(ref bool __result)
    {
        __result = false;
        return false;
    }

    [EnableIf(nameof(disableIO4))]
    [HarmonyPatch(typeof(SwitchInput), "get_HasOnNow")]
    [HarmonyPrefix]
    public static bool PreGetHasOnNow(ref bool __result)
    {
        __result = false;
        return false;
    }

    [ConfigEntry]
    public static readonly KeyCodeID Test = (KeyCodeID)115;

    [ConfigEntry]
    private static readonly KeyCodeID Service = (KeyCodeID)5;

    [ConfigEntry]
    private static readonly KeyCodeID Button1_1P = (KeyCodeID)67;

    [ConfigEntry]
    private static readonly KeyCodeID Button2_1P = (KeyCodeID)49;

    [ConfigEntry]
    private static readonly KeyCodeID Button3_1P = (KeyCodeID)48;

    [ConfigEntry]
    private static readonly KeyCodeID Button4_1P = (KeyCodeID)47;

    [ConfigEntry]
    private static readonly KeyCodeID Button5_1P = (KeyCodeID)68;

    [ConfigEntry]
    private static readonly KeyCodeID Button6_1P = (KeyCodeID)70;

    [ConfigEntry]
    private static readonly KeyCodeID Button7_1P = (KeyCodeID)45;

    [ConfigEntry]
    private static readonly KeyCodeID Button8_1P = (KeyCodeID)61;

    [ConfigEntry]
    private static readonly KeyCodeID Select_1P = (KeyCodeID)25;

    [ConfigEntry]
    private static readonly KeyCodeID Button1_2P = (KeyCodeID)80;

    [ConfigEntry]
    private static readonly KeyCodeID Button2_2P = (KeyCodeID)81;

    [ConfigEntry]
    private static readonly KeyCodeID Button3_2P = (KeyCodeID)78;

    [ConfigEntry]
    private static readonly KeyCodeID Button4_2P = (KeyCodeID)75;

    [ConfigEntry]
    private static readonly KeyCodeID Button5_2P = (KeyCodeID)74;

    [ConfigEntry]
    private static readonly KeyCodeID Button6_2P = (KeyCodeID)73;

    [ConfigEntry]
    private static readonly KeyCodeID Button7_2P = (KeyCodeID)76;

    [ConfigEntry]
    private static readonly KeyCodeID Button8_2P = (KeyCodeID)79;

    [ConfigEntry]
    private static readonly KeyCodeID Select_2P = (KeyCodeID)84;

    [ConfigEntry]
    private static readonly KeyCodeID Autoplay = (KeyCodeID)94;

    public static string GetAutoplay() {return Autoplay.ToString();}

    [HarmonyPatch(typeof(DB.JvsButtonTableRecord), MethodType.Constructor, typeof(int), typeof(string), typeof(string), typeof(int), typeof(string), typeof(int), typeof(int), typeof(int))]
    [HarmonyPostfix]
    public static void JvsButtonTableRecordConstructor(DB.JvsButtonTableRecord __instance, string Name)
    {
        var prop = (DB.KeyCodeID)typeof(KeyMap).GetField(Name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static).GetValue(null);
        __instance.SubstituteKey = prop;
    }
}
