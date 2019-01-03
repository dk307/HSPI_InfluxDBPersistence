using HomeSeerAPI;
using NullGuard;
using Scheduler;
using System;
using System.Collections.Specialized;
using System.Web;
using Hspi.Utils;
using static System.FormattableString;

namespace Hspi.Pages
{
    internal class PageHelper : PageBuilderAndMenu.clsPageBuilder
    {
        public PageHelper(IHSApplication HS, PluginConfig pluginConfig, string pageName) : base(pageName)
        {
            this.HS = HS;
            this.pluginConfig = pluginConfig;
        }

        public static string HtmlEncode<T>([AllowNull]T value)
        {
            if (value == null)
            {
                return string.Empty;
            }
            return HttpUtility.HtmlEncode(value);
        }

        protected static string HtmlTextBox(string name, string defaultText, int size = 25, string type = "text", bool @readonly = false)
        {
            return Invariant($@"<input type='{type}' id='{NameToIdWithPrefix(name)}' size='{size}' name='{name}' value='{HtmlEncode(defaultText)}' {(@readonly ? "readonly" : string.Empty)}>");
        }

        protected static string NameToId(string name)
        {
            return name.Replace(' ', '_');
        }

        protected static string NameToIdWithPrefix(string name)
        {
            return Invariant($"{IdPrefix}{NameToId(name)}");
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

        protected string FormPageButton(string name, string label)
        {
            var b = new clsJQuery.jqButton(name, label, PageName, true)
            {
                id = NameToIdWithPrefix(name),
            };

            return b.Build();
        }

        protected string PageTypeButton(string name, string label, string pageType, string deviceId = null)
        {
            var b = new clsJQuery.jqButton(name, label, PageName, false)
            {
                id = NameToIdWithPrefix(name),
                url = Invariant($@"/{HttpUtility.UrlEncode(ConfigPage.Name)}?{PageTypeId}={HttpUtility.UrlEncode(pageType)}&{RecordId}={HttpUtility.UrlEncode(deviceId ?? string.Empty)}"),
            };

            return b.Build();
        }

        protected string FormDropDown(string name, NameValueCollection options, string selected,
                              int width, string tooltip, bool autoPostBack = true)
        {
            return FormDropDown(name, options, selected,
                                      width, tooltip, autoPostBack, PageName);
        }

        protected static string FormDropDown(string name, NameValueCollection options, string selected,
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

        protected static NameValueCollection CreateNameValueCreation<T>() where T : Enum
        {
            var collection = new NameValueCollection();

            foreach (var value in EnumHelper.GetValues<T>())
            {
                collection.Add(value.ToString(), value.ToString());
            }

            return collection;
        }

        protected const string RecordId = "RecordId";
        protected const string PageTypeId = "type";
        protected readonly IHSApplication HS;
        protected readonly PluginConfig pluginConfig;
        private const string IdPrefix = "id_";
    }
}