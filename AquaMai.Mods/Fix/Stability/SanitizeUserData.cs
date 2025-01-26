using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AquaMai.Config.Attributes;
using AquaMai.Core.Helpers;
using Manager;
using MelonLoader;
using MelonLoader.TinyJSON;
using Net.VO.Mai2;

namespace AquaMai.Mods.Fix.Stability;

[ConfigSection(exampleHidden: true, defaultOn: true)]
public class SanitizeUserData
{
    public static void OnBeforePatch()
    {
        NetPacketHook.OnNetPacketComplete += OnNetPacketComplete;
    }

    private static Variant OnNetPacketComplete(string api, Variant request, Variant response)
    {
        var handlerMap = new Dictionary<string, Action<ProxyObject, ProxyObject>>
        {
            // ["GetUserPreviewApi"] = /* no need */,
            // ["GetUserFriendCheckApi"] = /* no need */,
            ["GetUserDataApi"] = OnUserDataResponse,
            // ["GetUserCardApi"] =  = /* no need */,
            ["GetUserCharacterApi"] = (_, response) => FilterListResponseById(api, response, "userCharacterList", "characterId", "GetCharas"),
            ["GetUserItemApi"] = OnUserItemResponse,
            ["GetUserCourseApi"] = (_, response) => FilterListResponseById(api, response, "userCourseList", "courseId", "GetCourses"),
            ["GetUserChargeApi"] = (_, response) => FilterListResponseById(api, response, "userChargeList", "chargeId", "GetTickets"),
            ["GetUserFavoriteApi"] = OnUserFavoriteResponse,
            // ["GetUserGhostApi"] = /* no need */,
            ["GetUserMapApi"] = (_, response) => FilterListResponseById(api, response, "userMapList", "mapId", "GetMapDatas"),
            // ["GetUserLoginBonusApi"] = /* no need */,
            // ["GetUserRegionApi"] = /* no need */,
            // ["GetUserRecommendRateMusicApi"] = /* no need */,
            // ["GetUserRecommendSelectionMusicApi"] = /* no need */,
            // ["GetUserOptionApi"] = /* no need */,
            ["GetUserExtendApi"] = OnUserExtendResponse,
            // ["GetUserRatingApi"] = /* no need */,
            // ["GetUserMusicApi"] = /* no need */,
            // ["GetUserPortraitApi"] = /* no need */,
            // ["GetUserActivityApi"] = /* no need */,
            // ["GetUserFriendSeasonRankingApi"] = /* no need */,
            ["GetUserFavoriteItemApi"] = OnUserFavoriteItemResponse,
            // ["GetUserRivalDataApi"] = /* no need */,
            // ["GetUserRivalMusicApi"] = /* no need */,
            // ["GetUserMissionDataApi"] = /* no need */,
            // ["GetUserFriendBonusApi"] = /* no need */,
            // ["GetUserIntimateApi"] = /* no need */,
            // ["GetUserShopStockApi"] = /* no need */,
            ["GetUserKaleidxScopeApi"] = (_, response) => FilterListResponseById(api, response, "userKaleidxScopeList", "gateId", "GetKaleidxScopeKeys"),
            // ["GetUserScoreRankingApi"] = /* no need */,
            // ["GetUserNewItemApi"] = /* no need */,
            // ["GetUserNewItemListApi"] = /* no need */,
        };
        if (handlerMap.TryGetValue(api, out var handler))
        {
            var requestObject = request is ProxyObject reqObj ? reqObj : [];
            var responseObject = response is ProxyObject resObj ? resObj : [];
            handler(requestObject, responseObject);
            return responseObject;
        }
        return null;
    }

    private static void OnUserDataResponse(ProxyObject _, ProxyObject response)
    {
        var userData = GetObjectOrSetDefault(response, "userData");
        SanitizeItemIdField(userData, "iconId", "GetIcons");
        SanitizeItemIdField(userData, "plateId", "GetPlates");
        SanitizeItemIdField(userData, "titleId", "GetTitles");
        SanitizeItemIdField(userData, "partnerId", "GetPartners");
        SanitizeItemIdField(userData, "frameId", "GetFrames");
        SanitizeItemIdField(userData, "selectMapId", "GetMapDatas");
        var charaSlot = GetArrayOrSetDefault(userData, "charaSlot");
        for (var i = 0; i < 5; i++)
        {
            if (charaSlot.Count <= i)
            {
                charaSlot.Add(new ProxyNumber(0));
            }
            else if (
                !JsonHelper.TryToInt32(charaSlot[i], out var charaSlotEntryInt) ||
                !SafelyCheckItemId("GetCharas", charaSlotEntryInt))
            {
                charaSlot.Add(new ProxyNumber(0));
                MelonLogger.Warning($"[SanitizeUserData] Filtered out invalid chara {charaSlot[i].ToJSON()} at index {i}");
            }
        }
        userData["charaSlot"] = ToProxyArray(charaSlot.Take(5));
    }

    private static void OnUserExtendResponse(ProxyObject _, ProxyObject response)
    {
        var userExtend = GetObjectOrSetDefault(response, "userExtend");
        SanitizeItemIdField(userExtend, "selectMusicId", "GetMusics");
        SanitizeItemIdField(userExtend, "selectDifficultyId", "GetMusicDifficultys");
        // categoryIndex?
        // musicIndex?
        SanitizeEnumFieldIfDefined(userExtend, "selectScoreType", ResolveEnumType("ConstParameter.ScoreKind"));
        SanitizeEnumFieldIfDefined(userExtend, "selectResultScoreViewType", ResolveEnumType("Process.ResultProcess.ResultScoreViewType"));
        SanitizeEnumFieldIfDefined(userExtend, "sortCategorySetting", ResolveEnumType("DB.SortTabID"));
        SanitizeEnumFieldIfDefined(userExtend, "sortMusicSetting", ResolveEnumType("DB.SortMusicID"));
        SanitizeEnumFieldIfDefined(userExtend, "playStatusSetting", ResolveEnumType("DB.PlaystatusTabID"));
    }

    private static void OnUserItemResponse(ProxyObject request, ProxyObject response)
    {
        var requestKind = (ItemKind)(
            request.TryGetValue("nextIndex", out var nextIndexVariant) &&
            JsonHelper.TryToInt64(nextIndexVariant, out var nextIndex)
                ? nextIndex / 10000000000L
                : 0);

        var filteredOutCount = FilterListResponse(
            null,
            response,
            "userItemList",
            userItem =>
                JsonHelper.TryToInt32(userItem["itemId"], out var itemId) &&
                SafelyCheckItemIdByKind(requestKind, itemId));

        if (filteredOutCount > 0)
        {
            MelonLogger.Warning($"[SanitizeUserData] Filtered out {filteredOutCount} invalid entries of kind {requestKind} in GetUserItemApi");
        }
    }

    private static void OnUserFavoriteResponse(ProxyObject request, ProxyObject response)
    {
        var requestKind = (ItemKind)(
            request.TryGetValue("itemKind", out var itemKindVariant) &&
            JsonHelper.TryToInt64(itemKindVariant, out var itemKind)
                ? itemKind
                : 0);

        var userFavorite = GetObjectOrSetDefault(response, "userFavorite");
        var itemIdList = GetArrayOrSetDefault(userFavorite, "itemIdList");
        var validItemIdList = itemIdList
            .Select(itemIdVariant =>
                JsonHelper.TryToInt32(itemIdVariant, out var itemId) &&
                SafelyCheckItemIdByKind(requestKind, itemId)
                    ? itemIdVariant
                    : null)
            .Where(itemIdVariant => itemIdVariant != null)
            .ToList();
        userFavorite["itemIdList"] = ToProxyArray(validItemIdList);

        var filteredOutCount = itemIdList.Count - validItemIdList.Count;
        if (filteredOutCount > 0)
        {
            MelonLogger.Warning($"[SanitizeUserData] Filtered out {filteredOutCount} invalid entries of kind {requestKind} in GetUserFavoriteApi");
        }
    }

    private static void OnUserFavoriteItemResponse(ProxyObject request, ProxyObject response)
    {
        // Older versions of the game don't have the FavoriteItemKind enum
        var enumType = AppDomain.CurrentDomain
            .GetAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .FirstOrDefault(type => type.FullName == "Net.VO.Mai2.FavoriteItemKind" && type.IsEnum);

        var requestKind = Enum.ToObject(
            enumType,
            request.TryGetValue("kind", out var kindVariant) &&
            JsonHelper.TryToInt64(kindVariant, out var kind)
                ? kind
                : 0);

        var userFavoriteItemList = GetArrayOrSetDefault(response, "userFavoriteItemList");
        var validItemList = userFavoriteItemList
            .Select(itemariant =>
                itemariant is ProxyObject itemObject &&
                itemObject.TryGetValue("id", out var idVariant) &&
                JsonHelper.TryToInt32(idVariant, out var id)
                    ? (id, itemObject)
                    : (0, null))
            .Where(tuple => tuple.itemObject != null)
            .Where(requestKind.ToString() switch
            {
                "FavoriteMusic" => tuple => SafelyCheckItemId("GetMusics", tuple.id),
                "RivalScore" => _ => true,
                _ => _ => true // Fail-safe for newly introduced favorite item kinds after mod release
            })
            .Select(tuple => tuple.itemObject)
            .ToList();
        response["userFavoriteItemList"] = ToProxyArray(validItemList);

        var filteredOutCount = userFavoriteItemList.Count - validItemList.Count;
        if (filteredOutCount > 0)
        {
            MelonLogger.Warning($"[SanitizeUserData] Filtered out {filteredOutCount} invalid entries of kind {requestKind} in GetUserFavoriteItemApi");
        }
    }

    private static ProxyObject GetObjectOrSetDefault(ProxyObject response, string fieldName)
    {
        if (
            !response.TryGetValue(fieldName, out var fieldVariant) ||
            fieldVariant is not ProxyObject field)
        {
            field = [];
            response[fieldName] = field;
        }
        return field;
    }

    private static ProxyArray GetArrayOrSetDefault(ProxyObject response, string fieldName)
    {
        if (
            !response.TryGetValue(fieldName, out var fieldVariant) ||
            fieldVariant is not ProxyArray field)
        {
            field = [];
            response[fieldName] = field;
        }
        return field;
    }

    private static int FilterListResponse(string logApiName, ProxyObject response, string listFieldName, Func<ProxyObject, bool> isValidEntry)
    {
        var before = GetArrayOrSetDefault(response, listFieldName);
        var after = GetArrayOrSetDefault(response, listFieldName)
            .Select(entry => entry is ProxyObject entryObject ? entryObject : null)
            .Select(entry => isValidEntry(entry) ? entry : null)
            .Where(entry => entry != null)
            .ToList();
        response[listFieldName] = ToProxyArray(after);
        var filteredOutCount = before.Count - after.Count;
        if (logApiName != null && filteredOutCount > 0)
        {
            MelonLogger.Warning($"[SanitizeUserData] Filtered out {filteredOutCount} invalid entries in {logApiName}");
        }
        return filteredOutCount;
    }

    private static int FilterListResponseById(string logApiName, ProxyObject response, string listFieldName, string idFieldName, string dataManagerGetDictionaryMethod) =>
        FilterListResponse(
            logApiName,
            response,
            listFieldName,
            listEntry =>
                listEntry.TryGetValue(idFieldName, out var idVariant) &&
                JsonHelper.TryToInt32(idVariant, out var idInt) &&
                SafelyCheckItemId(dataManagerGetDictionaryMethod, idInt));

    private static void SanitizeInt32Field(ProxyObject obj, string fieldName, Func<int, bool> isValid, int defaultValue)
    {
        if (
            !obj.TryGetValue(fieldName, out var fieldVariant) ||
            !JsonHelper.TryToInt32(fieldVariant, out var fieldValue) ||
            !isValid(fieldValue))
        {
            MelonLogger.Warning($"[SanitizeUserData] Set value of invalid int32 field {fieldName} from {fieldVariant?.ToJSON() ?? "null"} to {defaultValue}");
            obj[fieldName] = new ProxyNumber(defaultValue);
        }
    }

    private static void SanitizeEnumFieldIfDefined(ProxyObject obj, string fieldName, System.Type enumType) =>
        SanitizeInt32Field(
            obj,
            fieldName,
            value => enumType == null || Enum.IsDefined(enumType, value),
            enumType == null ? 0 : (int)enumType.GetEnumValues().GetValue(0));

    private static void SanitizeItemIdField(ProxyObject obj, string fieldName, string dataManagerGetDictionaryMethod) =>
        SanitizeInt32Field(
            obj,
            fieldName,
            itemId => SafelyCheckItemId(dataManagerGetDictionaryMethod, itemId),
            SafelyGetDefaultItemId(dataManagerGetDictionaryMethod));

    // The corresponding DataManager methods may not exist in all game versions
    private static object SafelyGetDataMangerDictionary(string dataManagerGetDictionaryMethod)
    {
        return typeof(DataManager)
            .GetMethod(dataManagerGetDictionaryMethod, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Invoke(DataManager.Instance, []);
    }

    private static int SafelyGetDefaultItemId(string dataManagerGetDictionaryMethod)
    {
        var dictionary = SafelyGetDataMangerDictionary(dataManagerGetDictionaryMethod);
        var enumerator = dictionary
            .GetType()
            .GetMethod("GetEnumerator", BindingFlags.Instance | BindingFlags.Public)
            .Invoke(dictionary, []) as IEnumerator;
        return !enumerator.MoveNext()
            ? 0
            : enumerator.Current
                .GetType()
                .GetProperty("Key", BindingFlags.Instance | BindingFlags.Public)
                .GetValue(enumerator.Current) as int? ?? 0;
    }

    private static bool SafelyCheckItemId(string dataManagerGetDictionaryMethod, int itemId)
    {
        var dictionary = SafelyGetDataMangerDictionary(dataManagerGetDictionaryMethod);
        return dictionary
            .GetType()
            .GetMethod("ContainsKey", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Invoke(dictionary, [itemId]) as bool? ?? false;
    }

    private static bool SafelyCheckItemIdByKind(ItemKind itemKind, int itemId) =>
        itemKind.ToString() switch
        {
            "Plate" => SafelyCheckItemId("GetPlates", itemId),
            "Title" => SafelyCheckItemId("GetTitles", itemId),
            "Icon" => SafelyCheckItemId("GetIcons", itemId),
            "Present" => DataManager.Instance.ConvertPresentID2Item(itemId, out _, out _),
            // It's safe to have invalid music IDs in the user item list
            "Music" => true,
            "MusicMas" => true,
            "MusicRem" => true,
            "MusicSrg" => true,
            "Character" => SafelyCheckItemId("GetCharas", itemId),
            "Partner" => SafelyCheckItemId("GetPartners", itemId),
            "Frame" => SafelyCheckItemId("GetFrames", itemId),
            "Ticket" => SafelyCheckItemId("GetTickets", itemId),
            "Mile" => true,
            "IntimateItem" => true,
            "KaleidxScopeKey" => SafelyCheckItemId("GetKaleidxScopeKeys", itemId),
            _ => true, // Fail-safe for newly introduced item kinds after mod release
        };

    private static ProxyArray ToProxyArray(IEnumerable<Variant> values)
    {
        var array = new ProxyArray();
        foreach (var value in values)
        {
            array.Add(value);
        }
        return array;
    }

    private static System.Type ResolveEnumType(string enumName)
    {
        return AppDomain.CurrentDomain
            .GetAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .FirstOrDefault(type => type.FullName == enumName && type.IsEnum);
    }
}
