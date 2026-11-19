using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using MonitorPlugin;

namespace PeripheralBatteryPlugin
{
    internal static class ReceiverBatteryHistory
    {
        // Wireless Battery's resident app allows Kysona devices to go silent for 30 min.
        // Its one-shot --list command provides no history, so the plugin keeps it between polls.
        private static readonly TimeSpan GracePeriod = TimeSpan.FromMinutes(30);
        private const string Receiver = "2.4 GHz receiver";
        private const int MaxEntries = 64;
        private static readonly Regex ReceiverId = new Regex(
            @"^kysona:([0-9a-f]{4}):([0-9a-f]{4})$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static void Apply(List<PeripheralBatteryReading> readings, string directory)
        {
            string path = Path.Combine(directory, "kysona-battery-history.json");
            if (!File.Exists(path) && !readings.Any(reading => reading.Connection == Receiver
                && reading.Percent.HasValue && ReceiverId.IsMatch(reading.Id))) return;
            try
            {
                Directory.CreateDirectory(directory);
                // A standalone diagnostic poll can overlap the host's poll. Serialize the
                // short read/merge/write rather than allowing competing snapshots to replace each other.
                using (File.Open(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                {
                    var previous = Read(path);
                    var retained = Merge(readings, previous, DateTime.UtcNow, IsUsbReceiverPresent);
                    if (previous.Length != 0 || retained.Length != 0) Write(path, retained);
                }
            }
            catch (IOException) { }                 // History is optional. Return current readings on contention or I/O failure.
            catch (UnauthorizedAccessException) { }
        }

        internal static Entry[] Merge(List<PeripheralBatteryReading> readings, Entry[] previous, DateTime now,
            Func<string, bool> isUsbReceiverPresent)
        {
            var indices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var retained = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < readings.Count; i++)
            {
                var reading = readings[i];
                if (reading.Connection != Receiver || !ReceiverId.IsMatch(reading.Id)) continue;
                indices[reading.Id] = i;
                if (reading.Percent.HasValue)
                    retained[reading.Id] = new Entry { Id = reading.Id, Name = reading.Name, Percent = reading.Percent.Value, MeasuredUtc = now };
            }

            foreach (var entry in previous.Take(MaxEntries))
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Name) || string.IsNullOrEmpty(entry.Id)
                    || entry.Percent < 0 || entry.Percent > 100 || entry.MeasuredUtc > now
                    || now - entry.MeasuredUtc >= GracePeriod || retained.ContainsKey(entry.Id)) continue;
                var match = ReceiverId.Match(entry.Id);
                if (!match.Success) continue;

                bool listed = indices.TryGetValue(entry.Id, out int index);
                if (listed && readings[index].Status?.StartsWith("未连接", StringComparison.Ordinal) == true) continue;
                if (readings.Any(reading => reading.Connection != Receiver && reading.Percent.HasValue
                    && string.Equals(reading.Name, entry.Name, StringComparison.OrdinalIgnoreCase))) continue;
                if (!isUsbReceiverPresent($@"USB\VID_{match.Groups[1].Value}&PID_{match.Groups[2].Value}")) continue;

                var stale = new PeripheralBatteryReading(entry.Name, Receiver, entry.Percent,
                    $"上次读数 · {entry.MeasuredUtc.ToLocalTime():HH:mm}", entry.Id);
                if (listed) readings[index] = stale;
                else readings.Add(stale);
                retained[entry.Id] = entry;
            }
            return retained.Values.Take(MaxEntries).ToArray();
        }

        private static Entry[] Read(string path)
        {
            if (!File.Exists(path)) return Array.Empty<Entry>();
            using (var stream = File.OpenRead(path))
            {
                if (stream.Length <= 64 * 1024)
                {
                    try { return Protocol.Read<Entry[]>(stream) ?? Array.Empty<Entry>(); }
                    catch (SerializationException) { } // A corrupt history can be rebuilt from the next report.
                }
                return Array.Empty<Entry>();
            }
        }

        private static void Write(string path, Entry[] entries)
        {
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew)) Protocol.Write(stream, entries);
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally { File.Delete(temporary); }
        }

        internal static bool IsUsbReceiverPresent(string deviceId)
        {
            const uint presentEnumerator = 0x100 | 0x01;
            const uint bufferSmall = 0x1A;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                if (CM_Get_Device_ID_List_SizeW(out uint length, deviceId, presentEnumerator) != 0 || length <= 1)
                    return false;
                var instances = new char[length];
                uint result = CM_Get_Device_ID_ListW(deviceId, instances, length, presentEnumerator);
                if (result != bufferSmall) return result == 0 && instances[0] != '\0';
            }
            return false;
        }

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern uint CM_Get_Device_ID_List_SizeW(out uint length, string filter, uint flags);

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern uint CM_Get_Device_ID_ListW(string filter, [Out] char[] buffer, uint length, uint flags);

        [DataContract]
        internal sealed class Entry
        {
            [DataMember(Name = "id")] public string Id { get; set; }
            [DataMember(Name = "name")] public string Name { get; set; }
            [DataMember(Name = "percent")] public int Percent { get; set; }
            [DataMember(Name = "measuredUtc")] public DateTime MeasuredUtc { get; set; }
        }
    }
}
