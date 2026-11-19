using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;

namespace PeripheralBatteryPlugin
{
    internal static class BundledBatteryBackend
    {
        private const string Portable = "WirelessBattery-1.0.8-portable.zip";
        private const string WirelessBattery = "WirelessBattery.exe";
        private const string HeadsetControl = "headsetcontrol.exe";

        public static string ResolveExecutable()
        {
            string folder = Path.Combine(Program.DataDirectory, "wireless-battery-1.0.8");
            const string batteryHash = "AD3BCA7AF7B56262E3A30E6F83AB1F9FC937B286926323FA6FFAE1A5D561DDBA";
            const string headsetHash = "E371DA043710DDB8FC70AF77F2CCAD1CD03F8C6F49BA423E08764AD7A94A3C40";
            Directory.CreateDirectory(folder);
            string executable = Path.Combine(folder, WirelessBattery);
            string headset = Path.Combine(folder, HeadsetControl);
            if (!HasHash(executable, batteryHash) || !HasHash(headset, headsetHash))
            {
                using (var archive = new ZipArchive(Resource(Portable), ZipArchiveMode.Read))
                {
                    Install(archive, HeadsetControl, headset, headsetHash);
                    Install(archive, WirelessBattery, executable, batteryHash);
                }
            }
            return executable;
        }

        private static void Install(ZipArchive archive, string entry, string destination, string hash)
        {
            if (HasHash(destination, hash)) return;
            string staged = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var input = archive.GetEntry(entry).Open())
                using (var output = new FileStream(staged, FileMode.CreateNew))
                    input.CopyTo(output);
                if (!HasHash(staged, hash)) throw new InvalidDataException("内置电量引擎校验失败");
                try
                {
                    if (File.Exists(destination)) File.Replace(staged, destination, null);
                    else File.Move(staged, destination);
                }
                catch (IOException) when (HasHash(destination, hash))
                {
                    // A simultaneous poll has already installed the identical file.
                }
            }
            finally { File.Delete(staged); }
        }

        private static bool HasHash(string file, string expected)
        {
            try
            {
                using (var stream = File.OpenRead(file))
                using (var sha = SHA256.Create())
                    return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "") == expected;
            }
            catch (FileNotFoundException) { return false; }
        }

        public static void ExportThirdParty(string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (string name in new[] { Portable, "WirelessBattery-1.0.8-source.zip", "headsetcontrol-4.3.0.tar.gz" })
                using (var embedded = Resource(name))
                using (var file = new FileStream(Path.Combine(destination, name), FileMode.CreateNew))
                    embedded.CopyTo(file);
        }

        private static Stream Resource(string name) =>
            Assembly.GetExecutingAssembly().GetManifestResourceStream("DeskMonitor.Battery." + name)
            ?? throw new InvalidDataException("插件内缺少第三方资源 " + name);
    }
}
