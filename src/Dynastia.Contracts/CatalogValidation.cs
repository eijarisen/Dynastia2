using System.Globalization;
using System.Text.Json;

namespace Dynastia.Contracts;

public static class CatalogValidation
{
    public static InvalidDataException Error(
        string path,
        string expected,
        int? row = null,
        string? item = null,
        string? field = null,
        object? value = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(expected);

        var location = path;
        if (row is int sourceRow)
            location += $" row {sourceRow}";
        if (!string.IsNullOrWhiteSpace(item))
            location += $" item '{item}'";
        if (!string.IsNullOrWhiteSpace(field))
            location += $" field '{field}'";

        return new InvalidDataException(
            $"{location}: invalid value {FormatValue(value)}; expected {expected}.");
    }

    public static InvalidDataException UnexpectedHeader(
        string path,
        string? actual,
        string expected) =>
        Error(
            path,
            $"header '{expected}'",
            row: 1,
            field: "Header",
            value: actual);

    public static InvalidDataException FieldCount(
        string path,
        int row,
        int actual,
        int expected) =>
        Error(
            path,
            $"exactly {expected} fields",
            row,
            field: "FieldCount",
            value: actual);

    public static int ParseInt(
        string path,
        int row,
        string field,
        string value)
    {
        if (int.TryParse(
                value.Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var parsed))
        {
            return parsed;
        }

        throw Error(
            path,
            "an integer",
            row,
            field: field,
            value: value);
    }

    public static double ParseDouble(
        string path,
        int row,
        string field,
        string value)
    {
        if (double.TryParse(
                value.Trim(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var parsed))
        {
            return parsed;
        }

        throw Error(
            path,
            "a number",
            row,
            field: field,
            value: value);
    }

    public static decimal ParseDecimal(
        string path,
        int row,
        string field,
        string value)
    {
        if (decimal.TryParse(
                value.Trim(),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var parsed))
        {
            return parsed;
        }

        throw Error(
            path,
            "a decimal number",
            row,
            field: field,
            value: value);
    }


    public static bool ParseBool(
        string path,
        int row,
        string field,
        string value)
    {
        if (bool.TryParse(value.Trim(), out var parsed))
            return parsed;

        throw Error(
            path,
            "true or false",
            row,
            field: field,
            value: value);
    }

    public static TEnum ParseEnum<TEnum>(
        string path,
        int row,
        string field,
        string value)
        where TEnum : struct, Enum
    {
        if (Enum.TryParse<TEnum>(value.Trim(), ignoreCase: true, out var parsed)
            && Enum.IsDefined(typeof(TEnum), parsed))
        {
            return parsed;
        }

        throw Error(
            path,
            $"one of: {string.Join(", ", Enum.GetNames<TEnum>())}",
            row,
            field: field,
            value: value);
    }

    public static T DeserializeJson<T>(
        IGameDataService data,
        string path,
        JsonSerializerOptions? options = null)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            return JsonSerializer.Deserialize<T>(
                    data.ReadText(path),
                    options)
                ?? throw Error(
                    path,
                    $"JSON data compatible with {typeof(T).Name}",
                    field: "Root",
                    value: null);
        }
        catch (JsonException exception)
        {
            var row = exception.LineNumber is long lineNumber
                && lineNumber < int.MaxValue
                    ? (int)lineNumber + 1
                    : (int?)null;

            throw new InvalidDataException(
                $"{path}" +
                (row is int sourceRow ? $" row {sourceRow}" : string.Empty) +
                $" field '{exception.Path ?? "$"}': invalid JSON; expected data compatible with {typeof(T).Name}. {exception.Message}",
                exception);
        }
    }

    private static string FormatValue(object? value)
    {
        if (value is null)
            return "<null>";

        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        if (text.Length == 0)
            return "<empty>";

        text = text
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);

        return $"'{text}'";
    }
}
