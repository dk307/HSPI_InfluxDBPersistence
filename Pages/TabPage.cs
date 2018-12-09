using Hspi.DeviceData;
using Scheduler;
using Scheduler.Classes;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;

namespace Hspi
{
    using static System.FormattableString;

    internal partial class ConfigPage : PageBuilderAndMenu.clsPageBuilder
    {
        private string IFrameChangeUrlButton(string name, string iframe, string pageType, string url)
        {
            var button = new clsJQuery.jqButton(name, name, PageName, false)
            {
                id = NameToIdWithPrefix(name),
            };

            button.functionToCallOnClick = Invariant($"$('#{iframe}').prop('src', '{url}')");
            return button.Build();
        }

        private static string BuildChartUri(string finalQuery, string title)
        {
            return BuildUri(pageUrl, new NameValueCollection()
            {
                { PageTypeId, DeviceChartTablePageType},
                { QueryPartId, finalQuery },
                { TitlePartId, title },
            });
        }

        private static string BuildTableUri(string finalQuery, int tableSize)
        {
            return BuildUri(pageUrl, new NameValueCollection()
            {
                { PageTypeId, DeviceDataTablePageType},
                { QueryPartId, finalQuery },
                { TableSizeId, Invariant($"{tableSize}") },
            });
        }

        private IDictionary<string, string> GetDeviceHistoryTabQueries(DevicePersistenceData data)
        {
            var queries = new Dictionary<string, string>();
            HSHelper hSHelper = new HSHelper(HS);
            string deviceName = hSHelper.GetName(data.DeviceRefId);
            var fields = string.Join(",", GetFields(data));
            queries.Add("100 Records",
                        Invariant($"SELECT {fields} AS \"{ deviceName}\" from \"{data.Measurement}\" WHERE {PluginConfig.DeviceRefIdTag}='{data.DeviceRefId}' ORDER BY time DESC LIMIT 100"));

            queries.Add("1h",
                        Invariant($"SELECT {fields} AS \"{ deviceName}\" from \"{data.Measurement}\" WHERE {PluginConfig.DeviceRefIdTag}='{data.DeviceRefId}' AND time > now() - 1h ORDER BY time DESC LIMIT 10000"));

            queries.Add("24h",
                        Invariant($"SELECT {fields} AS \"{ deviceName}\" from \"{data.Measurement}\" WHERE {PluginConfig.DeviceRefIdTag}='{data.DeviceRefId}' AND time > now() - 24h ORDER BY time DESC LIMIT 10000"));

            queries.Add("7d",
                        Invariant($"SELECT {fields} AS \"{ deviceName}\" from \"{data.Measurement}\" WHERE {PluginConfig.DeviceRefIdTag}='{data.DeviceRefId}' AND time > now() - 7d ORDER BY time DESC LIMIT 10000"));

            queries.Add("30d",
                        Invariant($"SELECT {fields} AS \"{ deviceName}\" from \"{data.Measurement}\" WHERE {PluginConfig.DeviceRefIdTag}='{data.DeviceRefId}' AND time > now() - 30d ORDER BY time DESC LIMIT 10000"));

            return queries;
        }

        public string GetDeviceHistoryTab(DeviceClass deviceClass)
        {
            int refId = deviceClass.get_Ref(HS);
            var dataKeyPair = pluginConfig.DevicePersistenceData.SingleOrDefault(x => x.Value.DeviceRefId == refId);
            var data = dataKeyPair.Value;
            if (data != null)
            {
                var queries = GetDeviceHistoryTabQueries(data);

                StringBuilder stb = new StringBuilder();
                IncludeDataTableFiles(stb);
                IncludeResourceScript(stb, "iframeSizer.min.js");

                stb.Append(@"<table style='width:100%;border-spacing:0px;'");
                stb.Append("<tr height='5'><td></td></tr>");
                stb.Append("<tr><td>");
                foreach (var queryPair in queries)
                {
                    stb.Append(IFrameChangeUrlButton(Invariant($"Table - {queryPair.Key}"), TableFrameId, DeviceDataTablePageType, BuildTableUri(queryPair.Value, 10)));
                }
                stb.Append("<br>");
                if (!string.IsNullOrWhiteSpace(data.Field))
                {
                    foreach (var queryPair in queries)
                    {
                        stb.Append(IFrameChangeUrlButton(Invariant($"Chart - {queryPair.Key}"), TableFrameId, DeviceChartTablePageType, BuildChartUri(queryPair.Value, string.Empty)));
                    }
                }

                stb.Append("</td></tr>");
                stb.Append(Invariant($"<tr><td class='tableheader'>History</td></tr>"));
                stb.Append("<tr><td class='tablecell'>");
                BuildQueryTableIFrame(stb, queries.First().Value);
                stb.Append("</td></tr>");
                stb.Append("<tr height='5'><td></td></tr>");
                stb.Append("<tr><td>");
                stb.Append(PageTypeButton(Invariant($"Edit{data.Id}"), "Edit", EditDevicePageType, id: data.Id));
                stb.Append("&nbsp;");
                stb.Append(PageTypeButton(Invariant($"Queries{data.Id}"), "More Queries", HistoryDevicePageType, id: data.Id));
                stb.Append("</td></tr>");
                stb.Append("</table>");

                return stb.ToString();
            }

            return string.Empty;
        }

        public string GetDeviceImportTab(DeviceIdentifier deviceIdentifier)
        {
            foreach (var device in pluginConfig.ImportDevicesData)
            {
                if (device.Key == deviceIdentifier.DeviceId)
                {
                    StringBuilder stb = new StringBuilder();

                    stb.Append(@"<table style='width:100%;border-spacing:0px;'");
                    stb.Append("<tr height='5'><td style='width:25%'></td><td style='width:20%'></td><td style='width:55%'></td></tr>");
                    stb.Append(Invariant($"<tr><td class='tableheader' colspan=3>Import Settings</td></tr>"));
                    stb.Append(Invariant($"<tr><td class='tablecell'>Name:</td><td class='tablecell' colspan=2>{HtmlEncode(device.Value.Name)}</td></tr>"));
                    stb.Append(Invariant($"<tr><td class='tablecell'>Sql:</td><td class='tablecell' colspan=2>{HtmlEncode(device.Value.Sql)}</td></tr>"));
                    stb.Append(Invariant($"<tr><td class='tablecell'>Refresh Interval(seconds):</td><td class='tablecell' colspan=2>{HtmlEncode(device.Value.Interval.TotalSeconds)}</td></tr>"));
                    stb.Append(Invariant($"<tr><td class='tablecell'>Unit:</td><td class='tablecell' colspan=2>{HtmlEncode(device.Value.Unit)}</td></tr>"));
                    stb.Append(Invariant($"</td><td></td></tr>"));
                    stb.Append("<tr height='5'><td colspan=3></td></tr>");
                    stb.Append("<tr><td colspan=3>");
                    stb.Append(PageTypeButton(Invariant($"Edit{device.Value.Id}"), "Edit", EditDeviceImportPageType, id: device.Value.Id));
                    stb.Append("</td></tr>");
                    stb.Append(@" </table>");

                    return stb.ToString();
                }
            }

            return string.Empty;
        }
    }
}