using System.Runtime.CompilerServices;
using System.Text;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Reports;

namespace Benchmark;

public static class SummaryTableExtensions
{
    // Custom Summary table rendering logics (Disable row highlight and suppress unrequired log outputs)
    public static void RenderToConsole(this SummaryTable table, ILogger logger)
    {
        if (table.FullContent.Length == 0)
        {
            logger.WriteLineError("There are no benchmarks found ");
            logger.WriteLine();
            return;
        }

        // table.PrintCommonColumns(logger);
        if (table.Columns.All(c => !c.NeedToShow))
        {
            logger.WriteLine();
            logger.WriteLine("There are no columns to show ");
            return;
        }

        // Render table header
        logger.WriteStatistic("| ");
        PrintTableHeader(table, table.FullHeader, logger, string.Empty, " | ");

        // Render table header separater.
        var columns = table.Columns.Where(c => c.NeedToShow).ToArray();
        var sb = new StringBuilder(4096);
        sb.Append("|-");
        foreach (var column in columns.SkipLast(1))
        {
            sb.Append('-', column.Width - 1);
            sb.Append(column.OriginalColumn.IsNumeric ? ':' : ' ');
            sb.Append("|-");
        }

        var lastColumn = columns.Last();
        sb.Append('-', lastColumn.Width - 1);
        sb.Append(lastColumn.OriginalColumn.IsNumeric ? ":|" : "|");
        sb.AppendLine();

        // Render table data
        int rowCounter = 0;
        var separatorLine = Enumerable.Range(0, table.ColumnCount).Select(_ => "").ToArray();
        foreach (var line in table.FullContent)
        {
            // Print logical separator
            if (rowCounter > 0 && table.FullContentStartOfLogicalGroup[rowCounter] && table.SeparateLogicalGroups)
            {
                sb.Append("| ");
                PrintColumns(table, separatorLine, sb, string.Empty, string.Empty);
            }

            sb.Append("| ");
            PrintColumns(table, line, sb, string.Empty, " | ");

            rowCounter++;
        }

        logger.WriteLineStatistic(sb.ToString());
    }

    private static void PrintTableHeader(SummaryTable table, string[] line, ILogger logger, string leftDel, string rightDel)
    {
        for (int columnIndex = 0; columnIndex < table.ColumnCount; columnIndex++)
        {
            if (table.Columns[columnIndex].NeedToShow)
            {
                logger.WriteStatistic(BuildStandardText(table, line, leftDel, rightDel, columnIndex));
            }
        }

        logger.WriteLine();
    }

    public static void PrintColumns(SummaryTable table, string[] line, StringBuilder builder, string leftDel, string rightDel)
    {
        for (int columnIndex = 0; columnIndex < table.ColumnCount; columnIndex++)
        {
            if (table.Columns[columnIndex].NeedToShow)
            {
                builder.Append(BuildStandardText(table, line, leftDel, rightDel, columnIndex));
            }
        }

        builder.AppendLine();
    }

    private static string BuildStandardText(SummaryTable table, string[] line, string leftDel, string rightDel, int columnIndex)
    {
        var buffer = new StringBuilder(64);
        var isBuildingHeader = table.FullHeader[columnIndex] == line[columnIndex];
        var columnJustification = isBuildingHeader
            ? SummaryTable.SummaryTableColumn.TextJustification.Left
            : table.Columns[columnIndex].Justify;

        buffer.Append(leftDel);
        if (columnJustification == SummaryTable.SummaryTableColumn.TextJustification.Right)
        {
            AddPadding(table, line, leftDel, rightDel, columnIndex, buffer);
        }

        buffer.Append(line[columnIndex]);

        if (columnJustification == SummaryTable.SummaryTableColumn.TextJustification.Left)
        {
            AddPadding(table, line, leftDel, rightDel, columnIndex, buffer);
        }
        var isLastColumn = columnIndex == table.ColumnCount - 1;
        buffer.Append(isLastColumn ? rightDel.TrimEnd() : rightDel);

        return buffer.ToString();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddPadding(SummaryTable table, string[] line, string leftDel, string rightDel, int columnIndex, StringBuilder buffer)
    {
        const char space = ' ';
        const int extraWidth = 2; // " |".Length is not included in the column's Width

        int repeatCount = table.Columns[columnIndex].Width
                               + extraWidth
                               - leftDel.Length
                               - line[columnIndex].Length
                               - rightDel.Length;
        if (repeatCount > 0)
        {
            buffer.Append(space, repeatCount);
        }
    }
}
