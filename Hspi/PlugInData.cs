#nullable enable

namespace Hspi
{
    /// <summary>
    /// Class to store static data
    /// </summary>
    internal static class PlugInData
    {
        /// <summary>
        /// The plugin Id
        /// </summary>
        public const string PlugInId = @"InfluxDBPersistence";

        /// <summary>
        /// The plugin name
        /// </summary>
        public const string PlugInName = @"Influx DB History";

        /// <summary>
        /// The plugin name
        /// </summary>
        public const string Hs3PlugInName = @"Influx DB Persistence";

        /// <summary>
        /// The plugin Id
        /// </summary>
        public const string SettingFileName = @"HSPI_InfluxDBPersistence.exe.ini";

#pragma warning disable CA1308 // Normalize strings to uppercase
        /// <summary>
        /// Device lugIn Data name keys
        /// </summary>
        public static readonly string DevicePlugInDataNamedKey = PlugInId.ToLowerInvariant() + ".plugindata";

        public static readonly string DevicePlugInDataTypeKey = PlugInId.ToLowerInvariant() + ".plugindatatype";
#pragma warning restore CA1308 // Normalize strings to uppercase
    }
}