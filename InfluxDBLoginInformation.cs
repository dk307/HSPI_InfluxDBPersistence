using NullGuard;
using System;

namespace Hspi
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal sealed class InfluxDBLoginInformation : IEquatable<InfluxDBLoginInformation>
    {
        public InfluxDBLoginInformation([AllowNull] System.Uri dBUri, [AllowNull] string user,
                                        [AllowNull] string password, [AllowNull] string db)
        {
            DBUri = dBUri;
            User = user ?? string.Empty;
            Password = password ?? string.Empty;
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

        public bool Equals([AllowNull] InfluxDBLoginInformation other)
        {
            if (other == null)
            {
                return false;
            }
            if (this == other)
            {
                return true;
            }

            return this.DBUri == other.DBUri &&
                this.DB == other.DB &&
                this.User == other.User &&
                this.Password == other.Password;
        }

        public override bool Equals([AllowNull] object other)
        {
            if (other == null)
            {
                return false;
            }
            if (this == other)
            {
                return true;
            }
            return Equals(other as InfluxDBLoginInformation);
        }

        public override int GetHashCode()
        {
            return DB.GetHashCode() ^
                   DBUri.GetHashCode() ^
                   Password.GetHashCode() ^
                   User.GetHashCode();
        }
    }
}