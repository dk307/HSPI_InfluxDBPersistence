using HomeSeer.Jui.Views;
using HomeSeer.PluginSdk.Devices;
using Hspi.DeviceData;
using Hspi.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using static System.FormattableString;

#nullable enable

namespace Hspi
{
    internal partial class PlugIn : HspiBase
    {
        public override bool SupportsConfigDeviceAll => true;

        public override bool SupportsConfigFeature => true;

        public string BuildAverageStatsData(string refIdString, string duration, string grouping)
        {
            StringBuilder stb = new StringBuilder();
            stb.Append("<tbody>");

            try
            {
                (int refId, TimeSpan? queryDuration) = ParseDataCallValues(refIdString, duration);

                if (!queryDuration.HasValue)
                {
                    throw new ArgumentException("Invalid Duration", nameof(duration));
                }

                TimeSpan? groupInterval = null;
                if (!string.IsNullOrEmpty(grouping))
                {
                    groupInterval = TimeSpan.Parse(grouping, CultureInfo.InvariantCulture);
                }

                var dataKeyPair = pluginConfig!.DevicePersistenceData.FirstOrDefault(x => x.Value.DeviceRefId == refId);
                var data = dataKeyPair.Value;

                if (data != null)
                {
                    string deviceName = HSDeviceHelper.GetName(HomeSeerSystem, refId);

                    var queries = InfluxDbQueryBuilder.GetStatsQueries(data,
                                                                  queryDuration.Value,
                                                                  pluginConfig.DBLoginInformation,
                                                                  groupInterval,
                                                                  -TimeZoneInfo.Local.BaseUtcOffset).ResultForSync();

                    foreach (var query in queries)
                    {
                        var queryData = GetData(query);
                        if (queryData.Count > 0)
                        {
                            var firstRow = queryData[0];
                            foreach (var pair in firstRow)
                            {
                                if (string.CompareOrdinal(pair.Key, InfluxDBHelper.TimeColumn) != 0)
                                {
                                    stb.Append(@"<tr>");
                                    stb.Append(@"<td>");
                                    stb.Append(WebUtility.HtmlEncode(pair.Key));
                                    stb.Append(@"</td>");

                                    stb.Append(@"<td>");
                                    stb.Append(WebUtility.HtmlEncode(InfluxDBHelper.GetSerieValue(CultureInfo.InvariantCulture, pair.Value)));
                                    stb.Append(@"</td>");

                                    stb.Append(@"</tr>");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                stb.Append(Invariant($"<tr><td style='color:Red' colspan=\"2\">{ex.GetFullMessage()}</td><tr>"));
            }

            stb.Append("</tbody>");
            return stb.ToString();
        }

        public string GetRegularDataAsJSArray(string refIdString, string duration, string grouping)
        {
            StringBuilder stb = new StringBuilder();
            try
            {
                (int refId, TimeSpan? queryDuration) = ParseDataCallValues(refIdString, duration);

                if (!queryDuration.HasValue)
                {
                    throw new ArgumentException("Invalid Duration", nameof(duration));
                }

                TimeSpan? groupInterval = null;
                if (!string.IsNullOrEmpty(grouping))
                {
                    groupInterval = TimeSpan.Parse(grouping, CultureInfo.InvariantCulture);
                }

                var dataKeyPair = pluginConfig!.DevicePersistenceData.FirstOrDefault(x => x.Value.DeviceRefId == refId);
                var data = dataKeyPair.Value;

                stb.AppendLine("[");
                if (data != null)
                {
                    string columnName = "Value";

                    var chartQuery = InfluxDbQueryBuilder.GetChartQuery(data, columnName, queryDuration.Value,
                                                          pluginConfig.DBLoginInformation,
                                                          groupInterval,
                                                          -TimeZoneInfo.Local.BaseUtcOffset).ResultForSync();

                    var queryData = GetData(chartQuery);

                    if (queryData.Count > 0)
                    {
                        var dataStrings = new Dictionary<string, string>();
                        foreach (var row in queryData)
                        {
                            if (row.TryGetValue(InfluxDBHelper.TimeColumn, out var timeValue) &&
                                row.TryGetValue(columnName, out var columnNameValue) &&
                                (timeValue != null) && (columnNameValue != null))
                            {
                                var timePoint = (DateTime)timeValue;
                                DateTimeOffset timeForPoint = new DateTimeOffset(timePoint);
                                var jsMilliseconds = timeForPoint.ToLocalTime().ToUnixTimeMilliseconds();

                                string line = Invariant($"{{ date: new Date({jsMilliseconds}),value: {InfluxDBHelper.GetSerieValue(CultureInfo.InvariantCulture, columnNameValue)}}},");
                                stb.AppendLine(line);
                            }
                        }
                    }
                }
                stb.AppendLine("]");
            }
            catch (Exception ex)
            {
                logger.Error(Invariant($"Error {ex.GetFullMessage()}"));
            }

            return stb.ToString();
        }

        public string BuildHistogramData(string refIdString, string duration)
        {
            StringBuilder stb = new StringBuilder();
            try
            {
                (int refId, TimeSpan? queryDuration) = ParseDataCallValues(refIdString, duration);

                var dataKeyPair = pluginConfig!.DevicePersistenceData.FirstOrDefault(x => x.Value.DeviceRefId == refId);
                var data = dataKeyPair.Value;
                if (queryDuration == null)
                {
                    throw new ArgumentException("Invalid Duration", nameof(duration));
                }

                if (data != null)
                {
                    var query = InfluxDbQueryBuilder.GetHistogramQuery(data, queryDuration.Value, pluginConfig.DBLoginInformation).ResultForSync();
                    var queryData = GetData(query);
                    var histogram = InfluxDBHelper.CreateHistogram(queryData, queryDuration.Value);

                    if (histogram.Count > 0)
                    {
                        stb.Append(@"<tbody>");

                        var firstRow = queryData[0];

                        foreach (var pair in histogram)
                        {
                            stb.Append(@"<tr>");
                            stb.Append(@"<td>");
                            stb.Append(WebUtility.HtmlEncode(pair.Key));
                            stb.Append(@"</td>");

                            stb.Append(@"<td>");
                            stb.Append(WebUtility.HtmlEncode(InfluxDBHelper.GetSerieValue(CultureInfo.InvariantCulture, pair.Value)));
                            stb.Append(@"</td>");
                            stb.Append(@"<td>");
                            double percentage = 100 * pair.Value.TotalMilliseconds / queryDuration.Value.TotalMilliseconds;
                            stb.Append(WebUtility.HtmlEncode(InfluxDBHelper.GetSerieValue(CultureInfo.InvariantCulture, percentage)));
                            stb.Append(@"</td>");
                            stb.Append(@"</tr>");
                        }
                        stb.Append(@"</tbody>");
                    }
                }
            }
            catch (Exception ex)
            {
                stb.Append(Invariant($"<tr><td style='color:Red'>{ex.GetFullMessage()}</td><tr>"));
            }
            return stb.ToString();
        }

        public string BuildHistoryTableData(string refIdString, string maxCount, string duration)
        {
            StringBuilder stb = new StringBuilder();
            try
            {
                (int refId, TimeSpan? queryDuration) = ParseDataCallValues(refIdString, duration);

                int? maxRecords = null;
                if (!string.IsNullOrEmpty(maxCount))
                {
                    maxRecords = int.Parse(maxCount, CultureInfo.InvariantCulture);
                }

                var dataKeyPair = pluginConfig!.DevicePersistenceData.FirstOrDefault(x => x.Value.DeviceRefId == refId);
                var data = dataKeyPair.Value;

                if (data != null)
                {
                    string deviceName = HSDeviceHelper.GetName(HomeSeerSystem, refId);

                    var queries = InfluxDbQueryBuilder.GetHistoryQueries(data, deviceName, maxRecords, queryDuration);
                    var queryData = GetData(queries.Item1);
                    BuildTable(stb, queryData);

                    if (stb.Length == 0)
                    {
                        var queryData2 = GetData(queries.Item2);
                        BuildTable(stb, queryData2);
                    }
                }
            }
            catch (Exception ex)
            {
                stb.Append(Invariant($"<tr><td style='color:Red'>{ex.GetFullMessage()}</td><tr>"));
            }

            return stb.ToString();
        }

        public IList<string> DeletePersistanceData(string refIdString)
        {
            var errors = new List<string>();
            try
            {
                int refId = ParseRefId(refIdString);

                logger.Debug(Invariant($"Deleting persitence for Ref Id:{refId}"));

                var dataKeyPairs = pluginConfig!.DevicePersistenceData.Where(x => x.Value.DeviceRefId == refId);

                foreach (var pair in dataKeyPairs)
                {
                    pluginConfig.RemoveDevicePersistenceData(pair.Key);
                }

                PluginConfigChanged();
            }
            catch (Exception ex)
            {
                errors.Add(ex.GetFullMessage());
            }
            return errors;
        }

        public IList<string> GetAllowedDisplays(string? refIdString)
        {
            var displays = new List<string>();

            if (string.IsNullOrEmpty(refIdString))
            {
                return displays;
            }

            int refId = ParseRefId(refIdString);
            AddToDisplayDetails(displays, refId);
            return displays;
        }

        public IList<IDictionary<string, string>> GetAllPersistantData()
        {
            var list = new List<IDictionary<string, string>>();

            foreach (var pair in this.pluginConfig!.DevicePersistenceData)
            {
                var data = ScribanHelper.ToDictionaryS(pair.Value);
                list.Add(data);
            }

            return list;
        }

        public IDictionary<int, string> GetDeviceAndFeaturesNames(string refIdString)
        {
            var idNames = new Dictionary<int, string>();
            int refId = ParseRefId(refIdString);

            idNames.Add(refId, GetNameOfDevice(refId));

            var featureRefIds = (HashSet<int>)HomeSeerSystem.GetPropertyByRef(refId, EProperty.AssociatedDevices);

            foreach (var featureRefId in featureRefIds)
            {
                idNames.Add(featureRefId, GetNameOfDevice(featureRefId));
            }
            return idNames;

            string GetNameOfDevice(int deviceRefId)
            {
                return HomeSeerSystem.GetPropertyByRef(deviceRefId, EProperty.Name).ToString() ?? Invariant($"RefId:{deviceRefId}");
            }
        }

        public IDictionary<string, object> GetDeviceDetails(string refIdString)
        {
            var data = new Dictionary<string, object>();
            try
            {
                int refId = ParseRefId(refIdString);

                var device = HomeSeerSystem.GetDeviceByRef(refId);

                if (HomeSeerSystem.IsRefDevice(refId))
                {
                    data.Add("ref", refId);
                }
                else
                {
                    var associatedDevices = (HashSet<int>)HomeSeerSystem.GetPropertyByRef(refId, EProperty.AssociatedDevices);
                    data.Add("ref", associatedDevices.First());
                    data.Add("feature", refId);
                }
            }
            catch
            {
            }
            return data;
        }

        public override string GetJuiDeviceConfigPage(int deviceOrFeatureRef)
        {
            HomeSeerSystem.IsRefDevice(deviceOrFeatureRef);
            
            var device = HomeSeerSystem.GetDeviceByRef(deviceOrFeatureRef);
            return CreateDeviceConfigPage(device, device.Interface == Id ? "deviceimport.html" : "feature.html");
        }

        public IDictionary<string, object> GetPersistanceData(string? refIdString)
        {
            int refId = ParseRefId(refIdString);
            var dataKeyPair = pluginConfig!.DevicePersistenceData.FirstOrDefault(x => x.Value.DeviceRefId == refId);
            var data = dataKeyPair.Value;

            if (data != null)
            {
                return ScribanHelper.ToDictionary(data);
            }

            // prefill values
            HSHelper hSHelper = new HSHelper(HomeSeerSystem);
            hSHelper.Fill(refId, out var measurement, out var field, out var fieldString, out var maxValidValue, out var minValidValue);

            data = new DevicePersistenceData(string.Empty,
                                            refId,
                                            measurement ?? string.Empty,
                                            field,
                                            fieldString,
                                            maxValidValue,
                                            minValidValue,
                                            null);

            return ScribanHelper.ToDictionary(data);
        }

        public override bool HasJuiDeviceConfigPage(int deviceRef)
        {
            try
            {
                string deviceInterface = (string)HomeSeerSystem.GetPropertyByRef(deviceRef, EProperty.Interface);

                if (deviceInterface == PlugInData.PlugInId)
                {
                    string? deviceType = HSDeviceHelper.GetDeviceTypeFromPlugInData(HomeSeerSystem, deviceRef);
                    return DeviceImportDevice.DeviceType == deviceType ||
                           DeviceImportDevice.RootDeviceType == deviceType;
                }

                return true;
            }
            catch (Exception ex)
            {
                logger.Warn(Invariant($"Failed to determine Device ConfigPage for {deviceRef} with {ex.GetFullMessage()}"));
                return true;
            }
        }

        public IList<string> SavePersistanceData(IDictionary<string, string> persistanceDataDict)
        {
            var errors = new List<string>();
            try
            {
                if (!persistanceDataDict.TryGetValue("id", out var value) || string.IsNullOrEmpty(value))
                {
                    logger.Debug(Invariant($"Adding new persitence for Ref Id:{persistanceDataDict["devicerefid"]}"));
                    persistanceDataDict["id"] = Guid.NewGuid().ToString();
                }
                else
                {
                    logger.Debug(Invariant($"Updating existing persitence for Ref Id:{persistanceDataDict["devicerefid"]}"));
                }

                var persistantData = ScribanHelper.FromDictionary<DevicePersistenceData>(persistanceDataDict);

                // validate
                if (string.IsNullOrWhiteSpace(persistantData.Field) && string.IsNullOrWhiteSpace(persistantData.FieldString))
                {
                    errors.Add("Both <B>Field for Numeric Value</B> and <B>Field for String Value</B> cannot be empty");
                }

                if (errors.Count == 0)
                {
                    // save
                    pluginConfig!.AddDevicePersistenceData(persistantData);
                    PluginConfigChanged();
                }
            }
            catch (Exception ex)
            {
                errors.Add(ex.GetFullMessage());
            }
            return errors;
        }

        private static void BuildTable(StringBuilder stb, IList<IDictionary<string, object>> queryData)
        {
            var culture = CultureInfo.CurrentUICulture;

            if (queryData.Count > 0)
            {
                var columns = queryData[0].Keys;

                DateTimeOffset today = DateTimeOffset.Now.Date;
                foreach (var row in queryData)
                {
                    bool anyValue = row.Any(x => (string.CompareOrdinal(x.Key, InfluxDBHelper.TimeColumn) != 0) && (x.Value != null));

                    if (!anyValue)
                    {
                        continue;
                    }

                    stb.Append(@"<tr>");

                    foreach (var columnName in columns)
                    {
                        object column = row[columnName];
                        string value = string.Empty;
                        string? sortValue = null;

                        if (string.CompareOrdinal(columnName, InfluxDBHelper.TimeColumn) == 0)
                        {
                            DateTime timePoint = ((DateTime)column).ToLocalTime();
                            sortValue = (new DateTimeOffset(timePoint)).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
                            value = ConvertInfluxDBDateTimeToString(today, culture, timePoint);
                        }
                        else
                        {
                            value = InfluxDBHelper.GetSerieValue(culture, column) ?? string.Empty;
                        }

                        if (sortValue != null)
                        {
                            stb.Append(Invariant($"<td data-order='{WebUtility.HtmlEncode(sortValue)}'>{WebUtility.HtmlEncode(value)}</td>"));
                        }
                        else
                        {
                            stb.Append(Invariant($"<td>{WebUtility.HtmlEncode(value)}</td>"));
                        }
                    }
                    stb.Append(@"</tr>");
                }
                stb.Append(@"</tbody>");
            }
        }

        private static string ConvertInfluxDBDateTimeToString(DateTimeOffset today, CultureInfo culture, DateTime dateTime)
        {
            var dateTimeToday = dateTime.Date;

            if (today == dateTimeToday)
            {
                return "Today " + dateTime.ToString(culture.DateTimeFormat.LongTimePattern, culture);
            }
            else if (today.AddDays(-1) == dateTimeToday)
            {
                return "Yesterday " + dateTime.ToString(culture.DateTimeFormat.LongTimePattern, culture);
            }

            string dateTimePattern = culture.DateTimeFormat.ShortDatePattern +
                         " " + culture.DateTimeFormat.LongTimePattern;

            return dateTime.ToString(dateTimePattern, culture);
        }

        private static (int, TimeSpan?) ParseDataCallValues(string refIdString, string duration)
        {
            int refId = ParseRefId(refIdString);
            TimeSpan? queryDuration = null;

            if (!string.IsNullOrEmpty(duration))
            {
                queryDuration = TimeSpan.Parse(duration, CultureInfo.InvariantCulture);
            }

            return (refId, queryDuration);
        }

        private void AddToDisplayDetails(IList<string> displayTypes, int refId)
        {
            var dataKeyPair = pluginConfig!.DevicePersistenceData.FirstOrDefault(x => x.Value.DeviceRefId == refId);
            var data = dataKeyPair.Value;

            if (data != null)
            {
                bool hasNumericData = !string.IsNullOrWhiteSpace(data.Field);

                displayTypes.Add("table");

                if (hasNumericData)
                {
                    displayTypes.Add("chart");
                    displayTypes.Add("averageStats");
                }
                else
                {
                    displayTypes.Add("histogram");
                }
            }
        }

        private string CreateDeviceConfigPage(AbstractHsDevice device, string iFrameName)
        {
            StringBuilder stb = new StringBuilder();
            stb.Append($"<script src=\"{CreatePlugInUrl("iframeSizer.min.js")}\"></script>");
            stb.Append($"<script src=\"{CreatePlugInUrl("feature.js")}\"></script>");
            stb.Append($"<script>setupIFrame({device.Ref}, '{iFrameName}');</script>");
            var page = PageFactory.CreateGenericPage(Id, "InfluxDb Device").WithLabel("id_influxdbpersistance", stb.ToString());
            return page.Page.ToJsonString();
        }

        private string CreatePlugInUrl(string fileName)
        {
            return "/" + Id + "/" + fileName;
        }

        private IList<IDictionary<string, object>> GetData(string query)
        {
            var loginInformation = pluginConfig!.DBLoginInformation;
            return InfluxDBHelper.ExecuteInfluxDBQuery(query, loginInformation).ResultForSync();
        }
    }
}