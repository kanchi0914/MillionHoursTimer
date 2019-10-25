using MHTimer;
using System;
using System.Runtime.InteropServices;

public static class LastInputCounter
{
    public static int GetLastInputSeconds()
    {
        var plii = new WinAPI.LASTINPUTINFO();
        plii.cbSize = (uint)Marshal.SizeOf(plii);
        if (WinAPI.GetLastInputInfo(ref plii))
        {
            var lastInputTime = TimeSpan.FromMilliseconds(Environment.TickCount - plii.dwTime).TotalSeconds;
            return (int)lastInputTime;
        }

        else
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }

}