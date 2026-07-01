using System.Globalization;

namespace UI;

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
        BenchmarkSourceTabDefinition sourceTab,
        int elementCount = 0){
        var rowsByMethod = new Dictionary<string,BenchmarkComparisonBucket>(StringComparer.OrdinalIgnoreCase);
        var duplicatedProjectMethods = rows
            .GroupBy(static row => row.Project+"|"+row.Method,StringComparer.OrdinalIgnoreCase)
            .Where(static group => group.Select(static row => row.TypeFullName).Distinct(StringComparer.OrdinalIgnoreCase).Count()>1)
            .Select(static group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach(var row in rows){
            if(!sourceTab.ContainsProject(row.Project)){
                throw new InvalidDataException($"Unknown benchmark project for {sourceTab.Id}: {row.Project}");
            }

            var typeDiscriminator = ScalableBenchmarkNameFormatter.IsScalableSourceTab(sourceTab.Tab)||duplicatedProjectMethods.Contains(row.Project+"|"+row.Method)
                ? ScalableBenchmarkNameFormatter.ShortTypeName(row.TypeFullName)
                : string.Empty;
            var key = CreateComparisonKey(row.Method,typeDiscriminator,row.ArgumentKey);
            if(!rowsByMethod.TryGetValue(key,out var bucket)){
                bucket = new BenchmarkComparisonBucket(row.Method,typeDiscriminator,row.TypeFullName,row.ArgumentKey,row.Arguments);
                rowsByMethod.Add(key,bucket);
            }

            bucket.MetricsByProject[NormalizeProjectName(row.Project,sourceTab)] = CreateMetrics(row);
        }

        return rowsByMethod
            .OrderBy(static item => item.Value.Method,StringComparer.OrdinalIgnoreCase)
            .ThenBy(static item => item.Value.TypeFullName,StringComparer.OrdinalIgnoreCase)
            .ThenBy(static item => item.Value.ArgumentKey,StringComparer.OrdinalIgnoreCase)
            .Select(item => CreateRow(
                CreateBenchmarkName(item.Value,sourceTab,elementCount),
                item.Value.Method,
                item.Value.TypeFullName,
                item.Value.MetricsByProject,
                sourceTab,
                item.Value.ArgumentKey,
                item.Value.Arguments))
            .ToArray();
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
        string benchmark,
        string method,
        string typeFullName,
        IReadOnlyDictionary<string,ProjectMetrics> metrics,
        BenchmarkSourceTabDefinition sourceTab,
        string argumentKey = "",
        IReadOnlyList<BenchmarkArgument>? arguments = null){
        var left = metrics.GetValueOrDefault(sourceTab.LeftProject);
        var right = metrics.GetValueOrDefault(sourceTab.RightProject);
        var row = new BenchmarkComparisonRow(
            benchmark,
            sourceTab,
            left,
            right);
        return row with{
            MethodName = method,
            TypeFullName = typeFullName,
            ArgumentKey = argumentKey,
            Arguments = arguments??[],
            PairRatio = CreatePairComparison(row,new ComparisonPairDefinition(
                sourceTab.Id,
                sourceTab.DisplayName,
                sourceTab.LeftProject+"/"+sourceTab.RightProject,
                sourceTab.LeftProject,
                sourceTab.RightProject)),
        };
    }

    private static string CreateBenchmarkName(
        BenchmarkComparisonBucket bucket,
        BenchmarkSourceTabDefinition sourceTab,
        int elementCount) =>
        ScalableBenchmarkNameFormatter.IsScalableSourceTab(sourceTab.Tab)
            ? ScalableBenchmarkNameFormatter.CreateBenchmarkName(bucket.SourceTypeFullName,bucket.Method)
            : bucket.Method;

    private static string CreateComparisonKey(string method,string typeFullName,string argumentKey) =>
        method+"|"+typeFullName+"|"+argumentKey;

    private sealed class BenchmarkComparisonBucket{
        public BenchmarkComparisonBucket(
            string method,
            string typeFullName,
            string sourceTypeFullName,
            string argumentKey,
            IReadOnlyList<BenchmarkArgument> arguments){
            this.Method=method;
            this.TypeFullName=typeFullName;
            this.SourceTypeFullName=sourceTypeFullName;
            this.ArgumentKey=argumentKey;
            this.Arguments=arguments;
        }

        public string Method { get; }
        public string TypeFullName { get; }
        public string SourceTypeFullName { get; }
        public string ArgumentKey { get; }
        public IReadOnlyList<BenchmarkArgument> Arguments { get; }
        public Dictionary<string,ProjectMetrics> MetricsByProject { get; } = new(StringComparer.OrdinalIgnoreCase);
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
    public string MethodName { get; init; } = Benchmark;
    public string TypeFullName { get; init; } = string.Empty;
    public string ArgumentKey { get; init; } = string.Empty;
    public IReadOnlyList<BenchmarkArgument> Arguments { get; init; } = [];
    public PairComparison PairRatio { get; init; }

    public ProjectMetrics GetProject(string project){
        if(this.SourceTab is null){
            return ProjectMetrics.Missing;
        }

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
