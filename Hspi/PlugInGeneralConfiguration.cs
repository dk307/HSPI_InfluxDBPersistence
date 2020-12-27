using Hspi.Utils;
using System;
using System.Collections.Generic;

namespace Hspi
{
    internal partial class PlugIn : HspiBase
    {
        private const string DebugLoggingConfiguration = "debuglogging";

        public IDictionary<string, object> GetGeneralInformation()
        {
            var configuration = new Dictionary<string, object>();
            configuration[DebugLoggingConfiguration] = pluginConfig.DebugLogging;
            return configuration;
        }

        public IList<string> UpdateGeneralConfiguration(IDictionary<string, string> configuration)
        {
            var errors = new List<string>();
            try
            {
                pluginConfig.DebugLogging = configuration.ContainsKey(DebugLoggingConfiguration) &&
                                            configuration[DebugLoggingConfiguration] == "on";
                PluginConfigChanged();
            }
            catch (Exception ex)
            {
                errors.Add(ex.GetFullMessage());
            }
            return errors;
        }
    }
}