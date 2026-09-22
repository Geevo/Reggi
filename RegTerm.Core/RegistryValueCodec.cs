using System.Globalization;
using System.Text;

namespace RegTerm.Core;

/// <summary>Which base a DWORD or QWORD is being read and written in.</summary>
public enum NumberBase
{
    Hexadecimal,
    Decimal
}

/// <summary>
/// Formats and parses registry values.
/// <see cref="ToEditable"/> and <see cref="Parse"/> are inverses of each other for a
/// given <see cref="NumberBase"/>.
/// </summary>
public static class RegistryValueCodec
{
    /// <summary>Separator used when a REG_MULTI_SZ is flattened into one edit box.</summary>
    public const char MultiStringSeparator = ';';

    private const int _maxBinaryBytesShown = 128;

    /// <summary>Kinds offered when creating a new value, in the order they are listed.</summary>
    public static readonly RegistryValueType[] CreatableKinds =
    [
        RegistryValueType.String,
        RegistryValueType.ExpandString,
        RegistryValueType.MultiString,
        RegistryValueType.DWord,
        RegistryValueType.QWord,
        RegistryValueType.Binary
    ];

    /// <summary>True for the kinds that have a selectable number base.</summary>
    public static bool IsNumeric(RegistryValueType kind) => kind is RegistryValueType.DWord or RegistryValueType.QWord;

    public static string TypeName(RegistryValueType kind) => kind switch
    {
        RegistryValueType.String => "REG_SZ",
        RegistryValueType.ExpandString => "REG_EXPAND_SZ",
        RegistryValueType.MultiString => "REG_MULTI_SZ",
        RegistryValueType.DWord => "REG_DWORD",
        RegistryValueType.QWord => "REG_QWORD",
        RegistryValueType.Binary => "REG_BINARY",
        RegistryValueType.None => "REG_NONE",
        _ => "REG_UNKNOWN"
    };

    /// <summary>Human-readable rendering for the Data column. Not round-trippable.</summary>
    public static string Format(RegistryValueType kind, object? value)
    {
        if (value is null) return string.Empty;

        return kind switch
        {
            RegistryValueType.DWord when value is int i =>
                string.Create(CultureInfo.InvariantCulture, $"0x{(uint)i:x8} ({(uint)i})"),
            RegistryValueType.QWord when value is long l =>
                string.Create(CultureInfo.InvariantCulture, $"0x{(ulong)l:x16} ({(ulong)l})"),
            RegistryValueType.MultiString when value is string[] parts => parts.Length == 0
                ? "(empty)"
                : string.Join("  ", parts),
            RegistryValueType.Binary or RegistryValueType.None or RegistryValueType.Unknown
                when value is byte[] bytes => FormatBytes(bytes),
            _ => value as string ?? value.ToString() ?? string.Empty
        };
    }

    /// <summary>
    /// Round-trippable rendering for an edit box.
    /// </summary>
    public static string ToEditable(RegistryValueType kind, object? value, NumberBase numberBase = NumberBase.Decimal)
    {
        if (value is null) return string.Empty;

        return kind switch
        {
            RegistryValueType.DWord when value is int i => numberBase == NumberBase.Hexadecimal
                ? ((uint)i).ToString("x", CultureInfo.InvariantCulture)
                : ((uint)i).ToString(CultureInfo.InvariantCulture),

            RegistryValueType.QWord when value is long l => numberBase == NumberBase.Hexadecimal
                ? ((ulong)l).ToString("x", CultureInfo.InvariantCulture)
                : ((ulong)l).ToString(CultureInfo.InvariantCulture),

            RegistryValueType.MultiString when value is string[] parts => FormatMultiString(parts),
            RegistryValueType.Binary or RegistryValueType.None or RegistryValueType.Unknown
                when value is byte[] bytes => ToHex(bytes),
            _ => value as string ?? value.ToString() ?? string.Empty
        };
    }

    /// <summary>
    /// Parses edit-box text, throwing <see cref="FormatException"/> for invalid input.
    /// </summary>
    public static object Parse(
        RegistryValueType kind, string text, NumberBase numberBase = NumberBase.Decimal) => kind switch
        {
            RegistryValueType.DWord => unchecked((int)ParseUnsigned(text, uint.MaxValue, "32-bit", numberBase)),
            RegistryValueType.QWord => unchecked((long)ParseUnsigned(text, ulong.MaxValue, "64-bit", numberBase)),
            RegistryValueType.Binary or RegistryValueType.None => ParseHex(text),
            RegistryValueType.MultiString => ParseMultiString(text),
            RegistryValueType.Unknown =>
                throw new FormatException("Unsupported registry data types cannot be edited safely."),
            _ => text
        };

    /// <summary>
    /// Returns the searchable representations of a value.
    /// </summary>
    public static IEnumerable<string> SearchableForms(RegistryValueType kind, object? value,
                                                      bool includeHexForm = false)
    {
        if (value is null) yield break;

        switch (kind)
        {
            case RegistryValueType.DWord when value is int i:
                yield return ((uint)i).ToString(CultureInfo.InvariantCulture);
                if (includeHexForm)
                    yield return string.Create(CultureInfo.InvariantCulture, $"0x{(uint)i:x8}");
                break;

            case RegistryValueType.QWord when value is long l:
                yield return ((ulong)l).ToString(CultureInfo.InvariantCulture);
                if (includeHexForm)
                    yield return string.Create(CultureInfo.InvariantCulture, $"0x{(ulong)l:x16}");
                break;

            case RegistryValueType.MultiString when value is string[] parts:
                foreach (var part in parts) yield return part;
                break;

            default:
                yield return ToEditable(kind, value);
                break;
        }
    }

    private static string FormatBytes(byte[] bytes)
    {
        if (bytes.Length == 0) return "(zero-length)";
        var shown = Math.Min(bytes.Length, _maxBinaryBytesShown);
        var hex = ToHex(bytes.AsSpan(0, shown));
        return shown == bytes.Length ? hex : $"{hex} ... ({bytes.Length} bytes)";
    }

    private static string ToHex(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0) return string.Empty;
        return string.Create(bytes.Length * 3 - 1, bytes.ToArray(), static (span, src) =>
        {
            for (var i = 0; i < src.Length; i++)
            {
                if (i > 0) span[i * 3 - 1] = ' ';
                var pos = i * 3;
                span[pos] = ToHexDigit(src[i] >> 4);
                span[pos + 1] = ToHexDigit(src[i] & 0xF);
            }
        });
    }

    private static char ToHexDigit(int nibble) => (char)(nibble < 10 ? '0' + nibble : 'A' + nibble - 10);

    /// <summary>
    /// Encodes multi-string separators, backslashes and empty entries.
    /// </summary>
    private static string FormatMultiString(IEnumerable<string> parts) =>
        string.Join(MultiStringSeparator, parts.Select(EncodeMultiStringPart));

    private static string EncodeMultiStringPart(string part)
    {
        if (part.Length == 0) return @"\0";
        return part.Replace(@"\", @"\\", StringComparison.Ordinal)
                   .Replace(";", @"\;", StringComparison.Ordinal);
    }

    private static string[] ParseMultiString(string text)
    {
        if (text.Length == 0) return [];

        var parts = new List<string>();
        var encoded = new StringBuilder();

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == MultiStringSeparator)
            {
                parts.Add(DecodeMultiStringPart(encoded.ToString()));
                encoded.Clear();
                continue;
            }

            encoded.Append(ch);
            if (ch == '\\' && i + 1 < text.Length)
                encoded.Append(text[++i]);
        }

        parts.Add(DecodeMultiStringPart(encoded.ToString()));
        return parts.ToArray();
    }

    private static string DecodeMultiStringPart(string encoded)
    {
        if (encoded == @"\0") return string.Empty;

        var decoded = new StringBuilder(encoded.Length);
        for (var i = 0; i < encoded.Length; i++)
        {
            var ch = encoded[i];
            if (ch == '\\' && i + 1 < encoded.Length && encoded[i + 1] is '\\' or MultiStringSeparator)
            {
                decoded.Append(encoded[++i]);
                continue;
            }

            decoded.Append(ch);
        }
        return decoded.ToString();
    }

    /// <summary>
    /// Parses an unsigned integer, with an optional 0x prefix overriding <paramref name="numberBase"/>.
    /// </summary>
    private static ulong ParseUnsigned(string text, ulong max, string label, NumberBase numberBase)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0) return 0;

        var negative = trimmed.StartsWith('-');
        if (negative) trimmed = trimmed[1..];

        var hex = numberBase == NumberBase.Hexadecimal;
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            hex = true;
            trimmed = trimmed[2..];
        }

        var style = hex ? NumberStyles.HexNumber : NumberStyles.None;
        if (!ulong.TryParse(trimmed, style, CultureInfo.InvariantCulture, out var parsed) || parsed > max)
        {
            var expected = hex ? "hexadecimal" : "decimal";
            throw new FormatException($"'{text}' is not a valid {label} {expected} value.");
        }

        if (!negative) return parsed;

        // Signed input is stored using the corresponding unsigned bit pattern.
        if (parsed > max / 2 + 1)
            throw new FormatException($"'{text}' is out of range for a {label} value.");
        return unchecked(0UL - parsed) & max;
    }

    private static byte[] ParseHex(string text)
    {
        var cleaned = string.Concat(text.Where(c => !char.IsWhiteSpace(c)));
        if (cleaned.Length == 0) return [];
        if (cleaned.Length % 2 != 0)
            throw new FormatException("Binary data needs an even number of hex digits.");

        var bytes = new byte[cleaned.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            if (!byte.TryParse(
                    cleaned.AsSpan(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out bytes[i]))
                throw new FormatException($"'{cleaned.Substring(i * 2, 2)}' is not a valid hex byte.");
        }
        return bytes;
    }
}
