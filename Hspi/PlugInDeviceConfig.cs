using HomeSeer.Jui.Views;
using HomeSeer.PluginSdk.Devices;
using Hspi.Utils;
using NullGuard;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using static System.FormattableString;

namespace Hspi
{
    internal partial class PlugIn : HspiBase
    {
        public override bool SupportsConfigDeviceAll => true;

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

                var dataKeyPair = pluginConfig.DevicePersistenceData.FirstOrDefault(x => x.Value.DeviceRefId == refId);
                var data = dataKeyPair.Value;

                if (data != null)
                {
                    string deviceName = HSHelper.GetName(HomeSeerSystem, refId);

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

                var dataKeyPair = pluginConfig.DevicePersistenceData.FirstOrDefault(x => x.Value.DeviceRefId == refId);
                var data = dataKeyPair.Value;

                if (data != null)
                {
                    var chartQuery = InfluxDbQueryBuilder.GetChartQuery(data, "value", queryDuration.Value,
                                                          pluginConfig.DBLoginInformation,
                                                          groupInterval,
                                                          -TimeZoneInfo.Local.BaseUtcOffset).ResultForSync();

                    var queryData = GetData(chartQuery);

                    if (queryData.Count > 0)
                    {
                        var nonTimeColumns = queryData.First().Keys.Where(x => (0 != string.CompareOrdinal(x, InfluxDBHelper.TimeColumn)));

                        var dataStrings = new Dictionary<string, StringBuilder>();
                        foreach (var row in queryData)
                        {
                            long jsMilliseconds = 0;
                            foreach (var pair in row)
                            {
                                if (string.CompareOrdinal(pair.Key, InfluxDBHelper.TimeColumn) == 0)
                                {
                                    var timePoint = (DateTime)pair.Value;
                                    DateTimeOffset timeForPoint = new DateTimeOffset(timePoint);
                                    jsMilliseconds = timeForPoint.ToLocalTime().ToUnixTimeMilliseconds();
                                }
                                else
                                {
                                    if (pair.Value != null)
                                    {
                                        if (!dataStrings.TryGetValue(pair.Key, out StringBuilder stringBuilder))
                                        {
                                            stringBuilder = new StringBuilder();
                                            dataStrings.Add(pair.Key, stringBuilder);
                                        }
                                        stringBuilder.AppendLine(Invariant($"{{ date: new Date({jsMilliseconds}),value: {InfluxDBHelper.GetSerieValue(CultureInfo.InvariantCulture, pair.Value)}}},"));
                                    }
                                }
                            }
                        }

                        stb.AppendLine("[");
                        foreach (var nonTimeColumn in nonTimeColumns)
                        {
                            stb.AppendLine("[");
                            stb.Append(dataStrings[nonTimeColumn].ToString());
                            stb.AppendLine("],");
                        }
                        stb.AppendLine("]");
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError(Invariant($"Error {ex.GetFullMessage()}"));
            }

            return stb.ToString();
        }

        public string BuildHistogramData(string refIdString, string duration)
        {
            StringBuilder stb = new StringBuilder();
            try
            {
                (int refId, TimeSpan? queryDuration) = ParseDataCallValues(refIdString, duration);

                var dataKeyPair = pluginConfig.DevicePersistenceData.FirstOrDefault(x => x.Value.DeviceRefId == refId);
                var data = dataKeyPair.Value;

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

                var dataKeyPair = pluginConfig.DevicePersistenceData.FirstOrDefault(x => x.Value.DeviceRefId == refId);
                var data = dataKeyPair.Value;

                if (data != null)
                {
                    string deviceName = HSHelper.GetName(HomeSeerSystem, refId);

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

                Trace.WriteLine(Invariant($"Deleting persitence for Ref Id:{refId}"));

                var dataKeyPairs = pluginConfig.DevicePersistenceData.Where(x => x.Value.DeviceRefId == refId);

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

        public IList<string> GetAllowedDisplays([AllowNull] string refIdString)
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

        public IList<IDictionary<string, object>> GetAllPersistantData()
        {
            var list = new List<IDictionary<string, object>>();

            foreach (var pair in this.pluginConfig.DevicePersistenceData)
            {
                var data = ScribanHelper.ToDictionary(pair.Value);
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

        public override string GetJuiDeviceConfigPage(int deviceRef)
        {
            var device = HomeSeerSystem.GetDeviceByRef(deviceRef);
            return CreateDeviceConfigPage(device, device.Interface == Id ? "deviceimport.html" : "feature.html");
        }

        public IDictionary<string, object> GetPersistanceData([AllowNull] string refIdString)
        {
            int refId = ParseRefId(refIdString);
            var dataKeyPair = pluginConfig.DevicePersistenceData.FirstOrDefault(x => x.Value.DeviceRefId == refId);
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
                                            null,
                                            maxValidValue,
                                            minValidValue,
                                            null);

            return ScribanHelper.ToDictionary(data);
        }

        public override bool HasJuiDeviceConfigPage(int deviceRef)
        {
            return true;
        }

        public IList<string> SavePersistanceData(IDictionary<string, string> persistanceDataDict)
        {
            var errors = new List<string>();
            try
            {
                var persistanceDataDict2 = ScribanHelper.ConvertToStringObjectDictionary(persistanceDataDict);

                if (!persistanceDataDict.TryGetValue("id", out var value) || string.IsNullOrEmpty(value))
                {
                    Trace.WriteLine(Invariant($"Adding new persitence for Ref Id:{persistanceDataDict["devicerefid"]}"));
                    persistanceDataDict2["id"] = Guid.NewGuid().ToString();
                }
                else
                {
                    Trace.WriteLine(Invariant($"Adding existing persitence for Ref Id:{persistanceDataDict["devicerefid"]}"));
                }

                var persistantData = ScribanHelper.FromDictionary<DevicePersistenceData>(persistanceDataDict2);

                // validate
                if (string.IsNullOrWhiteSpace(persistantData.Field) && string.IsNullOrWhiteSpace(persistantData.FieldString))
                {
                    errors.Add("Both <B>Field for Numeric Value</B> and <B>Field for String Value</B> cannot be empty");
                }

                if (errors.Count == 0)
                {
                    // save
                    pluginConfig.AddDevicePersistenceData(persistantData);
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
                        string sortValue = null;

                        if (string.CompareOrdinal(columnName, InfluxDBHelper.TimeColumn) == 0)
                        {
                            DateTime timePoint = ((DateTime)column).ToLocalTime();
                            sortValue = (new DateTimeOffset(timePoint)).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
                            value = ConvertInfluxDBDateTimeToString(today, culture, timePoint);
                        }
                        else
                        {
                            value = InfluxDBHelper.GetSerieValue(culture, column);
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

        private static int ParseRefId(string refIdString)
        {
            return int.Parse(refIdString,
                                  System.Globalization.NumberStyles.Any,
                                  CultureInfo.InvariantCulture);
        }

        private void AddToDisplayDetails(IList<string> displayTypes, int refId)
        {
            var dataKeyPair = pluginConfig.DevicePersistenceData.FirstOrDefault(x => x.Value.DeviceRefId == refId);
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

            stb.Append("<script> $('#save_device_config').hide(); </script>");

            string iFrameUrl = Invariant($"{CreatePlugInUrl(iFrameName)}?refId={device.Ref}");

            // iframeSizer.min.js
            stb.Append($"<script type=\"text/javascript\" src=\"{CreatePlugInUrl("iframeSizer.min.js")}\"></script>");
            stb.Append($"<script type=\"text/javascript\" src=\"{CreatePlugInUrl("feature.js")}\"></script>");

            stb.Append(@"<style>iframe{width: 1px;min-width: 100%;border: none; width: 100%; height: 475px}</style>");
            stb.Append(Invariant($"<iframe id=\"tableFrame\" src=\"about:blank\" scrolling=\"no\"></iframe>"));
            stb.Append(Invariant($"<script>var iFrameUrl678='{iFrameUrl}';</script>"));
            stb.Append(Invariant($"<script>$('#tableFrame')[0].contentWindow.location.replace(iFrameUrl678 + '&feature=' + getUrlParameterOrEmpty('feature'));</script>"));
            stb.Append(Invariant($"<script>iFrameResize({{log:true}});</script>"));

            var page = PageFactory.CreateGenericPage(Id, "Device").WithLabel("id", stb.ToString());
            return page.Page.ToJsonString();
        }

        private string CreatePlugInUrl(string fileName)
        {
            return "/" + Id + "/" + fileName;
        }

        private IList<IDictionary<string, object>> GetData(string query)
        {
            var loginInformation = pluginConfig.DBLoginInformation;
            return InfluxDBHelper.ExecuteInfluxDBQuery(query, loginInformation).ResultForSync();
        }
    }
}