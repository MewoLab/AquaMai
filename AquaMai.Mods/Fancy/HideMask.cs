using AquaMai.Config.Attributes;
using HarmonyLib;
using Monitor;
using UnityEngine;

namespace AquaMai.Mods.Fancy;

[ConfigSection(
    name: "移除圆形遮罩",
    en: "Remove the circle mask of the game screen.")]
public class HideMask
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Main.GameMain), "LateInitialize", typeof(MonoBehaviour), typeof(Transform), typeof(Transform))]
    public static void LateInitialize(MonoBehaviour gameMainObject)
    {
        GameObject.Find("Mask").SetActive(false);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MonitorBackgroundTownController), "Awake")]
    public static void Awake(GameObject ____bgObject)
    {
        try
        {
            ____bgObject.transform.Find("BG").Find("Monitor_Mask").gameObject.SetActive(false);
        }
        catch
        {
            // ignored
        }
    }
}