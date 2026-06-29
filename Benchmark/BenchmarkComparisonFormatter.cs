using System.Globalization;
using System.Text;
using Microsoft.VisualBasic.FileIO;

namespace Benchmark;

[global::Seamless.FLinq.LinqIntercept(false)]
internal static class BenchmarkComparisonFormatter{
    public static readonly string[] ProjectOrder =
        BenchmarkSourceTabs.All
            .SelectMany(static tab => tab.ProjectOrder)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public static readonly ComparisonPairDefinition[] PairDefinitions =
        BenchmarkSourceTabs.All
            .Select(static tab => new ComparisonPairDefinition(
                tab.Id,
                tab.DisplayName,
                tab.LeftProject+"/"+tab.RightProject,
                tab.LeftProject,
                tab.RightProject))
            .ToArray();

    public static IReadOnlyList<BenchmarkComparisonRow> BuildComparisonRows(IReadOnlyList<ReportRow> rows){
        var tab = rows
            .Select(row => BenchmarkSourceTabs.TryGetByProject(row.Project))
            .FirstOrDefault(static item => item is not null)
            ??BenchmarkSourceTabs.Default;
        return BuildComparisonRows(rows,tab);
    }

    public static IReadOnlyList<BenchmarkComparisonRow> BuildComparisonRows(
        IReadOnlyList<ReportRow> rows,
        BenchmarkSourceTabDefinition sourceTab){
        var rowsByMethod = new Dictionary<string,Dictionary<string,ProjectMetrics>>(StringComparer.OrdinalIgnoreCase);
        foreach(var row in rows){
            if(!sourceTab.ContainsProject(row.Project)){
                throw new InvalidDataException($"Unknown benchmark project for {sourceTab.Id}: {row.Project}");
            }

            if(!rowsByMethod.TryGetValue(row.Method,out var metricsByProject)){
                metricsByProject = new Dictionary<string,ProjectMetrics>(StringComparer.OrdinalIgnoreCase);
                rowsByMethod.Add(row.Method,metricsByProject);
            }

            metricsByProject[NormalizeProjectName(row.Project,sourceTab)] = CreateMetrics(row);
        }

        return rowsByMethod
            .OrderBy(static item => item.Key,StringComparer.OrdinalIgnoreCase)
            .Select(item => CreateRow(item.Key,item.Value,sourceTab))
            .ToArray();
    }

    public static string WriteCsv(IReadOnlyList<BenchmarkComparisonRow> rows){
        var sourceTab = rows.FirstOrDefault().SourceTab;
        if(sourceTab is null){
            sourceTab=BenchmarkSourceTabs.Default;
        }
        return WriteCsv(rows,sourceTab);
    }

    public static string WriteCsv(IReadOnlyList<BenchmarkComparisonRow> rows,BenchmarkSourceTabDefinition sourceTab){
        var header = CreateCsvHeader(sourceTab);
        var writer = new StringBuilder();
        writer.AppendLine(string.Join(",",header));
        foreach(var row in rows.OrderBy(static row => row.Benchmark,StringComparer.OrdinalIgnoreCase)){
            writer.AppendCsv(row.Benchmark);
            foreach(var project in sourceTab.ProjectOrder){
                var metrics = row.GetProject(project);
                writer.Append(',');
                if(metrics.HasMeasuredValue&&metrics.TimeNanoseconds is { } time){
                    writer.Append(time.ToString("G17",CultureInfo.InvariantCulture));
                }

                writer.Append(',');
                if(metrics.HasMeasuredValue&&metrics.AllocatedBytes is { } allocated){
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

        var sourceTab = ParseSourceTabFromHeader(header);
        var result = new List<BenchmarkComparisonRow>();
        while(!parser.EndOfData){
            var fields = parser.ReadFields()??[];
            if(fields.Length==0||fields.All(string.IsNullOrWhiteSpace)){
                continue;
            }

            var method = GetCell(fields,0);
            if(string.IsNullOrWhiteSpace(method)){
                throw new InvalidDataException("Benchmark name is required.");
            }

            var metrics = new Dictionary<string,ProjectMetrics>(StringComparer.OrdinalIgnoreCase);
            for(var projectIndex=0;projectIndex<sourceTab.ProjectOrder.Count;projectIndex++){
                var timeIndex = 1+(projectIndex*2);
                var allocatedIndex = timeIndex+1;
                var timeText = GetCell(fields,timeIndex);
                var allocatedText = GetCell(fields,allocatedIndex);
                if(string.IsNullOrWhiteSpace(timeText)&&string.IsNullOrWhiteSpace(allocatedText)){
                    continue;
                }

                var time = ParseDoubleCell(timeText,header[timeIndex]);
                var allocated = ParseLongCell(allocatedText,header[allocatedIndex]);
                var metric = ProjectMetrics.FromMeasurement(time,allocated);
                if(metric.HasMeasuredValue){
                    metrics[sourceTab.ProjectOrder[projectIndex]]=metric;
                }
            }

            result.Add(CreateRow(method,metrics,sourceTab));
        }

        return result;
    }

    public static RatioValue CalculateTimeRatio(ProjectMetrics left,ProjectMetrics right){
        if(!left.HasMeasuredValue||!right.HasMeasuredValue||left.TimeNanoseconds is not { } leftValue||right.TimeNanoseconds is not { } rightValue||leftValue<=0||rightValue<=0){
            return RatioValue.Missing;
        }

        return RatioValue.FromNumeric(leftValue/rightValue);
    }

    public static RatioValue CalculateAllocatedRatio(ProjectMetrics left,ProjectMetrics right){
        if(!left.HasMeasuredValue||!right.HasMeasuredValue||left.AllocatedBytes is not { } leftValue||right.AllocatedBytes is not { } rightValue){
            return RatioValue.Missing;
        }

        if(leftValue==0&&rightValue==0){
            return new RatioValue(RatioValueKind.BothZero,null,"0",RatioJudgement.Tie);
        }

        if(leftValue>0&&rightValue==0){
            return new RatioValue(RatioValueKind.LeftOnly,null,"+",RatioJudgement.LeftLose);
        }

        if(leftValue==0&&rightValue>0){
            return new RatioValue(RatioValueKind.RightOnly,null,"-",RatioJudgement.LeftWin);
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

    private static BenchmarkComparisonRow CreateRow(
        string method,
        IReadOnlyDictionary<string,ProjectMetrics> metrics,
        BenchmarkSourceTabDefinition sourceTab){
        var left = metrics.GetValueOrDefault(sourceTab.LeftProject);
        var right = metrics.GetValueOrDefault(sourceTab.RightProject);
        var row = new BenchmarkComparisonRow(
            method,
            sourceTab,
            left,
            right);
        return row with{
            PairRatio = CreatePairComparison(row,new ComparisonPairDefinition(
                sourceTab.Id,
                sourceTab.DisplayName,
                sourceTab.LeftProject+"/"+sourceTab.RightProject,
                sourceTab.LeftProject,
                sourceTab.RightProject)),
        };
    }

    private static ProjectMetrics CreateMetrics(ReportRow row){
        if(!string.Equals(row.Status,"Completed",StringComparison.OrdinalIgnoreCase)){
            return ProjectMetrics.Missing;
        }

        var time = row.MeanNanoseconds>0 ? row.MeanNanoseconds : (double?)null;
        var allocated = string.IsNullOrWhiteSpace(row.AllocatedBytes) ? null : (long?)row.AllocatedBytesValue;
        return ProjectMetrics.FromMeasurement(time,allocated);
    }

    private static string NormalizeProjectName(string project,BenchmarkSourceTabDefinition sourceTab){
        if(string.Equals(project,sourceTab.LeftProject,StringComparison.OrdinalIgnoreCase)){
            return sourceTab.LeftProject;
        }

        if(string.Equals(project,sourceTab.RightProject,StringComparison.OrdinalIgnoreCase)){
            return sourceTab.RightProject;
        }

        return project;
    }

    private static string[] CreateCsvHeader(BenchmarkSourceTabDefinition sourceTab) => [
        "Benchmark",
        sourceTab.LeftProject+"時間ns",
        sourceTab.LeftProject+"割当bytes",
        sourceTab.RightProject+"時間ns",
        sourceTab.RightProject+"割当bytes",
    ];

    private static BenchmarkSourceTabDefinition ParseSourceTabFromHeader(string[] header){
        if(header.Length!=5){
            throw new InvalidDataException("Only the source-tab two-project CSV format is supported.");
        }

        var leftProject = ExtractProjectName(header[1],"時間ns");
        var rightProject = ExtractProjectName(header[3],"時間ns");
        var sourceTab = BenchmarkSourceTabs.All.FirstOrDefault(tab =>
            string.Equals(tab.LeftProject,leftProject,StringComparison.OrdinalIgnoreCase)
            &&string.Equals(tab.RightProject,rightProject,StringComparison.OrdinalIgnoreCase));
        if(sourceTab is null){
            throw new InvalidDataException("Unknown benchmark source tab CSV header.");
        }

        var expected = CreateCsvHeader(sourceTab);
        for(var i=0;i<expected.Length;i++){
            if(!string.Equals(header[i],expected[i],StringComparison.Ordinal)){
                throw new InvalidDataException("CSV header does not match the source-tab format.");
            }
        }

        return sourceTab;
    }

    private static string ExtractProjectName(string value,string suffix) =>
        value.EndsWith(suffix,StringComparison.Ordinal)
            ? value[..^suffix.Length]
            : value;

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

        throw new InvalidDataException($"Column '{column}' must be numeric.");
    }

    private static long? ParseLongCell(string value,string column){
        if(string.IsNullOrWhiteSpace(value)){
            return null;
        }

        if(long.TryParse(value,NumberStyles.Integer,CultureInfo.InvariantCulture,out var result)){
            return result;
        }

        throw new InvalidDataException($"Column '{column}' must be an integer.");
    }
}

public readonly record struct ProjectMetrics(double? TimeNanoseconds,long? AllocatedBytes,bool HasValue){
    public static readonly ProjectMetrics Missing = new(null,null,false);

    public bool HasMeasuredValue => this.HasValue&&this.TimeNanoseconds is not null;

    public static ProjectMetrics FromMeasurement(double? timeNanoseconds,long? allocatedBytes) =>
        timeNanoseconds is null
            ? Missing
            : new ProjectMetrics(timeNanoseconds,allocatedBytes,true);
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
    BenchmarkSourceTabDefinition SourceTab,
    ProjectMetrics Left,
    ProjectMetrics Right){
    public PairComparison PairRatio { get; init; }

    public ProjectMetrics GetProject(string project){
        if(string.Equals(project,this.SourceTab.LeftProject,StringComparison.OrdinalIgnoreCase)){
            return this.Left;
        }

        if(string.Equals(project,this.SourceTab.RightProject,StringComparison.OrdinalIgnoreCase)){
            return this.Right;
        }

        return ProjectMetrics.Missing;
    }

    public PairComparison GetPair(string key) =>
        string.Equals(key,this.SourceTab.Id,StringComparison.OrdinalIgnoreCase)
            ? this.PairRatio
            : default;
}

public readonly record struct ComparisonPairDefinition(
    string Key,
    string DisplayName,
    string TooltipText,
    string LeftProject,
    string RightProject);

internal static class BenchmarkComparisonPairs{
    public static IReadOnlyList<string>? GetPairProjects(string project) =>
        BenchmarkSourceTabs.TryGetByProject(project)?.ProjectOrder;
}

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
