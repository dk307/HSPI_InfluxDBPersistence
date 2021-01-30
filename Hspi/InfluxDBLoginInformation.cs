namespace Hspi
{
    internal sealed record InfluxDBLoginInformation
    {
        public InfluxDBLoginInformation(System.Uri? dBUri, string? user,
                                        string? password, string? db)
        {
            DBUri = dBUri;
            User = user ?? string.Empty;
            Password = password ?? string.Empty;
            DB = db ?? string.Empty;
        }

        public string DB { get; }
        public System.Uri? DBUri { get; }
        public string Password { get; }
        public string User { get; }

        public bool IsValid
        {
            get
            {
                return !string.IsNullOrWhiteSpace(DB) &&
                       (DBUri != null);
            }
        }
    }
}