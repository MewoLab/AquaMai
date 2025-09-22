using HarmonyLib;
using AquaMai.Config.Attributes;
using AquaMai.Core.Helpers;
using Process;
using UnityEngine;
using UnityEngine.UI;
using System.Reflection;
using System;
using MelonLoader;

namespace AquaMai.Mods.Fancy;

[ConfigSection(
    en: "Set Fade Animation",
    zh: "设置转场动画"
)]

public class SetFade
{
    [ConfigEntry(
        en: "Type: Non-Plus 0, Plus 1. (If SDEZ 1.60 can choose Festa 2)",
        zh: "类型: Non-Plus 0, Plus 1. (SDEZ 1.60 限定可选 Festa 2)")]
    public static readonly int FadeType = 0;


    private static bool isInitialized = false;
    private static Sprite[] subBGs = new Sprite[3];


    [HarmonyPrepare]
    public static bool SetFade_Prepare()
    {
        SetFade_Initialize();
        if (!isInitialized)
            MelonLogger.Msg("[SetFade] Initialization failed, this patch will not be applied.");
        return isInitialized;
    }

    private static void SetFade_Initialize()
    {
        bool areSubBGsValid;
        bool isFadeTypeValid;

        if (GameInfo.GameVersion != 26000)
        {
            subBGs[0] = Resources.Load<Sprite>("Process/ChangeScreen/Sprites/Sub_01");
            subBGs[1] = Resources.Load<Sprite>("Process/ChangeScreen/Sprites/Sub_02");
            areSubBGsValid = subBGs[0] != null && subBGs[1] != null;
            isFadeTypeValid = FadeType == 0 || FadeType == 1;
        }
        else
        {
            subBGs[0] = Resources.Load<Sprite>("Process/ChangeScreen/Sprites/Sub_01");
            subBGs[1] = Resources.Load<Sprite>("Process/ChangeScreen/Sprites/Sub_02");
            subBGs[2] = Resources.Load<Sprite>("Process/ChangeScreen/Sprites/Sub_03");
            areSubBGsValid = subBGs[0] != null && subBGs[1] != null && subBGs[2] != null;
            isFadeTypeValid = FadeType == 0 || FadeType == 1 || FadeType == 2;
        }

        if (!areSubBGsValid)
            MelonLogger.Msg($"[SwitchFade] Couldn't find SubBG sprites.");

        if (!isFadeTypeValid)
            MelonLogger.Msg($"[SwitchFade] Invalid FadeType.");

        isInitialized = areSubBGsValid && isFadeTypeValid;
    }







    // 在显示转场前启用临时patch替换预制体
    [HarmonyPrefix]
    [HarmonyPatch(typeof(FadeProcess), "OnStart")]
    public static void FadeProcessOnStartPreFix() { SetFade_ResourcesLoadPatch.Enable(); }
    [HarmonyPrefix]
    [HarmonyPatch(typeof(AdvertiseProcess), "InitFade")]
    public static void AdvertiseProcessInitFadePreFix() { SetFade_ResourcesLoadPatch.Enable(); }
    [HarmonyPrefix]
    [HarmonyPatch(typeof(NextTrackProcess), "OnStart")]
    public static void NextTrackProcessOnStartPreFix() { SetFade_ResourcesLoadPatch.Enable(); }

    // 在显示转场后禁用临时patch
    [HarmonyPostfix]
    [HarmonyPatch(typeof(FadeProcess), "OnStart")]
    public static void FadeProcessOnStartPostFix(GameObject[] ___fadeObject) { ReplaceSubBG(___fadeObject); }
    [HarmonyPostfix]
    [HarmonyPatch(typeof(AdvertiseProcess), "InitFade")]
    public static void AdvertiseProcessInitFadePostFix(GameObject[] ___fadeObject) { ReplaceSubBG(___fadeObject); }
    [HarmonyPostfix]
    [HarmonyPatch(typeof(NextTrackProcess), "OnStart")]
    public static void NextTrackProcessOnStartPostFix(GameObject[] ___fadeObject) { ReplaceSubBG(___fadeObject); }
    

    private static void ReplaceSubBG(GameObject[] fadeObjects)
    {
        SetFade_ResourcesLoadPatch.Disable();
        foreach (var monitor in fadeObjects)
        {
            var subBG = monitor.transform.Find("Canvas/Sub/Sub_ChangeScreen(Clone)/Sub_BG").GetComponent<Image>();
            subBG.sprite = subBGs[FadeType];
        }
    }
}



public static class SetFade_ResourcesLoadPatch
{
    private static bool isPatchEnabled = false;
    private static HarmonyLib.Harmony harmony = null;
    private static MethodInfo be_patched_method = null;
    private static MethodInfo harmony_patch_method = null;

    public static void Enable()
    {
        if (isPatchEnabled) return;

        harmony = new HarmonyLib.Harmony("com.AquaMai.Mods.Fancy.SetFade.SetFade_ResourcesLoadPatch");
        be_patched_method = typeof(Resources).GetMethod("Load", new[] { typeof(string), typeof(Type) });
        harmony_patch_method = typeof(SetFade_ResourcesLoadPatch).GetMethod("Prefix", BindingFlags.Static | BindingFlags.Public);

        harmony.Patch(be_patched_method, prefix: new HarmonyMethod(harmony_patch_method));
        isPatchEnabled = true;
    }

    public static void Disable()
    {
        if (!isPatchEnabled) return;
        harmony.Unpatch(be_patched_method, harmony_patch_method);
        isPatchEnabled = false;
    }

    public static bool Prefix(ref string path, Type systemTypeInstance, ref UnityEngine.Object __result)
    {
        // 神金实现，但因为是短暂的临时patch，所以不会影响性能（也许吧）
        if (path.StartsWith("Process/ChangeScreen/Prefabs/ChangeScreen_0")&& path != $"Process/ChangeScreen/Prefabs/ChangeScreen_0{SetFade.FadeType + 1}")
        {
            __result = Resources.Load($"Process/ChangeScreen/Prefabs/ChangeScreen_0{SetFade.FadeType + 1}", systemTypeInstance);
            return false;
        }
        return true;
    }
}
