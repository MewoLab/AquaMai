using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using AquaMai.Config.Attributes;
using HarmonyLib;
using Manager;
using MelonLoader;
using Monitor;

namespace AquaMai.Mods.Fancy.GamePlay;

[ConfigSection(
    en: "Enable the game to correctly judge Touch Hold outside the C zone.",
    zh: "使游戏可以正常判定非 C 区的 Touch Hold"
)]
public class JudgeTouchHoldInNormalArea
{
    private static TouchHoldC _currentInstance;

    [HarmonyPatch(typeof(TouchHoldC), "NoteCheck")]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> NoteCheckTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);

        try
        {
            var setCurrentInstanceMethod =
                AccessTools.Method(typeof(JudgeTouchHoldInNormalArea), nameof(SetCurrentInstance));

            codes.Insert(0, new CodeInstruction(OpCodes.Ldarg_0));
            codes.Insert(1, new CodeInstruction(OpCodes.Call, setCurrentInstanceMethod));

            // 获取方法引用
            var cDownMethod =
                AccessTools.Method(typeof(InputManager), "InGameTouchPanelArea_C_Down", [typeof(int)]);
            var cPushMethod =
                AccessTools.Method(typeof(InputManager), "InGameTouchPanelArea_C_Push", [typeof(int)]);

            var customDownMethod = AccessTools.Method(typeof(JudgeTouchHoldInNormalArea), nameof(CustomTouchDown));
            var customPushMethod = AccessTools.Method(typeof(JudgeTouchHoldInNormalArea), nameof(CustomTouchPush));

            if (cDownMethod == null || cPushMethod == null || customDownMethod == null || customPushMethod == null)
            {
                MelonLogger.Error("Failed to find required methods");
                return codes;
            }

            // 替换方法调用
            int replacedCount = 0;
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Call && codes[i].operand is MethodInfo method)
                {
                    if (method == cDownMethod)
                    {
                        codes[i] = new CodeInstruction(OpCodes.Call, customDownMethod);
                        replacedCount++;
                        MelonLogger.Msg($"Replaced C_Down method call at index {i}");
                    }
                    else if (method == cPushMethod)
                    {
                        codes[i] = new CodeInstruction(OpCodes.Call, customPushMethod);
                        replacedCount++;
                        MelonLogger.Msg($"Replaced C_Push method call at index {i}");
                    }
                }
            }

            MelonLogger.Msg($"Replaced {replacedCount} method calls");
            return codes;
        }
        catch (Exception ex)
        {
            MelonLogger.Error($"Error in TranspilerNoteCheck: {ex}");
            return codes;
        }
    }

    public static void SetCurrentInstance(TouchHoldC instance)
    {
        _currentInstance = instance;
    }

    private static int GetTouchAreaValue(TouchHoldC instance)
    {
        try
        {
            // 尝试多种可能的字段名
            var possibleFieldNames = new[] { "TouchArea", "touchArea", "_touchArea", "m_touchArea" };

            foreach (var fieldName in possibleFieldNames)
            {
                var field = AccessTools.Field(typeof(TouchHoldC), fieldName);
                if (field != null)
                {
                    var value = field.GetValue(instance);
                    if (value != null)
                    {
                        // MelonLogger.Msg($"Found TouchArea field: {fieldName}, value: {value}, type: {value.GetType()}");
                        return (int)value;
                    }
                }
            }

            // 如果找不到字段，尝试属性
            var possiblePropertyNames = new[] { "TouchArea", "touchArea" };

            foreach (var propertyName in possiblePropertyNames)
            {
                var property = AccessTools.Property(typeof(TouchHoldC), propertyName);
                if (property != null)
                {
                    var value = property.GetValue(instance);
                    if (value != null)
                    {
                        // MelonLogger.Msg($"Found TouchArea property: {propertyName}, value: {value}, type: {value.GetType()}");
                        return (int)value;
                    }
                }
            }

            // 如果还是找不到，尝试反射获取所有字段
            var allFields =
                typeof(TouchHoldC).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            // MelonLogger.Msg($"All fields in TouchHoldC:");
            foreach (var field in allFields)
            {
                var value = field.GetValue(instance);
                // MelonLogger.Msg($"  {field.Name}: {value} (type: {field.FieldType})");

                // 查找可能是TouchArea的字段
                if (field.Name.ToLower().Contains("touch") || field.Name.ToLower().Contains("area") ||
                    field.FieldType.Name.Contains("TouchSensor") || field.FieldType.Name.Contains("Touch"))
                {
                    if (value != null && value.GetType().IsEnum)
                    {
                        // MelonLogger.Msg($"  Potential TouchArea field found: {field.Name} = {value}");
                        return (int)value;
                    }
                }
            }

            // 尝试获取所有属性
            var allProperties =
                typeof(TouchHoldC).GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            // MelonLogger.Msg($"All properties in TouchHoldC:");
            foreach (var property in allProperties)
            {
                try
                {
                    var value = property.GetValue(instance);
                    // MelonLogger.Msg($"  {property.Name}: {value} (type: {property.PropertyType})");

                    // 查找可能是TouchArea的属性
                    if (property.Name.ToLower().Contains("touch") || property.Name.ToLower().Contains("area") ||
                        property.PropertyType.Name.Contains("TouchSensor") ||
                        property.PropertyType.Name.Contains("Touch"))
                    {
                        if (value != null && value.GetType().IsEnum)
                        {
                            // MelonLogger.Msg($"  Potential TouchArea property found: {property.Name} = {value}");
                            return (int)value;
                        }
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Msg($"  {property.Name}: Error reading - {ex.Message}");
                }
            }

            MelonLogger.Error("Could not find TouchArea field or property");
            return 1; // 默认返回C区域
        }
        catch (Exception ex)
        {
            MelonLogger.Error($"Error in GetTouchAreaValue: {ex}");
            return 1; // 默认返回C区域
        }
    }

    public static bool CustomTouchDown(int monitorId)
    {
        try
        {
            if (_currentInstance == null)
            {
                MelonLogger.Warning("currentInstance is null, fallback to C_Down");
                return InputManager.InGameTouchPanelArea_C_Down(monitorId);
            }

            var touchAreaValue = GetTouchAreaValue(_currentInstance);
            var buttonIdProp = AccessTools.PropertyGetter(typeof(TouchHoldC), "ButtonId");
            var buttonId = (InputManager.ButtonSetting)buttonIdProp.Invoke(_currentInstance, null);

            // MelonLogger.Msg($"TouchArea: {touchAreaValue}, ButtonId: {buttonId}, MonitorId: {monitorId}");

            // 检查是否在AutoPlay模式
            if (GameManager.IsAutoPlay())
            {
                // MelonLogger.Msg("Auto play mode, returning false");
                return false;
            }

            bool touchDown;

            switch (touchAreaValue)
            {
                case 0: // TouchSensorType.B
                    touchDown = InputManager.InGameTouchPanelArea_B_Down(monitorId, buttonId);
                    // MelonLogger.Msg($"TouchSensorType.B - TouchDown: {touchDown}");
                    break;
                case 1: // TouchSensorType.C
                    touchDown = InputManager.InGameTouchPanelArea_C_Down(monitorId);
                    // MelonLogger.Msg($"TouchSensorType.C - TouchDown: {touchDown}");
                    break;
                case 2: // TouchSensorType.E
                    touchDown = InputManager.InGameTouchPanelArea_E_Down(monitorId, buttonId);
                    // MelonLogger.Msg($"TouchSensorType.E - TouchDown: {touchDown}");
                    break;
                case 3: // TouchSensorType.A
                    touchDown = InputManager.InGameTouchPanelAreaDown(monitorId, buttonId);
                    // MelonLogger.Msg($"TouchSensorType.A - TouchDown: {touchDown}");
                    break;
                case 4: // TouchSensorType.D
                    touchDown = InputManager.InGameTouchPanelArea_D_Down(monitorId, buttonId);
                    // MelonLogger.Msg($"TouchSensorType.D - TouchDown: {touchDown}");
                    break;
                default:
                    touchDown = InputManager.InGameTouchPanelArea_C_Down(monitorId);
                    // MelonLogger.Msg($"Default TouchSensorType - TouchDown: {touchDown}");
                    break;
            }

            if (!touchDown)
            {
                // MelonLogger.Msg("Touch not down, returning false");
                return false;
            }

            // 根据TouchArea类型计算正确的TouchPanelArea枚举值
            InputManager.TouchPanelArea targetTouchPanelArea = GetTouchPanelAreaFromButtonId(buttonId, touchAreaValue);
            bool isUsed = InputManager.IsUsedThisFrame(monitorId, targetTouchPanelArea);
            // MelonLogger.Msg($"IsUsedThisFrame check for {targetTouchPanelArea}: {isUsed}");

            return !isUsed;
        }
        catch (Exception ex)
        {
            MelonLogger.Error($"Error in CustomTouchDown: {ex}");
            return InputManager.InGameTouchPanelArea_C_Down(monitorId);
        }
    }

    // 根据ButtonId和TouchArea计算正确的TouchPanelArea枚举值
    private static InputManager.TouchPanelArea GetTouchPanelAreaFromButtonId(InputManager.ButtonSetting buttonId,
        int touchAreaValue)
    {
        switch (touchAreaValue)
        {
            case 0: // TouchSensorType.B  
                return InputManager.TouchPanelArea.B1 + (int)buttonId;
            case 1: // TouchSensorType.C
                return InputManager.TouchPanelArea.C1 + (int)buttonId;
            case 2: // TouchSensorType.E
                return InputManager.TouchPanelArea.E1 + (int)buttonId;
            case 3: // TouchSensorType.A
                return InputManager.TouchPanelArea.A1 + (int)buttonId;
            case 4: // TouchSensorType.D
                return InputManager.TouchPanelArea.D1 + (int)buttonId;
            default:
                return InputManager.TouchPanelArea.C1 + (int)buttonId;
        }
    }

    public static bool CustomTouchPush(int monitorId)
    {
        try
        {
            if (_currentInstance == null)
            {
                MelonLogger.Warning("currentInstance is null, fallback to C_Push");
                return InputManager.InGameTouchPanelArea_C_Push(monitorId);
            }

            var touchAreaValue = GetTouchAreaValue(_currentInstance);
            var buttonIdProp = AccessTools.PropertyGetter(typeof(TouchHoldC), "ButtonId");
            var buttonId = (InputManager.ButtonSetting)buttonIdProp.Invoke(_currentInstance, null);

            // MelonLogger.Msg($"Push - TouchArea: {touchAreaValue}, ButtonId: {buttonId}, MonitorId: {monitorId}");

            bool touchPush;

            switch (touchAreaValue)
            {
                case 0: // TouchSensorType.B
                    touchPush = InputManager.InGameTouchPanelArea_B_Push(monitorId, buttonId);
                    // MelonLogger.Msg($"TouchSensorType.B - TouchPush: {touchPush}");
                    break;
                case 1: // TouchSensorType.C
                    touchPush = InputManager.InGameTouchPanelArea_C_Push(monitorId);
                    // MelonLogger.Msg($"TouchSensorType.C - TouchPush: {touchPush}");
                    break;
                case 2: // TouchSensorType.E
                    touchPush = InputManager.InGameTouchPanelArea_E_Push(monitorId, buttonId);
                    // MelonLogger.Msg($"TouchSensorType.E - TouchPush: {touchPush}");
                    break;
                case 3: // TouchSensorType.A
                    touchPush = InputManager.InGameTouchPanelAreaPush(monitorId, buttonId);
                    // MelonLogger.Msg($"TouchSensorType.A - TouchPush: {touchPush}");
                    break;
                case 4: // TouchSensorType.D
                    touchPush = InputManager.InGameTouchPanelArea_D_Push(monitorId, buttonId);
                    // MelonLogger.Msg($"TouchSensorType.D - TouchPush: {touchPush}");
                    break;
                default:
                    touchPush = InputManager.InGameTouchPanelArea_C_Push(monitorId);
                    // MelonLogger.Msg($"Default TouchSensorType - TouchPush: {touchPush}");
                    break;
            }

            // MelonLogger.Msg($"Final TouchPush result: {touchPush}");
            return touchPush;
        }
        catch (Exception ex)
        {
            MelonLogger.Error($"Error in CustomTouchPush: {ex}");
            return InputManager.InGameTouchPanelArea_C_Push(monitorId);
        }
    }
}