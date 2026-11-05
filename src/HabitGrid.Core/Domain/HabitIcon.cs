namespace HabitGrid.Core.Domain;

/// <summary>Icon thói quen: <c>fluent:&lt;key&gt;</c> hoặc <c>emoji:&lt;ký tự&gt;</c>.</summary>
public readonly record struct HabitIcon
{
    public const string FluentPrefix = "fluent:";
    public const string EmojiPrefix = "emoji:";
    public const string DefaultValue = "fluent:Checkmark24Regular";

    private readonly string? _value;

    public HabitIcon(string? value)
    {
        _value = Normalize(value);
    }

    public string Value => _value ?? DefaultValue;

    public bool IsEmoji => Value.StartsWith(EmojiPrefix, StringComparison.Ordinal);

    public string Key => Value[(Value.IndexOf(':') + 1)..];

    public static HabitIcon Default => new(DefaultValue);

    public static HabitIcon Fluent(string key) => new(FluentPrefix + key);

    public static HabitIcon Emoji(string grapheme) => new(EmojiPrefix + grapheme);

    /// <summary>Nhận cả định dạng v1 (không tiền tố, ví dụ <c>Checkmark24Regular</c>) và định dạng v2.</summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DefaultValue;
        }

        var trimmed = value.Trim();

        return trimmed.StartsWith(FluentPrefix, StringComparison.Ordinal) ||
               trimmed.StartsWith(EmojiPrefix, StringComparison.Ordinal)
            ? trimmed
            : FluentPrefix + trimmed;
    }

    public override string ToString() => Value;
}
