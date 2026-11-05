using System.Text.Json;
using System.Text.Json.Serialization;

namespace HabitGrid.Core.Domain;

/// <summary>Tuần tự hóa <see cref="Hlc"/> thành chuỗi sắp xếp được.</summary>
public sealed class HlcJsonConverter : JsonConverter<Hlc>
{
    public override Hlc Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.GetString();
        return Hlc.TryParse(text, out var value)
            ? value
            : throw new JsonException($"HLC không hợp lệ: '{text}'.");
    }

    public override void Write(Utf8JsonWriter writer, Hlc value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}

/// <summary>Tuần tự hóa <see cref="HabitIcon"/> thành chuỗi (<c>fluent:…</c> hoặc <c>emoji:…</c>).</summary>
public sealed class HabitIconJsonConverter : JsonConverter<HabitIcon>
{
    public override HabitIcon Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.GetString());

    public override void Write(Utf8JsonWriter writer, HabitIcon value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);
}

/// <summary>Cấu hình JSON dùng chung cho lưu trữ, sao lưu và đồng bộ để mọi nơi cùng một định dạng.</summary>
public static class DomainJson
{
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };

        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
