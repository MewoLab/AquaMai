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
using Manager.UserDatas;
using Net.VO.Mai2;
using Process;
using Util;

namespace AquaMai.Mods.GameSystem;

[ConfigSection(
    en: """
        Unlock normally locked (including normally non-unlockable) game content.
        Anything unlocked (except the characters you leveled-up) by this mod will not be uploaded your account.
        You'll still "get" those musics/collections/courses by normal plays.
        """,
    zh: """
        解锁原本锁定（包括正常途径无法解锁）的游戏内容
        由本 Mod 解锁的内容（除了被你升级过的角色以外）不会上传到你的账户
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

    [EnableIf(typeof(Unlock), nameof(maps))]
    public class MapHook
    {
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

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UserMapData), "get_IsLock")]
        public static bool get_IsLock(ref bool __result)
        {
            __result = false;
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MapMaster), "IsLock")]
        public static bool PreIsLock(ref bool __result)
        {
            __result = false;
            return false;
        }
    }

    [ConfigEntry(
        en: "Unlock all songs (Same as setting AllOpen=1 in mai2.ini).",
        zh: "解锁所有乐曲（和 mai2.ini 里面设置 AllOpen=1 是一样的效果）")]
    private static readonly bool songs = true;

    [EnableIf(typeof(Unlock), nameof(songs))]
    public class SongHook
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MAI2System.Config), "IsAllOpen", MethodType.Getter)]
        public static bool IsAllOpen(ref bool __result)
        {
            __result = true;
            return false;
        }
    }

    [ConfigEntry(
        en: "Unlock normally event-only tickets.",
        zh: "解锁游戏里所有可能的跑图券")]
    private static readonly bool tickets = true;

    [EnableIf(typeof(Unlock), nameof(tickets))]
    public class TicketHook
    {
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
    }

    [ConfigEntry(
        en: "Unlock all course-mode courses (no need to reach 10th dan to play \"real\" dan).",
        zh: "解锁所有段位模式的段位（不需要十段就可以打真段位）")]
    private static readonly bool courses = true;

    [EnableIf(typeof(Unlock), nameof(courses))]
    public class CourseHook
    {
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

        [HarmonyPrefix]
        [HarmonyPatch(typeof(CourseData), "get_isLock")]
        public static bool get_isLock(ref bool __result)
        {
            __result = false;
            return false;
        }
    }

    [ConfigEntry(
        en: "Unlock Utage without the need of DXRating 10000.",
        zh: "不需要万分也可以进宴会场")]
    private static readonly bool utage = true;

    [EnableIf(typeof(Unlock), nameof(utage))]
    [EnableGameVersion(24000)]
    public class UtageHook
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(GameManager), "CanUnlockUtageTotalJudgement")]
        public static bool CanUnlockUtageTotalJudgement(out ConstParameter.ResultOfUnlockUtageJudgement result1P, out ConstParameter.ResultOfUnlockUtageJudgement result2P)
        {
            result1P = ConstParameter.ResultOfUnlockUtageJudgement.Unlocked;
            result2P = ConstParameter.ResultOfUnlockUtageJudgement.Unlocked;
            return false;
        }
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
    }

    private static bool CollectionHookEnabled => collectionHooks.Count > 0;

    // The data in collection process is initialized in CreateXXXData() methods
    // Hook those method, set corrsponding UserData property to the all unlocked list and restore it after the method call
    // So the all unlocked items list will not be uploaded to the server
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
                            .GetValue(pair))
                    .Select(id =>
                        new Manager.UserDatas.UserItem
                        {
                            itemId = (int)id,
                            stock = 1,
                            isValid = true
                        })
                    .ToList();
            allUnlockedItemsCache[dataManagerMethod] = result;
            return result;
        }

        public static IEnumerable<MethodBase> TargetMethods() => collectionHooks.Select(target => target.collectionProcessMethod);

        public record PropertyChangeLog(object From, object To);

        public static void Prefix(out Dictionary<UserData, Dictionary<PropertyInfo, PropertyChangeLog>> __state)
        {
            __state = [];
            ModifyUserData(false, ref __state);
        }

        public static void Postfix(Dictionary<UserData, Dictionary<PropertyInfo, PropertyChangeLog>> __state)
        {
            ModifyUserData(true, ref __state);
        }

        private static void ModifyUserData(bool restore, ref Dictionary<UserData, Dictionary<PropertyInfo, PropertyChangeLog>> backup)
        {
            for (int i = 0; i < 2; i++)
            {
                var userData = UserDataManager.Instance.GetUserData(i);
                if (!userData.IsEntry) continue;
                if (!backup.TryGetValue(userData, out var userBackup))
                {
                    backup[userData] = userBackup = [];
                }
                foreach (var (_, userDataProperty, dataManagerMethod) in collectionHooks)
                {
                    var currentValue = userDataProperty.GetValue(userData);
                    if (restore)
                    {
                        if (!userBackup.TryGetValue(userDataProperty, out var backupData))
                        {
                            MelonLogger.Error($"[Unlock.CollectionHook] Failed to restore {userDataProperty.Name} to the original value. Backup data not found.");
                            continue;
                        }
                        else if (currentValue != backupData.To)
                        {
                            MelonLogger.Error($"[Unlock.CollectionHook] Failed to restore {userDataProperty.Name} to the original value. Value changed unexpectedly, incompatible mods loaded?");
                            continue;
                        }
                        userDataProperty.SetValue(userData, backupData.From);
                    }
                    else
                    {
                        var allUnlockedItems = GetAllUnlockedItemList(dataManagerMethod);
                        userBackup[userDataProperty] = new(From: currentValue, To: allUnlockedItems);
                        userDataProperty.SetValue(userData, allUnlockedItems);
                    }
                }
            }
        }
    }

    [ConfigEntry(
        en: "Unlock all characters.",
        zh: "解锁所有旅行伙伴"
    )]
    private static readonly bool characters = true;

    [EnableIf(typeof(Unlock), nameof(characters))]
    public class CharacterHook
    {
        private enum StateFlag
        {
            InInitializeMethod = 1,
            InRestoreCharadataMethod = 2,
            InAddCollectionsMethod = 4,
            InExportUserAllMethod = 8,
        }

        private static StateFlag stateFlags = 0;

        // State flag manipulations -- I don't know a better way to do this in Harmony
        [HarmonyPatch(typeof(UserData), "Initialize")] [HarmonyPrefix] public static void PreInitialize() { stateFlags |= StateFlag.InInitializeMethod; }
        [HarmonyPatch(typeof(UserData), "Initialize")] [HarmonyPostfix] public static void PostInitialize() { stateFlags &= ~StateFlag.InInitializeMethod; }
        [HarmonyPatch(typeof(PlInformationProcess), "RestoreCharadata")] [HarmonyPrefix] public static void PreRestoreCharadata() { stateFlags |= StateFlag.InRestoreCharadataMethod; }
        [HarmonyPatch(typeof(PlInformationProcess), "RestoreCharadata")] [HarmonyPostfix] public static void PostRestoreCharadata() { stateFlags &= ~StateFlag.InRestoreCharadataMethod; }
        [HarmonyPatch(typeof(UserData), "AddCollections")] [HarmonyPrefix] public static void PreAddCollections() { stateFlags |= StateFlag.InAddCollectionsMethod; }
        [HarmonyPatch(typeof(UserData), "AddCollections")] [HarmonyPostfix] public static void PostAddCollections() { stateFlags &= ~StateFlag.InAddCollectionsMethod; }
        [HarmonyPatch(typeof(VOExtensions), "ExportUserAll")] [HarmonyPrefix] public static void PreExportUserAll() { stateFlags |= StateFlag.InExportUserAllMethod; }
        [HarmonyPatch(typeof(VOExtensions), "ExportUserAll")] [HarmonyPostfix] public static void PostExportUserAll() { stateFlags &= ~StateFlag.InExportUserAllMethod; }

        // Ideally, the all unlocked list should be computed every time based on the user's owned characters
        // to avoid one character id being referenced by multiple UserChara instances
        // to ensure leveling-up characters is saved correctly
        private readonly static Dictionary<UserData, (int originalCount, List<UserChara> overrideList)> allUnlockedCharaCache = [];

        [HarmonyPatch(typeof(UserData), "Initialize")]
        [HarmonyPostfix]
        public static void PostInitialize(ref UserData __instance)
        {
            allUnlockedCharaCache[__instance] =
            (
                originalCount: -1,
                overrideList: DataManager.Instance
                    .GetCharas()
                    .Select(pair => pair.Value)
                    .Select(chara => new UserChara(chara.GetID()))
                    .ToList()
            );
        }

        // Since we cache the all unlocked list for each user, we need to merge the original list with the override list
        // each time when the original list is changed, judged by the count of the original list
        // (assuming the references in two lists always consistent, except in the skip methods)

        [HarmonyPatch(typeof(UserData), "get_CharaList")]
        [HarmonyPostfix]
        public static void get_CharaList(ref UserData __instance, ref List<UserChara> __result)
        {
            if (// The original list is being cleared and initialized in these methods
                (stateFlags & StateFlag.InInitializeMethod) != 0 ||
                (stateFlags & StateFlag.InRestoreCharadataMethod) != 0 ||
                // When adding a character to the user's list, let the game add it to the original list
                (stateFlags & StateFlag.InAddCollectionsMethod) != 0)
            {
                return;
            }
            else if ((stateFlags & StateFlag.InExportUserAllMethod) != 0)
            {
                // Return a list of characters that should be saved to the server, i.e.
                // 1. Characters in the original list
                // 2. Characters NOT in the original list, but leveled-up
                Dictionary<int, UserChara> originalDict = __result.ToDictionary(chara => chara.ID);
                __result = MaybeMergeUserCharaList(__instance, __result)
                    .Where(chara => originalDict.ContainsKey(chara.ID) || chara.Level > 1)
                    .ToList();
            }
            else
            {
                __result = MaybeMergeUserCharaList(__instance, __result);
            }
        }

        private static List<UserChara> MaybeMergeUserCharaList(UserData userData, List<UserChara> originalList)
        {
            var cache = allUnlockedCharaCache[userData];
            if (cache.originalCount == originalList.Count)
            {
                // The original list is not changed, return the cached override list
                return cache.overrideList;
            }

            // The original list is changed, merge the original list with the override list
            Dictionary<int, UserChara> originalDict = originalList.ToDictionary(chara => chara.ID);
            Dictionary<int, UserChara> cachedDict = cache.overrideList.ToDictionary(chara => chara.ID);
            
            // Apply leveling-ups of the all unlocked characters to the original ones (if present)
            // (if not present, they'll be finally added to the user's list in ExportUserAll() hook)
            foreach (var (id, chara) in cachedDict)
            {
                if (originalDict.TryGetValue(id, out var originalChara) &&
                    originalChara != chara &&
                    originalChara.Level < chara.Level)
                {
                    foreach (var property in typeof(UserChara).GetProperties())
                    {
                        if (property.CanWrite)
                        {
                            property.SetValue(originalChara, property.GetValue(chara));
                        }
                    }
                }
            }

            cache.overrideList = cachedDict
                .Select(pair =>
                    originalDict.TryGetValue(pair.Key, out var originalChara)
                    ? originalChara
                    : pair.Value)
                .ToList();
            cache.originalCount = originalList.Count;
            return cache.overrideList;
        }
    }
}
