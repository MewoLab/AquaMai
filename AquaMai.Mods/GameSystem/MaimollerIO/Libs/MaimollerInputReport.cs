#nullable enable

using System;
using System.Runtime.InteropServices;

namespace AquaMai.Mods.GameSystem.MaimollerIO.Libs;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public class MaimollerInputReport
{
    public enum SwitchClass
    {
        SYSTEM,
        BUTTON,
        TOUCH_A,
        TOUCH_B,
        TOUCH_C,
        TOUCH_D,
        TOUCH_E
    }

    [Flags]
    public enum ButtonMask : byte
    {
        BTN_1 = 1,
        BTN_2 = 2,
        BTN_3 = 4,
        BTN_4 = 8,
        BTN_5 = 0x10,
        BTN_6 = 0x20,
        BTN_7 = 0x40,
        BTN_8 = 0x80,
        SELECT = 8,
        TEST = 4,
        SERVICE = 2,
        COIN = 1,
        ANY_PLAYER = byte.MaxValue,
        ANY_SYSTEM = 0xF,
        ANY_TOUCH_A = byte.MaxValue,
        ANY_TOUCH_B = byte.MaxValue,
        ANY_TOUCH_C = 3,
        ANY_TOUCH_D = byte.MaxValue,
        ANY_TOUCH_E = byte.MaxValue
    }

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
    public byte[] touches = new byte[5];

    public byte playerBtn;
    public byte systemBtn;

    private static readonly uint[] _lookup32 = CreateLookup32();

    public uint GetSwitchState(SwitchClass cat, ButtonMask mask)
    {
        return (cat switch
        {
            SwitchClass.SYSTEM => systemBtn,
            SwitchClass.BUTTON => playerBtn,
            _ => touches[(int)(cat - 2)],
        }) & (uint)mask;
    }

    public override string ToString()
    {
        byte[] bytes = [playerBtn, systemBtn];
        return $"{ToHex(touches)} {ToHex(bytes)}";
    }

    private static uint[] CreateLookup32()
    {
        var array = new uint[256];
        for (int i = 0; i < 256; i++)
        {
            string text = i.ToString("X2");
            array[i] = text[0] | ((uint)text[1] << 16);
        }
        return array;
    }

    private static string ToHex(byte[] bytes)
    {
        var array = new char[bytes.Length * 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            uint num = _lookup32[bytes[i]];
            array[2 * i] = (char)num;
            array[2 * i + 1] = (char)(num >> 16);
        }
        return new string(array);
    }
}

