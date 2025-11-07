using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Repository.Utils
{
    public class MySqlTimeZoneInterceptor : DbConnectionInterceptor
    {
        private const string SetTimeZoneCommand = "SET time_zone = '+07:00'";

        public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            await SetTimeZoneAsync(connection, cancellationToken);
            await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
        }

        public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
        {
            SetTimeZone(connection);
            base.ConnectionOpened(connection, eventData);
        }

        private static void SetTimeZone(DbConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText = SetTimeZoneCommand;
            command.ExecuteNonQuery();
        }

        private static async Task SetTimeZoneAsync(DbConnection connection, CancellationToken cancellationToken)
        {
            using var command = connection.CreateCommand();
            command.CommandText = SetTimeZoneCommand;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}

