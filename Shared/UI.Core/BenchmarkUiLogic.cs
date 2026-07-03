namespace UI;

public enum BenchmarkProjectLiveState{
    Idle,
    Running,
    Completed,
}

public enum BenchmarkMeasurementHighlightState{
    None,
    Fresh,
    RetainedDuringRun,
}

public enum BenchmarkStatusImageKind{
    None,
    Waiting,
    Running,
    Completed,
    FinalCompleted,
    Failed,
    NoMeasurement,
}

internal static class BenchmarkStatusImageNames{
    public const string Waiting = "status_waiting.png";
    public const string Completed = "status_completed.png";
    public const string FinalCompleted = "status_final_completed.png";
    public const string Failed = "status_failed.png";
    public const string NoMeasurement = "status_no_measurement.png";
    public const string CpuIdle = "status_cpu_idle.png";
    public const string CpuRunning = "status_cpu_running.png";

    public static string FromKind(BenchmarkStatusImageKind kind) =>
        kind switch{
            BenchmarkStatusImageKind.Waiting => Waiting,
            BenchmarkStatusImageKind.Completed => Completed,
            BenchmarkStatusImageKind.FinalCompleted => FinalCompleted,
            BenchmarkStatusImageKind.Failed => Failed,
            BenchmarkStatusImageKind.NoMeasurement => NoMeasurement,
            BenchmarkStatusImageKind.Running => CpuRunning,
            _ => string.Empty,
        };
}

internal static class BenchmarkCpuCoreImageNames{
    public const string Performance = "cpu_p_core.png";
    public const string Efficiency = "cpu_e_core.png";

    public static string FromCoreKind(CpuCoreKind kind) =>
        kind switch{
            CpuCoreKind.Performance => Performance,
            CpuCoreKind.Efficiency => Efficiency,
            _ => string.Empty,
        };
}

internal readonly record struct BenchmarkStatusImageInput(
    bool HasCase,
    bool IsRunning,
    bool IsFailed,
    bool HasMeasuredValue,
    bool IsCompletedWithoutMeasurement,
    bool IsSelected,
    bool IsRowFinalCompleted);

internal static class BenchmarkStatusImageLogic{
    public static BenchmarkStatusImageKind GetProjectKind(BenchmarkStatusImageInput input){
        if(!input.HasCase){
            return BenchmarkStatusImageKind.None;
        }

        if(input.IsRunning){
            return BenchmarkStatusImageKind.Running;
        }

        if(input.IsFailed){
            return BenchmarkStatusImageKind.Failed;
        }

        if(input.HasMeasuredValue){
            return input.IsRowFinalCompleted
                ? BenchmarkStatusImageKind.FinalCompleted
                : BenchmarkStatusImageKind.Completed;
        }

        if(input.IsCompletedWithoutMeasurement){
            return BenchmarkStatusImageKind.NoMeasurement;
        }

        return input.IsSelected
            ? BenchmarkStatusImageKind.Waiting
            : BenchmarkStatusImageKind.None;
    }
}

internal static class BenchmarkCpuStatusImageLogic{
    public static string GetImageSource(string status) =>
        string.Equals(status,JobStatus.Running.ToString(),StringComparison.OrdinalIgnoreCase)
            ? BenchmarkStatusImageNames.CpuRunning
            : string.Equals(status,"Completed",StringComparison.OrdinalIgnoreCase)
                ? BenchmarkStatusImageNames.Completed
                : string.Equals(status,"Failed",StringComparison.OrdinalIgnoreCase)
                    ? BenchmarkStatusImageNames.Failed
                    : BenchmarkStatusImageNames.CpuIdle;

    public static string GetCellStatusText(string status,string currentJobText) =>
        string.IsNullOrWhiteSpace(currentJobText)
            ? (string.Equals(status,JobStatus.Pending.ToString(),StringComparison.OrdinalIgnoreCase) ? string.Empty : GetStatusText(status))
            : currentJobText;

    public static string GetStatusText(string status) =>
        string.Equals(status,JobStatus.Running.ToString(),StringComparison.OrdinalIgnoreCase)
            ? "実行中"
            : string.Equals(status,"Completed",StringComparison.OrdinalIgnoreCase)
                ? "完了"
                : string.Equals(status,"Failed",StringComparison.OrdinalIgnoreCase)
                    ? "失敗"
                    : status;
}

internal static class BenchmarkMeasurementHighlightLogic{
    public static BenchmarkMeasurementHighlightState AdvanceForRunningElement(
        BenchmarkMeasurementHighlightState current,
        int? measuredElementCount,
        int runningElementCount) =>
        current==BenchmarkMeasurementHighlightState.Fresh
        &&measuredElementCount is { } measured
        &&runningElementCount>measured
            ? BenchmarkMeasurementHighlightState.RetainedDuringRun
            : current;

    public static BenchmarkMeasurementHighlightState ClearAfterRun(BenchmarkMeasurementHighlightState current) =>
        current==BenchmarkMeasurementHighlightState.None
            ? BenchmarkMeasurementHighlightState.None
            : BenchmarkMeasurementHighlightState.None;
}

internal static class BenchmarkRunDisplaySourceLogic{
    public static bool ShouldUseCurrentRunMemory(bool hasCurrentRunResultsForSelectedTab) =>
        hasCurrentRunResultsForSelectedTab;

    public static bool ShouldKeepCurrentRunMemoryDisplay(bool hasAnyCurrentRunResults) =>
        hasAnyCurrentRunResults;

    public static bool ShouldLoadSelectedRunFromDatabase(
        bool hasCurrentRunResultsForSelectedTab,
        bool hasAnyCurrentRunResults,
        bool isBenchmarkRunActive,
        bool isLoadingSelectedRunDb,
        bool isExternalRunOption,
        bool isNewRunOption,
        bool isTransientRunOption,
        bool hasSavedRunSelection){
        if(isLoadingSelectedRunDb||isBenchmarkRunActive){
            return false;
        }

        if(hasCurrentRunResultsForSelectedTab||hasAnyCurrentRunResults){
            return false;
        }

        if(isExternalRunOption||isNewRunOption||isTransientRunOption){
            return false;
        }

        return hasSavedRunSelection;
    }
}

internal static class BenchmarkTimestampDisplay{
    public const string Format = "yyyy/MM/dd HH:mm:ss";

    public static string FormatLocal(DateTimeOffset timestamp) =>
        timestamp.ToLocalTime().ToString(Format,System.Globalization.CultureInfo.InvariantCulture);
}

internal static class BenchmarkProjectLiveStateLogic{
    public static bool IsPending(BenchmarkProjectLiveState state,bool hasMetrics) =>
        state==BenchmarkProjectLiveState.Idle&&!hasMetrics;

    public static bool IsRunning(BenchmarkProjectLiveState state) =>
        state==BenchmarkProjectLiveState.Running;

    public static bool IsCompleted(BenchmarkProjectLiveState state) =>
        state==BenchmarkProjectLiveState.Completed;
}

internal static class BenchmarkElementCountLogic{
    public static IReadOnlyList<int> BuildAdaptiveCandidates(IReadOnlyList<int> elementCounts,int maxElementCount){
        var candidates = elementCounts
            .Where(count => count<=maxElementCount)
            .Distinct()
            .Order()
            .ToArray();
        return candidates.Length==0 ? [BenchmarkElementCounts.DefaultInitial[0]] : candidates;
    }
}

internal readonly record struct BenchmarkChartBarRect(float Left,float Right,float Width);

internal readonly record struct BenchmarkChartLabelPoint(float X,float Y);

internal readonly record struct BenchmarkChartLabelRect(float X,float Y,float Width,float Height);

internal static class BenchmarkChartDisplayOrderLogic{
    public static IReadOnlyList<T> CreateTransposedSourceOrder<T>(IReadOnlyList<T> gridDisplayOrder){
        var ordered = new T[gridDisplayOrder.Count];
        for(var index=0;index<gridDisplayOrder.Count;index++){
            ordered[index]=gridDisplayOrder[gridDisplayOrder.Count-index-1];
        }

        return ordered;
    }
}

internal readonly record struct BenchmarkResultSelectionCell(
    string Project,
    string Method,
    string TypeFullName,
    string ArgumentKey,
    bool HasCase,
    bool IsSelected);

[global::Seamless.FLinq.LinqIntercept(false)]
internal static class BenchmarkResultDisplaySelectionLogic{
    public static bool IsResultSelected(BenchmarkResultItem result,IEnumerable<BenchmarkResultSelectionCell> cells) =>
        cells.Any(cell => cell.HasCase
            &&cell.IsSelected
            &&string.Equals(cell.Project,result.Project,StringComparison.OrdinalIgnoreCase)
            &&string.Equals(cell.Method,result.Method,StringComparison.OrdinalIgnoreCase)
            &&IsSameType(result.TypeFullName,cell.TypeFullName)
            &&IsSameArgument(result.ArgumentKey,cell.ArgumentKey));

    private static bool IsSameType(string left,string right) =>
        string.IsNullOrWhiteSpace(right)
            ? true
            : !string.IsNullOrWhiteSpace(left)
                &&(string.Equals(left,right,StringComparison.OrdinalIgnoreCase)
                    ||string.Equals(ShortTypeName(left),right,StringComparison.OrdinalIgnoreCase)
                    ||string.Equals(left,ShortTypeName(right),StringComparison.OrdinalIgnoreCase));

    private static bool IsSameArgument(string left,string right) =>
        string.IsNullOrWhiteSpace(right)
            ||string.Equals(left,right,StringComparison.OrdinalIgnoreCase);

    private static string ShortTypeName(string typeFullName) {
        if(string.IsNullOrWhiteSpace(typeFullName)) {
            return string.Empty;
        }

        var index = typeFullName.LastIndexOf('.');
        return index<0||index==typeFullName.Length-1
            ? typeFullName
            : typeFullName[(index+1)..];
    }
}

internal static class BenchmarkChartSelectionLayoutLogic{
    public static float CalculateStackBoundary(float plotLeft,float plotRight,float leftPercent) =>
        plotLeft+(plotRight-plotLeft)*Math.Clamp(leftPercent,0,100)/100;

    public static (string LeftText,string RightText) CreateShareTexts(double leftValue,double rightValue){
        var leftNumber = Math.Max(0,leftValue);
        var rightNumber = Math.Max(0,rightValue);
        var total = leftNumber+rightNumber;
        if(total<=0){
            return ("0.00","0.00");
        }

        var leftShare = Math.Round(leftNumber/total,2,MidpointRounding.AwayFromZero);
        leftShare=Math.Clamp(leftShare,0,1);
        var rightShare = 1-leftShare;
        return (
            leftShare.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture),
            rightShare.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture));
    }

    public static double? CreateRatio(double leftValue,double rightValue){
        return leftValue>0&&rightValue>0
            ? leftValue/rightValue
            : null;
    }

    public static (BenchmarkChartBarRect LeftBarRect,BenchmarkChartBarRect RightBarRect) CreateBarRects(
        float plotLeft,
        float plotRight,
        double leftValue,
        double rightValue){
        var total = leftValue+rightValue;
        var leftPercent = total>0
            ? (float)Math.Clamp(leftValue/total*100,0,100)
            : 0;
        var leftEnd = Math.Clamp(CalculateStackBoundary(plotLeft,plotRight,leftPercent),plotLeft,plotRight);
        return (
            new BenchmarkChartBarRect(plotLeft,leftEnd,MathF.Max(1,leftEnd-plotLeft)),
            new BenchmarkChartBarRect(leftEnd,plotRight,MathF.Max(1,plotRight-leftEnd)));
    }

    public static (BenchmarkChartLabelPoint LeftLabelPoint,BenchmarkChartLabelPoint RightLabelPoint) CreateBarLabelPoints(
        BenchmarkChartBarRect leftBarRect,
        BenchmarkChartBarRect rightBarRect,
        float y,
        float padding) =>
        (
            new BenchmarkChartLabelPoint(leftBarRect.Right-padding,y),
            new BenchmarkChartLabelPoint(rightBarRect.Left+padding,y));

    public static (BenchmarkChartLabelRect LeftLabelRect,BenchmarkChartLabelRect RightLabelRect) CreateBoundaryLabelRects(
        BenchmarkChartBarRect leftBarRect,
        BenchmarkChartBarRect rightBarRect,
        float centerY,
        float labelWidth,
        float labelHeight,
        float padding){
        var plotLeft = MathF.Min(leftBarRect.Left,rightBarRect.Left);
        var plotRight = MathF.Max(leftBarRect.Right,rightBarRect.Right);
        var plotWidth = MathF.Max(1,plotRight-plotLeft);
        var safeLabelWidth = MathF.Max(1,MathF.Min(labelWidth,MathF.Max(1,(plotWidth-padding)/2)));
        var boundary = leftBarRect.Right;
        var top = centerY-labelHeight/2;
        var leftX = ClampLabelX(boundary-padding-safeLabelWidth,plotLeft,plotRight,safeLabelWidth);
        var rightX = ClampLabelX(boundary+padding,plotLeft,plotRight,safeLabelWidth);
        if(leftX+safeLabelWidth>rightX&&plotWidth>=safeLabelWidth*2+padding){
            if(boundary>plotLeft+plotWidth/2){
                rightX=plotRight-safeLabelWidth;
                leftX=MathF.Max(plotLeft,rightX-padding-safeLabelWidth);
            }else{
                leftX=plotLeft;
                rightX=MathF.Min(plotRight-safeLabelWidth,leftX+safeLabelWidth+padding);
            }
        }

        return (
            new BenchmarkChartLabelRect(
                leftX,
                top,
                safeLabelWidth,
                labelHeight),
            new BenchmarkChartLabelRect(
                rightX,
                top,
                safeLabelWidth,
                labelHeight));
    }

    private static float ClampLabelX(float x,float plotLeft,float plotRight,float labelWidth){
        if(plotRight-plotLeft<=labelWidth){
            return plotLeft;
        }

        return Math.Clamp(x,plotLeft,plotRight-labelWidth);
    }
}

public readonly record struct BenchmarkTabProgress(int Completed,int Total){
    public double Fraction => this.Total<=0 ? 0 : (double)this.Completed/this.Total;
    public string FractionText => this.Total<=0
        ? string.Empty
        : this.Completed.ToString(System.Globalization.CultureInfo.InvariantCulture)
            +"/"
            +this.Total.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

internal readonly record struct BenchmarkSourceTabRunningJob(
    BenchmarkSourceTab SourceTab,
    string Project,
    string Method,
    string TypeFullName,
    string ArgumentKey,
    int AxisCount);

internal sealed class BenchmarkSourceTabRunningState{
    private readonly System.Collections.Generic.HashSet<BenchmarkSourceTabRunningJob> runningJobs = [];

    public bool Apply(BenchmarkResultItem result){
        var job = CreateJob(result);
        if(string.Equals(result.Status,JobStatus.Running.ToString(),StringComparison.OrdinalIgnoreCase)){
            return this.runningJobs.Add(job);
        }

        if(string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase)
            ||string.Equals(result.Status,"Failed",StringComparison.OrdinalIgnoreCase)){
            return this.runningJobs.Remove(job);
        }

        return false;
    }

    public bool IsRunning(BenchmarkSourceTab sourceTab) =>
        this.runningJobs.Any(job => job.SourceTab==sourceTab);

    public bool Clear(){
        if(this.runningJobs.Count==0){
            return false;
        }

        this.runningJobs.Clear();
        return true;
    }

    private static BenchmarkSourceTabRunningJob CreateJob(BenchmarkResultItem result) =>
        new(
            result.SourceTab,
            result.Project,
            result.Method,
            result.TypeFullName,
            result.ArgumentKey,
            result.AxisCount);
}

internal static class BenchmarkTabProgressSourceLogic{
    public static IReadOnlyList<BenchmarkTabProgressCell> CreateCellsFromSelectedCases(
        IEnumerable<BenchmarkCase> selectedCases,
        BenchmarkSourceTabDefinition sourceTab){
        return selectedCases
            .Where(sourceTab.ContainsCase)
            .GroupBy(static benchmark => BenchmarkRowGroupingLogic.CreateCaseKey(
                benchmark.Project,
                benchmark.TypeFullName,
                benchmark.MethodName,
                benchmark.ArgumentKey),StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .Select(static benchmark => new BenchmarkTabProgressCell(
                benchmark.MethodName,
                benchmark.Project,
                benchmark.TypeFullName,
                benchmark.ArgumentKey,
                true,
                benchmark.MethodName+"|"+benchmark.ArgumentKey))
            .ToArray();
    }
}

internal static class BenchmarkFailureDisplayLogic{
    public const string DefaultFailureReason = "失敗理由が報告されていません。";

    public static string CreateTooltipReason(string failureReason){
        var firstLine = GetFirstNonEmptyLine(failureReason);
        return "\u7406\u7531: "+(string.IsNullOrWhiteSpace(firstLine) ? DefaultFailureReason : firstLine);
    }

    public static string CreateLogText(
        string project,
        string method,
        int elementCount,
        int? assignedLogicalCpu,
        string artifactFolder,
        string failureReason,
        string typeFullName = ""){
        var builder = new System.Text.StringBuilder();
        builder.AppendLine("Benchmark failed.");
        builder.AppendLine("Project="+project);
        builder.AppendLine("Method="+method);
        if(!string.IsNullOrWhiteSpace(typeFullName)){
            builder.AppendLine("Type="+typeFullName);
        }
        builder.AppendLine("N="+elementCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.AppendLine("CPU="+(assignedLogicalCpu?.ToString(System.Globalization.CultureInfo.InvariantCulture)??"-"));
        builder.AppendLine("Artifact="+(string.IsNullOrWhiteSpace(artifactFolder) ? "-" : artifactFolder));
        builder.AppendLine("FailureReason:");
        builder.AppendLine(string.IsNullOrWhiteSpace(failureReason) ? DefaultFailureReason : failureReason);
        return builder.ToString().TrimEnd();
    }

    private static string GetFirstNonEmptyLine(string text){
        using var reader = new System.IO.StringReader(text??string.Empty);
        while(reader.ReadLine() is { } line){
            if(!string.IsNullOrWhiteSpace(line)){
                return line.Trim();
            }
        }

        return string.Empty;
    }
}

internal readonly record struct BenchmarkTabProgressCell(string Method,string Project,string TypeFullName,string ArgumentKey,bool IsSelected,string PairKey){
    public BenchmarkTabProgressCell(string method,string project,string typeFullName,bool isSelected)
        : this(method,project,typeFullName,string.Empty,isSelected,string.Empty){
    }

    public BenchmarkTabProgressCell(string method,string project,bool isSelected)
        : this(method,project,string.Empty,string.Empty,isSelected,string.Empty){
    }

    public BenchmarkTabProgressCell(string method,string project,string typeFullName,string argumentKey,bool isSelected)
        : this(method,project,typeFullName,argumentKey,isSelected,string.Empty){
    }
}

internal static class BenchmarkArgumentKeyMatchLogic{
    public static bool IsExactMatch(string left,string right) =>
        string.Equals(left,right,StringComparison.OrdinalIgnoreCase);
}

internal static class BenchmarkPairCompletionLogic{
    public static bool IsCellTerminal(
        IEnumerable<BenchmarkElementMeasurement> measurements,
        IEnumerable<BenchmarkTabProgressCell> selectedCells,
        BenchmarkTabProgressCell cell,
        string leftProject,
        string rightProject,
        double thresholdNanoseconds){
        var measurementArray = measurements as BenchmarkElementMeasurement[]??measurements.ToArray();
        if(HasFailedMeasurementForCell(measurementArray,cell)){
            return true;
        }

        if(HasReachedThresholdForCell(measurementArray,cell,thresholdNanoseconds)){
            return true;
        }

        return HasSelectedPairThresholdCompletion(
            measurementArray,
            selectedCells,
            cell,
            leftProject,
            rightProject,
            thresholdNanoseconds);
    }

    public static bool HasSelectedPairThresholdCompletion(
        IEnumerable<BenchmarkElementMeasurement> measurements,
        IEnumerable<BenchmarkTabProgressCell> selectedCells,
        BenchmarkTabProgressCell cell,
        string leftProject,
        string rightProject,
        double thresholdNanoseconds){
        if(!IsPairProject(cell.Project,leftProject,rightProject)){
            return false;
        }

        var measurementArray = measurements as BenchmarkElementMeasurement[]??measurements.ToArray();
        return selectedCells
            .Where(candidate => candidate.IsSelected&&IsSamePair(candidate,cell,leftProject,rightProject))
            .Any(candidate => HasReachedThresholdForCell(measurementArray,candidate,thresholdNanoseconds));
    }

    public static bool HasReachedThresholdForCell(
        IEnumerable<BenchmarkElementMeasurement> measurements,
        BenchmarkTabProgressCell cell,
        double thresholdNanoseconds) =>
        measurements.Any(result =>
            IsMeasurementForCell(result,cell)
            &&string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase)
            &&result.MeanNanoseconds is { } mean
            &&mean>=thresholdNanoseconds);

    public static bool HasCompletedMeasurementForCell(
        IEnumerable<BenchmarkElementMeasurement> measurements,
        BenchmarkTabProgressCell cell,
        int elementCount) =>
        measurements.Any(result =>
            IsMeasurementForCell(result,cell)
            &&result.ElementCount==elementCount
            &&string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase)
            &&result.MeanNanoseconds is not null);

    public static bool IsMeasurementForCell(BenchmarkElementMeasurement result,BenchmarkTabProgressCell cell) =>
        string.Equals(result.Project,cell.Project,StringComparison.OrdinalIgnoreCase)
        &&string.Equals(result.Method,cell.Method,StringComparison.OrdinalIgnoreCase)
        &&IsSameType(result.TypeFullName,cell.TypeFullName)
        &&IsSameArgument(result.ArgumentKey,cell.ArgumentKey)
        &&result.ElementCount>0;

    private static bool HasFailedMeasurementForCell(
        IEnumerable<BenchmarkElementMeasurement> measurements,
        BenchmarkTabProgressCell cell) =>
        measurements.Any(result =>
            IsMeasurementForCell(result,cell)
            &&string.Equals(result.Status,"Failed",StringComparison.OrdinalIgnoreCase));

    private static bool IsSamePair(
        BenchmarkTabProgressCell candidate,
        BenchmarkTabProgressCell cell,
        string leftProject,
        string rightProject) =>
        IsPairProject(candidate.Project,leftProject,rightProject)
        &&IsPairProject(cell.Project,leftProject,rightProject)
        &&string.Equals(candidate.Method,cell.Method,StringComparison.OrdinalIgnoreCase)
        &&IsSamePairIdentity(candidate,cell);

    private static bool IsSamePairIdentity(BenchmarkTabProgressCell left,BenchmarkTabProgressCell right){
        if(!string.IsNullOrWhiteSpace(left.PairKey)||!string.IsNullOrWhiteSpace(right.PairKey)){
            return !string.IsNullOrWhiteSpace(left.PairKey)
                &&!string.IsNullOrWhiteSpace(right.PairKey)
                &&string.Equals(left.PairKey,right.PairKey,StringComparison.OrdinalIgnoreCase);
        }

        return IsSameType(left.TypeFullName,right.TypeFullName)
            &&IsSameArgument(left.ArgumentKey,right.ArgumentKey);
    }

    private static bool IsPairProject(string project,string leftProject,string rightProject) =>
        string.Equals(project,leftProject,StringComparison.OrdinalIgnoreCase)
        ||string.Equals(project,rightProject,StringComparison.OrdinalIgnoreCase);

    private static bool IsSameArgument(string left,string right) =>
        BenchmarkArgumentKeyMatchLogic.IsExactMatch(left,right);

    private static bool IsSameType(string left,string right) =>
        string.IsNullOrWhiteSpace(right)
            ? true
            : !string.IsNullOrWhiteSpace(left)
                &&string.Equals(left,right,StringComparison.OrdinalIgnoreCase);
}

internal static class BenchmarkTabProgressLogic{
    public static BenchmarkTabProgress Calculate(
        IEnumerable<BenchmarkTabProgressCell> cells,
        IEnumerable<BenchmarkElementMeasurement> measurements,
        string leftProject,
        string rightProject,
        int maxElementCount,
        double thresholdNanoseconds){
        var selectedCells = cells.Where(static cell => cell.IsSelected).ToArray();
        if(selectedCells.Length==0){
            return new BenchmarkTabProgress(0,0);
        }

        var measurementArray = measurements.ToArray();
        var completed = selectedCells.Count(cell => HasTerminalMeasurement(
            measurementArray,
            selectedCells,
            cell,
            leftProject,
            rightProject,
            thresholdNanoseconds));
        return new BenchmarkTabProgress(completed,selectedCells.Length);
    }

    private static bool HasTerminalMeasurement(
        IEnumerable<BenchmarkElementMeasurement> measurements,
        IEnumerable<BenchmarkTabProgressCell> selectedCells,
        BenchmarkTabProgressCell cell,
        string leftProject,
        string rightProject,
        double thresholdNanoseconds) =>
        BenchmarkPairCompletionLogic.IsCellTerminal(
            measurements,
            selectedCells,
            cell,
            leftProject,
            rightProject,
            thresholdNanoseconds);
}

internal readonly record struct BenchmarkRowDefinition(
    string RowKey,
    string MethodName,
    string BenchmarkName,
    IReadOnlyDictionary<string,BenchmarkCase> CasesByProject);

internal static class BenchmarkRowGroupingLogic{
    public static IReadOnlyList<BenchmarkRowDefinition> CreateRows(
        IReadOnlyList<BenchmarkCase> cases,
        BenchmarkSourceTabDefinition sourceTab,
        int elementCount = 0){
        var rows = new List<BenchmarkRowDefinition>();
        foreach(var methodGroup in cases
            .Where(item => sourceTab.ContainsProject(item.Project))
            .GroupBy(static item => item.MethodName+"|"+item.ArgumentKey,StringComparer.OrdinalIgnoreCase)
            .OrderBy(static group => group.Key,StringComparer.OrdinalIgnoreCase)){
            var firstCase = methodGroup.First();
            var casesByProject = sourceTab.ProjectOrder
                .ToDictionary(
                    static project => project,
                    project => FilterProjectCases(methodGroup,sourceTab,project)
                        .OrderBy(static item => item.TypeFullName,StringComparer.OrdinalIgnoreCase)
                        .ThenBy(static item => item.AssemblyName,StringComparer.OrdinalIgnoreCase)
                        .ToArray(),
                    StringComparer.OrdinalIgnoreCase);
            var rowCount = Math.Max(1,casesByProject.Values.Max(static items => items.Length));
            for(var index=0;index<rowCount;index++){
                var rowCases = new Dictionary<string,BenchmarkCase>(StringComparer.OrdinalIgnoreCase);
                foreach(var project in sourceTab.ProjectOrder){
                    var projectCases = casesByProject[project];
                    if(index<projectCases.Length){
                        rowCases[project]=projectCases[index];
                    }
                }

                rows.Add(new BenchmarkRowDefinition(
                    CreateRowKey(sourceTab,firstCase.MethodName,rowCases),
                    firstCase.MethodName,
                    CreateBenchmarkName(sourceTab,firstCase.MethodName,rowCases,elementCount),
                    rowCases));
            }
        }

        return rows;
    }

    private static IEnumerable<BenchmarkCase> FilterProjectCases(
        IEnumerable<BenchmarkCase> methodGroup,
        BenchmarkSourceTabDefinition sourceTab,
        string project){
        var projectCases = methodGroup
            .Where(item => string.Equals(item.Project,project,StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var assemblyName = sourceTab.GetAssemblyName(project);
        if(string.IsNullOrWhiteSpace(assemblyName)){
            return projectCases;
        }

        var assemblyCases = projectCases
            .Where(item => string.Equals(item.AssemblyName,assemblyName,StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if(assemblyCases.Length>0){
            return assemblyCases;
        }

        return projectCases;
    }

    public static string CreateCaseKey(string project,string typeFullName,string methodName,string argumentKey = "") =>
        project+"|"+typeFullName+"|"+methodName+"|"+argumentKey;

    private static string CreateBenchmarkName(
        BenchmarkSourceTabDefinition sourceTab,
        string methodName,
        IReadOnlyDictionary<string,BenchmarkCase> casesByProject,
        int elementCount){
        var typeFullName = sourceTab.ProjectOrder
            .Select(project => casesByProject.TryGetValue(project,out var benchmark) ? benchmark.TypeFullName : string.Empty)
            .FirstOrDefault(static text => !string.IsNullOrWhiteSpace(text))??string.Empty;
        return ScalableBenchmarkNameFormatter.CreateBenchmarkName(sourceTab,typeFullName,methodName,elementCount);
    }

    public static string CreateRowKey(
        BenchmarkSourceTabDefinition sourceTab,
        string methodName,
        IReadOnlyDictionary<string,BenchmarkCase> casesByProject){
        var parts = sourceTab.ProjectOrder
            .Select(project => casesByProject.TryGetValue(project,out var benchmark)
                ? CreateCaseKey(benchmark.Project,benchmark.TypeFullName,benchmark.MethodName,benchmark.ArgumentKey)
                : project+"||"+methodName);
        return sourceTab.Id+"|"+string.Join("||",parts);
    }

}

internal readonly record struct BenchmarkComparisonRowMatchCase(string TypeFullName,string ArgumentKey);

internal static class BenchmarkComparisonRowMatchLogic{
    public static bool IsMatch(
        string methodName,
        string benchmarkName,
        BenchmarkSourceTab sourceTab,
        IEnumerable<BenchmarkComparisonRowMatchCase> cases,
        BenchmarkComparisonRow row) =>
        IsBenchmarkNameMatch(methodName,benchmarkName,row)
        &&cases.Any(item =>
            IsSameArgument(item.ArgumentKey,row.ArgumentKey)
            &&IsTypeMatch(sourceTab,item.TypeFullName,row.TypeFullName));

    private static bool IsTypeMatch(BenchmarkSourceTab sourceTab,string benchmarkTypeFullName,string comparisonTypeFullName) {
        if(ScalableBenchmarkNameFormatter.UsesTypedBenchmarkName(sourceTab)) {
            if(string.IsNullOrWhiteSpace(comparisonTypeFullName)) {
                return false;
            }
            var benchmarkType = ScalableBenchmarkNameFormatter.ShortTypeName(benchmarkTypeFullName);
            if(!ScalableBenchmarkNameFormatter.IsScalableSourceTab(sourceTab)) {
                var method1Type=ScalableBenchmarkNameFormatter.CreateLegacyMethod1ComparisonTypeText(benchmarkTypeFullName);
                if(!string.IsNullOrWhiteSpace(method1Type)) {
                    benchmarkType=method1Type;
                }
            }
            return string.Equals(benchmarkType,comparisonTypeFullName,StringComparison.OrdinalIgnoreCase);
        }

        return string.IsNullOrWhiteSpace(comparisonTypeFullName)
            ||string.Equals(ScalableBenchmarkNameFormatter.ShortTypeName(benchmarkTypeFullName),comparisonTypeFullName,StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBenchmarkNameMatch(string methodName,string benchmarkName,BenchmarkComparisonRow row) =>
        string.Equals(row.MethodName,methodName,StringComparison.OrdinalIgnoreCase)
        ||string.Equals(row.Benchmark,benchmarkName,StringComparison.OrdinalIgnoreCase)
        ||row.Benchmark.StartsWith(benchmarkName+"/",StringComparison.OrdinalIgnoreCase);

    private static bool IsSameArgument(string left,string right) =>
        BenchmarkArgumentKeyMatchLogic.IsExactMatch(left,right);
}

internal sealed record BenchmarkElementMeasurement(
    string Project,
    string Method,
    string TypeFullName,
    string ArgumentKey,
    int ElementCount,
    string Status,
    double? MeanNanoseconds){
    public BenchmarkElementMeasurement(string project,string method,string typeFullName,int elementCount,string status,double? meanNanoseconds)
        : this(project,method,typeFullName,string.Empty,elementCount,status,meanNanoseconds){
    }

    public BenchmarkElementMeasurement(string project,string method,int elementCount,string status,double? meanNanoseconds)
        : this(project,method,string.Empty,string.Empty,elementCount,status,meanNanoseconds){
    }
}

internal static class BenchmarkRowCompletionLogic{
    public static bool IsFinalCompleted(
        IEnumerable<BenchmarkElementMeasurement> measurements,
        string method,
        string leftProject,
        string rightProject,
        int maxElementCount,
        double thresholdNanoseconds) =>
        IsFinalCompleted(measurements,method,string.Empty,leftProject,rightProject,maxElementCount,thresholdNanoseconds);

    public static bool IsFinalCompleted(
        IEnumerable<BenchmarkElementMeasurement> measurements,
        string method,
        string typeFullName,
        string leftProject,
        string rightProject,
        int maxElementCount,
        double thresholdNanoseconds){
        return IsFinalCompleted(measurements,method,typeFullName,string.Empty,leftProject,rightProject,maxElementCount,thresholdNanoseconds);
    }

    public static bool IsFinalCompleted(
        IEnumerable<BenchmarkElementMeasurement> measurements,
        string method,
        string typeFullName,
        string argumentKey,
        string leftProject,
        string rightProject,
        int maxElementCount,
        double thresholdNanoseconds){
        var pairKey = CreateSyntheticPairKey(method,typeFullName,argumentKey);
        return IsFinalCompleted(
            measurements,
            new BenchmarkTabProgressCell(method,leftProject,typeFullName,argumentKey,true,pairKey),
            new BenchmarkTabProgressCell(method,rightProject,typeFullName,argumentKey,true,pairKey),
            maxElementCount,
            thresholdNanoseconds);
    }

    public static bool IsFinalCompleted(
        IEnumerable<BenchmarkElementMeasurement> measurements,
        BenchmarkTabProgressCell leftCell,
        BenchmarkTabProgressCell rightCell,
        int maxElementCount,
        double thresholdNanoseconds){
        var measurementArray = measurements as BenchmarkElementMeasurement[]??measurements.ToArray();
        var selectedCells = new[] {
            leftCell with { IsSelected = true },
            rightCell with { IsSelected = true },
        };
        if(selectedCells.Any(cell => BenchmarkPairCompletionLogic.HasSelectedPairThresholdCompletion(
            measurementArray,
            selectedCells,
            cell,
            leftCell.Project,
            rightCell.Project,
            thresholdNanoseconds))){
            return true;
        }

        return HasCompletedMaxElementCount(measurementArray,selectedCells,maxElementCount);
    }

    private static bool HasCompletedMaxElementCount(
        IEnumerable<BenchmarkElementMeasurement> measurements,
        IReadOnlyList<BenchmarkTabProgressCell> selectedCells,
        int maxElementCount){
        if(maxElementCount<=0){
            return false;
        }

        return selectedCells.All(cell =>
            BenchmarkPairCompletionLogic.HasCompletedMeasurementForCell(measurements,cell,maxElementCount));
    }

    private static string CreateSyntheticPairKey(string method,string typeFullName,string argumentKey) =>
        string.Join("|",method,typeFullName,argumentKey);
}

internal static class BenchmarkSkippedDisplayLogic{
    public static int? FindPairStoppedAt(
        IEnumerable<BenchmarkElementMeasurement> measurements,
        string method,
        string project,
        int selectedElementCount,
        double thresholdNanoseconds) =>
        FindPairStoppedAt(measurements,method,string.Empty,project,selectedElementCount,thresholdNanoseconds);

    public static int? FindPairStoppedAt(
        IEnumerable<BenchmarkElementMeasurement> measurements,
        string method,
        string typeFullName,
        string project,
        int selectedElementCount,
        double thresholdNanoseconds){
        return FindPairStoppedAt(measurements,method,typeFullName,string.Empty,project,selectedElementCount,thresholdNanoseconds);
    }

    public static int? FindPairStoppedAt(
        IEnumerable<BenchmarkElementMeasurement> measurements,
        string method,
        string typeFullName,
        string argumentKey,
        string project,
        int selectedElementCount,
        double thresholdNanoseconds){
        var pairProjects = BenchmarkComparisonPairs.GetPairProjects(project);
        if(pairProjects is null){
            return null;
        }

        var pairKey = CreateSyntheticPairKey(method,typeFullName,argumentKey);
        return FindPairStoppedAt(
            measurements,
            new BenchmarkTabProgressCell(method,pairProjects[0],typeFullName,argumentKey,true,pairKey),
            new BenchmarkTabProgressCell(method,pairProjects[1],typeFullName,argumentKey,true,pairKey),
            selectedElementCount,
            thresholdNanoseconds);
    }

    public static int? FindPairStoppedAt(
        IEnumerable<BenchmarkElementMeasurement> measurements,
        BenchmarkTabProgressCell leftCell,
        BenchmarkTabProgressCell rightCell,
        int selectedElementCount,
        double thresholdNanoseconds){
        var cells = new[] {
            leftCell with { IsSelected = true },
            rightCell with { IsSelected = true },
        };
        return measurements
            .Where(result => result.ElementCount<selectedElementCount
                &&string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase)
                &&result.MeanNanoseconds is not null
                &&cells.Any(cell => BenchmarkPairCompletionLogic.IsMeasurementForCell(result,cell)))
            .GroupBy(static result => result.ElementCount)
            .OrderBy(static group => group.Key)
            .Where(group => cells.All(cell => BenchmarkPairCompletionLogic.HasCompletedMeasurementForCell(group,cell,group.Key)))
            .Where(group => cells.Any(cell => BenchmarkPairCompletionLogic.HasReachedThresholdForCell(group,cell,thresholdNanoseconds)))
            .Select(group => (int?)group.Key)
            .FirstOrDefault();
    }

    public static int? FindLatestCompletedPairElementCountBefore(
        IEnumerable<BenchmarkElementMeasurement> measurements,
        string method,
        string leftProject,
        string rightProject,
        int selectedElementCount) =>
        FindLatestCompletedPairElementCountBefore(measurements,method,string.Empty,leftProject,rightProject,selectedElementCount);

    public static int? FindLatestCompletedPairElementCountBefore(
        IEnumerable<BenchmarkElementMeasurement> measurements,
        string method,
        string typeFullName,
        string leftProject,
        string rightProject,
        int selectedElementCount) =>
        FindLatestCompletedPairElementCountBefore(measurements,method,typeFullName,string.Empty,leftProject,rightProject,selectedElementCount);

    public static int? FindLatestCompletedPairElementCountBefore(
        IEnumerable<BenchmarkElementMeasurement> measurements,
        string method,
        string typeFullName,
        string argumentKey,
        string leftProject,
        string rightProject,
        int selectedElementCount) {
        var pairKey = CreateSyntheticPairKey(method,typeFullName,argumentKey);
        return FindLatestCompletedPairElementCountBefore(
            measurements,
            new BenchmarkTabProgressCell(method,leftProject,typeFullName,argumentKey,true,pairKey),
            new BenchmarkTabProgressCell(method,rightProject,typeFullName,argumentKey,true,pairKey),
            selectedElementCount);
    }

    public static int? FindLatestCompletedPairElementCountBefore(
        IEnumerable<BenchmarkElementMeasurement> measurements,
        BenchmarkTabProgressCell leftCell,
        BenchmarkTabProgressCell rightCell,
        int selectedElementCount){
        var cells = new[] {
            leftCell with { IsSelected = true },
            rightCell with { IsSelected = true },
        };
        return measurements
            .Where(result => result.ElementCount>0
                &&result.ElementCount<selectedElementCount
                &&string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase)
                &&result.MeanNanoseconds is not null
                &&cells.Any(cell => BenchmarkPairCompletionLogic.IsMeasurementForCell(result,cell)))
            .GroupBy(static result => result.ElementCount)
            .OrderByDescending(static group => group.Key)
            .Where(group => cells.All(cell => BenchmarkPairCompletionLogic.HasCompletedMeasurementForCell(group,cell,group.Key)))
            .Select(group => (int?)group.Key)
            .FirstOrDefault();
    }

    private static string CreateSyntheticPairKey(string method,string typeFullName,string argumentKey) =>
        string.Join("|",method,typeFullName,argumentKey);
}

internal enum BenchmarkToolbarButtonRole{
    Run,
    Stop,
    Pause,
}

internal readonly record struct BenchmarkToolbarStateInput(
    bool IsRefreshing,
    bool IsBenchmarkRunActive,
    bool IsBenchmarkPaused,
    bool CanUseRunButton,
    bool CanStopBenchmark,
    bool CanPauseBenchmark);

internal readonly record struct BenchmarkToolbarButtonState(
    bool IsEnabled,
    bool InvokesPrimaryAction,
    bool ShowDisabledVisual,
    bool ShouldPulse);

internal static class BenchmarkToolbarStateLogic{
    public static BenchmarkToolbarButtonState CreateState(BenchmarkToolbarButtonRole role,BenchmarkToolbarStateInput input){
        if(input.IsBenchmarkPaused){
            return role switch{
                BenchmarkToolbarButtonRole.Run => new BenchmarkToolbarButtonState(
                    input.CanUseRunButton,
                    input.CanUseRunButton,
                    !input.CanUseRunButton,
                    input.CanUseRunButton),
                BenchmarkToolbarButtonRole.Stop => new BenchmarkToolbarButtonState(
                    input.CanStopBenchmark,
                    input.CanStopBenchmark,
                    !input.CanStopBenchmark,
                    input.CanStopBenchmark),
                BenchmarkToolbarButtonRole.Pause => new BenchmarkToolbarButtonState(true,false,true,false),
                _ => new BenchmarkToolbarButtonState(false,false,true,false),
            };
        }

        return role switch{
            BenchmarkToolbarButtonRole.Run => CreateDirectActionState(input.CanUseRunButton),
            BenchmarkToolbarButtonRole.Stop => CreateDirectActionState(input.CanStopBenchmark),
            BenchmarkToolbarButtonRole.Pause => CreateDirectActionState(input.CanPauseBenchmark),
            _ => new BenchmarkToolbarButtonState(false,false,true,false),
        };
    }

    public static bool IsEnabled(BenchmarkToolbarButtonRole role,BenchmarkToolbarStateInput input) =>
        CreateState(role,input).IsEnabled;

    private static BenchmarkToolbarButtonState CreateDirectActionState(bool isEnabled) =>
        new(isEnabled,isEnabled,!isEnabled,false);
}

internal static class BenchmarkToolbarPauseGuardLogic{
    public static bool RequiresDecisionDialog(BenchmarkToolbarButtonRole role,bool isBenchmarkPaused) =>
        isBenchmarkPaused&&role==BenchmarkToolbarButtonRole.Pause;
}

internal enum BenchmarkCloseAction{
    Cancel,
    StopAndClose,
}

internal static class BenchmarkCloseDialogLogic{
    public static IReadOnlyList<(string Text,BenchmarkCloseAction Action,BenchmarkToolbarButtonRole Kind)> CreateOptions(
        bool isBenchmarkRunActive,
        bool isBenchmarkPaused,
        bool hasUnsavedResults){
        if(isBenchmarkPaused){
            return [
                ("停止して閉じる",BenchmarkCloseAction.StopAndClose,BenchmarkToolbarButtonRole.Stop),
                ("キャンセル",BenchmarkCloseAction.Cancel,BenchmarkToolbarButtonRole.Pause),
            ];
        }

        if(isBenchmarkRunActive){
            return [
                ("停止して終了",BenchmarkCloseAction.StopAndClose,BenchmarkToolbarButtonRole.Stop),
                ("キャンセル",BenchmarkCloseAction.Cancel,BenchmarkToolbarButtonRole.Pause),
            ];
        }

        if(hasUnsavedResults){
            return [
                ("破棄して閉じる",BenchmarkCloseAction.StopAndClose,BenchmarkToolbarButtonRole.Stop),
                ("キャンセル",BenchmarkCloseAction.Cancel,BenchmarkToolbarButtonRole.Pause),
            ];
        }

        return [
            ("キャンセル",BenchmarkCloseAction.Cancel,BenchmarkToolbarButtonRole.Pause),
        ];
    }
}

internal enum BenchmarkCellSelectionGesture{
    Replace,
    Toggle,
    Extend,
    ExtendToggle,
}

internal enum BenchmarkCellSelectionTargetKind{
    Cell,
    RowHeader,
    ColumnHeader,
    CornerHeader,
}

internal readonly record struct BenchmarkCellCoordinate(int RowIndex,int ColumnIndex);

internal readonly record struct BenchmarkCellSelectionAnchor(BenchmarkCellSelectionTargetKind Kind,BenchmarkCellCoordinate Cell);

internal readonly record struct BenchmarkCellSelectionTarget(
    BenchmarkCellSelectionTargetKind Kind,
    int RowIndex,
    int ColumnIndex,
    int ColumnEndIndex){
    public static BenchmarkCellSelectionTarget Cell(int rowIndex,int columnIndex) =>
        new(BenchmarkCellSelectionTargetKind.Cell,rowIndex,columnIndex,columnIndex);

    public static BenchmarkCellSelectionTarget RowHeader(int rowIndex) =>
        new(BenchmarkCellSelectionTargetKind.RowHeader,rowIndex,0,0);

    public static BenchmarkCellSelectionTarget ColumnHeader(int columnIndex,int columnEndIndex) =>
        new(BenchmarkCellSelectionTargetKind.ColumnHeader,0,columnIndex,columnEndIndex);

    public static BenchmarkCellSelectionTarget CornerHeader() =>
        new(BenchmarkCellSelectionTargetKind.CornerHeader,0,0,0);
}

internal sealed record BenchmarkCellSelectionResult(
    IReadOnlySet<BenchmarkCellCoordinate> SelectedCells,
    BenchmarkCellSelectionAnchor? Anchor);

internal readonly record struct BenchmarkRowUiSelectionCell(bool HasCase,bool IsUiSelected);

internal static class BenchmarkRowUiSelectionLogic{
    public static bool IsWholeRowSelected(IEnumerable<BenchmarkRowUiSelectionCell> cells){
        var targets = cells.Where(static cell => cell.HasCase).ToArray();
        return targets.Length>0&&targets.All(static cell => cell.IsUiSelected);
    }
}

internal static class BenchmarkCellSelectionLogic{
    public static BenchmarkCellSelectionResult Apply(
        int rowCount,
        int columnCount,
        IEnumerable<BenchmarkCellCoordinate> currentSelection,
        BenchmarkCellSelectionAnchor? currentAnchor,
        BenchmarkCellSelectionTarget target,
        BenchmarkCellSelectionGesture gesture){
        var selected = currentSelection.ToHashSet();
        if(rowCount<=0||columnCount<=0){
            return new BenchmarkCellSelectionResult(selected,currentAnchor);
        }

        if(target.Kind==BenchmarkCellSelectionTargetKind.CornerHeader){
            return new BenchmarkCellSelectionResult(CreateAllCells(rowCount,columnCount).ToHashSet(),currentAnchor);
        }

        var anchor = currentAnchor ?? CreateAnchor(target,rowCount,columnCount);
        var targets = CreateTargetCells(rowCount,columnCount,anchor,target,gesture).ToArray();
        if(targets.Length==0){
            return new BenchmarkCellSelectionResult(selected,currentAnchor);
        }

        if(gesture==BenchmarkCellSelectionGesture.Replace||gesture==BenchmarkCellSelectionGesture.Extend){
            selected=targets.ToHashSet();
        }else{
            var shouldRemove = targets.All(selected.Contains);
            foreach(var cell in targets){
                if(shouldRemove){
                    selected.Remove(cell);
                }else{
                    selected.Add(cell);
                }
            }
        }

        var nextAnchor = gesture is BenchmarkCellSelectionGesture.Replace or BenchmarkCellSelectionGesture.Toggle
            ? CreateAnchor(target,rowCount,columnCount)
            : currentAnchor;
        return new BenchmarkCellSelectionResult(selected,nextAnchor);
    }

    private static BenchmarkCellSelectionAnchor CreateAnchor(BenchmarkCellSelectionTarget target,int rowCount,int columnCount){
        var row = Clamp(target.RowIndex,0,rowCount-1);
        var column = Clamp(target.ColumnIndex,0,columnCount-1);
        return target.Kind switch{
            BenchmarkCellSelectionTargetKind.RowHeader => new BenchmarkCellSelectionAnchor(target.Kind,new BenchmarkCellCoordinate(row,0)),
            BenchmarkCellSelectionTargetKind.ColumnHeader => new BenchmarkCellSelectionAnchor(target.Kind,new BenchmarkCellCoordinate(0,column)),
            _ => new BenchmarkCellSelectionAnchor(BenchmarkCellSelectionTargetKind.Cell,new BenchmarkCellCoordinate(row,column)),
        };
    }

    private static IEnumerable<BenchmarkCellCoordinate> CreateTargetCells(
        int rowCount,
        int columnCount,
        BenchmarkCellSelectionAnchor anchor,
        BenchmarkCellSelectionTarget target,
        BenchmarkCellSelectionGesture gesture){
        if(gesture is BenchmarkCellSelectionGesture.Replace or BenchmarkCellSelectionGesture.Toggle){
            return CreateDirectTargetCells(rowCount,columnCount,target);
        }

        return target.Kind switch{
            BenchmarkCellSelectionTargetKind.RowHeader => CreateRowHeaderRange(rowCount,columnCount,anchor,target),
            BenchmarkCellSelectionTargetKind.ColumnHeader => CreateColumnHeaderRange(rowCount,columnCount,anchor,target),
            _ => CreateRectangle(rowCount,columnCount,anchor.Cell,new BenchmarkCellCoordinate(target.RowIndex,target.ColumnIndex)),
        };
    }

    private static IEnumerable<BenchmarkCellCoordinate> CreateDirectTargetCells(int rowCount,int columnCount,BenchmarkCellSelectionTarget target){
        return target.Kind switch{
            BenchmarkCellSelectionTargetKind.RowHeader => CreateRectangle(rowCount,columnCount,new BenchmarkCellCoordinate(target.RowIndex,0),new BenchmarkCellCoordinate(target.RowIndex,columnCount-1)),
            BenchmarkCellSelectionTargetKind.ColumnHeader => CreateRectangle(rowCount,columnCount,new BenchmarkCellCoordinate(0,target.ColumnIndex),new BenchmarkCellCoordinate(rowCount-1,target.ColumnEndIndex)),
            _ => CreateRectangle(rowCount,columnCount,new BenchmarkCellCoordinate(target.RowIndex,target.ColumnIndex),new BenchmarkCellCoordinate(target.RowIndex,target.ColumnIndex)),
        };
    }

    private static IEnumerable<BenchmarkCellCoordinate> CreateRowHeaderRange(int rowCount,int columnCount,BenchmarkCellSelectionAnchor anchor,BenchmarkCellSelectionTarget target){
        if(anchor.Kind==BenchmarkCellSelectionTargetKind.ColumnHeader){
            return CreateRectangle(rowCount,columnCount,anchor.Cell,new BenchmarkCellCoordinate(target.RowIndex,0));
        }

        return CreateRectangle(rowCount,columnCount,new BenchmarkCellCoordinate(anchor.Cell.RowIndex,0),new BenchmarkCellCoordinate(target.RowIndex,columnCount-1));
    }

    private static IEnumerable<BenchmarkCellCoordinate> CreateColumnHeaderRange(int rowCount,int columnCount,BenchmarkCellSelectionAnchor anchor,BenchmarkCellSelectionTarget target){
        if(anchor.Kind==BenchmarkCellSelectionTargetKind.RowHeader){
            return CreateRectangle(rowCount,columnCount,anchor.Cell,new BenchmarkCellCoordinate(0,target.ColumnIndex));
        }

        return CreateRectangle(rowCount,columnCount,new BenchmarkCellCoordinate(0,anchor.Cell.ColumnIndex),new BenchmarkCellCoordinate(rowCount-1,target.ColumnEndIndex));
    }

    private static IEnumerable<BenchmarkCellCoordinate> CreateAllCells(int rowCount,int columnCount) =>
        CreateRectangle(rowCount,columnCount,new BenchmarkCellCoordinate(0,0),new BenchmarkCellCoordinate(rowCount-1,columnCount-1));

    private static IEnumerable<BenchmarkCellCoordinate> CreateRectangle(int rowCount,int columnCount,BenchmarkCellCoordinate first,BenchmarkCellCoordinate second){
        var minRow = Clamp(Math.Min(first.RowIndex,second.RowIndex),0,rowCount-1);
        var maxRow = Clamp(Math.Max(first.RowIndex,second.RowIndex),0,rowCount-1);
        var minColumn = Clamp(Math.Min(first.ColumnIndex,second.ColumnIndex),0,columnCount-1);
        var maxColumn = Clamp(Math.Max(first.ColumnIndex,second.ColumnIndex),0,columnCount-1);
        for(var row=minRow;row<=maxRow;row++){
            for(var column=minColumn;column<=maxColumn;column++){
                yield return new BenchmarkCellCoordinate(row,column);
            }
        }
    }

    private static int Clamp(int value,int min,int max) =>
        Math.Min(Math.Max(value,min),max);
}

internal sealed record CpuTableSectionDefinition(string Title,CpuCoreKind CoreKind,IReadOnlyList<CpuTableRowDefinition> Rows){
    public int ColumnCount => this.Rows.Count==0 ? 0 : this.Rows.Max(static row => row.LogicalProcessorIndexes.Count);
}

internal sealed record CpuTableRowDefinition(string Label,IReadOnlyList<int> LogicalProcessorIndexes);

[global::Seamless.FLinq.LinqIntercept(false)]
internal static class CpuSelectionTableLogic{
    public static IReadOnlyList<CpuTableSectionDefinition> BuildSections(IReadOnlyList<CpuCoreInfo> cpuCores){
        return [
            ..BuildSection("P core",CpuCoreKind.Performance,cpuCores),
            ..BuildSection("E core",CpuCoreKind.Efficiency,cpuCores),
            ..BuildSection("Unknown core",CpuCoreKind.Unknown,cpuCores),
        ];
    }

    public static void ApplyBulkSelection(IReadOnlyList<CpuCoreInfo> cpuCores,IEnumerable<int> targetLogicalProcessorIndexes,bool selected){
        var targets = targetLogicalProcessorIndexes
            .Distinct()
            .Order()
            .ToHashSet();
        if(targets.Count==0){
            return;
        }

        if(!selected){
            var selectedOutsideTargets = cpuCores.Any(cpu => cpu.IsSelected&&!targets.Contains(cpu.LogicalProcessorIndex));
            if(!selectedOutsideTargets&&cpuCores.Any(static cpu => cpu.IsSelected)){
                var keep = cpuCores
                    .Where(cpu => targets.Contains(cpu.LogicalProcessorIndex)&&cpu.IsSelected)
                    .OrderBy(static cpu => cpu.LogicalProcessorIndex)
                    .FirstOrDefault()
                    ??cpuCores
                        .Where(cpu => targets.Contains(cpu.LogicalProcessorIndex))
                        .OrderBy(static cpu => cpu.LogicalProcessorIndex)
                        .FirstOrDefault();
                foreach(var cpu in cpuCores.Where(cpu => targets.Contains(cpu.LogicalProcessorIndex))){
                    cpu.IsSelected=ReferenceEquals(cpu,keep);
                }
                return;
            }
        }

        foreach(var cpu in cpuCores.Where(cpu => targets.Contains(cpu.LogicalProcessorIndex))){
            cpu.IsSelected=selected;
        }
    }

    private static IEnumerable<CpuTableSectionDefinition> BuildSection(string title,CpuCoreKind kind,IReadOnlyList<CpuCoreInfo> cpuCores){
        var rows = cpuCores
            .Where(cpu => cpu.CoreKind==kind)
            .GroupBy(static cpu => cpu.PhysicalCoreIndex)
            .OrderBy(static group => group.Min(static cpu => cpu.LogicalProcessorIndex))
            .Select(static group => group
                .OrderBy(static cpu => cpu.LogicalProcessorIndex)
                .Select(static cpu => cpu.LogicalProcessorIndex)
                .ToArray())
            .Where(static row => row.Length>0)
            .Select(static row => new CpuTableRowDefinition(CreateRangeLabel(row),row))
            .ToArray();
        if(rows.Length>0){
            yield return new CpuTableSectionDefinition(title,kind,rows);
        }
    }

    private static string CreateRangeLabel(IReadOnlyList<int> indexes){
        var min = indexes.Min();
        var max = indexes.Max();
        return min==max ? min.ToString(System.Globalization.CultureInfo.InvariantCulture) : min+"-"+max;
    }
}

public static class BenchmarkResultCpuDisplayLogic{
    public static int? ResolveAssignedLogicalCpu(int? assignedLogicalCpu,IEnumerable<int> availableLogicalProcessors){
        if(assignedLogicalCpu is not { } value){
            return null;
        }

        return availableLogicalProcessors.Contains(value) ? value : null;
    }
}

internal static class BenchmarkRunTimestampDisplayLogic{
    public static DateTimeOffset ResolveRunningTimestamp(DateTimeOffset? startedAt,DateTimeOffset observedAt) =>
        startedAt??observedAt;
}

internal readonly record struct UiSortValue(string? Text,double? Number,bool IsMissing){
    public static readonly UiSortValue Missing = new(null,null,true);
    public static UiSortValue FromText(string text) => new(text,null,string.IsNullOrWhiteSpace(text));
    public static UiSortValue FromNumber(double? value) => value is { } number&&double.IsFinite(number) ? new(null,number,false) : Missing;

    public static int Compare(UiSortValue left,UiSortValue right,bool descending){
        if(left.IsMissing&&right.IsMissing){
            return 0;
        }

        if(left.IsMissing){
            return 1;
        }

        if(right.IsMissing){
            return -1;
        }

        var result = left.Number is { } leftNumber&&right.Number is { } rightNumber
            ? leftNumber.CompareTo(rightNumber)
            : string.Compare(left.Text,right.Text,StringComparison.OrdinalIgnoreCase);
        return descending ? -result : result;
    }
}

