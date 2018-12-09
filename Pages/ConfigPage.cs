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

namespace Hspi
{
    using static System.FormattableString;

    /// <summary>
    /// Helper class to generate configuration page for plugin
    /// </summary>
    /// <seealso cref="Scheduler.PageBuilderAndMenu.clsPageBuilder" />
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal partial class ConfigPage : PageBuilderAndMenu.clsPageBuilder
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigPage" /> class.
        /// </summary>
        /// <param name="HS">The hs.</param>
        /// <param name="pluginConfig">The plugin configuration.</param>
        public ConfigPage(IHSApplication HS, PluginConfig pluginConfig) : base(pageName)
        {
            this.HS = HS;
            this.pluginConfig = pluginConfig;
        }

        /// <summary>
        /// Gets the name of the web page.
        /// </summary>
        public static string Name => pageName;

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

        public static string HtmlEncode<T>([AllowNull]T value)
        {
            if (value == null)
            {
                return string.Empty;
            }
            return HttpUtility.HtmlEncode(value);
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
                            stb.Append(PageBuilderAndMenu.clsPageBuilder.DivStart("table", string.Empty));
                            stb.Append(BuildTablePage(parts));
                            stb.Append(PageBuilderAndMenu.clsPageBuilder.DivEnd());
                            AddBody(stb.ToString());
                        }
                        break;

                    case DeviceChartTablePageType:
                        {
                            stb.Append(PageBuilderAndMenu.clsPageBuilder.DivStart("chart", string.Empty));
                            stb.Append(BuildChartsPage(parts));
                            stb.Append(PageBuilderAndMenu.clsPageBuilder.DivEnd());
                            AddBody(stb.ToString());
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

        protected static string HtmlTextBox(string name, [AllowNull]string defaultText, int size = 25, string type = "text", bool @readonly = false)
        {
            return Invariant($"<input type=\'{type}\' id=\'{NameToIdWithPrefix(name)}\' size=\'{size}\' name=\'{name}\' value=\'{HtmlEncode(defaultText)}\' {(@readonly ? "readonly" : string.Empty)}>");
        }

        protected static string TextArea(string name, [AllowNull]string defaultText, int rows = 6, int cols = 120, bool @readonly = false)
        {
            return Invariant($"<textarea form_id=\'{NameToIdWithPrefix(name)}\' rows=\'{rows}\' cols=\'{cols}\' name=\'{name}\'  {(@readonly ? "readonly" : string.Empty)}>{HtmlEncode(defaultText)}</textarea>");
        }

        protected string FormButton(string name, string label, string toolTip)
        {
            var button = new clsJQuery.jqButton(name, label, PageName, true)
            {
                id = NameToIdWithPrefix(name),
                toolTip = toolTip,
            };
            button.toolTip = toolTip;
            button.enabled = true;

            return button.Build();
        }

        protected string FormCheckBox(string name, string label, bool @checked, bool autoPostBack = false)
        {
            this.UsesjqCheckBox = true;
            var cb = new clsJQuery.jqCheckBox(name, label, PageName, true, true)
            {
                id = NameToIdWithPrefix(name),
                @checked = @checked,
                autoPostBack = autoPostBack,
            };
            return cb.Build();
        }

        protected string FormDropDown(string name, NameValueCollection options, string selected,
                                      int width, string tooltip, bool autoPostBack = true)
        {
            return FormDropDown(name, options, selected,
                                      width, tooltip, autoPostBack, PageName);
        }

        protected string FormDropDown(string name, NameValueCollection options, string selected,
                                      int width, string tooltip, bool autoPostBack, string pageName)
        {
            var dropdown = new clsJQuery.jqDropList(name, pageName, false)
            {
                selectedItemIndex = -1,
                id = NameToIdWithPrefix(name),
                autoPostBack = autoPostBack,
                toolTip = tooltip,
                style = Invariant($"width: {width}px;"),
                enabled = true,
                submitForm = autoPostBack,
            };

            if (options != null)
            {
                for (var i = 0; i < options.Count; i++)
                {
                    var sel = options.GetKey(i) == selected;
                    dropdown.AddItem(options.Get(i), options.GetKey(i), sel);
                }
            }

            return dropdown.Build();
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

        protected string FormPageButton(string name, string label)
        {
            var b = new clsJQuery.jqButton(name, label, PageName, true)
            {
                id = NameToIdWithPrefix(name),
            };

            return b.Build();
        }

        protected string PageTypeButton(string name, string label, string type, string id = null)
        {
            var b = new clsJQuery.jqButton(name, label, PageName, false)
            {
                id = NameToIdWithPrefix(name),
                url = Invariant($"/{pageUrl}?{PageTypeId}={HttpUtility.UrlEncode(type)}&{RecordId}={HttpUtility.UrlEncode(id ?? string.Empty)}"),
            };

            return b.Build();
        }

        private static string NameToId(string name)
        {
            return name.Replace(' ', '_');
        }

        private static string NameToIdWithPrefix(string name)
        {
            return Invariant($"{ IdPrefix}{NameToId(name)}");
        }

        private string BuildDBSettingTab()
        {
            var dbConfig = pluginConfig.DBLoginInformation;

            StringBuilder stb = new StringBuilder();
            stb.Append(PageBuilderAndMenu.clsPageBuilder.FormStart("ftmSettings", "IdSettings", "Post"));

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
            stb.Append(@" </table>");
            stb.Append(@"</div>");
            stb.Append(PageBuilderAndMenu.clsPageBuilder.FormEnd());

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

            var tabs = new clsJQuery.jqTabs("tab1id", PageName);
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

                var databases = influxDbClient.Database.GetDatabasesAsync().Result;

                var selectedDb = databases.Where((db) => { return db.Name == database; }).FirstOrDefault();
                if (selectedDb == null)
                {
                    results.AppendLine("Database not found on server.<br>");
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(retention))
                    {
                        var retentionPolcies = influxDbClient.Retention.GetRetentionPoliciesAsync(selectedDb.Name).Result;
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
        private const string PageTypeId = "type";
        private const string PasswordKey = "PasswordId";
        private const string RetentionKey = "RetentionId";
        private const string SaveErrorDivId = "SaveErrorDivId";
        private const string SettingSaveButtonName = "SettingSave";
        private const string TabId = "tab";
        private const string UserKey = "UserId";
        private static readonly string pageName = Invariant($"{PlugInData.PlugInName} Configuration").Replace(' ', '_');
        private static readonly string pageUrl = HttpUtility.UrlEncode(pageName);
        private readonly IHSApplication HS;
        private readonly PluginConfig pluginConfig;
    }
}