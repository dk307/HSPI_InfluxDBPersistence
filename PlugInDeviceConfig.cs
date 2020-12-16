using HomeSeer.Jui.Views;
using HomeSeer.PluginSdk.Devices;
using Hspi.Pages;
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
           
            stb.Append(@"<thead><tr>");
            stb.Append(@"<th>Type</th>");
            stb.Append(@"<th>Value</th>");
            stb.Append(@"</tr></thead>");
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
                    HSHelper hSHelper = new HSHelper(HomeSeerSystem);
                    string deviceName = hSHelper.GetName(refId);

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
                                    stb.Append(WebUtility.HtmlEncode(FirstCharToUpper(pair.Key, CultureInfo.InvariantCulture)));
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

        public string BuildChartData(string refIdString, string duration, string grouping)
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

                    stb.AppendLine("<script>");
                    stb.AppendLine(@"function chartData() {");

                    var legands = new List<string>();
                    if (queryData.Count > 0)
                    {
                        var nonTimeColumns = queryData.First().Keys.Where(x => (0 != string.CompareOrdinal(x, InfluxDBHelper.TimeColumn)));

                        foreach (var nonTimeColumn in nonTimeColumns)
                        {
                            legands.Add(Invariant($"'{FirstCharToUpper(nonTimeColumn, CultureInfo.CurrentUICulture)}'"));
                        }

                        var limit = DateTimeOffset.UtcNow - queryDuration.Value;
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
                                    if (timeForPoint < limit)
                                    {
                                        // time is before the range
                                        // break;
                                    }

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

                        stb.AppendLine("return [");
                        foreach (var nonTimeColumn in nonTimeColumns)
                        {
                            stb.AppendLine("[");
                            stb.Append(dataStrings[nonTimeColumn].ToString());
                            stb.AppendLine("],");
                        }
                        stb.AppendLine("]");
                    }

                    stb.AppendLine(@"}");
                    stb.AppendLine("</script>");
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
                    HSHelper hSHelper = new HSHelper(HomeSeerSystem);

                    var query = InfluxDbQueryBuilder.GetHistogramQuery(data, queryDuration.Value, pluginConfig.DBLoginInformation).ResultForSync();

                    var culture = CultureInfo.CurrentUICulture;
                    var queryData = GetData(query);

                    var histogram = InfluxDBHelper.CreateHistogram(queryData, queryDuration.Value);

                    if (histogram.Count > 0)
                    {
                        stb.Append(@"<thead><tr>");
                        stb.Append(@"<th>Value</th>");
                        stb.Append(@"<th>Total time</th>");
                        stb.Append(@"<th>Percentage</th>");
                        stb.Append(@"</tr></thead>");

                        stb.Append(@"<tbody>");

                        var firstRow = queryData[0];

                        foreach (var pair in histogram)
                        {
                            stb.Append(@"<tr>");
                            stb.Append(@"<td>");
                            stb.Append(WebUtility.HtmlEncode(FirstCharToUpper(pair.Key, culture)));
                            stb.Append(@"</td>");

                            stb.Append(@"<td>");
                            stb.Append(WebUtility.HtmlEncode(InfluxDBHelper.GetSerieValue(culture, pair.Value)));
                            stb.Append(@"</td>");
                            stb.Append(@"<td>");
                            double percentage = 100 * pair.Value.TotalMilliseconds / queryDuration.Value.TotalMilliseconds;
                            stb.Append(WebUtility.HtmlEncode(InfluxDBHelper.GetSerieValue(culture, percentage)));
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
                    HSHelper hSHelper = new HSHelper(HomeSeerSystem);
                    string deviceName = hSHelper.GetName(refId);

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

        public IList<IDictionary<string, object>> GetAllowedDisplays([AllowNull] string refIdString)
        {
            var graphs = new List<IDictionary<string, object>>();

            if (string.IsNullOrEmpty(refIdString))
            {
                return graphs;
            }

            int refId = ParseRefId(refIdString);

            var device = HomeSeerSystem.GetDeviceByRef(refId);

            HSHelper hSHelper = new HSHelper(HomeSeerSystem);
            AddToDisplayDetails(graphs, hSHelper, device);

            return graphs;
        }

        public string GetDisplayName(string refIdString)
        {
            try
            {
                int refId = ParseRefId(refIdString);
                HSHelper hSHelper = new HSHelper(HomeSeerSystem);
                return hSHelper.GetName(refId);
            }
            catch (Exception)
            {
                return "Unknown";
            }
        }

        public override string GetJuiDeviceConfigPage(int deviceRef)
        {
            StringBuilder stb = new StringBuilder();

            stb.Append("<script> $('#save_device_config').hide(); </script>");

            string iFrameUrl = Invariant($"{CreatePlugInUrl("feature.html")}?refId={deviceRef}");

            // iframeSizer.min.js
            stb.Append($"<script type=\"text/javascript\" src=\"{CreatePlugInUrl("iframeSizer.min.js")}\"></script>");
            stb.Append($"<script type=\"text/javascript\" src=\"{CreatePlugInUrl("feature.js")}\"></script>");

            stb.Append(@"<style>iframe{width: 1px;min-width: 100%;border: none; width: 100%; height: 475px}</style>");
            stb.Append(Invariant($"<iframe id=\"tableFrame\" src=\"about:blank\" scrolling=\"no\"></iframe>"));
            stb.Append(Invariant($"<script>var iFrameUrl678='{iFrameUrl}';</script>"));
            stb.Append(Invariant($"<script>$('#tableFrame').attr('src', iFrameUrl678 + '&feature=' + getUrlParameterOrEmpty('feature'));</script>"));
            stb.Append(Invariant($"<script>iFrameResize({{log:true}});</script>"));

            var page = PageFactory.CreateGenericPage(Id, "Device").WithLabel("id", stb.ToString());

            return page.Page.ToJsonString();
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

        public override bool HasJuiDeviceConfigPage(int deviceRef)
        {
            return true;
        }

        private static void BuildTable(StringBuilder stb, IList<IDictionary<string, object>> queryData)
        {
            var culture = CultureInfo.CurrentUICulture;

            if (queryData.Count > 0)
            {
                var columns = queryData[0].Keys;

                stb.Append(@"<thead><tr>");
                foreach (var column in columns)
                {
                    stb.Append(Invariant($"<th>{ WebUtility.HtmlEncode(FirstCharToUpper(column, culture))}</th>"));
                }
                stb.Append(@"</tr></thead>");
                stb.Append(@"<tbody>");

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

        private static string FirstCharToUpper(string input, CultureInfo culture)
        {
            switch (input)
            {
                case null: return null;
                case "": return string.Empty;
                default: return input.First().ToString(culture).ToUpper(culture) + input.Substring(1);
            }
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

        private void AddToDisplayDetails(IList<IDictionary<string, object>> graphs,
                                         HSHelper hSHelper,
                                         AbstractHsDevice device)
        {
            var dataKeyPair = pluginConfig.DevicePersistenceData.FirstOrDefault(x => x.Value.DeviceRefId == device.Ref);
            var data = dataKeyPair.Value;

            if (data != null)
            {
                string featureName = hSHelper.GetName(device);
                bool hasNumericData = !string.IsNullOrWhiteSpace(data.Field);

                var displayData = new Dictionary<string, object>();
                displayData.Add("refId", device.Ref);
                displayData.Add("name", featureName);

                var displayTypes = new List<string>();
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

                displayData["displayTypes"] = displayTypes.ToArray();
                graphs.Add(displayData);
            }
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