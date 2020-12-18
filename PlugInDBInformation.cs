using Hspi.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using static System.FormattableString;

namespace Hspi
{
    internal partial class PlugIn : HspiBase
    {
        public IDictionary<string, object> GetDBInformation()
        {
            return ScribanHelper.ToDictionary(pluginConfig.DBLoginInformation);
        }

        public IList<string> SaveDBInformation(IDictionary<string, string> dbInformationDBDict)
        {
            var errors = new List<string>();
            try
            {
                Trace.WriteLine(Invariant($"Updating DB Information"));

                if (!Uri.TryCreate(dbInformationDBDict["dburi"], UriKind.Absolute, out var uri))
                {
                    errors.Add("Database Uri is not valid");
                }
                else
                {
                    var dbInformationDBDict2 = ScribanHelper.ConvertToStringObjectDictionary(dbInformationDBDict);
                    dbInformationDBDict2["dburi"] = uri;

                    var influxDBLoginInformation = ScribanHelper.FromDictionary<InfluxDBLoginInformation>(dbInformationDBDict2);

                    if (string.IsNullOrWhiteSpace(influxDBLoginInformation.DB))
                    {
                        errors.Add("Database is empty");
                    }

                    if (errors.Count == 0)
                    {
                        // save
                        pluginConfig.DBLoginInformation = influxDBLoginInformation;
                        PluginConfigChanged();
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add(ex.GetFullMessage());
            }
            return errors;
        }
    }
}