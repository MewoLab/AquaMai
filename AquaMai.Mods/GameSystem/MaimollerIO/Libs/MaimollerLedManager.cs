#nullable enable

using System.Linq;
using Manager;
using UnityEngine;

namespace AquaMai.Mods.GameSystem.MaimollerIO.Libs;

public class MaimollerLedManager(MaimollerOutputReport outputReport)
{
    private class ButtonLED
    {
        public Color32 color;
        public Color32 c0;
        public Color32 c1;
        public long begin;
        public long duration;
    }

    private readonly ButtonLED[] _btns = [.. Enumerable.Repeat(0, 8).Select(_ => new ButtonLED())];

    public void PreExecute()
    {
        long gameMSec = GameManager.GetGameMSec();
        for (int i = 0; i < 8; i++)
        {
            ButtonLED aDXButtonLED = _btns[i];
            if (aDXButtonLED.begin + aDXButtonLED.duration < gameMSec)
            {
                if (aDXButtonLED.begin != 0L)
                {
                    SetButtonColor(i, aDXButtonLED.color);
                }
            }
            else
            {
                float t = 1f * (gameMSec - aDXButtonLED.begin) / aDXButtonLED.duration;
                aDXButtonLED.color = Color32.Lerp(aDXButtonLED.c0, aDXButtonLED.c1, t);
                SetButtonColor(i, aDXButtonLED.color.r, aDXButtonLED.color.g, aDXButtonLED.color.b);
            }
        }
    }

    public void SetButtonColor(int index, byte r, byte g, byte b)
    {
        int i = index * 3;
        outputReport.buttonColors[i++] = r;
        outputReport.buttonColors[i++] = g;
        outputReport.buttonColors[i++] = b;
    }

    public void SetButtonColor(int index, Color32 color)
    {
        int n = index >= 0 ? 1 : 8;
        if (index < 0) index = 0;
        while (n-- > 0)
        {
            _btns[index].color = color;
            _btns[index].duration = 0L;
            _btns[index].begin = 0L;
            SetButtonColor(index, color.r, color.g, color.b);
            index++;
        }
    }

    public void SetButtonColorFade(int index, Color32 color, long duration)
    {
        if (duration == 0L)
        {
            SetButtonColor(index, color);
            return;
        }
        long gameMSec = GameManager.GetGameMSec();
        int n = index >= 0 ? 1 : 8;
        if (index < 0) index = 0;
        while (n-- > 0)
        {
            ButtonLED obj = _btns[index];
            obj.c0 = obj.color;
            obj.c1 = color;
            obj.begin = gameMSec;
            obj.duration = duration;
            index++;
        }
    }

    public void SetBodyIntensity(int index, byte intensity)
    {
        if (index == 8 || index < 0) outputReport.bodyBrightness = intensity;
        if (index == 9 || index < 0) outputReport.circleBrightness = intensity;
        if (index == 10 || index < 0) outputReport.sideBrightness = intensity;
    }

    public void SetBillboardColor(Color32 color)
    {
        outputReport.billboardColor[0] = color.r;
        outputReport.billboardColor[1] = color.g;
        outputReport.billboardColor[2] = color.b;
    }
}
