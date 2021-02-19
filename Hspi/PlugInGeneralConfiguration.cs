using Hspi.Utils;
using System;
using System.Collections.Generic;

#nullable enable

namespace Hspi
{
    internal partial class PlugIn : HspiBase
    {
        private const string DebugLoggingConfiguration = "debuglogging";
        private const string LogToFileConfiguration = "logtofile";

        public IDictionary<string, object> GetGeneralInformation()
        {
            var configuration = new Dictionary<string, object>
            {
                [DebugLoggingConfiguration] = pluginConfig!.DebugLogging,
                [LogToFileConfiguration] = pluginConfig!.LogToFile
            };
            return configuration;
        }

        public IList<string> UpdateGeneralConfiguration(IDictionary<string, string> configuration)
        {
            var errors = new List<string>();
            try
            {
                pluginConfig!.DebugLogging = CheckBoolValue(DebugLoggingConfiguration);
                pluginConfig!.LogToFile = CheckBoolValue(LogToFileConfiguration);
                PluginConfigChanged();
            }
            catch (Exception ex)
            {
                errors.Add(ex.GetFullMessage());
            }
            return errors;

            bool CheckBoolValue(string key)
            {
                return configuration.ContainsKey(key) && configuration[key] == "on";
            }
        }
    }
}