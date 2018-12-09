using InfluxData.Net.InfluxDb.Models.Responses;
using Scheduler;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web;

namespace Hspi
{
    using static System.FormattableString;

    internal partial class ConfigPage : PageBuilderAndMenu.clsPageBuilder
    {
        private static string ConvertInfluxDBDateTimeToString(DateTimeOffset today, CultureInfo culture, long timePoint)
        {
            var dateTime = DateTimeOffset.FromUnixTimeSeconds(timePoint).ToLocalTime();
            var dateTimeToday = dateTime.Date;

            if (today == dateTimeToday)
            {
                return "Today " + dateTime.ToString(culture.DateTimeFormat.LongTimePattern);
            }
            else if (today.AddDays(-1) == dateTimeToday)
            {
                return "Yesterday " + dateTime.ToString(culture.DateTimeFormat.LongTimePattern);
            }

            string dateTimePattern = culture.DateTimeFormat.ShortDatePattern +
                         " " + culture.DateTimeFormat.LongTimePattern;

            return dateTime.ToString(dateTimePattern, culture);
        }

        private static string FirstCharToUpper(string input)
        {
            switch (input)
            {
                case null: return null;
                case "": return string.Empty;
                default: return input.First().ToString().ToUpper() + input.Substring(1);
            }
        }

        private static List<string> GetFields(DevicePersistenceData data)
        {
            List<string> fields = new List<string>();

            if (!string.IsNullOrWhiteSpace(data.Field))
            {
                fields.Add(Invariant($"\"{data.Field}\""));
            }
            else if (!string.IsNullOrWhiteSpace(data.FieldString))
            {
                fields.Add(Invariant($"\"{data.FieldString}\""));
            }

            return fields;
        }

        private string BuildChartsPage(NameValueCollection parts)
        {
            StringBuilder stb = new StringBuilder();
            var query = parts[QueryPartId] ?? string.Empty;
            var title = parts[TitlePartId] ?? string.Empty;
            try
            {
                var queryData = GetData(HttpUtility.UrlDecode(query)).ToArray();

                IncludeResourceCSS(stb, "metricsgraphics.css");
                IncludeResourceScript(stb, "d3.min.js");
                IncludeResourceScript(stb, "metricsgraphics.min.js");
                stb.AppendLine("<table align=\"center\" ");
                stb.AppendLine("<tr><td id='chartGraph2' align=\"center\"></td>");
                stb.AppendLine("<tr><td id='legend' align=\"center\"></td>");
                stb.AppendLine("</table>");

                stb.AppendLine("<script>");
                stb.AppendLine(@"function chartData() {");

                List<string> legands = new List<string>();
                if (queryData.Length > 0)
                {
                    for (var i = 1; i < queryData[0].Columns.Count; i++)
                    {
                        var column = queryData[0].Columns[i];
                        legands.Add(Invariant($"'{FirstCharToUpper(column)}'"));
                    }

                    List<StringBuilder> dataStrings = new List<StringBuilder>();
                    foreach (var row in queryData[0].Values)
                    {
                        long jsMilliseconds = 0;
                        for (int i = 0; i < row.Count; i++)
                        {
                            object column = row[i];

                            if (i == 0)
                            {
                                var timePoint = Convert.ToInt64(column, CultureInfo.InvariantCulture);
                                jsMilliseconds = DateTimeOffset.FromUnixTimeSeconds(timePoint).ToLocalTime().ToUnixTimeMilliseconds();
                            }
                            else
                            {
                                if (column != null)
                                {
                                    if (dataStrings.Count < (i))
                                    {
                                        dataStrings.Add(new StringBuilder());
                                    }
                                    dataStrings[i - 1].AppendLine(Invariant($"{{ date: new Date({jsMilliseconds}),value: {column}}},"));
                                }
                            }
                        }
                    }

                    stb.AppendLine("return [");
                    foreach (var dataString in dataStrings)
                    {
                        stb.AppendLine("[");
                        stb.Append(dataString.ToString());
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

        private string BuildHistoryPage(NameValueCollection parts, DevicePersistenceData data)
        {
            StringBuilder stb = new StringBuilder();
            IncludeDataTableFiles(stb);
            IncludeResourceScript(stb, "iframeSizer.min.js");

            HSHelper hsHelper = new HSHelper(HS);

            string header = Invariant($"History - {hsHelper.GetName(data.DeviceRefId)}");

            stb.Append(PageBuilderAndMenu.clsPageBuilder.FormStart("ftmDeviceHistory", "IdHistory", "Post"));
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
            stb.Append(Invariant($"<tr height='10'><td>{HtmlTextBox(RecordId, data.Id.ToString(), @type: "hidden")}</td></tr>"));
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
            stb.Append(PageBuilderAndMenu.clsPageBuilder.FormEnd());

            return stb.ToString();
        }

        private void BuildQueryTableIFrame(StringBuilder stb, string finalQuery, int tableSize = 10)
        {
            string iFrameUrl = BuildTableUri(finalQuery, tableSize);
            stb.Append(@"<style>iframe{width: 1px;min-width: 100%;border: none; width: 100%; height: 600px}</style>");
            stb.Append(Invariant($"<iframe id=\"tableFrame\" src=\"{iFrameUrl}\" scrolling=\"no\"></iframe>"));
            stb.Append(Invariant($"<script>iFrameResize({{log:false}}, '#{TableFrameId}')</script>"));
        }

        private void BuildQueryChartIFrame(StringBuilder stb, string finalQuery, string title = "")
        {
            string iFrameUrl = BuildChartUri(finalQuery, title);
            stb.Append(@"<style>iframe{width: 1px;min-width: 100%;border: none; width: 100%; height: 600px}</style>");
            stb.Append(Invariant($"<iframe id=\"tableFrame\" src=\"{iFrameUrl}\" scrolling=\"no\"></iframe>"));
            stb.Append(Invariant($"<script>iFrameResize({{log:false}}, '#{TableFrameId}')</script>"));
        }

        private void BuildTable(string query, StringBuilder stb, int pageLength)
        {
            try
            {
                var culture = CultureInfo.CurrentUICulture;
                var queryData = GetData(query).ToArray();

                if (queryData.Length > 0)
                {
                    int columns = queryData[0].Columns.Count;

                    stb.Append("<table id=\"results\" class=\"cell-border compact\" style=\"width:100%\">");
                    stb.Append(@"<thead><tr>");
                    foreach (var column in queryData[0].Columns)
                    {
                        stb.Append(Invariant($"<th>{ HtmlEncode(FirstCharToUpper(column))}</th>"));
                    }
                    stb.Append(@"</tr></thead>");
                    stb.Append(@"<tbody>");

                    DateTimeOffset today = DateTimeOffset.Now.Date;
                    foreach (var row in queryData[0].Values)
                    {
                        stb.Append(@"<tr>");
                        for (int i = 0; i < row.Count; i++)
                        {
                            object column = row[i];
                            string value = string.Empty;
                            string sortValue = null;

                            if (i == 0)
                            {
                                var timePoint = Convert.ToInt64(column, CultureInfo.InvariantCulture);
                                sortValue = column.ToString();
                                value = ConvertInfluxDBDateTimeToString(today, culture, timePoint);
                            }
                            else
                            {
                                switch (column)
                                {
                                    case double doubleValue:
                                        value = Math.Round(doubleValue, 3, MidpointRounding.AwayFromZero).ToString("G", culture);
                                        break;

                                    case float floatValue:
                                        value = Math.Round(floatValue, 3, MidpointRounding.AwayFromZero).ToString("G", culture);
                                        break;

                                    case null:
                                        break;

                                    default:
                                        value = Convert.ToString(column, culture);
                                        break;
                                }
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

            int size;
            if (!int.TryParse(HttpUtility.UrlDecode(parts[TableSizeId] ?? string.Empty), out size))
            {
                size = 25;
            }

            IncludeDataTableFiles(stb);
            IncludeResourceScript(stb, "iframeResizer.contentWindow.min.js");

            BuildTable(HttpUtility.UrlDecode(query), stb, size);
            return stb.ToString();
        }

        private IEnumerable<Serie> GetData(string query)
        {
            var loginInformation = pluginConfig.DBLoginInformation;
            return InfluxDBHelper.ExecuteInfluxDBQuery(query, loginInformation).Result;
        }

        private IDictionary<string, FormattableString> GetDefaultValueQueries(DevicePersistenceData data)
        {
            List<string> fields = GetFields(data);

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

        private string HistoryBackButton()
        {
            var b = new clsJQuery.jqButton("Back", "Back", PageName, false)
            {
                id = NameToIdWithPrefix("Back"),
                url = Invariant($"/{pageUrl}?{TabId}=1"),
            };

            return b.Build();
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
        private const string QueryPartId = "query";
        private const string QueryTestDivId = "querytestdivid";
        private const string QueryTestId = "querytextid";
        private const string TableFrameId = "tableFrame";
        private const string TableSizeId = "rows";
        private const string TitlePartId = "title";
    }
}