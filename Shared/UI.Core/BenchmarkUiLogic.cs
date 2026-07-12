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

        return BenchmarkStatusImageKind.Waiting;
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

        if(isExternalRunOption||isNewRunOption||isTransientRunOption){
            return false;
        }

        if(hasCurrentRunResultsForSelectedTab||hasAnyCurrentRunResults){
            return hasSavedRunSelection;
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
            return ("0.50","0.50");
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
            : 50;
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
    private readonly Lock syncRoot = new();
    private readonly System.Collections.Generic.HashSet<BenchmarkSourceTabRunningJob> runningJobs = [];

    public bool Apply(BenchmarkResultItem result){
        var job = CreateJob(result);
        lock(this.syncRoot){
            if(string.Equals(result.Status,JobStatus.Running.ToString(),StringComparison.OrdinalIgnoreCase)){
                return this.runningJobs.Add(job);
            }

            if(string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase)
                ||string.Equals(result.Status,"Failed",StringComparison.OrdinalIgnoreCase)){
                return this.runningJobs.Remove(job);
            }
        }

        return false;
    }

    public bool IsRunning(BenchmarkSourceTab sourceTab) {
        lock(this.syncRoot){
            foreach(var job in this.runningJobs){
                if(job.SourceTab==sourceTab){
                    return true;
                }
            }

            return false;
        }
    }

    public bool Clear(){
        lock(this.syncRoot){
            if(this.runningJobs.Count==0){
                return false;
            }

            this.runningJobs.Clear();
            return true;
        }
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
            &&result.TotalNanoseconds is { } total
            &&total>=thresholdNanoseconds);

    public static bool HasCompletedMeasurementForCell(
        IEnumerable<BenchmarkElementMeasurement> measurements,
        BenchmarkTabProgressCell cell,
        int elementCount) =>
        measurements.Any(result =>
            IsMeasurementForCell(result,cell)
            &&result.ElementCount==elementCount
            &&string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase));

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
                &&(string.Equals(left,right,StringComparison.OrdinalIgnoreCase)
                    ||string.Equals(ShortTypeName(left),right,StringComparison.OrdinalIgnoreCase)
                    ||string.Equals(left,ShortTypeName(right),StringComparison.OrdinalIgnoreCase));

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
    private static readonly IComparer<BenchmarkCase> CaseDisplayOrderComparer =
        Comparer<BenchmarkCase>.Create(static (left,right) => {
            var typeResult=string.Compare(left.TypeFullName,right.TypeFullName,StringComparison.OrdinalIgnoreCase);
            if(typeResult!=0){
                return typeResult;
            }

            return string.Compare(left.AssemblyName,right.AssemblyName,StringComparison.OrdinalIgnoreCase);
        });

    public static IReadOnlyList<BenchmarkRowDefinition> CreateRows(
        IReadOnlyList<BenchmarkCase> cases,
        BenchmarkSourceTabDefinition sourceTab,
        int elementCount = 0){
        var rows = new List<BenchmarkRowDefinition>();
        var projectOrder=sourceTab.ProjectOrder;
        if(projectOrder.Count==0||cases.Count==0){
            return rows;
        }

        var assemblyNames=new string[projectOrder.Count];
        for(var index=0;index<projectOrder.Count;index++){
            assemblyNames[index]=sourceTab.GetAssemblyName(projectOrder[index]);
        }

        var methodGroups=new Dictionary<string,BenchmarkMethodCaseGroup>(StringComparer.OrdinalIgnoreCase);
        foreach(var benchmark in cases){
            var projectIndex=FindProjectIndex(projectOrder,benchmark.Project);
            if(projectIndex<0){
                continue;
            }

            var key=benchmark.BenchmarkPath+"|"+benchmark.MethodName+"|"+benchmark.ArgumentKey;
            if(!methodGroups.TryGetValue(key,out var methodGroup)){
                methodGroup=new BenchmarkMethodCaseGroup(benchmark,projectOrder.Count);
                methodGroups.Add(key,methodGroup);
            }
            methodGroup.Add(projectIndex,assemblyNames[projectIndex],benchmark);
        }

        foreach(var methodGroup in methodGroups.Values.OrderBy(static group => group.Key,StringComparer.OrdinalIgnoreCase)){
            var firstCase=methodGroup.FirstCase;
            var casesByProject=new Dictionary<string,BenchmarkCase[]>(StringComparer.OrdinalIgnoreCase);
            var rowCount=1;
            for(var projectIndex=0;projectIndex<projectOrder.Count;projectIndex++){
                var projectCases=methodGroup.GetSortedCases(projectIndex);
                casesByProject[projectOrder[projectIndex]]=projectCases;
                if(projectCases.Length>rowCount){
                    rowCount=projectCases.Length;
                }
            }
            for(var index=0;index<rowCount;index++){
                var rowCases = new Dictionary<string,BenchmarkCase>(StringComparer.OrdinalIgnoreCase);
                for(var projectIndex=0;projectIndex<projectOrder.Count;projectIndex++){
                    var project=projectOrder[projectIndex];
                    var projectCases=casesByProject[project];
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

    public static string CreateCaseKey(string project,string typeFullName,string methodName,string argumentKey = "") =>
        project+"|"+typeFullName+"|"+methodName+"|"+argumentKey;

    private static string CreateBenchmarkName(
        BenchmarkSourceTabDefinition sourceTab,
        string methodName,
        IReadOnlyDictionary<string,BenchmarkCase> casesByProject,
        int elementCount){
        var typeFullName=string.Empty;
        var benchmarkPath=string.Empty;
        foreach(var project in sourceTab.ProjectOrder){
            if(casesByProject.TryGetValue(project,out var benchmark)
                &&!string.IsNullOrWhiteSpace(benchmark.TypeFullName)){
                typeFullName=benchmark.TypeFullName;
                benchmarkPath=benchmark.BenchmarkPath;
                break;
            }
        }
        return string.IsNullOrWhiteSpace(benchmarkPath)
            ?ScalableBenchmarkNameFormatter.CreateBenchmarkName(sourceTab,typeFullName,methodName,elementCount)
            :benchmarkPath+"."+methodName;
    }

    public static string CreateRowKey(
        BenchmarkSourceTabDefinition sourceTab,
        string methodName,
        IReadOnlyDictionary<string,BenchmarkCase> casesByProject){
        var parts=new string[sourceTab.ProjectOrder.Count];
        for(var index=0;index<sourceTab.ProjectOrder.Count;index++){
            var project=sourceTab.ProjectOrder[index];
            parts[index]=casesByProject.TryGetValue(project,out var benchmark)
                ? CreateCaseKey(benchmark.Project,benchmark.TypeFullName,benchmark.MethodName,benchmark.ArgumentKey)
                : project+"||"+methodName;
        }
        return sourceTab.Id+"|"+string.Join("||",parts);
    }

    private static int FindProjectIndex(IReadOnlyList<string> projectOrder,string project){
        for(var index=0;index<projectOrder.Count;index++){
            if(string.Equals(projectOrder[index],project,StringComparison.OrdinalIgnoreCase)){
                return index;
            }
        }

        return -1;
    }

    private sealed class BenchmarkMethodCaseGroup{
        private readonly BenchmarkProjectCaseGroup[] projectGroups;

        public BenchmarkMethodCaseGroup(BenchmarkCase firstCase,int projectCount){
            this.FirstCase=firstCase;
            this.Key=firstCase.MethodName+"|"+firstCase.ArgumentKey;
            this.projectGroups=new BenchmarkProjectCaseGroup[projectCount];
            for(var index=0;index<projectCount;index++){
                this.projectGroups[index]=new BenchmarkProjectCaseGroup();
            }
        }

        public string Key { get; }
        public BenchmarkCase FirstCase { get; }

        public void Add(int projectIndex,string assemblyName,BenchmarkCase benchmark){
            this.projectGroups[projectIndex].Add(assemblyName,benchmark);
        }

        public BenchmarkCase[] GetSortedCases(int projectIndex) =>
            this.projectGroups[projectIndex].GetSortedCases();
    }

    private sealed class BenchmarkProjectCaseGroup{
        private readonly List<BenchmarkCase> allCases = [];
        private readonly List<BenchmarkCase> assemblyCases = [];

        public void Add(string assemblyName,BenchmarkCase benchmark){
            this.allCases.Add(benchmark);
            if(!string.IsNullOrWhiteSpace(assemblyName)
                &&string.Equals(benchmark.AssemblyName,assemblyName,StringComparison.OrdinalIgnoreCase)){
                this.assemblyCases.Add(benchmark);
            }
        }

        public BenchmarkCase[] GetSortedCases(){
            var source=this.assemblyCases.Count>0
                ? this.assemblyCases
                : this.allCases;
            if(source.Count==0){
                return [];
            }

            source.Sort(CaseDisplayOrderComparer);
            return source.ToArray();
        }
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
    double? MeanNanoseconds,
    double? TotalNanoseconds = null){
    public BenchmarkElementMeasurement(string project,string method,string typeFullName,int elementCount,string status,double? meanNanoseconds)
        : this(project,method,typeFullName,string.Empty,elementCount,status,meanNanoseconds){
    }

    public BenchmarkElementMeasurement(string project,string method,int elementCount,string status,double? meanNanoseconds)
        : this(project,method,string.Empty,string.Empty,elementCount,status,meanNanoseconds){
    }
}

internal static class BenchmarkRowCompletionLogic{
    public static bool IsLoopCountFinalCompleted(
        IEnumerable<BenchmarkElementMeasurement> measurements,
        BenchmarkTabProgressCell leftCell,
        BenchmarkTabProgressCell rightCell,
        double thresholdNanoseconds) {
        var measurementArray = measurements as BenchmarkElementMeasurement[]??measurements.ToArray();
        var leftLoopCounts=GetThresholdLoopCounts(measurementArray,leftCell,thresholdNanoseconds);
        return leftLoopCounts.Count>0
            &&GetLatestCompletedLoopCount(measurementArray,rightCell) is { } rightLoopCount
            &&leftLoopCounts.Contains(rightLoopCount);
    }

    public static bool IsLoopCountTabFinalCompleted(
        IEnumerable<BenchmarkElementMeasurement> measurements,
        IEnumerable<BenchmarkTabProgressCell> selectedCells,
        double thresholdNanoseconds) {
        var measurementArray = measurements as BenchmarkElementMeasurement[]??measurements.ToArray();
        var cells = selectedCells.Where(static cell => cell.IsSelected).ToArray();
        if(cells.Length==0) {
            return false;
        }

        return cells
            .GroupBy(static cell => string.Join("|",cell.Method,cell.TypeFullName,cell.ArgumentKey),StringComparer.OrdinalIgnoreCase)
            .All(group => {
                var orderedCells=group.ToArray();
                var thresholdLoopCounts=GetThresholdLoopCounts(measurementArray,orderedCells[0],thresholdNanoseconds);
                if(thresholdLoopCounts.Count==0){
                    return false;
                }
                var targetLoopCount=thresholdLoopCounts.Max();
                return orderedCells.Skip(1).All(cell=>GetLatestCompletedLoopCount(measurementArray,cell)==targetLoopCount);
            });
    }

    private static global::System.Collections.Generic.HashSet<int> GetThresholdLoopCounts(
        IEnumerable<BenchmarkElementMeasurement> measurements,
        BenchmarkTabProgressCell cell,
        double thresholdNanoseconds) =>
        measurements
            .Where(result =>
                BenchmarkPairCompletionLogic.IsMeasurementForCell(result,cell)
                &&string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase)
                &&result.TotalNanoseconds is { } total
                &&total>=thresholdNanoseconds)
            .Select(static result => result.ElementCount)
            .ToHashSet();

    private static global::System.Collections.Generic.HashSet<int> GetCompletedLoopCounts(
        IEnumerable<BenchmarkElementMeasurement> measurements,
        BenchmarkTabProgressCell cell) =>
        measurements
            .Where(result =>
                BenchmarkPairCompletionLogic.IsMeasurementForCell(result,cell)
                &&string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase)
                &&result.TotalNanoseconds is not null)
            .Select(static result => result.ElementCount)
            .ToHashSet();

    private static int? GetLatestCompletedLoopCount(
        IEnumerable<BenchmarkElementMeasurement> measurements,
        BenchmarkTabProgressCell cell){
        var loopCounts=GetCompletedLoopCounts(measurements,cell);
        return loopCounts.Count==0 ? null : loopCounts.Max();
    }

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

    public static bool IsTabFinalCompleted(
        IEnumerable<BenchmarkElementMeasurement> measurements,
        IEnumerable<BenchmarkTabProgressCell> selectedCells,
        int maxElementCount,
        double thresholdNanoseconds){
        var measurementArray = measurements as BenchmarkElementMeasurement[]??measurements.ToArray();
        var cells = selectedCells.Where(static cell => cell.IsSelected).ToArray();
        if(cells.Length==0){
            return false;
        }

        return cells.All(cell =>
            BenchmarkPairCompletionLogic.HasCompletedMeasurementForCell(measurementArray,cell,maxElementCount)
            ||BenchmarkPairCompletionLogic.IsCellTerminal(
                measurementArray,
                cells,
                cell,
                cell.Project,
                ResolvePairProject(cells,cell),
                thresholdNanoseconds));
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

    private static string ResolvePairProject(IEnumerable<BenchmarkTabProgressCell> cells,BenchmarkTabProgressCell cell){
        foreach(var candidate in cells){
            if(!string.Equals(candidate.Project,cell.Project,StringComparison.OrdinalIgnoreCase)
                &&string.Equals(candidate.PairKey,cell.PairKey,StringComparison.OrdinalIgnoreCase)){
                return candidate.Project;
            }
        }

        return cell.Project;
    }
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
    RunInfinite,
    Stop,
    Pause,
    SaveDatabase,
    NewGrid,
}

internal readonly record struct BenchmarkToolbarStateInput(
    bool IsRefreshing,
    bool IsBenchmarkRunActive,
    bool IsBenchmarkPaused,
    bool CanUseRunButton,
    bool CanStopBenchmark,
    bool CanPauseBenchmark,
    bool CanSaveDatabase,
    bool CanCreateNewGrid){
    public BenchmarkToolbarStateInput(
        bool IsRefreshing,
        bool IsBenchmarkRunActive,
        bool IsBenchmarkPaused,
        bool CanUseRunButton,
        bool CanStopBenchmark,
        bool CanPauseBenchmark)
        : this(
            IsRefreshing,
            IsBenchmarkRunActive,
            IsBenchmarkPaused,
            CanUseRunButton,
            CanStopBenchmark,
            CanPauseBenchmark,
            false,
            false){
    }
}

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
                BenchmarkToolbarButtonRole.RunInfinite => new BenchmarkToolbarButtonState(
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
                BenchmarkToolbarButtonRole.SaveDatabase => CreateDirectActionState(input.CanSaveDatabase),
                BenchmarkToolbarButtonRole.NewGrid => CreateDirectActionState(input.CanCreateNewGrid),
                _ => new BenchmarkToolbarButtonState(false,false,true,false),
            };
        }

        return role switch{
            BenchmarkToolbarButtonRole.Run => CreateDirectActionState(input.CanUseRunButton),
            BenchmarkToolbarButtonRole.RunInfinite => CreateDirectActionState(input.CanUseRunButton),
            BenchmarkToolbarButtonRole.Stop => CreateDirectActionState(input.CanStopBenchmark),
            BenchmarkToolbarButtonRole.Pause => CreateDirectActionState(input.CanPauseBenchmark),
            BenchmarkToolbarButtonRole.SaveDatabase => CreateDirectActionState(input.CanSaveDatabase),
            BenchmarkToolbarButtonRole.NewGrid => CreateDirectActionState(input.CanCreateNewGrid),
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
    public static bool RequiresDialog(
        bool isBenchmarkRunActive,
        bool isBenchmarkPaused,
        bool hasUnsavedResults) =>
        isBenchmarkRunActive||isBenchmarkPaused||hasUnsavedResults;

    public static bool ShouldDiscardUnsavedResultsBeforeClose(
        bool isBenchmarkRunActive,
        bool isBenchmarkPaused,
        bool hasUnsavedResults,
        BenchmarkCloseAction action) =>
        action==BenchmarkCloseAction.StopAndClose
        &&!isBenchmarkRunActive
        &&!isBenchmarkPaused
        &&hasUnsavedResults;

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

internal readonly record struct BenchmarkRowUiSelectionCell(bool HasCase,bool IsUiSelected);

internal static class BenchmarkRowUiSelectionLogic{
    public static bool IsWholeRowSelected(IEnumerable<BenchmarkRowUiSelectionCell> cells){
        var targets = cells.Where(static cell => cell.HasCase).ToArray();
        return targets.Length>0&&targets.All(static cell => cell.IsUiSelected);
    }
}

internal static class GridCheckToggleLogic{
    public static bool? ResolveTargetCheckedState(IEnumerable<bool?> states){
        var values=states.ToArray();
        if(values.Length==0){
            return null;
        }

        return values.All(static value => value is true) ? false : true;
    }
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

