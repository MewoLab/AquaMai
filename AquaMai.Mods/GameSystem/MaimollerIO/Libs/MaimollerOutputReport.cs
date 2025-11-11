#nullable enable

using System;
using System.Runtime.InteropServices;

namespace AquaMai.Mods.GameSystem.MaimollerIO.Libs;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public class MaimollerOutputReport
{
    [Flags]
    public enum IndicatorMask : byte
    {
        COIN_BLOCKER = 1,
        QR_SCANNER = 2,
        CAMERA_RING = 4,
        CAMERA_REC = 8
    }

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 24)]
    public byte[] buttonColors = new byte[24];

    public byte circleBrightness;
    public byte bodyBrightness;
    public byte sideBrightness;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public byte[] billboardColor = new byte[3];

    public IndicatorMask indicators;
}

