using System.Collections.ObjectModel;
using System.ComponentModel;

namespace UI;

// 縺薙％繧堤ｷｨ髮・☆繧句ｴ蜷・
// - 繧ｽ繝ｼ繝医Ν繝ｼ繝ｫ霑ｽ蜉/蜑企勁譎ゅ・蜑ｯ菴懃畑鬆・// - 菫晏ｭ倩ｨｭ螳壹く繝ｼ繧・ｴ謳肴凾繝輔か繝ｼ繝ｫ繝舌ャ繧ｯ
// - 荳ｦ縺ｳ譖ｿ縺域ｯ碑ｼ・Ο繧ｸ繝・け
internal sealed class DefaultSortController:ISortController {
    private const string SortSettingsPreferenceKey = "Seamless.SortRules.v1";

    private readonly ISortControllerContext context;
    private IReadOnlyList<SortRuleSnapshot> savedSortRuleSnapshots = [];
    private bool isApplyingSortRules;

    public DefaultSortController(ISortControllerContext context) {
        this.context=context;
    }

    public void Initialize() {
        this.savedSortRuleSnapshots=this.LoadSavedSortRules();
        this.RefreshSortColumnOptions();
        this.ResetToDefault(false);
    }

    public void SortBenchmarkRows(GridSortColumn column) {
        var descending=false;
        if(this.context.CurrentSortColumn==column&&this.context.SortRules.Count==1) {
            descending=!this.context.CurrentSortDescending;
        }

        this.SetSortRules([new SortRule(column,descending)],true);
    }

    public void AddBenchmarkSortRule() {
        var option = this.context.SortColumnOptions
            .FirstOrDefault(option => this.context.SortRules.All(rule => rule.Column!=option.Column))
            ??this.context.SortColumnOptions.FirstOrDefault();
        if(option is null) {
            return;
        }

        var rule = new SortRule(option.Column);
        this.AttachSortRule(rule);
        this.context.SortRules.Add(rule);
        this.ApplySortRuleChanges(true);
    }

    public void RemoveBenchmarkSortRule(SortRule rule) {
        this.context.SortRules.Remove(rule);
        if(this.context.SortRules.Count==0) {
            this.ResetToDefault(true);
            return;
        }

        this.ApplySortRuleChanges(true);
    }

    public void MoveBenchmarkSortRule(SortRule source,SortRule target) {
        var oldIndex = this.context.SortRules.IndexOf(source);
        var newIndex = this.context.SortRules.IndexOf(target);
        if(oldIndex<0||newIndex<0||oldIndex==newIndex) {
            return;
        }

        this.context.SortRules.Move(oldIndex,newIndex);
        this.ApplySortRuleChanges(true);
    }

    public void MoveBenchmarkSortRuleUp(SortRule rule) {
        var index = this.context.SortRules.IndexOf(rule);
        if(index<=0) {
            return;
        }

        this.context.SortRules.Move(index,index-1);
        this.ApplySortRuleChanges(true);
    }

    public void MoveBenchmarkSortRuleDown(SortRule rule) {
        var index = this.context.SortRules.IndexOf(rule);
        if(index<0||index>=this.context.SortRules.Count-1) {
            return;
        }

        this.context.SortRules.Move(index,index+1);
        this.ApplySortRuleChanges(true);
    }

    public void SetBenchmarkSortRuleColumn(SortRule rule,GridSortColumn column) {
        rule.Column=column;
        this.ApplySortRuleChanges(true);
    }

    public void ToggleBenchmarkSortRuleDirection(SortRule rule) {
        rule.Descending=!rule.Descending;
        this.ApplySortRuleChanges(true);
    }

    public void ResetBenchmarkSortRules() =>
        this.ResetToDefault(true);

    public void RefreshBenchmarkSortColumns() {
        this.context.HasBenchmarkTypeColumn=true;
        this.RefreshSortColumnOptions();
        var rules = this.CreateValidSortRules(this.context.SortRules).ToArray();
        if(rules.Length==0) {
            rules=this.CreateDefaultSortRules().ToArray();
        }
        this.SetSortRules(rules,false);
    }

    public void RefreshSortColumnOptions() {
        var columns = this.CreateAvailableSortColumns().ToArray();
        this.context.SortColumnOptions.Clear();
        foreach(var column in columns) {
            this.context.SortColumnOptions.Add(new SortColumnOption(column,SortRule.GetColumnText(column)));
        }
    }

    public void ApplyCurrentSort() {
        if(this.context.IsRunActive||this.context.IsPaused) {
            return;
        }
        var sortRules = this.GetEffectiveSortRules();
        foreach(var state in this.context.SourceTabViewStates) {
            if(state.Rows.Count<2) {
                continue;
            }

            var ordered = state.Rows
                .OrderBy(row => row,Comparer<TableRow>.Create((left,right) => {
                    foreach(var rule in sortRules) {
                        var result = SortValue.Compare(
                            left.GetSortValue(rule.Column),
                            right.GetSortValue(rule.Column),
                            rule.Descending);
                        if(result!=0) {
                            return result;
                        }
                    }

                    var typeResult = string.Compare(left.BenchmarkTypeText,right.BenchmarkTypeText,StringComparison.OrdinalIgnoreCase);
                    if(typeResult!=0) {
                        return typeResult;
                    }
                    var methodResult = string.Compare(left.BenchmarkMethodText,right.BenchmarkMethodText,StringComparison.OrdinalIgnoreCase);
                    return methodResult!=0
                        ? methodResult
                        : string.Compare(left.RowKey,right.RowKey,StringComparison.OrdinalIgnoreCase);
                }))
                .ToArray();
            state.Rows.ReplaceRange(ordered);
        }
        this.context.UpdateAllSourceTabChartsForSort();
    }

    public void ApplyFinalBenchmarkSort() =>
        this.SetSortRules([new SortRule(GridSortColumn.PairRatioTime,true)],true);

    private void SetSortRules(IEnumerable<SortRule> rules,bool save) {
        this.isApplyingSortRules=true;
        try {
            this.context.SortRules.Clear();
            foreach(var rule in rules) {
                this.AttachSortRule(rule);
                this.context.SortRules.Add(rule);
            }
        }
        finally {
            this.isApplyingSortRules=false;
        }
        this.ApplySortRuleChanges(save);
    }

    private void AttachSortRule(SortRule rule) {
        rule.PropertyChanged+=this.OnSortRulePropertyChanged;
    }

    private void OnSortRulePropertyChanged(object? sender,PropertyChangedEventArgs args) {
        if(this.isApplyingSortRules) {
            return;
        }
        if(args.PropertyName==nameof(SortRule.Column)
            ||args.PropertyName==nameof(SortRule.Descending)) {
            this.ApplySortRuleChanges(true);
        }
    }

    private void ApplySortRuleChanges(bool save) {
        this.RefreshCurrentSortIndicator();
        this.context.NotifySortSummaryChanged();
        this.ApplyCurrentSort();
        if(save) {
            this.SaveSortRules();
        }
    }

    private IEnumerable<GridSortColumn> CreateAvailableSortColumns() {
        if(this.context.HasBenchmarkTypeColumn) {
            yield return GridSortColumn.BenchmarkType;
        }
        yield return GridSortColumn.BenchmarkMethod;
        yield return GridSortColumn.ElementCount;
        foreach(var column in BenchmarkArgumentColumns.Get(this.context.SelectedSourceDefinition)) {
            yield return column switch {
                ScalableBenchmarkArgumentColumn.Count => GridSortColumn.ArgumentCount,
                ScalableBenchmarkArgumentColumn.Index => GridSortColumn.ArgumentIndex,
                ScalableBenchmarkArgumentColumn.Page => GridSortColumn.ArgumentPage,
                ScalableBenchmarkArgumentColumn.Segment => GridSortColumn.ArgumentSegment,
                ScalableBenchmarkArgumentColumn.Memory => GridSortColumn.ArgumentMemory,
                ScalableBenchmarkArgumentColumn.Fraction => GridSortColumn.ArgumentFraction,
                ScalableBenchmarkArgumentColumn.Initial => GridSortColumn.ArgumentInitial,
                ScalableBenchmarkArgumentColumn.Data => GridSortColumn.ArgumentData,
                _ => GridSortColumn.BenchmarkMethod,
            };
        }
        yield return GridSortColumn.PairRatioTime;
        yield return GridSortColumn.PairRatioAllocated;
        yield return GridSortColumn.LeftTime;
        yield return GridSortColumn.LeftAllocated;
        yield return GridSortColumn.RightTime;
        yield return GridSortColumn.RightAllocated;
    }

    private IReadOnlyList<SortRule> CreateValidSortRules(IEnumerable<SortRule> source) {
        var allowed = this.CreateAvailableSortColumns().ToHashSet();
        var used = new HashSet<GridSortColumn>();
        return source
            .Where(rule => allowed.Contains(rule.Column)&&used.Add(rule.Column))
            .Select(rule => new SortRule(rule.Column,rule.Descending))
            .ToArray();
    }

    private IReadOnlyList<SortRule> CreateDefaultSortRules() {
        if(this.context.HasBenchmarkTypeColumn) {
            return [
                new SortRule(GridSortColumn.BenchmarkType),
                new SortRule(GridSortColumn.BenchmarkMethod),
            ];
        }

        return [new SortRule(GridSortColumn.BenchmarkMethod)];
    }

    private void ResetToDefault(bool save) =>
        this.SetSortRules(this.CreateDefaultSortRules(),save);

    private IReadOnlyList<SortRule> GetEffectiveSortRules() {
        var rules = this.CreateValidSortRules(this.context.SortRules);
        return rules.Count>0 ? rules : this.CreateDefaultSortRules();
    }

    private void RefreshCurrentSortIndicator() {
        var first = this.GetEffectiveSortRules().FirstOrDefault();
        if(first is null) {
            this.context.CurrentSortColumn=GridSortColumn.BenchmarkMethod;
            this.context.CurrentSortDescending=false;
            return;
        }

        this.context.CurrentSortColumn=first.Column;
        this.context.CurrentSortDescending=first.Descending;
    }

    private IReadOnlyList<SortRuleSnapshot> LoadSavedSortRules() {
        try {
            var json = this.context.UiPreferences.Get(SortSettingsPreferenceKey,string.Empty);
            return string.IsNullOrWhiteSpace(json)
                ? []
                : JsonSerializer.Deserialize<SortRuleSnapshot[]>(json)?? [];
        }
        catch(Exception ex) {
            StartupDiagnostics.Write("sort settings load failed.",ex);
            return [];
        }
    }

    private void SaveSortRules() {
        try {
            var snapshot = this.context.SortRules
                .Select(static rule => new SortRuleSnapshot(rule.Column,rule.Descending))
                .ToArray();
            this.savedSortRuleSnapshots=snapshot;
            this.context.UiPreferences.Set(SortSettingsPreferenceKey,JsonSerializer.Serialize(snapshot));
        }
        catch(Exception ex) {
            StartupDiagnostics.Write("sort settings save failed.",ex);
        }
    }
}

