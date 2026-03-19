using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using SqlWebApi;
public class SqlConnectionTestResult
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public DateTimeOffset? ServerTime { get; set; }
    public Exception Exception { get; set; }
}

public static class SqlConnectionTester
{
    public static async Task<SqlConnectionTestResult> TestAsync(
        string connectionString,
        bool getServerTime,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return new SqlConnectionTestResult
            {
                Success = false,
                Message = "Connection string is empty."
            };
        }

        SqlConnection conn = null;

        try
        {
            conn = new SqlConnection(connectionString);
            await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

            DateTimeOffset? serverTime = null;

            if (getServerTime)
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT SYSDATETIMEOFFSET()";
                    cmd.CommandType = CommandType.Text;

                    var result = await cmd.ExecuteScalarAsync(cancellationToken)
                                           .ConfigureAwait(false);

                    if (result is DateTimeOffset dto)
                        serverTime = dto;
                    else if (result is DateTime dt)
                        serverTime = new DateTimeOffset(dt);
                }
            }

            return new SqlConnectionTestResult
            {
                Success = true,
                Message = "Connection successful.",
                ServerTime = serverTime
            };
        }
        catch (SqlException ex)
        {
            return new SqlConnectionTestResult
            {
                Success = false,
                Message = GetFriendlySqlError(ex),
                Exception = ex
            };
        }
        catch (Exception ex)
        {
            return new SqlConnectionTestResult
            {
                Success = false,
                Message = "Unexpected error: " + ex.Message,
                Exception = ex
            };
        }
        finally
        {
            if (conn != null)
                conn.Dispose();
        }
    }

    private static string GetFriendlySqlError(SqlException ex)
    {
        switch (ex.Number)
        {
            case 18456:
                return "Login failed: incorrect username or password.";
            case 4060:
                return "Cannot open database: database does not exist or access denied.";
            case 53:
                return "SQL Server not reachable: check hostname, port, firewall, or VPN.";
            case 258:
                return "Timeout while connecting to SQL Server.";
            case 10060:
                return "TCP connection timeout: SQL Server firewall or network issue.";
            default:
                return "SQL error " + ex.Number + ": " + ex.Message;
        }
    }
}
