using AquaMai.Config.Attributes;
using AquaMai.Core.Attributes;
using MAI2System;
using Manager;
using Manager.MaiStudio;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using MelonLoader;
using System.Collections;

namespace AquaMai.Mods.GameSystem;

[ConfigSection(
    en: """
        Unlock normally locked (including normally non-unlockable) game content.
        Anything unlocked (except the characters you selected) by this mod will not be uploaded your account.
        You'll still "get" those musics/collections/courses by normal plays.
        """,
    zh: """
        解锁原本锁定（包括正常途径无法解锁）的游戏内容
        由本 Mod 解锁的内容（除了被你选择的角色以外）不会上传到你的账户
        你仍然可以通过正常游玩来「获得」那些乐曲/收藏品/段位
        """)]
public class Unlock
{
    public static void OnBeforeEnableCheck()
    {
        InitializeCollectionHooks();
    }

    [ConfigEntry(
        en: "Unlock maps that are not in this version.",
        zh: "解锁游戏里所有的区域，包括非当前版本的（并不会帮你跑完）")]
    private static readonly bool maps = true;

    [EnableIf(nameof(maps))]
    [HarmonyPrefix]
    [HarmonyPatch(typeof(MapData), "get_OpenEventId")]
    public static bool get_OpenEventId(ref StringID __result)
    {
        // For any map, return the event ID 1 to unlock it
        var id = new Manager.MaiStudio.Serialize.StringID
        {
            id = 1,
            str = "無期限常時解放"
        };
        
        var sid = new StringID();
        sid.Init(id);
        
        __result = sid;
        return false;
    }

    [ConfigEntry(
        en: "Unlock all songs (Same as setting AllOpen=1 in mai2.ini).",
        zh: "解锁所有乐曲（和 mai2.ini 里面设置 AllOpen=1 是一样的效果）")]
    private static readonly bool songs = true;

    [EnableIf(nameof(songs))]
    [HarmonyPrefix]
    [HarmonyPatch(typeof(MAI2System.Config), "IsAllOpen", MethodType.Getter)]
    public static bool IsAllOpen(ref bool __result)
    {
        __result = true;
        return false;
    }

    [ConfigEntry(
        en: "Unlock normally event-only tickets.",
        zh: "解锁游戏里所有可能的跑图券")]
    private static readonly bool tickets = true;

    [EnableIf(nameof(tickets))]
    [HarmonyPrefix]
    [HarmonyPatch(typeof(TicketData), "get_ticketEvent")]
    public static bool get_ticketEvent(ref StringID __result)
    {
        // For any ticket, return the event ID 1 to unlock it
        var id = new Manager.MaiStudio.Serialize.StringID
        {
            id = 1,
            str = "無期限常時解放"
        };

        var sid = new StringID();
        sid.Init(id);

        __result = sid;
        return false;
    }

    [EnableIf(nameof(tickets))]
    [HarmonyPrefix]
    [HarmonyPatch(typeof(TicketData), "get_maxCount")]
    public static bool get_maxCount(ref int __result)
    {
        // Modify the maxTicketNum to 0
        // this is because TicketManager.GetTicketData adds the ticket to the list if either
        // the player owns at least one ticket or the maxTicketNum = 0
        __result = 0;
        return false;
    }

    [ConfigEntry(
        en: "Unlock all course-mode courses (no need to reach 10th dan to play \"real\" dan).",
        zh: "解锁所有段位模式的段位（不需要十段就可以打真段位）")]
    private static readonly bool courses = true;

    [EnableIf(nameof(courses))]
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CourseData), "get_eventId")]
    public static void get_eventId(ref StringID __result)
    {
        if (__result.id == 0) return; // Should not be unlocked

        // Return the event ID 1 to unlock it
        var id = new Manager.MaiStudio.Serialize.StringID
        {
            id = 1,
            str = "無期限常時解放"
        };

        var sid = new StringID();
        sid.Init(id);

        __result = sid;
    }

    [EnableIf(nameof(courses))]
    [HarmonyPrefix]
    [HarmonyPatch(typeof(CourseData), "get_isLock")]
    public static bool get_isLock(ref bool __result)
    {
        __result = false;
        return false;
    }

    [ConfigEntry(
        en: "Unlock Utage without the need of DXRating 10000.",
        zh: "不需要万分也可以进宴会场")]
    private static readonly bool utage = true;

    [EnableIf(nameof(utage))]
    [EnableGameVersion(24000)]
    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameManager), "CanUnlockUtageTotalJudgement")]
    public static bool CanUnlockUtageTotalJudgement(out ConstParameter.ResultOfUnlockUtageJudgement result1P, out ConstParameter.ResultOfUnlockUtageJudgement result2P)
    {
        result1P = ConstParameter.ResultOfUnlockUtageJudgement.Unlocked;
        result2P = ConstParameter.ResultOfUnlockUtageJudgement.Unlocked;
        return false;
    }

    private static readonly List<
    (
        string configField,
        string collectionProcessMethod,
        string userDataProperty,
        string dataManagerMethod
    )> collectionHookSpecification =
    [
        (nameof(titles), "CreateTitleData", "TitleList", "GetTitles"),
        (nameof(icons), "CreateIconData", "IconList", "GetIcons"),
        (nameof(plates), "CreatePlateData", "PlateList", "GetPlates"),
        (nameof(frames), "CreateFrameData", "FrameList", "GetFrames"),
        (nameof(partners), "CreatePartnerData", "PartnerList", "GetPartners"),
    ];

    [ConfigEntry(
        en: "Unlock all titles.",
        zh: "解锁所有称号"
    )]
    private static readonly bool titles = true;

    [ConfigEntry(
        en: "Unlock all icons.",
        zh: "解锁所有头像"
    )]
    private static readonly bool icons = true;

    [ConfigEntry(
        en: "Unlock all plates.",
        zh: "解锁所有姓名框"
    )]
    private static readonly bool plates = true;

    [ConfigEntry(
        en: "Unlock all frames.",
        zh: "解锁所有背景"
    )]
    private static readonly bool frames = true;

    [ConfigEntry(
        en: "Unlock all partners.",
        zh: "解锁所有搭档"
    )]
    private static readonly bool partners = true;

    private static List<
    (
        MethodInfo collectionProcessMethod,
        PropertyInfo userDataProperty,
        MethodInfo dataManagerMethod
    )> collectionHooks;

    private static void InitializeCollectionHooks()
    {
        collectionHooks = collectionHookSpecification
            .Where(spec =>
                typeof(Unlock)
                    .GetField(spec.configField, BindingFlags.Static | BindingFlags.NonPublic)
                    .GetValue(null) as bool? ?? false)
            .Select(spec =>
            (
                collectionProcessMethod: typeof(CollectionProcess)
                    .GetMethod(spec.collectionProcessMethod, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                userDataProperty: typeof(UserData)
                    .GetProperty(spec.userDataProperty, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                dataManagerMethod: typeof(DataManager)
                    .GetMethod(spec.dataManagerMethod, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ))
            .Where(target =>
                target.collectionProcessMethod != null &&
                target.userDataProperty != null &&
                target.dataManagerMethod != null)
            .ToList();
        MelonLogger.Msg($"[Unlock.CollectionHook] Collection hooks generated = {collectionHooks.Count()}");
    }

    private static bool CollectionHookEnabled => collectionHooks.Count > 0;

    [EnableIf(typeof(Unlock), nameof(CollectionHookEnabled))]
    [HarmonyPatch]
    public class CollectionHook
    {
        private static readonly Dictionary<MethodInfo, List<Manager.UserDatas.UserItem>> allUnlockedItemsCache = [];

        public static List<Manager.UserDatas.UserItem> GetAllUnlockedItemList(MethodInfo dataManagerMethod)
        {
            if (allUnlockedItemsCache.TryGetValue(dataManagerMethod, out var result))
            {
                return result;
            }
            result = dataManagerMethod.Invoke(DataManager.Instance, null) is not IEnumerable dictionary
                ? []
                : dictionary
                    .Cast<object>()
                    .Select(pair =>
                        pair
                            .GetType()
                            .GetProperty("Key")
                            .GetValue(pair) is not int id
                                ? null
                                : new Manager.UserDatas.UserItem
                                  {
                                      itemId = id,
                                      stock = 1,
                                      isValid = true
                                  })
                    .ToList();
            allUnlockedItemsCache[dataManagerMethod] = result;
            MelonLogger.Msg($"[Unlock.CollectionHook] {dataManagerMethod.Name} generated = {result.Count}");
            return result;
        }

        public static IEnumerable<MethodBase> TargetMethods() => collectionHooks.Select(target => target.collectionProcessMethod);

        public record PropertyChangeLog(object From, object To);

        public static void Prefix(out Dictionary<PropertyInfo, PropertyChangeLog> __state)
        {
            __state = [];
            ModifyUserData(false, ref __state);
        }

        public static void Postfix(Dictionary<PropertyInfo, PropertyChangeLog> __state)
        {
            ModifyUserData(false, ref __state);
        }

        private static void ModifyUserData(bool restore, ref Dictionary<PropertyInfo, PropertyChangeLog> backup)
        {
            for (int i = 0; i < 2; i++)
            {
                var userData = UserDataManager.Instance.GetUserData(i);
                if (!userData.IsEntry) continue;
                foreach (var (_, userDataProperty, dataManagerMethod) in collectionHooks)
                {
                    var currentValue = userDataProperty.GetValue(userData);
                    if (restore)
                    {
                        if (!backup.TryGetValue(userDataProperty, out var backupData))
                        {
                            MelonLogger.Error($"[Unlock.CollectionHook] Failed to restore {userDataProperty.Name} to the original value. Backup data not found.");
                            continue;
                        }
                        else if (currentValue != backupData.From)
                        {
                            MelonLogger.Error($"[Unlock.CollectionHook] Failed to restore {userDataProperty.Name} to the original value. Value changed unexpectedly, incompatible mods loaded?");
                            continue;
                        }
                        userDataProperty.SetValue(userData, backupData.From);
                    }
                    else
                    {
                        var allUnlockedItems = GetAllUnlockedItemList(dataManagerMethod);
                        backup[userDataProperty] = new(From: currentValue, To: allUnlockedItems);
                        userDataProperty.SetValue(userData, allUnlockedItems);
                    }
                    MelonLogger.Msg($"[Unlock.CollectionHook] {userDataProperty.Name} {(restore ? "restored" : "modified")}.");
                }
            }
        }
    }
}
