using Hspi.DeviceData;
using Hspi.Utils;
using NullGuard;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using static System.FormattableString;

namespace Hspi
{
    internal partial class PlugIn : HspiBase
    {
        public override bool SupportsConfigDevice => true;

        public IDictionary<string, object> GetDeviceImportData([AllowNull] string refIdString)
        {
            int refId = ParseRefId(refIdString);

            var data = new NumberDeviceData(HomeSeerSystem, refId);
          
            return ScribanHelper.ToDictionary(data.Data);
        }

        public IList<string> SaveDeviceImportData(IDictionary<string, string> deviceImportDataDict)
        {
            var errors = new List<string>();
            try
            {
                int refId = ParseRefId(deviceImportDataDict["refId"]);
                Trace.WriteLine(Invariant($"Adding existing persitence for Ref Id:{refId}"));

                var importDeviceData = ScribanHelper.FromDictionary<ImportDeviceData>(deviceImportDataDict);

                if (errors.Count == 0)
                {
                    // save
                    DeviceData.DeviceData.UpdateImportDevice(HomeSeerSystem, refId, importDeviceData);

                    PluginConfigChanged();
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