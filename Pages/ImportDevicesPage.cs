using NullGuard;
using Scheduler;
using System;
using System.Collections.Specialized;
using System.Text;

namespace Hspi
{
    using static System.FormattableString;

    /// <summary>
    /// Helper class to generate configuration page for plugin
    /// </summary>
    /// <seealso cref="Scheduler.PageBuilderAndMenu.clsPageBuilder" />
    internal partial class ConfigPage : PageBuilderAndMenu.clsPageBuilder
    {
        private string BuildAddNewDeviceImportWebPageBody([AllowNull]ImportDeviceData data)
        {
            HSHelper hsHelper = new HSHelper(HS);

            string name = data?.Name ?? string.Empty;
            string sql = data?.Sql ?? string.Empty;
            string unit = data?.Unit ?? string.Empty;
            int intervalSeconds = data != null ? (int)data.Interval.TotalSeconds : 60;
            string id = data != null ? data.Id : string.Empty;
            string buttonLabel = data != null ? "Save" : "Add";
            string header = data != null ? "Edit Device Import" : "Add New Device Import";

            StringBuilder stb = new StringBuilder();

            stb.Append(PageBuilderAndMenu.clsPageBuilder.FormStart("ftmDeviceImportChange", "IdChange", "Post"));

            stb.Append(@"<div>");
            stb.Append(@"<table class='full_width_table'>");
            stb.Append("<tr height='5'><td></td><td></td></tr>");
            stb.Append(Invariant($"<tr><td class='tableheader' colspan=2>{header}</td></tr>"));
            stb.Append("</td></tr>");
            stb.Append(Invariant($"<tr><td class='tablecell'>Name:</td><td class='tablecell'>"));
            stb.Append(HtmlTextBox(NameId, name, @readonly: data != null ));
            stb.Append("</td></tr>");
            stb.Append(Invariant($"<tr><td class='tablecell'>Sql:</td><td class='tablecell'>"));
            stb.Append(TextArea(SqlId, sql, 6, 65));
            stb.Append("</td></tr>");
            stb.Append(Invariant($"<tr><td class='tablecell'>Refresh Intervals(seconds):</td><td class='tablecell'>"));
            stb.Append(HtmlTextBox(IntervalId, Invariant($"{intervalSeconds}")));
            stb.Append("</td></tr>");
            stb.Append("</td></tr>");
            stb.Append(Invariant($"<tr><td class='tablecell'>Unit:</td><td class='tablecell'>"));
            stb.Append(HtmlTextBox(UnitId, unit, @readonly: data != null));
            stb.Append("</td></tr>");

            stb.Append(Invariant($"<tr><td colspan=2>{HtmlTextBox(RecordId, id, type: "hidden")}<div id='{SaveErrorDivId}' style='color:Red'></div></td><td></td></tr>"));
            stb.Append(Invariant($"<tr><td colspan=2>{FormPageButton(SaveDeviceImport, buttonLabel)}"));

            if (data != null)
            {
                stb.Append(FormPageButton(DeleteDeviceImport, "Delete"));
            }

            stb.Append(FormPageButton(CancelDeviceImport, "Cancel"));
            stb.Append(Invariant($"</td></tr>"));
            stb.Append("<tr height='5'><td colspan=2></td></tr>");
            stb.Append(@"</table>");
            stb.Append(@"</div>");
            stb.Append(PageBuilderAndMenu.clsPageBuilder.FormEnd());

            return stb.ToString();
        }

        private string BuildImportDevicesTab(NameValueCollection parts)
        {
            StringBuilder stb = new StringBuilder();

            stb.Append(@"<div>");
            stb.Append(@"<table class='full_width_table'>");
            stb.Append("<tr><td>");

            stb.Append("<table id=\"importDeviceTable\" class=\"cell-border compact\" style=\"width:100%\">");
            stb.Append(@"<thead><tr>");
            stb.Append(Invariant($"<th>Name</th>"));
            stb.Append(Invariant($"<th>Sql</th>"));
            stb.Append(Invariant($"<th>Interval</th>"));
            stb.Append(Invariant($"<th>Unit</th>"));
            stb.Append(Invariant($"<th></th>"));
            stb.Append(@"</tr></thead>");
            stb.Append(@"<tbody>");

            foreach (var pair in pluginConfig.ImportDevicesData)
            {
                var id = pair.Key;
                var device = pair.Value;

                stb.Append(@"<tr>");
                stb.Append(Invariant($"<td class='tablecell'>{device.Name}</td>"));
                stb.Append(Invariant($"<td class='tablecell'>{device.Sql}</td>"));
                stb.Append(Invariant($"<td class='tablecell'>{device.Interval}</td>"));
                stb.Append(Invariant($"<td class='tablecell'>{device.Unit}</td>"));
                stb.Append("</td>");
                stb.Append("<td class='tablecell'>");
                stb.Append(PageTypeButton(Invariant($"Edit{id}"), "Edit", EditDeviceImportPageType, id: id));
                stb.Append("</td></tr>");
            }
            stb.Append(@"</tbody>");
            stb.Append(@"</table>");

            stb.AppendLine("<script type='text/javascript'>");
            stb.AppendLine(@"$(document).ready(function() {");
            stb.AppendLine(@"$('#importDeviceTable').DataTable({
                                       'pageLength':25
                                    });
                                });");
            stb.AppendLine("</script>");

            stb.Append(Invariant($"<tr><td>{PageTypeButton("Add New Device Import", AddNewName, EditDeviceImportPageType)}</td><td></td></tr>"));

            stb.Append(Invariant($"<tr><td></td></tr>"));
            stb.Append(@"<tr height='5'><td></td></tr>");
            stb.Append(@"</table>");
            stb.Append(@"</div>");

            return stb.ToString();
        }

        private void HandleSavingDeviceImportPostBack(NameValueCollection parts, string form)
        {
            if (form == NameToIdWithPrefix(DeleteDeviceImport))
            {
                this.pluginConfig.RemoveImportDeviceData(parts[RecordId]);
                this.pluginConfig.FireConfigChanged();
                this.divToUpdate.Add(SaveErrorDivId, RedirectPage(Invariant($"/{pageUrl}?{TabId}=2")));
            }
            else if (form == NameToIdWithPrefix(CancelDeviceImport))
            {
                this.divToUpdate.Add(SaveErrorDivId, RedirectPage(Invariant($"/{pageUrl}?{TabId}=2")));
            }
            else if (form == NameToIdWithPrefix(SaveDeviceImport))
            {
                StringBuilder results = new StringBuilder();

                string name = parts[NameId];
                if (string.IsNullOrWhiteSpace(name))
                {
                    results.AppendLine("Name is empty.<br>");
                }

                string sql = parts[SqlId];
                if (string.IsNullOrWhiteSpace(sql))
                {
                    results.AppendLine("Sql is empty.<br>");
                }

                string intervalString = parts[IntervalId];
                if (!int.TryParse(intervalString, out int intervalSeconds))
                {
                    results.AppendLine("Interval is not valid.<br>");
                }

                if (results.Length > 0)
                {
                    this.divToUpdate.Add(SaveErrorDivId, results.ToString());
                }
                else
                {
                    try
                    {
                        GetData(sql);
                    }
                    catch (Exception ex)
                    {
                        results.Append(Invariant($"Query Failed with {ex.GetFullMessage()}"));
                    }

                    if (results.Length > 0)
                    {
                        this.divToUpdate.Add(SaveErrorDivId, results.ToString());
                    }
                    else
                    {
                        string id = parts[RecordId];

                        if (string.IsNullOrWhiteSpace(id))
                        {
                            id = System.Guid.NewGuid().ToString();
                        }

                        var data = new ImportDeviceData(id, name, sql, TimeSpan.FromSeconds(intervalSeconds), parts[UnitId]);
                        this.pluginConfig.AddImportDeviceData(data);
                        this.pluginConfig.FireConfigChanged();
                        this.divToUpdate.Add(SaveErrorDivId, RedirectPage(Invariant($"/{pageUrl}?{TabId}=2")));
                    }
                }
            }
        }

        private const string CancelDeviceImport = "CancelDI";
        private const string DeleteDeviceImport = "DeleteDI";
        private const string SaveDeviceImport = "SaveDI";
        private const string NameId = "NameId";
    }
}