using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using MonitorPlugin;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length != 1 || args[0] != Protocol.PollArgument) return 2;
        if (!GetSystemPowerStatus(out PowerStatus power)) return 1;
        var report = new Report { Readings = Array.Empty<Reading>() };
        if (power.BatteryFlags != 255 && (power.BatteryFlags & 128) != 0)
            report.Message = "Windows 未检测到电脑电池。";
        else if (power.BatteryPercent <= 100)
        {
            string state = (power.BatteryFlags & 8) != 0 && power.BatteryFlags != 255 ? "充电中"
                : power.AcLineStatus == 1 ? "已接通电源" : power.AcLineStatus == 0 ? "电池供电" : "供电状态未知";
            var rows = new List<Reading>
            {
                new Reading { Label = "电脑电池", Value = $"{power.BatteryPercent}%", Detail = state, Icon = "battery" },
            };
            if (power.AcLineStatus == 0 && power.BatterySeconds != uint.MaxValue)
                rows.Add(new Reading { Label = "估计剩余续航", Value = FormatDuration(power.BatterySeconds),
                    Icon = "time", ShowOnTaskbar = false });
            report.Readings = rows.ToArray();
        }
        else report.Message = "Windows 暂未提供电脑电池电量。";
        Protocol.Write(Console.OpenStandardOutput(), report);
        return 0;
    }

    private static string FormatDuration(uint seconds)
    {
        var time = TimeSpan.FromSeconds(seconds);
        return $"{(int)time.TotalHours} 小时 {time.Minutes} 分钟";
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PowerStatus
    {
        public byte AcLineStatus, BatteryFlags, BatteryPercent, SystemStatus;
        public uint BatterySeconds, FullBatterySeconds;
    }

    [DllImport("kernel32.dll")]
    private static extern bool GetSystemPowerStatus(out PowerStatus status);
}
