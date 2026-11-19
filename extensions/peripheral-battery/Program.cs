using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MonitorPlugin;

namespace PeripheralBatteryPlugin
{
    internal sealed class PeripheralBatteryReading
    {
        public PeripheralBatteryReading(string name, string connection, int? percent, string status, string id)
        {
            Name = name;
            Connection = connection;
            Percent = percent;
            Status = status;
            Id = id;
        }
        public string Name { get; }
        public string Connection { get; }
        public int? Percent { get; }
        public string Status { get; }
        public string Id { get; }

        public Reading ToReading()
        {
            string label = Name.ToLowerInvariant();
            string icon = Regex.IsMatch(label, "headphone|headset|earbud|airpods|freebuds|galaxy buds|耳机|耳麦") ? "headphones"
                : Regex.IsMatch(label, "keyboard|keychron|键盘") ? "keyboard"
                : Regex.IsMatch(label, "mouse|鼠标") ? "mouse"
                : Regex.IsMatch(label, "controller|dualshock|dualsense|gamepad|game.?sir|xbox|手柄")
                    || Connection.StartsWith("Xbox Wireless", StringComparison.OrdinalIgnoreCase) ? "gamepad" : "battery";
            return new Reading
            {
                Id = Id,
                Label = Name,
                Icon = icon,
                Value = Percent.HasValue ? $"{Percent}%" : Status,
                Detail = Percent.HasValue && !string.IsNullOrEmpty(Status) ? $"{Connection} · {Status}" : Connection,
                ShowOnTaskbar = Percent.HasValue || Status == "低" || Status == "极低" || Status == "中"
                    || Status == "良好" || Status == "高" || Status == "满电" || Status == "已耗尽",
            };
        }
    }

    internal static class Program
    {
        internal static readonly string DataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeskMonitor", "peripheral-battery");
        private const string EmptyList = "No devices with battery info found.";
        private const int MaxOutputBytes = 64 * 1024;
        private static readonly Regex DeviceLine = new Regex(
            @"^(.+?)  \[(.*?)\]  (?:\{[^}]+\}  )?(.*?)   \((.+)\)$",
            RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        private static readonly Regex PercentPrefix = new Regex(@"^(100|[0-9]{1,2})%(?:$|[ ,])");

        private static int Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "--export-third-party")
            {
                BundledBatteryBackend.ExportThirdParty(args[1]);
                return 0;
            }
            if (args.Length != 1 || args[0] != Protocol.PollArgument) return 2;
            var devices = new List<PeripheralBatteryReading>();
            string message = null;
            try
            {
                var readings = ReadWirelessBattery(BundledBatteryBackend.ResolveExecutable());
                if (readings == null) message = "内置采集引擎暂不可用。";
                else devices.AddRange(readings);
            }
            catch (Exception)
            {
                message = "内置采集引擎暂不可用。";
            }
            try
            {
                var ids = new HashSet<string>(devices.Select(device => device.Id), StringComparer.OrdinalIgnoreCase);
                var names = new HashSet<string>(devices.Where(device => device.Connection.StartsWith("蓝牙", StringComparison.Ordinal))
                    .Select(device => device.Name), StringComparer.OrdinalIgnoreCase);
                foreach (var bluetooth in BluetoothBatteryInterop.Read())
                    if (!names.Contains(bluetooth.Name) && ids.Add(bluetooth.Id)) devices.Add(bluetooth);
            }
            catch (System.ComponentModel.Win32Exception) { }

            ReceiverBatteryHistory.Apply(devices, DataDirectory);
            if (devices.Count == 0 && message == null) message = "暂无可读取电量的外设。";
            Protocol.Write(Console.OpenStandardOutput(), new Report
            {
                Readings = devices.OrderBy(device => device.Name, StringComparer.CurrentCultureIgnoreCase)
                    .Take(64).Select(device => device.ToReading()).ToArray(),
                Message = message,
            });
            return 0;
        }

        private static PeripheralBatteryReading[] ReadWirelessBattery(string path)
        {
            using (var process = Process.Start(new ProcessStartInfo
            {
                FileName = path,
                Arguments = "--list",
                WorkingDirectory = Path.GetDirectoryName(path),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            }))
            {
                try
                {
                    var output = ReadOutputAsync(process.StandardOutput.BaseStream);
                    if (!process.WaitForExit(12000) || process.ExitCode != 0 || !output.Wait(1000)) return null;
                    return output.Result == null ? null : Parse(output.Result);
                }
                finally
                {
                    if (!process.HasExited)
                    {
                        try { process.Kill(); }
                        catch (InvalidOperationException) { }
                    }
                }
            }
        }

        private static async Task<string> ReadOutputAsync(Stream stream)
        {
            using (var memory = new MemoryStream())
            {
                var buffer = new byte[4096];
                for (int count; (count = await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) != 0;)
                {
                    if (memory.Length + count > MaxOutputBytes) return null;
                    memory.Write(buffer, 0, count);
                }
                return Encoding.UTF8.GetString(memory.ToArray());
            }
        }

        internal static PeripheralBatteryReading[] Parse(string text)
        {
            var readings = new List<PeripheralBatteryReading>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool reportedEmpty = false;
            using (var reader = new StringReader(text))
                for (string line; (line = reader.ReadLine()) != null;)
                {
                    if (line == EmptyList) reportedEmpty = true;
                    if (line.Length > 2048) continue;
                    Match fields = DeviceLine.Match(line);
                    if (!fields.Success || !seen.Add(fields.Groups[4].Value)) continue;
                    string value = fields.Groups[3].Value.Trim();
                    Match percent = PercentPrefix.Match(value);
                    int? level = percent.Success ? (int?)int.Parse(percent.Groups[1].Value) : null;
                    string status = level.HasValue ? value.Substring(percent.Length).TrimStart(' ', ',') : value;
                    readings.Add(new PeripheralBatteryReading(fields.Groups[1].Value.Trim(),
                        fields.Groups[2].Value.Replace("Bluetooth", "蓝牙"), level, TranslateStatus(status), fields.Groups[4].Value));
                }
            return readings.Count > 0 || reportedEmpty ? readings.ToArray() : null;
        }

        private static string TranslateStatus(string status)
        {
            var keyword = Regex.Match(status, @"^(off / not connected|asleep|unknown|Empty|Critical|Low|Medium|Good|High|Full)(?=$|[ ,])");
            string translated = keyword.Value switch
            {
                "off / not connected" => "未连接", "asleep" => "休眠", "unknown" => "未知",
                "Empty" => "已耗尽", "Critical" => "极低", "Low" => "低", "Medium" => "中",
                "Good" => "良好", "High" => "高", "Full" => "满电", _ => keyword.Value,
            };
            return (translated + status.Substring(keyword.Length)).Replace("charging", "充电中");
        }
    }
}
