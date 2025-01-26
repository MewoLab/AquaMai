using System.Collections.Generic;
using System.Linq;
using AquaMai.Config.Attributes;
using HarmonyLib;
using Manager;
using Manager.MaiStudio;
using MelonLoader;
using Monitor;
using Process;
using Util;

namespace AquaMai.Mods.Fix.Stability;

/**
 * Fix character selection crashing due to missing character data
 */
[ConfigSection(exampleHidden: true, defaultOn: true)]
public class FixMissingCharaCrash
{
    // Check if the return is null. If it is, make up a color
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CharacterSelectProces), "GetMapColorData")]
    public static void GetMapColorData(int colorID, ref CharacterMapColorData __result)
    {
        if (__result != null) return;

        // NOTE: should not reach here.
        // Fall back to the first map's color if the color is missing.
        var firstMapId = DataManager.Instance.GetMapDatas().First().Key;
        MelonLogger.Warning($"[FixMissingCharaCrash] CharacterMapColorData for [MapId={colorID}] is missing, falling back to [MapId={firstMapId}]");
        var mapColorData = MapMaster.GetSlotData(firstMapId);
        mapColorData.Load();
        __result = mapColorData;
    }

    // This is called when loading the music selection screen, to display characters on the top screen.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(CommonMonitor), "SetCharacterSlot", [typeof(MessageCharactorInfomationData)])]
    public static bool SetCharacterSlot(ref MessageCharactorInfomationData data, Dictionary<int, CharacterSlotData> ____characterSlotData)
    {
        // Some characters are not found in this dictionary. We simply skip loading those characters
        if (!____characterSlotData.ContainsKey(data.MapKey))
        {
            // NOTE: should not reach here.
            MelonLogger.Warning($"[FixMissingCharaCrash] Could not get CharacterSlotData for character [Index={data.Index}, MapKey={data.MapKey}], ignoring");
            return false;
        }

        return true;
    }

    // Initialize the missing map colors that are used by characters but correspond to non-existing maps.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(MapMaster), "Initialize")]
    public static void PostMapMasterInitialize(Dictionary<int, CharacterMapColorData> ____characterMapColors)
    {
        foreach (var (mapColorId, mapId) in GetMaybeAllMapColorIdToMapId())
        {
            if (!____characterMapColors.ContainsKey(mapId))
            {
                ____characterMapColors[mapId] = new CharacterMapColorData(mapId, mapColorId);
            }
        }
    }

    // Initialize the missing character slot data that are used by characters but correspond to non-existing maps.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CommonMonitor), "CreateCharacterSlotData")]
    public static void PostCreateCharacterSlotData(Dictionary<int, CharacterSlotData> ____characterSlotData)
    {
        foreach (var (mapColorId, mapId) in GetMaybeAllMapColorIdToMapId())
        {
            if (!____characterSlotData.ContainsKey(mapId))
            {
                MapColorData mapColorData = DataManager.Instance.GetMapColorData(mapColorId);
                ____characterSlotData[mapId] = new CharacterSlotData(mapId, mapColorData.Color ?? new Color24(), mapColorData.ColorDark ?? new Color24());
            }
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(DataManager), "GetMapColorData")]
    public static void PostGetMapColorData(int id, ref MapColorData __result)
    {
        // Fall back to the first color if the color is missing.
        if (__result == null)
        {
            var firstMapColor = DataManager.Instance.GetMapColorDatas().First();
            MelonLogger.Warning($"[FixMissingCharaCrash] MapColorData for [MapColorId={id}] is missing, falling back to [MapColorId={firstMapColor.Key}]");
            __result = firstMapColor.Value;
        }
    }

    private static Dictionary<int, int> GetMaybeAllMapColorIdToMapId()
    {
        var idsFromMap = DataManager.Instance
            .GetMapDatas()
            .Select(x => (x.Value.ColorId.id, x.Key));
        var idsFromCharaGenre = DataManager.Instance
            .GetCharas()
            .Select(x => (x.Value.color.id, x.Value.genre.id));
        // We don't know the mapping of map color IDs and non-existing map IDs, without a map or chara record.
        // But except some small IDs, the mapping is usually the same.
        // So, assuming the key is always the same as the color ID. This will not break anything.
        // Ideally, this contributes nothing since all map color IDs should be referenced as chara color IDs.
        var idsFromMapColor = DataManager.Instance
            .GetMapColorDatas()
            .Select(x => (x.Key, x.Key));
        return idsFromMap
            .Concat(idsFromCharaGenre)
            .Concat(idsFromMapColor)
            .GroupBy(x => x.Item1)
            .ToDictionary(x => x.Key, x => x.First().Item2);
    }
}
