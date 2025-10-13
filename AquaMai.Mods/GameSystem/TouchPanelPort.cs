using AquaMai.Config.Attributes;
using HarmonyLib;
using IO;

namespace AquaMai.Mods.GameSystem;

[ConfigSection(
    name: "触摸屏串口",
    en: """
        Adjust the port of the touch screen serial port, default value is COM3 COM4.
        Requires configuration by Device Manager. If you are unsure, don't use it.
        """,
    zh: """
        调整触摸屏串口号，默认值 COM3 COM4
        需要设备管理器配置，如果你不清楚你是否可以使用，请不要使用
        """)]
public class TouchPanelPort
{
    [ConfigEntry(
        en: "Port for 1P.",
        name: "1P串口号")]
    private static readonly string portName_1P = "COM3";

    [ConfigEntry(
        en: "Port for 2P.",
        name: "2P串口号")]
    private static readonly string portName_2P = "COM4";

    [HarmonyPatch(typeof(NewTouchPanel), "Open")]
    [HarmonyPrefix]
    private static void OpenPrefix(ref string[] ___PortName)
    {
        if (___PortName == null || ___PortName.Length < 2)
        {
            ___PortName = new string[2];
        }
        ___PortName[0] = portName_1P;
        ___PortName[1] = portName_2P;
    }
}
