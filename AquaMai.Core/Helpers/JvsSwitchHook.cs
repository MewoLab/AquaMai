using System.Collections.Generic;
using System.Reflection;
using AMDaemon;
using UnityEngine;
using HarmonyLib;

namespace AquaMai.Core.Helpers;

public struct AuxiliaryState
{
    public bool select1P;
    public bool select2P;
    public bool service;
    public bool test;
}

public static class JvsSwitchHook
{
    public delegate bool ButtonChecker(int playerNo, int buttonIndex1To8);
    public delegate AuxiliaryState AuxiliaryStateProvider();

    private static readonly List<ButtonChecker> _buttonCheckers = [];
    private static readonly List<AuxiliaryStateProvider> _auxiliaryStateProviders = [];

    public static void RegisterButtonChecker(ButtonChecker buttonChecker) => _buttonCheckers.Add(buttonChecker);
    public static void RegisterAuxiliaryStateProvider(AuxiliaryStateProvider auxiliaryStateProvider) => _auxiliaryStateProviders.Add(auxiliaryStateProvider);

    private static bool IsInputPushed(int playerNo, InputId inputId)
    {
        if (inputId.Value.StartsWith("button_"))
        {
            int buttonIndex = int.Parse(inputId.Value.Substring("button_0".Length));
            foreach (var checker in _buttonCheckers) if (checker(playerNo, buttonIndex)) return true;
            return false;
        }
        else
        {
            bool wantSelect1P = inputId.Value == "select" && playerNo == 0;
            bool wantSelect2P = inputId.Value == "select" && playerNo == 1;
            bool wantService = inputId.Value == "service";
            bool wantTest = inputId.Value == "test";
            foreach (var provider in _auxiliaryStateProviders)
            {
                var auxiliaryState = provider();
                if (wantSelect1P && auxiliaryState.select1P ||
                    wantSelect2P && auxiliaryState.select2P ||
                    wantService && auxiliaryState.service ||
                    wantTest && auxiliaryState.test) return true;
            }
            return false;
        }
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
            flag |= IsInputPushed(____playerNo, ____inputId);
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
