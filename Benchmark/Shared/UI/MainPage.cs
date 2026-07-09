using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Syncfusion.Maui.Buttons;
using Syncfusion.Maui.DataGrid;
using Syncfusion.Maui.Toolkit.Charts;
using Syncfusion.Maui.Toolkit.TabView;
using Seamless.UI.Storage;
using UI.ViewModels;

namespace UI;

public sealed partial class MainPage:ContentPage {
    private const double CpuCheckBoxWidth = 34;
    private const double CpuCheckBoxHeight = 28;
    private const double BenchmarkSelectionColumnWidth = 52;
    private const double BenchmarkAggregateCheckBoxSize = 32;
    private const double BenchmarkCellCheckBoxWidth = 34;
    private const double BenchmarkCellCheckBoxHeight = 28;
    private const double BenchmarkTypeColumnWidth = 96;
    private const double BenchmarkNameColumnWidth = 320;
    private const double BenchmarkCpuColumnWidth = 58;
    private const double BenchmarkArgumentColumnWidth = 72;
    private const double BenchmarkCountArgumentColumnWidth = 78;
    private const double BenchmarkMemoryArgumentColumnWidth = 96;
    private const double BenchmarkCompactArgumentColumnWidth = 64;
    private const double BenchmarkProjectStatusColumnWidth = 176;
    private const double BenchmarkProjectMetricColumnWidth = 78;
    private const double BenchmarkPairStopNColumnWidth = 72;
    private const double BenchmarkCellCheckSlotWidth = 38;
    private const double BenchmarkHeaderCheckSlotWidth = 38;
    private const double BenchmarkScrollBarSize = 20;
    private const double CpuLogicalColumnWidth = 420;
    private const double CpuLogicalCheckColumnWidth = 38;
    private const double CpuLogicalNumberWidth = 26;
    private const double LowerTabBarHeight = 34;
    private const double CpuSectionHeaderHeight = 34;
    private const double BenchmarkGridRowHeight = 24;
    private const double BenchmarkGridHeaderRowHeight = 30;
    private const double BenchmarkGridStackedHeaderRowCount = 1;
    private const double BenchmarkGridExtraHeight = 2;
    private const double BenchmarkGridCellFontSize = 10;
    private const double BenchmarkGridDetailFontSize = 9;
    private const double BenchmarkHoverCellExpansionPadding = 14;
    private const double CpuGridRowHeight = 24;
    private const double CpuGridHeaderRowHeight = 30;
    private const double CpuGridCellFontSize = 10;
    private const double CpuSectionGridHeaderHeight = CpuGridHeaderRowHeight;
    private const double CpuSectionRowHeight = CpuGridRowHeight;
    private const double CpuSectionGridExtraHeight = 2;
    private const double CpuSectionSpacing = 3;
    private const double CpuListSpacing = 10;
    private const double CpuContentRowSpacing = 10;
    private readonly RunnerViewModel? viewModel;
    private readonly Grid? rootLayout;
    private View? activeDialogOverlay;
    private TaskCompletionSource<string>? activeDialogCompletion;
    private string activeDialogFallbackValue = string.Empty;
    private readonly List<Action> collapsibleSectionRefreshers = [];
    private bool isImprovementPromptExpanded;
    private readonly Dictionary<BenchmarkToolbarButtonRole,VisualElement> toolbarButtons = [];
    private readonly List<SfDataGrid> dataGridUpdateBatchTargets = [];
    private readonly HashSet<SfDataGrid> dataGridUpdateBatchStartedTargets = [];
    private readonly Dictionary<BenchmarkSourceTab,SfDataGrid> benchmarkGridsBySourceTab = [];
    private SfDataGrid? benchmarkGrid;
    private SfTabView? sourceTabView;
    private int dataGridUpdateBatchDepth;
    private IDispatcherTimer? pausedToolbarPulseTimer;
    private bool pausedToolbarPulseOn;
    private IDispatcherTimer? benchmarkSelectionPulseTimer;
    private bool benchmarkSelectionPulseOn;
    private bool isApplyingSourceTabViewSelection;
    private bool isDataGridUpdateBatchSubscribed;
    private BenchmarkSortRule? draggedBenchmarkSortRule;

    public double BenchmarkSelectionFillOpacity => this.benchmarkSelectionPulseOn ? 0.22 : 0.08;

#if WINDOWS
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern short GetKeyState(int virtualKey);
#endif

    public MainPage(RunnerViewModel viewModel) {
        try {
            StartupDiagnostics.Write("MainPage constructor started.");
            this.Title=BenchmarkAppProfile.Title;
            this.BackgroundColor=Color.FromArgb("#F8FAFC");
            this.viewModel=viewModel;
            this.BindingContext=this.viewModel;
            this.SubscribeDataGridUpdateBatch();

            var toolbar = this.CreateToolbar();
            var content = this.CreateBenchmarkPanelContent();

            this.rootLayout=new Grid {
                RowDefinitions= {
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = GridLength.Star },
                },
                Children= {
                    toolbar,
                    content,
                },
            };
            this.Content=this.rootLayout;
            Grid.SetRow(content,1);
            this.Loaded+=this.OnLoadedAsync;
            this.Unloaded+=this.OnUnloaded;
            this.StartBenchmarkSelectionPulse();
            StartupDiagnostics.Write("MainPage constructor completed.");
        }
        catch(Exception ex) {
            StartupDiagnostics.Write("MainPage constructor failed.",ex);
            this.Content=StartupDiagnostics.CreateErrorPage("Benchmark page failed",ex).Content;
        }
    }

    private void OnUnloaded(object? sender,EventArgs args) {
        this.StopBenchmarkSelectionPulse();
        this.UnsubscribeDataGridUpdateBatch();
        this.EndAllDataGridViewUpdateBatches();
    }

    private async void OnLoadedAsync(object? sender,EventArgs args) {
        if(this.viewModel is null) {
            return;
        }

        try {
            this.SubscribeDataGridUpdateBatch();
            StartupDiagnostics.Write("MainPage InitializeAsync started.");
            await this.viewModel.InitializeAsync();
            StartupDiagnostics.Write("MainPage InitializeAsync completed.");
        }
        catch(Exception ex) {
            StartupDiagnostics.Write("MainPage InitializeAsync failed.",ex);
            this.viewModel.StatusText="起動時読み込み失敗: "+ex.Message;
        }
    }

    private void SubscribeDataGridUpdateBatch() {
        if(this.viewModel is null||this.isDataGridUpdateBatchSubscribed) {
            return;
        }

        this.viewModel.DataGridUpdateBatchChanged+=this.OnDataGridUpdateBatchChanged;
        this.isDataGridUpdateBatchSubscribed=true;
    }

    private void UnsubscribeDataGridUpdateBatch() {
        if(this.viewModel is null||!this.isDataGridUpdateBatchSubscribed) {
            return;
        }

        this.viewModel.DataGridUpdateBatchChanged-=this.OnDataGridUpdateBatchChanged;
        this.isDataGridUpdateBatchSubscribed=false;
    }

    private void RegisterDataGridUpdateBatchTarget(SfDataGrid grid) {
        if(this.dataGridUpdateBatchTargets.Contains(grid)) {
            return;
        }

        this.dataGridUpdateBatchTargets.Add(grid);
        grid.HandlerChanged+=(_,_) => {
            if(this.dataGridUpdateBatchDepth>0) {
                this.BeginDataGridViewUpdate(grid);
            }
        };
        if(this.dataGridUpdateBatchDepth>0) {
            this.BeginDataGridViewUpdate(grid);
        }
    }

    private void OnDataGridUpdateBatchChanged(object? sender,BenchmarkDataGridUpdateBatchEventArgs args) {
        StartupDiagnostics.BeginInvokeOnMainThread(
            args.IsBeginning ? "data grid update batch begin" : "data grid update batch end",
            () => {
                if(args.IsBeginning) {
                    this.BeginDataGridViewUpdateBatch();
                    return;
                }

                this.EndDataGridViewUpdateBatch();
            });
    }

    private void BeginDataGridViewUpdateBatch() {
        if(this.dataGridUpdateBatchDepth++>0) {
            return;
        }

        foreach(var grid in this.dataGridUpdateBatchTargets.ToArray()) {
            this.BeginDataGridViewUpdate(grid);
        }
    }

    private void EndDataGridViewUpdateBatch() {
        if(this.dataGridUpdateBatchDepth<=0) {
            return;
        }

        this.dataGridUpdateBatchDepth--;
        if(this.dataGridUpdateBatchDepth>0) {
            return;
        }

        this.EndStartedDataGridViewUpdates();
    }

    private void EndAllDataGridViewUpdateBatches() {
        this.dataGridUpdateBatchDepth=0;
        this.EndStartedDataGridViewUpdates();
    }

    private void BeginDataGridViewUpdate(SfDataGrid grid) {
        if(this.dataGridUpdateBatchStartedTargets.Contains(grid)) {
            return;
        }

        try {
            var view = grid.View;
            if(view is null) {
                return;
            }

            view.BeginInit();
            this.dataGridUpdateBatchStartedTargets.Add(grid);
        }
        catch(Exception ex) {
            StartupDiagnostics.Write("SfDataGrid View.BeginInit failed.",ex);
        }
    }

    private void EndStartedDataGridViewUpdates() {
        var started = this.dataGridUpdateBatchStartedTargets.ToArray();
        Array.Reverse(started);
        foreach(var grid in started) {
            try {
                grid.View?.EndInit();
            }
            catch(Exception ex) {
                StartupDiagnostics.Write("SfDataGrid View.EndInit failed.",ex);
            }
        }
        this.dataGridUpdateBatchStartedTargets.Clear();
    }

    public async Task<bool> RequestCloseAsync() {
        if(this.viewModel is null) {
            return true;
        }

        // 結果は実行中も SQLite へ自動保存されるため、終了確認は実行中/一時停止中のときだけ出す。
        var isBenchmarkActive = this.viewModel.IsBenchmarkRunActive;
        var isBenchmarkPaused = this.viewModel.IsBenchmarkPaused;
        if(!isBenchmarkActive&&!isBenchmarkPaused) {
            await this.viewModel.PrepareForCloseAsync();
            return true;
        }

        var action = await this.ShowCloseConfirmDialogAsync(isBenchmarkActive,isBenchmarkPaused,this.viewModel.HasUnsavedBenchmarkResults);
        if(action==BenchmarkCloseAction.Cancel) {
            return false;
        }

        if(isBenchmarkActive||isBenchmarkPaused) {
            this.viewModel.StatusText="終了前に停止しています...";
            var stopTask = this.viewModel.StopAsync();
            if(!await this.WaitForBenchmarkStopBeforeCloseAsync(stopTask)) {
                return false;
            }
        }

        await this.viewModel.PrepareForCloseAsync();
        return true;
    }

    private async Task<bool> WaitForBenchmarkStopBeforeCloseAsync(Task stopTask) {
        var quickWait = await Task.WhenAny(stopTask,Task.Delay(350));
        if(ReferenceEquals(quickWait,stopTask)) {
            await stopTask;
            return true;
        }

        var dialogTask = this.ShowConfirmDialogAsync(
            "停止待ち",
            "実行中のベンチマークを停止しています。停止しない場合は強制終了できます。",
            [
                new ConfirmDialogOption("強制終了","force",ConfirmDialogButtonKind.Danger),
                new ConfirmDialogOption("キャンセル","cancel",ConfirmDialogButtonKind.Neutral),
            ]);
        var completed = await Task.WhenAny(stopTask,dialogTask);
        if(ReferenceEquals(completed,stopTask)) {
            if(this.rootLayout is { } root) {
                this.CompleteActiveDialog(root,"stopped");
            }
            await stopTask;
            return true;
        }

        var action = await dialogTask;
        if(string.Equals(action,"force",StringComparison.OrdinalIgnoreCase)) {
            return true;
        }

        return false;
    }

    private void ShowBenchmarkSortDialog() {
        var root = this.rootLayout;
        if(root is null||this.viewModel is null) {
            return;
        }

        this.CompleteActiveDialog(root,string.Empty);
        var overlay = this.CreateBenchmarkSortDialogOverlay(() => this.CompleteActiveDialog(root,string.Empty));
        this.activeDialogOverlay=overlay;
        this.activeDialogCompletion=null;
        this.activeDialogFallbackValue=string.Empty;
        root.Add(overlay,0,0);
        Grid.SetRowSpan(overlay,2);
    }

    private View CreateBenchmarkSortDialogOverlay(Action close) {
        var overlay = new Grid {
            BackgroundColor=Color.FromRgba(15,23,42,110),
            Padding=new Thickness(24),
            HorizontalOptions=LayoutOptions.Fill,
            VerticalOptions=LayoutOptions.Fill,
            InputTransparent=false,
        };
        overlay.BindingContext=this.viewModel;
        var card = new Border {
            BackgroundColor=Colors.White,
            Stroke=Color.FromArgb("#CBD5E1"),
            StrokeThickness=1,
            StrokeShape=new Microsoft.Maui.Controls.Shapes.RoundRectangle {
                CornerRadius=14,
            },
            Padding=new Thickness(20,18),
            MaximumWidthRequest=820,
            HorizontalOptions=LayoutOptions.Center,
            VerticalOptions=LayoutOptions.Center,
        };
        var content = new Grid {
            RowDefinitions= {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(300) },
                new RowDefinition { Height = GridLength.Auto },
            },
            RowSpacing=12,
        };
        content.Add(new Label {
            Text="ソート設定",
            FontSize=18,
            FontAttributes=FontAttributes.Bold,
            TextColor=Color.FromArgb("#0F172A"),
            HorizontalTextAlignment=TextAlignment.Center,
        },0,0);
        var summary = new Label {
            FontSize=12,
            TextColor=Color.FromArgb("#334155"),
            LineBreakMode=LineBreakMode.TailTruncation,
            HorizontalTextAlignment=TextAlignment.Center,
        };
        summary.SetBinding(Label.TextProperty,nameof(RunnerViewModel.BenchmarkSortSummaryText));
        content.Add(summary,0,1);
        content.Add(this.CreateBenchmarkSortRuleList(),0,2);
        content.Add(this.CreateBenchmarkSortDialogButtons(close),0,3);
        card.Content=content;
        overlay.Children.Add(card);
        return overlay;
    }

    private View CreateBenchmarkSortRuleList() {
        var list = new CollectionView {
            SelectionMode=SelectionMode.None,
            ItemsLayout=new LinearItemsLayout(ItemsLayoutOrientation.Vertical) {
                ItemSpacing=3,
            },
            ItemTemplate=new DataTemplate(() => this.CreateBenchmarkSortRuleRow()),
        };
        list.SetBinding(ItemsView.ItemsSourceProperty,nameof(RunnerViewModel.BenchmarkSortRules));
        return new Border {
            Stroke=Color.FromArgb("#CBD5E1"),
            StrokeThickness=1,
            BackgroundColor=Color.FromArgb("#F8FAFC"),
            Padding=new Thickness(8),
            Content=list,
        };
    }

    private View CreateBenchmarkSortRuleRow() {
        var row = new Grid {
            ColumnDefinitions= {
                new ColumnDefinition { Width = new GridLength(32) },
                new ColumnDefinition { Width = new GridLength(220) },
                new ColumnDefinition { Width = new GridLength(32) },
                new ColumnDefinition { Width = new GridLength(32) },
                new ColumnDefinition { Width = new GridLength(32) },
                new ColumnDefinition { Width = new GridLength(32) },
            },
            ColumnSpacing=5,
            Padding=new Thickness(6,3),
            BackgroundColor=Colors.White,
            MinimumHeightRequest=34,
        };
        var handle = new Image {
            Source=ImageSource.FromFile("drag_handle.png"),
            WidthRequest=18,
            HeightRequest=18,
            Aspect=Aspect.AspectFit,
            VerticalOptions=LayoutOptions.Center,
            HorizontalOptions=LayoutOptions.Center,
        };
        handle.SetValue(ToolTipProperties.TextProperty,"ドラッグで並び替え");
        var picker = new Picker {
            HeightRequest=28,
            ItemDisplayBinding=new Binding(nameof(BenchmarkSortColumnOption.DisplayName)),
            Margin=new Thickness(0),
        };
        picker.ItemsSource=this.viewModel?.BenchmarkSortColumnOptions;
        var direction = this.CreateSortRuleIconButton("sort_asc.png","昇順/降順を切り替え");
        var up = this.CreateSortRuleIconButton("arrow_up.png","上へ移動");
        var down = this.CreateSortRuleIconButton("arrow_down.png","下へ移動");
        var remove = this.CreateSortRuleIconButton("delete.png","削除");
        foreach(var editButton in new[] { picker as VisualElement,direction,up,down,remove }) {
            editButton.SetBinding(VisualElement.IsEnabledProperty,new Binding(nameof(RunnerViewModel.IsSortRuleEditingEnabled),source: this.viewModel));
        }

        void RefreshPicker() {
            if(row.BindingContext is not BenchmarkSortRule rule||this.viewModel is null) {
                return;
            }
            picker.ItemsSource=this.viewModel.BenchmarkSortColumnOptions;
            picker.SelectedItem=this.viewModel.BenchmarkSortColumnOptions.FirstOrDefault(option => option.Column==rule.Column);
            direction.Source=ImageSource.FromFile(rule.Descending ? "sort_desc.png" : "sort_asc.png");
            direction.SetValue(ToolTipProperties.TextProperty,rule.Descending ? "降順" : "昇順");
        }

        row.BindingContextChanged+=(_,_) => RefreshPicker();
        direction.BindingContextChanged+=(_,_) => RefreshPicker();
        picker.SelectedIndexChanged+=(_,_) => {
            if(row.BindingContext is BenchmarkSortRule rule
                &&picker.SelectedItem is BenchmarkSortColumnOption option) {
                this.viewModel?.SetBenchmarkSortRuleColumn(rule,option.Column);
            }
        };
        direction.Clicked+=(_,_) => {
            if(row.BindingContext is BenchmarkSortRule rule) {
                this.viewModel?.ToggleBenchmarkSortRuleDirection(rule);
                RefreshPicker();
            }
        };
        up.Clicked+=(_,_) => {
            if(row.BindingContext is BenchmarkSortRule rule) {
                this.viewModel?.MoveBenchmarkSortRuleUp(rule);
            }
        };
        down.Clicked+=(_,_) => {
            if(row.BindingContext is BenchmarkSortRule rule) {
                this.viewModel?.MoveBenchmarkSortRuleDown(rule);
            }
        };
        remove.Clicked+=(_,_) => {
            if(row.BindingContext is BenchmarkSortRule rule) {
                this.viewModel?.RemoveBenchmarkSortRule(rule);
            }
        };

        var drag = new DragGestureRecognizer();
        drag.DragStarting+=(_,args) => {
            if(this.viewModel?.IsSortRuleEditingEnabled!=true) {
                args.Cancel=true;
                return;
            }
            this.draggedBenchmarkSortRule=row.BindingContext as BenchmarkSortRule;
        };
        var drop = new DropGestureRecognizer();
        drop.Drop+=(_,_) => {
            if(this.viewModel?.IsSortRuleEditingEnabled==true
                &&this.draggedBenchmarkSortRule is not null
                &&row.BindingContext is BenchmarkSortRule target) {
                this.viewModel?.MoveBenchmarkSortRule(this.draggedBenchmarkSortRule,target);
            }
            this.draggedBenchmarkSortRule=null;
        };
        row.GestureRecognizers.Add(drag);
        row.GestureRecognizers.Add(drop);

        row.Add(handle,0,0);
        row.Add(picker,1,0);
        row.Add(direction,2,0);
        row.Add(up,3,0);
        row.Add(down,4,0);
        row.Add(remove,5,0);
        return row;
    }

    private ImageButton CreateSortRuleIconButton(string imageSource,string tooltipText) {
        var button = new ImageButton {
            Source=ImageSource.FromFile(imageSource),
            WidthRequest=28,
            HeightRequest=28,
            Padding=new Thickness(5),
            CornerRadius=5,
            BackgroundColor=Colors.White,
            BorderColor=Color.FromArgb("#CBD5E1"),
            BorderWidth=1,
            Aspect=Aspect.AspectFit,
            Margin=new Thickness(0),
        };
        button.SetValue(ToolTipProperties.TextProperty,tooltipText);
        return button;
    }

    private View CreateBenchmarkSortDialogButtons(Action close) {
        var add = this.CreateConfirmDialogButton(new ConfirmDialogOption("追加","add",ConfirmDialogButtonKind.Neutral));
        var reset = this.CreateConfirmDialogButton(new ConfirmDialogOption("既定順","reset",ConfirmDialogButtonKind.Neutral));
        var closeButton = this.CreateConfirmDialogButton(new ConfirmDialogOption("閉じる","close",ConfirmDialogButtonKind.Primary));
        add.SetBinding(VisualElement.IsEnabledProperty,new Binding(nameof(RunnerViewModel.IsSortRuleEditingEnabled),source: this.viewModel));
        reset.SetBinding(VisualElement.IsEnabledProperty,new Binding(nameof(RunnerViewModel.IsSortRuleEditingEnabled),source: this.viewModel));
        add.Clicked+=(_,_) => this.viewModel?.AddBenchmarkSortRule();
        reset.Clicked+=(_,_) => this.viewModel?.ResetBenchmarkSortRules();
        closeButton.Clicked+=(_,_) => close();
        return new HorizontalStackLayout {
            Spacing=10,
            HorizontalOptions=LayoutOptions.Center,
            Children= {
                add,
                reset,
                closeButton,
            },
        };
    }

    private View CreateToolbar() {
        var runButton = this.CreateToolbarTextButton("計測");
        this.BindToolbarButtonState(runButton,BenchmarkToolbarButtonRole.Run);
        var stopButton = this.CreateToolbarIconButton(
            "stop.png",
            "停止");
        this.BindToolbarButtonState(stopButton,BenchmarkToolbarButtonRole.Stop);
        var saveDatabaseButton = this.CreateToolbarTextButton("DB保存");
        this.BindToolbarButtonState(saveDatabaseButton,BenchmarkToolbarButtonRole.SaveDatabase);
        var newGridButton = this.CreateToolbarTextButton("新規");
        this.BindToolbarButtonState(newGridButton,BenchmarkToolbarButtonRole.NewGrid);
#if DEBUG
        var infiniteButton = new Button {
            Text="永久実行",
            HeightRequest=30,
            MinimumWidthRequest=92,
            Padding=new Thickness(10,0),
            FontSize=12,
            FontAttributes=FontAttributes.Bold,
            BackgroundColor=Color.FromArgb("#F8FAFC"),
            BorderColor=Color.FromArgb("#CBD5E1"),
            BorderWidth=1,
            TextColor=Color.FromArgb("#334155"),
            CornerRadius=6,
        };
        this.BindToolbarButtonState(infiniteButton,BenchmarkToolbarButtonRole.RunInfinite);
#endif
        var thresholdPicker = new Picker {
            WidthRequest=112,
            HeightRequest=30,
            ItemDisplayBinding=new Binding(nameof(ThresholdOption.DisplayName)),
            Margin=new Thickness(0),
        };
        thresholdPicker.SetBinding(Picker.ItemsSourceProperty,nameof(RunnerViewModel.ThresholdOptions));
        thresholdPicker.SetBinding(Picker.SelectedItemProperty,nameof(RunnerViewModel.SelectedThreshold));
        thresholdPicker.SetBinding(IsEnabledProperty,nameof(RunnerViewModel.CanChangeBenchmarkConditions));

        var executionProfilePicker = new Picker {
            WidthRequest=96,
            HeightRequest=30,
            ItemDisplayBinding=new Binding(nameof(ExecutionProfileOption.DisplayName)),
            Margin=new Thickness(0),
        };
        executionProfilePicker.SetBinding(Picker.ItemsSourceProperty,nameof(RunnerViewModel.ExecutionProfiles));
        executionProfilePicker.SetBinding(Picker.SelectedItemProperty,nameof(RunnerViewModel.SelectedExecutionProfile));
        executionProfilePicker.SetBinding(IsEnabledProperty,nameof(RunnerViewModel.CanChangeBenchmarkConditions));

        var runPicker = new Picker {
            WidthRequest=220,
            HeightRequest=30,
            ItemDisplayBinding=new Binding(nameof(BenchmarkRunOption.DisplayName)),
            Margin=new Thickness(0),
        };
        runPicker.SetBinding(Picker.ItemsSourceProperty,nameof(RunnerViewModel.RunOptions));
        runPicker.SetBinding(Picker.SelectedItemProperty,nameof(RunnerViewModel.SelectedRunOption));
        runPicker.SetBinding(IsEnabledProperty,nameof(RunnerViewModel.CanSelectRunHistory));

#if DEBUG
        var clearRunHistoryButton = this.CreateToolbarIconButton("delete.png","履歴消去");
        clearRunHistoryButton.SetBinding(IsEnabledProperty,nameof(RunnerViewModel.CanClearRunHistory));
        clearRunHistoryButton.Clicked+=async (_,_) => await this.ShowClearRunHistoryConfirmDialogAsync();
#endif

        var grid = new Grid {
            Padding=new Thickness(8,4,8,3),
            RowDefinitions= {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
            },
            ColumnDefinitions= {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
            },
            ColumnSpacing=6,
        };
        grid.Add(this.ToolbarLabel("測定時間"),0,0);
        grid.Add(thresholdPicker,1,0);
        grid.Add(this.ToolbarLabel("モード"),2,0);
        grid.Add(executionProfilePicker,3,0);
        grid.Add(runButton,4,0);
        grid.Add(stopButton,5,0);
        grid.Add(saveDatabaseButton,6,0);
        grid.Add(newGridButton,7,0);
#if DEBUG
        grid.Add(infiniteButton,8,0);
        grid.Add(this.ToolbarLabel("結果"),9,0);
        grid.Add(runPicker,10,0);
        grid.Add(clearRunHistoryButton,11,0);
#else
        grid.Add(this.ToolbarLabel("結果"),8,0);
        grid.Add(runPicker,9,0);
#endif
        var preview = this.CreateStatusIconPreview();
        grid.Add(preview,0,1);
#if DEBUG
        Grid.SetColumnSpan(preview,12);
#else
        Grid.SetColumnSpan(preview,10);
#endif
        return grid;
    }

    private View CreateStatusIconPreview() {
        var layout = new HorizontalStackLayout {
            Spacing=10,
            Padding=new Thickness(0,2,0,0),
            VerticalOptions=LayoutOptions.Center,
        };
        foreach(var item in new[] {
            ("待機",BenchmarkStatusImageNames.Waiting),
            ("N完了",BenchmarkStatusImageNames.Completed),
            ("完全完了",BenchmarkStatusImageNames.FinalCompleted),
            ("失敗",BenchmarkStatusImageNames.Failed),
            ("測定値なし",BenchmarkStatusImageNames.NoMeasurement),
            ("CPU P",BenchmarkCpuCoreImageNames.Performance),
            ("CPU E",BenchmarkCpuCoreImageNames.Efficiency),
        }) {
            layout.Add(this.CreateStatusIconPreviewItem(item.Item1,item.Item2));
        }

        return layout;
    }

    private View CreateStatusIconPreviewItem(string text,string imageSource) {
        var grid = new Grid {
            ColumnDefinitions= {
                new ColumnDefinition { Width = new GridLength(18) },
                new ColumnDefinition { Width = GridLength.Auto },
            },
            ColumnSpacing=3,
            VerticalOptions=LayoutOptions.Center,
        };
        grid.Add(new Image {
            Source=ImageSource.FromFile(imageSource),
            WidthRequest=16,
            HeightRequest=16,
            Aspect=Aspect.AspectFit,
            VerticalOptions=LayoutOptions.Center,
            HorizontalOptions=LayoutOptions.Center,
        },0,0);
        grid.Add(new Label {
            Text=text,
            FontSize=9,
            TextColor=Color.FromArgb("#334155"),
            VerticalTextAlignment=TextAlignment.Center,
        },1,0);
        return grid;
    }

    private ImageButton CreateToolbarIconButton(string imageSource,string tooltipText) {
        var button = new ImageButton {
            Source=ImageSource.FromFile(imageSource),
            WidthRequest=30,
            HeightRequest=30,
            BackgroundColor=Color.FromArgb("#F8FAFC"),
            BorderColor=Color.FromArgb("#CBD5E1"),
            BorderWidth=1,
            CornerRadius=6,
            Padding=new Thickness(5),
            Margin=new Thickness(0),
            Aspect=Aspect.AspectFit,
        };
        if(tooltipText==nameof(RunnerViewModel.RunButtonTooltipText)) {
            button.SetBinding(ToolTipProperties.TextProperty,nameof(RunnerViewModel.RunButtonTooltipText));
        }
        else {
            button.SetValue(ToolTipProperties.TextProperty,tooltipText);
        }
        return button;
    }

    private Button CreateToolbarTextButton(string text) {
        return new Button {
            Text=text,
            HeightRequest=30,
            MinimumWidthRequest=72,
            Padding=new Thickness(10,0),
            FontSize=12,
            BackgroundColor=Color.FromArgb("#F8FAFC"),
            BorderColor=Color.FromArgb("#CBD5E1"),
            BorderWidth=1,
            TextColor=Color.FromArgb("#334155"),
            CornerRadius=6,
        };
    }

    private void BindToolbarButtonState(ImageButton button,BenchmarkToolbarButtonRole role) {
        this.toolbarButtons[role]=button;
        button.Clicked+=async (_,_) => await this.HandleToolbarButtonClickAsync(role);
        this.BindToolbarButtonStateCore(button,role);
    }

    private void BindToolbarButtonState(Button button,BenchmarkToolbarButtonRole role) {
        this.toolbarButtons[role]=button;
        button.Clicked+=async (_,_) => await this.HandleToolbarButtonClickAsync(role);
        this.BindToolbarButtonStateCore(button,role);
    }

    private void BindToolbarButtonStateCore(VisualElement button,BenchmarkToolbarButtonRole role) {
        void Apply() {
            if(this.viewModel is null) {
                return;
            }

            this.ApplyToolbarButtonState(button,this.viewModel.GetToolbarButtonState(role));
        }

        Apply();
        if(this.viewModel is not null) {
            this.viewModel.PropertyChanged+=(_,args) => {
                if(args.PropertyName==nameof(RunnerViewModel.IsBenchmarkRunActive)
                    ||args.PropertyName==nameof(RunnerViewModel.IsBenchmarkPaused)
                    ||args.PropertyName==nameof(RunnerViewModel.IsRefreshing)
                    ||args.PropertyName==nameof(RunnerViewModel.CanRunBenchmark)
                    ||args.PropertyName==nameof(RunnerViewModel.CanResumeBenchmark)
                    ||args.PropertyName==nameof(RunnerViewModel.CanUseRunToolbarButton)
                    ||args.PropertyName==nameof(RunnerViewModel.CanSaveDatabase)
                    ||args.PropertyName==nameof(RunnerViewModel.CanCreateNewGrid)
                    ||args.PropertyName==nameof(RunnerViewModel.HasSelectedBenchmarkCells)
                    ||args.PropertyName==nameof(RunnerViewModel.HasUnsavedBenchmarkResults)
                    ||args.PropertyName==nameof(RunnerViewModel.ToolbarStateInput)) {
                    StartupDiagnostics.BeginInvokeOnMainThread("toolbar state apply "+role,Apply);
                }
            };
        }
    }

    private void ApplyToolbarButtonState(VisualElement button,BenchmarkToolbarButtonState state) {
        button.IsEnabled=state.IsEnabled;
        button.InputTransparent=false;
        button.Opacity=state.ShowDisabledVisual ? 0.45 : 1;
        var borderColor = state.ShouldPulse&&this.pausedToolbarPulseOn
            ? Color.FromArgb("#EA580C")
            : Color.FromArgb("#CBD5E1");
        var backgroundColor = state.ShouldPulse&&this.pausedToolbarPulseOn
            ? Color.FromArgb("#FFF7ED")
            : Color.FromArgb("#F8FAFC");
        if(button is ImageButton imageButton) {
            imageButton.BorderColor=borderColor;
            imageButton.BackgroundColor=backgroundColor;
        }else if(button is Button textButton) {
            textButton.BorderColor=borderColor;
            textButton.BackgroundColor=backgroundColor;
        }
        button.Scale=state.ShouldPulse&&this.pausedToolbarPulseOn ? 1.08 : 1;
        this.RefreshPausedToolbarPulse();
    }

    private async Task HandleToolbarButtonClickAsync(BenchmarkToolbarButtonRole role) {
        if(this.viewModel is null) {
            return;
        }

        var state = this.viewModel.GetToolbarButtonState(role);
        if(!state.IsEnabled) {
            return;
        }

        if(BenchmarkToolbarPauseGuardLogic.RequiresDecisionDialog(role,this.viewModel.IsBenchmarkPaused)) {
            await this.ShowPausedToolbarDecisionDialogAsync(role);
            return;
        }

        if(!state.InvokesPrimaryAction) {
            return;
        }

        await this.ExecuteToolbarButtonCommandAsync(role);
    }

    private async Task ExecuteToolbarButtonCommandAsync(BenchmarkToolbarButtonRole role) {
        if(this.viewModel is null) {
            return;
        }

        switch(role) {
            case BenchmarkToolbarButtonRole.Run:
                if(this.viewModel.RunCommand.CanExecute(null)) {
                    await this.viewModel.RunCommand.ExecuteAsync(null);
                }
                break;
#if DEBUG
            case BenchmarkToolbarButtonRole.RunInfinite:
                if(this.viewModel.RunInfiniteCommand.CanExecute(null)) {
                    await this.viewModel.RunInfiniteCommand.ExecuteAsync(null);
                }
                break;
#endif
            case BenchmarkToolbarButtonRole.Stop:
                if(this.viewModel.StopCommand.CanExecute(null)) {
                    await this.viewModel.StopCommand.ExecuteAsync(null);
                }
                break;
            case BenchmarkToolbarButtonRole.Pause:
                if(this.viewModel.PauseCommand.CanExecute(null)) {
                    await this.viewModel.PauseCommand.ExecuteAsync(null);
                }
                break;
            case BenchmarkToolbarButtonRole.SaveDatabase:
                if(this.viewModel.SaveDatabaseCommand.CanExecute(null)) {
                    await this.viewModel.SaveDatabaseCommand.ExecuteAsync(null);
                }
                break;
            case BenchmarkToolbarButtonRole.NewGrid:
                if(this.viewModel.NewGridCommand.CanExecute(null)) {
                    this.viewModel.NewGridCommand.Execute(null);
                }
                break;
        }
    }

#if DEBUG
    private async Task ShowClearRunHistoryConfirmDialogAsync() {
        if(this.viewModel is null||!this.viewModel.ClearRunHistoryCommand.CanExecute(null)) {
            return;
        }

        var result = await this.ShowConfirmDialogAsync(
            "履歴消去",
            "DB内の実行履歴をすべて消去します。よろしいですか？",
            [
                new ConfirmDialogOption("全履歴を消去","clear",ConfirmDialogButtonKind.Danger),
                new ConfirmDialogOption("キャンセル","cancel",ConfirmDialogButtonKind.Neutral),
            ]);
        if(result!="clear"||this.viewModel is null||!this.viewModel.ClearRunHistoryCommand.CanExecute(null)) {
            return;
        }

        await this.viewModel.ClearRunHistoryCommand.ExecuteAsync(null);
    }
#endif

    private async Task ShowPausedToolbarDecisionDialogAsync(BenchmarkToolbarButtonRole role) {
        var result = await this.ShowConfirmDialogAsync(
            "一時停止中",
            CreatePausedToolbarDecisionMessage(role),
            [
                new ConfirmDialogOption("再開","resume",ConfirmDialogButtonKind.Primary),
                new ConfirmDialogOption("停止","stop",ConfirmDialogButtonKind.Danger),
                new ConfirmDialogOption("キャンセル","cancel",ConfirmDialogButtonKind.Neutral),
            ]);
        switch(result) {
            case "resume":
                await this.ExecuteToolbarButtonCommandAsync(BenchmarkToolbarButtonRole.Run);
                break;
            case "stop":
                await this.ExecuteToolbarButtonCommandAsync(BenchmarkToolbarButtonRole.Stop);
                break;
        }
    }

    private static string CreatePausedToolbarDecisionMessage(BenchmarkToolbarButtonRole role) =>
        "一時停止中です。続けるなら再開、終了するなら停止を選んでください。";

    private void RefreshPausedToolbarPulse() {
        if(this.viewModel is null) {
            this.StopPausedToolbarPulse();
            return;
        }

        var shouldPulse = this.toolbarButtons.Keys
            .Any(role => this.viewModel.GetToolbarButtonState(role).ShouldPulse);
        if(!shouldPulse) {
            this.StopPausedToolbarPulse();
            return;
        }

        if(this.pausedToolbarPulseTimer is not null) {
            return;
        }

        this.pausedToolbarPulseTimer=this.Dispatcher.CreateTimer();
        this.pausedToolbarPulseTimer.Interval=TimeSpan.FromMilliseconds(420);
        this.pausedToolbarPulseTimer.Tick+=(_,_) => {
            this.pausedToolbarPulseOn=!this.pausedToolbarPulseOn;
            this.ApplyToolbarButtonVisualStates();
        };
        this.pausedToolbarPulseTimer.Start();
    }

    private void StopPausedToolbarPulse() {
        this.pausedToolbarPulseTimer?.Stop();
        this.pausedToolbarPulseTimer=null;
        if(!this.pausedToolbarPulseOn) {
            return;
        }

        this.pausedToolbarPulseOn=false;
        this.ApplyToolbarButtonVisualStates();
    }

    private void StartBenchmarkSelectionPulse() {
        if(this.benchmarkSelectionPulseTimer is not null) {
            return;
        }

        this.benchmarkSelectionPulseTimer=this.Dispatcher.CreateTimer();
        this.benchmarkSelectionPulseTimer.Interval=TimeSpan.FromMilliseconds(420);
        this.benchmarkSelectionPulseTimer.Tick+=(_,_) => {
            this.benchmarkSelectionPulseOn=!this.benchmarkSelectionPulseOn;
            this.OnPropertyChanged(nameof(this.BenchmarkSelectionFillOpacity));
        };
        this.benchmarkSelectionPulseTimer.Start();
    }

    private void StopBenchmarkSelectionPulse() {
        this.benchmarkSelectionPulseTimer?.Stop();
        this.benchmarkSelectionPulseTimer=null;
    }

    private void ApplyToolbarButtonVisualStates() {
        if(this.viewModel is null) {
            return;
        }

        foreach(var pair in this.toolbarButtons) {
            this.ApplyToolbarButtonState(pair.Value,this.viewModel.GetToolbarButtonState(pair.Key));
        }
    }

    private Label ToolbarLabel(string text) {
        return new Label {
            Text=text,
            FontSize=12,
            VerticalTextAlignment=TextAlignment.Center,
            TextColor=Color.FromArgb("#334155"),
            Margin=new Thickness(0),
        };
    }

    private View CreateBenchmarkPanelContent() {
        var tabView = new SfTabView {
            TabBarHeight=30,
            TabBarBackground=Color.FromArgb("#F8FAFC"),
            IndicatorBackground=Colors.Transparent,
            HeaderItemSpacing=2,
            EnableSwiping=false,
            ContentTransitionDuration=0,
            BackgroundColor=Colors.White,
            VerticalOptions=LayoutOptions.Fill,
            AutomationId="Benchmark.SourceTabView",
        };
        AutomationProperties.SetIsInAccessibleTree(tabView,true);
        var tabItems = new TabItemCollection();
        var loadedSourceTabContents = new HashSet<int>();
        var sourceTabIndex=0;
        foreach(var option in this.viewModel?.SourceTabs?? []) {
            var capturedIndex=sourceTabIndex;
            var header = this.CreateSourceTabHeader(option,tabView,capturedIndex);
            var state = this.viewModel?.GetSourceTabViewState(option.Definition.Tab);
            var tabItem = new SfTabItem {
                Header=option.HeaderText,
                HeaderContent=header,
                FontSize=12,
                AutomationId="Benchmark.SourceTabItem."+option.Definition.Tab,
                Content=capturedIndex==0
                    ? this.CreateSourceTabContent(state)
                    : this.CreateSourceTabPlaceholder(option),
            };
            AutomationProperties.SetIsInAccessibleTree(tabItem,true);
            if(capturedIndex==0) {
                loadedSourceTabContents.Add(capturedIndex);
            }
            option.PropertyChanged+=(_,args) => {
                if(args.PropertyName==nameof(SourceTabOption.HeaderText)) {
                    StartupDiagnostics.BeginInvokeOnMainThread("source tab header text "+option.Definition.Id,() => tabItem.Header=option.HeaderText);
                }
            };
            tabItems.Add(tabItem);
            sourceTabIndex++;
        }

        tabView.Items=tabItems;
        tabView.SelectedIndex=0;
        this.sourceTabView=tabView;
        void EnsureSelectedSourceTabContent() {
            if(this.viewModel is null) {
                return;
            }

            var index = tabView.SelectedIndex;
            if(index<0||index>=this.viewModel.SourceTabs.Count||loadedSourceTabContents.Contains(index)) {
                StartupDiagnostics.AddBreadcrumb("Source tab content skip index="+index.ToString(System.Globalization.CultureInfo.InvariantCulture));
                return;
            }

            var option = this.viewModel.SourceTabs[index];
            var state = this.viewModel.GetSourceTabViewState(option.Definition.Tab);
            StartupDiagnostics.AddBreadcrumb("Source tab content create "+option.Definition.Id+" index="+index.ToString(System.Globalization.CultureInfo.InvariantCulture));
            tabItems[index].Content=this.CreateSourceTabContent(state);
            loadedSourceTabContents.Add(index);
            StartupDiagnostics.AddBreadcrumb("Source tab content created "+option.Definition.Id+" rows="+state.Rows.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        tabView.PropertyChanged+=(_,args) => {
            if(args.PropertyName!=nameof(SfTabView.SelectedIndex)||this.viewModel is null) {
                return;
            }

            StartupDiagnostics.AddBreadcrumb(
                "Source tab view selected index changed index="
                +tabView.SelectedIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)
                +" running="+this.viewModel.IsBenchmarkRunActive.ToString(System.Globalization.CultureInfo.InvariantCulture)
                +" selected="+this.viewModel.SelectedSourceTab.Id);
            EnsureSelectedSourceTabContent();
            if(this.isApplyingSourceTabViewSelection) {
                this.SelectBenchmarkGridForActiveSource();
                return;
            }

            var index = tabView.SelectedIndex;
            if(index>=0&&index<this.viewModel.SourceTabs.Count) {
                this.viewModel.SelectedSourceTab=this.viewModel.SourceTabs[index];
            }
            this.SelectBenchmarkGridForActiveSource();
        };
        if(this.viewModel is not null) {
            this.viewModel.PropertyChanged+=(_,args) => {
                if(args.PropertyName==nameof(RunnerViewModel.SelectedSourceTab)
                    ||args.PropertyName==nameof(RunnerViewModel.HasBenchmarkTypeColumn)) {
                    StartupDiagnostics.BeginInvokeOnMainThread("source tab vm property "+args.PropertyName,() => {
                        this.SyncSourceTabViewSelection();
                        EnsureSelectedSourceTabContent();
                        this.SelectBenchmarkGridForActiveSource();
                    });
                }else if(args.PropertyName==nameof(RunnerViewModel.LatestBenchmarkAutoScrollRequest)) {
                    var request = this.viewModel.LatestBenchmarkAutoScrollRequest;
                    if(request is not null) {
                        StartupDiagnostics.BeginInvokeOnMainThread("latest benchmark auto scroll "+request.SourceTab,() => this.ScrollBenchmarkGridToLatestExecutionAsync(request));
                    }
                }
            };
        }
        var lowerContent = this.CreateBenchmarkTabContent();
        var overallProgress = this.CreateOverallSourceProgressView();
        var grid = new Grid {
            RowDefinitions= {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star },
                new RowDefinition { Height = GridLength.Auto },
            },
            RowSpacing=1,
            BackgroundColor=Colors.White,
            Padding=new Thickness(4,1,4,2),
            Children= {
                overallProgress,
                tabView,
                lowerContent,
            },
        };
        Grid.SetRow(tabView,1);
        Grid.SetRow(lowerContent,2);
        return grid;
    }

    private View CreateOverallSourceProgressView() {
        var text = new Label {
            FontSize=12,
            TextColor=Color.FromArgb("#334155"),
            VerticalTextAlignment=TextAlignment.Center,
            HorizontalTextAlignment=TextAlignment.Center,
            LineBreakMode=LineBreakMode.NoWrap,
            Margin=new Thickness(8,2),
        };
        var badge = new Border {
            Stroke=Color.FromArgb("#CBD5E1"),
            StrokeThickness=1,
            BackgroundColor=Color.FromArgb("#F8FAFC"),
            StrokeShape=new Microsoft.Maui.Controls.Shapes.RoundRectangle {
                CornerRadius=new CornerRadius(6),
            },
            Content=text,
            HorizontalOptions=LayoutOptions.Start,
            VerticalOptions=LayoutOptions.Center,
        };
        var layout = new Grid {
            HeightRequest=18,
            HorizontalOptions=LayoutOptions.Start,
            Children= {
                badge,
            },
        };
        void Apply() {
            if(this.viewModel is null) {
                text.Text="確定 0/0";
                return;
            }

            var progress = this.viewModel.OverallSourceTabProgress;
            text.Text="確定 "+(progress.Total<=0 ? "0/0" : progress.FractionText);
            text.TextColor=progress.Total<=0 ? Color.FromArgb("#94A3B8") : Color.FromArgb("#334155");
        }
        layout.Loaded+=(_,_) => Apply();
        if(this.viewModel is not null) {
            this.viewModel.PropertyChanged+=(_,args) => {
                if(args.PropertyName==nameof(RunnerViewModel.OverallSourceTabProgress)) {
                    StartupDiagnostics.BeginInvokeOnMainThread("overall source progress apply",Apply);
                }
            };
        }
        Apply();
        return layout;
    }

    private View CreateTabHeader(TabHeaderOptionBase option,Action onSelect,IReadOnlyList<View>? trailingViews = null) {
        var label = new Label {
            FontSize=12,
            HorizontalTextAlignment=TextAlignment.Center,
            VerticalTextAlignment=TextAlignment.Center,
            LineBreakMode=LineBreakMode.TailTruncation,
            Margin=new Thickness(8,3,4,3),
        };
        label.SetBinding(Label.TextProperty,new Binding(nameof(TabHeaderOptionBase.HeaderText),source: option));
        label.SetBinding(Label.FontAttributesProperty,new Binding(nameof(TabHeaderOptionBase.HeaderFontAttributes),source: option));
        label.SetBinding(Label.TextColorProperty,new Binding(nameof(TabHeaderOptionBase.HeaderTextColor),source: option));

        var content = new Grid {
            ColumnDefinitions= {
                new ColumnDefinition { Width = GridLength.Star },
            },
            ColumnSpacing=2,
            Children= {
                label,
            },
        };
        if(trailingViews is { Count: >0 }) {
            foreach(var trailing in trailingViews) {
                content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                content.Add(trailing,content.ColumnDefinitions.Count-1,0);
            }
        }

        var layout = new Border {
            StrokeThickness=1,
            StrokeShape=new Microsoft.Maui.Controls.Shapes.RoundRectangle {
                CornerRadius=new CornerRadius(6),
            },
            Content=content,
            Margin=new Thickness(1,0),
        };
        layout.SetBinding(BackgroundColorProperty,new Binding(nameof(TabHeaderOptionBase.HeaderBackgroundColor),source: option));
        layout.SetBinding(Border.StrokeProperty,new Binding(nameof(TabHeaderOptionBase.HeaderStroke),source: option));
        var tap = new TapGestureRecognizer();
        tap.Tapped+=(_,_) => onSelect();
        layout.GestureRecognizers.Add(tap);
        return layout;
    }

    private View CreateSourceTabHeader(SourceTabOption option,SfTabView tabView,int tabIndex) {
        var progress = new BenchmarkCircularProgressView {
            WidthRequest=18,
            HeightRequest=18,
            VerticalOptions=LayoutOptions.Center,
            HorizontalOptions=LayoutOptions.Center,
        };
        progress.SetBinding(BenchmarkCircularProgressView.ProgressProperty,new Binding(nameof(SourceTabOption.ProgressFraction),source: option));
        progress.SetBinding(BenchmarkCircularProgressView.OutlineColorProperty,new Binding(nameof(SourceTabOption.ProgressOutlineColor),source: option));
        progress.SetBinding(IsVisibleProperty,new Binding(nameof(SourceTabOption.HasProgress),source: option));
        progress.SetBinding(ToolTipProperties.TextProperty,new Binding(nameof(SourceTabOption.ProgressTooltipText),source: option));
        var completedIcon = new Image {
            WidthRequest=14,
            HeightRequest=14,
            VerticalOptions=LayoutOptions.Center,
            HorizontalOptions=LayoutOptions.Center,
            InputTransparent=true,
        };
        completedIcon.SetBinding(Image.SourceProperty,new Binding(nameof(SourceTabOption.CompletionIconSource),source: option,converter: new ImageFileSourceConverter()));
        completedIcon.SetBinding(IsVisibleProperty,new Binding(nameof(SourceTabOption.IsCompletionIconVisible),source: option));
        completedIcon.SetBinding(ToolTipProperties.TextProperty,new Binding(nameof(SourceTabOption.ProgressTooltipText),source: option));
        var progressHost = new Grid {
            WidthRequest=20,
            HeightRequest=20,
            VerticalOptions=LayoutOptions.Center,
            HorizontalOptions=LayoutOptions.Center,
            Margin=new Thickness(0,0,2,0),
            Children= {
                progress,
                completedIcon,
            },
        };
        progressHost.SetBinding(IsVisibleProperty,new Binding(nameof(SourceTabOption.HasProgress),source: option));
        progressHost.SetBinding(ToolTipProperties.TextProperty,new Binding(nameof(SourceTabOption.ProgressTooltipText),source: option));
        var spinner = new ActivityIndicator {
            WidthRequest=14,
            HeightRequest=14,
            VerticalOptions=LayoutOptions.Center,
            HorizontalOptions=LayoutOptions.Center,
            Color=Color.FromArgb("#00FF00"),
        };
        spinner.SetBinding(ActivityIndicator.IsRunningProperty,new Binding(nameof(SourceTabOption.IsRunning),source: option));
        spinner.SetBinding(IsVisibleProperty,new Binding(nameof(SourceTabOption.IsRunning),source: option));
        spinner.SetBinding(ToolTipProperties.TextProperty,new Binding(nameof(SourceTabOption.ProgressTooltipText),source: option));
        var spinnerHost = new Grid {
            WidthRequest=16,
            HeightRequest=20,
            VerticalOptions=LayoutOptions.Center,
            HorizontalOptions=LayoutOptions.Center,
            Margin=new Thickness(0,0,6,0),
            Children= {
                spinner,
            },
        };
        spinnerHost.SetBinding(ToolTipProperties.TextProperty,new Binding(nameof(SourceTabOption.ProgressTooltipText),source: option));

        var header = this.CreateTabHeader(
            option,
            () => {
                if(tabView.SelectedIndex!=tabIndex) {
                    tabView.SelectedIndex=tabIndex;
                }
                if(this.viewModel is not null&&this.viewModel.SelectedSourceTab!=option) {
                    this.viewModel.SelectedSourceTab=option;
                }
            },
            [progressHost,spinnerHost]);
        header.AutomationId="Benchmark.SourceTab."+option.Definition.Tab;
        return header;
    }

    private static double CalculateProgressWidth(double trackWidth,BenchmarkTabProgress progress) {
        if(trackWidth<=0||progress.Total<=0) {
            return 0;
        }

        return Math.Round(trackWidth*progress.Fraction,MidpointRounding.AwayFromZero);
    }

    private void SyncSourceTabViewSelection() {
        if(this.viewModel is null||this.sourceTabView is null) {
            return;
        }

        var index = -1;
        for(var i=0;i<this.viewModel.SourceTabs.Count;i++) {
            if(this.viewModel.SourceTabs[i]==this.viewModel.SelectedSourceTab) {
                index=i;
                break;
            }
        }
        if(index<0||this.sourceTabView.SelectedIndex==index) {
            return;
        }

        this.isApplyingSourceTabViewSelection=true;
        try {
            this.sourceTabView.SelectedIndex=index;
        }
        finally {
            this.isApplyingSourceTabViewSelection=false;
        }
    }

    private async void ScrollBenchmarkGridToLatestExecutionAsync(BenchmarkAutoScrollRequest request) {
        this.SyncSourceTabViewSelection();
        this.ScrollBenchmarkGridToMethod(request.MethodName,request.RowKey);
        await Task.Delay(150);
        StartupDiagnostics.BeginInvokeOnMainThread("benchmark grid delayed auto scroll "+request.SourceTab,() => this.ScrollBenchmarkGridToMethod(request.MethodName,request.RowKey));
    }

    private void ScrollBenchmarkGridToMethod(string methodName,string rowKey) {
        if(this.viewModel is null||this.benchmarkGrid is null) {
            return;
        }

        var rowIndex = -1;
        for(var index=0;index<this.viewModel.BenchmarkRows.Count;index++) {
            var row = this.viewModel.BenchmarkRows[index];
            if(!string.IsNullOrWhiteSpace(rowKey)
                &&string.Equals(row.RowKey,rowKey,StringComparison.OrdinalIgnoreCase)) {
                rowIndex=index;
                break;
            }

            if(string.IsNullOrWhiteSpace(rowKey)
                &&string.Equals(row.MethodName,methodName,StringComparison.OrdinalIgnoreCase)) {
                rowIndex=index;
                break;
            }
        }

        if(rowIndex<0) {
            return;
        }

        this.TryScrollBenchmarkGridToRow(rowIndex);
    }

    private void TryScrollBenchmarkGridToRow(int rowIndex) {
        if(this.benchmarkGrid is null) {
            return;
        }

        try {
            var headerOffset = this.benchmarkGrid.StackedHeaderRows.Count+1;
            this.TryInvokeMember(this.benchmarkGrid,"ScrollToRowIndex",rowIndex+headerOffset);
            this.TryInvokeMember(this.benchmarkGrid,"ScrollToRowIndex",rowIndex+1);
            this.TryInvokeMember(this.benchmarkGrid,"ScrollToRowIndex",rowIndex);
            var visualContainer = this.benchmarkGrid.GetVisualContainer();
            if(visualContainer is not null) {
                this.TryInvokeMember(visualContainer,"ScrollToRowIndex",rowIndex+headerOffset);
                this.TryInvokeMember(visualContainer,"ScrollToRowIndex",rowIndex+1);
                this.TryInvokeMember(visualContainer,"ScrollToRowIndex",rowIndex);
            }
        }
        catch(Exception ex) {
            StartupDiagnostics.Write("Failed to scroll benchmark grid to latest execution.",ex);
        }
    }

    private View CreateSourceTabContent(BenchmarkSourceTabViewState? state) {
        var grid = new Grid {
            RowDefinitions= {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star },
            },
            ColumnDefinitions= {
                new ColumnDefinition { Width = new GridLength(5,GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(5,GridUnitType.Star) },
            },
            RowSpacing=4,
            ColumnSpacing=6,
        };
        var summary = new Label {
            FontSize=12,
            FontAttributes=FontAttributes.Bold,
            TextColor=Color.FromArgb("#0F172A"),
            BackgroundColor=Color.FromArgb("#ECFDF5"),
            Padding=new Thickness(8,3),
            LineBreakMode=LineBreakMode.TailTruncation,
        };
        if(state is not null) {
            summary.SetBinding(Label.TextProperty,new Binding(nameof(BenchmarkSourceTabViewState.BenchmarkRatioSummaryText),source: state));
        }else{
            summary.SetBinding(Label.TextProperty,nameof(RunnerViewModel.BenchmarkRatioSummaryText));
        }
        grid.Add(summary,0,0);
        grid.SetColumnSpan(summary,2);

        var benchmarkGrid = this.CreateBenchmarkGrid(state);
        benchmarkGrid.VerticalOptions=LayoutOptions.Fill;
        grid.Add(benchmarkGrid,0,1);
        grid.Add(this.CreateBenchmarkChartPanel(state),1,1);
        return grid;
    }

    private View CreateSourceTabPlaceholder(SourceTabOption option) =>
        new Grid {
            AutomationId="Benchmark.SourceDisplay.Placeholder."+option.Definition.Tab,
            BackgroundColor=Colors.White,
            HorizontalOptions=LayoutOptions.Fill,
            VerticalOptions=LayoutOptions.Fill,
        };

    private View CreateBenchmarkTabContent() {
        var grid = new Grid {
            RowDefinitions= {
                new RowDefinition { Height = GridLength.Auto },
            },
        };
        var lowerTabView = new SfTabView {
            TabBarHeight=LowerTabBarHeight,
            TabBarBackground=Color.FromArgb("#E2E8F0"),
            IndicatorBackground=Colors.Transparent,
            HeaderItemSpacing=4,
            EnableSwiping=false,
            ContentTransitionDuration=0,
            BackgroundColor=Color.FromArgb("#F8FAFC"),
        };
        var lowerItems = new TabItemCollection();
        var lowerTabOptions = new[] {
            new TabHeaderOption("CPU"),
            new TabHeaderOption("ログ"),
            new TabHeaderOption("改善プロンプト"),
        };
        var lowerContents = new View[] { this.CreateCpuView(),this.CreateLogView(),this.CreateImprovementPromptView() };
        for(var index = 0; index<lowerTabOptions.Length; index++) {
            var capturedIndex = index;
            lowerItems.Add(new SfTabItem {
                Header=lowerTabOptions[index].HeaderText,
                HeaderContent=this.CreateTabHeader(lowerTabOptions[index],() => lowerTabView.SelectedIndex=capturedIndex),
                FontSize=12,
                Content=lowerContents[index],
            });
        }
        lowerTabOptions[0].SetSelected(true);
        lowerTabView.Items=lowerItems;
        void ApplyLowerTabHeight() {
            var height = LowerTabBarHeight+this.CalculateCpuTabContentHeight();
            if(Math.Abs(lowerTabView.HeightRequest-height)>0.1) {
                lowerTabView.HeightRequest=height;
            }
        }

        ApplyLowerTabHeight();
        lowerTabView.Loaded+=(_,_) => ApplyLowerTabHeight();
        lowerTabView.SizeChanged+=(_,_) => ApplyLowerTabHeight();
        lowerTabView.SelectedIndex=(int)(this.viewModel?.SelectedLowerTab??BenchmarkLowerTab.Cpu);
        lowerTabView.PropertyChanged+=(_,args) => {
            if(args.PropertyName!=nameof(SfTabView.SelectedIndex)) {
                return;
            }
            for(var i = 0; i<lowerTabOptions.Length; i++) {
                lowerTabOptions[i].SetSelected(i==lowerTabView.SelectedIndex);
            }
            if(this.viewModel is null) {
                return;
            }

            this.viewModel.SelectedLowerTab=lowerTabView.SelectedIndex switch {
                1 => BenchmarkLowerTab.Log,
                2 => BenchmarkLowerTab.ImprovementPrompt,
                _ => BenchmarkLowerTab.Cpu,
            };
        };
        var lowerTabFrame = new Border {
            Stroke=Color.FromArgb("#CBD5E1"),
            StrokeThickness=1,
            StrokeShape=new Microsoft.Maui.Controls.Shapes.RoundRectangle {
                CornerRadius=6,
            },
            BackgroundColor=Color.FromArgb("#F8FAFC"),
            Content=lowerTabView,
        };
        grid.Add(lowerTabFrame,0,0);

        if(this.viewModel is not null) {
            this.viewModel.PropertyChanged+=(_,args) => {
                if(args.PropertyName==nameof(RunnerViewModel.SelectedLowerTab)) {
                    StartupDiagnostics.BeginInvokeOnMainThread("lower tab sync",() => lowerTabView.SelectedIndex=(int)this.viewModel.SelectedLowerTab);
                }
            };
        }
        return grid;
    }

    private double CalculateBenchmarkGridContentHeight() {
        var rowCount = this.viewModel?.BenchmarkRows.Count??0;
        return (BenchmarkGridStackedHeaderRowCount*BenchmarkGridHeaderRowHeight)
            +BenchmarkGridHeaderRowHeight
            +(rowCount*BenchmarkGridRowHeight)
            +BenchmarkScrollBarSize
            +BenchmarkGridExtraHeight;
    }

    private double CalculateCpuTabContentHeight() {
        if(this.viewModel is null) {
            return 0;
        }

        var performanceHeight = this.CalculateCpuSectionListHeight(this.viewModel.PerformanceCpuSections);
        var efficiencyHeight = this.CalculateCpuSectionListHeight(this.viewModel.EfficiencyCpuSections);
        var unknownHeight = this.CalculateCpuSectionListHeight(this.viewModel.UnknownCpuSections);
        var topRowHeight = Math.Max(performanceHeight,efficiencyHeight);
        if(unknownHeight<=0) {
            return topRowHeight;
        }

        return topRowHeight<=0
            ? unknownHeight
            : topRowHeight+CpuContentRowSpacing+unknownHeight;
    }

    private double CalculateCpuSectionListHeight(IEnumerable<CpuTableSection> sections) {
        var heights = sections.Select(this.CalculateCpuSectionHeight).ToArray();
        if(heights.Length==0) {
            return 0;
        }

        return heights.Sum()+((heights.Length-1)*CpuListSpacing);
    }

    private double CalculateCpuSectionHeight(CpuTableSection section) =>
        CpuSectionHeaderHeight
        +CpuSectionSpacing
        +CpuSectionGridHeaderHeight
        +(section.Rows.Count*CpuSectionRowHeight)
        +CpuSectionGridExtraHeight;

    private View CreateBenchmarkChartPanel() =>
        this.CreateBenchmarkChartPanel(this.viewModel?.SelectedSourceTabViewState);

    private View CreateBenchmarkChartPanel(BenchmarkSourceTabViewState? state) {
        var header = new Grid {
            ColumnDefinitions= {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
            },
            Padding=new Thickness(8,4),
            BackgroundColor=Color.FromArgb("#F1F5F9"),
            ColumnSpacing=8,
        };
        header.Add(this.CreateMetricTabView(),0,0);

        var chartPanel = new Grid {
            RowDefinitions= {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star },
            },
            BackgroundColor=Colors.White,
            VerticalOptions=LayoutOptions.Fill,
        };
        chartPanel.Add(header,0,0);
        chartPanel.Add(this.CreateChartView(state),0,1);

        var tabs = new SfTabView {
            TabBarHeight=32,
            TabBarBackground=Color.FromArgb("#E2E8F0"),
            IndicatorBackground=Color.FromArgb("#0F766E"),
            HeaderItemSpacing=2,
            EnableSwiping=false,
            ContentTransitionDuration=0,
            BackgroundColor=Colors.White,
            VerticalOptions=LayoutOptions.Fill,
        };
        var chartTabOptions = new List<TabHeaderOption>();
        var chartTabItems = new TabItemCollection();

        void AddChartTab(string title,View content) {
            var option = new TabHeaderOption(title);
            chartTabOptions.Add(option);
            var index = chartTabOptions.Count-1;
            chartTabItems.Add(new SfTabItem {
                Header=title,
                HeaderContent=this.CreateTabHeader(option,() => tabs.SelectedIndex=index),
                FontSize=12,
                Content=content,
            });
        }

        AddChartTab("基準性能比",chartPanel);
        AddChartTab("履歴性能比",this.CreateHistoryRatioTabView());

        tabs.Items=chartTabItems;
        chartTabOptions[0].SetSelected(true);
        tabs.PropertyChanged+=(_,args) => {
            if(args.PropertyName!=nameof(SfTabView.SelectedIndex)) {
                return;
            }
            for(var i = 0; i<chartTabOptions.Count; i++) {
                chartTabOptions[i].SetSelected(i==tabs.SelectedIndex);
            }
            this.viewModel?.SetGeneratedCodeAuditTabVisible(false);
        };
        return tabs;
    }

    private View CreateHistoryRatioTabView() {
        var tabs = new SfTabView {
            TabBarHeight=28,
            TabBarBackground=Color.FromArgb("#F1F5F9"),
            IndicatorBackground=Color.FromArgb("#0F766E"),
            HeaderItemSpacing=2,
            EnableSwiping=false,
            ContentTransitionDuration=0,
            BackgroundColor=Colors.White,
            VerticalOptions=LayoutOptions.Fill,
        };
        var options = new[] { new TabHeaderOption("時間比"),new TabHeaderOption("メモリ比") };
        tabs.Items=new TabItemCollection {
            new SfTabItem {
                Header="時間比",
                HeaderContent=this.CreateTabHeader(options[0],() => tabs.SelectedIndex=0),
                FontSize=12,
                Content=this.CreateTrendChartTab(() => this.viewModel?.LoadHistoryTimeRatioChartData()??[],"RunId","時間比"),
            },
            new SfTabItem {
                Header="メモリ比",
                HeaderContent=this.CreateTabHeader(options[1],() => tabs.SelectedIndex=1),
                FontSize=12,
                Content=this.CreateTrendChartTab(() => this.viewModel?.LoadHistoryMemoryRatioChartData()??[],"RunId","メモリ比"),
            },
        };
        options[0].SetSelected(true);
        tabs.PropertyChanged+=(_,args) => {
            if(args.PropertyName!=nameof(SfTabView.SelectedIndex)) {
                return;
            }
            for(var i = 0; i<options.Length; i++) {
                options[i].SetSelected(i==tabs.SelectedIndex);
            }
        };
        return tabs;
    }

    private static View CreateAnalysisTabRefreshButton(Action reload) {
        var refresh = new Button {
            Text="更新",
            FontSize=12,
            HeightRequest=28,
            Padding=new Thickness(12,0),
            HorizontalOptions=LayoutOptions.Start,
            Margin=new Thickness(8,4),
        };
        refresh.Clicked+=(_,_) => reload();
        return refresh;
    }

    // 改善ランキング/時間比/割当比: 比率でランキングした横棒チャート。
    private View CreateRankedRatioChartTab(Func<IReadOnlyList<BenchmarkRankedRatioPoint>> load,string valueAxisTitle) {
        var chart = new SfCartesianChart {
            IsTransposed=true,
            TooltipBehavior=new ChartTooltipBehavior { FontSize=12,Duration=4 },
            VerticalOptions=LayoutOptions.Fill,
        };
        chart.XAxes.Add(new CategoryAxis { LabelPlacement=LabelPlacement.BetweenTicks });
        chart.YAxes.Add(new NumericalAxis { Title=new ChartAxisTitle { Text=valueAxisTitle } });
        var points = new ObservableCollection<BenchmarkRankedRatioPoint>();
        var series = new ColumnSeries {
            XBindingPath=nameof(BenchmarkRankedRatioPoint.Label),
            YBindingPath=nameof(BenchmarkRankedRatioPoint.Value),
            ItemsSource=points,
            Fill=new SolidColorBrush(Color.FromArgb("#0F766E")),
        };
        chart.Series.Add(series);
        void Reload() {
            points.Clear();
            foreach(var point in load()) {
                points.Add(point);
            }
        }
        var layout = new Grid {
            RowDefinitions= { new RowDefinition { Height = GridLength.Auto },new RowDefinition { Height = GridLength.Star } },
            BackgroundColor=Colors.White,
            VerticalOptions=LayoutOptions.Fill,
        };
        layout.Add(CreateAnalysisTabRefreshButton(Reload),0,0);
        layout.Add(chart,0,1);
        layout.Loaded+=(_,_) => Reload();
        return layout;
    }

    // カテゴリ: カテゴリ別の時間比/メモリ比を並べた棒チャート。
    private View CreateCategoryRatioChartTab() {
        var chart = new SfCartesianChart {
            IsTransposed=true,
            Legend=new ChartLegend(),
            TooltipBehavior=new ChartTooltipBehavior { FontSize=12,Duration=4 },
            VerticalOptions=LayoutOptions.Fill,
        };
        chart.XAxes.Add(new CategoryAxis { LabelPlacement=LabelPlacement.BetweenTicks });
        chart.YAxes.Add(new NumericalAxis { Title=new ChartAxisTitle { Text="比率" } });
        var points = new ObservableCollection<BenchmarkCategoryRatioPoint>();
        chart.Series.Add(new ColumnSeries {
            Label="時間比",
            XBindingPath=nameof(BenchmarkCategoryRatioPoint.Category),
            YBindingPath=nameof(BenchmarkCategoryRatioPoint.AvgMeanRatio),
            ItemsSource=points,
            Fill=new SolidColorBrush(Color.FromArgb("#2563EB")),
        });
        chart.Series.Add(new ColumnSeries {
            Label="メモリ比",
            XBindingPath=nameof(BenchmarkCategoryRatioPoint.Category),
            YBindingPath=nameof(BenchmarkCategoryRatioPoint.AvgAllocationRatio),
            ItemsSource=points,
            Fill=new SolidColorBrush(Color.FromArgb("#F97316")),
        });
        void Reload() {
            points.Clear();
            foreach(var point in this.viewModel?.LoadCategorySummaryChartData()??[]) {
                points.Add(point);
            }
        }
        var layout = new Grid {
            RowDefinitions= { new RowDefinition { Height = GridLength.Auto },new RowDefinition { Height = GridLength.Star } },
            BackgroundColor=Colors.White,
            VerticalOptions=LayoutOptions.Fill,
        };
        layout.Add(CreateAnalysisTabRefreshButton(Reload),0,0);
        layout.Add(chart,0,1);
        layout.Loaded+=(_,_) => Reload();
        return layout;
    }

    private static readonly string[] TrendSeriesPalette = ["#0F766E","#2563EB","#F97316","#DC2626","#7C3AED","#0891B2","#65A30D","#DB2777"];

    // 履歴/スケール: メソッドごとの複数系列トレンド折れ線チャート。
    private View CreateTrendChartTab(Func<IReadOnlyList<BenchmarkTrendSeries>> load,string xAxisTitle,string yAxisTitle) {
        var chart = new SfCartesianChart {
            TooltipBehavior=new ChartTooltipBehavior { FontSize=12,Duration=4 },
            VerticalOptions=LayoutOptions.Fill,
        };
        chart.XAxes.Add(new NumericalAxis { Title=new ChartAxisTitle { Text=xAxisTitle },Interval=1 });
        chart.YAxes.Add(new NumericalAxis { Title=new ChartAxisTitle { Text=yAxisTitle } });
        void Reload() {
            chart.Series.Clear();
            var seriesList = load();
            for(var index = 0; index<seriesList.Count; index++) {
                var series = seriesList[index];
                chart.Series.Add(new LineSeries {
                    Label=series.SeriesLabel,
                    XBindingPath=nameof(BenchmarkTrendPoint.X),
                    YBindingPath=nameof(BenchmarkTrendPoint.Y),
                    ItemsSource=series.Points,
                    Fill=new SolidColorBrush(Color.FromArgb(TrendSeriesPalette[index%TrendSeriesPalette.Length])),
                    StrokeWidth=2,
                });
            }
        }
        var layout = new Grid {
            RowDefinitions= { new RowDefinition { Height = GridLength.Auto },new RowDefinition { Height = GridLength.Star } },
            BackgroundColor=Colors.White,
            VerticalOptions=LayoutOptions.Fill,
        };
        layout.Add(CreateAnalysisTabRefreshButton(Reload),0,0);
        layout.Add(chart,0,1);
        layout.Loaded+=(_,_) => Reload();
        return layout;
    }

    // 失敗/欠測・fallback診断: 状態バッジの一覧。
    private View CreateBadgeChecklistTab(Func<IReadOnlyList<BenchmarkBadgeItem>> load) {
        var list = new VerticalStackLayout { Spacing=4,Padding=new Thickness(8) };
        void Reload() {
            list.Children.Clear();
            foreach(var item in load()) {
                list.Children.Add(CreateBadgeRow(item));
            }
        }
        var layout = new Grid {
            RowDefinitions= { new RowDefinition { Height = GridLength.Auto },new RowDefinition { Height = GridLength.Star } },
            BackgroundColor=Colors.White,
            VerticalOptions=LayoutOptions.Fill,
        };
        layout.Add(CreateAnalysisTabRefreshButton(Reload),0,0);
        layout.Add(new ScrollView { Content=list },0,1);
        layout.Loaded+=(_,_) => Reload();
        return layout;
    }

    private static View CreateBadgeRow(BenchmarkBadgeItem item) {
        var (background,foreground) = item.Severity switch {
            BenchmarkBadgeSeverity.Critical => (Color.FromArgb("#FEE2E2"),Color.FromArgb("#DC2626")),
            BenchmarkBadgeSeverity.Warning => (Color.FromArgb("#FFEDD5"),Color.FromArgb("#F97316")),
            BenchmarkBadgeSeverity.Ok => (Color.FromArgb("#ECFDF5"),Color.FromArgb("#0F766E")),
            _ => (Color.FromArgb("#F1F5F9"),Color.FromArgb("#475569")),
        };
        var dot = new BoxView {
            WidthRequest=10,
            HeightRequest=10,
            CornerRadius=5,
            Color=foreground,
            VerticalOptions=LayoutOptions.Center,
        };
        var title = new Label {
            Text=item.Title,
            FontSize=12,
            FontAttributes=FontAttributes.Bold,
            TextColor=Color.FromArgb("#0F172A"),
            LineBreakMode=LineBreakMode.TailTruncation,
        };
        var detail = new Label {
            Text=item.Detail,
            FontSize=11,
            TextColor=Color.FromArgb("#475569"),
            LineBreakMode=LineBreakMode.WordWrap,
        };
        var textStack = new VerticalStackLayout { Spacing=1,Children= { title,detail } };
        var row = new Grid {
            ColumnDefinitions= { new ColumnDefinition { Width = GridLength.Auto },new ColumnDefinition { Width = GridLength.Star } },
            ColumnSpacing=8,
            Padding=new Thickness(6,4),
        };
        row.Add(dot,0,0);
        row.Add(textStack,1,0);
        return new Border {
            Stroke=Color.FromArgb("#CBD5E1"),
            StrokeThickness=1,
            BackgroundColor=background,
            Content=row,
            Margin=new Thickness(0,0,0,2),
        };
    }

    // 計測品質: StdDev 比率をゲージ表示する行の一覧。
    private View CreateQualityGaugeTab() {
        var list = new VerticalStackLayout { Spacing=4,Padding=new Thickness(8) };
        void Reload() {
            list.Children.Clear();
            foreach(var point in this.viewModel?.LoadMeasurementQualityGauges()??[]) {
                list.Children.Add(CreateQualityGaugeRow(point));
            }
        }
        var layout = new Grid {
            RowDefinitions= { new RowDefinition { Height = GridLength.Auto },new RowDefinition { Height = GridLength.Star } },
            BackgroundColor=Colors.White,
            VerticalOptions=LayoutOptions.Fill,
        };
        layout.Add(CreateAnalysisTabRefreshButton(Reload),0,0);
        layout.Add(new ScrollView { Content=list },0,1);
        layout.Loaded+=(_,_) => Reload();
        return layout;
    }

    private static View CreateQualityGaugeRow(BenchmarkQualityGaugePoint point) {
        var color = point.StdDevRatio>0.1
            ? Color.FromArgb("#DC2626")
            : point.StdDevRatio>0.05 ? Color.FromArgb("#F97316") : Color.FromArgb("#0F766E");
        var gauge = new BenchmarkCircularProgressView {
            WidthRequest=24,
            HeightRequest=24,
            Progress=point.StdDevRatio,
            ProgressColor=color,
            VerticalOptions=LayoutOptions.Center,
        };
        var label = new Label {
            Text=point.Label+" ("+point.DetailText+")",
            FontSize=12,
            TextColor=Color.FromArgb("#0F172A"),
            LineBreakMode=LineBreakMode.TailTruncation,
            VerticalOptions=LayoutOptions.Center,
        };
        var ratioText = new Label {
            Text=point.StdDevRatio.ToString("P1",System.Globalization.CultureInfo.InvariantCulture),
            FontSize=11,
            TextColor=color,
            VerticalOptions=LayoutOptions.Center,
            HorizontalTextAlignment=TextAlignment.End,
        };
        var row = new Grid {
            ColumnDefinitions= {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto },
            },
            ColumnSpacing=8,
            Padding=new Thickness(6,4),
        };
        row.Add(gauge,0,0);
        row.Add(label,1,0);
        row.Add(ratioText,2,0);
        return new Border {
            Stroke=Color.FromArgb("#CBD5E1"),
            StrokeThickness=1,
            BackgroundColor=Colors.White,
            Content=row,
            Margin=new Thickness(0,0,0,2),
        };
    }

    private View CreateDiagnosticGrid(string itemsSourcePath) =>
        this.CreateDiagnosticGrid(null,itemsSourcePath);

    private View CreateDiagnosticGrid(IEnumerable<BenchmarkDiagnosticRow>? itemsSource,string itemsSourcePath) {
        var grid = new SfDataGrid {
            AutoGenerateColumnsMode=AutoGenerateColumnsMode.None,
            RowHeight=24,
            HeaderRowHeight=30,
            SortingMode=DataGridSortingMode.None,
            SelectionMode=DataGridSelectionMode.None,
            GridLinesVisibility=GridLinesVisibility.Both,
            HorizontalScrollBarVisibility=ScrollBarVisibility.Always,
            VerticalScrollBarVisibility=ScrollBarVisibility.Always,
            DefaultColumnWidth=92,
            BackgroundColor=Colors.White,
            VerticalOptions=LayoutOptions.Fill,
        };
        this.RegisterDataGridUpdateBatchTarget(grid);
        this.ConfigureDataGridLines(grid);
        this.ConfigureDataGridScrollBars(grid);
        grid.Columns.Add(this.CreateDiagnosticTextColumn("区分",nameof(BenchmarkDiagnosticRow.Category),70,TextAlignment.Start));
        grid.Columns.Add(this.CreateDiagnosticTextColumn("メソッド",nameof(BenchmarkDiagnosticRow.Method),160,TextAlignment.Start));
        grid.Columns.Add(this.CreateDiagnosticTextColumn("最悪N",nameof(BenchmarkDiagnosticRow.WorstN),70,TextAlignment.End));
        grid.Columns.Add(this.CreateDiagnosticTextColumn("比率/CPU",nameof(BenchmarkDiagnosticRow.RatioText),80,TextAlignment.End));
        grid.Columns.Add(this.CreateDiagnosticTextColumn("Left/時間",nameof(BenchmarkDiagnosticRow.LeftText),110,TextAlignment.End));
        grid.Columns.Add(this.CreateDiagnosticTextColumn("Right/メモリ",nameof(BenchmarkDiagnosticRow.RightText),110,TextAlignment.End));
        grid.Columns.Add(this.CreateDiagnosticTextColumn("根拠",nameof(BenchmarkDiagnosticRow.DetailText),280,TextAlignment.Start));
        if(itemsSource is not null) {
            grid.ItemsSource=itemsSource;
        }else{
            grid.SetBinding(SfDataGrid.ItemsSourceProperty,itemsSourcePath);
        }
        return grid;
    }

    private DataGridTextColumn CreateDiagnosticTextColumn(string header,string mappingName,double width,TextAlignment alignment) {
        return new DataGridTextColumn {
            HeaderText=header,
            MappingName=mappingName,
            Width=width,
            HeaderTextAlignment=TextAlignment.Center,
            CellTextAlignment=alignment,
            AllowSorting=false,
        };
    }

    private View CreateGeneratedCodeAuditInfographic() {
        var targetLabel = new Label { FontSize=12,FontAttributes=FontAttributes.Bold,TextColor=Color.FromArgb("#0F172A") };
        var pairLabel = new Label { FontSize=11,TextColor=Color.FromArgb("#475569") };
        var auditTargetLabel = new Label { FontSize=11,TextColor=Color.FromArgb("#475569") };
        var fileLabel = new Label { FontSize=11,TextColor=Color.FromArgb("#0F766E") };
        var flagsList = new VerticalStackLayout { Spacing=4,Padding=new Thickness(0,8) };
        var verdictLabel = new Label { FontSize=11,TextColor=Color.FromArgb("#334155"),LineBreakMode=LineBreakMode.WordWrap };

        void Apply() {
            var report = this.viewModel?.GeneratedCodeAuditReport;
            targetLabel.Text="対象メソッド: "+(report?.TargetMethod??"-");
            pairLabel.Text="比較ペア: "+(report?.ComparisonPair??"-");
            auditTargetLabel.Text="監査対象: "+(report?.AuditTarget??"-");
            fileLabel.Text=report?.GeneratedFileName is { } fileName ? "生成ファイル: "+fileName : string.Empty;
            fileLabel.IsVisible=report?.GeneratedFileName is not null;
            flagsList.Children.Clear();
            foreach(var flag in report?.Flags??[]) {
                flagsList.Children.Add(CreateBadgeRow(flag));
            }
            var hasVerdict=report?.Verdict is { Length: >0 } verdict&&verdict!="-";
            verdictLabel.Text=hasVerdict ? "判定: "+report!.Verdict : string.Empty;
            verdictLabel.IsVisible=hasVerdict;
        }

        var header = new VerticalStackLayout {
            Spacing=2,
            Padding=new Thickness(10,10,10,0),
            Children= { targetLabel,pairLabel,auditTargetLabel,fileLabel },
        };
        var content = new VerticalStackLayout {
            Children= { header,flagsList,verdictLabel },
        };
        if(this.viewModel is not null) {
            this.viewModel.PropertyChanged+=(_,args) => {
                if(args.PropertyName==nameof(RunnerViewModel.GeneratedCodeAuditReport)) {
                    StartupDiagnostics.BeginInvokeOnMainThread("chart element count label apply",Apply);
                }
            };
        }
        Apply();
        return new ScrollView {
            BackgroundColor=Colors.White,
            Content=content,
        };
    }

    private View CreateCollapsibleSection(
        string title,
        Func<bool> getExpanded,
        Action<bool> setExpanded,
        View content,
        View? trailingContent) {
        var icon = new Label {
            WidthRequest=18,
            FontSize=12,
            FontAttributes=FontAttributes.Bold,
            TextColor=Color.FromArgb("#0F172A"),
            HorizontalTextAlignment=TextAlignment.Center,
            VerticalTextAlignment=TextAlignment.Center,
        };
        var titleLabel = new Label {
            Text=title,
            FontSize=12,
            FontAttributes=FontAttributes.Bold,
            TextColor=Color.FromArgb("#0F172A"),
            VerticalTextAlignment=TextAlignment.Center,
            LineBreakMode=LineBreakMode.TailTruncation,
        };
        var toggleArea = new Grid {
            ColumnDefinitions= {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
            },
            ColumnSpacing=2,
            BackgroundColor=Colors.Transparent,
        };
        toggleArea.Add(icon,0,0);
        toggleArea.Add(titleLabel,1,0);
        var header = new Grid {
            ColumnDefinitions= {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto },
            },
            ColumnSpacing=8,
            Padding=new Thickness(6,3),
            BackgroundColor=Color.FromArgb("#F1F5F9"),
        };
        header.Add(toggleArea,0,0);
        if(trailingContent is not null) {
            header.Add(trailingContent,1,0);
        }

        var border = new Border {
            Stroke=Color.FromArgb("#CBD5E1"),
            StrokeThickness=1,
            BackgroundColor=Color.FromArgb("#F8FAFC"),
            Content=header,
        };
        var layout = new Grid {
            RowDefinitions= {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
            },
            RowSpacing=3,
            Children= {
                border,
                content,
            },
        };
        Grid.SetRow(content,1);

        void Apply() {
            var expanded = getExpanded();
            icon.Text=expanded ? "v" : ">";
            content.IsVisible=expanded;
        }

        var tap = new TapGestureRecognizer();
        tap.Tapped+=(_,_) => {
            setExpanded(!getExpanded());
            foreach(var refresh in this.collapsibleSectionRefreshers) {
                refresh();
            }
        };
        toggleArea.GestureRecognizers.Add(tap);
        this.collapsibleSectionRefreshers.Add(Apply);
        Apply();
        return layout;
    }

    private View CreateMetricTabView() {
        var tabView = new SfTabView {
            HeightRequest=28,
            TabBarHeight=28,
            TabBarBackground=Colors.Transparent,
            IndicatorBackground=Colors.Transparent,
            HeaderItemSpacing=2,
            EnableSwiping=false,
            ContentTransitionDuration=0,
            BackgroundColor=Colors.Transparent,
            WidthRequest=180,
            HorizontalOptions=LayoutOptions.Start,
        };
        var timeOption = new TabHeaderOption("時間比");
        var allocatedOption = new TabHeaderOption("メモリ比");
        var options = new[] { timeOption,allocatedOption };
        tabView.Items=new TabItemCollection {
            new SfTabItem { Header="時間比",HeaderContent=this.CreateTabHeader(timeOption,() => tabView.SelectedIndex=0),FontSize=12,Content=new Grid() },
            new SfTabItem { Header="メモリ比",HeaderContent=this.CreateTabHeader(allocatedOption,() => tabView.SelectedIndex=1),FontSize=12,Content=new Grid() },
        };
        tabView.SelectedIndex=this.viewModel?.IsAllocatedMetricSelected==true ? 1 : 0;
        options[tabView.SelectedIndex].SetSelected(true);

        var isApplyingMetricTabSelection = false;
        tabView.PropertyChanged+=(_,args) => {
            if(args.PropertyName!=nameof(SfTabView.SelectedIndex)||this.viewModel is null) {
                return;
            }
            for(var i = 0; i<options.Length; i++) {
                options[i].SetSelected(i==tabView.SelectedIndex);
            }
            isApplyingMetricTabSelection=true;
            try {
                if(tabView.SelectedIndex==1) {
                    this.viewModel.IsAllocatedMetricSelected=true;
                } else {
                    this.viewModel.IsTimeMetricSelected=true;
                }
            } finally {
                isApplyingMetricTabSelection=false;
            }
        };
        if(this.viewModel is not null) {
            this.viewModel.PropertyChanged+=(_,args) => {
                if(isApplyingMetricTabSelection||args.PropertyName!=nameof(RunnerViewModel.SelectedMetric)) {
                    return;
                }
                StartupDiagnostics.BeginInvokeOnMainThread("metric tab sync",() => tabView.SelectedIndex=this.viewModel.IsAllocatedMetricSelected ? 1 : 0);
            };
        }
        return tabView;
    }

    private View CreateBenchmarkGrid() =>
        this.CreateBenchmarkGrid(this.viewModel?.SelectedSourceTabViewState);

    private View CreateBenchmarkGrid(BenchmarkSourceTabViewState? state) {
        StartupDiagnostics.WriteSyncfusionAssemblies("CreateBenchmarkGrid before SfDataGrid");
        var grid = new SfDataGrid {
            AutoGenerateColumnsMode=AutoGenerateColumnsMode.None,
            RowHeight=BenchmarkGridRowHeight,
            HeaderRowHeight=BenchmarkGridHeaderRowHeight,
            AllowTriStateSorting=false,
            AllowResizingColumns=true,
            SortingMode=DataGridSortingMode.None,
            SelectionMode=DataGridSelectionMode.None,
            HorizontalScrollBarVisibility=ScrollBarVisibility.Always,
            VerticalScrollBarVisibility=ScrollBarVisibility.Always,
            GridLinesVisibility=GridLinesVisibility.Both,
            DefaultColumnWidth=92,
        };
        this.RegisterDataGridUpdateBatchTarget(grid);
        if(state is not null) {
            grid.AutomationId="Benchmark.Grid."+state.Definition.Tab;
            this.benchmarkGridsBySourceTab[state.Definition.Tab]=grid;
            if(this.viewModel?.SelectedSourceDefinition.Tab==state.Definition.Tab) {
                this.benchmarkGrid=grid;
            }
        }else{
            grid.AutomationId="Benchmark.Grid";
            this.benchmarkGrid=grid;
        }
        AutomationProperties.SetIsInAccessibleTree(grid,true);
        grid.QueryRowHeight+=(_,args) => {
            if(args.RowIndex<2) {
                args.Height=BenchmarkGridHeaderRowHeight;
            }
        };
        grid.CellDoubleTapped+=this.OnBenchmarkGridCellDoubleTapped;
        if(state is not null) {
            grid.ItemsSource=state.Rows;
        }else{
            grid.SetBinding(SfDataGrid.ItemsSourceProperty,nameof(RunnerViewModel.BenchmarkRows));
        }
        this.ConfigureDataGridLines(grid);
        this.ConfigureDataGridScrollBars(grid);

        var sourceTab = state?.Definition??this.viewModel?.SelectedSourceDefinition??BenchmarkSourceTabs.Default;
        this.RebuildBenchmarkGridColumns(grid,sourceTab);
        return grid;
    }

    private void SelectBenchmarkGridForActiveSource() {
        if(this.viewModel is null) {
            return;
        }

        if(this.benchmarkGridsBySourceTab.TryGetValue(this.viewModel.SelectedSourceDefinition.Tab,out var grid)) {
            this.benchmarkGrid=grid;
        }
    }

    private void RebuildBenchmarkGridColumns(SfDataGrid grid,BenchmarkSourceTabDefinition sourceTab) {
        grid.Columns.Clear();
        grid.StackedHeaderRows.Clear();
        var projectHeaderRow = new DataGridStackedHeaderRow();
        grid.StackedHeaderRows.Add(projectHeaderRow);

        grid.Columns.Add(this.CreateCheckColumn(sourceTab));
        grid.Columns.Add(this.CreateTextColumn("型",nameof(BenchmarkTableRow.BenchmarkTypeText),BenchmarkGridSortColumn.BenchmarkType,BenchmarkTypeColumnWidth,TextAlignment.End));
        grid.Columns.Add(this.CreateTextColumn("ベンチマーク",nameof(BenchmarkTableRow.BenchmarkMethodText),BenchmarkGridSortColumn.BenchmarkMethod,BenchmarkNameColumnWidth,TextAlignment.End));
        var cpuMapping = nameof(BenchmarkTableRow.LogicalProcessorText);
        grid.Columns.Add(this.CreateCpuColumn(cpuMapping));
        var argumentMappings = this.AddArgumentColumns(grid,sourceTab);
        this.AddBenchmarkBaseStackedHeaders(projectHeaderRow,cpuMapping,argumentMappings);
        var leftMappings = this.AddProjectColumns(grid,"Left",nameof(BenchmarkTableRow.LeftCell),BenchmarkGridSortColumn.LeftTime,BenchmarkGridSortColumn.LeftAllocated);
        var timeRatioMapping = nameof(BenchmarkTableRow.PairRatioTimeText);
        var allocatedRatioMapping = nameof(BenchmarkTableRow.PairRatioAllocatedText);
        grid.Columns.Add(this.CreateTextColumn("時間比",timeRatioMapping,BenchmarkGridSortColumn.PairRatioTime,BenchmarkPairStopNColumnWidth,TextAlignment.End,Color.FromArgb("#F8FAFC")));
        grid.Columns.Add(this.CreateTextColumn("メモリ比",allocatedRatioMapping,BenchmarkGridSortColumn.PairRatioAllocated,BenchmarkPairStopNColumnWidth,TextAlignment.End,Color.FromArgb("#F8FAFC")));
        var rightMappings = this.AddProjectColumns(grid,"Right",nameof(BenchmarkTableRow.RightCell),BenchmarkGridSortColumn.RightTime,BenchmarkGridSortColumn.RightAllocated);
        this.AddBenchmarkProjectSelectionGroup(projectHeaderRow,this.GetBenchmarkProjectSelectionGroup("Left","Left"),leftMappings,sourceTab);
        this.AddBenchmarkRatioHeader(projectHeaderRow,timeRatioMapping,"時間比");
        this.AddBenchmarkRatioHeader(projectHeaderRow,allocatedRatioMapping,"メモリ比");
        this.AddBenchmarkProjectSelectionGroup(projectHeaderRow,this.GetBenchmarkProjectSelectionGroup("Right","Right"),rightMappings,sourceTab);
    }

    private void OnBenchmarkGridCellDoubleTapped(object? sender,DataGridCellDoubleTappedEventArgs args) {
        if(sender is not SfDataGrid grid||args.Column is null||args.RowData is not null) {
            return;
        }

        var calculatedWidth = this.CalculateBenchmarkColumnAutoWidth(args.Column)+8;
        var minimumWidth = args.Column.MinimumWidth;
        var maximumWidth = args.Column.MaximumWidth;
        var width = Math.Max(calculatedWidth,double.IsNaN(minimumWidth) ? 0 : minimumWidth);
        if(!double.IsNaN(maximumWidth)&&maximumWidth>0) {
            width=Math.Min(width,maximumWidth);
        }

        args.Column.Width=width;
    }

    private double CalculateBenchmarkColumnAutoWidth(DataGridColumn column) {
        var headerWidth = MeasureBenchmarkColumnText(column.HeaderText);
        var rows = this.viewModel?.BenchmarkRows?? [];
        var contentWidth = rows
            .Select(row => MeasureBenchmarkColumnText(GetBenchmarkColumnText(row,column.MappingName)))
            .DefaultIfEmpty(0)
            .Max();
        return Math.Clamp(Math.Max(headerWidth,contentWidth)+14,48,900);
    }

    private static double MeasureBenchmarkColumnText(string? text) {
        if(string.IsNullOrWhiteSpace(text)) {
            return 0;
        }

        var label = new Label {
            Text=text,
            FontSize=10,
            LineBreakMode=LineBreakMode.NoWrap,
            Padding=new Thickness(2,0),
        };
        return label.Measure(double.PositiveInfinity,BenchmarkGridRowHeight).Width;
    }

    private static string GetBenchmarkColumnText(BenchmarkTableRow row,string mappingName) =>
        mappingName switch {
            nameof(BenchmarkTableRow.BenchmarkName) => row.BenchmarkName,
            nameof(BenchmarkTableRow.BenchmarkTypeText) => row.BenchmarkTypeText,
            nameof(BenchmarkTableRow.BenchmarkMethodText) => row.BenchmarkMethodText,
            nameof(BenchmarkTableRow.CountArgument) => row.CountArgument,
            nameof(BenchmarkTableRow.IndexArgument) => row.IndexArgument,
            nameof(BenchmarkTableRow.PageArgument) => row.PageArgument,
            nameof(BenchmarkTableRow.SegmentArgument) => row.SegmentArgument,
            nameof(BenchmarkTableRow.MemoryArgument) => row.MemoryArgument,
            nameof(BenchmarkTableRow.FractionArgument) => row.FractionArgument,
            nameof(BenchmarkTableRow.InitialArgument) => row.InitialArgument,
            nameof(BenchmarkTableRow.DataArgument) => row.DataArgument,
            nameof(BenchmarkTableRow.StopNText) or "StopN" => row.StopNText,
            nameof(BenchmarkTableRow.PairRatioTimeText) => row.PairRatioTimeText,
            nameof(BenchmarkTableRow.PairRatioAllocatedText) => row.PairRatioAllocatedText,
            nameof(BenchmarkTableRow.LeftTimeText) or "LeftCellTime" => row.LeftTimeText,
            nameof(BenchmarkTableRow.LeftAllocatedText) or "LeftCellAllocated" => row.LeftAllocatedText,
            nameof(BenchmarkTableRow.RightTimeText) or "RightCellTime" => row.RightTimeText,
            nameof(BenchmarkTableRow.RightAllocatedText) or "RightCellAllocated" => row.RightAllocatedText,
            "LeftCellCheck" => row.LeftCell.ExecutionText,
            "RightCellCheck" => row.RightCell.ExecutionText,
            _ => string.Empty,
        };

    private DataGridTemplateColumn CreateCheckColumn(BenchmarkSourceTabDefinition sourceTab) {
        return new DataGridTemplateColumn {
            MappingName=nameof(BenchmarkTableRow.IsSelectionChecked),
            HeaderText="",
            Width=BenchmarkSelectionColumnWidth,
            AllowSorting=false,
            HeaderTemplate=this.CreateAllBenchmarkSelectionColumnHeaderTemplate(sourceTab),
            CellTemplate=new DataTemplate(() => {
                var layout = new Grid {
                    ColumnDefinitions= {
                        new ColumnDefinition { Width = new GridLength(BenchmarkAggregateCheckBoxSize+4) },
                        new ColumnDefinition { Width = GridLength.Star },
                    },
                    WidthRequest=BenchmarkSelectionColumnWidth,
                    HorizontalOptions=LayoutOptions.Fill,
                    VerticalOptions=LayoutOptions.Fill,
                };
                var check = this.CreateSfCheckBox(BenchmarkAggregateCheckBoxSize,BenchmarkAggregateCheckBoxSize,true);
                check.InputTransparent=true;
                check.SetBinding(SfCheckBox.IsCheckedProperty,new Binding(nameof(BenchmarkTableRow.IsSelectionChecked),BindingMode.OneWay));
                var checkHost = new Grid {
                    HorizontalOptions=LayoutOptions.Fill,
                    VerticalOptions=LayoutOptions.Fill,
                    AutomationId="Benchmark.RowCheckHost",
                };
                checkHost.Add(check,0,0);
                var checkTap = new TapGestureRecognizer();
                checkTap.Tapped+=(_,_) => {
                    if(layout.BindingContext is BenchmarkTableRow row) {
                        this.viewModel?.ToggleBenchmarkRow(row);
                    }
                };
                checkHost.GestureRecognizers.Add(checkTap);
                var selectionHost = new Grid {
                    BackgroundColor=Colors.Transparent,
                    HorizontalOptions=LayoutOptions.Fill,
                    VerticalOptions=LayoutOptions.Fill,
                };
                var selectionTap = new TapGestureRecognizer();
                selectionTap.Tapped+=(_,_) => {
                    if(layout.BindingContext is BenchmarkTableRow row) {
                        this.viewModel?.SelectBenchmarkRow(row,CreateBenchmarkSelectionGesture());
                    }
                };
                selectionHost.GestureRecognizers.Add(selectionTap);
                this.AddBenchmarkRowUiSelectionFill(layout);
                layout.Add(checkHost,0,0);
                layout.Add(selectionHost,1,0);
                return layout;
            }),
        };
    }

    private void AddBenchmarkBaseStackedHeaders(
        DataGridStackedHeaderRow projectHeaderRow,
        string cpuMapping,
        IReadOnlyList<string> argumentMappings) {
        var baseMappings = new List<string> {
            nameof(BenchmarkTableRow.IsSelectionChecked),
            nameof(BenchmarkTableRow.BenchmarkTypeText),
            nameof(BenchmarkTableRow.BenchmarkMethodText),
            cpuMapping,
        };
        baseMappings.AddRange(argumentMappings);
        projectHeaderRow.Columns.Add(new DataGridStackedColumn {
            MappingName="BenchmarkBaseHeaderSpacer",
            ColumnMappingNames=string.Join(",",baseMappings),
            Template=this.CreateBenchmarkNeutralGroupHeaderTemplate("項目"),
        });
    }

    private IReadOnlyList<string> AddArgumentColumns(SfDataGrid grid,BenchmarkSourceTabDefinition sourceTab) {
        var mappings = new List<string>();
        foreach(var argumentColumn in BenchmarkArgumentColumns.Get(sourceTab)){
            var column = GetArgumentColumnUi(argumentColumn);
            grid.Columns.Add(this.CreateTextColumn(column.Header,column.Property,column.SortColumn,column.Width,TextAlignment.End));
            mappings.Add(column.Property);
        }
        return mappings;
    }

    private static BenchmarkArgumentColumnUi GetArgumentColumnUi(ScalableBenchmarkArgumentColumn column) =>
        column switch{
            ScalableBenchmarkArgumentColumn.Count => new("Count",nameof(BenchmarkTableRow.CountArgument),BenchmarkGridSortColumn.ArgumentCount,BenchmarkCountArgumentColumnWidth),
            ScalableBenchmarkArgumentColumn.Index => new("Index",nameof(BenchmarkTableRow.IndexArgument),BenchmarkGridSortColumn.ArgumentIndex,BenchmarkArgumentColumnWidth),
            ScalableBenchmarkArgumentColumn.Page => new("Page",nameof(BenchmarkTableRow.PageArgument),BenchmarkGridSortColumn.ArgumentPage,BenchmarkCompactArgumentColumnWidth),
            ScalableBenchmarkArgumentColumn.Segment => new("Segment",nameof(BenchmarkTableRow.SegmentArgument),BenchmarkGridSortColumn.ArgumentSegment,BenchmarkArgumentColumnWidth),
            ScalableBenchmarkArgumentColumn.Memory => new("Memory",nameof(BenchmarkTableRow.MemoryArgument),BenchmarkGridSortColumn.ArgumentMemory,BenchmarkMemoryArgumentColumnWidth),
            ScalableBenchmarkArgumentColumn.Fraction => new("Fraction",nameof(BenchmarkTableRow.FractionArgument),BenchmarkGridSortColumn.ArgumentFraction,BenchmarkCompactArgumentColumnWidth),
            ScalableBenchmarkArgumentColumn.Initial => new("Initial",nameof(BenchmarkTableRow.InitialArgument),BenchmarkGridSortColumn.ArgumentInitial,BenchmarkCompactArgumentColumnWidth),
            ScalableBenchmarkArgumentColumn.Data => new("Data",nameof(BenchmarkTableRow.DataArgument),BenchmarkGridSortColumn.ArgumentData,BenchmarkCompactArgumentColumnWidth),
            _ => throw new ArgumentOutOfRangeException(nameof(column)),
        };

    private readonly record struct BenchmarkArgumentColumnUi(
        string Header,
        string Property,
        BenchmarkGridSortColumn SortColumn,
        double Width);

    private IReadOnlyList<string> AddProjectColumns(
        SfDataGrid grid,
        string header,
        string cellPath,
        BenchmarkGridSortColumn timeSortColumn,
        BenchmarkGridSortColumn allocatedSortColumn) {
        var checkMapping = cellPath+"Check";
        var timeMapping = cellPath+"Time";
        var allocatedMapping = cellPath+"Allocated";
        grid.Columns.Add(this.CreateProjectCheckColumn(checkMapping,cellPath));
        grid.Columns.Add(this.CreateProjectTextColumn("時間",timeMapping,cellPath,cellPath+"."+nameof(BenchmarkProjectCell.TimeText),timeSortColumn,BenchmarkProjectMetricColumnWidth,TextAlignment.End));
        grid.Columns.Add(this.CreateProjectTextColumn("メモリ",allocatedMapping,cellPath,cellPath+"."+nameof(BenchmarkProjectCell.AllocatedText),allocatedSortColumn,BenchmarkProjectMetricColumnWidth,TextAlignment.End));
        return [checkMapping,timeMapping,allocatedMapping];
    }

    private string GetProjectFromCellPath(string cellPath) {
        // "Left"/"Right" トークンは VM 側で行が属する SourceTab の定義から実プロジェクト名へ解決される
        return cellPath switch {
            nameof(BenchmarkTableRow.LeftCell) => "Left",
            nameof(BenchmarkTableRow.RightCell) => "Right",
            _ => cellPath,
        };
    }

    private static BenchmarkCellSelectionGesture CreateBenchmarkSelectionGesture() {
        var isShiftPressed = false;
        var isControlPressed = false;
#if WINDOWS
        isShiftPressed=(GetKeyState(0x10)&0x8000)!=0;
        isControlPressed=(GetKeyState(0x11)&0x8000)!=0;
#endif
        return isShiftPressed
            ? isControlPressed
                ? BenchmarkCellSelectionGesture.ExtendToggle
                : BenchmarkCellSelectionGesture.Extend
            : isControlPressed
                ? BenchmarkCellSelectionGesture.Toggle
                : BenchmarkCellSelectionGesture.Replace;
    }

    private DataGridTemplateColumn CreateCpuColumn(string mappingName) {
        return new DataGridTemplateColumn {
            MappingName=mappingName,
            HeaderText="CPU",
            Width=BenchmarkCpuColumnWidth,
            AllowSorting=false,
            HeaderTemplate=this.CreateHeaderTemplate("CPU",BenchmarkGridSortColumn.LogicalCpu),
            CellTemplate=new DataTemplate(() => {
                var grid = new Grid {
                    ColumnDefinitions= {
                        new ColumnDefinition { Width = GridLength.Auto },
                        new ColumnDefinition { Width = GridLength.Star },
                    },
                    ColumnSpacing=2,
                    Padding=new Thickness(2,0),
                    BackgroundColor=Colors.Transparent,
                    HorizontalOptions=LayoutOptions.Fill,
                    VerticalOptions=LayoutOptions.Fill,
                };
                this.AddBenchmarkRowUiSelectionFill(grid);
                var badge = this.CreateCoreBadge();
                badge.SetBinding(IsVisibleProperty,nameof(BenchmarkTableRow.HasCpuDisplay));
                badge.SetBinding(BackgroundColorProperty,nameof(BenchmarkTableRow.BadgeBackgroundColor));
                if(badge.Content is Image badgeImage) {
                    badgeImage.SetBinding(
                        Image.SourceProperty,
                        new Binding(nameof(BenchmarkTableRow.CoreKindImageSource),converter: new ImageFileSourceConverter()));
                }
                var number = new Label {
                    FontSize=10,
                    TextColor=Color.FromArgb("#0F172A"),
                    FontAttributes=FontAttributes.Bold,
                    InputTransparent=true,
                    LineBreakMode=LineBreakMode.TailTruncation,
                    HorizontalTextAlignment=TextAlignment.Start,
                    VerticalTextAlignment=TextAlignment.Center,
                };
                number.SetBinding(IsVisibleProperty,nameof(BenchmarkTableRow.HasCpuDisplay));
                number.SetBinding(Label.TextProperty,nameof(BenchmarkTableRow.LogicalProcessorText));
                grid.SetBinding(ToolTipProperties.TextProperty,nameof(BenchmarkTableRow.CpuTooltipText));
                var tap = new TapGestureRecognizer();
                tap.Tapped+=(_,_) => {
                    if(grid.BindingContext is BenchmarkTableRow row) {
                        this.viewModel?.SelectBenchmarkRow(row,CreateBenchmarkSelectionGesture());
                    }
                };
                grid.GestureRecognizers.Add(tap);
                grid.Add(badge,0,0);
                grid.Add(number,1,0);
                return grid;
            }),
        };
    }

    private void AddBenchmarkProjectSelectionGroup(DataGridStackedHeaderRow headerRow,BenchmarkColumnSelectionGroup group,IReadOnlyList<string> childMappings,BenchmarkSourceTabDefinition sourceTab) {
        headerRow.Columns.Add(new DataGridStackedColumn {
            MappingName="BenchmarkProjectGroup"+group.Id,
            ColumnMappingNames=string.Join(",",childMappings),
            Template=this.CreateBenchmarkColumnGroupHeaderTemplate(group,sourceTab),
        });
    }

    private void AddBenchmarkRatioHeader(DataGridStackedHeaderRow headerRow,string mappingName,string text) {
        headerRow.Columns.Add(new DataGridStackedColumn {
            MappingName="BenchmarkRatio"+mappingName,
            ColumnMappingNames=mappingName,
            Template=this.CreateBenchmarkNeutralGroupHeaderTemplate(text),
        });
    }

    private BenchmarkColumnSelectionGroup GetBenchmarkProjectSelectionGroup(string id,string headerText) {
        return this.viewModel?.BenchmarkProjectSelectionGroups.FirstOrDefault(group => group.Id==id)
            ??new BenchmarkColumnSelectionGroup(id,headerText,[]);
    }

    private DataTemplate CreateAllBenchmarkSelectionColumnHeaderTemplate(BenchmarkSourceTabDefinition sourceTab) {
        return new DataTemplate(() => {
            var layout = new Grid {
                ColumnDefinitions= {
                    new ColumnDefinition { Width = new GridLength(BenchmarkAggregateCheckBoxSize+4) },
                    new ColumnDefinition { Width = GridLength.Star },
                },
                WidthRequest=BenchmarkSelectionColumnWidth,
                BackgroundColor=Color.FromArgb("#F1F5F9"),
                HorizontalOptions=LayoutOptions.Fill,
                VerticalOptions=LayoutOptions.Fill,
            };
            var check = this.CreateBenchmarkAggregateCheckBox();
            check.AutomationId="Benchmark.AllSelectionCheck";
            if(this.viewModel is not null) {
                this.BindAggregateCheckBox(check,this.viewModel,nameof(RunnerViewModel.IsAllBenchmarkSelectionChecked),() => this.viewModel.IsAllBenchmarkSelectionChecked);
            }
            var checkHost = new Grid {
                BackgroundColor=Colors.Transparent,
                HorizontalOptions=LayoutOptions.Fill,
                VerticalOptions=LayoutOptions.Fill,
                AutomationId="Benchmark.AllSelectionCheckHost",
            };
            checkHost.Add(check,0,0);
            var checkTap = new TapGestureRecognizer();
            checkTap.Tapped+=(_,_) => {
                this.viewModel?.ToggleAllBenchmarkSelection(sourceTab);
            };
            checkHost.GestureRecognizers.Add(checkTap);
            var selectionHost = new Grid {
                BackgroundColor=Colors.Transparent,
                HorizontalOptions=LayoutOptions.Fill,
                VerticalOptions=LayoutOptions.Fill,
            };
            var selectionTap = new TapGestureRecognizer();
            selectionTap.Tapped+=(_,_) => this.viewModel?.SelectAllBenchmarkUiCells(sourceTab);
            selectionHost.GestureRecognizers.Add(selectionTap);
            layout.Add(checkHost,0,0);
            layout.Add(selectionHost,1,0);
            return layout;
        });
    }

    private DataTemplate CreateBenchmarkColumnGroupHeaderTemplate(BenchmarkColumnSelectionGroup group,BenchmarkSourceTabDefinition sourceTab) {
        return new DataTemplate(() => {
            var body = this.CreateBenchmarkHeaderBody(nameof(BenchmarkColumnSelectionGroup.HeaderText),Color.FromArgb("#EAF3FF"),Color.FromArgb("#2563EB"));
            body.BindingContext=group;
            var bodyTap = new TapGestureRecognizer();
            bodyTap.Tapped+=(_,_) => this.viewModel?.SelectBenchmarkColumnGroup(group,CreateBenchmarkSelectionGesture(),sourceTab);
            body.GestureRecognizers.Add(bodyTap);
            return body;
        });
    }

    private DataTemplate CreateBenchmarkNeutralGroupHeaderTemplate(string text) {
        return new DataTemplate(() => this.CreateBenchmarkHeaderBody(text,Color.FromArgb("#F8FAFC"),Color.FromArgb("#94A3B8")));
    }

    private Grid CreateBenchmarkHeaderCheckLayout(string text) {
        var layout = new Grid {
            ColumnDefinitions= {
                new ColumnDefinition { Width = new GridLength(BenchmarkHeaderCheckSlotWidth) },
                new ColumnDefinition { Width = GridLength.Star },
            },
            ColumnSpacing=2,
            Padding=new Thickness(2,0),
            BackgroundColor=Color.FromArgb("#F1F5F9"),
            HorizontalOptions=LayoutOptions.Fill,
            VerticalOptions=LayoutOptions.Fill,
        };
        return layout;
    }

    private Grid CreateBenchmarkHeaderCheckHost(SfCheckBox check) {
        var host = new Grid {
            BackgroundColor=Colors.Transparent,
            HorizontalOptions=LayoutOptions.Fill,
            VerticalOptions=LayoutOptions.Fill,
        };
        host.Add(check,0,0);
        return host;
    }

    private Grid CreateBenchmarkHeaderBody(string text) =>
        this.CreateBenchmarkHeaderBody(text,Color.FromArgb("#F1F5F9"),Color.FromArgb("#CBD5E1"));

    private Grid CreateBenchmarkHeaderBody(string text,Color backgroundColor,Color borderColor) {
        var body = new Grid {
            BackgroundColor=backgroundColor,
            HorizontalOptions=LayoutOptions.Fill,
            VerticalOptions=LayoutOptions.Fill,
        };
        body.Add(new BoxView {
            WidthRequest=2,
            BackgroundColor=borderColor,
            HorizontalOptions=LayoutOptions.Start,
            VerticalOptions=LayoutOptions.Fill,
            InputTransparent=true,
        },0,0);
        body.Add(new BoxView {
            WidthRequest=2,
            BackgroundColor=borderColor,
            HorizontalOptions=LayoutOptions.End,
            VerticalOptions=LayoutOptions.Fill,
            InputTransparent=true,
        },0,0);
        if(!string.IsNullOrWhiteSpace(text)) {
            var label = new Label {
                FontSize=12,
                FontAttributes=FontAttributes.Bold,
                TextColor=Color.FromArgb("#0F172A"),
                InputTransparent=true,
                HorizontalOptions=LayoutOptions.Fill,
                HorizontalTextAlignment=TextAlignment.Center,
                VerticalTextAlignment=TextAlignment.Center,
                LineBreakMode=LineBreakMode.TailTruncation,
                Margin=new Thickness(4,0),
            };
            if(text==nameof(BenchmarkColumnSelectionGroup.HeaderText)) {
                label.SetBinding(Label.TextProperty,nameof(BenchmarkColumnSelectionGroup.HeaderText));
            }
            else {
                label.Text=text;
            }
            body.Add(label,0,0);
        }
        return body;
    }

    private SfCheckBox CreateBenchmarkAggregateCheckBox() {
        var check = this.CreateSfCheckBox(BenchmarkAggregateCheckBoxSize,BenchmarkAggregateCheckBoxSize,true);
        check.InputTransparent=true;
        return check;
    }

    private void BindAggregateCheckBox(SfCheckBox check,INotifyPropertyChanged source,string propertyName,Func<bool?> getValue) {
        check.IsChecked=getValue();
        check.SetBinding(SfCheckBox.IsCheckedProperty,new Binding(propertyName,BindingMode.OneWay,source: source));
        PropertyChangedEventHandler handler = (_,args) => {
            if(args.PropertyName==propertyName) {
                StartupDiagnostics.BeginInvokeOnMainThread("benchmark column header selection apply",() => {
                    check.IsChecked=getValue();
                });
            }
        };
        source.PropertyChanged+=handler;
        check.Unloaded+=(_,_) => source.PropertyChanged-=handler;
    }

    private DataGridTemplateColumn CreateProjectCheckColumn(string mappingName,string cellPath) {
        return new DataGridTemplateColumn {
            MappingName=mappingName,
            HeaderText="日時",
            Width=BenchmarkProjectStatusColumnWidth,
            AllowSorting=false,
            HeaderTemplate=new DataTemplate(() => {
                var header = new Grid {
                    ColumnDefinitions= {
                        new ColumnDefinition { Width = new GridLength(18) },
                        new ColumnDefinition { Width = GridLength.Star },
                    },
                    ColumnSpacing=2,
                    Padding=new Thickness(1,0),
                    BackgroundColor=Color.FromArgb("#F1F5F9"),
                };
                var measuredAt = new Label {
                    Text="日時",
                    FontSize=10,
                    FontAttributes=FontAttributes.Bold,
                    TextColor=Color.FromArgb("#0F172A"),
                    HorizontalOptions=LayoutOptions.Fill,
                    HorizontalTextAlignment=TextAlignment.Start,
                    VerticalTextAlignment=TextAlignment.Center,
                    LineBreakMode=LineBreakMode.TailTruncation,
                    Padding=new Thickness(2,0),
                };
                header.Add(measuredAt,1,0);
                return header;
            }),
            CellTemplate=new DataTemplate(() => {
                var layout = new Grid {
                    RowDefinitions= {
                        new RowDefinition { Height = new GridLength(2) },
                        new RowDefinition { Height = GridLength.Star },
                        new RowDefinition { Height = new GridLength(2) },
                    },
                    ColumnDefinitions= {
                        new ColumnDefinition { Width = new GridLength(2) },
                        new ColumnDefinition { Width = GridLength.Star },
                        new ColumnDefinition { Width = new GridLength(2) },
                    },
                    BackgroundColor=Colors.Transparent,
                    VerticalOptions=LayoutOptions.Fill,
                    HorizontalOptions=LayoutOptions.Fill,
                };
                var content = new Grid {
                    ColumnDefinitions= {
                        new ColumnDefinition { Width = new GridLength(18) },
                        new ColumnDefinition { Width = GridLength.Star },
                    },
                    ColumnSpacing=2,
                    Padding=new Thickness(1,0),
                    VerticalOptions=LayoutOptions.Center,
                };
                this.AddBenchmarkRowUiSelectionFill(layout);
                this.AddBenchmarkProjectUiSelectionFill(layout,this.GetBenchmarkProjectSelectionSide(cellPath));
                var spinner = new ActivityIndicator {
                    WidthRequest=14,
                    HeightRequest=14,
                    Color=Color.FromArgb("#2563EB"),
                    VerticalOptions=LayoutOptions.Center,
                    HorizontalOptions=LayoutOptions.Center,
                };
                spinner.SetBinding(ActivityIndicator.IsRunningProperty,new Binding(cellPath+"."+nameof(BenchmarkProjectCell.IsRunningVisual)));
                spinner.SetBinding(IsVisibleProperty,new Binding(cellPath+"."+nameof(BenchmarkProjectCell.IsRunningVisual)));
                var statusIcon = new Image {
                    WidthRequest=14,
                    HeightRequest=14,
                    InputTransparent=true,
                    HorizontalOptions=LayoutOptions.Center,
                    VerticalOptions=LayoutOptions.Center,
                    Aspect=Aspect.AspectFit,
                };
                statusIcon.SetBinding(
                    Image.SourceProperty,
                    new Binding(cellPath+"."+nameof(BenchmarkProjectCell.StatusImageSource),converter: new ImageFileSourceConverter()));
                statusIcon.SetBinding(IsVisibleProperty,cellPath+"."+nameof(BenchmarkProjectCell.IsStatusIconVisible));
                statusIcon.SetBinding(ToolTipProperties.TextProperty,cellPath+"."+nameof(BenchmarkProjectCell.ExecutionTooltipText));
                var detail = new Label {
                    FontSize=BenchmarkGridDetailFontSize,
                    TextColor=Color.FromArgb("#334155"),
                    InputTransparent=true,
                    LineBreakMode=LineBreakMode.TailTruncation,
                    VerticalTextAlignment=TextAlignment.Center,
                };
                detail.SetBinding(Label.TextProperty,cellPath+"."+nameof(BenchmarkProjectCell.ExecutionText));
                detail.SetBinding(Label.TextColorProperty,cellPath+"."+nameof(BenchmarkProjectCell.ExecutionTextColor));
                detail.SetBinding(Label.FontAttributesProperty,cellPath+"."+nameof(BenchmarkProjectCell.ExecutionFontAttributes));
                var failureDetail = new Label {
                    FontSize=BenchmarkGridDetailFontSize,
                    TextColor=Color.FromArgb("#DC2626"),
                    InputTransparent=true,
                    LineBreakMode=LineBreakMode.TailTruncation,
                    VerticalTextAlignment=TextAlignment.Center,
                };
                failureDetail.SetBinding(Label.TextProperty,cellPath+"."+nameof(BenchmarkProjectCell.FailureExecutionText));
                failureDetail.SetBinding(Label.TextColorProperty,cellPath+"."+nameof(BenchmarkProjectCell.FailureExecutionTextColor));
                var detailHost = new Grid {
                    ColumnDefinitions= {
                        new ColumnDefinition { Width = GridLength.Auto },
                        new ColumnDefinition { Width = GridLength.Auto },
                    },
                    BackgroundColor=Colors.Transparent,
                    HorizontalOptions=LayoutOptions.Fill,
                    VerticalOptions=LayoutOptions.Fill,
                };
                detailHost.SetBinding(ToolTipProperties.TextProperty,cellPath+"."+nameof(BenchmarkProjectCell.ExecutionTooltipText));
                detailHost.Add(detail,0,0);
                detailHost.Add(failureDetail,1,0);
                var detailTap = new TapGestureRecognizer();
                detailTap.Tapped+=(_,_) => {
                    if(layout.BindingContext is BenchmarkTableRow row) {
                        this.viewModel?.SelectBenchmarkCell(row,this.GetProjectFromCellPath(cellPath),CreateBenchmarkSelectionGesture());
                    }
                };
                detailHost.GestureRecognizers.Add(detailTap);
                content.Add(statusIcon,0,0);
                content.Add(spinner,0,0);
                content.Add(detailHost,1,0);
                layout.Add(content,1,1);
                return layout;
            }),
        };
    }

    private DataGridTemplateColumn CreateProjectTextColumn(
        string header,
        string mappingName,
        string cellPath,
        string bindingPath,
        BenchmarkGridSortColumn sortColumn,
        double width,
        TextAlignment alignment) {
        return new DataGridTemplateColumn {
            MappingName=mappingName,
            HeaderText=header,
            Width=width,
            AllowSorting=false,
            HeaderTemplate=this.CreateHeaderTemplate(header,sortColumn),
            CellTemplate=new DataTemplate(() => {
                var grid = new Grid {
                    RowDefinitions= {
                        new RowDefinition { Height = new GridLength(2) },
                        new RowDefinition { Height = GridLength.Star },
                        new RowDefinition { Height = new GridLength(2) },
                    },
                    ColumnDefinitions= {
                        new ColumnDefinition { Width = new GridLength(2) },
                        new ColumnDefinition { Width = GridLength.Star },
                        new ColumnDefinition { Width = new GridLength(2) },
                    },
                    BackgroundColor=Colors.Transparent,
                    VerticalOptions=LayoutOptions.Fill,
                    HorizontalOptions=LayoutOptions.Fill,
                };
                var runningBackground = new BoxView {
                    BackgroundColor=Color.FromArgb("#DBEAFE"),
                    Opacity=0.85,
                    InputTransparent=true,
                };
                runningBackground.SetBinding(IsVisibleProperty,cellPath+"."+nameof(BenchmarkProjectCell.IsRunningVisual));
                var label = new Label {
                    FontSize=10,
                    TextColor=Color.FromArgb("#1E293B"),
                    InputTransparent=true,
                    LineBreakMode=LineBreakMode.TailTruncation,
                    HorizontalTextAlignment=alignment,
                    VerticalTextAlignment=TextAlignment.Center,
                    Margin=new Thickness(0),
                    Padding=new Thickness(2,0),
                };
                label.SetBinding(Label.TextProperty,bindingPath);
                label.SetBinding(Label.TextColorProperty,cellPath+"."+nameof(BenchmarkProjectCell.MetricTextColor));
                label.SetBinding(Label.FontAttributesProperty,cellPath+"."+nameof(BenchmarkProjectCell.MetricFontAttributes));
                label.SetBinding(ToolTipProperties.TextProperty,bindingPath);
                grid.SetBinding(ToolTipProperties.TextProperty,bindingPath);
                grid.Add(runningBackground,0,0);
                Grid.SetColumnSpan(runningBackground,3);
                Grid.SetRowSpan(runningBackground,3);
                this.AddBenchmarkRowUiSelectionFill(grid);
                this.AddBenchmarkProjectUiSelectionFill(grid,this.GetBenchmarkProjectSelectionSide(cellPath));
                grid.Add(label,0,0);
                Grid.SetColumnSpan(label,3);
                Grid.SetRowSpan(label,3);
                this.EnableHoverCellExpansion(grid,label,alignment);
                var tap = new TapGestureRecognizer();
                tap.Tapped+=(_,_) => {
                    if(grid.BindingContext is BenchmarkTableRow row) {
                        this.viewModel?.SelectBenchmarkCell(row,this.GetProjectFromCellPath(cellPath),CreateBenchmarkSelectionGesture());
                    }
                };
                grid.GestureRecognizers.Add(tap);
                return grid;
            }),
        };
    }

    private BenchmarkProjectSelectionSide GetBenchmarkProjectSelectionSide(string cellPath) =>
        cellPath==nameof(BenchmarkTableRow.RightCell)
            ? BenchmarkProjectSelectionSide.Right
            : BenchmarkProjectSelectionSide.Left;

    private void AddBenchmarkProjectUiSelectionFill(Grid grid,BenchmarkProjectSelectionSide side) {
        var selectedPath = side==BenchmarkProjectSelectionSide.Right
            ? nameof(BenchmarkTableRow.IsRightUiSelected)
            : nameof(BenchmarkTableRow.IsLeftUiSelected);
        this.AddBenchmarkSelectionFill(grid,selectedPath);
    }

    private void AddBenchmarkRowUiSelectionFill(Grid grid) {
        this.AddBenchmarkSelectionFill(grid,nameof(BenchmarkTableRow.IsUiSelected));
    }

    private void AddBenchmarkSelectionFill(Grid grid,string visiblePath) {
        var fill = new BoxView {
            BackgroundColor=Color.FromArgb("#22C55E"),
            InputTransparent=true,
        };
        fill.SetBinding(IsVisibleProperty,visiblePath);
        fill.SetBinding(OpacityProperty,new Binding(nameof(this.BenchmarkSelectionFillOpacity),source: this));
        grid.Add(fill,0,0);
        Grid.SetColumnSpan(fill,Math.Max(1,grid.ColumnDefinitions.Count));
        Grid.SetRowSpan(fill,Math.Max(1,grid.RowDefinitions.Count));
    }

    private DataGridTemplateColumn CreateTextColumn(string header,string property,BenchmarkGridSortColumn sortColumn,double width,TextAlignment alignment,Color? backgroundColor = null) {
        var resolvedBackgroundColor = backgroundColor??Color.FromArgb("#F1F5F9");
        return new DataGridTemplateColumn {
            MappingName=property,
            HeaderText=header,
            Width=width,
            AllowSorting=false,
            HeaderTemplate=this.CreateHeaderTemplate(header,sortColumn,resolvedBackgroundColor),
            CellTemplate=new DataTemplate(() => {
                var grid = new Grid {
                    BackgroundColor=backgroundColor??Colors.Transparent,
                    HorizontalOptions=LayoutOptions.Fill,
                    VerticalOptions=LayoutOptions.Fill,
                };
                var label = new Label {
                    FontSize=10,
                    TextColor=Color.FromArgb("#1E293B"),
                    InputTransparent=true,
                    LineBreakMode=LineBreakMode.TailTruncation,
                    HorizontalTextAlignment=alignment,
                    VerticalTextAlignment=TextAlignment.Center,
                    Margin=new Thickness(0),
                    Padding=new Thickness(2,0),
                };
                label.SetBinding(Label.TextProperty,property);
                label.SetBinding(ToolTipProperties.TextProperty,property);
                grid.SetBinding(ToolTipProperties.TextProperty,property);
                this.AddBenchmarkRowUiSelectionFill(grid);
                if(property==nameof(BenchmarkTableRow.BenchmarkMethodText)) {
                    grid.SetBinding(ToolTipProperties.TextProperty,nameof(BenchmarkTableRow.BenchmarkArgumentTooltipText));
                }
                if(UsesBenchmarkRowSelection(property)) {
                    var tap = new TapGestureRecognizer();
                    tap.Tapped+=(_,_) => {
                        if(grid.BindingContext is BenchmarkTableRow row) {
                            this.viewModel?.SelectBenchmarkRow(row,CreateBenchmarkSelectionGesture());
                        }
                    };
                    grid.GestureRecognizers.Add(tap);
                }
                grid.Add(label,0,0);
                this.EnableHoverCellExpansion(grid,label,alignment);
                return grid;
            }),
        };
    }

    private static bool UsesBenchmarkRowSelection(string property) {
        return property==nameof(BenchmarkTableRow.BenchmarkTypeText)
            ||property==nameof(BenchmarkTableRow.BenchmarkMethodText)
            ||property==nameof(BenchmarkTableRow.PairRatioTimeText)
            ||property==nameof(BenchmarkTableRow.PairRatioAllocatedText);
    }

    private void EnableHoverCellExpansion(Grid host,Label label,TextAlignment alignment) {
        var pointer = new PointerGestureRecognizer();
        var originalWidthRequest = label.WidthRequest;
        var originalMinimumWidthRequest = label.MinimumWidthRequest;
        var originalBackground = label.BackgroundColor;
        var originalLineBreakMode = label.LineBreakMode;
        var originalHorizontalOptions = label.HorizontalOptions;
        var originalHostZIndex = host.ZIndex;
        var originalLabelZIndex = label.ZIndex;
        var isExpanded = false;

        pointer.PointerEntered+=(_,_) => {
            if(isExpanded||string.IsNullOrWhiteSpace(label.Text)||host.Width<=0) {
                return;
            }

            var desiredWidth = label.Measure(double.PositiveInfinity,BenchmarkGridRowHeight).Width
                +BenchmarkHoverCellExpansionPadding;
            if(desiredWidth<=host.Width+1) {
                return;
            }

            isExpanded=true;
            host.ZIndex=1000;
            label.ZIndex=1000;
            label.LineBreakMode=LineBreakMode.NoWrap;
            label.WidthRequest=desiredWidth;
            label.MinimumWidthRequest=desiredWidth;
            label.BackgroundColor=Colors.White;
            label.HorizontalOptions=alignment switch {
                TextAlignment.End => LayoutOptions.End,
                TextAlignment.Center => LayoutOptions.Center,
                _ => LayoutOptions.Start,
            };
        };
        pointer.PointerExited+=(_,_) => {
            if(!isExpanded) {
                return;
            }

            isExpanded=false;
            label.WidthRequest=originalWidthRequest;
            label.MinimumWidthRequest=originalMinimumWidthRequest;
            label.BackgroundColor=originalBackground;
            label.LineBreakMode=originalLineBreakMode;
            label.HorizontalOptions=originalHorizontalOptions;
            host.ZIndex=originalHostZIndex;
            label.ZIndex=originalLabelZIndex;
        };
        host.GestureRecognizers.Add(pointer);
    }

    private DataTemplate CreateHeaderTemplate(string text,BenchmarkGridSortColumn sortColumn) =>
        this.CreateHeaderTemplate(text,sortColumn,Color.FromArgb("#F1F5F9"));

    private DataTemplate CreateHeaderTemplate(string text,BenchmarkGridSortColumn sortColumn,Color backgroundColor) {
        return new DataTemplate(() => {
            var grid = new Grid {
                ColumnDefinitions= {
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = new GridLength(12) },
                },
                BackgroundColor=backgroundColor,
                Padding=new Thickness(2,0),
            };
            var label = new Label {
                Text=text,
                FontSize=10,
                FontAttributes=FontAttributes.Bold,
                TextColor=Color.FromArgb("#0F172A"),
                HorizontalOptions=LayoutOptions.Fill,
                HorizontalTextAlignment=TextAlignment.Start,
                VerticalTextAlignment=TextAlignment.Center,
                LineBreakMode=LineBreakMode.TailTruncation,
            };
            var icon = new Image {
                WidthRequest=10,
                HeightRequest=10,
                Aspect=Aspect.AspectFit,
                VerticalOptions=LayoutOptions.Center,
                HorizontalOptions=LayoutOptions.Center,
            };
            void RefreshIcon() {
                if(this.viewModel is null||this.viewModel.CurrentSortColumn!=sortColumn) {
                    icon.IsVisible=false;
                    return;
                }

                icon.IsVisible=true;
                icon.Source=ImageSource.FromFile(this.viewModel.CurrentSortDescending ? "sort_desc.png" : "sort_asc.png");
            }

            RefreshIcon();
            if(this.viewModel is not null) {
                PropertyChangedEventHandler handler = (_,args) => {
                    if(args.PropertyName==nameof(RunnerViewModel.CurrentSortColumn)
                        ||args.PropertyName==nameof(RunnerViewModel.CurrentSortDescending)) {
                        StartupDiagnostics.BeginInvokeOnMainThread("benchmark status icon refresh",RefreshIcon);
                    }
                };
                this.viewModel.PropertyChanged+=handler;
                grid.Unloaded+=(_,_) => this.viewModel.PropertyChanged-=handler;
            }

            grid.Add(label,0,0);
            grid.Add(icon,1,0);
            var leftTap = new TapGestureRecognizer {
                Buttons=ButtonsMask.Primary,
            };
            leftTap.Tapped+=(_,_) => this.viewModel?.SortBenchmarkRows(sortColumn);
            grid.GestureRecognizers.Add(leftTap);
            var rightTap = new TapGestureRecognizer {
                Buttons=ButtonsMask.Secondary,
            };
            rightTap.Tapped+=(_,_) => this.ShowBenchmarkSortDialog();
            grid.GestureRecognizers.Add(rightTap);
            return grid;
        });
    }

    private View CreateCpuView() {
        var PCoreList = this.CreateCpuSectionList(nameof(RunnerViewModel.PerformanceCpuSections));
        var ECoreList = this.CreateCpuSectionList(nameof(RunnerViewModel.EfficiencyCpuSections));
        var UnknownList = this.CreateCpuSectionList(nameof(RunnerViewModel.UnknownCpuSections));

        var layout = new Grid {
            RowDefinitions= {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
            },
            ColumnDefinitions= {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
            },
            RowSpacing=10,
            ColumnSpacing=10,
            Children= {
                PCoreList,
                ECoreList,
                UnknownList,
            },
        };
        Grid.SetColumn(ECoreList,1);
        Grid.SetRow(UnknownList,1);
        Grid.SetColumnSpan(UnknownList,2);
        return layout;
    }

    private View CreateLogView() {
        var logList = new CollectionView {
            SelectionMode=SelectionMode.None,
            VerticalOptions=LayoutOptions.Fill,
            HorizontalOptions=LayoutOptions.Fill,
            ItemTemplate=new DataTemplate(() => {
                var label = new Label {
                    FontSize=11,
                    TextColor=Color.FromArgb("#0F172A"),
                    LineBreakMode=LineBreakMode.WordWrap,
                    Margin=new Thickness(0),
                    Padding=new Thickness(0,2),
                };
                label.SetBinding(Label.TextProperty,".");
                label.BindingContextChanged+=(_,_) => {
                    var isHighlighted = label.BindingContext is string text
                        &&text.StartsWith(RunnerViewModel.HighlightedLogEntryPrefix,StringComparison.Ordinal);
                    label.FontAttributes=isHighlighted ? FontAttributes.Bold : FontAttributes.None;
                    label.TextColor=isHighlighted ? Color.FromArgb("#DC2626") : Color.FromArgb("#0F172A");
                };
                return label;
            }),
            ItemsLayout=new LinearItemsLayout(ItemsLayoutOrientation.Vertical) {
                ItemSpacing=0,
            },
        };
        logList.SetBinding(ItemsView.ItemsSourceProperty,nameof(RunnerViewModel.LogEntries));
        if(this.viewModel is not null) {
            this.viewModel.LogEntries.CollectionChanged+=(_,args) => {
                if(args.Action is NotifyCollectionChangedAction.Add or NotifyCollectionChangedAction.Reset) {
                    StartupDiagnostics.BeginInvokeOnMainThread("cpu selection header refresh",() => {
                        if(this.viewModel?.LogEntries.Count>0) {
                            
                            logList.ScrollTo(this.viewModel.LogEntries.Count-1,position: ScrollToPosition.End,animate: false);
                        }
                    });
                }
            };
        }

        return new Border {
            Stroke=Color.FromArgb("#CBD5E1"),
            StrokeThickness=1,
            BackgroundColor=Color.FromArgb("#F8FAFC"),
            Padding=new Thickness(8),
            Content=logList,
        };
    }

    private View CreateImprovementPromptView() {
        var summary = new Label {
            FontSize=12,
            FontAttributes=FontAttributes.Bold,
            TextColor=Color.FromArgb("#0F172A"),
            VerticalTextAlignment=TextAlignment.Center,
            LineBreakMode=LineBreakMode.TailTruncation,
        };
        summary.SetBinding(Label.TextProperty,nameof(RunnerViewModel.ImprovementCandidateSummaryText));
        var label = new Label {
            FontSize=11,
            TextColor=Color.FromArgb("#0F172A"),
            LineBreakMode=LineBreakMode.WordWrap,
            Margin=new Thickness(0),
            Padding=new Thickness(0),
        };
        label.SetBinding(Label.TextProperty,nameof(RunnerViewModel.ImprovementPromptText));
        var copyButton = new Button {
            Text="⧉",
            WidthRequest=30,
            HeightRequest=28,
            Padding=new Thickness(0),
            FontSize=14,
            FontAttributes=FontAttributes.Bold,
            TextColor=Color.FromArgb("#0F172A"),
            BackgroundColor=Color.FromArgb("#E2E8F0"),
        };
        copyButton.SetBinding(Button.CommandProperty,nameof(RunnerViewModel.CopyImprovementPromptCommand));
        copyButton.SetBinding(IsEnabledProperty,nameof(RunnerViewModel.CanCopyImprovementPrompt));
        ToolTipProperties.SetText(copyButton,"改善プロンプトをコピー");
        var header = new Grid {
            ColumnDefinitions= {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto },
            },
            Padding=new Thickness(0,0,0,6),
        };
        header.Add(summary,0,0);
        header.Add(copyButton,1,0);
        var candidateHeader = new Grid {
            ColumnDefinitions= {
                new ColumnDefinition { Width = new GridLength(32) },
                new ColumnDefinition { Width = new GridLength(88) },
                new ColumnDefinition { Width = new GridLength(190) },
                new ColumnDefinition { Width = new GridLength(72) },
                new ColumnDefinition { Width = new GridLength(132) },
                new ColumnDefinition { Width = new GridLength(132) },
                new ColumnDefinition { Width = new GridLength(86) },
                new ColumnDefinition { Width = new GridLength(86) },
                new ColumnDefinition { Width = GridLength.Star },
            },
            Padding=new Thickness(4,2),
            BackgroundColor=Color.FromArgb("#E2E8F0"),
            ColumnSpacing=6,
        };
        AddCandidateHeader(candidateHeader,"",0);
        AddCandidateHeader(candidateHeader,"カテゴリ",1);
        AddCandidateHeader(candidateHeader,"メソッド",2);
        AddCandidateHeader(candidateHeader,"N",3);
        AddCandidateHeader(candidateHeader,"時間比",4);
        AddCandidateHeader(candidateHeader,"メモリ比",5);
        AddCandidateHeader(candidateHeader,"Left時間",6);
        AddCandidateHeader(candidateHeader,"Right時間",7);
        AddCandidateHeader(candidateHeader,"比較",8);
        var candidates = new CollectionView {
            SelectionMode=SelectionMode.None,
            HeightRequest=160,
            ItemSizingStrategy=ItemSizingStrategy.MeasureFirstItem,
            ItemTemplate=new DataTemplate(this.CreateImprovementCandidateRowView),
            ItemsLayout=new LinearItemsLayout(ItemsLayoutOrientation.Vertical) {
                ItemSpacing=1,
            },
        };
        candidates.SetBinding(ItemsView.ItemsSourceProperty,nameof(RunnerViewModel.ImprovementCandidates));
        var content = new Grid {
            RowDefinitions= {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(164) },
                new RowDefinition { Height = GridLength.Star },
            },
            RowSpacing=4,
        };
        content.Add(header,0,0);
        content.Add(candidateHeader,0,1);
        content.Add(candidates,0,2);
        var promptSection = this.CreateCollapsibleSection(
            "改善プロンプト (Markdown)",
            () => this.isImprovementPromptExpanded,
            value => this.isImprovementPromptExpanded=value,
            new ScrollView {
                Orientation=ScrollOrientation.Vertical,
                Content=label,
            },
            null);
        content.Add(promptSection,0,3);
        return new Border {
            Stroke=Color.FromArgb("#CBD5E1"),
            StrokeThickness=1,
            BackgroundColor=Color.FromArgb("#F8FAFC"),
            Padding=new Thickness(8),
            Content=content,
        };
    }

    private View CreateImprovementCandidateRowView() {
        var grid = new Grid {
            RowDefinitions= {
                new RowDefinition { Height = GridLength.Auto },
            },
            ColumnDefinitions= {
                new ColumnDefinition { Width = new GridLength(32) },
                new ColumnDefinition { Width = new GridLength(88) },
                new ColumnDefinition { Width = new GridLength(190) },
                new ColumnDefinition { Width = new GridLength(72) },
                new ColumnDefinition { Width = new GridLength(132) },
                new ColumnDefinition { Width = new GridLength(132) },
                new ColumnDefinition { Width = new GridLength(86) },
                new ColumnDefinition { Width = new GridLength(86) },
                new ColumnDefinition { Width = GridLength.Star },
            },
            ColumnSpacing=6,
            Padding=new Thickness(4,2),
            BackgroundColor=Colors.White,
        };
        var check = new CheckBox {
            WidthRequest=24,
            HeightRequest=22,
            VerticalOptions=LayoutOptions.Center,
            HorizontalOptions=LayoutOptions.Center,
        };
        check.SetBinding(CheckBox.IsCheckedProperty,new Binding(nameof(BenchmarkImprovementCandidateRow.IsSelected),BindingMode.TwoWay));
        grid.Add(check,0,0);
        AddCandidateCell(grid,nameof(BenchmarkImprovementCandidateRow.Category),1,TextAlignment.Start);
        AddCandidateCell(grid,nameof(BenchmarkImprovementCandidateRow.MethodName),2,TextAlignment.Start);
        AddCandidateCell(grid,nameof(BenchmarkImprovementCandidateRow.WorstElementCountText),3,TextAlignment.End);
        grid.Add(CreateCandidateGaugeCell(
            nameof(BenchmarkImprovementCandidateRow.TimeGaugeText),
            static row => row.TimeGaugeFraction,
            Color.FromArgb("#2563EB")),4,0);
        grid.Add(CreateCandidateGaugeCell(
            nameof(BenchmarkImprovementCandidateRow.AllocatedGaugeText),
            static row => row.AllocatedGaugeFraction,
            Color.FromArgb("#F97316")),5,0);
        AddCandidateCell(grid,nameof(BenchmarkImprovementCandidateRow.LeftTimeText),6,TextAlignment.End);
        AddCandidateCell(grid,nameof(BenchmarkImprovementCandidateRow.RightTimeText),7,TextAlignment.End);
        AddCandidateCell(grid,nameof(BenchmarkImprovementCandidateRow.SourceTabText),8,TextAlignment.Start);
        grid.SetBinding(ToolTipProperties.TextProperty,nameof(BenchmarkImprovementCandidateRow.TooltipText));
        return grid;
    }

    // 時間比/メモリ比を横幅で表す簡易バー。比率 3.0x でバー満タンとして正規化する。
    private static View CreateCandidateGaugeCell(string textPath,Func<BenchmarkImprovementCandidateRow,double> getFraction,Color barColor) {
        var fraction = 0.0;
        var track = new Grid { HeightRequest=6,BackgroundColor=Color.FromArgb("#E2E8F0") };
        var fill = new BoxView { Color=barColor,HorizontalOptions=LayoutOptions.Start,WidthRequest=0 };
        track.Children.Add(fill);
        var text = new Label {
            FontSize=9,
            TextColor=Color.FromArgb("#334155"),
            LineBreakMode=LineBreakMode.TailTruncation,
            HorizontalTextAlignment=TextAlignment.End,
        };
        text.SetBinding(Label.TextProperty,textPath);
        track.SizeChanged+=(_,_) => fill.WidthRequest=track.Width*fraction;
        var cell = new Grid {
            RowDefinitions= {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(6) },
            },
            RowSpacing=1,
            VerticalOptions=LayoutOptions.Center,
        };
        cell.Add(text,0,0);
        cell.Add(track,0,1);
        void Refresh() {
            fraction=cell.BindingContext is BenchmarkImprovementCandidateRow row
                ? getFraction(row)
                : 0.0;
            fill.WidthRequest=track.Width>0 ? track.Width*fraction : 0;
        }
        cell.BindingContextChanged+=(_,_) => Refresh();
        return cell;
    }

    private static void AddCandidateHeader(Grid grid,string text,int column) {
        grid.Add(new Label {
            Text=text,
            FontSize=10,
            FontAttributes=FontAttributes.Bold,
            TextColor=Color.FromArgb("#334155"),
            LineBreakMode=LineBreakMode.TailTruncation,
            VerticalTextAlignment=TextAlignment.Center,
            HorizontalTextAlignment=column>=3&&column<=7 ? TextAlignment.End : TextAlignment.Start,
        },column,0);
    }

    private static void AddCandidateCell(Grid grid,string path,int column,TextAlignment alignment) {
        var label = new Label {
            FontSize=10,
            TextColor=Color.FromArgb("#0F172A"),
            LineBreakMode=LineBreakMode.TailTruncation,
            VerticalTextAlignment=TextAlignment.Center,
            HorizontalTextAlignment=alignment,
        };
        label.SetBinding(Label.TextProperty,path);
        grid.Add(label,column,0);
    }

    private VerticalStackLayout CreateCpuSectionList(string itemsSourcePath) {
        var layout = new VerticalStackLayout {
            Spacing=10,
        };
        BindableLayout.SetItemTemplate(layout,new DataTemplate(this.CreateCpuSectionView));
        layout.SetBinding(BindableLayout.ItemsSourceProperty,itemsSourcePath);
        return layout;
    }

    private View CreateCpuSectionView() {
        var header = new Border {
            Stroke=Color.FromArgb("#CBD5E1"),
            StrokeThickness=1,
            BackgroundColor=Color.FromArgb("#F8FAFC"),
            Padding=new Thickness(6,3),
        };
        var headerGrid = new Grid {
            ColumnDefinitions= {
                new ColumnDefinition { Width = new GridLength(CpuLogicalCheckColumnWidth) },
                new ColumnDefinition { Width = new GridLength(24) },
                new ColumnDefinition { Width = GridLength.Star },
            },
            ColumnSpacing=4,
            HorizontalOptions=LayoutOptions.Fill,
        };
        var sectionCheck = this.CreateCpuCheckBox();
        sectionCheck.InputTransparent=true;
        sectionCheck.SetBinding(SfCheckBox.IsCheckedProperty,new Binding(nameof(CpuTableSection.State),BindingMode.OneWay,converter: new CpuSelectionStateToBoolConverter()));
        var sectionCheckHost = new Grid {
            BackgroundColor=Colors.Transparent,
            HorizontalOptions=LayoutOptions.Fill,
            VerticalOptions=LayoutOptions.Fill,
        };
        sectionCheckHost.Add(sectionCheck,0,0);
        sectionCheckHost.GestureRecognizers.Add(new TapGestureRecognizer {
            Command=new Command(() => {
                if(header.BindingContext is CpuTableSection section) {
                    this.viewModel?.ToggleCpuSection(section);
                }
            }),
        });
        var badge = this.CreateCoreBadge();
        badge.SetBinding(BackgroundColorProperty,nameof(CpuTableSection.BadgeBackgroundColor));
        if(badge.Content is Image badgeImage) {
            badgeImage.SetBinding(
                Image.SourceProperty,
                new Binding(nameof(CpuTableSection.BadgeImageSource),converter: new ImageFileSourceConverter()));
        }
        var title = new Label {
            FontSize=12,
            FontAttributes=FontAttributes.Bold,
            TextColor=Color.FromArgb("#0F172A"),
            VerticalTextAlignment=TextAlignment.Center,
            LineBreakMode=LineBreakMode.TailTruncation,
        };
        title.SetBinding(Label.TextProperty,nameof(CpuTableSection.Title));
        headerGrid.Add(sectionCheckHost,0,0);
        headerGrid.Add(badge,1,0);
        headerGrid.Add(title,2,0);
        header.Content=headerGrid;

        StartupDiagnostics.WriteSyncfusionAssemblies("CreateCpuSectionView before SfDataGrid");
        var grid = new SfDataGrid {
            AutoGenerateColumnsMode=AutoGenerateColumnsMode.None,
            RowHeight=CpuGridRowHeight,
            HeaderRowHeight=CpuGridHeaderRowHeight,
            AllowTriStateSorting=false,
            SortingMode=DataGridSortingMode.None,
            GridLinesVisibility=GridLinesVisibility.Both,
            DefaultColumnWidth=56,
        };
        this.RegisterDataGridUpdateBatchTarget(grid);
        this.ConfigureDataGridLines(grid);
        var sectionLayout = new VerticalStackLayout {
            Spacing=3,
            Children= {
                header,
                grid,
            },
        };
        grid.SetBinding(SfDataGrid.ItemsSourceProperty,nameof(CpuTableSection.Rows));
        grid.BindingContextChanged+=(_,_) => {
            if(grid.BindingContext is not CpuTableSection section) {
                return;
            }

            this.RebuildCpuDataGridColumns(grid,section);
            grid.HeightRequest=CpuSectionGridHeaderHeight+(section.Rows.Count*CpuSectionRowHeight)+CpuSectionGridExtraHeight;
            if(section.CoreKind==CpuCoreKind.Performance) {
                var maxWidth = this.GetCpuRightEdgeWidth(section);
                grid.MaximumWidthRequest=maxWidth;
                grid.WidthRequest=maxWidth;
                grid.HorizontalOptions=LayoutOptions.Start;
                header.WidthRequest=maxWidth;
                header.MaximumWidthRequest=maxWidth;
                header.HorizontalOptions=LayoutOptions.Start;
                sectionLayout.WidthRequest=maxWidth;
                sectionLayout.MaximumWidthRequest=maxWidth;
                sectionLayout.HorizontalOptions=LayoutOptions.Start;
            }
            else if(section.CoreKind==CpuCoreKind.Efficiency) {
                var maxWidth = this.GetCpuVisibleWidth(section,4);
                grid.MaximumWidthRequest=maxWidth;
                grid.WidthRequest=maxWidth;
                grid.HorizontalOptions=LayoutOptions.Start;
                header.WidthRequest=maxWidth;
                header.MaximumWidthRequest=maxWidth;
                header.HorizontalOptions=LayoutOptions.Start;
                sectionLayout.WidthRequest=maxWidth;
                sectionLayout.MaximumWidthRequest=maxWidth;
                sectionLayout.HorizontalOptions=LayoutOptions.Start;
            }
            else {
                grid.HorizontalOptions=LayoutOptions.Fill;
                header.ClearValue(WidthRequestProperty);
                header.ClearValue(MaximumWidthRequestProperty);
                header.HorizontalOptions=LayoutOptions.Fill;
                sectionLayout.ClearValue(WidthRequestProperty);
                sectionLayout.ClearValue(MaximumWidthRequestProperty);
                sectionLayout.HorizontalOptions=LayoutOptions.Fill;
            }
        };

        return sectionLayout;
    }

    private void RebuildCpuDataGridColumns(SfDataGrid grid,CpuTableSection section) {
        grid.Columns.Clear();
        grid.Columns.Add(this.CreateCpuCoreColumn(section));
        foreach(var column in section.Columns) {
            grid.Columns.Add(this.CreateCpuLogicalColumn(column));
        }
    }

    private DataGridTemplateColumn CreateCpuCoreColumn(CpuTableSection section) {
        return new DataGridTemplateColumn {
            MappingName=nameof(CpuTableRow.Label),
            HeaderText="コア",
            Width=this.GetCpuCoreColumnWidth(section),
            AllowSorting=false,
            HeaderTemplate=this.CreateCpuSimpleHeaderTemplate("コア"),
            CellTemplate=new DataTemplate(() => {
                var layout = new Grid {
                    ColumnDefinitions= {
                        new ColumnDefinition { Width = new GridLength(CpuLogicalCheckColumnWidth) },
                        new ColumnDefinition { Width = new GridLength(24) },
                        new ColumnDefinition { Width = GridLength.Star },
                    },
                    ColumnSpacing=4,
                    Padding=new Thickness(2,0),
                    HorizontalOptions=LayoutOptions.Fill,
                    VerticalOptions=LayoutOptions.Fill,
                };
                var check = this.CreateCpuCheckBox();
                check.InputTransparent=true;
                check.SetBinding(SfCheckBox.IsCheckedProperty,new Binding(nameof(CpuTableRow.IsChecked),BindingMode.OneWay));
                var badge = this.CreateCoreBadge();
                badge.SetBinding(BackgroundColorProperty,nameof(CpuTableRow.BadgeBackgroundColor));
                if(badge.Content is Image badgeImage) {
                    badgeImage.SetBinding(
                        Image.SourceProperty,
                        new Binding(nameof(CpuTableRow.BadgeImageSource),converter: new ImageFileSourceConverter()));
                }
                var label = new Label {
                    FontSize=CpuGridCellFontSize,
                    TextColor=Color.FromArgb("#334155"),
                    HorizontalTextAlignment=TextAlignment.Start,
                    VerticalTextAlignment=TextAlignment.Center,
                    LineBreakMode=LineBreakMode.TailTruncation,
                };
                label.SetBinding(Label.TextProperty,nameof(CpuTableRow.Label));
                layout.Add(check,0,0);
                layout.Add(badge,1,0);
                layout.Add(label,2,0);
                var tap = new TapGestureRecognizer();
                tap.Tapped+=(_,_) => {
                    if(layout.BindingContext is CpuTableRow row) {
                        this.viewModel?.ToggleCpuRow(row);
                    }
                };
                layout.GestureRecognizers.Add(tap);
                return layout;
            }),
        };
    }

    private double GetCpuCoreColumnWidth(CpuTableSection section) {
        var labelWidth = section.Rows.Count==0
            ? 24
            : section.Rows.Max(static row => row.Label.Length)*7;
        var baseWidth = Math.Max(180,CpuLogicalCheckColumnWidth+24+labelWidth+42);
        return section.CoreKind switch {
            CpuCoreKind.Performance or CpuCoreKind.Efficiency => Math.Max(1,baseWidth*0.65),
            _ => baseWidth,
        };
    }

    private double GetCpuRightEdgeWidth(CpuTableSection section) {
        return this.GetCpuCoreColumnWidth(section)
            +section.Columns.Count*CpuLogicalColumnWidth
            +2*CpuLogicalCheckColumnWidth;
    }

    private double GetCpuVisibleWidth(CpuTableSection section,int logicalColumnCount) {
        return this.GetCpuCoreColumnWidth(section)
            +Math.Min(section.Columns.Count,logicalColumnCount)*CpuLogicalColumnWidth
            +2*CpuLogicalCheckColumnWidth;
    }

    private DataGridTemplateColumn CreateCpuLogicalColumn(CpuTableColumn column) {
        var cellPath = nameof(CpuTableRow.GridCells)+"["+column.Index+"]";
        return new DataGridTemplateColumn {
            MappingName="Cpu"+column.Index,
            HeaderText=column.HeaderText,
            Width=CpuLogicalColumnWidth,
            AllowSorting=false,
            HeaderTemplate=this.CreateCpuColumnHeaderTemplate(column),
            CellTemplate=new DataTemplate(() => {
                var layout = new Grid {
                    ColumnDefinitions= {
                        new ColumnDefinition { Width = new GridLength(CpuLogicalCheckColumnWidth) },
                        new ColumnDefinition { Width = new GridLength(18) },
                        new ColumnDefinition { Width = new GridLength(24) },
                        new ColumnDefinition { Width = new GridLength(CpuLogicalNumberWidth) },
                        new ColumnDefinition { Width = GridLength.Star },
                    },
                    ColumnSpacing=2,
                    HorizontalOptions=LayoutOptions.Fill,
                    VerticalOptions=LayoutOptions.Center,
                };
                layout.SetBinding(IsVisibleProperty,new Binding(cellPath,converter: new NullToBoolConverter()));
                var check = this.CreateCpuCheckBox();
                check.SetBinding(SfCheckBox.IsCheckedProperty,new Binding(cellPath+"."+nameof(CpuTableCell.IsSelected),BindingMode.TwoWay));
                var spinner = new ActivityIndicator {
                    WidthRequest=14,
                    HeightRequest=14,
                    Color=Color.FromArgb("#2563EB"),
                    VerticalOptions=LayoutOptions.Center,
                    HorizontalOptions=LayoutOptions.Center,
                };
                spinner.SetBinding(ActivityIndicator.IsRunningProperty,new Binding(cellPath+"."+nameof(CpuTableCell.IsRunningJob)));
                spinner.SetBinding(IsVisibleProperty,new Binding(cellPath+"."+nameof(CpuTableCell.IsRunningJob)));
                var statusIcon = new Image {
                    WidthRequest=14,
                    HeightRequest=14,
                    InputTransparent=true,
                    HorizontalOptions=LayoutOptions.Center,
                    VerticalOptions=LayoutOptions.Center,
                    Aspect=Aspect.AspectFit,
                };
                statusIcon.SetBinding(
                    Image.SourceProperty,
                    new Binding(cellPath+"."+nameof(CpuTableCell.StatusImageSource),converter: new ImageFileSourceConverter()));
                statusIcon.SetBinding(IsVisibleProperty,cellPath+"."+nameof(CpuTableCell.IsStatusImageVisible));
                var badge = this.CreateCoreBadge();
                badge.SetBinding(BackgroundColorProperty,cellPath+"."+nameof(CpuTableCell.BadgeBackgroundColor));
                if(badge.Content is Image badgeImage) {
                    badgeImage.SetBinding(
                        Image.SourceProperty,
                        new Binding(cellPath+"."+nameof(CpuTableCell.CoreKindImageSource),converter: new ImageFileSourceConverter()));
                }
                var numberLabel = new Label {
                    FontSize=CpuGridCellFontSize,
                    TextColor=Color.FromArgb("#0F172A"),
                    HorizontalTextAlignment=TextAlignment.Start,
                    VerticalTextAlignment=TextAlignment.Center,
                    LineBreakMode=LineBreakMode.TailTruncation,
                };
                numberLabel.SetBinding(Label.TextProperty,cellPath+"."+nameof(CpuTableCell.LogicalProcessorText));
                numberLabel.SetBinding(ToolTipProperties.TextProperty,cellPath+"."+nameof(CpuTableCell.LogicalProcessorText));
                var statusLabel = new Label {
                    FontSize=10,
                    TextColor=Color.FromArgb("#334155"),
                    HorizontalTextAlignment=TextAlignment.Start,
                    VerticalTextAlignment=TextAlignment.Center,
                    LineBreakMode=LineBreakMode.TailTruncation,
                };
                statusLabel.SetBinding(Label.TextProperty,cellPath+"."+nameof(CpuTableCell.CellStatusText));
                statusLabel.SetBinding(ToolTipProperties.TextProperty,cellPath+"."+nameof(CpuTableCell.DisplayText));
                layout.SetBinding(ToolTipProperties.TextProperty,cellPath+"."+nameof(CpuTableCell.DisplayText));
                layout.Add(check,0,0);
                layout.Add(statusIcon,1,0);
                layout.Add(spinner,1,0);
                layout.Add(badge,2,0);
                layout.Add(numberLabel,3,0);
                layout.Add(statusLabel,4,0);
                return layout;
            }),
        };
    }

    private DataTemplate CreateCpuSimpleHeaderTemplate(string text) {
        return new DataTemplate(() => new Label {
            Text=text,
            FontSize=10,
            FontAttributes=FontAttributes.Bold,
            TextColor=Color.FromArgb("#0F172A"),
            BackgroundColor=Color.FromArgb("#F1F5F9"),
            HorizontalOptions=LayoutOptions.Fill,
            HorizontalTextAlignment=TextAlignment.Start,
            VerticalTextAlignment=TextAlignment.Center,
            Padding=new Thickness(2,0),
        });
    }

    private DataTemplate CreateCpuColumnHeaderTemplate(CpuTableColumn column) {
        return new DataTemplate(() => {
            var layout = new Grid {
                ColumnDefinitions= {
                    new ColumnDefinition { Width = new GridLength(CpuLogicalCheckColumnWidth) },
                    new ColumnDefinition { Width = GridLength.Star },
                },
                HorizontalOptions=LayoutOptions.Fill,
                VerticalOptions=LayoutOptions.Fill,
                BackgroundColor=Color.FromArgb("#F1F5F9"),
            };
            var check = this.CreateCpuCheckBox();
            check.InputTransparent=true;
            check.SetBinding(SfCheckBox.IsCheckedProperty,new Binding(nameof(CpuTableColumn.IsChecked),BindingMode.OneWay,source: column));
            var checkHost = new Grid {
                BackgroundColor=Colors.Transparent,
                HorizontalOptions=LayoutOptions.Fill,
                VerticalOptions=LayoutOptions.Fill,
            };
            checkHost.Add(check,0,0);
            layout.Add(checkHost,0,0);
            layout.Add(new Label {
                Text="論理コア",
                FontSize=10,
                FontAttributes=FontAttributes.Bold,
                TextColor=Color.FromArgb("#0F172A"),
                HorizontalTextAlignment=TextAlignment.Start,
                VerticalTextAlignment=TextAlignment.Center,
                LineBreakMode=LineBreakMode.TailTruncation,
                Padding=new Thickness(2,0),
            },1,0);
            var tap = new TapGestureRecognizer();
            tap.Tapped+=(_,_) => {
                if(column.Section is { } section) {
                    this.viewModel?.ToggleCpuColumn(section,column.Index);
                }
            };
            checkHost.GestureRecognizers.Add(tap);
            return layout;
        });
    }

    private SfCheckBox CreateCpuCheckBox() =>
        this.CreateSfCheckBox(CpuCheckBoxWidth,CpuCheckBoxHeight);

    private SfCheckBox CreateSfCheckBox(double width,double height,bool isThreeState = false) {
        return new SfCheckBox {
            WidthRequest=width,
            HeightRequest=height,
            MinimumWidthRequest=width,
            MinimumHeightRequest=height,
            IsThreeState=isThreeState,
            HorizontalOptions=LayoutOptions.Center,
            VerticalOptions=LayoutOptions.Center,
            Margin=new Thickness(0),
        };
    }

    private void ConfigureDataGridLines(SfDataGrid grid) {
        var lineColor = Color.FromArgb("#CBD5E1");
        grid.DefaultStyle=new DataGridStyle {
            GridLineColor=lineColor,
            HeaderGridLineColor=lineColor,
            GridLineStrokeThickness=1,
            HeaderGridLineStrokeThickness=1,
        };
    }

    private void ConfigureDataGridScrollBars(SfDataGrid grid) {
        void Apply() {
            var visualContainer = grid.GetVisualContainer();
            if(visualContainer is null) {
                return;
            }

            this.ApplyDataGridScrollBarSize(visualContainer);
            this.TryInvokeMember(visualContainer,"UpdateScrollBars");
            visualContainer.InvalidateMeasure();
        }

        void ApplySafely() {
            try {
                Apply();
            }
            catch(Exception ex) {
                StartupDiagnostics.Write("Failed to apply benchmark grid scroll bar size.",ex);
            }
        }

        async void ApplyNowAndLater() {
            ApplySafely();
            await Task.Delay(100);
            StartupDiagnostics.BeginInvokeOnMainThread("benchmark grid scroll bar apply",ApplySafely);
        }

        grid.Loaded+=(_,_) => ApplyNowAndLater();
        grid.HandlerChanged+=(_,_) => StartupDiagnostics.BeginInvokeOnMainThread("benchmark grid scroll bar handler changed",ApplyNowAndLater);
        grid.SizeChanged+=(_,_) => StartupDiagnostics.BeginInvokeOnMainThread("benchmark grid scroll bar size changed",ApplySafely);
    }

    private void ApplyDataGridScrollBarSize(object visualContainer) {
        this.TrySetMemberValue(visualContainer,"ScrollerWidth",BenchmarkScrollBarSize);
        this.TrySetMemberValue(visualContainer,"ScrollerHeight",BenchmarkScrollBarSize);

        if(this.TryGetMemberValue(visualContainer,"HorizontalScrollbar") is { } horizontalScrollbar) {
            this.TrySetMemberValue(horizontalScrollbar,"Height",BenchmarkScrollBarSize);
            this.TrySetMemberValue(horizontalScrollbar,"HeightRequest",BenchmarkScrollBarSize);
            this.TrySetMemberValue(horizontalScrollbar,"MinimumHeightRequest",BenchmarkScrollBarSize);
            this.TrySetMemberValue(horizontalScrollbar,"ScrollerHeight",BenchmarkScrollBarSize);
        }

        if(this.TryGetMemberValue(visualContainer,"VerticalScrollbar") is { } verticalScrollbar) {
            this.TrySetMemberValue(verticalScrollbar,"Width",BenchmarkScrollBarSize);
            this.TrySetMemberValue(verticalScrollbar,"WidthRequest",BenchmarkScrollBarSize);
            this.TrySetMemberValue(verticalScrollbar,"MinimumWidthRequest",BenchmarkScrollBarSize);
            this.TrySetMemberValue(verticalScrollbar,"ScrollerWidth",BenchmarkScrollBarSize);
        }
    }

    private object? TryGetMemberValue(object target,string name) {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
        var property = target.GetType().GetProperty(name,flags);
        if(property is not null&&property.CanRead) {
            return property.GetValue(target);
        }

        return target.GetType().GetField(name,flags)?.GetValue(target);
    }

    private void TrySetMemberValue(object target,string name,double value) {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
        var property = target.GetType().GetProperty(name,flags);
        if(property is not null&&property.CanWrite) {
            property.SetValue(target,this.ConvertMemberValue(value,property.PropertyType));
            return;
        }

        var field = target.GetType().GetField(name,flags);
        field?.SetValue(target,this.ConvertMemberValue(value,field.FieldType));
    }

    private object ConvertMemberValue(double value,Type type) {
        var targetType = Nullable.GetUnderlyingType(type)??type;
        if(targetType==typeof(double)) {
            return value;
        }

        if(targetType==typeof(float)) {
            return (float)value;
        }

        if(targetType==typeof(int)) {
            return (int)value;
        }

        return Convert.ChangeType(value,targetType);
    }

    private void TryInvokeMember(object target,string name,params object[] arguments) {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
        var method = target.GetType()
            .GetMethods(flags)
            .FirstOrDefault(item => item.Name==name&&item.GetParameters().Length==arguments.Length);
        method?.Invoke(target,arguments);
    }

    private Border CreateCoreBadge() {
        return new Border {
            WidthRequest=20,
            HeightRequest=18,
            Stroke=Color.FromArgb("#CBD5E1"),
            StrokeThickness=1,
            Padding=new Thickness(0),
            Content=new Image {
                WidthRequest=12,
                HeightRequest=12,
                Aspect=Aspect.AspectFit,
                HorizontalOptions=LayoutOptions.Center,
                VerticalOptions=LayoutOptions.Center,
            },
        };
    }

    private Label CreateSelectionStateBox() {
        return new Label {
            FontSize=10,
            FontAttributes=FontAttributes.Bold,
            TextColor=Color.FromArgb("#0F172A"),
            BackgroundColor=Colors.White,
            HorizontalTextAlignment=TextAlignment.Center,
            VerticalTextAlignment=TextAlignment.Center,
            WidthRequest=26,
            HeightRequest=18,
            LineBreakMode=LineBreakMode.TailTruncation,
        };
    }

    private View CreateResultsView() {
        var resultList = new CollectionView {
            SelectionMode=SelectionMode.None,
            ItemTemplate=new DataTemplate(this.CreateResultRow),
            ItemsLayout=new LinearItemsLayout(ItemsLayoutOrientation.Vertical) {
                ItemSpacing=0,
            },
        };
        resultList.SetBinding(ItemsView.ItemsSourceProperty,nameof(RunnerViewModel.VisibleResultItems));
        resultList.IsVisible=false;
        return resultList;
    }

    private View CreateResultRow() {
        var grid = new Grid {
            Padding=new Thickness(0),
            HeightRequest=22,
            ColumnDefinitions= {
                new ColumnDefinition { Width = new GridLength(2.0,GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(0.8,GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(0.4,GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(0.8,GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(1.1,GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(1.0,GridUnitType.Star) },
            },
            ColumnSpacing=6,
        };
        grid.Add(this.BoundCell(nameof(BenchmarkResultItem.Name),11,TextAlignment.Start),0,0);
        grid.Add(this.BoundCell(nameof(BenchmarkResultItem.Status),11,TextAlignment.Start),1,0);
        grid.Add(this.BoundCell(nameof(BenchmarkResultItem.ResultCheckText),11,TextAlignment.Center),2,0);
        grid.Add(this.BoundCell(nameof(BenchmarkResultItem.ResultSeedText),11,TextAlignment.End),3,0);
        grid.Add(this.BoundCell(nameof(BenchmarkResultItem.Mean),11,TextAlignment.End),4,0);
        grid.Add(this.BoundCell(nameof(BenchmarkResultItem.AllocatedBytes),11,TextAlignment.End),5,0);
        return grid;
    }

    private Label BoundCell(string propertyName,double fontSize,TextAlignment alignment) {
        var label = new Label {
            FontSize=fontSize,
            TextColor=Color.FromArgb("#1E293B"),
            LineBreakMode=LineBreakMode.TailTruncation,
            HorizontalTextAlignment=alignment,
            VerticalTextAlignment=TextAlignment.Center,
            Margin=new Thickness(0),
            Padding=new Thickness(1,0),
        };
        label.SetBinding(Label.TextProperty,propertyName);
        return label;
    }

    private View CreateChartView() =>
        this.CreateChartView(this.viewModel?.SelectedSourceTabViewState);

    private View CreateChartView(BenchmarkSourceTabViewState? state) {
        var chart = new SfCartesianChart {
            IsTransposed=true,
            Legend=new ChartLegend(),
            TooltipBehavior=new ChartTooltipBehavior {
                FontSize=12,
                Duration=4,
            },
            BackgroundColor=Colors.Transparent,
            PlotAreaBackgroundView=new BoxView {
                Color=Colors.Transparent,
                InputTransparent=true,
            },
            VerticalOptions=LayoutOptions.Fill,
        };
        if(state is not null) {
            chart.SetBinding(HeightRequestProperty,new Binding(nameof(BenchmarkSourceTabViewState.ChartHeight),source: state));
        }else{
            chart.SetBinding(HeightRequestProperty,nameof(RunnerViewModel.ChartHeight));
        }
        var xAxis = new CategoryAxis {
            LabelPlacement=LabelPlacement.BetweenTicks
        };
        var yAxis = new NumericalAxis {
            Minimum=0,
            Maximum=100,
        };
        chart.XAxes.Add(xAxis);
        chart.YAxes.Add(yAxis);

        var leftSeries = new StackingColumn100Series {
            XBindingPath=nameof(ChartComparisonPoint.DisplayName),
            YBindingPath=nameof(ChartComparisonPoint.LeftNormalValue),
            ShowDataLabels=false,
            EnableTooltip=true,
            Spacing=0,
            Width=0.58,
            Fill=new SolidColorBrush(Color.FromArgb("#2563EB")),
            DataLabelSettings=new CartesianDataLabelSettings {
                BarAlignment=DataLabelAlignment.Middle,
                LabelPlacement=DataLabelPlacement.Inner,
            },
        };
        if(state is not null) {
            leftSeries.ItemsSource=state.ChartPoints;
        }else{
            leftSeries.SetBinding(ChartSeries.ItemsSourceProperty,nameof(RunnerViewModel.ChartPoints));
        }
        leftSeries.TooltipTemplate=this.CreateTooltipTemplate();

        var leftFallbackSeries = new StackingColumn100Series {
            XBindingPath=nameof(ChartComparisonPoint.DisplayName),
            YBindingPath=nameof(ChartComparisonPoint.LeftFallbackValue),
            ShowDataLabels=false,
            EnableTooltip=true,
            Spacing=0,
            Width=0.58,
            Fill=new SolidColorBrush(Color.FromArgb("#93C5FD")),
            IsVisibleOnLegend=false,
            DataLabelSettings=new CartesianDataLabelSettings {
                BarAlignment=DataLabelAlignment.Middle,
                LabelPlacement=DataLabelPlacement.Inner,
            },
        };
        if(state is not null) {
            leftFallbackSeries.ItemsSource=state.ChartPoints;
        }else{
            leftFallbackSeries.SetBinding(ChartSeries.ItemsSourceProperty,nameof(RunnerViewModel.ChartPoints));
        }
        leftFallbackSeries.TooltipTemplate=this.CreateTooltipTemplate();

        var rightSeries = new StackingColumn100Series {
            XBindingPath=nameof(ChartComparisonPoint.DisplayName),
            YBindingPath=nameof(ChartComparisonPoint.RightNormalValue),
            ShowDataLabels=false,
            EnableTooltip=true,
            Spacing=0,
            Width=0.58,
            Fill=new SolidColorBrush(Color.FromArgb("#F97316")),
            DataLabelSettings=new CartesianDataLabelSettings {
                BarAlignment=DataLabelAlignment.Middle,
                LabelPlacement=DataLabelPlacement.Inner,
            },
        };
        if(state is not null) {
            rightSeries.ItemsSource=state.ChartPoints;
        }else{
            rightSeries.SetBinding(ChartSeries.ItemsSourceProperty,nameof(RunnerViewModel.ChartPoints));
        }
        rightSeries.TooltipTemplate=this.CreateTooltipTemplate();

        var rightFallbackSeries = new StackingColumn100Series {
            XBindingPath=nameof(ChartComparisonPoint.DisplayName),
            YBindingPath=nameof(ChartComparisonPoint.RightFallbackValue),
            ShowDataLabels=false,
            EnableTooltip=true,
            Spacing=0,
            Width=0.58,
            Fill=new SolidColorBrush(Color.FromArgb("#FDBA74")),
            IsVisibleOnLegend=false,
            DataLabelSettings=new CartesianDataLabelSettings {
                BarAlignment=DataLabelAlignment.Middle,
                LabelPlacement=DataLabelPlacement.Inner,
            },
        };
        if(state is not null) {
            rightFallbackSeries.ItemsSource=state.ChartPoints;
        }else{
            rightFallbackSeries.SetBinding(ChartSeries.ItemsSourceProperty,nameof(RunnerViewModel.ChartPoints));
        }
        rightFallbackSeries.TooltipTemplate=this.CreateTooltipTemplate();
        this.BindChartSeriesLabels(leftSeries,rightSeries);

        chart.Series.Add(leftSeries);
        chart.Series.Add(leftFallbackSeries);
        chart.Series.Add(rightSeries);
        chart.Series.Add(rightFallbackSeries);

        var chartBackgroundDrawable = new BenchmarkChartSelectionBackgroundDrawable(
            () => state?.ChartPoints??this.viewModel?.ChartPoints??[],
            () => this.BenchmarkSelectionFillOpacity,
            chart,
            xAxis,
            yAxis);
        var chartBackground = new GraphicsView {
            Drawable=chartBackgroundDrawable,
            InputTransparent=true,
            BackgroundColor=Colors.Transparent,
            VerticalOptions=LayoutOptions.Fill,
            HorizontalOptions=LayoutOptions.Fill,
        };
        var chartOverlay = new GraphicsView {
            Drawable=new BenchmarkChartSelectionHitDrawable(),
            InputTransparent=false,
            BackgroundColor=Colors.Transparent,
            VerticalOptions=LayoutOptions.Fill,
            HorizontalOptions=LayoutOptions.Fill,
        };
        if(state is not null) {
            chartBackground.SetBinding(HeightRequestProperty,new Binding(nameof(BenchmarkSourceTabViewState.ChartHeight),source: state));
            chartOverlay.SetBinding(HeightRequestProperty,new Binding(nameof(BenchmarkSourceTabViewState.ChartHeight),source: state));
        }else{
            chartBackground.SetBinding(HeightRequestProperty,nameof(RunnerViewModel.ChartHeight));
            chartOverlay.SetBinding(HeightRequestProperty,nameof(RunnerViewModel.ChartHeight));
        }
        PropertyChangedEventHandler chartSelectionPulseHandler = (_,args) => {
            if(args.PropertyName==nameof(this.BenchmarkSelectionFillOpacity)) {
                chartBackground.Invalidate();
            }
        };
        this.PropertyChanged+=chartSelectionPulseHandler;
        chartBackground.Unloaded+=(_,_) => this.PropertyChanged-=chartSelectionPulseHandler;
        if(state is not null) {
            NotifyCollectionChangedEventHandler handler = (_,_) => {
                chartBackground.Invalidate();
                chartOverlay.Invalidate();
            };
            state.ChartPoints.CollectionChanged+=handler;
            chartBackground.Unloaded+=(_,_) => state.ChartPoints.CollectionChanged-=handler;
            chartOverlay.Unloaded+=(_,_) => state.ChartPoints.CollectionChanged-=handler;
        }else if(this.viewModel is not null) {
            NotifyCollectionChangedEventHandler handler = (_,_) => {
                chartBackground.Invalidate();
                chartOverlay.Invalidate();
            };
            this.viewModel.ChartPoints.CollectionChanged+=handler;
            chartBackground.Unloaded+=(_,_) => this.viewModel.ChartPoints.CollectionChanged-=handler;
            chartOverlay.Unloaded+=(_,_) => this.viewModel.ChartPoints.CollectionChanged-=handler;
        }
        var chartLabelTooltipText = new Label {
            FontSize=12,
            Padding=new Thickness(8,6),
            TextColor=Colors.White,
            BackgroundColor=Color.FromArgb("#0F172A"),
            LineBreakMode=LineBreakMode.WordWrap,
        };
        var chartLabelTooltip = new Border {
            Stroke=Color.FromArgb("#334155"),
            StrokeThickness=1,
            BackgroundColor=Color.FromArgb("#0F172A"),
            StrokeShape=new Microsoft.Maui.Controls.Shapes.RoundRectangle {
                CornerRadius=4,
            },
            Content=chartLabelTooltipText,
            IsVisible=false,
            InputTransparent=true,
            HorizontalOptions=LayoutOptions.Start,
            VerticalOptions=LayoutOptions.Start,
            ZIndex=2000,
        };
        var chartHost = new Grid {
            VerticalOptions=LayoutOptions.Fill,
            Children= {
                chartBackground,
                chart,
                chartOverlay,
                chartLabelTooltip,
            },
        };
        if(state is not null) {
            chartHost.SetBinding(HeightRequestProperty,new Binding(nameof(BenchmarkSourceTabViewState.ChartHeight),source: state));
        }else{
            chartHost.SetBinding(HeightRequestProperty,nameof(RunnerViewModel.ChartHeight));
        }
        void HideChartLabelTooltip() {
            chartLabelTooltip.IsVisible=false;
        }

        void UpdateChartLabelTooltip(Point point) {
            if(chartHost.Width<=0||chartHost.Height<=0) {
                HideChartLabelTooltip();
                return;
            }

            var hit = chartBackgroundDrawable.HitTest(
                new SizeF((float)chartHost.Width,(float)chartHost.Height),
                (float)point.X,
                (float)point.Y);
            if(hit is not BenchmarkChartSelectionHit { Target: BenchmarkChartSelectionTarget.Row } labelHit) {
                HideChartLabelTooltip();
                return;
            }

            var tooltip = (state?.ChartPoints??this.viewModel?.ChartPoints??[])
                .FirstOrDefault(item => string.Equals(item.Benchmark,labelHit.Benchmark,StringComparison.OrdinalIgnoreCase))
                ?.TooltipText??string.Empty;
            if(string.IsNullOrWhiteSpace(tooltip)) {
                HideChartLabelTooltip();
                return;
            }

            chartLabelTooltipText.Text=tooltip;
            chartLabelTooltip.MaximumWidthRequest=Math.Max(240,Math.Min(720,chartHost.Width-24));
            var x = Math.Min(point.X+12,Math.Max(8,chartHost.Width-360));
            var y = Math.Max(8,point.Y-18);
            chartLabelTooltip.Margin=new Thickness(x,y,0,0);
            chartLabelTooltip.IsVisible=true;
        }

        var chartPointer = new PointerGestureRecognizer();
        chartPointer.PointerEntered+=(_,args) => {
            if(args.GetPosition(chartOverlay) is { } point) {
                UpdateChartLabelTooltip(point);
            }
        };
        chartPointer.PointerMoved+=(_,args) => {
            if(args.GetPosition(chartOverlay) is { } point) {
                UpdateChartLabelTooltip(point);
            }
        };
        chartPointer.PointerExited+=(_,_) => HideChartLabelTooltip();
        chartOverlay.GestureRecognizers.Add(chartPointer);
        var chartTap = new TapGestureRecognizer();
        chartTap.Tapped+=(_,args) => {
            var position = args.GetPosition(chartOverlay);
            if(position is not { } point) {
                return;
            }

            if(chartBackgroundDrawable.HitTest(new SizeF((float)chartHost.Width,(float)chartHost.Height),(float)point.X,(float)point.Y) is { } hit) {
                this.SelectBenchmarkChartHit(state,hit);
                chartBackground.Invalidate();
                chartOverlay.Invalidate();
            }
        };
        chartOverlay.GestureRecognizers.Add(chartTap);

        var emptyText = new Label {
            HorizontalOptions=LayoutOptions.Center,
            VerticalOptions=LayoutOptions.Center,
            TextColor=Color.FromArgb("#64748B"),
        };

        var chartContent = new Grid {
            RowDefinitions= {
                new RowDefinition { Height = GridLength.Star },
            },
            VerticalOptions=LayoutOptions.Fill,
            Children= {
                new ScrollView{
                    Orientation=ScrollOrientation.Vertical,
                    VerticalOptions=LayoutOptions.Fill,
                    Content=chartHost,
                },
            },
        };
        // 枠はチャート全体の点滅ではなく、常時表示の外枠としてだけ使う。
        var chartBorder = new Border {
            Stroke=Color.FromArgb("#CBD5E1"),
            StrokeThickness=1,
            BackgroundColor=Colors.White,
            Content=chartContent,
        };

        var grid = new Grid {
            Children= {
                chartBorder,
                emptyText,
            },
        };
        if(state is not null) {
            emptyText.SetBinding(IsVisibleProperty,new Binding(nameof(BenchmarkSourceTabViewState.ChartPoints)+".Count",source: state,converter: new CountIsZeroConverter()));
            emptyText.SetBinding(Label.TextProperty,new Binding(nameof(BenchmarkSourceTabViewState.ChartEmptyReasonText),source: state));
            ToolTipProperties.SetText(emptyText,state.ChartEmptyReasonTooltipText);
            emptyText.SetBinding(ToolTipProperties.TextProperty,new Binding(nameof(BenchmarkSourceTabViewState.ChartEmptyReasonTooltipText),source: state));
        }else{
            emptyText.SetBinding(IsVisibleProperty,new Binding(nameof(RunnerViewModel.ChartPoints)+".Count",converter: new CountIsZeroConverter()));
            emptyText.SetBinding(Label.TextProperty,nameof(RunnerViewModel.ChartEmptyReasonText));
            emptyText.SetBinding(ToolTipProperties.TextProperty,nameof(RunnerViewModel.ChartEmptyReasonTooltipText));
        }
        return grid;
    }

    private void SelectBenchmarkChartHit(BenchmarkSourceTabViewState? state,BenchmarkChartSelectionHit hit) {
        if(this.viewModel is null) {
            return;
        }

        var pair = state?.Definition.Pair??this.viewModel.SelectedComparisonPair;
        var gesture = CreateBenchmarkSelectionGesture();
        switch(hit.Target) {
            case BenchmarkChartSelectionTarget.Row:
                this.viewModel.SelectBenchmarkRowFromChart(state,hit.Benchmark,gesture);
                break;
            case BenchmarkChartSelectionTarget.LeftBar:
                this.viewModel.SelectBenchmarkCellFromChart(state,hit.Benchmark,pair.LeftProject,gesture);
                break;
            case BenchmarkChartSelectionTarget.RightBar:
                this.viewModel.SelectBenchmarkCellFromChart(state,hit.Benchmark,pair.RightProject,gesture);
                break;
        }
    }

    private void BindChartSeriesLabels(StackingColumn100Series leftSeries,StackingColumn100Series rightSeries) {
        void Apply() {
            if(this.viewModel is null) {
                return;
            }

            leftSeries.Label=this.viewModel.ChartLeftLegendLabel;
            rightSeries.Label=this.viewModel.ChartRightLegendLabel;
        }

        Apply();
        if(this.viewModel is not null) {
            this.viewModel.PropertyChanged+=(_,args) => {
                if(args.PropertyName==nameof(RunnerViewModel.SelectedComparisonPair)) {
                    StartupDiagnostics.BeginInvokeOnMainThread("chart point label apply",Apply);
                }
            };
        }
    }


    private DataTemplate CreateDataLabelTemplate(string textPropertyName) {
        return new DataTemplate(() => {
            var label = new Label {
                FontSize=10,
                TextColor=Colors.White,
                LineBreakMode=LineBreakMode.TailTruncation,
                HorizontalTextAlignment=TextAlignment.Center,
                VerticalTextAlignment=TextAlignment.Center,
            };
            label.SetBinding(Label.TextProperty,"Item."+textPropertyName);
            return label;
        });
    }

    private DataTemplate CreateTooltipTemplate() {
        return new DataTemplate(() => {
            var label = new Label {
                FontSize=12,
                Padding=new Thickness(8,6),
                TextColor=Colors.White,
                BackgroundColor=Color.FromArgb("#0F172A"),
                LineBreakMode=LineBreakMode.WordWrap,
            };
            label.SetBinding(Label.TextProperty,"Item."+nameof(ChartComparisonPoint.TooltipText));
            return label;
        });
    }
}

internal enum BenchmarkProjectSelectionSide {
    Left,
    Right,
}

internal enum BenchmarkChartSelectionTarget {
    Row,
    LeftBar,
    RightBar,
}

internal readonly record struct BenchmarkChartSelectionHit(string Benchmark,BenchmarkChartSelectionTarget Target);

internal sealed class BenchmarkChartSelectionBackgroundDrawable:IDrawable {
    private const float OuterPadding = 4;
    private const float LabelHitWidth = 280;
    private readonly Func<IReadOnlyList<ChartComparisonPoint>> getPoints;
    private readonly Func<double> getSelectionOpacity;
    private readonly SfCartesianChart chart;
    private readonly CategoryAxis xAxis;
    private readonly NumericalAxis yAxis;

    public BenchmarkChartSelectionBackgroundDrawable(
        Func<IReadOnlyList<ChartComparisonPoint>> getPoints,
        Func<double> getSelectionOpacity,
        SfCartesianChart chart,
        CategoryAxis xAxis,
        NumericalAxis yAxis) {
        this.getPoints=getPoints;
        this.getSelectionOpacity=getSelectionOpacity;
        this.chart=chart;
        this.xAxis=xAxis;
        this.yAxis=yAxis;
    }

    public void Draw(ICanvas canvas,RectF dirtyRect) {
        var points = this.getPoints();
        if(points.Count==0) {
            return;
        }

        canvas.FillColor=Color.FromArgb("#22C55E").WithAlpha((float)Math.Clamp(this.getSelectionOpacity(),0,1));
        foreach(var row in this.CreateLayout(points,new SizeF(dirtyRect.Width,dirtyRect.Height))) {
            if(row.Point.IsRowUiSelected) {
                canvas.FillRectangle(row.RowSelectionRect);
                continue;
            }

            if(row.Point.IsLeftUiSelected) {
                canvas.FillRectangle(row.LeftSelectionRect);
            }

            if(row.Point.IsRightUiSelected) {
                canvas.FillRectangle(row.RightSelectionRect);
            }
        }
    }

    public BenchmarkChartSelectionHit? HitTest(SizeF size,float x,float y) {
        var points = this.getPoints();
        if(points.Count==0) {
            return null;
        }

        foreach(var row in this.CreateLayout(points,size)) {
            if(!row.HitRect.Contains(x,y)) {
                continue;
            }

            if(x<row.PlotLeft) {
                return new BenchmarkChartSelectionHit(row.Point.Benchmark,BenchmarkChartSelectionTarget.Row);
            }

            if(x<=row.LeftBarRect.Right) {
                return new BenchmarkChartSelectionHit(row.Point.Benchmark,BenchmarkChartSelectionTarget.LeftBar);
            }

            if(x<=row.PlotRight) {
                return new BenchmarkChartSelectionHit(row.Point.Benchmark,BenchmarkChartSelectionTarget.RightBar);
            }
        }

        return null;
    }

    private IReadOnlyList<BenchmarkChartSelectionLayoutRow> CreateLayout(IReadOnlyList<ChartComparisonPoint> points,SizeF size) {
        var fallbackPlotLeft = MathF.Min(LabelHitWidth,size.Width*0.42f);
        var fallbackPlotRight = size.Width-OuterPadding;
        var (plotLeft,plotRight) = this.GetSeriesPlotRange(size,fallbackPlotLeft,fallbackPlotRight);
        var top = 42f;
        var bottomPadding = 16f;
        var rowHeight = MathF.Max(1,(size.Height-top-bottomPadding)/Math.Max(1,points.Count));
        var barHeight = MathF.Min(16,MathF.Max(6,rowHeight*0.58f));
        var rows = new BenchmarkChartSelectionLayoutRow[points.Count];
        for(var index=0;index<points.Count;index++) {
            var point = points[index];
            var fallbackCenter = top+rowHeight*(index+0.5f);
            var center = this.GetAxisCoordinate(this.xAxis,index,fallbackCenter,size.Height);
            var y = center-barHeight/2;
            var barRects = BenchmarkChartSelectionLayoutLogic.CreateBarRects(
                plotLeft,
                plotRight,
                point.LeftValue,
                point.RightValue);
            var splitX = Math.Clamp(barRects.LeftBarRect.Right,plotLeft,plotRight);
            var hitTop = center-rowHeight/2;
            rows[index]=new BenchmarkChartSelectionLayoutRow(
                point,
                plotLeft,
                plotRight,
                new RectF(OuterPadding,hitTop,MathF.Max(1,plotRight-OuterPadding),rowHeight),
                new RectF(plotLeft,hitTop,MathF.Max(1,splitX-plotLeft),rowHeight),
                new RectF(splitX,hitTop,MathF.Max(1,plotRight-splitX),rowHeight),
                new RectF(barRects.LeftBarRect.Left,y,barRects.LeftBarRect.Width,barHeight),
                new RectF(barRects.RightBarRect.Left,y,barRects.RightBarRect.Width,barHeight),
                new RectF(OuterPadding,hitTop,MathF.Max(1,plotRight-OuterPadding),rowHeight));
        }

        return rows;
    }

    private (float PlotLeft,float PlotRight) GetSeriesPlotRange(SizeF size,float fallbackPlotLeft,float fallbackPlotRight) {
        var seriesBounds = this.chart.SeriesBounds;
        var seriesLeft = (float)seriesBounds.Left;
        var seriesRight = (float)seriesBounds.Right;
        if(IsValidPlotRange(seriesLeft,seriesRight,size.Width)) {
            return (seriesLeft,seriesRight);
        }

        var plotLeft = this.GetAxisCoordinate(this.yAxis,0,fallbackPlotLeft,size.Width);
        var plotRight = this.GetAxisCoordinate(this.yAxis,100,fallbackPlotRight,size.Width);
        if(plotRight<plotLeft) {
            (plotLeft,plotRight)=(plotRight,plotLeft);
        }

        if(IsValidPlotRange(plotLeft,plotRight,size.Width)) {
            return (plotLeft,plotRight);
        }

        return (fallbackPlotLeft,fallbackPlotRight);
    }

    private static bool IsValidPlotRange(float plotLeft,float plotRight,float width) {
        return float.IsFinite(plotLeft)
            &&float.IsFinite(plotRight)
            &&plotLeft>=0
            &&plotRight<=width
            &&plotRight-plotLeft>=40;
    }

    private float GetAxisCoordinate(ChartAxis axis,double value,float fallback,float limit) {
        var coordinate = this.chart.ValueToPoint(axis,value);
        if(double.IsFinite(coordinate)&&coordinate>0&&coordinate<limit) {
            return (float)coordinate;
        }

        coordinate=axis.ValueToPoint(value);
        if(double.IsFinite(coordinate)&&coordinate>0&&coordinate<limit) {
            return (float)coordinate;
        }

        return fallback;
    }
}

internal readonly record struct BenchmarkChartSelectionLayoutRow(
    ChartComparisonPoint Point,
    float PlotLeft,
    float PlotRight,
    RectF RowSelectionRect,
    RectF LeftSelectionRect,
    RectF RightSelectionRect,
    RectF LeftBarRect,
    RectF RightBarRect,
    RectF HitRect);

internal sealed class BenchmarkChartSelectionHitDrawable:IDrawable {
    public void Draw(ICanvas canvas,RectF dirtyRect) {
    }
}

public sealed class BenchmarkCircularProgressView:GraphicsView {
    public static readonly BindableProperty ProgressProperty = BindableProperty.Create(
        nameof(Progress),
        typeof(double),
        typeof(BenchmarkCircularProgressView),
        0d,
        propertyChanged:static (bindable,_,_) => ((BenchmarkCircularProgressView)bindable).Invalidate());

    public static readonly BindableProperty ProgressColorProperty = BindableProperty.Create(
        nameof(ProgressColor),
        typeof(Color),
        typeof(BenchmarkCircularProgressView),
        DefaultProgressColor,
        propertyChanged:static (bindable,_,_) => ((BenchmarkCircularProgressView)bindable).Invalidate());

    public static readonly BindableProperty OutlineColorProperty = BindableProperty.Create(
        nameof(OutlineColor),
        typeof(Color),
        typeof(BenchmarkCircularProgressView),
        DefaultOutlineColor,
        propertyChanged:static (bindable,_,_) => ((BenchmarkCircularProgressView)bindable).Invalidate());

    private static readonly Color TrackColor=Color.FromArgb("#CBD5E1");
    private static readonly Color DefaultProgressColor=Color.FromArgb("#F97316");
    private static readonly Color DefaultOutlineColor=Color.FromArgb("#64748B");

    public BenchmarkCircularProgressView() {
        this.Drawable=new BenchmarkCircularProgressDrawable(this);
        this.Loaded+=(_,_) => this.Invalidate();
        this.SizeChanged+=(_,_) => this.Invalidate();
    }

    public double Progress{
        get => (double)this.GetValue(ProgressProperty);
        set => this.SetValue(ProgressProperty,value);
    }

    public Color ProgressColor{
        get => (Color)this.GetValue(ProgressColorProperty);
        set => this.SetValue(ProgressColorProperty,value);
    }

    public Color OutlineColor{
        get => (Color)this.GetValue(OutlineColorProperty);
        set => this.SetValue(OutlineColorProperty,value);
    }

    private sealed class BenchmarkCircularProgressDrawable(BenchmarkCircularProgressView owner):IDrawable {
        public void Draw(ICanvas canvas,RectF dirtyRect) {
            var size=MathF.Min(dirtyRect.Width,dirtyRect.Height);
            if(size<=0) {
                return;
            }

            var strokeSize=MathF.Max(1.25f,size*0.11f);
            var radius=MathF.Max(0,size/2f-strokeSize/2f);
            var centerX=dirtyRect.X+dirtyRect.Width/2f;
            var centerY=dirtyRect.Y+dirtyRect.Height/2f;
            canvas.FillColor=TrackColor;
            canvas.FillCircle(centerX,centerY,radius);

            var progress=Math.Clamp(owner.Progress,0,1);
            if(progress>0) {
                canvas.FillColor=owner.ProgressColor;
                if(progress>=1) {
                    canvas.FillCircle(centerX,centerY,radius);
                }else{
                    canvas.FillPath(CreateProgressPath(centerX,centerY,radius,progress));
                }
            }

            canvas.StrokeColor=owner.OutlineColor;
            canvas.StrokeSize=strokeSize;
            canvas.DrawCircle(centerX,centerY,radius);
            if(progress<=0) {
                return;
            }
        }

        private static PathF CreateProgressPath(float centerX,float centerY,float radius,double progress) {
            var path = new PathF();
            var startAngle = -90d;
            var sweep = 360d*progress;
            var steps = Math.Max(2,(int)Math.Ceiling(sweep/12d));
            path.MoveTo(centerX,centerY);
            for(var index=0;index<=steps;index++) {
                var angle = (float)((startAngle+(sweep*index/steps))*Math.PI/180d);
                path.LineTo(
                    centerX+(MathF.Cos(angle)*radius),
                    centerY+(MathF.Sin(angle)*radius));
            }
            path.Close();
            return path;
        }
    }
}

public sealed class CountIsZeroConverter:IValueConverter {
    public object Convert(object? value,Type targetType,object? parameter,System.Globalization.CultureInfo culture) {
        return value is int count&&count==0;
    }

    public object ConvertBack(object? value,Type targetType,object? parameter,System.Globalization.CultureInfo culture) {
        throw new NotSupportedException();
    }
}

public sealed class NullToBoolConverter:IValueConverter {
    public object Convert(object? value,Type targetType,object? parameter,System.Globalization.CultureInfo culture) {
        return value is not null;
    }

    public object ConvertBack(object? value,Type targetType,object? parameter,System.Globalization.CultureInfo culture) {
        throw new NotSupportedException();
    }
}

public sealed class ImageFileSourceConverter:IValueConverter {
    public object? Convert(object? value,Type targetType,object? parameter,System.Globalization.CultureInfo culture) {
        return value is string path&&!string.IsNullOrWhiteSpace(path)
            ? ImageSource.FromFile(path)
            : null;
    }

    public object ConvertBack(object? value,Type targetType,object? parameter,System.Globalization.CultureInfo culture) {
        throw new NotSupportedException();
    }
}

public sealed class CpuSelectionStateToBoolConverter:IValueConverter {
    public object Convert(object? value,Type targetType,object? parameter,System.Globalization.CultureInfo culture) {
        return value is CpuSelectionState.Checked;
    }

    public object ConvertBack(object? value,Type targetType,object? parameter,System.Globalization.CultureInfo culture) {
        throw new NotSupportedException();
    }
}
