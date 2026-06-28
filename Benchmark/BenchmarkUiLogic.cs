namespace Benchmark;

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

internal static class BenchmarkTimestampDisplay{
    public const string Format = "yyyy/MM/dd HH:mm:ss";

    public static string FormatLocal(DateTimeOffset timestamp) =>
        timestamp.ToLocalTime().ToString(Format,System.Globalization.CultureInfo.InvariantCulture);
}

internal static class BenchmarkCsvTimestampParser{
    public static DateTimeOffset? TryExtractTimestamp(string path){
        var fileTimestamp = TryParseTimestamp(System.IO.Path.GetFileNameWithoutExtension(path));
        if(fileTimestamp is not null){
            return fileTimestamp;
        }

        var directory = System.IO.Path.GetDirectoryName(path);
        while(!string.IsNullOrWhiteSpace(directory)){
            var folderTimestamp = TryParseTimestamp(System.IO.Path.GetFileName(directory));
            if(folderTimestamp is not null){
                return folderTimestamp;
            }

            directory=System.IO.Path.GetDirectoryName(directory);
        }

        return null;
    }

    private static DateTimeOffset? TryParseTimestamp(string text){
        foreach(var pattern in TimestampPatterns()){
            var match = System.Text.RegularExpressions.Regex.Match(text,pattern.Regex,System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if(!match.Success){
                continue;
            }

            var value = match.Groups["value"].Value;
            if(DateTime.TryParseExact(value,pattern.Format,System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.AssumeLocal,out var date)){
                return new DateTimeOffset(date);
            }
        }

        return null;
    }

    private static IEnumerable<(string Regex,string Format)> TimestampPatterns(){
        yield return (@"(?<value>\d{8}-\d{6})","yyyyMMdd-HHmmss");
        yield return (@"(?<value>\d{14})","yyyyMMddHHmmss");
        yield return (@"(?<value>\d{4}-\d{2}-\d{2}[-_ ]\d{2}[-_]\d{2}[-_]\d{2})","yyyy-MM-dd HH-mm-ss");
        yield return (@"(?<value>\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2})","yyyy-MM-dd_HH-mm-ss");
        yield return (@"(?<value>\d{4}-\d{2}-\d{2}-\d{2}-\d{2}-\d{2})","yyyy-MM-dd-HH-mm-ss");
    }
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

internal sealed record BenchmarkElementMeasurement(
    string Project,
    string Method,
    int ElementCount,
    string Status,
    double? MeanNanoseconds);

internal static class BenchmarkSkippedDisplayLogic{
    public static int? FindPairStoppedAt(
        IEnumerable<BenchmarkElementMeasurement> measurements,
        string method,
        string project,
        int selectedElementCount,
        double thresholdNanoseconds){
        var pairProjects = BenchmarkComparisonPairs.GetPairProjects(project);
        if(pairProjects is null){
            return null;
        }

        return measurements
            .Where(result => string.Equals(result.Method,method,StringComparison.OrdinalIgnoreCase)
                &&result.ElementCount<selectedElementCount
                &&string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase)
                &&result.MeanNanoseconds is not null
                &&pairProjects.Contains(result.Project,StringComparer.OrdinalIgnoreCase))
            .GroupBy(static result => result.ElementCount)
            .OrderBy(static group => group.Key)
            .Where(group => pairProjects.All(pairProject => group.Any(result => string.Equals(result.Project,pairProject,StringComparison.OrdinalIgnoreCase))))
            .Where(group => group.Any(result => result.MeanNanoseconds>thresholdNanoseconds))
            .Select(group => (int?)group.Key)
            .FirstOrDefault();
    }

    public static int? FindLatestCompletedPairElementCountBefore(
        IEnumerable<BenchmarkElementMeasurement> measurements,
        string method,
        string leftProject,
        string rightProject,
        int selectedElementCount) =>
        measurements
            .Where(result => string.Equals(result.Method,method,StringComparison.OrdinalIgnoreCase)
                &&result.ElementCount>0
                &&result.ElementCount<selectedElementCount
                &&string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase)
                &&result.MeanNanoseconds is not null
                &&(string.Equals(result.Project,leftProject,StringComparison.OrdinalIgnoreCase)
                    ||string.Equals(result.Project,rightProject,StringComparison.OrdinalIgnoreCase)))
            .GroupBy(static result => result.ElementCount)
            .OrderByDescending(static group => group.Key)
            .Where(group => group.Any(result => string.Equals(result.Project,leftProject,StringComparison.OrdinalIgnoreCase))
                &&group.Any(result => string.Equals(result.Project,rightProject,StringComparison.OrdinalIgnoreCase)))
            .Select(group => (int?)group.Key)
            .FirstOrDefault();
}

internal enum BenchmarkToolbarButtonRole{
    Run,
    Stop,
    Pause,
    Save,
    SaveAs,
    Open,
}

internal readonly record struct BenchmarkToolbarStateInput(
    bool IsRefreshing,
    bool IsBenchmarkRunActive,
    bool IsBenchmarkPaused,
    bool CanUseRunButton,
    bool CanStopBenchmark,
    bool CanPauseBenchmark,
    bool CanSaveBenchmarkResults,
    bool CanSaveAsBenchmarkResults,
    bool CanOpenResults);

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
                BenchmarkToolbarButtonRole.Save => new BenchmarkToolbarButtonState(true,false,true,false),
                BenchmarkToolbarButtonRole.SaveAs => new BenchmarkToolbarButtonState(true,false,true,false),
                BenchmarkToolbarButtonRole.Open => new BenchmarkToolbarButtonState(true,false,true,false),
                _ => new BenchmarkToolbarButtonState(false,false,true,false),
            };
        }

        return role switch{
            BenchmarkToolbarButtonRole.Run => CreateDirectActionState(input.CanUseRunButton),
            BenchmarkToolbarButtonRole.Stop => CreateDirectActionState(input.CanStopBenchmark),
            BenchmarkToolbarButtonRole.Pause => CreateDirectActionState(input.CanPauseBenchmark),
            BenchmarkToolbarButtonRole.Save => CreateDirectActionState(input.CanSaveBenchmarkResults),
            BenchmarkToolbarButtonRole.SaveAs => CreateDirectActionState(input.CanSaveAsBenchmarkResults),
            BenchmarkToolbarButtonRole.Open => CreateDirectActionState(input.CanOpenResults),
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
        isBenchmarkPaused&&role is BenchmarkToolbarButtonRole.Pause
            or BenchmarkToolbarButtonRole.Save
            or BenchmarkToolbarButtonRole.SaveAs
            or BenchmarkToolbarButtonRole.Open;
}

internal enum BenchmarkCloseAction{
    Cancel,
    SaveAndClose,
    DiscardAndClose,
    StopAndClose,
    StopSaveAndClose,
    StopDiscardAndClose,
}

internal static class BenchmarkCloseDialogLogic{
    public static IReadOnlyList<(string Text,BenchmarkCloseAction Action,BenchmarkToolbarButtonRole Kind)> CreateOptions(
        bool isBenchmarkRunActive,
        bool isBenchmarkPaused,
        bool hasUnsavedResults){
        if(isBenchmarkPaused){
            return [
                ("停止して閉じる",BenchmarkCloseAction.StopAndClose,BenchmarkToolbarButtonRole.Stop),
                ("キャンセル",BenchmarkCloseAction.Cancel,BenchmarkToolbarButtonRole.Open),
            ];
        }

        if(isBenchmarkRunActive&&hasUnsavedResults){
            return [
                ("中止して保存して終了",BenchmarkCloseAction.StopSaveAndClose,BenchmarkToolbarButtonRole.Save),
                ("中止して破棄して終了",BenchmarkCloseAction.StopDiscardAndClose,BenchmarkToolbarButtonRole.Stop),
                ("キャンセル",BenchmarkCloseAction.Cancel,BenchmarkToolbarButtonRole.Open),
            ];
        }

        if(isBenchmarkRunActive){
            return [
                ("中止して終了",BenchmarkCloseAction.StopAndClose,BenchmarkToolbarButtonRole.Stop),
                ("キャンセル",BenchmarkCloseAction.Cancel,BenchmarkToolbarButtonRole.Open),
            ];
        }

        return hasUnsavedResults
            ? [
                ("保存して終了",BenchmarkCloseAction.SaveAndClose,BenchmarkToolbarButtonRole.Save),
                ("破棄して終了",BenchmarkCloseAction.DiscardAndClose,BenchmarkToolbarButtonRole.Stop),
                ("キャンセル",BenchmarkCloseAction.Cancel,BenchmarkToolbarButtonRole.Open),
            ]
            : [
                ("キャンセル",BenchmarkCloseAction.Cancel,BenchmarkToolbarButtonRole.Open),
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
    public int ColumnCount => Rows.Count==0 ? 0 : Rows.Max(static row => row.LogicalProcessorIndexes.Count);
}

internal sealed record CpuTableRowDefinition(string Label,IReadOnlyList<int> LogicalProcessorIndexes);

#if TSAVORITE2_0BETA3
[global::Seamless.FLinq.LinqIntercept(false)]
#endif
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
