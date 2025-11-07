using HarmonyLib;
using Main;
using UnityEngine;

namespace AquaMai.Core.Helpers;

public class InvalidConfigAlert : MonoBehaviour
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameMainObject), "Awake")]
    public static void OnGameMainObjectAwake(GameMainObject __instance)
    {
        Destroy(__instance);
        var go = new GameObject("配置错误提示组件");
        go.AddComponent<InvalidConfigAlert>();
    }

    public const string ConfigNotFoundMessage = """
                                                找不到 AquaMai 配置文件！
                                                已生成示例配置文件并保存到 AquaMai.zh.toml，请将其重命名为 AquaMai.toml 并做需要的更改
                                                你可以使用 MaiChartManager 来图形化修改配置文件
                                                现在请退出游戏

                                                AquaMai Configuration Not Found!
                                                Example configuration files have been generated and saved to AquaMai.en.toml
                                                Please rename them to AquaMai.toml and make the necessary changes
                                                Now please exit the game
                                                """;

    public const string ConfigCorruptedMessage = """
                                                 AquaMai 配置文件损坏！读取配置文件时遇到问题
                                                 请退出游戏并检查配置文件是否有格式错误

                                                 AquaMai Configuration Corrupted! An error occurred while reading the configuration file
                                                 Please exit the game and check if the configuration file has any format errors
                                                 Now please exit the game
                                                 """;

    public static string message = "";

    private void OnGUI()
    {
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 25,
            alignment = TextAnchor.UpperLeft,
            wordWrap = true,
        };

        GUI.Label(new Rect(50, 50, Screen.width / 2f, Screen.height / 1920f * 450f), message, style);
    }
}