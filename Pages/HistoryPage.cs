using Hspi.Utils;
using NullGuard;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web;
using static System.FormattableString;

namespace Hspi.Pages
{
    internal partial class ConfigPage : PageHelper
    {

        private static void BuildQueryChartIFrame(StringBuilder stb, string finalQuery, string title = "")
        {
            string iFrameUrl = BuildChartUri(finalQuery, title);
            stb.Append(@"<style>iframe{width: 1px;min-width: 100%;border: none; width: 100%; height: 600px}</style>");
            stb.Append(Invariant($"<iframe id=\"tableFrame\" src=\"{iFrameUrl}\" scrolling=\"no\"></iframe>"));
            stb.Append(Invariant($"<script>iFrameResize({{log:false}}, '#{TableFrameId}')</script>"));
        }

        private static void BuildQueryTableIFrame(StringBuilder stb, string finalQuery, int tableSize = 10)
        {
            string iFrameUrl = BuildTableUri(finalQuery, tableSize);
            stb.Append(@"<style>iframe{width: 1px;min-width: 100%;border: none; width: 100%; height: 600px}</style>");
            stb.Append(Invariant($"<iframe id=\"tableFrame\" src=\"{iFrameUrl}\" scrolling=\"no\"></iframe>"));
            stb.Append(Invariant($"<script>iFrameResize({{log:false}}, '#{TableFrameId}')</script>"));
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

        private static IDictionary<string, FormattableString> GetDefaultValueQueries(DevicePersistenceData data)
        {
            List<string> fields = InfluxDbQueryBuilder.GetFields(data);

            var queries = new Dictionary<string, FormattableString>()
            {
                {
                    "Last 100 stored values",
                    $"SELECT {string.Join(",", fields)} from \"{data.Measurement}\" WHERE {PluginConfig.DeviceRefIdTag}='{data.DeviceRefId}' ORDER BY time DESC LIMIT 100"
                },
            };

            if (!string.IsNullOrWhiteSpace(data.Field))
            {
                var standardFields = Invariant($"MIN(\"{data.Field}\"), MAX(\"{data.Field}\"), MEAN(\"{data.Field}\"), MEDIAN(\"{data.Field}\"), PERCENTILE(\"{data.Field}\", 95) AS \"95 percentile\"");
                var subQuery24h = Invariant($"SELECT MEAN(\"{data.Field}\") as \"{data.Field}\" FROM \"{data.Measurement}\" WHERE {PluginConfig.DeviceRefIdTag}='{data.DeviceRefId}' AND time > now() - 24h GROUP BY time(1s) fill(previous)");
                queries.Add(
                     "Min/Max Values",
                     $"SELECT MIN(\"{data.Field}\"), MAX(\"{data.Field}\") from \"{data.Measurement}\" WHERE {PluginConfig.DeviceRefIdTag}='{data.DeviceRefId}'"
                );

                queries.Add(
                      "Min/Max/Average/Medium/Percentile Values(24h)",
                      $"SELECT {standardFields} FROM ({subQuery24h})"
                 );

                queries.Add(
                      "Min/Max/Average/Medium/Percentile Values By Hour(24h)",
                      $"SELECT {standardFields} FROM ({subQuery24h}) GROUP BY time(1h)"
                );
            }
            return queries;
        }


        private static string HistoryBackButton()
        {
            var b = new clsJQuery.jqButton("Back", "Back", DeviceUtiltyPageName, false)
            {
                id = NameToIdWithPrefix("Back"),
                url = Invariant($"/{pageUrl}?{TabId}=1"),
            };

            return b.Build();
        }


        private string BuildChartsPage(NameValueCollection parts)
        {
            CultureInfo cultureInfo = CultureInfo.CurrentUICulture;
            StringBuilder stb = new StringBuilder();
            var query = parts[QueryPartId] ?? string.Empty;
            var title = parts[TitlePartId] ?? string.Empty;
            try
            {
                var queryData = GetData(HttpUtility.UrlDecode(query));

                IncludeResourceCSS(stb, "metricsgraphics.css");
                IncludeResourceScript(stb, "d3.min.js");
                IncludeResourceScript(stb, "metricsgraphics.min.js");
                stb.AppendLine("<table align=\"center\" ");
                stb.AppendLine("<tr><td id='chartGraph2' align=\"center\"></td>");
                stb.AppendLine("<tr><td id='legend' align=\"center\"></td>");
                stb.AppendLine("</table>");

                stb.AppendLine("<script>");
                stb.AppendLine(@"function chartData() {");

                var legands = new List<string>();
                if (queryData.Count > 0)
                {
                    var nonTimeColumns = queryData.First().Keys.Where(x => (0 != string.CompareOrdinal(x, InfluxDBHelper.TimeColumn)));

                    foreach (var nonTimeColumn in nonTimeColumns)
                    {
                        legands.Add(Invariant($"'{FirstCharToUpper(nonTimeColumn, cultureInfo)}'"));
                    }

                    var dataStrings = new Dictionary<string, StringBuilder>();
                    foreach (var row in queryData)
                    {
                        long jsMilliseconds = 0;
                        foreach (var pair in row)
                        {
                            if (string.CompareOrdinal(pair.Key, InfluxDBHelper.TimeColumn) == 0)
                            {
                                var timePoint = (DateTime)pair.Value;
                                jsMilliseconds = (new DateTimeOffset(timePoint)).ToLocalTime().ToUnixTimeMilliseconds();
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

                stb.AppendLine(@"}
                var chartDataFromDB = chartData();");

                stb.AppendLine(@"MG.data_graphic({
                            data: chartDataFromDB,
                            target: document.getElementById('chartGraph2'),
                            width: 900,
                            x_extended_ticks: true,
                            height: 450,
                            area: true,
                            interpolate: d3.curveStep,
                            min_y_from_data: true,
                            right: 40,
                            // y_rollover_format: function(d) { return ' ' + String(d.value); },
                            x_rollover_format: function(d) { return d.date.toLocaleString() + ' '; },");
                stb.AppendLine(Invariant($"     title:'{title}',"));
                stb.AppendLine(Invariant($"     legend:[{string.Join(",", legands)}],"));
                stb.AppendLine("legend_target: document.getElementById('legend'),");
                stb.AppendLine("});");
                stb.AppendLine("</script>");
                IncludeResourceScript(stb, "iframeResizer.contentWindow.min.js");

                return stb.ToString();
            }
            catch (Exception ex)
            {
                return Invariant($"<br><div style='color:Red'>{ex.GetFullMessage()}</div><br>");
            }
        }

        private string BuildHistogramPage(NameValueCollection parts)
        {
            StringBuilder stb = new StringBuilder();
            var query = parts[QueryPartId] ?? string.Empty;
            var duration = parts[QueryDurationId] ?? string.Empty;

            QueryDuration queryDuration = QueryDuration.D1h;
            _ = Enum.TryParse<QueryDuration>(duration, out queryDuration);

            IncludeDataTableFiles(stb);
            IncludeResourceScript(stb, "iframeResizer.contentWindow.min.js");

            try
            {
                var culture = CultureInfo.CurrentUICulture;
                TimeSpan durationTimeSpan = InfluxDbQueryBuilder.GetTimeSpan(queryDuration);
                var queryData = GetData(HttpUtility.UrlDecode(query));

                var histogram = InfluxDBHelper.CreateHistogram(queryData, durationTimeSpan);

                if (histogram.Count > 0)
                {
                    //Display the first row/column only
                    stb.Append("<table id=\"results\" class=\"cell-border compact\" style=\"width:100%\">");

                    stb.Append(@"<thead align='left'><tr>");
                    stb.Append(@"<th>Value</th>");
                    stb.Append(@"<th>Total time</th>");
                    stb.Append(@"<th>Percentage</th>");
                    stb.Append(@"</tr></thead>");

                    stb.Append(@"<tbody>");

                    var firstRow = queryData[0];

                    foreach (var pair in histogram)
                    {
                        stb.Append(@"<tr class='tablecell'>");
                        stb.Append(@"<td>");
                        stb.Append(HtmlEncode(FirstCharToUpper(pair.Key, culture)));
                        stb.Append(@"</td>");

                        stb.Append(@"<td>");
                        stb.Append(HtmlEncode(InfluxDBHelper.GetSerieValue(culture, pair.Value)));
                        stb.Append(@"</td>");
                        stb.Append(@"<td>");
                        double percentage = 100 * pair.Value.TotalMilliseconds / durationTimeSpan.TotalMilliseconds;
                        stb.Append(HtmlEncode(InfluxDBHelper.GetSerieValue(culture, percentage)));
                        stb.Append(@"</td>");

                        stb.Append(@"</tr>");
                    }
                    stb.Append(@"</tbody>");

                    stb.AppendLine(@"</table>");
                }

                return stb.ToString();
            }
            catch (Exception ex)
            {
                return Invariant($"<br><div style='color:Red'>{ex.GetFullMessage()}</div><br>");
            }
        }


        private string BuildHistoryPage(NameValueCollection parts, DevicePersistenceData data)
        {
            StringBuilder stb = new StringBuilder();
            IncludeDataTableFiles(stb);
            IncludeResourceScript(stb, "iframeSizer.min.js");

            HSHelper hsHelper = new HSHelper(HS);

            string header = Invariant($"History - {hsHelper.GetName(data.DeviceRefId)}");

            stb.Append(FormStart("ftmDeviceHistory", "IdHistory", "Post"));
            stb.Append(@"<div>");

            stb.Append(@"<table class='full_width_table'>");

            stb.Append(Invariant($"<tr><td class='tableheader'>{header}</td></tr>"));
            var queries = GetDefaultValueQueries(data);
            string querySelection = parts[QueryPartId];
            if (string.IsNullOrWhiteSpace(querySelection))
            {
                querySelection = queries?.FirstOrDefault().Key;
            }

            NameValueCollection collection = new NameValueCollection();
            foreach (var query in queries)
            {
                collection.Add(query.Key, query.Key);
            }

            stb.Append(Invariant($"<tr><td>{FormDropDown(HistoryQueryTypeId, collection, querySelection, 400, string.Empty, true)}</td></tr>"));

            string finalQuery = Invariant(queries[querySelection]);
            stb.Append(Invariant($"<tr height='10'><td>{HtmlTextBox(RecordId, data.Id.ToString(CultureInfo.InvariantCulture), @type: "hidden")}</td></tr>"));
            stb.Append("<tr><td>");
            stb.Append(DivStart(QueryTestDivId, string.Empty));
            stb.Append(Invariant($"{TextArea(QueryTestId, finalQuery)}"));
            stb.Append(DivEnd());
            stb.Append(Invariant($"<br>{FormButton(HistoryRunQueryButtonName, "Run Query", "Run Query")}"));
            stb.Append("&nbsp;");
            stb.Append(Invariant($"{FormButton(HistoryShowChartButtonName, "Show Chart", "Show Chart")}</td></tr>"));
            stb.Append("<tr height='5'><td></td></tr>");
            stb.Append(Invariant($"<tr><td class='tableheader'>Results</td></tr>"));
            stb.Append("<tr><td>");
            stb.Append(DivStart(HistoryResultDivId, string.Empty));
            BuildQueryTableIFrame(stb, finalQuery);
            stb.Append(DivEnd());
            stb.Append("</td><tr>");
            stb.Append("<tr height='5'><td></td></tr>");
            stb.Append(Invariant($"<tr><td>{HistoryBackButton()}</td></tr>"));
            stb.Append("</table>");
            stb.Append("</div>");
            stb.Append(FormEnd());

            return stb.ToString();
        }


        private string BuildStatsPage(NameValueCollection parts)
        {
            StringBuilder stb = new StringBuilder();
            IncludeResourceScript(stb, "iframeResizer.contentWindow.min.js");

            var culture = CultureInfo.CurrentUICulture;

            var query = parts[QueryPartId] ?? string.Empty;
            try
            {
                var queryData = GetData(HttpUtility.UrlDecode(query));
                if (queryData.Count > 0)
                {
                    //Display the first row/column only
                    stb.Append("<table id=\"results\" class=\"cell-border compact\" style=\"width:100%\">");

                    var firstRow = queryData[0];

                    foreach (var pair in firstRow)
                    {
                        if (string.CompareOrdinal(pair.Key, InfluxDBHelper.TimeColumn) != 0)
                        {
                            stb.Append(@"<tr class='tablecell'>");
                            stb.Append(@"<td>");
                            stb.Append(HtmlEncode(FirstCharToUpper(pair.Key, culture)));
                            stb.Append(@"</td>");

                            stb.Append(@"<td>");
                            stb.Append(HtmlEncode(InfluxDBHelper.GetSerieValue(culture, pair.Value)));
                            stb.Append(@"</td>");

                            stb.Append(@"</tr>");
                        }
                    }
                    stb.AppendLine(@"</table>");
                }

                return stb.ToString();
            }
            catch (Exception ex)
            {
                return Invariant($"<br><div style='color:Red'>{ex.GetFullMessage()}</div><br>");
            }
        }

        private void BuildTable(string query, StringBuilder stb, int pageLength)
        {
            try
            {
                var culture = CultureInfo.CurrentUICulture;
                var queryData = GetData(query);

                if (queryData.Count > 0)
                {
                    var columns = queryData[0].Keys;

                    stb.Append("<table id=\"results\" class=\"cell-border compact\" style=\"width:100%\">");
                    stb.Append(@"<thead><tr>");
                    foreach (var column in columns)
                    {
                        stb.Append(Invariant($"<th>{ HtmlEncode(FirstCharToUpper(column, culture))}</th>"));
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
                                stb.Append(Invariant($"<td data-order='{HtmlEncode(sortValue)}' class='tablecell'>{HtmlEncode(value)}</td>"));
                            }
                            else
                            {
                                stb.Append(Invariant($"<td class='tablecell'>{HtmlEncode(value)}</td>"));
                            }
                        }
                        stb.Append(@"</tr>");
                    }
                    stb.Append(@"</tbody>");
                    stb.Append(@"</table>");

                    stb.AppendLine("<script type='text/javascript'>");
                    stb.AppendLine(@"$(document).ready(function() {");
                    stb.AppendLine(@"$('#results').DataTable({
                                        'order': [],");
                    stb.AppendLine(Invariant($"  'pageLength': {pageLength}, "));
                    stb.AppendLine(@"   'columnDefs': [
                                            { 'className': 'dt-left', 'targets': '_all'}
                                        ]
                                    });
                                });");
                    stb.AppendLine("</script>");
                } 
                else
                {
                    stb.AppendLine("No data");
                }
            }
            catch (Exception ex)
            {
                stb.Append(Invariant($"<br><div style='color:Red'>{ex.GetFullMessage()}</div><br>"));
            }
        }

        private string BuildTablePage(NameValueCollection parts)
        {
            StringBuilder stb = new StringBuilder();
            var query = parts[QueryPartId] ?? string.Empty;

            if (!int.TryParse(HttpUtility.UrlDecode(parts[TableSizeId] ?? string.Empty), out int size))
            {
                size = 25;
            }

            IncludeDataTableFiles(stb);
            IncludeResourceScript(stb, "iframeResizer.contentWindow.min.js");

            BuildTable(HttpUtility.UrlDecode(query), stb, size);
            return stb.ToString();
        }
        private IList<IDictionary<string, object>> GetData(string query)
        {
            var loginInformation = pluginConfig.DBLoginInformation;
            return InfluxDBHelper.ExecuteInfluxDBQuery(query, loginInformation).ResultForSync();
        }

        private void HandleHistoryPagePostBack(NameValueCollection parts, string form)
        {
            string finalQuery = null;
            if (form == NameToIdWithPrefix(HistoryQueryTypeId))
            {
                var queryType = parts[HistoryQueryTypeId];
                var id = parts[RecordId];
                var data = pluginConfig.DevicePersistenceData[id];
                var queries = GetDefaultValueQueries(data);

                finalQuery = Invariant(queries[queryType]);
                this.divToUpdate.Add(QueryTestDivId, TextArea(QueryTestId, finalQuery));
            }
            else if ((form == NameToIdWithPrefix(HistoryRunQueryButtonName) ||
                     (form == NameToIdWithPrefix(HistoryShowChartButtonName))))
            {
                finalQuery = parts[QueryTestId];
            }

            StringBuilder stb = new StringBuilder();
            if (form == NameToIdWithPrefix(HistoryShowChartButtonName))
            {
                BuildQueryChartIFrame(stb, finalQuery);
            }
            else
            {
                BuildQueryTableIFrame(stb, finalQuery);
            }
            this.divToUpdate.Add(HistoryResultDivId, stb.ToString());
        }

        private void IncludeDataTableFiles(StringBuilder stb)
        {
            IncludeResourceCSS(stb, "jquery.dataTables.css");
            IncludeResourceScript(stb, "jquery.dataTables.min.js");
        }

        private void IncludeResourceCSS(StringBuilder stb, string scriptFile)
        {
            stb.AppendLine("<style type=\"text/css\">");
            stb.AppendLine(Resource.ResourceManager.GetString(scriptFile.Replace('.', '_'), Resource.Culture));
            stb.AppendLine("</style>");
            this.AddScript(stb.ToString());
        }

        private void IncludeResourceScript(StringBuilder stb, string scriptFile)
        {
            stb.AppendLine("<script type='text/javascript'>");
            stb.AppendLine(Resource.ResourceManager.GetString(scriptFile.Replace('.', '_'), Resource.Culture));
            stb.AppendLine("</script>");
            this.AddScript(stb.ToString());
        }

        private const string HistoryQueryTypeId = "historyquerytypeid";
        private const string HistoryResultDivId = "historyresultdivid";
        private const string HistoryRunQueryButtonName = "historyrunquery";
        private const string HistoryShowChartButtonName = "historyshowchart";
        private const string QueryDurationId = "duration";
        private const string QueryPartId = "query";
        private const string QueryTestDivId = "querytestdivid";
        private const string QueryTestId = "querytextid";
        private const string TableFrameId = "tableFrame";
        private const string TableSizeId = "rows";
        private const string TitlePartId = "title";
    }
}