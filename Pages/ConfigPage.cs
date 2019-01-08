using HomeSeerAPI;
using InfluxData.Net.Common.Enums;
using InfluxData.Net.InfluxDb;
using NullGuard;
using Scheduler;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Web;
using Hspi.Utils;
using static System.FormattableString;

namespace Hspi.Pages
{
    /// <summary>
    /// Helper class to generate configuration page for plugin
    /// </summary>
    /// <seealso cref="Scheduler.PageBuilderAndMenu.clsPageBuilder" />
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal partial class ConfigPage : PageHelper
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigPage" /> class.
        /// </summary>
        /// <param name="HS">The hs.</param>
        /// <param name="pluginConfig">The plugin configuration.</param>
        public ConfigPage(IHSApplication HS, PluginConfig pluginConfig) : base(HS, pluginConfig, Name)
        {
        }

        /// <summary>
        /// Gets the name of the web page.
        /// </summary>
        public static string Name => Invariant($"{PlugInData.PlugInName} Configuration").Replace(' ', '_');

        public static string BuildUri(string path, NameValueCollection query)
        {
            var collection = HttpUtility.ParseQueryString(string.Empty);

            foreach (var key in query.Cast<string>().Where(key => !string.IsNullOrEmpty(query[key])))
            {
                collection[HttpUtility.UrlEncode(key)] = HttpUtility.UrlEncode(query[key]);
            }

            var builder = new UriBuilder()
            {
                Path = HttpUtility.UrlPathEncode(path),
                Query = collection.ToString()
            };
            return builder.Uri.PathAndQuery;
        }

        /// <summary>
        /// Get the web page string for the configuration page.
        /// </summary>
        /// <returns>
        /// System.String.
        /// </returns>
        public string GetWebPage(string queryString)
        {
            try
            {
                reset();
                this.UsesJqAll = false;

                NameValueCollection parts = HttpUtility.ParseQueryString(queryString);

                string pageType = parts[PageTypeId];
                StringBuilder stb = new StringBuilder();

                switch (pageType)
                {
                    case EditDevicePageType:
                        {
                            stb.Append(HS.GetPageHeader(Name, "Configuration", string.Empty, string.Empty, false, false));
                            stb.Append(PageBuilderAndMenu.clsPageBuilder.DivStart("pluginpage", string.Empty));
                            pluginConfig.DevicePersistenceData.TryGetValue(parts[RecordId], out var data);
                            stb.Append(BuildAddNewPersistenceWebPageBody(data));
                            stb.Append(PageBuilderAndMenu.clsPageBuilder.DivEnd());
                            AddBody(stb.ToString());
                            AddFooter(HS.GetPageFooter());
                        }
                        break;

                    case EditDeviceImportPageType:
                        {
                            stb.Append(HS.GetPageHeader(Name, "Configuration", string.Empty, string.Empty, false, false));
                            stb.Append(PageBuilderAndMenu.clsPageBuilder.DivStart("pluginpage", string.Empty));
                            pluginConfig.ImportDevicesData.TryGetValue(parts[RecordId], out var data);
                            stb.Append(BuildAddNewDeviceImportWebPageBody(data));
                            stb.Append(PageBuilderAndMenu.clsPageBuilder.DivEnd());
                            AddBody(stb.ToString());
                            AddFooter(HS.GetPageFooter());
                        }
                        break;

                    case DeviceDataTablePageType:
                        {
                            string pageDivId = "tablepage";
                            CreatePageWithAjaxLoad(parts, stb, pageDivId, BuildTablePage);
                        }
                        break;

                    case DeviceChartTablePageType:
                        {
                            string pageDivId = "chartPage";
                            CreatePageWithAjaxLoad(parts, stb, pageDivId, BuildChartsPage);
                        }
                        break;

                    case DeviceStatsPageType:
                        {
                            string pageDivId = "statspage";
                            CreatePageWithAjaxLoad(parts, stb, pageDivId, BuildStatsPage);
                        }
                        break;

                    case HistoryDevicePageType:
                        {
                            stb.Append(HS.GetPageHeader(Name, "Configuration", string.Empty, string.Empty, false, false));
                            stb.Append(PageBuilderAndMenu.clsPageBuilder.DivStart("pluginpage", string.Empty));
                            if (pluginConfig.DevicePersistenceData.TryGetValue(parts[RecordId], out var data))
                            {
                                stb.Append(BuildHistoryPage(parts, data));
                            }
                            else
                            {
                                stb.Append(BuildDefaultWebPageBody(parts));
                            }
                            stb.Append(PageBuilderAndMenu.clsPageBuilder.DivEnd());
                            AddBody(stb.ToString());
                            AddFooter(HS.GetPageFooter());
                        }
                        break;

                    default:
                    case null:
                        {
                            stb.Append(HS.GetPageHeader(Name, "Configuration", string.Empty, string.Empty, false, false));
                            stb.Append(PageBuilderAndMenu.clsPageBuilder.DivStart("pluginpage", string.Empty));
                            stb.Append(BuildDefaultWebPageBody(parts));
                            stb.Append(PageBuilderAndMenu.clsPageBuilder.DivEnd());
                            AddBody(stb.ToString());
                            AddFooter(HS.GetPageFooter());
                            break;
                        }
                }

                suppressDefaultFooter = true;
                return BuildPage();
            }
            catch (Exception)
            {
                return "error";
            }
        }

        private void CreatePageWithAjaxLoad(NameValueCollection parts, StringBuilder stb,
                                            string pageDivId, Func<NameValueCollection, string> func)
        {
            stb.Append(DivStart(pageDivId, string.Empty));

            if (!string.IsNullOrWhiteSpace(parts[RealLoadId]))
            {
                stb.Append(func(parts));
            }
            else
            {
                stb.Append("<div id=\"loading\">Please wait ...</div>");

                stb.Append(@"<script>
                                           var $loading = $('#loading').hide();
                                           $(document)
                                             .ajaxStart(function () {$loading.show();})
                                             .ajaxStop(function () {$loading.hide();});");
                stb.AppendLine("$( document ).ready(function() {");

                var newParts = new NameValueCollection();
                foreach (var key in parts.AllKeys)
                {
                    newParts.Add(key, HttpUtility.UrlDecode(parts[key]));
                }

                newParts.Add(RealLoadId, "1");
                stb.AppendFormat("$(\"#{0}\").load('{1}');", pageDivId, BuildUri(pageUrl, newParts));
                stb.AppendLine("});");
                stb.AppendLine("</script>");
            }
            stb.Append(DivEnd());

            AddBody(stb.ToString());
        }

        /// <summary>
        /// The user has selected a control on the configuration web page.
        /// The post data is provided to determine the control that initiated the post and the state of the other controls.
        /// </summary>
        /// <param name="data">The post data.</param>
        /// <param name="user">The name of logged in user.</param>
        /// <param name="userRights">The rights of the logged in user.</param>
        /// <returns>Any serialized data that needs to be passed back to the web page, generated by the clsPageBuilder class.</returns>
        public string PostBackProc(string data, [AllowNull]string user, int userRights)
        {
            NameValueCollection parts = HttpUtility.ParseQueryString(data);

            string form = parts["id"];

            if (form == NameToIdWithPrefix(SettingSaveButtonName))
            {
                HandleSaveDBSettingPostBack(parts);
            }
            else if ((form == NameToIdWithPrefix(EditPersistenceSave)) ||
                    (form == NameToIdWithPrefix(FillDefaultValuesButtonName)) ||
                    (form == NameToIdWithPrefix(EditPersistenceCancel)) ||
                    (form == NameToIdWithPrefix(DeletePersistenceSave)))
            {
                HandleSavingPersistencePostBack(parts, form);
            }
            else if ((form == NameToIdWithPrefix(DeleteDeviceImport)) ||
                     (form == NameToIdWithPrefix(CancelDeviceImport)) ||
                     (form == NameToIdWithPrefix(SaveDeviceImport)))
            {
                HandleSavingDeviceImportPostBack(parts, form);
            }
            else if ((form == NameToIdWithPrefix(HistoryQueryTypeId)) ||
                     (form == NameToIdWithPrefix(HistoryRunQueryButtonName)) ||
                     (form == NameToIdWithPrefix(HistoryShowChartButtonName)))
            {
                HandleHistoryPagePostBack(parts, form);
            }

            return base.postBackProc(Name, data, user, userRights);
        }

        protected string FormDropDownChosen(string name, IDictionary<int, string> options, int selected)
        {
            string id = NameToIdWithPrefix(name);
            StringBuilder stb = new StringBuilder();
            stb.AppendLine(Invariant($"<select id=\"{id}\" form_id='{id}' name='{name}'>"));
            foreach (var option in options)
            {
                stb.AppendLine(Invariant($" <option value=\"{option.Key}\" {(option.Key == selected ? "selected" : string.Empty)}>{option.Value}</option>"));
            }
            stb.AppendLine(Invariant($"</select>"));

            stb.AppendLine("<script type='text/javascript'>");
            stb.AppendLine("$(document).ready(function() {");
            stb.AppendLine(Invariant($"     $(\"#{id}\").chosen();"));
            stb.AppendLine("});");
            stb.AppendLine("</script>");
            return stb.ToString();
        }

        private string BuildDBSettingTab()
        {
            var dbConfig = pluginConfig.DBLoginInformation;

            StringBuilder stb = new StringBuilder();
            stb.Append(FormStart("ftmSettings", "IdSettings", "Post"));

            stb.Append(@"<br>");
            stb.Append(@"<div>");
            stb.Append(@"<table class='full_width_table'>");
            stb.Append("<tr height='5'><td style='width:35%'></td><td style='width:65%'></td></tr>");
            stb.Append(Invariant($"<tr><td class='tablecell'>Url:</td><td class='tablecell' style='width: 50px'>"));
            stb.Append(HtmlTextBox(DBUriKey, dbConfig.DBUri != null ? dbConfig.DBUri.ToString() : string.Empty, type: "url"));
            stb.Append(Invariant($"</td></tr>"));
            stb.Append(Invariant($"<tr><td class='tablecell'>User:</td><td class='tablecell'>{HtmlTextBox(UserKey, dbConfig.User)} </td></tr>"));
            stb.Append(Invariant($"<tr><td class='tablecell'>Password:</td><td class='tablecell'>{HtmlTextBox(PasswordKey, dbConfig.Password, 25, "password")}</td></tr>"));
            stb.Append(Invariant($"<tr><td class='tablecell'>Database:</td><td colspan=2 class='tablecell'>{HtmlTextBox(DBKey, dbConfig.DB)}</td></tr>"));
            stb.Append(Invariant($"<tr><td class='tablecell'>Retention Policy:</td><td colspan=2 class='tablecell'>{HtmlTextBox(RetentionKey, dbConfig.Retention)}</td></tr>"));
            stb.Append(Invariant($"<tr><td class='tablecell'>Debug Logging Enabled:</td><td class='tablecell'>{FormCheckBox(DebugLoggingId, string.Empty, this.pluginConfig.DebugLogging)}</td ></tr>"));
            stb.Append(Invariant($"<tr><td colspan=2><div id='{ErrorDivId}' style='color:Red'></div></td></tr>"));
            stb.Append(Invariant($"<tr><td colspan=2>{FormButton(SettingSaveButtonName, "Save", "Save Settings")}</td></tr>"));
            stb.Append("<tr height='5'><td colspan=2></td></tr>");
            stb.Append("</table>");
            stb.Append("</div>");
            stb.Append(FormEnd());

            return stb.ToString();
        }

        /// <summary>
        /// Builds the web page body for the configuration page.
        /// The page has separate forms so that only the data in the appropriate form is returned when a button is pressed.
        /// </summary>
        private string BuildDefaultWebPageBody(NameValueCollection parts)
        {
            this.UsesJqTabs = true;
            string tab = parts[TabId] ?? "0";
            int defaultTab = 0;
            int.TryParse(tab, out defaultTab);

            int i = 0;
            StringBuilder stb = new StringBuilder();
            IncludeDataTableFiles(stb);

            var tabs = new clsJQuery.jqTabs("tab1id", DeviceUtiltyPageName);
            var tab1 = new clsJQuery.Tab();
            tab1.tabTitle = "DB Settings";
            tab1.tabDIVID = Invariant($"tabs{i++}");
            tab1.tabContent = BuildDBSettingTab();
            tabs.tabs.Add(tab1);

            var tab2 = new clsJQuery.Tab();
            tab2.tabTitle = "Persistence";
            tab2.tabDIVID = Invariant($"tabs{i++}");
            tab2.tabContent = BuildPersistenceTab(parts);
            tabs.tabs.Add(tab2);

            var tab3 = new clsJQuery.Tab();
            tab3.tabTitle = "Devices Import";
            tab3.tabDIVID = Invariant($"tabs{i++}");
            tab3.tabContent = BuildImportDevicesTab(parts);
            tabs.tabs.Add(tab3);

            switch (defaultTab)
            {
                case 0:
                    tabs.defaultTab = tab1.tabDIVID;
                    break;

                case 1:
                    tabs.defaultTab = tab2.tabDIVID;
                    break;

                case 2:
                    tabs.defaultTab = tab3.tabDIVID;
                    break;
            }

            tabs.postOnTabClick = false;
            stb.Append(tabs.Build());

            return stb.ToString();
        }

        private void HandleSaveDBSettingPostBack(NameValueCollection parts)
        {
            StringBuilder results = new StringBuilder();

            // Validate

            System.Uri dbUri;

            if (!System.Uri.TryCreate(parts[DBUriKey], UriKind.Absolute, out dbUri))
            {
                results.AppendLine("Url is not Valid.<br>");
            }

            string database = parts[DBKey];

            if (string.IsNullOrWhiteSpace(database))
            {
                results.AppendLine("Database is not Valid.<br>");
            }

            string username = parts[UserKey];
            string password = parts[PasswordKey];
            string retention = parts[RetentionKey];

            try
            {
                var influxDbClient = new InfluxDbClient(dbUri.ToString(), username, password, InfluxDbVersion.v_1_3);

                var databases = influxDbClient.Database.GetDatabasesAsync().ResultForSync();

                var selectedDb = databases.Where((db) => { return db.Name == database; }).FirstOrDefault();
                if (selectedDb == null)
                {
                    results.AppendLine("Database not found on server.<br>");
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(retention))
                    {
                        var retentionPolcies = influxDbClient.Retention.GetRetentionPoliciesAsync(selectedDb.Name).ResultForSync();
                        if (!retentionPolcies.Any(r => r.Name == retention))
                        {
                            results.AppendLine("Retention policy not found for database.<br>");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                results.AppendLine(Invariant($"Failed to connect to InfluxDB with {ex.GetFullMessage()}"));
            }

            if (results.Length > 0)
            {
                this.divToUpdate.Add(ErrorDivId, results.ToString());
            }
            else
            {
                this.divToUpdate.Add(ErrorDivId, string.Empty);
                var dbConfig = new InfluxDBLoginInformation(dbUri,
                                                            PluginConfig.CheckEmptyOrWhitespace(username),
                                                            PluginConfig.CheckEmptyOrWhitespace(password),
                                                            PluginConfig.CheckEmptyOrWhitespace(parts[DBKey]),
                                                            PluginConfig.CheckEmptyOrWhitespace(retention));
                this.pluginConfig.DBLoginInformation = dbConfig;
                this.pluginConfig.DebugLogging = parts[DebugLoggingId] == "checked";
                this.pluginConfig.FireConfigChanged();
            }
        }

        private const string DBKey = "DBId";
        private const string DBUriKey = "DBUriId";
        private const string DebugLoggingId = "DebugLoggingId";
        private const string ErrorDivId = "message_id";
        private const string IdPrefix = "id_";
        private const string PasswordKey = "PasswordId";
        private const string RetentionKey = "RetentionId";
        private const string SaveErrorDivId = "SaveErrorDivId";
        private const string SettingSaveButtonName = "SettingSave";
        private const string TabId = "tab";
        private const string RealLoadId = "realload";
        private const string UserKey = "UserId";
        private static readonly string pageUrl = HttpUtility.UrlEncode(Name);
    }
}