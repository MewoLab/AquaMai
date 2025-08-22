using System.Collections.Generic;
using System.Linq;
using AMDaemon;
using AquaMai.Config.Attributes;
using Comio.BD15070_4;
using Mecha;
using HarmonyLib;
using System.Reflection.Emit;
using Manager;
using UnityEngine;

namespace AquaMai.Mods.GameSystem;

[ConfigSection(
    en: "Skip BoardNo check to use the old cab light board 837-15070-02",
    zh: "跳过 BoardNo 检查以使用 837-15070-02（旧框灯板）")]
public class SkipBoardNoCheck
{
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(BoardCtrl15070_4), "_md_initBoard_GetBoardInfo")]
    static IEnumerable<CodeInstruction> Transpiler1(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);
        bool patched = false;

        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].opcode == OpCodes.Callvirt &&
                codes[i].operand != null &&
                codes[i].operand.ToString().Contains("IsEqual"))
            {
                codes.Insert(i, new CodeInstruction(OpCodes.Pop));
                codes.Insert(i, new CodeInstruction(OpCodes.Pop));
                codes.Insert(i, new CodeInstruction(OpCodes.Ldc_I4_1));
                patched = true;
                break;
            }
        }

        if (patched)
        {
            MelonLoader.MelonLogger.Msg("修补 BoardCtrl15070_4._md_initBoard_GetBoardInfo 方法成功");
        }
        else
        {
            MelonLoader.MelonLogger.Warning("未找到需要修补的代码位置：BoardCtrl15070_4._md_initBoard_GetBoardInfo");
        }

        return codes;
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(Bd15070_4Control), "IsNeedFirmUpdate")]
    static IEnumerable<CodeInstruction> Transpiler2(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);
        bool patched = false;

        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].opcode == OpCodes.Callvirt &&
                codes[i].operand != null &&
                codes[i].operand.ToString().Contains("IsEqual"))
            {
                codes.Insert(i, new CodeInstruction(OpCodes.Pop));
                codes.Insert(i, new CodeInstruction(OpCodes.Pop));
                codes.Insert(i, new CodeInstruction(OpCodes.Ldc_I4_1));
                patched = true;
                break;
            }
        }

        if (patched)
        {
            MelonLoader.MelonLogger.Msg("修补 Bd15070_4Control.IsNeedFirmUpdate 方法成功");
        }
        else
        {
            MelonLoader.MelonLogger.Warning("未找到需要修补的代码位置：Bd15070_4Control.IsNeedFirmUpdate");
        }

        return codes;
    }
}