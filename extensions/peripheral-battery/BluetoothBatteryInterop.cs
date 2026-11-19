using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace PeripheralBatteryPlugin
{
    internal static class BluetoothBatteryInterop
    {
        // The Windows Bluetooth stack publishes this byte property on the device or a
        // profile devnode. A profile can report its own connection state independently.
        private static readonly PropertyKey Battery = new PropertyKey("104EA319-6EE2-4701-BD47-8DDBF425BBE5", 2);
        private static readonly PropertyKey Connected = new PropertyKey("83DA6326-97A6-4088-9453-A1923F573B29", 15);
        private static readonly PropertyKey ContainerId = new PropertyKey("8C7ED206-3F8A-4827-B3AB-AE9E1FAEFC6C", 2);
        private static readonly PropertyKey Name = new PropertyKey("B725F130-47EF-101A-A5F1-02608C9EEBAC", 10);
        private static readonly string[] BluetoothEnumerators = { "BTHENUM", "BTHLE", "BTHLEDEVICE" };

        public static PeripheralBatteryReading[] Read()
        {
            var nodes = BluetoothEnumerators.SelectMany(ReadEnumerator);
            var readings = new List<PeripheralBatteryReading>();
            foreach (var container in nodes.GroupBy(node => node.Container, StringComparer.OrdinalIgnoreCase))
            {
                var deviceNodes = container.Where(node => node.IsDevice).ToArray();
                var deviceConnection = deviceNodes.Where(node => node.Connected.HasValue).ToArray();
                var connectionNodes = deviceConnection.Length > 0
                    ? deviceConnection : container.Where(node => node.Percent.HasValue);
                if (connectionNodes.All(node => node.Connected == false)) continue;

                var battery = container.Where(node => node.Percent.HasValue)
                    .OrderByDescending(node => node.Connected == true)
                    .ThenByDescending(node => node.IsDevice)
                    .ThenBy(node => node.Percent)
                    .FirstOrDefault();
                if (battery == null) continue;
                string name = deviceNodes.FirstOrDefault(node => node.Connected == true && node.Name.Length != 0)?.Name
                    ?? deviceNodes.FirstOrDefault(node => node.Name.Length != 0)?.Name
                    ?? battery.Name;
                readings.Add(new PeripheralBatteryReading(
                    name.Length == 0 ? "蓝牙设备" : name, "蓝牙", battery.Percent, null, "bt:" + container.Key));
            }
            return readings.ToArray();
        }

        private static IEnumerable<DeviceNode> ReadEnumerator(string enumerator)
        {
            const uint presentAllClasses = 0x06;
            const int noMoreItems = 259;
            IntPtr devices = SetupDiGetClassDevsW(IntPtr.Zero, enumerator, IntPtr.Zero, presentAllClasses);
            if (devices == new IntPtr(-1)) throw new Win32Exception(Marshal.GetLastWin32Error());
            try
            {
                var buffer = new byte[1024];
                var instance = new StringBuilder(256);
                var data = new DeviceInfoData { Size = (uint)Marshal.SizeOf<DeviceInfoData>() };
                for (uint index = 0; ; index++)
                {
                    if (!SetupDiEnumDeviceInfo(devices, index, ref data))
                    {
                        int error = Marshal.GetLastWin32Error();
                        if (error == noMoreItems) yield break;
                        throw new Win32Exception(error);
                    }
                    if (!SetupDiGetDeviceInstanceIdW(devices, ref data, instance, (uint)instance.Capacity, out _))
                        continue;
                    string id = instance.ToString();
                    bool isDevice = id.StartsWith(@"BTHENUM\DEV_", StringComparison.OrdinalIgnoreCase)
                        || id.StartsWith(@"BTHLE\DEV_", StringComparison.OrdinalIgnoreCase);
                    uint batterySize = GetProperty(data.DevInst, Battery, 0x03, buffer);
                    int? percent = batterySize == 1 && buffer[0] <= 100 ? (int?)buffer[0] : null;
                    if (!isDevice && !percent.HasValue) continue;

                    uint connectedSize = GetProperty(data.DevInst, Connected, 0x11, buffer);
                    bool? connected = connectedSize == 1 ? (bool?)(buffer[0] != 0) : null;
                    uint containerSize = GetProperty(data.DevInst, ContainerId, 0x0D, buffer);
                    string container = id;
                    if (containerSize == 16)
                    {
                        var bytes = new byte[16];
                        Buffer.BlockCopy(buffer, 0, bytes, 0, bytes.Length);
                        var guid = new Guid(bytes);
                        if (guid != Guid.Empty) container = guid.ToString();
                    }
                    uint nameSize = GetProperty(data.DevInst, Name, 0x12, buffer);
                    string name = Encoding.Unicode.GetString(buffer, 0, (int)nameSize).TrimEnd('\0').Trim();
                    yield return new DeviceNode
                    {
                        Container = container, Name = name, IsDevice = isDevice,
                        Percent = percent, Connected = connected,
                    };
                }
            }
            finally
            {
                SetupDiDestroyDeviceInfoList(devices);
            }
        }

        private static uint GetProperty(uint device, PropertyKey key, uint expectedType, byte[] buffer)
        {
            uint size = (uint)buffer.Length;
            return CM_Get_DevNode_PropertyW(device, ref key, out uint type, buffer, ref size, 0) == 0
                && type == expectedType ? size : 0;
        }

        private sealed class DeviceNode
        {
            public string Container;
            public string Name;
            public bool IsDevice;
            public int? Percent;
            public bool? Connected;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PropertyKey
        {
            public PropertyKey(string guid, uint id) { Format = new Guid(guid); Id = id; }
            public Guid Format;
            public uint Id;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DeviceInfoData
        {
            public uint Size;
            public Guid ClassGuid;
            public uint DevInst;
            public UIntPtr Reserved;
        }

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevsW(IntPtr classGuid, string enumerator, IntPtr parent, uint flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInfo(IntPtr devices, uint index, ref DeviceInfoData data);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern bool SetupDiGetDeviceInstanceIdW(IntPtr devices, ref DeviceInfoData data,
            StringBuilder instanceId, uint capacity, out uint requiredSize);

        [DllImport("setupapi.dll")]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr devices);

        [DllImport("cfgmgr32.dll", ExactSpelling = true)]
        private static extern uint CM_Get_DevNode_PropertyW(uint device, ref PropertyKey key, out uint type,
            [Out] byte[] value, ref uint size, uint flags);
    }
}
