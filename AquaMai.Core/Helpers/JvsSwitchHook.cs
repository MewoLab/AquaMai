using System.Collections.Generic;
using System.Reflection;
using AMDaemon;
using UnityEngine;
using HarmonyLib;

namespace AquaMai.Core.Helpers;

public static class JvsSwitchHook
{
    public delegate bool JvsSwitchKeyChecker(int playerNo, InputId inputId);

    public static List<JvsSwitchKeyChecker> KeyCheckers = [];

    public static void AddKeyChecker(JvsSwitchKeyChecker keyChecker)
    {
        KeyCheckers.Add(keyChecker);
    }

    public static void RemoveKeyChecker(JvsSwitchKeyChecker keyChecker)
    {
        KeyCheckers.Remove(keyChecker);
    }

    [HarmonyPatch]
    public static class Hook
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            var jvsSwitch = typeof(IO.Jvs).GetNestedType("JvsSwitch", BindingFlags.NonPublic | BindingFlags.Public);
            return [jvsSwitch.GetMethod("Execute")];
        }

        public static bool Prefix(
            int ____playerNo,
            InputId ____inputId,
            KeyCode ____subKey,
            SwitchInput ____switchInput,
            ref bool ____invert,
            ref bool ____isStateOnOld,
            ref bool ____isStateOnOld2,
            ref bool ____isStateOn,
            ref bool ____isTriggerOn,
            ref bool ____isTriggerOff)
        {
            ____isStateOnOld2 = ____isStateOnOld;
            ____isStateOnOld = ____isStateOn;

            bool flag = false;
            foreach (var keyChecker in KeyCheckers)
            {
                flag |= keyChecker(____playerNo, ____inputId);
            }
            flag |= DebugInput.GetKey(____subKey);
            flag |= ____invert
                ? (!____switchInput.IsOn || ____switchInput.HasOffNow)
                : (____switchInput.IsOn || ____switchInput.HasOnNow);

            if (____isStateOnOld2 && !____isStateOnOld && flag)
            {
                flag = false;
            }
            ____isStateOn = flag;
            ____isTriggerOn = flag && (flag ^ ____isStateOnOld);
            ____isTriggerOff = !flag && (flag ^ ____isStateOnOld);
            return false;
        }
    }
}
