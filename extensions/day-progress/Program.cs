using System;
using MonitorPlugin;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length != 1 || args[0] != Protocol.PollArgument) return 2;
        DateTime now = DateTime.Now;
        TimeSpan remaining = now.Date.AddDays(1) - now;
        int minutes = (int)Math.Ceiling(remaining.TotalMinutes);
        Protocol.Write(Console.OpenStandardOutput(), new Report
        {
            Readings = new[]
            {
                new Reading
                {
                    Label = "今日进度",
                    Value = $"{(int)(now.TimeOfDay.TotalMinutes / 14.4)}%",
                    Detail = $"距离午夜还有 {minutes / 60} 小时 {minutes % 60} 分钟",
                    Icon = "time",
                },
            },
        });
        return 0;
    }
}
