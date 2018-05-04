using NullGuard;
using System;

namespace Hspi
{
    internal class InfluxDBLoginInformation : IEquatable<InfluxDBLoginInformation>
    {
        public InfluxDBLoginInformation([AllowNull]System.Uri dBUri, [AllowNull]string user, [AllowNull]string password, [AllowNull]string db)
        {
            DBUri = dBUri;
            User = user;
            Password = password;
            DB = db;
        }

        public string DB { get; }
        public System.Uri DBUri { get; }
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

        public bool Equals(InfluxDBLoginInformation other)
        {
            return this.DBUri == other.DBUri &&
                this.DB == other.DB &&
                this.User == other.User &&
                this.Password == this.Password;
        }
    }
}