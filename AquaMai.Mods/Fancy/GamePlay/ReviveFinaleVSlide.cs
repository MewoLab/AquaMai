using System.Collections.Generic;
using System.Reflection.Emit;
using AquaMai.Config.Attributes;
using HarmonyLib;
using Manager;

namespace AquaMai.Mods.Fancy.GamePlay;

[ConfigSection(
    name: "允许 1v1 星星",
    en: "Allow v-shaped slide with the same starting and ending point, such as \"1v1\" in Simai notation",
    zh: "允许形如 \"1v1\" 的，起点和终点相同的 v 型星星")]
public static class ReviveFinaleVSlide
{
    public static List<string> InsertData(List<string> list)
    {
        list[0] = "V_1.svg";
        return list;
    }
    
    public static List<List<SlideManager.HitArea>> InsertHitArea(List<List<SlideManager.HitArea>> list)
    {
        list[0] = [ 
            new SlideManager.HitArea
            {
                HitPoints = [InputManager.TouchPanelArea.A1],
                PushDistance = 156.42124938964844,
                ReleaseDistance = 43.27423858642578
            },
            new SlideManager.HitArea
            {
                HitPoints = [InputManager.TouchPanelArea.B1],
                PushDistance = 128.9917755126953,
                ReleaseDistance = 42.19921875
            },
            new SlideManager.HitArea
            {
                HitPoints = [InputManager.TouchPanelArea.C1],
                PushDistance = 218.6302947998047,
                ReleaseDistance = 42.19921875
            },
            new SlideManager.HitArea
            {
                HitPoints = [InputManager.TouchPanelArea.B1],
                PushDistance = 128.9917755126953,
                ReleaseDistance = 43.27423858642578
            },
            new SlideManager.HitArea
            {
                HitPoints = [InputManager.TouchPanelArea.A1],
                PushDistance = 156.42124938964844,
                ReleaseDistance = 0.0
            }
        ];
        return list;
    }

    [HarmonyPatch(typeof(SlideManager), MethodType.Constructor)]
    public static class SlideDataPatch
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var vDataList = AccessTools.Field(typeof(SlideManager), "_vDataList");
            var vHitAreaList = AccessTools.Field(typeof(SlideManager), "_vHitAreaList");

            foreach (var insn in instructions)
            {
                if (insn.StoresField(vDataList))
                {
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ReviveFinaleVSlide), nameof(InsertData)));
                }
                else if (insn.StoresField(vHitAreaList))
                {
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ReviveFinaleVSlide), nameof(InsertHitArea)));
                }

                yield return insn;
            }
        }
    }
}