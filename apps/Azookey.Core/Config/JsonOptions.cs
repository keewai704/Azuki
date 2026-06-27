using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Azookey.Core.Config;

public static class AzookeyJson
{
    public static readonly JsonSerializerOptions Options = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };
        options.Converters.Add(new JsonStringEnumMemberConverter());
        return options;
    }
}

public sealed class JsonStringEnumMemberConverter : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        Type converterType = typeof(JsonStringEnumMemberConverterInner<>).MakeGenericType(typeToConvert);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }
}

internal sealed class JsonStringEnumMemberConverterInner<TEnum> : JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    private static readonly Dictionary<string, TEnum> FromJson = BuildFromJson();
    private static readonly Dictionary<TEnum, string> ToJson = BuildToJson();

    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string? value = reader.GetString();
        if (value is not null && FromJson.TryGetValue(value, out TEnum result))
        {
            return result;
        }

        throw new JsonException($"Unknown {typeof(TEnum).Name} value '{value}'.");
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(ToJson[value]);
    }

    private static Dictionary<string, TEnum> BuildFromJson()
    {
        var map = new Dictionary<string, TEnum>(StringComparer.Ordinal);
        foreach (TEnum value in Enum.GetValues<TEnum>())
        {
            string name = ToSnakeCase(value.ToString());
            map[name] = value;

            EnumMemberAttribute? attribute = typeof(TEnum)
                .GetMember(value.ToString())[0]
                .GetCustomAttribute<EnumMemberAttribute>();
            if (!string.IsNullOrWhiteSpace(attribute?.Value))
            {
                map[attribute.Value] = value;
            }
        }

        return map;
    }

    private static Dictionary<TEnum, string> BuildToJson()
    {
        return Enum.GetValues<TEnum>().ToDictionary(value => value, value => ToSnakeCase(value.ToString()));
    }

    private static string ToSnakeCase(string value)
    {
        var chars = new List<char>(value.Length + 8);
        for (int index = 0; index < value.Length; index++)
        {
            char current = value[index];
            if (char.IsUpper(current) && index > 0)
            {
                chars.Add('_');
            }

            chars.Add(char.ToLowerInvariant(current));
        }

        return new string(chars.ToArray());
    }
}
