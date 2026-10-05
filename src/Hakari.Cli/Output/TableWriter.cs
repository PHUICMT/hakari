namespace Hakari.Cli.Output;

public sealed class TableWriter(params string[] headers)
{
    private const string ColumnGap = "  ";

    private readonly List<string[]> rows = [];

    public void AddRow(params string[] cells) => rows.Add(cells);

    public void WriteTo(TextWriter output)
    {
        var widths = MeasureColumnWidths();
        WriteRow(output, headers, widths);

        foreach (var row in rows)
        {
            WriteRow(output, row, widths);
        }
    }

    private int[] MeasureColumnWidths() =>
        headers
            .Select((header, column) => rows
                .Select(row => row[column].Length)
                .Append(header.Length)
                .Max())
            .ToArray();

    private static void WriteRow(TextWriter output, string[] cells, int[] widths)
    {
        var alignedCells = cells.Select((cell, column) => column == 0
            ? cell.PadRight(widths[column])
            : cell.PadLeft(widths[column]));

        output.WriteLine(string.Join(ColumnGap, alignedCells));
    }
}
