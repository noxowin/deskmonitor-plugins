using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using MonitorPlugin;

namespace PeripheralBatteryPlugin
{
    internal static class BundledBatteryBackend
    {
        [DataContract]
        private sealed class BundledFile
        {
            [DataMember(Name = "name", IsRequired = true)] public string Name { get; set; }
            [DataMember(Name = "sha256", IsRequired = true)] public string Sha256 { get; set; }
        }

        [DataContract]
        private sealed class VendorBundle
        {
            [DataMember(Name = "tag", IsRequired = true)] public string Tag { get; set; }
            [DataMember(Name = "portable", IsRequired = true)] public BundledFile Portable { get; set; }
            [DataMember(Name = "source", IsRequired = true)] public BundledFile Source { get; set; }
            [DataMember(Name = "headsetSource", IsRequired = true)] public BundledFile HeadsetSource { get; set; }
            [DataMember(Name = "engine", IsRequired = true)] public BundledFile Engine { get; set; }
            [DataMember(Name = "headset", IsRequired = true)] public BundledFile Headset { get; set; }
        }

        private static readonly VendorBundle Bundle = ReadBundle();

        private static VendorBundle ReadBundle()
        {
            using (var stream = Resource("upstream.json"))
                return Protocol.Read<VendorBundle>(stream);
        }

        public static string ResolveExecutable()
        {
            string folder = Path.Combine(Program.DataDirectory, "wireless-battery-" + Bundle.Tag.Substring(1));
            Directory.CreateDirectory(folder);
            string executable = Path.Combine(folder, Bundle.Engine.Name);
            string headset = Path.Combine(folder, Bundle.Headset.Name);
            if (!HasHash(executable, Bundle.Engine.Sha256) || !HasHash(headset, Bundle.Headset.Sha256))
            {
                using (var archive = new ZipArchive(Resource(Bundle.Portable.Name), ZipArchiveMode.Read))
                {
                    Install(archive, Bundle.Headset.Name, headset, Bundle.Headset.Sha256);
                    Install(archive, Bundle.Engine.Name, executable, Bundle.Engine.Sha256);
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
                    return string.Equals(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", ""),
                        expected, StringComparison.OrdinalIgnoreCase);
            }
            catch (FileNotFoundException) { return false; }
        }

        public static void ExportThirdParty(string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (string name in new[] { Bundle.Portable.Name, Bundle.Source.Name, Bundle.HeadsetSource.Name })
                using (var embedded = Resource(name))
                using (var file = new FileStream(Path.Combine(destination, name), FileMode.CreateNew))
                    embedded.CopyTo(file);
        }

        private static Stream Resource(string name) =>
            Assembly.GetExecutingAssembly().GetManifestResourceStream("DeskMonitor.Battery." + name)
            ?? throw new InvalidDataException("插件内缺少第三方资源 " + name);
    }
}
