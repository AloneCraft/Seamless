using System.Globalization;
using System.Text;

namespace UI;

/// <summary>
/// SQLite 分析 View の汎用行を、右ペイン分析タブ向けの等幅テキスト表へ整形する。
/// MAUI 依存を持たない純ロジックで、列は先頭行のキー順(SELECT 列順)に従う。
/// </summary>
[global::Seamless.FLinq.LinqIntercept(false)]
internal static class BenchmarkAnalysisTextFormatter{
    private const int MaxColumnWidth = 26;

    public static string Format(string title,IReadOnlyList<IReadOnlyDictionary<string,object?>> rows,int maxColumns = 12){
        var builder = new StringBuilder();
        builder.AppendLine(title);
        if(rows.Count==0){
            builder.AppendLine("(データなし。Completed Run の測定結果が保存されると表示されます)");
            return builder.ToString();
        }

        var columns = rows[0].Keys.Take(maxColumns).ToArray();
        var table = new List<string[]>(rows.Count+1){
            columns,
        };
        foreach(var row in rows){
            table.Add(columns.Select(column => FormatValue(row.TryGetValue(column,out var value) ? value : null)).ToArray());
        }

        var widths = new int[columns.Length];
        foreach(var line in table){
            for(var index = 0; index<columns.Length; index++){
                widths[index]=Math.Max(widths[index],line[index].Length);
            }
        }

        for(var lineIndex = 0; lineIndex<table.Count; lineIndex++){
            var line = table[lineIndex];
            builder.AppendLine(string.Join("  ",line.Select((cell,index) => cell.PadRight(widths[index]))));
            if(lineIndex==0){
                builder.AppendLine(string.Join("  ",widths.Select(static width => new string('-',width))));
            }
        }

        return builder.ToString();
    }

    private static string FormatValue(object? value){
        string text;
        switch(value){
            case null:
                text="-";
                break;
            case double doubleValue:
                text=doubleValue.ToString("0.###",CultureInfo.InvariantCulture);
                break;
            case float floatValue:
                text=floatValue.ToString("0.###",CultureInfo.InvariantCulture);
                break;
            case IFormattable formattable:
                text=formattable.ToString(null,CultureInfo.InvariantCulture)??"-";
                break;
            default:
                text=value.ToString()??"-";
                break;
        }
        if(text.Length>MaxColumnWidth){
            text=text.Substring(0,MaxColumnWidth-1)+"…";
        }
        return text;
    }
}
