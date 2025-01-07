using System.Collections.Generic;
using System.Reflection;
using AquaMai.Core;
using AquaMai.Core.Attributes;
using AquaMai.Config.Attributes;
using AquaMai.Mods.Fancy.GamePlay;
using HarmonyLib;
using MAI2.Util;
using Manager;
using MelonLoader;
using Monitor;
using Monitor.Common;
using Monitor.Entry;
using Monitor.Entry.Parts.Screens;
using UnityEngine;
using Fx;
using Type = System.Type;

namespace AquaMai.Mods.GameSystem;

// Hides the 2p (right hand side) UI.
// Note: this is not my original work. I simply interpreted the code and rewrote it as a mod.
[ConfigSection(
    en: "Single player: Show 1P only, at the center of the screen.",
    zh: "单人模式，不显示 2P")]
public class SinglePlayer
{
    [HarmonyPatch]
    public class WhateverInitialize
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            var lateInitialize = AccessTools.Method(typeof(Main.GameMain), "LateInitialize", [typeof(MonoBehaviour), typeof(Transform), typeof(Transform)]);
            if (lateInitialize is not null) return [lateInitialize];
            return [AccessTools.Method(typeof(Main.GameMain), "Initialize", [typeof(MonoBehaviour), typeof(Transform), typeof(Transform)])];
        }

        public static void Prefix(MonoBehaviour gameMainObject, ref Transform left, ref Transform right)
        {
            Vector3 position = Camera.main.gameObject.transform.position;
            Camera.main.gameObject.transform.position = new Vector3(position.x - 540f, position.y, position.z);
            right.localScale = Vector3.zero;
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(MeshButton), "IsPointInPolygon", new Type[] { typeof(Vector2[]), typeof(Vector2) })]
    public static bool IsPointInPolygon(Vector2[] polygon, ref Vector2 point, MeshButton __instance, ref bool __result)
    {
        __result = RectTransformUtility.RectangleContainsScreenPoint(__instance.GetComponent<RectTransform>(), point, Camera.main);
        return false;
    }

    [ConfigEntry(
        en: "Automatically skip the countdown when logging in with a card in single-player mode.",
        zh: "单人模式下刷卡登录直接进入下一个界面，无需跳过倒计时")]
    public static bool autoSkip = true;

    [EnableGameVersion(21500, noWarn: true)]
    [EnableIf(nameof(autoSkip))]
    [HarmonyPostfix]
    [HarmonyPatch(typeof(EntryMonitor), "DecideEntry")]
    public static void PostDecideEntry(EntryMonitor __instance)
    {
# if DEBUG
        MelonLogger.Msg("Confirm Entry");
# endif
        TimeManager.MarkGameStartTime();
        Singleton<EventManager>.Instance.UpdateEvent();
        Singleton<ScoreRankingManager>.Instance.UpdateData();
        __instance.Process.CreateDownloadProcess();
        __instance.ProcessManager.SendMessage(new Message(ProcessType.CommonProcess, 30001));
        __instance.ProcessManager.SendMessage(new Message(ProcessType.CommonProcess, 40000, 0, OperationInformationController.InformationType.Hide));
        __instance.Process.SetNextProcess();
    }

    // To prevent the "長押受付終了" overlay from appearing
    [EnableGameVersion(21500, noWarn: true)]
    [EnableIf(nameof(autoSkip))]
    [HarmonyPrefix]
    [HarmonyPatch(typeof(WaitPartner), "Open")]
    public static bool WaitPartnerPreOpen()
    {
        return false;
    }

    [ConfigEntry(
        en: "Fix hanabi effect under single-player mode (disabled automatically if HideHanabi is enabled).",
        zh: "修复单人模式下的烟花效果（如果启用了 HideHanabi，则会自动禁用）")]
    public static bool fixHanabi = true;

    private static bool fixHanabiDisableImplied = false;
    private static bool FixHanabiEnabled => fixHanabi && !fixHanabiDisableImplied;

    [EnableIf(nameof(FixHanabiEnabled))]
    [HarmonyPatch(typeof(TapCEffect), "SetUpParticle")]
    [HarmonyPostfix]
    public static void PostSetUpParticle(TapCEffect __instance, FX_Mai2_Note_Color ____particleControler)
    {
        var entities = ____particleControler.GetComponentsInChildren<ParticleSystemRenderer>(true);
        foreach (var entity in entities)
        {
            entity.maxParticleSize = 1f;
        }
    }

    public static void OnBeforePatch()
    {
        if (ConfigLoader.Config.GetSectionState(typeof(HideHanabi)).Enabled)
        {
            fixHanabiDisableImplied = true;
        }
    }

    public static void OnAfterPatch()
    {
        Core.Helpers.GuiSizes.SinglePlayer = true;
    }
}