using System;
using System.Globalization;
using AquaMai.Config.Attributes;
using HarmonyLib;
using JetBrains.Annotations;
using Manager;
using MelonLoader;

namespace AquaMai.Mods.Fix;

// Fixes various locale issues that pop up if the game runs in a different locale than ja-JP.
[ConfigSection(exampleHidden: true, defaultOn: true)]
public class FixLocaleIssues
{
    private static readonly CultureInfo JapanCultureInfo = CultureInfo.GetCultureInfo("ja-JP");
    [CanBeNull] private static TimeZoneInfo _tokyoStandardTime;

    public static void OnBeforePatch()
    {
        CultureInfo.DefaultThreadCurrentCulture = JapanCultureInfo;
        CultureInfo.DefaultThreadCurrentUICulture = JapanCultureInfo;

        try
        {
            _tokyoStandardTime = TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time");
        }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            _tokyoStandardTime = TimeZoneInfo.CreateCustomTimeZone(
                "Tokyo Standard Time",
                TimeManager.JpTime,
                "(UTC+09:00) Osaka, Sapporo, Tokyo",
                "Tokyo Standard Time",
                "Tokyo Daylight Time",
                null,
                true);
        }
        catch (Exception e)
        {
            MelonLogger.Warning($"Could not get JST timezone, DateTime.Now patch will not work: {e.StackTrace}");
        }
    }
    
    // Forces local timezone to be UTC+9, since segatools didn't patch it properly until recent versions,
    // which doesn't actually work well with maimai.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(DateTime), "get_Now")]
    private static bool DateTime_get_Now(ref DateTime __result)
    {
        if (_tokyoStandardTime == null)
        {
            return true;
        }

        __result = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _tokyoStandardTime!);
        return false;
    }
}
