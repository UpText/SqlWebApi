using Microsoft.Data.SqlClient;
using SqlWebApi.Configuration;

namespace sqlwebapi;

using System.Data;
using System.Data.SqlClient;
using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;

public class CategoryImageFunction
{
    private readonly ILogger _logger;
    private  IConfigProvider _config;

    public CategoryImageFunction(ILoggerFactory loggerFactory,
        IConfigProvider configProvider)
    {
        _logger = loggerFactory.CreateLogger<CategoryImageFunction>();
        _config = configProvider;
    }
    
    [Function("SqlImageFunc")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "swa/images/{Service}/{Resource}/{id}")] 
        HttpRequestData req,
        string Service, string Resource, string? id=null)
    {
        try
        {
            string SqlSchema = 
                await _config.GetAsync(Service + ":SqlSchema");

            var connString = await _config.GetAsync(Service + ":SqlConnectionString");
            if (string.IsNullOrWhiteSpace(connString))
            {
                var resp = req.CreateResponse(HttpStatusCode.InternalServerError);
                await resp.WriteStringAsync("SqlConnectionString is not configured.");
                return resp;
            }

            // Optional query: raw=true to return bytes as-is (no OLE stripping, no re-encode)
            var raw = System.Web.HttpUtility.ParseQueryString(req.Url.Query).Get("raw")?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;

            byte[]? blob = await ReadCategoryImageAsync(connString, SqlSchema, resource:Resource, id:id);
            if (blob == null || blob.Length == 0)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteStringAsync("Image not found.");
                return notFound;
            }

            if (raw)
            {
                var rawResp = req.CreateResponse(HttpStatusCode.OK);
                rawResp.Headers.Add("Content-Type", "application/octet-stream");
                rawResp.Headers.Add("Cache-Control", "public, max-age=3600");
                blob = TryExtractBmpFromOle(blob);
                await rawResp.WriteBytesAsync(blob);
                return rawResp;
            }

            // If the blob contains an OLE wrapper (classic Northwind “Picture”),
            // strip to the start of the BMP/DIB ("BM") then re-encode to PNG to fix the “missing top” issue.
            var processed = TryExtractBmpFromOle(blob) ?? blob;

            // Try decode with ImageSharp; if it fails, just stream the original as octet-stream
            try
                {
                using var inMs = new MemoryStream(processed, writable: false);
                using var image = await Image.LoadAsync(inMs); // ImageSharp auto-detects format
                using var outMs = new MemoryStream();
                // Re-encode to PNG for broad compatibility
                await image.SaveAsync(outMs, new PngEncoder());
                var pngBytes = outMs.ToArray();

                var ok = req.CreateResponse(HttpStatusCode.OK);
                ok.Headers.Add("Content-Type", "image/png");
                ok.Headers.Add("Cache-Control", "public, max-age=3600");
                await ok.WriteBytesAsync(pngBytes);
                return ok;
            }
                catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to decode image; returning raw bytes.");
                var fallback = req.CreateResponse(HttpStatusCode.OK);
                fallback.Headers.Add("Content-Type", "application/octet-stream");
                fallback.Headers.Add("Cache-Control", "public, max-age=3600");
                await fallback.WriteBytesAsync(blob);
                return fallback;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in GetCategoryImage");
            var resp = req.CreateResponse(HttpStatusCode.InternalServerError);
            await resp.WriteStringAsync("Unhandled server error.");
            return resp;
        }
    }

    private static async Task<byte[]?> ReadCategoryImageAsync(string connectionString, string service, string resource, string id)
    { 
        string sql = String.Format(
            "exec {0}.{1}_image @id={2}", service, resource, id
        );
 
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add(new SqlParameter("@id", SqlDbType.Int) { Value = id });

        var result = await cmd.ExecuteScalarAsync();
        return result == null || result is DBNull ? null : (byte[])result;
    }

    /// <summary>
    /// For Northwind OLE-wrapped images: find the start of the BMP ("BM" 0x42,0x4D) and slice.
    /// Returns null if "BM" not found (so caller can use original bytes).
    /// </summary>
    private static byte[]? TryExtractBmpFromOle(byte[] data)
    {
        // Search for "BM" magic
        for (int i = 0; i < data.Length - 1; i++)
        {
            if (data[i] == 0x42 && data[i + 1] == 0x4D) // 'B''M'
            {
                // Return from this point to the end
                var len = data.Length - i;
                var slice = new byte[len];
                Buffer.BlockCopy(data, i, slice, 0, len);
                return slice;
            }
        }
        return null;
    }
}
