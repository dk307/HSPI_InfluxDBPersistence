using HomeSeerAPI;
using Hspi.DeviceData;
using System;
using System.Collections.Specialized;
using System.ComponentModel;
using Hspi.Utils;
using System.Linq;
using System.Text;
using System.Web;
using static System.FormattableString;

namespace Hspi.Pages
{
    internal partial class ConfigPage : PageHelper
    {
        [Flags]
        public enum IFrameType
        {
            [Description("Table")]
            TableHistory,

            [Description("Charts")]
            ChartHistory,

            [Description("Average Statistics")]
            AverageStats,
        }

        [Flags]
        public enum IFrameGrouping
        {
            [Description("Auto")]
            Auto,

            [Description("1 second")]
            I1s,

            [Description("5 seconds")]
            I5s,

            [Description("15 seconds")]
            I15s,

            [Description("1 minute")]
            I1m,

            [Description("5 minute")]
            I5m,

            [Description("30 minute")]
            I30m,

            [Description("1 hour")]
            I1h,

            [Description("12 hours")]
            I12h,

            [Description("1 day")]
            I1d,
        }

        public Enums.ConfigDevicePostReturn GetDeviceHistoryPost(IAppCallbackAPI callback, int refId, string queryData)
        {
            var dataKeyPair = pluginConfig.DevicePersistenceData.FirstOrDefault(x => x.Value.DeviceRefId == refId);
            var data = dataKeyPair.Value;

            if (data != null)
            {
                NameValueCollection parts = HttpUtility.ParseQueryString(queryData ?? string.Empty);

                string id = parts["id"];

                if (id == NameToIdWithPrefix(DoneButtonId))
                {
                    return Enums.ConfigDevicePostReturn.DoneAndSave;
                }
                else
                {
                    if (Enum.TryParse(parts[IFrameTypeId], true, out IFrameType frameType) &&
                        Enum.TryParse(parts[IFrameDurationId], true, out QueryDuration duration))
                    {
                        Enum.TryParse(parts[IFrameGroupingId], true, out IFrameGrouping grouping);
                        callback.ConfigDivToUpdateAdd(resultsDivPartId, GetQueryResultFrame(data, frameType, duration, grouping));
                    }
                }
            }
            return Enums.ConfigDevicePostReturn.DoneAndCancelAndStay;
        }

        public string GetDeviceHistoryTab(int refId)
        {
            var dataKeyPair = pluginConfig.DevicePersistenceData.FirstOrDefault(x => x.Value.DeviceRefId == refId);
            var data = dataKeyPair.Value;

            if (data != null)
            {
                bool hasNumericData = !string.IsNullOrWhiteSpace(data.Field);
                IFrameType DefaultFrameType = hasNumericData ? IFrameType.ChartHistory : IFrameType.TableHistory;
                QueryDuration DefaultDuration = QueryDuration.D6h;
                IFrameGrouping DefaultGrouping = IFrameGrouping.Auto;
                StringBuilder stb = new StringBuilder();
                IncludeDataTableFiles(stb);
                IncludeResourceScript(stb, "iframeSizer.min.js");

                stb.Append(@"<table style='width:100%;border-spacing:0px;'");
                stb.Append("<tr height='5'><td></td></tr>");
                stb.Append("<tr><td>");
                stb.Append("Type:");

                NameValueCollection iframeType = new NameValueCollection();
                AddEnumValue(iframeType, IFrameType.TableHistory);
                if (hasNumericData)
                {
                    AddEnumValue(iframeType, IFrameType.ChartHistory);
                    AddEnumValue(iframeType, IFrameType.AverageStats);
                }

                stb.Append(FormDropDown(IFrameTypeId, iframeType, DefaultFrameType.ToString(),
                                        150, string.Empty, true, DeviceUtiltyPageName));

                stb.Append("&nbsp;Duration:");
                NameValueCollection duration = CreateNameValueCreation<QueryDuration>();
                stb.Append(FormDropDown(IFrameDurationId, duration, DefaultDuration.ToString(),
                                        100, string.Empty, true, DeviceUtiltyPageName));

                stb.Append("&nbsp;Grouping:");
                NameValueCollection grouping = CreateNameValueCreation<IFrameGrouping>();
                stb.Append(FormDropDown(IFrameGroupingId, grouping, DefaultGrouping.ToString(),
                                        100, string.Empty, true, DeviceUtiltyPageName, hasNumericData));

                stb.Append("</td></tr>");
                stb.Append("<tr height='5'><td></td></tr>");

                stb.Append("<tr><td class='tablecell'>");
                stb.Append(DivStart(resultsDivPartId, string.Empty));
                stb.Append(GetQueryResultFrame(data, DefaultFrameType, DefaultDuration, DefaultGrouping));
                stb.Append(DivEnd());

                stb.Append("</td></tr>");
                stb.Append("<tr height='5'><td></td></tr>");
                stb.Append("<tr><td>");
                stb.Append(PageTypeButton(Invariant($"Edit{data.Id}"), "Edit", EditDevicePageType, id: data.Id));
                stb.Append("&nbsp;");
                stb.Append(PageTypeButton(Invariant($"Queries{data.Id}"), "More Queries", HistoryDevicePageType, id: data.Id));
                stb.Append("&nbsp;");
                stb.Append(FormButton(DoneButtonId, "Done", string.Empty, DeviceUtiltyPageName));
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

        private static string BuildChartUri(string finalQuery, string title)
        {
            return BuildUri(pageUrl, new NameValueCollection()
            {
                { PageTypeId, DeviceChartTablePageType},
                { QueryPartId, finalQuery },
                { TitlePartId, title },
            });
        }

        private static string BuildStatsUri(string finalQuery)
        {
            return BuildUri(pageUrl, new NameValueCollection()
            {
                { PageTypeId, DeviceStatsPageType},
                { QueryPartId, finalQuery },
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

        private string GetQueryResultFrame(DevicePersistenceData data, IFrameType frameType,
                                           QueryDuration duration, IFrameGrouping grouping)
        {
            StringBuilder stb = new StringBuilder();
            string iFrameUrl = null;

            HSHelper hSHelper = new HSHelper(HS);
            string deviceName = hSHelper.GetName(data.DeviceRefId);

            switch (frameType)
            {
                case IFrameType.TableHistory:
                    var query = InfluxDbQueryBuilder.GetDeviceHistoryTabQuery(data, deviceName, duration);
                    iFrameUrl = BuildTableUri(query, 10);
                    break;

                case IFrameType.ChartHistory:
                    var chartQuery = InfluxDbQueryBuilder.GetGroupedDeviceHistoryTabQuery(data, deviceName, duration,
                                                                              pluginConfig.DBLoginInformation,
                                                                              GetTimeSpan(grouping)).ResultForSync();
                    iFrameUrl = BuildChartUri(chartQuery, string.Empty);
                    break;

                case IFrameType.AverageStats:
                    var statsQuery = InfluxDbQueryBuilder.GetStatsQuery(data, duration,
                                                                        pluginConfig.DBLoginInformation,
                                                                        GetTimeSpan(grouping)).ResultForSync();
                    iFrameUrl = BuildStatsUri(statsQuery);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(frameType));
            }
            stb.Append(@"<style>iframe{width: 1px;min-width: 100%;border: none; width: 100%; height: 475px}</style>");
            stb.Append(Invariant($"<iframe id=\"tableFrame\" src=\"{iFrameUrl}\" scrolling=\"no\"></iframe>"));
            stb.Append(Invariant($"<script>iFrameResize({{log:false}}, '#{TableFrameId}')</script>"));

            return stb.ToString();
        }

        private static TimeSpan? GetTimeSpan(IFrameGrouping grouping)
        {
            switch (grouping)
            {
                case IFrameGrouping.Auto:
                    return null;

                case IFrameGrouping.I1s:
                    return TimeSpan.FromSeconds(1);

                case IFrameGrouping.I5s:
                    return TimeSpan.FromSeconds(5);

                case IFrameGrouping.I15s:
                    return TimeSpan.FromSeconds(15);

                case IFrameGrouping.I1m:
                    return TimeSpan.FromMinutes(1);

                case IFrameGrouping.I5m:
                    return TimeSpan.FromMinutes(5);

                case IFrameGrouping.I30m:
                    return TimeSpan.FromMinutes(30);

                case IFrameGrouping.I1h:
                    return TimeSpan.FromHours(1);

                case IFrameGrouping.I12h:
                    return TimeSpan.FromHours(12);

                case IFrameGrouping.I1d:
                    return TimeSpan.FromDays(1);

                default:
                    throw new ArgumentOutOfRangeException(nameof(grouping));
            }
        }

        private const string DeviceUtiltyPageName = "deviceutility";
        private const string DoneButtonId = "DoneButtonId";
        private const string IFrameDurationId = "duration";
        private const string IFrameTypeId = "iframeType";
        private const string IFrameGroupingId = "iframeGroup";
        private const string resultsDivPartId = "resultsDivPartId";
    }
}