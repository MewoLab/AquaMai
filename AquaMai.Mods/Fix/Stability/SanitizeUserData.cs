using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AquaMai.Config.Attributes;
using AquaMai.Mods.Utils;
using HarmonyLib;
using Manager;
using MelonLoader.TinyJSON;
using Net.VO.Mai2;

namespace AquaMai.Mods.Fix.Stability;

[ConfigSection(exampleHidden: true, defaultOn: true)]
public class SanitizeUserData
{
    public static void OnBeforePatch()
    {
        NetPacketHook.OnNetPacketResponse += OnNetPacketResponse;
    }

    private static Variant OnNetPacketResponse(string api, Variant json)
    {
        var handlerMap = new Dictionary<string, Func<ProxyObject, ProxyObject>>
        {
            ["GetUserItemApi"] = OnUserItemResponse
        };
        if (handlerMap.TryGetValue(api, out var handler))
        {
            if (json is ProxyObject proxyObject)
            {
                return handler(proxyObject);
            }
            else
            {
                return handler([]);
            }
        }
        return null;
    }

    private static ProxyObject OnUserItemResponse(ProxyObject response)
    {
        if (
            !response.TryGetValue("userItemList", out var userItemListVariant) ||
            userItemListVariant is not ProxyArray userItemListArray)
        {
            userItemListArray = [];
        }

        var userItemList = userItemListArray
            .Select(entry => entry is ProxyObject userItem ? userItem : null)
            .Where(userItem => userItem != null)
            .Where(userItem =>
            {
                if (
                    !TryToInt32(userItem["itemKind"], out var itemKindInt) ||
                    !Enum.TryParse<ItemKind>(itemKindInt.ToString(), out var itemKind) ||
                    !TryToInt32(userItem["itemId"], out var itemId))
                {
                    return false;
                }

                return itemKind.ToString() switch
                {
                    "Plate" => SafelyCheckItemId("GetPlates", itemId),
                    "Title" => SafelyCheckItemId("GetTitles", itemId),
                    "Icon" => SafelyCheckItemId("GetIcons", itemId),
                    "Present" => DataManager.Instance.ConvertPresentID2Item(itemId, out _, out _),
                    "Music" => SafelyCheckItemId("GetMusics", itemId),
                    "MusicMas" => SafelyCheckItemId("GetMusics", itemId),
                    "MusicRem" => SafelyCheckItemId("GetMusics", itemId),
                    "MusicSrg" => SafelyCheckItemId("GetMusics", itemId),
                    "Character" => SafelyCheckItemId("GetCharas", itemId),
                    "Partner" => SafelyCheckItemId("GetPartners", itemId),
                    "Frame" => SafelyCheckItemId("GetFrames", itemId),
                    "Ticket" => SafelyCheckItemId("GetTickets", itemId),
                    "Mile" => true,
                    "IntimateItem" => true,
                    "KaleidxScopeKey" => SafelyCheckItemId("GetKaleidxScopeKeys", itemId),
                    _ => true, // Fail-safe for newly introduced item kinds after mod release
                };
            })
            .ToList();

        response["userItemList"] = userItemListArray;
        return response;
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

    private static bool TryToInt32(Variant variant, out int result)
    {
        if (variant is ProxyNumber proxyNumber)
        {
            try
            {
                result = proxyNumber.ToInt32(CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {}
        }
        result = 0;
        return false;
    }
}
