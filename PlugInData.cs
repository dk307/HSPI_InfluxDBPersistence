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
        public const string PlugInName = @"Influx DB Persistence";

        /// <summary>
        /// The plugin Id
        /// </summary>
        public const string SettingFileName = @"HSPI_InfluxDBPersistence.exe.ini";

        /// <summary>
        /// Device lugIn Data name keys
        /// </summary>
#pragma warning disable CA1308 // Normalize strings to uppercase
        public static readonly string DevicePlugInDataNamedKey = PlugInId.ToLowerInvariant() + ".plugindata";
        public static readonly string DevicePlugInDataIgnoreKey = PlugInId.ToLowerInvariant() + ".ignore";
#pragma warning restore CA1308 // Normalize strings to uppercase
    }
}