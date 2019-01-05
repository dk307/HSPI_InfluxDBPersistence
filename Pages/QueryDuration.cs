using System.ComponentModel;

namespace Hspi.Pages
{
    public enum QueryDuration
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
        D30d,

        [Description("60 days")]
        D60d,

        [Description("180 days")]
        D180d,

        [Description("365 days")]
        D365d,
    };
}