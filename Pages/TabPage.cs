using HomeSeerAPI;
using Hspi.DeviceData;
using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web;
using static System.FormattableString;

namespace Hspi.Pages
{
    internal partial class ConfigPage : PageHelper
    {
        public enum IFrameDuration
        {
            [Description("1 hour")]
            D1h,

            [Description("6 hour")]
            D6h,

            [Description("12 hours")]
            D12h,

            [Description("24 hours")]
            D24h,

            [Description("7 days")]
            D7d,

            [Description("30 days")]
            D30d
        };

        [Flags]
        public enum IFrameType
        {
            [Description("Table")]
            TableHistory,

            [Description("Charts")]
            ChartHistory,

            [Description("Max/Min Stats")]
            MaxMinStats,
        }

        public Enums.ConfigDevicePostReturn GetDeviceHistoryPost(IAppCallbackAPI callback, int refId, string queryData)
        {
            var dataKeyPair = pluginConfig.DevicePersistenceData.SingleOrDefault(x => x.Value.DeviceRefId == refId);
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
                        Enum.TryParse(parts[IFrameDurationId], true, out IFrameDuration duration))
                    {
                        callback.ConfigDivToUpdateAdd(resultsDivPartId, GetQueryResultFrame(data, frameType, duration));
                    }
                }
            }
            return Enums.ConfigDevicePostReturn.DoneAndCancelAndStay;
        }

        public string GetDeviceHistoryTab(int refId)
        {
            const IFrameType DefaultFrameType = IFrameType.TableHistory;
            const IFrameDuration DefaultDuration = IFrameDuration.D12h;

            var dataKeyPair = pluginConfig.DevicePersistenceData.SingleOrDefault(x => x.Value.DeviceRefId == refId);
            var data = dataKeyPair.Value;
            if (data != null)
            {
                StringBuilder stb = new StringBuilder();
                IncludeDataTableFiles(stb);
                IncludeResourceScript(stb, "iframeSizer.min.js");

                stb.Append(@"<table style='width:100%;border-spacing:0px;'");
                stb.Append("<tr height='5'><td></td></tr>");
                stb.Append("<tr><td>");
                stb.Append("Type:");

                NameValueCollection iframeType = new NameValueCollection();
                AddEnumValue(iframeType, IFrameType.TableHistory);
                if (!string.IsNullOrWhiteSpace(data.Field))
                {
                    AddEnumValue(iframeType, IFrameType.ChartHistory);
                    AddEnumValue(iframeType, IFrameType.MaxMinStats);
                }

                stb.Append(FormDropDown(IFrameTypeId, iframeType, DefaultFrameType.ToString(),
                                        150, string.Empty, true, DeviceUtiltyPageName));

                stb.Append("&nbsp;Duration:");
                NameValueCollection duration = CreateNameValueCreation<IFrameDuration>();
                stb.Append(FormDropDown(IFrameDurationId, duration, DefaultDuration.ToString(),
                                        100, string.Empty, true, DeviceUtiltyPageName));

                stb.Append("</td></tr>");
                stb.Append("<tr height='5'><td></td></tr>");

                //stb.Append(Invariant($"<tr><td class='tableheader'>History</td></tr>"));
                stb.Append("<tr><td class='tablecell'>");
                stb.Append(DivStart(resultsDivPartId, string.Empty));
                stb.Append(GetQueryResultFrame(data, DefaultFrameType, DefaultDuration));
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

        private static string BuildTableUri(string finalQuery, int tableSize)
        {
            return BuildUri(pageUrl, new NameValueCollection()
            {
                { PageTypeId, DeviceDataTablePageType},
                { QueryPartId, finalQuery },
                { TableSizeId, Invariant($"{tableSize}") },
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

        private string GetDeviceHistoryTabQuery(DevicePersistenceData data, IFrameDuration duration)
        {
            HSHelper hSHelper = new HSHelper(HS);
            string deviceName = hSHelper.GetName(data.DeviceRefId);
            var fields = string.Join(",", GetFields(data));

            StringBuilder stb = new StringBuilder();
            stb.Append("SELECT ");
            stb.Append(fields);
            stb.Append("AS \"");
            stb.Append(deviceName);
            stb.Append("\" from \"");
            stb.Append(data.Measurement);
            stb.Append("\" WHERE ");
            stb.Append(PluginConfig.DeviceRefIdTag);
            stb.Append("='");
            stb.AppendFormat(CultureInfo.InvariantCulture, "{0}", data.DeviceRefId);
            stb.Append("'");

            switch (duration)
            {
                case IFrameDuration.D1h:
                    stb.Append(" AND time > now() - 1h ORDER BY time DESC");
                    break;

                case IFrameDuration.D6h:
                    stb.Append(" AND time > now() - 6h ORDER BY time DESC");
                    break;

                case IFrameDuration.D12h:
                    stb.Append(" AND time > now() - 12h ORDER BY time DESC");
                    break;

                case IFrameDuration.D24h:
                    stb.Append(" AND time > now() - 24h ORDER BY time DESC");
                    break;

                case IFrameDuration.D7d:
                    stb.Append(" AND time > now() - 7d ORDER BY time DESC");
                    break;

                case IFrameDuration.D30d:
                    stb.Append(" AND time > now() - 30d ORDER BY time DESC");
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(duration));
            }

            return stb.ToString();
        }

        private string GetMaxMinStatsQuery(DevicePersistenceData data, IFrameDuration duration)
        {
            StringBuilder stb = new StringBuilder();
            stb.Append("SELECT");
            stb.AppendFormat(CultureInfo.InvariantCulture, " MAX(\"{0}\") as Max", data.Field);
            stb.AppendFormat(CultureInfo.InvariantCulture, ",MIN(\"{0}\") as Min", data.Field);
            stb.Append(" from \"");
            stb.Append(data.Measurement);
            stb.Append("\" WHERE ");
            stb.Append(PluginConfig.DeviceRefIdTag);
            stb.Append("='");
            stb.AppendFormat(CultureInfo.InvariantCulture, "{0}", data.DeviceRefId);
            stb.Append("'");

            switch (duration)
            {
                case IFrameDuration.D1h:
                    stb.Append(" AND time > now() - 1h ORDER BY time DESC");
                    break;

                case IFrameDuration.D6h:
                    stb.Append(" AND time > now() - 6h ORDER BY time DESC");
                    break;

                case IFrameDuration.D12h:
                    stb.Append(" AND time > now() - 12h ORDER BY time DESC");
                    break;

                case IFrameDuration.D24h:
                    stb.Append(" AND time > now() - 24h ORDER BY time DESC");
                    break;

                case IFrameDuration.D7d:
                    stb.Append(" AND time > now() - 7d ORDER BY time DESC");
                    break;

                case IFrameDuration.D30d:
                    stb.Append(" AND time > now() - 30d ORDER BY time DESC");
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(duration));
            }

            return stb.ToString();
        }

        private string GetQueryResultFrame(DevicePersistenceData data, IFrameType frameType, IFrameDuration duration)
        {
            StringBuilder stb = new StringBuilder();
            string iFrameUrl = null;
            switch (frameType)
            {
                case IFrameType.TableHistory:
                case IFrameType.ChartHistory:
                    var query = GetDeviceHistoryTabQuery(data, duration);
                    iFrameUrl = (frameType == IFrameType.TableHistory) ?
                                           BuildTableUri(query, 10) : BuildChartUri(query, string.Empty);
                    break;

                case IFrameType.MaxMinStats:
                    var statsQuery = GetMaxMinStatsQuery(data, duration);
                    iFrameUrl = BuildStatsUri(statsQuery);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(frameType));
            }
            stb.Append(@"<style>iframe{width: 1px;min-width: 100%;border: none; width: 100%; height: 600px}</style>");
            stb.Append(Invariant($"<iframe id=\"tableFrame\" src=\"{iFrameUrl}\" scrolling=\"no\"></iframe>"));
            stb.Append(Invariant($"<script>iFrameResize({{log:false}}, '#{TableFrameId}')</script>"));

            return stb.ToString();
        }

        private const string DeviceUtiltyPageName = "deviceutility";
        private const string IFrameDurationId = "duration";
        private const string IFrameTypeId = "iframeType";
        private const string DoneButtonId = "DoneButtonId";
        private const string resultsDivPartId = "resultsDivPartId";
    }
}