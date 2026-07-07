using System.Globalization;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;

namespace UI;
public sealed class TableRow:ObservableObject{
    private bool isSelected;
    private bool isUiSelected;
    private bool? isSelectionChecked;
    private string pairRatioTimeText = string.Empty;
    private string pairRatioAllocatedText = string.Empty;
    private string stopNText = string.Empty;
    private Color stopNTextColor = DefaultStopNTextColor;
    private FontAttributes stopNFontAttributes;
    private string leftTimeText = string.Empty;
    private string leftAllocatedText = string.Empty;
    private string rightTimeText = string.Empty;
    private string rightAllocatedText = string.Empty;
    private string benchmarkName = string.Empty;
    private string benchmarkTypeText = string.Empty;
    private string benchmarkMethodText = string.Empty;
    private bool isFinalCompleted;
    private static readonly Color DefaultStopNTextColor = Color.FromArgb("#334155");

    public TableRow(
        string rowKey,
        string methodName,
        string benchmarkName,
        BenchmarkSourceTabDefinition sourceTab,
        IReadOnlyDictionary<string,BenchmarkCase> casesByProject){
        this.RowKey=rowKey;
        this.MethodName=methodName;
        this.benchmarkName=benchmarkName;
        this.SourceTab=sourceTab;
        this.CasesByProject=casesByProject;
        this.RefreshBenchmarkDisplayParts();
        this.LeftCell=this.CreateCell(sourceTab.LeftProject);
        this.RightCell=this.CreateCell(sourceTab.RightProject);
        foreach(var cell in this.ProjectCells){
            cell.UiSelectionChanged+=this.OnCellUiSelectionChanged;
            cell.PropertyChanged+=this.OnCellPropertyChanged;
        }
        this.RefreshUiSelectionState();
    }

    public string RowKey { get; }
    public string MethodName { get; }
    public string BenchmarkName{
        get => this.benchmarkName;
        private set{
            if(this.SetProperty(ref this.benchmarkName,value)){
                this.RefreshBenchmarkDisplayParts();
            }
        }
    }
    public string BenchmarkTypeText{
        get => this.benchmarkTypeText;
        private set => this.SetProperty(ref this.benchmarkTypeText,value);
    }
    public string BenchmarkMethodText{
        get => this.benchmarkMethodText;
        private set => this.SetProperty(ref this.benchmarkMethodText,value);
    }
    public BenchmarkSourceTabDefinition SourceTab { get; }
    public IReadOnlyDictionary<string,BenchmarkCase> CasesByProject { get; }
    public ProjectCell LeftCell { get; }
    public ProjectCell RightCell { get; }
    public string CountArgument => this.GetArgumentValue(ScalableBenchmarkArgumentColumn.Count);
    public string IndexArgument => this.GetArgumentValue(ScalableBenchmarkArgumentColumn.Index);
    public string PageArgument => this.GetArgumentValue(ScalableBenchmarkArgumentColumn.Page);
    public string SegmentArgument => this.GetArgumentValue(ScalableBenchmarkArgumentColumn.Segment);
    public string MemoryArgument => this.GetArgumentValue(ScalableBenchmarkArgumentColumn.Memory);
    public string FractionArgument => this.GetArgumentValue(ScalableBenchmarkArgumentColumn.Fraction);
    public string InitialArgument => this.GetArgumentValue(ScalableBenchmarkArgumentColumn.Initial);
    public string DataArgument => this.GetArgumentValue(ScalableBenchmarkArgumentColumn.Data);

    public bool IsSelected{
        get => this.isSelected;
        set => this.SetProperty(ref this.isSelected,value);
    }

    public bool IsUiSelected{
        get => this.isUiSelected;
        private set => this.SetProperty(ref this.isUiSelected,value);
    }

    public bool IsLeftUiSelected => this.LeftCell.IsUiSelected;
    public bool IsRightUiSelected => this.RightCell.IsUiSelected;
    public bool IsLeftPartialUiSelected => this.LeftCell.IsUiSelected&&!this.IsUiSelected;
    public bool IsRightPartialUiSelected => this.RightCell.IsUiSelected&&!this.IsUiSelected;

    public bool? IsSelectionChecked{
        get => this.isSelectionChecked;
        private set => this.SetProperty(ref this.isSelectionChecked,value);
    }

    public bool IsFinalCompleted{
        get => this.isFinalCompleted;
        private set{
            if(this.SetProperty(ref this.isFinalCompleted,value)){
                foreach(var cell in this.ProjectCells){
                    cell.SetRowFinalCompleted(value);
                }
            }
        }
    }

    public string PairRatioTimeText{
        get => this.pairRatioTimeText;
        private set => this.SetProperty(ref this.pairRatioTimeText,value);
    }

    public string PairRatioAllocatedText{
        get => this.pairRatioAllocatedText;
        private set => this.SetProperty(ref this.pairRatioAllocatedText,value);
    }

    public string StopNText => string.IsNullOrWhiteSpace(this.stopNText)
        ? this.ElementCountText
        : this.stopNText;

    public double? ElementCountSortValue{
        get{
            var values = new[] {
                this.LeftCell.LastSuccessfulElementCount,
                this.RightCell.LastSuccessfulElementCount,
            }.Where(static value => value is not null).Select(static value => (double)value!.Value).ToArray();
            return values.Length==0 ? null : values.Max();
        }
    }

    private string ElementCountText{
        get{
            var left = this.LeftCell.ElementCountText;
            var right = this.RightCell.ElementCountText;
            if(string.Equals(left,right,StringComparison.Ordinal)){
                return left;
            }

            if(string.IsNullOrWhiteSpace(left)){
                return right;
            }

            if(string.IsNullOrWhiteSpace(right)){
                return left;
            }

            return left+"/"+right;
        }
    }

    public Color StopNTextColor{
        get => this.stopNTextColor;
        private set => this.SetProperty(ref this.stopNTextColor,value);
    }

    public FontAttributes StopNFontAttributes{
        get => this.stopNFontAttributes;
        private set => this.SetProperty(ref this.stopNFontAttributes,value);
    }

    public string LeftTimeText{
        get => this.leftTimeText;
        private set => this.SetProperty(ref this.leftTimeText,value);
    }

    public string LeftAllocatedText{
        get => this.leftAllocatedText;
        private set => this.SetProperty(ref this.leftAllocatedText,value);
    }

    public string RightTimeText{
        get => this.rightTimeText;
        private set => this.SetProperty(ref this.rightTimeText,value);
    }

    public string RightAllocatedText{
        get => this.rightAllocatedText;
        private set => this.SetProperty(ref this.rightAllocatedText,value);
    }

    public IReadOnlyList<BenchmarkCase> GetRunCases(){
        return this.ProjectCells
            .Where(static cell => cell.HasCase&&cell.IsSelected)
            .Select(cell => this.CasesByProject[cell.Project])
            .ToArray();
    }

    public IEnumerable<ProjectCell> ProjectCells{
        get{
            yield return this.LeftCell;
            yield return this.RightCell;
        }
    }

    public ProjectCell? GetProjectCell(string project) =>
        this.ProjectCells.FirstOrDefault(cell => string.Equals(cell.Project,project,StringComparison.OrdinalIgnoreCase));

    public void RefreshSelectionState(){
        var state = GetSelectionState(this.ProjectCells);
        this.IsSelectionChecked=state;
        this.IsSelected=state is true or null;
    }

    public void RefreshUiSelectionState(){
        this.IsUiSelected=BenchmarkRowUiSelectionLogic.IsWholeRowSelected(
            this.ProjectCells.Select(static cell => new BenchmarkRowUiSelectionCell(cell.HasCase,cell.IsUiSelected)));
        this.OnPropertyChanged(nameof(this.IsLeftUiSelected));
        this.OnPropertyChanged(nameof(this.IsRightUiSelected));
        this.OnPropertyChanged(nameof(this.IsLeftPartialUiSelected));
        this.OnPropertyChanged(nameof(this.IsRightPartialUiSelected));
    }

    public void SetFinalCompleted(bool value){
        this.IsFinalCompleted=value;
    }

    private void OnCellUiSelectionChanged(object? sender,EventArgs args){
        this.RefreshUiSelectionState();
    }

    private void OnCellPropertyChanged(object? sender,PropertyChangedEventArgs args){
        if(args.PropertyName==nameof(ProjectCell.ElementCountText)){
            this.OnPropertyChanged(nameof(this.StopNText));
        }
    }

    private static bool? GetSelectionState(IEnumerable<ProjectCell> cells){
        var targets = cells.Where(static cell => cell.HasCase).ToArray();
        if(targets.Length==0){
            return false;
        }

        var selected = targets.Count(static cell => cell.IsSelected);
        return selected==0
            ? false
            : selected==targets.Length
                ? true
                : null;
    }

    public void ApplyStoredResult(BenchmarkStoredResult result){
        if(this.GetProjectCell(result.Project) is { } cell){
            cell.ApplyStoredResult(result);
            this.SetProjectMetricText(result.Project,FormatTime(cell.Metrics),FormatAllocated(cell.Metrics));
            this.RefreshDisplayedRatios();
        }
    }

    public void ApplyRunResult(BenchmarkResultItem result,CpuSelectionItem? cpu,DateTimeOffset completedAt,bool highlightCompleted = true){
        if(this.GetProjectCell(result.Project) is { } cell){
            cell.ApplyRunResult(result,cpu,completedAt,highlightCompleted);
            this.SetProjectMetricText(result.Project,FormatTime(cell.Metrics),FormatAllocated(cell.Metrics));
            this.RefreshDisplayedRatios();
        }
    }

    public void ApplyResultCheck(string project,string checkText,string seedText){
        this.GetProjectCell(project)?.ApplyResultCheck(checkText,seedText);
    }

    public void ApplySkipped(string project,int stoppedAtElementCount,ProjectMetrics stoppedMetrics){
        this.ApplySkipped(project,stoppedAtElementCount,stoppedMetrics,null,null,false);
    }

    public void ApplySkipped(string project,int stoppedAtElementCount,ProjectMetrics stoppedMetrics,CpuSelectionItem? cpu,DateTimeOffset? measuredAt,bool metadataMissing){
        if(this.GetProjectCell(project) is { } cell){
            cell.ApplySkipped(stoppedAtElementCount,stoppedMetrics,cpu,measuredAt,metadataMissing);
        }

        this.SetProjectMetricText(project,FormatTime(stoppedMetrics),FormatAllocated(stoppedMetrics));
        this.RefreshDisplayedRatios();
    }

    public void ApplyPairStopped(string leftProject,string rightProject,int stoppedAtElementCount,bool highlight){
        var text = stoppedAtElementCount.ToString(CultureInfo.InvariantCulture);
        if(IsPair(leftProject,rightProject,this.SourceTab.LeftProject,this.SourceTab.RightProject)){
            this.stopNText=text;
            this.ApplyStopNStyle(highlight);
            this.OnPropertyChanged(nameof(this.StopNText));
            this.OnPropertyChanged(nameof(this.StopNTextColor));
            this.OnPropertyChanged(nameof(this.StopNFontAttributes));
        }
    }

    public void ClearLiveState(){
        foreach(var cell in this.ProjectCells){
            cell.ClearLiveState();
        }
    }

    public void RetainCompletedHighlightsForRunningElement(int runningElementCount){
        foreach(var cell in this.ProjectCells){
            cell.RetainCompletedHighlightForRunningElement(runningElementCount);
        }
    }

    public void ClearRunHighlights(){
        foreach(var cell in this.ProjectCells){
            cell.ClearRunHighlight();
        }
    }

    public void ClearResultDisplay(){
        this.ClearComparison(false);
        this.SetFinalCompleted(false);
    }

    public void ClearUnselectedResultDisplays(){
        foreach(var cell in this.ProjectCells){
            if(!cell.IsSelected){
                cell.ClearMetrics();
            }
        }
        this.LeftTimeText=FormatTime(this.LeftCell.Metrics);
        this.LeftAllocatedText=FormatAllocated(this.LeftCell.Metrics);
        this.RightTimeText=FormatTime(this.RightCell.Metrics);
        this.RightAllocatedText=FormatAllocated(this.RightCell.Metrics);
        this.RefreshDisplayedRatios();
    }

    public void ApplyComparison(BenchmarkComparisonRow? row,bool preserveRetainedDisplay = false){
        if(row is null){
            this.ClearComparison(preserveRetainedDisplay);
            return;
        }

        var value = row.Value;
        // 豈碑ｼ・｡後・蛟､縺後≠繧九→縺阪□縺第ｯ碑ｼ・Λ繝吶Ν繧呈峩譁ｰ縺励√そ繝ｫ縺ｮ菫晄戟蛟､縺ｯ蛻･縺ｫ谿九☆縲・
        this.ClearStopNText();
        var left = value.GetProject(this.SourceTab.LeftProject);
        var right = value.GetProject(this.SourceTab.RightProject);
        this.LeftCell.ApplyMetrics(left,preserveRetainedDisplay);
        this.RightCell.ApplyMetrics(right,preserveRetainedDisplay);
        this.LeftTimeText=FormatTime(this.LeftCell.Metrics);
        this.LeftAllocatedText=FormatAllocated(this.LeftCell.Metrics);
        this.RightTimeText=FormatTime(this.RightCell.Metrics);
        this.RightAllocatedText=FormatAllocated(this.RightCell.Metrics);
        this.RefreshDisplayedRatios();
    }

    public SortValue GetSortValue(GridSortColumn column){
        return column switch{
            GridSortColumn.BenchmarkType => SortValue.FromText(this.BenchmarkTypeText),
            GridSortColumn.BenchmarkMethod => SortValue.FromText(this.BenchmarkMethodText),
            GridSortColumn.BenchmarkName => SortValue.FromText(this.BenchmarkName),
            GridSortColumn.ElementCount => SortValue.FromNumber(this.ElementCountSortValue),
            GridSortColumn.ArgumentCount => SortValue.FromNumber(ParseArgumentNumber(this.CountArgument)),
            GridSortColumn.ArgumentIndex => SortValue.FromNumber(ParseArgumentNumber(this.IndexArgument)),
            GridSortColumn.ArgumentPage => SortValue.FromNumber(ParseArgumentNumber(this.PageArgument)),
            GridSortColumn.ArgumentSegment => SortValue.FromNumber(ParseArgumentNumber(this.SegmentArgument)),
            GridSortColumn.ArgumentMemory => SortValue.FromNumber(ParseArgumentNumber(this.MemoryArgument)),
            GridSortColumn.ArgumentFraction => SortValue.FromNumber(ParseArgumentNumber(this.FractionArgument)),
            GridSortColumn.ArgumentInitial => SortValue.FromNumber(ParseArgumentNumber(this.InitialArgument)),
            GridSortColumn.ArgumentData => SortValue.FromNumber(ParseArgumentNumber(this.DataArgument)),
            GridSortColumn.PairRatioTime => SortValue.FromRatio(this.GetDisplayedTimeRatio()),
            GridSortColumn.PairRatioAllocated => SortValue.FromRatio(this.GetDisplayedAllocatedRatio()),
            GridSortColumn.LeftTime => SortValue.FromMetric(this.LeftCell.Metrics,MetricKind.Time),
            GridSortColumn.LeftAllocated => SortValue.FromMetric(this.LeftCell.Metrics,MetricKind.Allocated),
            GridSortColumn.RightTime => SortValue.FromMetric(this.RightCell.Metrics,MetricKind.Time),
            GridSortColumn.RightAllocated => SortValue.FromMetric(this.RightCell.Metrics,MetricKind.Allocated),
            _ => SortValue.Missing,
        };
    }

    private void ClearComparison(bool preserveRetainedDisplay){
        this.PairRatioTimeText=string.Empty;
        this.PairRatioAllocatedText=string.Empty;
        this.stopNText=string.Empty;
        if(preserveRetainedDisplay){
            // 螳溯｡御ｸｭ/螳御ｺ・ｾ後・縲∵ｯ碑ｼ・｡後′豸医∴縺ｦ繧ゅそ繝ｫ縺ｮ遒ｺ螳壼､繧堤ｶｭ謖√☆繧九・
            this.LeftTimeText=FormatTime(this.LeftCell.Metrics);
            this.LeftAllocatedText=FormatAllocated(this.LeftCell.Metrics);
            this.RightTimeText=FormatTime(this.RightCell.Metrics);
            this.RightAllocatedText=FormatAllocated(this.RightCell.Metrics);
            this.RefreshDisplayedRatios();
        }else{
            this.LeftTimeText=string.Empty;
            this.LeftAllocatedText=string.Empty;
            this.RightTimeText=string.Empty;
            this.RightAllocatedText=string.Empty;
        }
        this.OnPropertyChanged(nameof(this.StopNText));
        foreach(var cell in this.ProjectCells){
            if(!preserveRetainedDisplay){
                cell.ClearMetrics();
            }
        }
    }

    private void RefreshDisplayedRatios(){
        this.PairRatioTimeText=FormatRatio(this.GetDisplayedTimeRatio());
        this.PairRatioAllocatedText=FormatRatio(this.GetDisplayedAllocatedRatio());
    }

    private RatioValue GetDisplayedTimeRatio() =>
        BenchmarkComparisonFormatter.CalculateTimeRatio(this.LeftCell.Metrics,this.RightCell.Metrics);

    private RatioValue GetDisplayedAllocatedRatio() =>
        BenchmarkComparisonFormatter.CalculateAllocatedRatio(this.LeftCell.Metrics,this.RightCell.Metrics);

    private void SetProjectMetricText(string project,string timeText,string allocatedText){
        if(string.Equals(project,this.SourceTab.LeftProject,StringComparison.OrdinalIgnoreCase)){
            this.LeftTimeText=timeText;
            this.LeftAllocatedText=allocatedText;
        }else if(string.Equals(project,this.SourceTab.RightProject,StringComparison.OrdinalIgnoreCase)){
            this.RightTimeText=timeText;
            this.RightAllocatedText=allocatedText;
        }
    }

    private void ClearStopNText(){
        this.stopNText=string.Empty;
        this.ApplyStopNStyle(false);
        this.OnPropertyChanged(nameof(this.StopNText));
    }

    private void ApplyStopNStyle(bool highlight){
        this.StopNTextColor=DefaultStopNTextColor;
        this.StopNFontAttributes=FontAttributes.None;
    }

    private static bool IsPair(string leftProject,string rightProject,string expectedLeft,string expectedRight) =>
        string.Equals(leftProject,expectedLeft,StringComparison.OrdinalIgnoreCase)
        &&string.Equals(rightProject,expectedRight,StringComparison.OrdinalIgnoreCase);

    private ProjectCell CreateCell(string project) =>
        new(project,this.CasesByProject.ContainsKey(project));

    public void RefreshBenchmarkName(int elementCount) {
        var typeFullName = this.FirstTypeFullName();
        this.BenchmarkName=ScalableBenchmarkNameFormatter.CreateBenchmarkName(typeFullName,this.MethodName);
    }

    private void RefreshBenchmarkDisplayParts(){
        var typeFullName = this.FirstTypeFullName();
        var typeText = ScalableBenchmarkNameFormatter.CreateBenchmarkTypeText(typeFullName);
        if(string.IsNullOrWhiteSpace(typeText)){
            typeText=ExtractTypeText(this.benchmarkName);
        }
        this.BenchmarkTypeText=typeText;
        this.BenchmarkMethodText=this.MethodName;
    }

    private string FirstTypeFullName(){
        foreach(var project in this.SourceTab.ProjectOrder){
            if(this.CasesByProject.TryGetValue(project,out var benchmark)&&!string.IsNullOrWhiteSpace(benchmark.TypeFullName)){
                return benchmark.TypeFullName;
            }
        }

        return string.Empty;
    }

    private static string ExtractTypeText(string benchmarkName){
        if(string.IsNullOrWhiteSpace(benchmarkName)||benchmarkName[0]!='<'){
            return string.Empty;
        }

        var end = benchmarkName.IndexOf('>');
        return end>1
            ? benchmarkName[1..end]
            : string.Empty;
    }

    private string GetArgumentValue(ScalableBenchmarkArgumentColumn column) =>
        this.CasesByProject.Values
            .Select(item => ScalableBenchmarkNameFormatter.GetDisplayArgumentValue(this.SourceTab,item.Arguments,column))
            .FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value))??string.Empty;

    public string BenchmarkArgumentTooltipText => ArgumentTooltipFormatter.CreateArgumentTooltip(this);

    private static double? ParseArgumentNumber(string text) =>
        double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out var value)
            ? value
            : null;

    private static string FormatTime(ProjectMetrics metrics){
        return metrics.HasMeasuredValue&&metrics.TimeNanoseconds is { } value
            ? BenchmarkFormatting.FormatNanoseconds(value)
            : string.Empty;
    }

    private static string FormatAllocated(ProjectMetrics metrics){
        return metrics.HasMeasuredValue&&metrics.AllocatedBytes is { } value
            ? BenchmarkFormatting.FormatBytes(value)
            : string.Empty;
    }

    private static string FormatRatio(RatioValue ratio){
        return ratio.Kind switch{
            RatioValueKind.Numeric when ratio.NumericValue is { } value => value.ToString("0.00",CultureInfo.InvariantCulture),
            RatioValueKind.BothZero => "0",
            RatioValueKind.LeftOnly => "inf",
            RatioValueKind.RightOnly => "0",
            _ => string.Empty,
        };
    }
}
