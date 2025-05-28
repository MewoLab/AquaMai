using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using AquaMai.Config.Attributes;
using HarmonyLib;
using Manager;
using Monitor;
using Monitor.MapResult;
using Monitor.MovieCheck;

namespace AquaMai.Mods.Fix;

[ConfigSection(exampleHidden: true, defaultOn: true)]
public class ParseWithCultureInvariant
{
    [HarmonyPatch]
    public class FixParseNumber
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(SlideManager), "loadXML");
            yield return AccessTools.Method(typeof(DebugLogCollector), "LoadCounter");
            yield return AccessTools.Method(typeof(MenuCardObject), "SetVolume");
            yield return AccessTools.Method(typeof(NaviCharaDebugView), "UpdateNaviChara");
            yield return AccessTools.Method(typeof(SimpleSettingMonitor), "SetVolume");
            yield return AccessTools.Method(
                typeof(DLMusicScoreManager), "ExistsMusicScore", [typeof(string), typeof(int), typeof(string).MakeByRefType()]);
            yield return AccessTools.Method(typeof(NotesReader), "loadHeader");
            yield return AccessTools.Method(typeof(Manager.Version), "init");
            yield return AccessTools.Method(typeof(MapStockController.StockOdometerNumber), "SetNumber");
            yield return AccessTools.Method(typeof(TrackStartMonitor), "SetTrackStart");
            yield return AccessTools.Method(typeof(BonusNumber), "SetNumber");
            yield return AccessTools.Method(typeof(CharaLevelNumber), "SetNumber");
            yield return AccessTools.Method(typeof(OdometerNumber), "SetNumber");
            yield return AccessTools.Method(typeof(MovieCheckMonitor), "GetSelectDropDown");
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var instrs = new List<CodeInstruction>(instructions);
            
            var methodIntParse = AccessTools.Method(typeof(int), "Parse", [typeof(string)]);
            var methodIntParseFull = AccessTools.Method(
                typeof(int), "Parse", [typeof(string), typeof(NumberStyles), typeof(IFormatProvider)]);
            var methodFloatParse = AccessTools.Method(typeof(float), "Parse", [typeof(string)]);
            var methodFloatParseFull = AccessTools.Method(
                typeof(float), "Parse", [typeof(string), typeof(NumberStyles), typeof(IFormatProvider)]);
            var methodGetInvariantCulture = AccessTools.PropertyGetter(typeof(CultureInfo), "InvariantCulture");
        
            for (var i = 0; i < instrs.Count; i++)
            {
                var oldInstruction = instrs[i];

                if (oldInstruction.Calls(methodFloatParse))
                {
                    instrs.RemoveAt(i);
                    instrs.InsertRange(
                        i,
                        [
                            new CodeInstruction(OpCodes.Ldc_I4, (int)(NumberStyles.Float | NumberStyles.AllowThousands))
                                .MoveLabelsFrom(oldInstruction),
                            new CodeInstruction(OpCodes.Call, methodGetInvariantCulture),
                            new CodeInstruction(OpCodes.Call, methodFloatParseFull),
                        ]);
                }
                else if (oldInstruction.Calls(methodIntParse))
                {
                    instrs.RemoveAt(i);
                    instrs.InsertRange(
                        i,
                        [
                            new CodeInstruction(OpCodes.Ldc_I4, (int)NumberStyles.Integer)
                                .MoveLabelsFrom(oldInstruction),
                            new CodeInstruction(OpCodes.Call, methodGetInvariantCulture),
                            new CodeInstruction(OpCodes.Call, methodIntParseFull),
                        ]);
                }
            }

            return instrs;
        }
    }
}
