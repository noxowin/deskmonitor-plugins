using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace MonitorPlugin
{
    [DataContract]
    public sealed class Manifest
    {
        [DataMember(Name = "apiVersion", IsRequired = true)] public int ApiVersion { get; set; }
        [DataMember(Name = "id", IsRequired = true)] public string Id { get; set; }
        [DataMember(Name = "name", IsRequired = true)] public string Name { get; set; }
        [DataMember(Name = "version", IsRequired = true)] public string Version { get; set; }
        [DataMember(Name = "description")] public string Description { get; set; }
        [DataMember(Name = "intervalSeconds", IsRequired = true)] public int IntervalSeconds { get; set; }
        [DataMember(Name = "inputFileName")] public string InputFileName { get; set; }
        [DataMember(Name = "inputFileLabel")] public string InputFileLabel { get; set; }
    }

    [DataContract]
    public sealed class Reading
    {
        [DataMember(Name = "id", EmitDefaultValue = false)] public string Id { get; set; }
        [DataMember(Name = "label", IsRequired = true)] public string Label { get; set; }
        [DataMember(Name = "value", IsRequired = true)] public string Value { get; set; }
        [DataMember(Name = "detail")] public string Detail { get; set; }
        [DataMember(Name = "icon")] public string Icon { get; set; }
        [DataMember(Name = "showOnTaskbar", EmitDefaultValue = false)] public bool? ShowOnTaskbar { get; set; }
    }

    [DataContract]
    public sealed class Report
    {
        [DataMember(Name = "readings", IsRequired = true)] public Reading[] Readings { get; set; }
        [DataMember(Name = "message", EmitDefaultValue = false)] public string Message { get; set; }
    }

    public static class Protocol
    {
        public const int Version = 1;
        public const string PollArgument = "--poll";
        public const string InputFileVariable = "MONITOR_PLUGIN_INPUT_FILE";

        public static T Read<T>(Stream stream) =>
            (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);

        public static void Write<T>(Stream stream, T value) =>
            new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value);
    }
}
