namespace Dynastia.App.ViewModels;

public static class ThoughtUiFormatter
{
    public static string QuoteAndWrap(
        string? text,
        int maximumLineLength = 46)
    {
        if (string.IsNullOrWhiteSpace(
                text))
        {
            return string.Empty;
        }

        var words =
            text.Trim()
                .Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries);

        var lines =
            new List<string>();

        var current =
            new List<string>();

        var currentLength =
            0;

        foreach (var word in words)
        {
            var nextLength =
                currentLength
                + (
                    current.Count > 0
                        ? 1
                        : 0
                )
                + word.Length;

            if (current.Count > 0
                && nextLength
                    > maximumLineLength)
            {
                lines.Add(
                    string.Join(
                        " ",
                        current));

                current.Clear();
                currentLength =
                    0;
            }

            current.Add(
                word);

            currentLength =
                currentLength
                + (
                    current.Count > 1
                        ? 1
                        : 0
                )
                + word.Length;
        }

        if (current.Count > 0)
        {
            lines.Add(
                string.Join(
                    " ",
                    current));
        }

        if (lines.Count == 0)
            return string.Empty;

        lines[0] =
            "“"
            + lines[0];

        lines[^1] +=
            "”";

        return string.Join(
            Environment.NewLine,
            lines);
    }
}
