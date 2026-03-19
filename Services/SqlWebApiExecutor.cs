using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Runtime.Caching;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using SqlWebApi.Configuration;
using sqlwebapi;
using MemoryCache = System.Runtime.Caching.MemoryCache;

namespace SqlWebApi.Services;

public class SqlWebApiExecutor : ISqlWebApiExecutor
{
    private static readonly MemoryCache MemoryCache = new("params");
    private readonly ILogger _logger;
    private readonly IConfigProvider _config;

    public SqlWebApiExecutor(ILoggerFactory loggerFactory, IConfigProvider configProvider)
    {
        _logger = loggerFactory.CreateLogger<SqlWebApiExecutor>();
        _config = configProvider;
    }

    public async Task<HttpResponseData> ExecuteAsync(
        HttpRequestData req,
        string service,
        string resource,
        string? id = null,
        string? details = null)
    {
        var timer = new Stopwatch();
        timer.Start();

        var reqContentType = req.Headers.TryGetValues("Content-Type", out var values)
            ? values.FirstOrDefault()
            : "application/json";

        string sqlSchema = await _config.GetAsync(service + ":SqlSchema");

        if (sqlSchema == null)
        {
            return LogAndReturn(service, string.Empty, req,
                new BadRequestObjectResult("Unknown service"), string.Empty,
                null, null, null, null, _logger);
        }

        string sqlExec = "EXEC ";
        if (id != null && id.Trim().ToLower() == "null")
            id = null;
        var hasIdPath = id != null;

        if (details != null)
            resource += "_" + details;

        resource += "_" + req.Method.ToLower();

        string? responseBody = null;
        string requestBody = string.Empty;
        int? returnCode = 200;

        string? sort_field = null;
        string? sort_order = null;
        int? first_row = 0;
        int? last_row = 100;
        int? total_rows = 0;
        int? returned_rows = null;
        JsonNode? data = null;

        if (req.Method.ToLower() != "get" && req.Body != null)
        {
            var body = await new StreamReader(req.Body).ReadToEndAsync();
            requestBody = body;
            if (requestBody.Length > 1 && requestBody.StartsWith("{") && requestBody.EndsWith("}"))
                data = JsonSerializer.Deserialize<JsonNode>(requestBody);
        }

        string requestHeaders = JsonSerializer.Serialize(req.Headers.ToList());
        string? jsonData = string.Empty;

        var connectionString = await _config.GetAsync(service + ":SqlConnectionString");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return LogAndReturn(sqlSchema, requestBody, req,
                new BadRequestObjectResult("Missing database connectionString "), sqlExec,
                timer, null, null, null, _logger);
        }

        using (SqlConnection connection = new(connectionString))
        {
            connection.InfoMessage += (_, e) => _logger.LogInformation("PRINT:{Message}", e.Message);

            var command = new SqlCommand();
            try
            {
                command.Connection = connection;
                command.CommandText = $"[{sqlSchema}].{resource}";
                command.CommandType = CommandType.StoredProcedure;

                connection.Open();

                var cachedParameters = (CachedParameter[]?)MemoryCache.Get(command.CommandText);
                if (cachedParameters == null)
                {
                    SqlCommandBuilder.DeriveParameters(command);
                    cachedParameters = CloneParms(command.Parameters);
                    MemoryCache.Set(command.CommandText, cachedParameters, DateTime.Now.AddSeconds(30));
                }
                else
                {
                    command.Parameters.Clear();
                    foreach (var par in cachedParameters)
                    {
                        var dbPar = (SqlParameter)((ICloneable)par.Param).Clone();
                        dbPar.Value = par.Value;
                        command.Parameters.Add(dbPar);
                    }
                }

                var queryDict = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                foreach (var queryParam in queryDict.Keys)
                {
                    if (queryParam == null)
                        continue;

                    string paramName = queryParam.ToString() ?? string.Empty;
                    string paramValue = Convert.ToString(queryDict[paramName]) ?? string.Empty;

                    if (paramName == "range" && paramValue.Length > 2 && paramValue[0] == '[')
                    {
                        JsonNode data2 = JsonSerializer.Deserialize<JsonNode>(paramValue)!;
                        var jsonArray = data2.AsArray();
                        first_row = jsonArray[0]!.GetValue<int>();
                        last_row = jsonArray[1]!.GetValue<int>();

                        if (command.Parameters.Contains("@first_row") &&
                            command.Parameters["@first_row"].Direction == ParameterDirection.Input)
                            command.Parameters["@first_row"].Value = first_row.ToString();
                        if (command.Parameters.Contains("@last_row") &&
                            command.Parameters["@last_row"].Direction == ParameterDirection.Input)
                            command.Parameters["@last_row"].Value = last_row;
                    }

                    if (paramName == "sort" && paramValue.Length > 2 && paramValue[0] == '[')
                    {
                        JsonNode data2 = JsonSerializer.Deserialize<JsonNode>(paramValue)!;
                        var jsonArray = data2.AsArray();
                        sort_field = jsonArray[0]!.ToString();
                        sort_order = jsonArray[1]!.ToString();
                        if (command.Parameters.Contains("@sort_field") &&
                            command.Parameters["@sort_field"].Direction == ParameterDirection.Input)
                            command.Parameters["@sort_field"].Value = sort_field;
                        if (command.Parameters.Contains("@sort_order") &&
                            command.Parameters["@sort_order"].Direction == ParameterDirection.Input)
                            command.Parameters["@sort_order"].Value = sort_order;
                    }
                }

                foreach (var queryParam in queryDict.Keys)
                {
                    if (queryParam == null || queryParam.ToString() == "range")
                        continue;

                    string paramName = queryParam.ToString() ?? string.Empty;
                    string param = "@" + paramName.Trim();
                    if (command.Parameters.Contains(param) &&
                        command.Parameters[param].Direction == ParameterDirection.Input)
                        command.Parameters[param].Value = Convert.ToString(queryDict[paramName]);
                }

                if (command.Parameters.Contains("@id") && queryDict["id"] == null)
                {
                    command.Parameters["@id"].Value = string.IsNullOrWhiteSpace(id) ? null : id;
                }

                if (command.Parameters.Contains("@requestBody"))
                    command.Parameters["@requestBody"].Value = requestBody;

                if (command.Parameters.Contains("@requestHeaders"))
                    command.Parameters["@requestHeaders"].Value = requestHeaders;

                if (data != null)
                {
                    foreach (SqlParameter param in command.Parameters)
                    {
                        if (param.Direction != ParameterDirection.InputOutput &&
                            param.Direction != ParameterDirection.Input)
                            continue;

                        string paramName = param.ParameterName[1..];
                        JsonNode? jsonNodeParam = data[paramName];
                        if (jsonNodeParam == null)
                            continue;

                        if (jsonNodeParam.GetValueKind() == JsonValueKind.Number)
                            param.Value = jsonNodeParam.GetValue<decimal>().ToString();
                        else if (jsonNodeParam.GetValueKind() == JsonValueKind.True)
                            param.Value = 1;
                        else if (jsonNodeParam.GetValueKind() == JsonValueKind.False)
                            param.Value = 0;
                        else if (jsonNodeParam.GetValueKind() is JsonValueKind.Array or JsonValueKind.Object)
                            param.Value = jsonNodeParam.ToJsonString();
                        else
                            param.Value = jsonNodeParam.GetValue<string>().ToString();
                    }
                }

                sqlExec += command.CommandText + " ";

                foreach (SqlParameter param in command.Parameters)
                {
                    if (param.ParameterName.ToLower() == "@url")
                    {
                        sqlExec += param.ParameterName + "='" + req.Url + "',";
                        param.Value = req.Url.ToString();
                    }
                    else if (param.ParameterName.ToLower().StartsWith("@auth_"))
                    {
                        var tokenValue = ParseSecurityHeader.GetValue(
                            param.ParameterName.ToLower().Substring(6), req.Headers);
                        if (tokenValue != null)
                        {
                            sqlExec += param.ParameterName + "='" + tokenValue + "',";
                            param.Value = tokenValue;
                        }
                        else
                        {
                            throw new Exception("Missing or invalid security header");
                        }
                    }
                    else if (param.ParameterName.ToLower() == "@passwordhash")
                    {
                        var password = queryDict["password"];
                        if (password == null && data != null)
                        {
                            JsonNode? jsonNodeParam = data["password"];
                            password = jsonNodeParam?.ToString();
                        }

                        if (password != null)
                        {
                            var passwordHash = UpHasher.HashPassword(password.ToString());
                            if (!string.IsNullOrWhiteSpace(passwordHash))
                            {
                                param.Value = passwordHash;
                                sqlExec += "@passwordHash='" + passwordHash + "',";
                            }
                        }
                    }
                    else if (param.Direction is ParameterDirection.InputOutput or ParameterDirection.Input)
                    {
                        if (param.Value != null && param.Value != DBNull.Value)
                            sqlExec += param.ParameterName + "='" + param.Value + "',";
                    }
                }

                if (sqlExec.EndsWith(','))
                    sqlExec = sqlExec[..^1];

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.HasRows)
                    {
                        if (reader.FieldCount == 1 && reader.GetName(0).ToLower() == "json")
                        {
                            if (reader.Read())
                                jsonData = reader.GetValue(0).ToString();
                        }
                        else
                        {
                            var dataTable = new DataTable();
                            dataTable.Load(reader);

                            var tableData = dataTable.Rows.OfType<DataRow>()
                                .Select(row => dataTable.Columns.OfType<DataColumn>()
                                    .ToDictionary(col => col.ColumnName, c => row[c]));

                            var serializeOptions = new JsonSerializerOptions
                            {
                                WriteIndented = true,
                                DefaultIgnoreCondition = JsonIgnoreCondition.Never,
                                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                                Converters =
                                {
                                    new UpStringConverter(),
                                    new DbNullConverter(),
                                    new DateOnlyConverter(),
                                }
                            };

                            jsonData = JsonSerializer.Serialize(tableData, serializeOptions);
                            returned_rows = dataTable.Rows.Count;

                            if (dataTable.Columns.Contains("total_rows"))
                                total_rows = int.Parse(dataTable.Rows[0]["total_rows"].ToString()!);
                            else
                                total_rows = dataTable.Rows.Count;

                            if (dataTable.Columns.Count == 1 && dataTable.Rows.Count == 1)
                            {
                                var contentType = "application/text";
                                var strResult = dataTable.Rows[0][0].ToString();

                                reader.Close();
                                if (command.Parameters.Contains("@RETURN_VALUE") &&
                                    command.Parameters["@RETURN_VALUE"].Value != null)
                                    returnCode = (int?)command.Parameters["@RETURN_VALUE"].Value;

                                return LogAndReturn(sqlSchema, requestBody, req,
                                    new ObjectResult(strResult)
                                    {
                                        ContentTypes = [contentType],
                                        StatusCode = returnCode
                                    },
                                    sqlExec, timer, null, null, null, _logger, contentType);
                            }

                            if (dataTable.Columns.Count == 2 &&
                                dataTable.Columns[0].ColumnName == "content_type" &&
                                dataTable.Rows.Count == 1)
                            {
                                var contentType = dataTable.Rows[0][0].ToString();
                                byte[] bytes = (byte[])dataTable.Rows[0][1];
                                reader.Close();
                                if (command.Parameters.Contains("@RETURN_VALUE") &&
                                    command.Parameters["@RETURN_VALUE"].Value != null)
                                    returnCode = (int?)command.Parameters["@RETURN_VALUE"].Value;

                                return LogAndReturn(sqlSchema, requestBody, req,
                                    new ObjectResult(bytes)
                                    {
                                        ContentTypes = [contentType!],
                                        StatusCode = returnCode
                                    },
                                    sqlExec, timer, null, null, null, _logger, contentType!);
                            }

                            if (dataTable.Rows.Count == 1 &&
                                ((hasIdPath && !string.IsNullOrWhiteSpace(id)) || req.Method.ToLower() == "post"))
                            {
                                var array = JsonNode.Parse(jsonData);
                                if (array?[0] != null)
                                    jsonData = array[0]!.ToJsonString();
                            }
                        }
                    }
                    else if (string.IsNullOrEmpty(id))
                    {
                        jsonData = "[]";
                    }

                    reader.Close();
                }

                if (command.Parameters.Contains("@RETURN_VALUE") &&
                    command.Parameters["@RETURN_VALUE"].Value != null)
                    returnCode = (int?)command.Parameters["@RETURN_VALUE"].Value;

                if (returnCode < 299)
                {
                    var isCompound = false;
                    var hasBodyOutputParameter = false;
                    foreach (SqlParameter param in command.Parameters)
                    {
                        if (param.Direction is ParameterDirection.InputOutput or ParameterDirection.Output)
                        {
                            if (param.ParameterName.ToLower() == "@body")
                                hasBodyOutputParameter = true;
                            else if (param.ParameterName.ToLower() == "@total_rows")
                                total_rows = param.Value == null ? 0 : int.Parse(param.Value.ToString()!);
                            else if (param.ParameterName.ToLower() != "@ui")
                                isCompound = true;
                        }

                        if (hasBodyOutputParameter)
                            isCompound = false;
                    }

                    if (isCompound)
                    {
                        JsonObject responseObject = new();
                        if (!string.IsNullOrWhiteSpace(jsonData))
                            responseObject.Add("data", JsonNode.Parse(jsonData));

                        foreach (SqlParameter param in command.Parameters)
                        {
                            if (param.Direction is not (ParameterDirection.InputOutput or ParameterDirection.Output))
                                continue;

                            string paramVal = Convert.ToString(param.Value) ?? string.Empty;
                            string paramName = param.ParameterName[1..];
                            try
                            {
                                responseObject.Add(paramName, JsonNode.Parse(paramVal));
                            }
                            catch (JsonException)
                            {
                                responseObject.Add(paramName, paramVal);
                            }
                        }

                        jsonData = JsonSerializer.Serialize(responseObject);
                    }

                    if (hasBodyOutputParameter)
                        jsonData = Convert.ToString(command.Parameters["@body"].Value) ?? string.Empty;
                }
            }
            catch (SqlException ex)
            {
                string errorString = string.Empty;
                for (int i = 0; i < ex.Errors.Count; i++)
                    errorString += ex.Errors[i].Message + "\n";
                _logger.LogError("SqlException: {Error}", errorString);
                if (command.Parameters.Contains("@RETURN_VALUE"))
                    returnCode = (int?)command.Parameters["@RETURN_VALUE"].Value;
                if (returnCode == null || returnCode < 400)
                    returnCode = 500;

                command.Parameters.Clear();
                return LogAndReturn(sqlSchema, requestBody, req,
                    new ObjectResult(errorString) { StatusCode = returnCode },
                    sqlExec, timer, null, null, null, _logger);
            }
            catch (Exception e)
            {
                command.Parameters.Clear();
                _logger.LogError("Exception: {Message}", e.Message);
                return LogAndReturn(sqlSchema, requestBody, req,
                    new BadRequestObjectResult(e.Message ),
                    sqlExec, timer, null, null, null, _logger);
            }

            command.Parameters.Clear();
        }

        if (!string.IsNullOrWhiteSpace(jsonData))
            responseBody = jsonData;
        else if (req.Method.ToLower() != "get")
            responseBody = string.Empty;
        else if (hasIdPath)
        {
            responseBody = "[]";
            returnCode = 404;
        }
        else
            responseBody = string.Empty;

        if (returnCode == null || returnCode == 0)
            returnCode = 200;

        if (resource == "login_post")
        {
            string protocol = req.Url.Scheme;     // http or https
            string host = req.Url.Host;           // localhost or domain
            int port = req.Url.Port;              // optional
            string baseUrl = $"{req.Url.Scheme}://{req.Url.Host}:{req.Url.Port}";

            var token = ParseAndGetToken.Parse(responseBody, new[]
            {
                new Claim("baseurl", baseUrl),
                new Claim("service", service)
            });
            if (token == null)
                return req.CreateResponse(System.Net.HttpStatusCode.NotFound);

            var tokenHandler = new JwtSecurityTokenHandler();
            var jwt = tokenHandler.WriteToken(token);
            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new { token = jwt });
            return response;
        }

        return LogAndReturn(sqlSchema, requestBody, req,
            new ObjectResult(responseBody) { StatusCode = returnCode },
            sqlExec, timer, total_rows, first_row, returned_rows, _logger,
            reqContentType == "application/json" ? "application/json" : reqContentType!);
    }

    private static CachedParameter[] CloneParms(DbParameterCollection pc)
    {
        var pa = new CachedParameter[pc.Count];
        int pi = 0;
        foreach (DbParameter p in pc)
            pa[pi++] = new CachedParameter { Param = p, Value = p.Value };
        return pa;
    }

    private static HttpResponseData LogAndReturn(
        string sqlSchema,
        string? requestBody,
        HttpRequestData req,
        ObjectResult result,
        string sqlExec,
        Stopwatch? timer,
        int? total_rows = null,
        int? first_row = null,
        int? rows_returned = null,
        ILogger? log = null,
        string contentType = "application/json")
    {
        if (timer == null || !timer.IsRunning)
            return null!;

        timer.Stop();
        var ts = timer.Elapsed;
        var ll = LogLevel.Information;
        var unexpectedError = string.Empty;
        var rb = string.IsNullOrWhiteSpace(requestBody) ? string.Empty : requestBody;
        var isStringResult = result.Value is string;
        if (result.StatusCode == 0)
            result.StatusCode = 200;
        if (result.StatusCode > 299 && result.StatusCode < 500)
            ll = LogLevel.Warning;
        if (result.StatusCode >= 500)
            ll = LogLevel.Error;

        if (result.StatusCode > 299 && result.Value != null)
        {
            var message = result.StatusCode == 500 ? "Database Error" : result.Value.ToString();
            if (result.StatusCode == 500)
                unexpectedError = result.Value.ToString() ?? string.Empty;

            result.Value = JsonSerializer.Serialize(new ErrorRet { message = message });
        }

        log?.Log(ll, "{Elapsed}ms {StatusCode} {SqlExec}", ts.Milliseconds, result.StatusCode, sqlExec);
        SqlLog.Log(sqlSchema, ts.Milliseconds.ToString(),
            (int)result.StatusCode!, rb, isStringResult ? result.Value?.ToString() ?? string.Empty : string.Empty,
            sqlExec, string.Empty, unexpectedError);

        var response = req.CreateResponse((System.Net.HttpStatusCode)Convert.ToInt16(result.StatusCode));

        if (req.Method.ToLower() == "get" && result.StatusCode < 300 && total_rows != null)
        {
            response.Headers.Add("Access-Control-Expose-Headers", "Content-Range");
            var contentRange = string.Format("{0}-{1}/{2}", first_row, first_row + rows_returned, total_rows);
            response.Headers.TryAddWithoutValidation("Content-Range", contentRange);
        }

        response.Headers.Add("Content-Type", contentType);
        if (isStringResult)
            response.WriteStringAsync(result.Value?.ToString());
        else if (result.Value is byte[] bytes)
        {
            response.Headers.Add("Content-Length", bytes.Length.ToString());
            response.Body.WriteAsync(bytes, 0, bytes.Length);
        }

        return response;
    }

    private struct CachedParameter
    {
        public DbParameter Param;
        public object? Value;
    }
}
