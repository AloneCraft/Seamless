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
            ??throw new InvalidDataException("Benchmark SourceTab could not be resolved uniquely from report projects: "+string.Join(", ",rows.Select(static row => row.Project).Distinct(StringComparer.OrdinalIgnoreCase)));
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

            var typeDiscriminator = CreateTypeDiscriminator(sourceTab,row,duplicatedProjectMethods);
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

    public static シェア値 時間シェアを計算(ProjectMetrics left,ProjectMetrics right){
        if(!left.HasMeasuredValue||!right.HasMeasuredValue||left.TimeNanoseconds is not { } leftValue||right.TimeNanoseconds is not { } rightValue){
            return シェア値.欠測;
        }

        return シェア値.時間から作成(leftValue,rightValue);
    }

    public static シェア値 メモリシェアを計算(ProjectMetrics left,ProjectMetrics right){
        if(!left.HasMeasuredValue||!right.HasMeasuredValue||left.AllocatedBytes is not { } leftValue||right.AllocatedBytes is not { } rightValue){
            return シェア値.欠測;
        }
        return シェア値.メモリから作成(leftValue,rightValue);
    }

    public static シェア比較 シェア比較を作成(BenchmarkComparisonRow row,ComparisonPairDefinition pair){
        var left = row.GetProject(pair.LeftProject);
        var right = row.GetProject(pair.RightProject);
        return new シェア比較(
            pair,
            left,
            right,
            時間シェアを計算(left,right),
            メモリシェアを計算(left,right));
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
            左右シェア = シェア比較を作成(row,new ComparisonPairDefinition(
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
        CreateBenchmarkNameCore(bucket,sourceTab);

    private static string CreateBenchmarkNameCore(
        BenchmarkComparisonBucket bucket,
        BenchmarkSourceTabDefinition sourceTab) {
        if(!ScalableBenchmarkNameFormatter.UsesTypedBenchmarkName(sourceTab.Tab)) {
            return bucket.Method;
        }
        if(!ScalableBenchmarkNameFormatter.IsScalableSourceTab(sourceTab.Tab)
            &&!string.IsNullOrWhiteSpace(bucket.TypeFullName)) {
            return "<"+bucket.TypeFullName+">."+bucket.Method;
        }
        return ScalableBenchmarkNameFormatter.CreateBenchmarkName(bucket.SourceTypeFullName,bucket.Method);
    }

    private static string CreateTypeDiscriminator(
        BenchmarkSourceTabDefinition sourceTab,
        ReportRow row,
        System.Collections.Generic.ISet<string> duplicatedProjectMethods) {
        if(ScalableBenchmarkNameFormatter.UsesTypedBenchmarkName(sourceTab.Tab)) {
            if(ScalableBenchmarkNameFormatter.IsScalableSourceTab(sourceTab.Tab)) {
                return ScalableBenchmarkNameFormatter.ShortTypeName(row.TypeFullName);
            }
            var method1Type=ScalableBenchmarkNameFormatter.CreateLegacyMethod1ComparisonTypeText(row.TypeFullName);
            if(!string.IsNullOrWhiteSpace(method1Type)) {
                return method1Type;
            }
            if(duplicatedProjectMethods.Contains(row.Project+"|"+row.Method)) {
                return ScalableBenchmarkNameFormatter.ShortTypeName(row.TypeFullName);
            }
            return string.Empty;
        }
        if(duplicatedProjectMethods.Contains(row.Project+"|"+row.Method)) {
            return ScalableBenchmarkNameFormatter.ShortTypeName(row.TypeFullName);
        }
        return string.Empty;
    }

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

public readonly record struct シェア値(
    double? 左シェア,
    double? 右シェア,
    string 表示文字列,
    シェア判定 判定){
    public static readonly シェア値 欠測 = new(null,null,"-",シェア判定.なし);

    public bool 欠測か => this.左シェア is null||this.右シェア is null;

    public static シェア値 時間から作成(double 左値,double 右値) =>
        作成(左値,右値,true);

    public static シェア値 メモリから作成(long 左値,long 右値) =>
        作成(左値,右値,false);

    public static シェア値 左シェアから作成(double 左シェア){
        if(!double.IsFinite(左シェア)||左シェア<0||左シェア>1){
            return 欠測;
        }

        return シェアから作成(左シェア,1-左シェア,厳密判定(左シェア));
    }

    private static シェア値 作成(double 左値,double 右値,bool 時間判定){
        if(!double.IsFinite(左値)||!double.IsFinite(右値)||左値<0||右値<0){
            return 欠測;
        }

        var 合計=左値+右値;
        if(!double.IsFinite(合計)){
            return 欠測;
        }
        if(合計==0){
            return new シェア値(0,0,"0.00:0.00",シェア判定.同等);
        }

        var 左シェア=左値/合計;
        var 右シェア=右値/合計;
        シェア判定 判定;
        if(時間判定){
            判定=時間判定を取得(左値,右値);
        }else{
            判定=厳密判定(左シェア);
        }
        return シェアから作成(左シェア,右シェア,判定);
    }

    private static シェア値 シェアから作成(double 左シェア,double 右シェア,シェア判定 判定){
        var 表示左=Math.Round(左シェア,2,MidpointRounding.AwayFromZero);
        double 表示右;
        if(左シェア+右シェア==0){
            表示右=0;
        }else{
            表示右=1-表示左;
        }
        return new シェア値(
            左シェア,
            右シェア,
            表示左.ToString("0.00",CultureInfo.InvariantCulture)+":"+表示右.ToString("0.00",CultureInfo.InvariantCulture),
            判定);
    }

    private static シェア判定 時間判定を取得(double 左値,double 右値){
        if(左値==0&&右値==0){
            return シェア判定.同等;
        }
        if(右値==0){
            return シェア判定.右優位;
        }

        var 比=左値/右値;
        if(比<0.95){
            return シェア判定.左優位;
        }
        if(比>1.05){
            return シェア判定.右優位;
        }
        return シェア判定.同等;
    }

    private static シェア判定 厳密判定(double 左シェア){
        if(左シェア<0.5){
            return シェア判定.左優位;
        }
        if(左シェア>0.5){
            return シェア判定.右優位;
        }
        return シェア判定.同等;
    }
}

public readonly record struct BenchmarkShareGaugeState(double LeftFraction,double RightFraction,bool HasColoredBar){
    public static BenchmarkShareGaugeState FromShare(シェア値 share){
        if(share.欠測か||share.左シェア is not { } left||share.右シェア is not { } right||left+right<=0){
            return new(0,0,false);
        }
        return new(Math.Clamp(left,0,1),Math.Clamp(right,0,1),true);
    }
}

public readonly record struct シェア比較(
    ComparisonPairDefinition Definition,
    ProjectMetrics Left,
    ProjectMetrics Right,
    シェア値 時間シェア,
    シェア値 メモリシェア);

public readonly record struct BenchmarkComparisonRow(
    string Benchmark,
    BenchmarkSourceTabDefinition SourceTab,
    ProjectMetrics Left,
    ProjectMetrics Right){
    public string MethodName { get; init; } = Benchmark;
    public string TypeFullName { get; init; } = string.Empty;
    public string ArgumentKey { get; init; } = string.Empty;
    public IReadOnlyList<BenchmarkArgument> Arguments { get; init; } = [];
    public シェア比較 左右シェア { get; init; }

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

    public シェア比較 GetPair(string key) =>
        string.Equals(key,this.SourceTab.Id,StringComparison.OrdinalIgnoreCase)
            ? this.左右シェア
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

public enum シェア判定{
    なし,
    左優位,
    右優位,
    同等,
}
