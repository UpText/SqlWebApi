using System.Drawing.Imaging;
using System.Text.Json;
using System.Text.Json.Serialization;
//using Newtonsoft.Json.Serialization;

namespace SqlWebApi;

public class UpStringConverter : JsonConverter<string>
{
        private readonly static JsonConverter<string> s_defaultConverter = 
            (JsonConverter<string>)JsonSerializerOptions.Default.GetConverter(typeof(string));


        public override string Read(
            ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return s_defaultConverter.Read(ref reader, typeToConvert, options);
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            if (value.Length > 0 && (value[0] == '{' || value[0] == '['))
            {
                try
                {
                    using var json = JsonDocument.Parse(value);
                    json.WriteTo(writer);
                    return;
                }
                catch (JsonException)
                {
                    // Fall back to a normal string if the value only looks like JSON.
                }
            }

            writer.WriteStringValue(value.ToString());
        }
}

public class DbNullConverter : JsonConverter<DBNull>
{
    public override DBNull Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Handle deserialization if needed, e.g., if you expect null to become DBNull
        if (reader.TokenType == JsonTokenType.Null)
        {
            return DBNull.Value;
        }
        throw new JsonException("Cannot deserialize non-null value to DBNull.");
    }

    public override void Write(Utf8JsonWriter writer, DBNull value, JsonSerializerOptions options)
    {
        // Serialize DBNull.Value as JSON null
        writer.WriteNullValue();
    }
}

public sealed class DateOnlyDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => DateTime.Parse(reader.GetString()!);

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString("yyyy-MM-dd"));
}

public sealed class DateOnlyConverter : JsonConverter<DateOnly>
{
    public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => DateOnly.Parse(reader.GetString()!);

    public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString("yyyy-MM-dd"));
}
