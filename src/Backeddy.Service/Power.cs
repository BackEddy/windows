using System.Runtime.InteropServices;

namespace Backeddy.Service;

/// <summary>Tomada ou bateria (GetSystemPowerStatus).</summary>
static class Power
{
    [StructLayout(LayoutKind.Sequential)]
    struct SystemPowerStatus
    {
        public byte ACLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag;
        public int BatteryLifeTime, BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll")]
    static extern bool GetSystemPowerStatus(out SystemPowerStatus s);

    /// <summary>(na bateria?, porcentagem ou null se não houver bateria)</summary>
    public static (bool OnBattery, int? Percent) Read()
    {
        if (!GetSystemPowerStatus(out var s)) return (false, null);
        bool noBattery = (s.BatteryFlag & 128) != 0 || s.BatteryFlag == 255;
        int? pct = noBattery || s.BatteryLifePercent == 255 ? null : s.BatteryLifePercent;
        return (s.ACLineStatus == 0, pct);
    }
}
