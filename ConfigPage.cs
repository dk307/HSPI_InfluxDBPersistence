﻿using HomeSeerAPI;
using InfluxData.Net.Common.Enums;
using InfluxData.Net.InfluxDb;
using NullGuard;
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
                NameValueCollection parts = HttpUtility.ParseQueryString(queryString);

                string pageType = parts[PageTypeId];
                reset();

                StringBuilder stb = new StringBuilder();
                stb.Append(HS.GetPageHeader(Name, "Configuration", string.Empty, string.Empty, false, false));
                stb.Append(PageBuilderAndMenu.clsPageBuilder.DivStart("pluginpage", string.Empty));

                switch (pageType)
                {
                    case EditDevicePageType:
                        {
                            pluginConfig.DevicePersistenceData.TryGetValue(parts[PersistenceId], out var data);
                            stb.Append(BuildAddNewWebPageBody(data));
                        }
                        break;

                    case HistoryDevicePageType:
                        {
                            if (pluginConfig.DevicePersistenceData.TryGetValue(parts[PersistenceId], out var data))
                            {
                                stb.Append(BuildHistoryPage(parts, data));
                            }
                            else
                            {
                                stb.Append(BuildWebPageBody(parts));
                            }
                        }
                        break;

                    default:
                    case null:
                        {
                            stb.Append(BuildWebPageBody(parts));
                            break;
                        }
                }

                stb.Append(PageBuilderAndMenu.clsPageBuilder.DivEnd());
                AddBody(stb.ToString());
                AddFooter(HS.GetPageFooter());
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
            else if (form == NameToIdWithPrefix(EditPersistenceCancel))
            {
                this.divToUpdate.Add(SaveErrorDivId, RedirectPage(Invariant($"/{pageUrl}?{TabId}=1")));
            }
            else if ((form == NameToIdWithPrefix(EditPersistenceSave)) ||
                     (form == NameToIdWithPrefix(FillDefaultValuesButtonName)))
            {
                HandleSavingPersistencePostBack(parts, form);
            }
            else if (form == NameToIdWithPrefix(DeletePersistenceSave))
            {
                this.pluginConfig.RemoveDevicePersistenceData(parts[PersistenceId]);
                this.pluginConfig.FireConfigChanged();
                this.divToUpdate.Add(SaveErrorDivId, RedirectPage(Invariant($"/{pageUrl}?{TabId}=1")));
            }
            else if ((form == NameToIdWithPrefix(HistoryQueryTypeId)) || (form == NameToIdWithPrefix(HistoryRunQueryButtonName)))
            {
                HandleHistoryPagePostBack(parts, form);
            }

            return base.postBackProc(Name, data, user, userRights);
        }

        protected static string HtmlTextBox(string name, string defaultText, int size = 25, string type = "text", bool @readonly = false)
        {
            return Invariant($"<input type=\'{type}\' id=\'{NameToIdWithPrefix(name)}\' size=\'{size}\' name=\'{name}\' value=\'{defaultText}\' {(@readonly ? "readonly" : string.Empty)}>");
        }

        protected static string TextArea(string name, string defaultText, int rows = 6, int cols = 120, bool @readonly = false)
        {
            return Invariant($"<textarea form_id=\'{NameToIdWithPrefix(name)}\' rows=\'{rows}\' cols=\'{cols}\' name=\'{name}\'  {(@readonly ? "readonly" : string.Empty)}>{defaultText}</textarea>");
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
            var cb = new clsJQuery.jqCheckBox(name, label, PageName, true, true)
            {
                id = NameToIdWithPrefix(name),
                @checked = @checked,
                autoPostBack = autoPostBack,
            };
            return cb.Build();
        }

        protected string FormDropDown(string name, NameValueCollection options, string selected, int width, string tooltip, bool autoPostBack = true)
        {
            var dropdown = new clsJQuery.jqDropList(name, PageName, false)
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

        protected string FormPageButton(string name, string label)
        {
            var b = new clsJQuery.jqButton(name, label, PageName, true)
            {
                id = NameToIdWithPrefix(name),
            };

            return b.Build();
        }

        protected string PageTypeButton(string name, string label, string type, string persistenceId = null)
        {
            var b = new clsJQuery.jqButton(name, label, PageName, false)
            {
                id = NameToIdWithPrefix(name),
                url = Invariant($"/{pageUrl}?{PageTypeId}={HttpUtility.UrlEncode(type)}&{PersistenceId}={HttpUtility.UrlEncode(persistenceId ?? string.Empty)}"),
            };

            return b.Build();
        }

        private static string CreateSortLinkOnTab1(string name, int column, bool descending)
        {
            return Invariant($"<a href=\"{pageUrl}?{TabId}=1&{SortColumnId}={column}&{DescId}={Convert.ToInt32(!descending)}\">{name}</a>");
        }

        private static string NameToId(string name)
        {
            return name.Replace(' ', '_');
        }

        private static string NameToIdWithPrefix(string name)
        {
            return Invariant($"{ IdPrefix}{NameToId(name)}");
        }

        private string BuildAddNewWebPageBody([AllowNull]DevicePersistenceData data)
        {
            HSHelper hsHelper = new HSHelper(HS);
            NameValueCollection persistanceNameCollection = new NameValueCollection();

            var devices = hsHelper.GetDevices();
            var devicesSorted = devices.OrderBy(x => x.Value);
            foreach (var device in devicesSorted)
            {
                persistanceNameCollection.Add(device.Key.ToString(CultureInfo.InvariantCulture), device.Value);
            }

            int deviceRefId = data != null ? data.DeviceRefId : -1;

            string measurement = data != null ? data.Measurement : string.Empty;
            string field = data?.Field ?? string.Empty;
            string fieldString = data?.FieldString ?? string.Empty;
            string maxValidValue = data?.MaxValidValue?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            string minValidValue = data?.MinValidValue?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            string tags = string.Empty;
            string id = data != null ? data.Id : string.Empty;
            string buttonLabel = data != null ? "Save" : "Add";
            string header = data != null ? "Edit Persistence" : "Add New DB Persistence";

            if (data != null && data.Tags != null)
            {
                foreach (var tag in data.Tags)
                {
                    tags += Invariant($"{tag.Key}={tag.Value}{Environment.NewLine}");
                }
            }

            StringBuilder stb = new StringBuilder();

            stb.Append(PageBuilderAndMenu.clsPageBuilder.FormStart("ftmDeviceChange", "IdChange", "Post"));

            stb.Append(@"<div>");
            stb.Append(@"<table class='full_width_table'>");
            stb.Append("<tr height='5'><td></td><td></td></tr>");
            stb.Append(Invariant($"<tr><td class='tableheader' colspan=2>{header}</td></tr>"));
            stb.Append(Invariant($"<tr><td class='tablecell'>Name:</td><td class='tablecell'>"));
            stb.Append(FormDropDown(DeviceRefId, persistanceNameCollection, deviceRefId.ToString(CultureInfo.InvariantCulture), 250, string.Empty, false));
            stb.Append(Invariant($"&nbsp;"));
            stb.Append(FormButton(FillDefaultValuesButtonName, "Fill Default Values", "Fill default values"));
            stb.Append(Invariant($"</td></tr>"));
            stb.Append(Invariant($"<tr><td class='tablecell'>Measurement:</td><td class='tablecell'>"));
            stb.Append(DivStart(MeasurementDivId, string.Empty));
            stb.Append(HtmlTextBox(MeasurementId, measurement));
            stb.Append(DivEnd());
            stb.Append("</td></tr>");
            stb.Append(Invariant($"<tr><td class='tablecell'>Field for value:</td><td class='tablecell'>"));
            stb.Append(DivStart(FieldDivId, string.Empty));
            stb.Append(HtmlTextBox(FieldId, field));
            stb.Append(DivEnd());
            stb.Append("</td></tr>");

            stb.Append(Invariant($"<tr><td class='tablecell'>Max valid value:</td><td class='tablecell'>"));
            stb.Append(DivStart(MaxValidValueDivId, string.Empty));

            stb.Append(HtmlTextBox(MaxValidValueId, maxValidValue));
            stb.Append(DivEnd());

            stb.Append("</td></tr>");
            stb.Append(Invariant($"<tr><td class='tablecell'>Min valid value:</td><td class='tablecell'>"));
            stb.Append(DivStart(MinValidValueDivId, string.Empty));

            stb.Append(HtmlTextBox(MinValidValueId, minValidValue));
            stb.Append(DivEnd());

            stb.Append("</td></tr>");
            stb.Append(Invariant($"<tr><td class='tablecell'>Field for string value:</td><td class='tablecell'>{HtmlTextBox(FieldStringId, fieldString)}</td></tr>"));
            stb.Append(Invariant($"<tr><td class='tablecell'>Tags:</td><td class='tablecell'><p><small>Name and locations are automatically added as tags.</small></p>{TextArea(TagsId, tags, cols:35)}</td></tr>"));
            stb.Append(Invariant($"<tr><td colspan=2>{HtmlTextBox(PersistenceId, id, type: "hidden")}<div id='{SaveErrorDivId}' style='color:Red'></div></td><td></td></tr>"));
            stb.Append(Invariant($"<tr><td colspan=2>{FormPageButton(EditPersistenceSave, buttonLabel)}"));

            if (data != null)
            {
                stb.Append(FormPageButton(DeletePersistenceSave, "Delete"));
            }

            stb.Append(FormPageButton(EditPersistenceCancel, "Cancel"));
            stb.Append(Invariant($"</td></tr>"));
            stb.Append("<tr height='5'><td colspan=2></td></tr>");
            stb.Append(@"</table>");
            stb.Append(@"</div>");
            stb.Append(PageBuilderAndMenu.clsPageBuilder.FormEnd());

            return stb.ToString();
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
            stb.Append(Invariant($"<tr><td class='tablecell'>Debug Logging Enabled:</td><td class='tablecell'>{FormCheckBox(DebugLoggingId, string.Empty, this.pluginConfig.DebugLogging)}</td ></tr>"));
            stb.Append(Invariant($"<tr><td colspan=2><div id='{ErrorDivId}' style='color:Red'></div></td></tr>"));
            stb.Append(Invariant($"<tr><td colspan=2>{FormButton(SettingSaveButtonName, "Save", "Save Settings")}</td></tr>"));
            stb.Append("<tr height='5'><td colspan=2></td></tr>");
            stb.Append(@" </table>");
            stb.Append(@"</div>");
            stb.Append(PageBuilderAndMenu.clsPageBuilder.FormEnd());

            return stb.ToString();
        }

        private string BuildPersistenceTab(NameValueCollection parts)
        {
            int? sortColumn = null;
            if (int.TryParse(parts[SortColumnId] ?? string.Empty, out var value))
            {
                if ((value > 0) && (value < 5))
                {
                    sortColumn = value;
                }
            }

            bool ascending = true;
            if (int.TryParse(parts[DescId] ?? string.Empty, out var descValue))
            {
                ascending = descValue > 0;
            }

            HSHelper hsHelper = new HSHelper(HS);
            StringBuilder stb = new StringBuilder();

            stb.Append(@"<div>");
            stb.Append(@"<table class='full_width_table'>");
            stb.Append("<tr height='5'><td colspan=7></td></tr>");
            stb.Append("<tr>");
            stb.Append(Invariant($"<td class='tablecolumn'>{CreateSortLinkOnTab1("Device", 1, ascending)}</td>"));
            stb.Append(Invariant($"<td class='tablecolumn'>{CreateSortLinkOnTab1("Measurement", 2, ascending)}</td>"));
            stb.Append(Invariant($"<td class='tablecolumn'>{CreateSortLinkOnTab1("Field for value", 3, ascending)}</td>"));
            stb.Append(@"<td class='tablecolumn'>Range for value</td>");
            stb.Append(Invariant($"<td class='tablecolumn'>{CreateSortLinkOnTab1("Field for device string", 4, ascending)}</td>"));
            stb.Append("<td class='tablecolumn'>Tags</td>");
            stb.Append("<td class='tablecolumn'></td></tr>");

            IEnumerable<string> sortedData;

            if (sortColumn.HasValue)
            {
                sortedData = pluginConfig.DevicePersistenceData.Keys.OrderBy(x =>
                {
                    switch (sortColumn.Value)
                    {
                        case 1:
                            return hsHelper.GetName(pluginConfig.DevicePersistenceData[x].DeviceRefId) ?? string.Empty;

                        case 2:
                            return pluginConfig.DevicePersistenceData[x].Measurement;

                        case 3:
                            return pluginConfig.DevicePersistenceData[x].Field;

                        case 4:
                            return pluginConfig.DevicePersistenceData[x].FieldString;

                        default:
                            return x;
                    }
                }, StringComparer.Ordinal);

                if (!ascending)
                {
                    sortedData = sortedData.Reverse();
                }
            }
            else
            {
                sortedData = pluginConfig.DevicePersistenceData.Keys;
            }

            foreach (var id in sortedData)
            {
                var device = pluginConfig.DevicePersistenceData[id];

                stb.Append(@"<tr>");
                string name = hsHelper.GetName(device.DeviceRefId) ?? Invariant($"Unknown(RefId:{device.DeviceRefId})");
                stb.Append(Invariant($"<td class='tablecell'><a href='/deviceutility?ref={device.DeviceRefId}&edit=1'>{name}</a></td>"));
                stb.Append(Invariant($"<td class='tablecell'>{device.Measurement}</td>"));
                stb.Append(Invariant($"<td class='tablecell'>{device.Field ?? string.Empty}</td>"));
                string rangeString = !string.IsNullOrWhiteSpace(device.Field) ?
                                        Invariant($"{device.MaxValidValue ?? double.PositiveInfinity} to {device.MinValidValue ?? double.NegativeInfinity}") : string.Empty;
                stb.Append(Invariant($"<td class='tablecell'>{rangeString}</td>"));
                stb.Append(Invariant($"<td class='tablecell'>{device.FieldString ?? string.Empty}</td>"));
                stb.Append(@"<td class='tablecell'>");
                if (device.Tags != null)
                {
                    foreach (var item in device.Tags)
                    {
                        stb.Append(Invariant($"{item.Key}={item.Value}<br>"));
                    }
                }
                stb.Append("</td>");
                stb.Append("<td class='tablecell'>");
                stb.Append(PageTypeButton(Invariant($"Edit{id}"), "Edit", EditDevicePageType, persistenceId: id));
                stb.Append("&nbsp;");
                stb.Append(PageTypeButton(Invariant($"History{id}"), "History", HistoryDevicePageType, persistenceId: id));
                stb.Append("</td></tr>");
            }

            stb.Append(Invariant($"<tr><td colspan=7>{PageTypeButton("Add New Device", AddNewName, EditDevicePageType)}</td><td></td></tr>"));

            stb.Append(Invariant($"<tr><td colspan=7></td></tr>"));
            stb.Append(@"<tr height='5'><td colspan=7></td></tr>");
            stb.Append(@" </table>");
            stb.Append(@"</div>");

            return stb.ToString();
        }

        /// <summary>
        /// Builds the web page body for the configuration page.
        /// The page has separate forms so that only the data in the appropriate form is returned when a button is pressed.
        /// </summary>
        private string BuildWebPageBody(NameValueCollection parts)
        {
            string tab = parts[TabId] ?? "0";
            int defaultTab = 0;
            int.TryParse(tab, out defaultTab);

            int i = 0;
            StringBuilder stb = new StringBuilder();

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

            switch (defaultTab)
            {
                case 0:
                    tabs.defaultTab = tab1.tabDIVID;
                    break;

                case 1:
                    tabs.defaultTab = tab2.tabDIVID;
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

            try
            {
                var influxDbClient = new InfluxDbClient(dbUri.ToString(), username, password, InfluxDbVersion.v_1_3);

                var databases = influxDbClient.Database.GetDatabasesAsync().Result;

                if (!databases.Any((db) => { return db.Name == database; }))
                {
                    results.AppendLine("Database not found on server.<br>");
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
                var dbConfig = new InfluxDBLoginInformation(dbUri, username, password, parts[DBKey]);
                this.pluginConfig.DBLoginInformation = dbConfig;
                this.pluginConfig.DebugLogging = parts[DebugLoggingId] == "checked";
                this.pluginConfig.FireConfigChanged();
            }
        }

        private void HandleSavingPersistencePostBack(NameValueCollection parts, string form)
        {
            StringBuilder results = new StringBuilder();

            string deviceId = parts[DeviceRefId];
            if (!int.TryParse(deviceId, out int deviceRefId))
            {
                results.AppendLine("Device is not valid.<br>");
            }

            if (form == NameToIdWithPrefix(FillDefaultValuesButtonName))
            {
                if (results.Length == 0)
                {
                    HSHelper hSHelper = new HSHelper(HS);

                    hSHelper.Fill(deviceRefId, out var typeString, out var maxValidValue, out var minValidValue);

                    divToUpdate.Add(MeasurementDivId, HtmlTextBox(MeasurementId, typeString ?? string.Empty));
                    if (!string.IsNullOrEmpty(typeString))
                    {
                        divToUpdate.Add(FieldDivId, HtmlTextBox(FieldId, PluginConfig.DefaultFieldValueString));
                        divToUpdate.Add(MaxValidValueDivId, HtmlTextBox(MaxValidValueId, maxValidValue?.ToString(CultureInfo.InvariantCulture) ?? string.Empty));
                        divToUpdate.Add(MinValidValueDivId, HtmlTextBox(MinValidValueId, minValidValue?.ToString(CultureInfo.InvariantCulture) ?? string.Empty));
                    }
                }
            }
            else
            {
                string measurement = parts[MeasurementId];
                if (string.IsNullOrWhiteSpace(measurement))
                {
                    results.AppendLine("Measurement is not valid.<br>");
                }

                string field = parts[FieldId];
                string fieldString = parts[FieldStringId];
                if (string.IsNullOrWhiteSpace(field) && string.IsNullOrWhiteSpace(fieldString))
                {
                    results.AppendLine("Both Field and FieldString are not valid. One of them need to valid.<br>");
                }

                string tagsString = parts[TagsId];
                var tagsList = tagsString.Split(new string[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);

                var tags = new Dictionary<string, string>();
                foreach (var tagString in tagsList)
                {
                    if (string.IsNullOrWhiteSpace(tagString))
                    {
                        continue;
                    }

                    var pair = tagString.Split('=');

                    if (pair.Length != 2)
                    {
                        results.AppendLine(Invariant($"Unknown tag type: {tagString}. Format tagType= value<br>"));
                    }
                    else
                    {
                        tags.Add(pair[0], pair[1]);
                    }
                }

                string maxValidValueString = parts[MaxValidValueId];
                string minValidValueString = parts[MinValidValueId];

                double? maxValidValue = null;
                double? minValidValue = null;

                if (!string.IsNullOrEmpty(maxValidValueString))
                {
                    if (double.TryParse(parts[MaxValidValueId], out var value))
                    {
                        maxValidValue = value;
                    }
                    else
                    {
                        results.AppendLine("Max valid value is not valid.<br>");
                    }
                }

                if (!string.IsNullOrEmpty(minValidValueString))
                {
                    if (double.TryParse(parts[MinValidValueId], out var value))
                    {
                        minValidValue = value;
                    }
                    else
                    {
                        results.AppendLine("Min valid value is not valid.<br>");
                    }
                }

                if (maxValidValue.HasValue && minValidValue.HasValue)
                {
                    if ((maxValidValue.Value - minValidValue.Value) <= 0)
                    {
                        results.AppendLine("Max and Min valid values are not valid.<br>");
                    }
                }

                if ((maxValidValue.HasValue || minValidValue.HasValue) && string.IsNullOrWhiteSpace(field))
                {
                    results.AppendLine("Max and Min valid values don't mean anything without field to store them.<br>");
                }

                if (results.Length > 0)
                {
                    this.divToUpdate.Add(SaveErrorDivId, results.ToString());
                }
                else
                {
                    string persistenceId = parts[PersistenceId];

                    if (string.IsNullOrWhiteSpace(persistenceId))
                    {
                        persistenceId = System.Guid.NewGuid().ToString();
                    }

                    var persistenceData = new DevicePersistenceData(persistenceId, deviceRefId, measurement, field, fieldString, tags, maxValidValue, minValidValue);
                    this.pluginConfig.AddDevicePersistenceData(persistenceData);
                    this.pluginConfig.FireConfigChanged();
                    this.divToUpdate.Add(SaveErrorDivId, RedirectPage(Invariant($"/{pageUrl}?{TabId}=1")));
                }
            }
        }

        private const string AddNewName = "Add New";
        private const string DBKey = "DBId";
        private const string DBUriKey = "DBUriId";
        private const string DebugLoggingId = "DebugLoggingId";
        private const string DeletePersistenceSave = "DeleteP";
        private const string DescId = "desc";
        private const string DeviceRefId = "DeviceRefId";
        private const string EditDevicePageType = "edit";
        private const string EditPersistenceCancel = "CancelP";
        private const string EditPersistenceSave = "SaveP";
        private const string ErrorDivId = "message_id";
        private const string FieldDivId = "FieldDivId";
        private const string FieldId = "FieldId";
        private const string FieldStringId = "FieldStringId";
        private const string FillDefaultValuesButtonName = "FillDefaultValues";
        private const string HistoryDevicePageType = "history";
        private const string IdPrefix = "id_";
        private const string ImageDivId = "image_id";
        private const string MaxValidValueDivId = "MaxValidValueDivId";
        private const string MaxValidValueId = "MaxValidValueId";
        private const string MeasurementDivId = "MeasurementDivId";
        private const string MeasurementId = "MeasurementId";
        private const string MinValidValueDivId = "MinValidValueDivId";
        private const string MinValidValueId = "MinValidValueId";
        private const string PageTypeId = "type";
        private const string PasswordKey = "PasswordId";
        private const string PersistenceId = "PersistenceId";
        private const string SaveErrorDivId = "SaveErrorDivId";
        private const string SettingSaveButtonName = "SettingSave";
        private const string SortColumnId = "sort";
        private const string TabId = "tab";
        private const string TagsId = "TagsId";
        private const string UseDefaultsId = "UseDefaultsId";
        private const string UserKey = "UserId";
        private static readonly string pageName = Invariant($"{PlugInData.PlugInName} Configuration").Replace(' ', '_');
        private static readonly string pageUrl = HttpUtility.UrlEncode(pageName);
        private readonly IHSApplication HS;
        private readonly PluginConfig pluginConfig;
    }
}