using InfluxData.Net.InfluxDb.Models.Responses;
using System.Collections.Generic;

namespace InfluxData.Net.InfluxDb.ResponseParsers
{
    public interface IUserResponseParser
    {
        IEnumerable<User> GetUsers(IEnumerable<Serie> series);

        IEnumerable<Grant> GetPrivileges(IEnumerable<Serie> series);
    }
}