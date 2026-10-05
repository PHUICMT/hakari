namespace Hakari.Cli.CommandLine;

public sealed class CliArguments
{
    private const string OptionPrefix = "--";

    private readonly Dictionary<string, List<string>> optionValues;

    private CliArguments(string? commandName, Dictionary<string, List<string>> optionValues)
    {
        CommandName = commandName;
        this.optionValues = optionValues;
    }

    public string? CommandName { get; }

    public static CliArguments Parse(IReadOnlyList<string> arguments)
    {
        string? commandName = null;
        var optionValues = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        for (var position = 0; position < arguments.Count; position++)
        {
            var argument = arguments[position];
            if (!argument.StartsWith(OptionPrefix, StringComparison.Ordinal))
            {
                commandName ??= argument;
                continue;
            }

            var hasValue = position + 1 < arguments.Count
                && !arguments[position + 1].StartsWith(OptionPrefix, StringComparison.Ordinal);
            var value = hasValue ? arguments[++position] : string.Empty;
            AddValue(optionValues, argument, value);
        }

        return new CliArguments(commandName, optionValues);
    }

    public bool HasFlag(string optionName) => optionValues.ContainsKey(optionName);

    public string? GetValue(string optionName) =>
        optionValues.TryGetValue(optionName, out var values) ? values[^1] : null;

    public IReadOnlyList<string> GetValues(string optionName) =>
        optionValues.TryGetValue(optionName, out var values) ? values : [];

    private static void AddValue(
        Dictionary<string, List<string>> optionValues,
        string optionName,
        string value)
    {
        if (!optionValues.TryGetValue(optionName, out var values))
        {
            values = [];
            optionValues[optionName] = values;
        }

        values.Add(value);
    }
}
