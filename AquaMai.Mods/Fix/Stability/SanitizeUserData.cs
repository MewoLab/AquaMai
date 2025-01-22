using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AquaMai.Config.Attributes;
using AquaMai.Core.Helpers;
using AquaMai.Mods.Utils;
using HarmonyLib;
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
            var requestObject = request is ProxyObject reqObj ? reqObj : new ProxyObject();
            var responseObject = response is ProxyObject resObj ? resObj : new ProxyObject();
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
        SanitizeItemIdField(userExtend, "categoryIndex", "GetMusicGenres");
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
                itemIdVariant is ProxyNumber itemIdNumber &&
                JsonHelper.TryToInt32(itemIdNumber, out var itemId) &&
                SafelyCheckItemIdByKind(requestKind, itemId)
                    ? itemIdNumber
                    : null)
            .Where(itemIdNumber => itemIdNumber != null)
            .ToList();

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
                idVariant is ProxyNumber idNumber &&
                JsonHelper.TryToInt32(idNumber, out var idInt) &&
                SafelyCheckItemId(dataManagerGetDictionaryMethod, idInt));

    private static void SanitizeNumberField(ProxyObject response, string fieldName, Func<ProxyNumber, bool> isValid, ProxyNumber defaultValue)
    {
        if (
            !response.TryGetValue(fieldName, out var fieldVariant) ||
            fieldVariant is not ProxyNumber fieldNumber ||
            !isValid(fieldNumber))
        {
            MelonLogger.Warning($"[SanitizeUserData] Set value of invalid number field {fieldName} from {fieldVariant?.ToJSON() ?? "null"} to {defaultValue.ToJSON()}");
            response[fieldName] = defaultValue;
        }
    }

    private static void SanitizeInt32Field(ProxyObject response, string fieldName, Func<int, bool> isValid, int defaultValue) =>
        SanitizeNumberField(
            response,
            fieldName,
            field => JsonHelper.TryToInt32(field, out var value) && isValid(value),
            new ProxyNumber(defaultValue));

    private static void SanitizeEnumFieldIfDefined(ProxyObject response, string fieldName, System.Type enumType) =>
        SanitizeInt32Field(
            response,
            fieldName,
            value => enumType == null || Enum.IsDefined(enumType, value),
            enumType == null ? 0 : (int)enumType.GetEnumValues().GetValue(0));

    private static void SanitizeItemIdField(ProxyObject response, string fieldName, string dataManagerGetDictionaryMethod) =>
        SanitizeInt32Field(
            response,
            fieldName,
            itemId => SafelyCheckItemId(dataManagerGetDictionaryMethod, itemId),
            SafelyGetDefaultItemId(dataManagerGetDictionaryMethod));

    private static int SafelyGetDefaultItemId(string dataManagerGetDictionaryMethod)
    {
        var dictionary = Traverse
            .Create(DataManager.Instance)
            .Method(dataManagerGetDictionaryMethod)
            .GetValue();
        var enumerator = Traverse
            .Create(dictionary)
            .Method("GetEnumerator")
            .GetValue<IEnumerator>();
        return !enumerator.MoveNext()
            ? 0
            : Traverse
                .Create(enumerator.Current)
                .Property("Key")
                .GetValue<int>();
    }

    private static bool SafelyCheckItemId(string dataManagerGetDictionaryMethod, int itemId)
    {
        var dictionary = Traverse
            .Create(DataManager.Instance)
            .Method(dataManagerGetDictionaryMethod, itemId)
            .GetValue();
        return Traverse
            .Create(dictionary)
            .Method("ContainsKey", itemId)
            .GetValue<bool>();
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
