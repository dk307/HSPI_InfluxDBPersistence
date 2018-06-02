using NullGuard;
using Scheduler;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Hspi
{
    using static System.FormattableString;

    /// <summary>
    /// Helper class to generate configuration page for plugin
    /// </summary>
    /// <seealso cref="Scheduler.PageBuilderAndMenu.clsPageBuilder" />
    //[NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal partial class ConfigPage : PageBuilderAndMenu.clsPageBuilder
    {
        private string BuildAddNewPersistenceWebPageBody([AllowNull]DevicePersistenceData data)
        {
            HSHelper hsHelper = new HSHelper(HS);

            var devices = hsHelper.GetDevices();
            var devicesSorted = devices.OrderBy(x => x.Value);
            var options = new Dictionary<int, string>();
            foreach (var device in devicesSorted)
            {
                options.Add(device.Key, device.Value);
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

            IncludeResourceCSS(stb, "chosen.css");
            IncludeResourceScript(stb, "chosen.jquery.min.js");

            stb.Append(PageBuilderAndMenu.clsPageBuilder.FormStart("ftmDeviceChange", "IdChange", "Post"));

            stb.Append(@"<div>");
            stb.Append(@"<table class='full_width_table'>");
            stb.Append("<tr height='5'><td></td><td></td></tr>");
            stb.Append(Invariant($"<tr><td class='tableheader' colspan=2>{header}</td></tr>"));
            stb.Append(Invariant($"<tr><td class='tablecell'>Name:</td><td class='tablecell'>"));
            stb.Append(FormDropDownChosen(DeviceRefIdId, options, deviceRefId));
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
            stb.Append(Invariant($"<tr><td class='tablecell'>Tags:</td><td class='tablecell'><p><small>Name and locations are automatically added as tags.</small></p>{TextArea(TagsId, tags, cols: 35)}</td></tr>"));
            stb.Append(Invariant($"<tr><td colspan=2>{HtmlTextBox(RecordId, id, type: "hidden")}<div id='{SaveErrorDivId}' style='color:Red'></div></td><td></td></tr>"));
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

        private string BuildPersistenceTab(NameValueCollection parts)
        {
            StringBuilder stb = new StringBuilder();
            IncludeResourceCSS(stb, "jquery.dataTables.css");
            IncludeResourceScript(stb, "jquery.dataTables.min.js");

            HSHelper hsHelper = new HSHelper(HS);

            stb.Append(@"<div>");
            stb.Append(@"<table class='full_width_table'>");
            stb.Append("<tr><td>");

            stb.Append("<table id=\"deviceTable\" class=\"cell-border compact\" style=\"width:100%\">");
            stb.Append(@"<thead><tr>");

            stb.Append(Invariant($"<th>Device</th>"));
            stb.Append(Invariant($"<th>Measurement</th>"));
            stb.Append(Invariant($"<th>Field for value</th>"));
            stb.Append(Invariant($"<th>Range</th>"));
            stb.Append(Invariant($"<th>Field for device string</th>"));
            stb.Append(Invariant($"<th>Tags</th>"));
            stb.Append(Invariant($"<th></th>"));

            stb.Append(@"</tr></thead>");
            stb.Append(@"<tbody>");

            foreach (var pair in pluginConfig.DevicePersistenceData)
            {
                var id = pair.Key;
                var device = pair.Value;

                stb.Append(@"<tr>");
                string name = hsHelper.GetName(device.DeviceRefId) ?? Invariant($"Unknown(RefId:{device.DeviceRefId})");
                stb.Append(Invariant($"<td class='tablecell'><a href='/deviceutility?ref={device.DeviceRefId}&edit=1'>{name}</a></td>"));
                stb.Append(Invariant($"<td class='tablecell'>{device.Measurement}</td>"));
                stb.Append(Invariant($"<td class='tablecell'>{device.Field ?? string.Empty}</td>"));
                string rangeString = !string.IsNullOrWhiteSpace(device.Field) ?
                                        Invariant($"{device.MaxValidValue ?? double.PositiveInfinity} to {device.MinValidValue ?? double.NegativeInfinity}") : string.Empty;
                stb.Append(Invariant($"<td class='tablecell'>{rangeString}</td>"));
                stb.Append(Invariant($"<td class='tablecell'>{device.FieldString ?? string.Empty}</td>"));
                stb.Append("<td class='tablecell'>");
                if (device.Tags != null)
                {
                    foreach (var item in device.Tags)
                    {
                        stb.Append(Invariant($"{item.Key}={item.Value}<br>"));
                    }
                }

                stb.Append("</td>");
                stb.Append("<td class='tablecell'>");
                stb.Append(PageTypeButton(Invariant($"Edit{id}"), "Edit", EditDevicePageType, id: id));
                stb.Append("&nbsp;");
                stb.Append(PageTypeButton(Invariant($"History{id}"), "History", HistoryDevicePageType, id: id));
                stb.Append("</td></tr>");
            }
            stb.Append(@"</tbody>");
            stb.Append(@"</table>");

            stb.AppendLine("<script type='text/javascript'>");
            stb.AppendLine(@"$(document).ready(function() {");
            stb.AppendLine(@"$('#deviceTable').DataTable({
                                       'pageLength':25,
                                        'order': [],
                                        'columnDefs': [
                                            { 'className': 'dt-left', 'targets': '_all'}
                                        ],
                                        'columns': [
                                            null,
                                            null,
                                            null,
                                            null,
                                            null,
                                            null,
                                            { 'orderable': false }
                                          ]
                                    });
                                });");
            stb.AppendLine("</script>");

            stb.Append(Invariant($"<tr><td>{PageTypeButton("Add New Device", AddNewName, EditDevicePageType)}</td><td></td></tr>"));

            stb.Append(Invariant($"<tr><td></td></tr>"));
            stb.Append(@"<tr height='5'><td></td></tr>");
            stb.Append(@"</table>");
            stb.Append(@"</div>");

            return stb.ToString();
        }

        private void HandleSavingPersistencePostBack(NameValueCollection parts, string form)
        {
            if (form == NameToIdWithPrefix(DeletePersistenceSave))
            {
                this.pluginConfig.RemoveDevicePersistenceData(parts[RecordId]);
                this.pluginConfig.FireConfigChanged();
                this.divToUpdate.Add(SaveErrorDivId, RedirectPage(Invariant($"/{pageUrl}?{TabId}=1")));
            }
            else if (form == NameToIdWithPrefix(EditPersistenceCancel))
            {
                this.pluginConfig.RemoveDevicePersistenceData(parts[RecordId]);
                this.pluginConfig.FireConfigChanged();
                this.divToUpdate.Add(SaveErrorDivId, RedirectPage(Invariant($"/{pageUrl}?{TabId}=1")));
            }
            else if ((form == NameToIdWithPrefix(EditPersistenceSave)) ||
                     (form == NameToIdWithPrefix(FillDefaultValuesButtonName)))
            {
                StringBuilder results = new StringBuilder();

                string deviceId = parts[DeviceRefIdId];
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
                        string persistenceId = parts[RecordId];

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
        }

        private const string AddNewName = "Add New";
        private const string DeletePersistenceSave = "DeleteP";
        private const string DescId = "desc";
        private const string DeviceRefIdId = "devicerefidid";
        private const string EditDevicePageType = "editdevice";
        private const string DeviceDataTablePageType = "table";
        private const string EditDeviceImportPageType = "editdeviceimport";
        private const string EditPersistenceCancel = "CancelP";
        private const string EditPersistenceSave = "SaveP";
        private const string FieldDivId = "FieldDivId";
        private const string FieldId = "FieldId";
        private const string FieldStringId = "FieldStringId";
        private const string FillDefaultValuesButtonName = "FillDefaultValues";
        private const string HistoryDevicePageType = "history";
        private const string ImageDivId = "image_id";
        private const string MaxValidValueDivId = "MaxValidValueDivId";
        private const string MaxValidValueId = "MaxValidValueId";
        private const string MeasurementDivId = "MeasurementDivId";
        private const string MeasurementId = "MeasurementId";
        private const string MinValidValueDivId = "MinValidValueDivId";
        private const string MinValidValueId = "MinValidValueId";
        private const string RecordId = "RecordId";
        private const string TagsId = "TagsId";
        private const string UseDefaultsId = "UseDefaultsId";
        private const string SqlId = "sqlid";
        private const string IntervalId = "intervalid";
        private const string UnitId = "unitid";
    }
}