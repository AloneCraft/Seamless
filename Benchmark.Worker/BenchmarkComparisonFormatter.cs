using System.Globalization;
using System.Text;
using Microsoft.VisualBasic.FileIO;

namespace Benchmark.Runner.Uno;

public static class BenchmarkComparisonFormatter{
    public const string SystemFLinq = "SystemFLinq";
    public const string SystemZLinq = "SystemZLinq";
    public const string PersistedFLinq = "PersistedFLinq";
    public const string PersistedLinq = "PersistedLinq";
    public const string TsavoriteFLinq = "TsavoriteFLinq";
    public const string TsavoriteLinq = "TsavoriteLinq";

    public static readonly string[] ProjectOrder = [
        SystemFLinq,
        SystemZLinq,
        PersistedFLinq,
        PersistedLinq,
        TsavoriteFLinq,
        TsavoriteLinq,
    ];

    public static readonly ComparisonPairDefinition[] PairDefinitions = [
        new("System", "System比", "SystemFLinq/SystemZLinq", SystemFLinq, SystemZLinq),
        new("Persisted", "Persisted比", "PersistedFLinq/PersistedLinq", PersistedFLinq, PersistedLinq),
        new("Tsavorite", "Tsavorite比", "TsavoriteFLinq/TsavoriteLinq", TsavoriteFLinq, TsavoriteLinq),
        new("PersistedLinqTsavoriteLinq", "PersistedLinq/TsavoriteLinq比", "PersistedLinq/TsavoriteLinq", PersistedLinq, TsavoriteLinq),
        new("PersistedFLinqTsavoriteFLinq", "PersistedFLinq/TsavoriteFLinq比", "PersistedFLinq/TsavoriteFLinq", PersistedFLinq, TsavoriteFLinq),
    ];

    private static readonly string[] csvHeader = [
        "ベンチマーク",
        "SystemFLinq時間ns",
        "SystemFLinq割当bytes",
        "SystemZLinq時間ns",
        "SystemZLinq割当bytes",
        "PersistedFLinq時間ns",
        "PersistedFLinq割当bytes",
        "PersistedLinq時間ns",
        "PersistedLinq割当bytes",
        "TsavoriteFLinq時間ns",
        "TsavoriteFLinq割当bytes",
        "TsavoriteLinq時間ns",
        "TsavoriteLinq割当bytes",
    ];

    private static readonly HashSet<string> legacyProjectNames = new(StringComparer.OrdinalIgnoreCase){
        "System LINQ",
        "SystemLINQ",
        "System FLinq",
        "System ZLinq",
        "Persisted FLinq",
        "Persisted LINQ",
        "Tsavorite FLinq",
        "Tsavorite LINQ",
    };

    public static IReadOnlyList<BenchmarkComparisonRow> BuildComparisonRows(IReadOnlyList<ReportRow> rows){
        EnsureSupportedRows(rows);
        var rowsByMethod = new Dictionary<string,Dictionary<string,ProjectMetrics>>(StringComparer.OrdinalIgnoreCase);
        foreach(var row in rows){
            var project = NormalizeProjectName(row.Project);
            if(project is null){
                continue;
            }

            if(!rowsByMethod.TryGetValue(row.Method,out var metricsByProject)){
                metricsByProject = new Dictionary<string,ProjectMetrics>(StringComparer.OrdinalIgnoreCase);
                rowsByMethod.Add(row.Method,metricsByProject);
            }

            metricsByProject[project] = CreateMetrics(row);
        }

        return rowsByMethod
            .OrderBy(static item => item.Key,StringComparer.OrdinalIgnoreCase)
            .Select(static item => CreateRow(item.Key,item.Value))
            .ToArray();
    }

    public static string WriteCsv(IReadOnlyList<BenchmarkComparisonRow> rows){
        var writer = new StringBuilder();
        writer.AppendLine(string.Join(",",csvHeader));
        foreach(var row in rows.OrderBy(static row => row.Benchmark,StringComparer.OrdinalIgnoreCase)){
            writer.AppendCsv(row.Benchmark);
            foreach(var project in ProjectOrder){
                var metrics = row.GetProject(project);
                writer.Append(',');
                if(metrics.HasValue&&metrics.TimeNanoseconds is { } time){
                    writer.Append(time.ToString("G17",CultureInfo.InvariantCulture));
                }

                writer.Append(',');
                if(metrics.HasValue&&metrics.AllocatedBytes is { } allocated){
                    writer.Append(allocated.ToString(CultureInfo.InvariantCulture));
                }
            }

            writer.AppendLine();
        }

        return writer.ToString();
    }

    public static IReadOnlyList<BenchmarkComparisonRow> ReadCsv(string csv){
        if(string.IsNullOrWhiteSpace(csv)){
            return [];
        }

        using var reader = new StringReader(csv);
        using var parser = new TextFieldParser(reader){
            TextFieldType=FieldType.Delimited,
            HasFieldsEnclosedInQuotes=true,
            TrimWhiteSpace=false,
        };
        parser.SetDelimiters(",");
        if(parser.EndOfData){
            return [];
        }

        var header = parser.ReadFields()??[];
        if(header.Length>0){
            header[0]=header[0].TrimStart('\uFEFF');
        }

        EnsureNewCsvHeader(header);
        var result = new List<BenchmarkComparisonRow>();
        while(!parser.EndOfData){
            var fields = parser.ReadFields()??[];
            if(fields.Length==0||fields.All(string.IsNullOrWhiteSpace)){
                continue;
            }

            var method = GetCell(fields,0);
            if(string.IsNullOrWhiteSpace(method)){
                throw new InvalidDataException("新形式CSVのみ対応しています。ベンチマーク名が空です。");
            }

            var metrics = new Dictionary<string,ProjectMetrics>(StringComparer.OrdinalIgnoreCase);
            for(var projectIndex=0;projectIndex<ProjectOrder.Length;projectIndex++){
                var timeIndex = 1+(projectIndex*2);
                var allocatedIndex = timeIndex+1;
                var timeText = GetCell(fields,timeIndex);
                var allocatedText = GetCell(fields,allocatedIndex);
                if(string.IsNullOrWhiteSpace(timeText)&&string.IsNullOrWhiteSpace(allocatedText)){
                    continue;
                }

                var time = ParseDoubleCell(timeText,csvHeader[timeIndex]);
                var allocated = ParseLongCell(allocatedText,csvHeader[allocatedIndex]);
                metrics[ProjectOrder[projectIndex]]=new ProjectMetrics(time,allocated,true);
            }

            result.Add(CreateRow(method,metrics));
        }

        return result;
    }

    public static RatioValue CalculateTimeRatio(ProjectMetrics left,ProjectMetrics right){
        if(!left.HasValue||!right.HasValue||left.TimeNanoseconds is not { } leftValue||right.TimeNanoseconds is not { } rightValue||leftValue<=0||rightValue<=0){
            return RatioValue.Missing;
        }

        return RatioValue.FromNumeric(leftValue/rightValue);
    }

    public static RatioValue CalculateAllocatedRatio(ProjectMetrics left,ProjectMetrics right){
        if(!left.HasValue||!right.HasValue||left.AllocatedBytes is not { } leftValue||right.AllocatedBytes is not { } rightValue){
            return RatioValue.Missing;
        }

        if(leftValue==0&&rightValue==0){
            return new RatioValue(RatioValueKind.BothZero,null,"○",RatioJudgement.Tie);
        }

        if(leftValue>0&&rightValue==0){
            return new RatioValue(RatioValueKind.LeftOnly,null,"＋",RatioJudgement.LeftLose);
        }

        if(leftValue==0&&rightValue>0){
            return new RatioValue(RatioValueKind.RightOnly,null,"−",RatioJudgement.LeftWin);
        }

        return RatioValue.FromNumeric((double)leftValue/rightValue);
    }

    public static PairComparison CreatePairComparison(BenchmarkComparisonRow row,ComparisonPairDefinition pair){
        var left = row.GetProject(pair.LeftProject);
        var right = row.GetProject(pair.RightProject);
        return new PairComparison(
            pair,
            left,
            right,
            CalculateTimeRatio(left,right),
            CalculateAllocatedRatio(left,right));
    }

    private static BenchmarkComparisonRow CreateRow(string method,IReadOnlyDictionary<string,ProjectMetrics> metrics){
        var row = new BenchmarkComparisonRow(
            method,
            metrics.GetValueOrDefault(SystemFLinq),
            metrics.GetValueOrDefault(SystemZLinq),
            metrics.GetValueOrDefault(PersistedFLinq),
            metrics.GetValueOrDefault(PersistedLinq),
            metrics.GetValueOrDefault(TsavoriteFLinq),
            metrics.GetValueOrDefault(TsavoriteLinq));
        var system = CreatePairComparison(row,PairDefinitions[0]);
        var persisted = CreatePairComparison(row,PairDefinitions[1]);
        var tsavorite = CreatePairComparison(row,PairDefinitions[2]);
        var persistedLinqTsavoriteLinq = CreatePairComparison(row,PairDefinitions[3]);
        var persistedFLinqTsavoriteFLinq = CreatePairComparison(row,PairDefinitions[4]);
        return row with{
            SystemRatio = system,
            PersistedRatio = persisted,
            TsavoriteRatio = tsavorite,
            PersistedLinqTsavoriteLinqRatio = persistedLinqTsavoriteLinq,
            PersistedFLinqTsavoriteFLinqRatio = persistedFLinqTsavoriteFLinq,
        };
    }

    private static ProjectMetrics CreateMetrics(ReportRow row){
        if(!string.Equals(row.Status,"Completed",StringComparison.OrdinalIgnoreCase)){
            return ProjectMetrics.Missing;
        }

        var time = row.MeanNanoseconds>0 ? row.MeanNanoseconds : (double?)null;
        return new ProjectMetrics(time,row.AllocatedBytesValue,true);
    }

    private static string? NormalizeProjectName(string project){
        if(legacyProjectNames.Contains(project)){
            throw new InvalidDataException("新形式CSVのみ対応しています。旧プロジェクト名またはSystemLINQ/System LINQを含むデータは読み込めません。");
        }

        return ProjectOrder.Contains(project,StringComparer.OrdinalIgnoreCase)
            ? ProjectOrder.First(item => string.Equals(item,project,StringComparison.OrdinalIgnoreCase))
            : null;
    }

    private static void EnsureSupportedRows(IReadOnlyList<ReportRow> rows){
        foreach(var row in rows){
            _ = NormalizeProjectName(row.Project);
        }
    }

    private static void EnsureNewCsvHeader(string[] header){
        if(header.Any(item => legacyProjectNames.Contains(item))
            ||header.Any(static item => string.Equals(item,"Project",StringComparison.OrdinalIgnoreCase))
            ||header.Any(static item => string.Equals(item,"Method",StringComparison.OrdinalIgnoreCase))
            ||header.Any(static item => string.Equals(item,"Mean",StringComparison.OrdinalIgnoreCase))){
            throw new InvalidDataException("新形式CSVのみ対応しています。旧CSV形式は読み込めません。");
        }

        if(header.Length!=csvHeader.Length){
            throw new InvalidDataException("新形式CSVのみ対応しています。列数が一致しません。");
        }

        for(var i=0;i<csvHeader.Length;i++){
            if(!string.Equals(header[i],csvHeader[i],StringComparison.Ordinal)){
                throw new InvalidDataException($"新形式CSVのみ対応しています。列 '{csvHeader[i]}' が必要です。");
            }
        }
    }

    private static string GetCell(string[] row,int index){
        return index>=0&&index<row.Length
            ? row[index]
            : string.Empty;
    }

    private static double? ParseDoubleCell(string value,string column){
        if(string.IsNullOrWhiteSpace(value)){
            return null;
        }

        if(double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out var result)&&double.IsFinite(result)){
            return result;
        }

        throw new InvalidDataException($"新形式CSVのみ対応しています。列 '{column}' は数値である必要があります。");
    }

    private static long? ParseLongCell(string value,string column){
        if(string.IsNullOrWhiteSpace(value)){
            return null;
        }

        if(long.TryParse(value,NumberStyles.Integer,CultureInfo.InvariantCulture,out var result)){
            return result;
        }

        throw new InvalidDataException($"新形式CSVのみ対応しています。列 '{column}' は整数である必要があります。");
    }
}

public readonly record struct ProjectMetrics(double? TimeNanoseconds,long? AllocatedBytes,bool HasValue){
    public static readonly ProjectMetrics Missing = new(null,null,false);
}

public readonly record struct RatioValue(RatioValueKind Kind,double? NumericValue,string DisplayText,RatioJudgement Judgement){
    public static readonly RatioValue Missing = new(RatioValueKind.Missing,null,"-",RatioJudgement.None);

    public bool IsNumeric => this.Kind==RatioValueKind.Numeric&&this.NumericValue is not null;
    public bool IsSaturatedHigh => this.Kind==RatioValueKind.LeftOnly||this.NumericValue>=2.0;
    public bool IsSaturatedLow => this.Kind==RatioValueKind.RightOnly||this.NumericValue<=0.5;
    public bool IsMissing => this.Kind==RatioValueKind.Missing;

    public static RatioValue FromNumeric(double value){
        var judgement = value<0.95
            ? RatioJudgement.LeftWin
            : value>1.05
                ? RatioJudgement.LeftLose
                : RatioJudgement.Tie;
        return new RatioValue(RatioValueKind.Numeric,value,value.ToString("0.00",CultureInfo.InvariantCulture),judgement);
    }
}

public readonly record struct PairComparison(
    ComparisonPairDefinition Definition,
    ProjectMetrics Left,
    ProjectMetrics Right,
    RatioValue TimeRatio,
    RatioValue AllocatedRatio);

public readonly record struct BenchmarkComparisonRow(
    string Benchmark,
    ProjectMetrics SystemFLinq,
    ProjectMetrics SystemZLinq,
    ProjectMetrics PersistedFLinq,
    ProjectMetrics PersistedLinq,
    ProjectMetrics TsavoriteFLinq,
    ProjectMetrics TsavoriteLinq){
    public PairComparison SystemRatio { get; init; }
    public PairComparison PersistedRatio { get; init; }
    public PairComparison TsavoriteRatio { get; init; }
    public PairComparison PersistedLinqTsavoriteLinqRatio { get; init; }
    public PairComparison PersistedFLinqTsavoriteFLinqRatio { get; init; }

    public ProjectMetrics GetProject(string project){
        return project switch{
            BenchmarkComparisonFormatter.SystemFLinq => this.SystemFLinq,
            BenchmarkComparisonFormatter.SystemZLinq => this.SystemZLinq,
            BenchmarkComparisonFormatter.PersistedFLinq => this.PersistedFLinq,
            BenchmarkComparisonFormatter.PersistedLinq => this.PersistedLinq,
            BenchmarkComparisonFormatter.TsavoriteFLinq => this.TsavoriteFLinq,
            BenchmarkComparisonFormatter.TsavoriteLinq => this.TsavoriteLinq,
            _ => ProjectMetrics.Missing,
        };
    }

    public PairComparison GetPair(string key){
        return key switch{
            "System" => this.SystemRatio,
            "Persisted" => this.PersistedRatio,
            "Tsavorite" => this.TsavoriteRatio,
            "PersistedLinqTsavoriteLinq" => this.PersistedLinqTsavoriteLinqRatio,
            "PersistedFLinqTsavoriteFLinq" => this.PersistedFLinqTsavoriteFLinqRatio,
            _ => default,
        };
    }
}

public readonly record struct ComparisonPairDefinition(
    string Key,
    string DisplayName,
    string TooltipText,
    string LeftProject,
    string RightProject);

public enum RatioValueKind{
    Numeric,
    BothZero,
    LeftOnly,
    RightOnly,
    Missing,
}

public enum RatioJudgement{
    None,
    LeftWin,
    LeftLose,
    Tie,
}
