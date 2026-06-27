using System.ComponentModel;
using System.Collections.Specialized;
using Syncfusion.Maui.Buttons;
using Syncfusion.Maui.DataGrid;
using Syncfusion.Maui.Toolkit.Charts;
using Syncfusion.Maui.Toolkit.TabView;

namespace Benchmark;

public sealed class MainPage:ContentPage{
    private const double CpuCheckBoxWidth = 34;
    private const double CpuCheckBoxHeight = 28;
    private const double BenchmarkSelectionColumnWidth = 52;
    private const double BenchmarkAggregateCheckBoxSize = 32;
    private const double BenchmarkCellCheckBoxWidth = 34;
    private const double BenchmarkCellCheckBoxHeight = 28;
    private const double BenchmarkProjectStatusColumnWidth = 248;
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
    private const double CpuSectionGridHeaderHeight = 30;
    private const double CpuSectionRowHeight = 24;
    private const double CpuSectionGridExtraHeight = 2;
    private const double CpuSectionSpacing = 3;
    private const double CpuListSpacing = 10;
    private const double CpuContentRowSpacing = 10;
    private BenchmarkRunnerViewModel? viewModel;
    private readonly List<Action> collapsibleSectionRefreshers = [];

#if WINDOWS
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern short GetKeyState(int virtualKey);
#endif

    public MainPage(){
        try{
            StartupDiagnostics.Write("MainPage constructor started.");
            this.Title="Seamless Benchmark";
            this.BackgroundColor=Color.FromArgb("#F8FAFC");
            this.viewModel=this.CreateViewModel();
            this.BindingContext=this.viewModel;

            var toolbar = this.CreateToolbar();
            var content = this.CreateBenchmarkPanelContent();

            this.Content=new Grid{
                RowDefinitions = {
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = GridLength.Star },
                },
                Children = {
                    toolbar,
                    content,
                },
            };
            Grid.SetRow(content,1);
            this.Loaded+=this.OnLoadedAsync;
            StartupDiagnostics.Write("MainPage constructor completed.");
        }catch(Exception ex){
            StartupDiagnostics.Write("MainPage constructor failed.",ex);
            this.Content=StartupDiagnostics.CreateErrorPage("Benchmark page failed",ex).Content;
        }
    }

    private async void OnLoadedAsync(object? sender,EventArgs args){
        if(this.viewModel is null){
            return;
        }

        try{
            StartupDiagnostics.Write("MainPage InitializeAsync started.");
            await this.viewModel.InitializeAsync();
            StartupDiagnostics.Write("MainPage InitializeAsync completed.");
        }catch(Exception ex){
            StartupDiagnostics.Write("MainPage InitializeAsync failed.",ex);
            this.viewModel.StatusText="Startup load failed: "+ex.Message;
        }
    }

    public async Task<bool> RequestCloseAsync(){
        if(this.viewModel is null){
            return true;
        }

        if(this.viewModel.IsBenchmarkRunActive){
            var shouldClose = await this.DisplayAlert(
                "Benchmark is running",
                "Stop the running benchmark worker and close the application?",
                "OK",
                "Cancel");
            if(!shouldClose){
                return false;
            }

            this.viewModel.StatusText="Stopping before close...";
            await this.viewModel.CancelAndWaitAsync();
        }

        if(!this.viewModel.HasPendingCloseStateSave){
            return true;
        }

        var shouldSave = await this.DisplayAlert(
            "ベンチマーク状態の保存",
            "保存して閉じますか?",
            "保存して閉じる",
            "保存せずに閉じる");
        if(shouldSave){
            this.viewModel.SaveUiState();
            return true;
        }

        var shouldDiscard = await this.DisplayAlert(
            "保存せずに閉じる",
            "保存せずに閉じますか?",
            "保存せずに閉じる",
            "キャンセル");
        if(shouldDiscard){
            this.viewModel.DiscardPendingCloseStateSave();
            return true;
        }

        return false;
    }

    private BenchmarkRunnerViewModel CreateViewModel(){
        var resultService = new BenchmarkResultService();
        var comparisonService = new BenchmarkComparisonService(resultService);
        var csvService = new BenchmarkCsvService();
        var benchmarkRoot = Path.Combine(
            this.FindSolutionRoot()??Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Work",
            "Benchmark");
        return new BenchmarkRunnerViewModel(
            new BenchmarkCatalogService(),
            new CpuTopologyService(),
            new BenchmarkRunService(
                new BenchmarkQueueService(),
                new BenchmarkWorkerProcessService(),
                csvService,
                comparisonService),
            csvService,
            comparisonService,
            new BenchmarkResultStore(benchmarkRoot),
            new BenchmarkCsvDiscoveryService(csvService),
            new BenchmarkUiStateStore(benchmarkRoot),
            benchmarkRoot);
    }

    private string? FindSolutionRoot(){
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while(directory is not null){
            if(File.Exists(Path.Combine(directory.FullName,"Seamless.slnx"))){
                return directory.FullName;
            }
            directory=directory.Parent;
        }

        return null;
    }

    private View CreateToolbar(){
        var runToggleButton = this.CommandButton("実行",nameof(BenchmarkRunnerViewModel.ToggleRunCommand));
        this.BindRunToggleButtonState(runToggleButton);

        var thresholdPicker = new Picker{
            WidthRequest=112,
            HeightRequest=30,
            ItemDisplayBinding=new Binding(nameof(ThresholdOption.DisplayName)),
            Margin=new Thickness(0),
        };
        thresholdPicker.SetBinding(Picker.ItemsSourceProperty,nameof(BenchmarkRunnerViewModel.ThresholdOptions));
        thresholdPicker.SetBinding(Picker.SelectedItemProperty,nameof(BenchmarkRunnerViewModel.SelectedThreshold));
        thresholdPicker.SetBinding(IsEnabledProperty,nameof(BenchmarkRunnerViewModel.CanChangeBenchmarkConditions));

        var executionProfilePicker = new Picker{
            WidthRequest=96,
            HeightRequest=30,
            ItemDisplayBinding=new Binding(nameof(ExecutionProfileOption.DisplayName)),
            Margin=new Thickness(0),
        };
        executionProfilePicker.SetBinding(Picker.ItemsSourceProperty,nameof(BenchmarkRunnerViewModel.ExecutionProfiles));
        executionProfilePicker.SetBinding(Picker.SelectedItemProperty,nameof(BenchmarkRunnerViewModel.SelectedExecutionProfile));
        executionProfilePicker.SetBinding(IsEnabledProperty,nameof(BenchmarkRunnerViewModel.CanChangeBenchmarkConditions));

        var runPicker = new Picker{
            WidthRequest=220,
            HeightRequest=30,
            ItemDisplayBinding=new Binding(nameof(BenchmarkRunOption.DisplayName)),
            Margin=new Thickness(0),
        };
        runPicker.SetBinding(Picker.ItemsSourceProperty,nameof(BenchmarkRunnerViewModel.RunOptions));
        runPicker.SetBinding(Picker.SelectedItemProperty,nameof(BenchmarkRunnerViewModel.SelectedRunOption));

        var grid = new Grid{
            Padding=new Thickness(10,8,10,6),
            ColumnDefinitions = {
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
        grid.Add(this.ToolbarLabel("Mode"),2,0);
        grid.Add(executionProfilePicker,3,0);
        grid.Add(runToggleButton,4,0);
        grid.Add(this.ToolbarLabel("結果"),5,0);
        grid.Add(runPicker,6,0);
        return grid;
    }

    private Button CommandButton(string text,string commandPath){
        var button = new Button{
            Text=text,
            Padding=new Thickness(9,3),
            MinimumHeightRequest=28,
            HeightRequest=30,
            FontSize=12,
            Margin=new Thickness(0),
        };
        button.SetBinding(Button.CommandProperty,commandPath);
        return button;
    }

    private void BindRunToggleButtonState(Button button){
        void Apply(){
            if(this.viewModel is null){
                return;
            }

            var isRunning=this.viewModel.IsBenchmarkRunActive;
            button.Text=this.viewModel.RunToggleText;
            button.BackgroundColor=isRunning ? Color.FromArgb("#4C1D95") : Color.FromArgb("#F8FAFC");
            button.TextColor=isRunning ? Colors.White : Color.FromArgb("#0F172A");
            button.BorderColor=isRunning ? Color.FromArgb("#7C3AED") : Color.FromArgb("#CBD5E1");
            button.BorderWidth=isRunning ? 2 : 1;
            button.Padding=isRunning ? new Thickness(8,5,10,2) : new Thickness(9,3);
            button.TranslationY=isRunning ? 2 : 0;
            button.FontAttributes=isRunning ? FontAttributes.Bold : FontAttributes.None;
        }

        Apply();
        if(this.viewModel is not null){
            this.viewModel.PropertyChanged+=(_,args) => {
                if(args.PropertyName==nameof(BenchmarkRunnerViewModel.IsBenchmarkRunActive)
                    ||args.PropertyName==nameof(BenchmarkRunnerViewModel.RunToggleText)){
                    MainThread.BeginInvokeOnMainThread(Apply);
                }
            };
        }
    }

    private Label ToolbarLabel(string text){
        return new Label{
            Text=text,
            FontSize=12,
            VerticalTextAlignment=TextAlignment.Center,
            TextColor=Color.FromArgb("#334155"),
            Margin=new Thickness(0),
        };
    }

    private View CreateBenchmarkPanelContent(){
        var tabView = new SfTabView{
            HeightRequest=30,
            TabBarHeight=30,
            TabBarBackground=Color.FromArgb("#F8FAFC"),
            IndicatorBackground=Color.FromArgb("#0F766E"),
            HeaderItemSpacing=2,
            EnableSwiping=false,
            ContentTransitionDuration=0,
            BackgroundColor=Colors.White,
        };
        var tabItems = new TabItemCollection();
        foreach(var option in this.viewModel?.SourceTabs??[]){
            tabItems.Add(new SfTabItem{
                Header=option.DisplayName,
                FontSize=12,
                Content=new Grid(),
            });
        }

        tabView.Items=tabItems;
        tabView.SelectedIndex=0;
        tabView.PropertyChanged+=(_,args) => {
            if(args.PropertyName!=nameof(SfTabView.SelectedIndex)||this.viewModel is null){
                return;
            }

            var index=tabView.SelectedIndex;
            if(index>=0&&index<this.viewModel.SourceTabs.Count){
                this.viewModel.SelectedSourceTab=this.viewModel.SourceTabs[index];
            }
        };
        var content = this.CreateBenchmarkTabContent();
        var grid = new Grid{
            RowDefinitions = {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star },
            },
            RowSpacing=3,
            BackgroundColor=Colors.White,
            Padding=new Thickness(6,4),
            Children = {
                tabView,
                content,
            },
        };
        Grid.SetRow(content,1);
        return grid;
    }

    private View CreateBenchmarkTabContent(){
        var grid = new Grid{
            RowDefinitions = {
                new RowDefinition { Height = GridLength.Star },
                new RowDefinition { Height = GridLength.Auto },
            },
            ColumnDefinitions = {
                new ColumnDefinition { Width = new GridLength(5,GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(5,GridUnitType.Star) },
            },
            RowSpacing=4,
            ColumnSpacing=6,
        };
        var benchmarkGrid = this.CreateBenchmarkGrid();
        benchmarkGrid.VerticalOptions=LayoutOptions.Fill;
        grid.Add(benchmarkGrid,0,0);
        grid.Add(this.CreateBenchmarkChartPanel(),1,0);
        var lowerTabView = new SfTabView{
            TabBarHeight=LowerTabBarHeight,
            TabBarBackground=Color.FromArgb("#E2E8F0"),
            IndicatorBackground=Color.FromArgb("#0F766E"),
            HeaderItemSpacing=4,
            EnableSwiping=false,
            ContentTransitionDuration=0,
            BackgroundColor=Color.FromArgb("#F8FAFC"),
        };
        var lowerItems = new TabItemCollection();
        lowerItems.Add(new SfTabItem{
            Header="CPU",
            FontSize=12,
            Content=this.CreateCpuView(),
        });
        lowerItems.Add(new SfTabItem{
            Header="ログ",
            FontSize=12,
            Content=this.CreateLogView(),
        });
        lowerTabView.Items=lowerItems;
        void ApplyLowerTabHeight(){
            var height=LowerTabBarHeight+this.CalculateCpuTabContentHeight();
            if(Math.Abs(lowerTabView.HeightRequest-height)>0.1){
                lowerTabView.HeightRequest=height;
            }
        }

        ApplyLowerTabHeight();
        lowerTabView.Loaded+=(_,_) => ApplyLowerTabHeight();
        lowerTabView.SizeChanged+=(_,_) => ApplyLowerTabHeight();
        lowerTabView.SelectedIndex=(int)(this.viewModel?.SelectedLowerTab??BenchmarkLowerTab.Cpu);
        lowerTabView.PropertyChanged+=(_,args) => {
            if(args.PropertyName!=nameof(SfTabView.SelectedIndex)||this.viewModel is null){
                return;
            }

            this.viewModel.SelectedLowerTab=lowerTabView.SelectedIndex==1
                ? BenchmarkLowerTab.Log
                : BenchmarkLowerTab.Cpu;
        };
        var lowerTabFrame = new Border{
            Stroke=Color.FromArgb("#CBD5E1"),
            StrokeThickness=1,
            StrokeShape=new Microsoft.Maui.Controls.Shapes.RoundRectangle{
                CornerRadius=6,
            },
            BackgroundColor=Color.FromArgb("#F8FAFC"),
            Content=lowerTabView,
        };
        grid.Add(lowerTabFrame,0,1);
        grid.SetColumnSpan(lowerTabFrame,2);
        if(this.viewModel is not null){
            this.viewModel.PropertyChanged+=(_,args) => {
                if(args.PropertyName==nameof(BenchmarkRunnerViewModel.SelectedLowerTab)){
                    MainThread.BeginInvokeOnMainThread(() => lowerTabView.SelectedIndex=(int)this.viewModel.SelectedLowerTab);
                }
            };
        }
        return grid;
    }

    private double CalculateCpuTabContentHeight(){
        if(this.viewModel is null){
            return 0;
        }

        var performanceHeight=this.CalculateCpuSectionListHeight(this.viewModel.PerformanceCpuSections);
        var efficiencyHeight=this.CalculateCpuSectionListHeight(this.viewModel.EfficiencyCpuSections);
        var unknownHeight=this.CalculateCpuSectionListHeight(this.viewModel.UnknownCpuSections);
        var topRowHeight=Math.Max(performanceHeight,efficiencyHeight);
        if(unknownHeight<=0){
            return topRowHeight;
        }

        return topRowHeight<=0
            ? unknownHeight
            : topRowHeight+CpuContentRowSpacing+unknownHeight;
    }

    private double CalculateCpuSectionListHeight(IEnumerable<CpuTableSection> sections){
        var heights=sections.Select(this.CalculateCpuSectionHeight).ToArray();
        if(heights.Length==0){
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

    private View CreateBenchmarkChartPanel(){
        var header = new Grid{
            ColumnDefinitions = {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
            },
            Padding=new Thickness(8,4),
            BackgroundColor=Color.FromArgb("#F1F5F9"),
            ColumnSpacing=8,
        };
        header.Add(this.CreateMetricRadioGroup(),0,0);
        header.Add(new Label{
            Text="比較チャート",
            FontAttributes=FontAttributes.Bold,
            FontSize=13,
            VerticalTextAlignment=TextAlignment.Center,
            TextColor=Color.FromArgb("#0F172A"),
        },1,0);

        var panel = new Grid{
            RowDefinitions = {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star },
            },
            BackgroundColor=Colors.White,
            VerticalOptions=LayoutOptions.Fill,
        };
        panel.Add(header,0,0);
        panel.Add(this.CreateChartView(),0,1);
        return panel;
    }

    private View CreateCollapsibleSection(
        string title,
        Func<bool> getExpanded,
        Action<bool> setExpanded,
        View content,
        View? trailingContent){
        var icon = new Label{
            WidthRequest=18,
            FontSize=12,
            FontAttributes=FontAttributes.Bold,
            TextColor=Color.FromArgb("#0F172A"),
            HorizontalTextAlignment=TextAlignment.Center,
            VerticalTextAlignment=TextAlignment.Center,
        };
        var titleLabel = new Label{
            Text=title,
            FontSize=12,
            FontAttributes=FontAttributes.Bold,
            TextColor=Color.FromArgb("#0F172A"),
            VerticalTextAlignment=TextAlignment.Center,
            LineBreakMode=LineBreakMode.TailTruncation,
        };
        var toggleArea = new Grid{
            ColumnDefinitions = {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
            },
            ColumnSpacing=2,
            BackgroundColor=Colors.Transparent,
        };
        toggleArea.Add(icon,0,0);
        toggleArea.Add(titleLabel,1,0);
        var header = new Grid{
            ColumnDefinitions = {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto },
            },
            ColumnSpacing=8,
            Padding=new Thickness(6,3),
            BackgroundColor=Color.FromArgb("#F1F5F9"),
        };
        header.Add(toggleArea,0,0);
        if(trailingContent is not null){
            header.Add(trailingContent,1,0);
        }

        var border = new Border{
            Stroke=Color.FromArgb("#CBD5E1"),
            StrokeThickness=1,
            BackgroundColor=Color.FromArgb("#F8FAFC"),
            Content=header,
        };
        var layout = new Grid{
            RowDefinitions = {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
            },
            RowSpacing=3,
            Children = {
                border,
                content,
            },
        };
        Grid.SetRow(content,1);

        void Apply(){
            var expanded=getExpanded();
            icon.Text=expanded ? "v" : ">";
            content.IsVisible=expanded;
        }

        var tap = new TapGestureRecognizer();
        tap.Tapped+=(_,_) => {
            setExpanded(!getExpanded());
            foreach(var refresh in this.collapsibleSectionRefreshers){
                refresh();
            }
        };
        toggleArea.GestureRecognizers.Add(tap);
        this.collapsibleSectionRefreshers.Add(Apply);
        Apply();
        return layout;
    }

    private View CreateMetricRadioGroup(){
        var group = new HorizontalStackLayout{
            Spacing=8,
            VerticalOptions=LayoutOptions.Center,
        };
        group.Children.Add(this.CreateMetricRadioButton("処理時間",nameof(BenchmarkRunnerViewModel.IsTimeMetricSelected)));
        group.Children.Add(this.CreateMetricRadioButton("メモリ",nameof(BenchmarkRunnerViewModel.IsAllocatedMetricSelected)));
        return group;
    }

    private RadioButton CreateMetricRadioButton(string text,string isCheckedPath){
        var radio = new RadioButton{
            Content=text,
            GroupName="ChartMetric",
            Margin=new Thickness(0),
            FontSize=12,
        };
        radio.SetBinding(RadioButton.IsCheckedProperty,isCheckedPath,BindingMode.TwoWay);
        return radio;
    }

    private View CreateBenchmarkGrid(){
        StartupDiagnostics.WriteSyncfusionAssemblies("CreateBenchmarkGrid before SfDataGrid");
        var grid = new SfDataGrid{
            AutoGenerateColumnsMode=AutoGenerateColumnsMode.None,
            RowHeight=36,
            HeaderRowHeight=30,
            AllowTriStateSorting=false,
            SortingMode=DataGridSortingMode.None,
            SelectionMode=DataGridSelectionMode.None,
            HorizontalScrollBarVisibility=ScrollBarVisibility.Always,
            VerticalScrollBarVisibility=ScrollBarVisibility.Always,
            DefaultColumnWidth=92,
        };
        grid.QueryRowHeight+=(_,args) => {
            if(args.RowIndex<2){
                args.Height=30;
            }
        };
        grid.SetBinding(SfDataGrid.ItemsSourceProperty,nameof(BenchmarkRunnerViewModel.BenchmarkRows));
        this.ConfigureDataGridScrollBars(grid);

        var categoryHeaderRow = new DataGridStackedHeaderRow();
        var projectHeaderRow = new DataGridStackedHeaderRow();
        grid.StackedHeaderRows.Add(categoryHeaderRow);
        grid.StackedHeaderRows.Add(projectHeaderRow);

        grid.Columns.Add(this.CreateCheckColumn());
        grid.Columns.Add(this.CreateTextColumn("Benchmark",nameof(BenchmarkTableRow.BenchmarkName),BenchmarkGridSortColumn.BenchmarkName,240,TextAlignment.Start));
        this.AddBenchmarkBaseStackedHeaders(categoryHeaderRow,projectHeaderRow);
        var leftMappings = this.AddProjectColumns(grid,"Left",nameof(BenchmarkTableRow.LeftCell),BenchmarkGridSortColumn.LeftTime,BenchmarkGridSortColumn.LeftAllocated);
        var stopMapping = this.AddPairStopNColumn(
            grid,
            "StopN",
            nameof(BenchmarkTableRow.StopNText),
            nameof(BenchmarkTableRow.StopNTextColor),
            nameof(BenchmarkTableRow.StopNFontAttributes));
        var rightMappings = this.AddProjectColumns(grid,"Right",nameof(BenchmarkTableRow.RightCell),BenchmarkGridSortColumn.RightTime,BenchmarkGridSortColumn.RightAllocated);
        IReadOnlyList<string>[] mappings = [leftMappings,[stopMapping],rightMappings];
        this.AddBenchmarkSelectionGroup(categoryHeaderRow,this.GetBenchmarkSelectionGroup("Active"),mappings);
        this.AddBenchmarkProjectSelectionGroup(projectHeaderRow,this.GetBenchmarkProjectSelectionGroup("Left","Left"),leftMappings);
        this.AddBenchmarkStopNHeader(projectHeaderRow,stopMapping);
        this.AddBenchmarkProjectSelectionGroup(projectHeaderRow,this.GetBenchmarkProjectSelectionGroup("Right","Right"),rightMappings);
        return grid;
    }

    private DataGridTemplateColumn CreateCheckColumn(){
        return new DataGridTemplateColumn{
            MappingName=nameof(BenchmarkTableRow.IsSelectionChecked),
            HeaderText="",
            Width=BenchmarkSelectionColumnWidth,
            AllowSorting=false,
            HeaderTemplate=new DataTemplate(() => new BoxView{
                BackgroundColor=Colors.Transparent,
            }),
            CellTemplate=new DataTemplate(() => {
                var layout = new Grid{
                    ColumnDefinitions = {
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
                var checkHost = new Grid{
                    HorizontalOptions=LayoutOptions.Fill,
                    VerticalOptions=LayoutOptions.Fill,
                };
                checkHost.Add(check,0,0);
                var checkTap = new TapGestureRecognizer();
                checkTap.Tapped+=(_,_) => {
                    if(this.IsBenchmarkSelectionLockedForCurrentTab()) {
                        return;
                    }

                    if(layout.BindingContext is BenchmarkTableRow row){
                        this.viewModel?.ToggleBenchmarkRow(row);
                    }
                };
                checkHost.GestureRecognizers.Add(checkTap);
                var selectionHost = new Grid{
                    BackgroundColor=Colors.Transparent,
                    HorizontalOptions=LayoutOptions.Fill,
                    VerticalOptions=LayoutOptions.Fill,
                };
                var selectionTap = new TapGestureRecognizer();
                selectionTap.Tapped+=(_,_) => {
                    if(layout.BindingContext is BenchmarkTableRow row){
                        this.viewModel?.SelectBenchmarkRow(row,CreateBenchmarkSelectionGesture());
                    }
                };
                selectionHost.GestureRecognizers.Add(selectionTap);
                this.AddBenchmarkRowUiSelectionBackground(layout);
                layout.Add(checkHost,0,0);
                layout.Add(selectionHost,1,0);
                return layout;
            }),
        };
    }

    private void AddBenchmarkBaseStackedHeaders(DataGridStackedHeaderRow categoryHeaderRow,DataGridStackedHeaderRow projectHeaderRow){
        categoryHeaderRow.Columns.Add(new DataGridStackedColumn{
            MappingName="AllBenchmarkSelection",
            ColumnMappingNames=nameof(BenchmarkTableRow.IsSelectionChecked),
            Template=this.CreateAllBenchmarkSelectionHeaderTemplate(),
        });
        categoryHeaderRow.Columns.Add(new DataGridStackedColumn{
            MappingName="BenchmarkNameHeader",
            ColumnMappingNames=nameof(BenchmarkTableRow.BenchmarkName),
            Text=string.Empty,
        });
        projectHeaderRow.Columns.Add(new DataGridStackedColumn{
            MappingName="AllBenchmarkSelectionProjectSpacer",
            ColumnMappingNames=nameof(BenchmarkTableRow.IsSelectionChecked),
            Text=string.Empty,
        });
        projectHeaderRow.Columns.Add(new DataGridStackedColumn{
            MappingName="BenchmarkNameProjectSpacer",
            ColumnMappingNames=nameof(BenchmarkTableRow.BenchmarkName),
            Text=string.Empty,
        });
    }

    private IReadOnlyList<string> AddProjectColumns(
        SfDataGrid grid,
        string header,
        string cellPath,
        BenchmarkGridSortColumn timeSortColumn,
        BenchmarkGridSortColumn allocatedSortColumn){
        var checkMapping = cellPath+"Check";
        var timeMapping = cellPath+"Time";
        var allocatedMapping = cellPath+"Allocated";
        grid.Columns.Add(this.CreateProjectCheckColumn(checkMapping,cellPath));
        grid.Columns.Add(this.CreateProjectTextColumn("Time",timeMapping,cellPath,cellPath+"."+nameof(BenchmarkProjectCell.TimeText),timeSortColumn,BenchmarkProjectMetricColumnWidth,TextAlignment.End));
        grid.Columns.Add(this.CreateProjectTextColumn("Allocated",allocatedMapping,cellPath,cellPath+"."+nameof(BenchmarkProjectCell.AllocatedText),allocatedSortColumn,BenchmarkProjectMetricColumnWidth,TextAlignment.End));
        return [checkMapping,timeMapping,allocatedMapping];
    }

    private string AddPairStopNColumn(SfDataGrid grid,string mappingName,string bindingPath,string textColorPath,string fontAttributesPath){
        grid.Columns.Add(this.CreatePairStopNColumn(mappingName,bindingPath,textColorPath,fontAttributesPath));
        return mappingName;
    }

    private string GetProjectFromCellPath(string cellPath){
        return cellPath switch{
            nameof(BenchmarkTableRow.LeftCell) => this.viewModel?.SelectedSourceDefinition.LeftProject??cellPath,
            nameof(BenchmarkTableRow.RightCell) => this.viewModel?.SelectedSourceDefinition.RightProject??cellPath,
            _ => cellPath,
        };
    }

    private static BenchmarkCellSelectionGesture CreateBenchmarkSelectionGesture(){
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

    private bool IsBenchmarkSelectionLockedForCurrentTab() {
        return this.viewModel is {
            IsBenchmarkRunActive: true,
            ActiveRunSourceTab: not null,
        } vm && vm.ActiveRunSourceTab==vm.SelectedSourceDefinition.Tab;
    }

    private void AddBenchmarkSelectionGroup(DataGridStackedHeaderRow headerRow,BenchmarkColumnSelectionGroup group,IEnumerable<IReadOnlyList<string>> childMappings){
        var mappings = childMappings.SelectMany(static item => item).ToArray();
        headerRow.Columns.Add(new DataGridStackedColumn{
            MappingName="BenchmarkGroup"+group.Id,
            ColumnMappingNames=string.Join(",",mappings),
            Template=this.CreateBenchmarkColumnGroupHeaderTemplate(group),
        });
    }

    private void AddBenchmarkProjectSelectionGroup(DataGridStackedHeaderRow headerRow,BenchmarkColumnSelectionGroup group,IReadOnlyList<string> childMappings){
        headerRow.Columns.Add(new DataGridStackedColumn{
            MappingName="BenchmarkProjectGroup"+group.Id,
            ColumnMappingNames=string.Join(",",childMappings),
            Template=this.CreateBenchmarkColumnGroupHeaderTemplate(group),
        });
    }

    private void AddBenchmarkStopNHeader(DataGridStackedHeaderRow headerRow,string mappingName){
        headerRow.Columns.Add(new DataGridStackedColumn{
            MappingName="BenchmarkStopN"+mappingName,
            ColumnMappingNames=mappingName,
            Text=string.Empty,
        });
    }

    private BenchmarkColumnSelectionGroup GetBenchmarkSelectionGroup(string id){
        return this.viewModel?.BenchmarkColumnSelectionGroups.FirstOrDefault(group => group.Id==id)
            ??new BenchmarkColumnSelectionGroup(id,id,[]);
    }

    private BenchmarkColumnSelectionGroup GetBenchmarkProjectSelectionGroup(string id,string headerText){
        return this.viewModel?.BenchmarkProjectSelectionGroups.FirstOrDefault(group => group.Id==id)
            ??new BenchmarkColumnSelectionGroup(id,headerText,[]);
    }

    private DataTemplate CreateAllBenchmarkSelectionHeaderTemplate(){
        return new DataTemplate(() => {
            var layout = this.CreateBenchmarkHeaderCheckLayout(string.Empty);
            var check = this.CreateBenchmarkAggregateCheckBox();
            if(this.viewModel is not null){
                this.BindAggregateCheckBox(check,this.viewModel,nameof(BenchmarkRunnerViewModel.IsAllBenchmarkSelectionChecked),() => this.viewModel.IsAllBenchmarkSelectionChecked);
            }
            var checkHost = this.CreateBenchmarkHeaderCheckHost(check);
            var checkTap = new TapGestureRecognizer();
            checkTap.Tapped+=(_,_) => {
                if(this.IsBenchmarkSelectionLockedForCurrentTab()) {
                    return;
                }

                this.viewModel?.ToggleAllBenchmarkSelection();
            };
            checkHost.GestureRecognizers.Add(checkTap);
            var body = this.CreateBenchmarkHeaderBody(string.Empty);
            var bodyTap = new TapGestureRecognizer();
            bodyTap.Tapped+=(_,_) => this.viewModel?.SelectAllBenchmarkUiCells();
            body.GestureRecognizers.Add(bodyTap);
            layout.Add(checkHost,0,0);
            layout.Add(body,1,0);
            return layout;
        });
    }

    private DataTemplate CreateBenchmarkColumnGroupHeaderTemplate(BenchmarkColumnSelectionGroup group){
        return new DataTemplate(() => {
            var layout = this.CreateBenchmarkHeaderCheckLayout(group.HeaderText);
            var check = this.CreateBenchmarkAggregateCheckBox();
            this.BindAggregateCheckBox(check,group,nameof(BenchmarkColumnSelectionGroup.IsChecked),() => group.IsChecked);
            var checkHost = this.CreateBenchmarkHeaderCheckHost(check);
            var checkTap = new TapGestureRecognizer();
            checkTap.Tapped+=(_,_) => {
                if(this.IsBenchmarkSelectionLockedForCurrentTab()) {
                    return;
                }

                this.viewModel?.ToggleBenchmarkColumnGroup(group);
            };
            checkHost.GestureRecognizers.Add(checkTap);
            var body = this.CreateBenchmarkHeaderBody(nameof(BenchmarkColumnSelectionGroup.HeaderText));
            body.BindingContext=group;
            var bodyTap = new TapGestureRecognizer();
            bodyTap.Tapped+=(_,_) => this.viewModel?.SelectBenchmarkColumnGroup(group,CreateBenchmarkSelectionGesture());
            body.GestureRecognizers.Add(bodyTap);
            layout.Add(checkHost,0,0);
            layout.Add(body,1,0);
            return layout;
        });
    }

    private Grid CreateBenchmarkHeaderCheckLayout(string text){
        var layout = new Grid{
            ColumnDefinitions = {
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

    private Grid CreateBenchmarkHeaderCheckHost(SfCheckBox check){
        var host = new Grid{
            BackgroundColor=Colors.Transparent,
            HorizontalOptions=LayoutOptions.Fill,
            VerticalOptions=LayoutOptions.Fill,
        };
        host.Add(check,0,0);
        return host;
    }

    private Grid CreateBenchmarkHeaderBody(string text){
            var body = new Grid{
            BackgroundColor=Colors.Transparent,
            HorizontalOptions=LayoutOptions.Fill,
            VerticalOptions=LayoutOptions.Fill,
        };
        if(!string.IsNullOrWhiteSpace(text)){
            var label = new Label{
                FontSize=12,
                FontAttributes=FontAttributes.Bold,
                TextColor=Color.FromArgb("#0F172A"),
                InputTransparent=true,
                HorizontalTextAlignment=TextAlignment.Start,
                VerticalTextAlignment=TextAlignment.Center,
                LineBreakMode=LineBreakMode.TailTruncation,
            };
            if(text==nameof(BenchmarkColumnSelectionGroup.HeaderText)){
                label.SetBinding(Label.TextProperty,nameof(BenchmarkColumnSelectionGroup.HeaderText));
            }else{
                label.Text=text;
            }
            body.Add(label,0,0);
        }
        return body;
    }

    private SfCheckBox CreateBenchmarkAggregateCheckBox(){
        var check = this.CreateSfCheckBox(BenchmarkAggregateCheckBoxSize,BenchmarkAggregateCheckBoxSize,true);
        check.InputTransparent=true;
        return check;
    }

    private void BindAggregateCheckBox(SfCheckBox check,INotifyPropertyChanged source,string propertyName,Func<bool?> getValue){
        check.IsChecked=getValue();
        check.SetBinding(SfCheckBox.IsCheckedProperty,new Binding(propertyName,BindingMode.OneWay,source:source));
        PropertyChangedEventHandler handler = (_,args) => {
            if(args.PropertyName==propertyName){
                MainThread.BeginInvokeOnMainThread(() => {
                    check.IsChecked=getValue();
                });
            }
        };
        source.PropertyChanged+=handler;
        check.Unloaded+=(_,_) => source.PropertyChanged-=handler;
    }

    private DataGridTemplateColumn CreateProjectCheckColumn(string mappingName,string cellPath){
        return new DataGridTemplateColumn{
            MappingName=mappingName,
            HeaderText="論理コア",
            Width=BenchmarkProjectStatusColumnWidth,
            AllowSorting=false,
            HeaderTemplate=new DataTemplate(() => {
                var header = new Grid{
                    ColumnDefinitions = {
                        new ColumnDefinition { Width = new GridLength(BenchmarkCellCheckSlotWidth) },
                        new ColumnDefinition { Width = new GridLength(18) },
                        new ColumnDefinition { Width = new GridLength(24) },
                        new ColumnDefinition { Width = new GridLength(26) },
                        new ColumnDefinition { Width = GridLength.Star },
                    },
                    ColumnSpacing=2,
                    Padding=new Thickness(1,0),
                    BackgroundColor=Color.FromArgb("#F1F5F9"),
                };
                var logicalCore = new Label{
                    Text="論理コア",
                    FontSize=10,
                    FontAttributes=FontAttributes.Bold,
                    TextColor=Color.FromArgb("#0F172A"),
                    HorizontalTextAlignment=TextAlignment.Center,
                    VerticalTextAlignment=TextAlignment.Center,
                    LineBreakMode=LineBreakMode.TailTruncation,
                };
                var measuredAt = new Label{
                    Text="日時",
                    FontSize=10,
                    FontAttributes=FontAttributes.Bold,
                    TextColor=Color.FromArgb("#0F172A"),
                    HorizontalTextAlignment=TextAlignment.Start,
                    VerticalTextAlignment=TextAlignment.Center,
                    LineBreakMode=LineBreakMode.TailTruncation,
                    Padding=new Thickness(2,0),
                };
                header.Add(logicalCore,2,0);
                header.SetColumnSpan(logicalCore,2);
                header.Add(measuredAt,4,0);
                return header;
            }),
            CellTemplate=new DataTemplate(() => {
                var layout = new Grid{
                    RowDefinitions = {
                        new RowDefinition { Height = new GridLength(2) },
                        new RowDefinition { Height = GridLength.Star },
                        new RowDefinition { Height = new GridLength(2) },
                    },
                    ColumnDefinitions = {
                        new ColumnDefinition { Width = new GridLength(2) },
                        new ColumnDefinition { Width = GridLength.Star },
                        new ColumnDefinition { Width = new GridLength(2) },
                    },
                    BackgroundColor=Colors.Transparent,
                    VerticalOptions=LayoutOptions.Fill,
                    HorizontalOptions=LayoutOptions.Fill,
                };
                this.AddBenchmarkUiSelectionBackground(layout,cellPath);
                var content = new Grid{
                    ColumnDefinitions = {
                        new ColumnDefinition { Width = new GridLength(BenchmarkCellCheckSlotWidth) },
                        new ColumnDefinition { Width = new GridLength(18) },
                        new ColumnDefinition { Width = new GridLength(24) },
                        new ColumnDefinition { Width = new GridLength(26) },
                        new ColumnDefinition { Width = GridLength.Star },
                    },
                    ColumnSpacing=2,
                    Padding=new Thickness(1,0),
                    VerticalOptions=LayoutOptions.Center,
                };
                var check = this.CreateSfCheckBox(BenchmarkCellCheckBoxWidth,BenchmarkCellCheckBoxHeight);
                check.InputTransparent=true;
                check.SetBinding(SfCheckBox.IsCheckedProperty,new Binding(cellPath+"."+nameof(BenchmarkProjectCell.IsSelected),BindingMode.OneWay));
                check.SetBinding(IsEnabledProperty,cellPath+"."+nameof(BenchmarkProjectCell.HasCase));
                var checkHost = new Grid{
                    BackgroundColor=Colors.Transparent,
                    HorizontalOptions=LayoutOptions.Fill,
                    VerticalOptions=LayoutOptions.Fill,
                };
                checkHost.Add(check,0,0);
                var checkTap = new TapGestureRecognizer();
                checkTap.Tapped+=(_,_) => {
                    if(this.IsBenchmarkSelectionLockedForCurrentTab()) {
                        return;
                    }

                    if(layout.BindingContext is BenchmarkTableRow row){
                        this.viewModel?.ToggleBenchmarkCellCheck(row,this.GetProjectFromCellPath(cellPath));
                    }
                };
                checkHost.GestureRecognizers.Add(checkTap);
                var spinner = new ActivityIndicator{
                    WidthRequest=14,
                    HeightRequest=14,
                    Color=Color.FromArgb("#2563EB"),
                    VerticalOptions=LayoutOptions.Center,
                    HorizontalOptions=LayoutOptions.Center,
                };
                spinner.SetBinding(ActivityIndicator.IsRunningProperty,new Binding(cellPath+"."+nameof(BenchmarkProjectCell.IsRunningVisual)));
                spinner.SetBinding(IsVisibleProperty,new Binding(cellPath+"."+nameof(BenchmarkProjectCell.IsRunningVisual)));
                var statusIcon = new Label{
                    FontSize=13,
                    TextColor=Color.FromArgb("#0F172A"),
                    InputTransparent=true,
                    HorizontalTextAlignment=TextAlignment.Center,
                    VerticalTextAlignment=TextAlignment.Center,
                    LineBreakMode=LineBreakMode.TailTruncation,
                };
                statusIcon.SetBinding(Label.TextProperty,cellPath+"."+nameof(BenchmarkProjectCell.StatusIcon));
                statusIcon.SetBinding(IsVisibleProperty,cellPath+"."+nameof(BenchmarkProjectCell.IsStatusIconVisible));
                var badge = this.CreateCoreBadge();
                badge.SetBinding(IsVisibleProperty,cellPath+"."+nameof(BenchmarkProjectCell.HasCpuDisplay));
                badge.SetBinding(BackgroundColorProperty,cellPath+"."+nameof(BenchmarkProjectCell.BadgeBackgroundColor));
                if(badge.Content is Label badgeLabel){
                    badgeLabel.SetBinding(Label.TextProperty,cellPath+"."+nameof(BenchmarkProjectCell.CoreKindLabel));
                    badgeLabel.SetBinding(Label.TextColorProperty,cellPath+"."+nameof(BenchmarkProjectCell.BadgeTextColor));
                }
                var cpuLabel = new Label{
                    FontSize=10,
                    TextColor=Color.FromArgb("#0F172A"),
                    InputTransparent=true,
                    LineBreakMode=LineBreakMode.TailTruncation,
                    VerticalTextAlignment=TextAlignment.Center,
                };
                cpuLabel.SetBinding(IsVisibleProperty,cellPath+"."+nameof(BenchmarkProjectCell.HasCpuDisplay));
                cpuLabel.SetBinding(Label.TextProperty,cellPath+"."+nameof(BenchmarkProjectCell.LogicalProcessorText));
                cpuLabel.SetBinding(ToolTipProperties.TextProperty,cellPath+"."+nameof(BenchmarkProjectCell.LogicalProcessorText));
                var detail = new Label{
                    FontSize=9,
                    TextColor=Color.FromArgb("#334155"),
                    InputTransparent=true,
                    LineBreakMode=LineBreakMode.TailTruncation,
                    VerticalTextAlignment=TextAlignment.Center,
                };
                detail.SetBinding(Label.TextProperty,cellPath+"."+nameof(BenchmarkProjectCell.ExecutionText));
                detail.SetBinding(Label.TextColorProperty,cellPath+"."+nameof(BenchmarkProjectCell.ExecutionTextColor));
                detail.SetBinding(Label.FontAttributesProperty,cellPath+"."+nameof(BenchmarkProjectCell.ExecutionFontAttributes));
                var failureDetail = new Label{
                    FontSize=9,
                    TextColor=Color.FromArgb("#DC2626"),
                    InputTransparent=true,
                    LineBreakMode=LineBreakMode.TailTruncation,
                    VerticalTextAlignment=TextAlignment.Center,
                };
                failureDetail.SetBinding(Label.TextProperty,cellPath+"."+nameof(BenchmarkProjectCell.FailureExecutionText));
                failureDetail.SetBinding(Label.TextColorProperty,cellPath+"."+nameof(BenchmarkProjectCell.FailureExecutionTextColor));
                var detailHost = new Grid{
                    ColumnDefinitions = {
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
                    if(layout.BindingContext is BenchmarkTableRow row){
                        this.viewModel?.SelectBenchmarkCell(row,this.GetProjectFromCellPath(cellPath),CreateBenchmarkSelectionGesture());
                    }
                };
                detailHost.GestureRecognizers.Add(detailTap);
                content.Add(checkHost,0,0);
                content.Add(statusIcon,1,0);
                content.Add(spinner,1,0);
                content.Add(badge,2,0);
                content.Add(cpuLabel,3,0);
                content.Add(detailHost,4,0);
                layout.Add(content,1,1);
                return layout;
            }),
        };
    }

    private DataGridTemplateColumn CreatePairStopNColumn(string mappingName,string bindingPath,string textColorPath,string fontAttributesPath){
        return new DataGridTemplateColumn{
            MappingName=mappingName,
            HeaderText="要素数",
            Width=BenchmarkPairStopNColumnWidth,
            AllowSorting=false,
            HeaderTemplate=new DataTemplate(() => new Label{
                Text="要素数",
                FontSize=10,
                FontAttributes=FontAttributes.Bold,
                TextColor=Color.FromArgb("#0F172A"),
                BackgroundColor=Color.FromArgb("#F1F5F9"),
                HorizontalTextAlignment=TextAlignment.Center,
                VerticalTextAlignment=TextAlignment.Center,
                Padding=new Thickness(2,0),
            }),
            CellTemplate=new DataTemplate(() => {
                var grid = new Grid{
                    BackgroundColor=Colors.Transparent,
                    HorizontalOptions=LayoutOptions.Fill,
                    VerticalOptions=LayoutOptions.Fill,
                };
                var label = new Label{
                    FontSize=9,
                    InputTransparent=true,
                    LineBreakMode=LineBreakMode.TailTruncation,
                    HorizontalTextAlignment=TextAlignment.Center,
                    VerticalTextAlignment=TextAlignment.Center,
                    Margin=new Thickness(0),
                    Padding=new Thickness(2,0),
                };
                label.SetBinding(Label.TextProperty,bindingPath);
                label.SetBinding(Label.TextColorProperty,textColorPath);
                label.SetBinding(Label.FontAttributesProperty,fontAttributesPath);
                label.SetBinding(ToolTipProperties.TextProperty,bindingPath);
                grid.SetBinding(ToolTipProperties.TextProperty,bindingPath);
                grid.Add(label,0,0);
                return grid;
            }),
        };
    }

    private DataGridTemplateColumn CreateProjectTextColumn(string header,string mappingName,string cellPath,string bindingPath,BenchmarkGridSortColumn sortColumn,double width,TextAlignment alignment){
        return new DataGridTemplateColumn{
            MappingName=mappingName,
            HeaderText=header,
            Width=width,
            AllowSorting=false,
            HeaderTemplate=this.CreateHeaderTemplate(header,sortColumn),
            CellTemplate=new DataTemplate(() => {
                var grid = new Grid{
                    RowDefinitions = {
                        new RowDefinition { Height = new GridLength(2) },
                        new RowDefinition { Height = GridLength.Star },
                        new RowDefinition { Height = new GridLength(2) },
                    },
                    ColumnDefinitions = {
                        new ColumnDefinition { Width = new GridLength(2) },
                        new ColumnDefinition { Width = GridLength.Star },
                        new ColumnDefinition { Width = new GridLength(2) },
                    },
                    BackgroundColor=Colors.Transparent,
                    VerticalOptions=LayoutOptions.Fill,
                    HorizontalOptions=LayoutOptions.Fill,
                };
                this.AddBenchmarkUiSelectionBackground(grid,cellPath);
                var runningBackground = new BoxView{
                    BackgroundColor=Color.FromArgb("#DBEAFE"),
                    Opacity=0.85,
                    InputTransparent=true,
                };
                runningBackground.SetBinding(IsVisibleProperty,cellPath+"."+nameof(BenchmarkProjectCell.IsRunningVisual));
                var label = new Label{
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
                grid.Add(label,0,0);
                Grid.SetColumnSpan(label,3);
                Grid.SetRowSpan(label,3);
                var tap = new TapGestureRecognizer();
                tap.Tapped+=(_,_) => {
                    if(grid.BindingContext is BenchmarkTableRow row){
                        this.viewModel?.SelectBenchmarkCell(row,this.GetProjectFromCellPath(cellPath),CreateBenchmarkSelectionGesture());
                    }
                };
                grid.GestureRecognizers.Add(tap);
                return grid;
            }),
        };
    }

    private void AddBenchmarkUiSelectionBackground(Grid grid,string cellPath){
        var background = new BoxView{
            BackgroundColor=Color.FromArgb("#E9D5FF"),
            Opacity=0.75,
            InputTransparent=true,
        };
        background.SetBinding(IsVisibleProperty,cellPath+"."+nameof(BenchmarkProjectCell.IsUiSelected));
        grid.Add(background,0,0);
        Grid.SetColumnSpan(background,3);
        Grid.SetRowSpan(background,3);
    }

    private void AddBenchmarkRowUiSelectionBackground(Grid grid){
        var background = new BoxView{
            BackgroundColor=Color.FromArgb("#E9D5FF"),
            Opacity=0.75,
            InputTransparent=true,
        };
        background.SetBinding(IsVisibleProperty,nameof(BenchmarkTableRow.IsUiSelected));
        grid.Add(background,0,0);
        Grid.SetColumnSpan(background,Math.Max(1,grid.ColumnDefinitions.Count));
        Grid.SetRowSpan(background,Math.Max(1,grid.RowDefinitions.Count));
    }

    private DataGridTemplateColumn CreateTextColumn(string header,string property,BenchmarkGridSortColumn sortColumn,double width,TextAlignment alignment){
        return new DataGridTemplateColumn{
            MappingName=property,
            HeaderText=header,
            Width=width,
            AllowSorting=false,
            HeaderTemplate=this.CreateHeaderTemplate(header,sortColumn),
            CellTemplate=new DataTemplate(() => {
                var grid = new Grid{
                    BackgroundColor=Colors.Transparent,
                    HorizontalOptions=LayoutOptions.Fill,
                    VerticalOptions=LayoutOptions.Fill,
                };
                var label = new Label{
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
                if(property==nameof(BenchmarkTableRow.BenchmarkName)){
                    this.AddBenchmarkRowUiSelectionBackground(grid);
                    var tap = new TapGestureRecognizer();
                    tap.Tapped+=(_,_) => {
                        if(grid.BindingContext is BenchmarkTableRow row){
                            this.viewModel?.SelectBenchmarkRow(row,CreateBenchmarkSelectionGesture());
                        }
                    };
                    grid.GestureRecognizers.Add(tap);
                }
                grid.Add(label,0,0);
                return grid;
            }),
        };
    }

    private DataTemplate CreateHeaderTemplate(string text,BenchmarkGridSortColumn sortColumn){
        return new DataTemplate(() => {
            var label = new Label{
                Text=text,
                FontSize=10,
                FontAttributes=FontAttributes.Bold,
                TextColor=Color.FromArgb("#0F172A"),
                BackgroundColor=Color.FromArgb("#F1F5F9"),
                HorizontalTextAlignment=TextAlignment.Center,
                VerticalTextAlignment=TextAlignment.Center,
                LineBreakMode=LineBreakMode.TailTruncation,
                Padding=new Thickness(2,0),
            };
            var tap = new TapGestureRecognizer();
            tap.Tapped+=(_,_) => this.viewModel?.SortBenchmarkRows(sortColumn);
            label.GestureRecognizers.Add(tap);
            return label;
        });
    }

    private View CreateCpuView(){
        var performanceList = this.CreateCpuSectionList(nameof(BenchmarkRunnerViewModel.PerformanceCpuSections));
        var efficiencyList = this.CreateCpuSectionList(nameof(BenchmarkRunnerViewModel.EfficiencyCpuSections));
        var unknownList = this.CreateCpuSectionList(nameof(BenchmarkRunnerViewModel.UnknownCpuSections));

        var layout = new Grid{
            RowDefinitions = {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
            },
            ColumnDefinitions = {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star },
            },
            RowSpacing=10,
            ColumnSpacing=10,
            Children = {
                performanceList,
                efficiencyList,
                unknownList,
            },
        };
        Grid.SetColumn(efficiencyList,1);
        Grid.SetRow(unknownList,1);
        Grid.SetColumnSpan(unknownList,2);
        return layout;
    }

    private View CreateLogView(){
        var logList = new CollectionView{
            SelectionMode=SelectionMode.None,
            VerticalOptions=LayoutOptions.Fill,
            HorizontalOptions=LayoutOptions.Fill,
            ItemTemplate=new DataTemplate(() => {
                var label = new Label{
                    FontSize=11,
                    TextColor=Color.FromArgb("#0F172A"),
                    LineBreakMode=LineBreakMode.WordWrap,
                    Margin=new Thickness(0),
                    Padding=new Thickness(0,2),
                };
                label.SetBinding(Label.TextProperty,".");
                return label;
            }),
            ItemsLayout=new LinearItemsLayout(ItemsLayoutOrientation.Vertical){
                ItemSpacing=0,
            },
        };
        logList.SetBinding(ItemsView.ItemsSourceProperty,nameof(BenchmarkRunnerViewModel.LogEntries));
        if(this.viewModel is not null){
            this.viewModel.LogEntries.CollectionChanged+=(_,args) => {
                if(args.Action is NotifyCollectionChangedAction.Add or NotifyCollectionChangedAction.Reset){
                    MainThread.BeginInvokeOnMainThread(() => {
                        if(this.viewModel?.LogEntries.Count>0){
                            logList.ScrollTo(this.viewModel.LogEntries.Count-1,position:ScrollToPosition.End,animate:false);
                        }
                    });
                }
            };
        }

        return new Border{
            Stroke=Color.FromArgb("#CBD5E1"),
            StrokeThickness=1,
            BackgroundColor=Color.FromArgb("#F8FAFC"),
            Padding=new Thickness(8),
            Content=logList,
        };
    }

    private VerticalStackLayout CreateCpuSectionList(string itemsSourcePath){
        var layout = new VerticalStackLayout{
            Spacing=10,
        };
        BindableLayout.SetItemTemplate(layout,new DataTemplate(this.CreateCpuSectionView));
        layout.SetBinding(BindableLayout.ItemsSourceProperty,itemsSourcePath);
        return layout;
    }

    private View CreateCpuSectionView(){
        var header = new Border{
            Stroke=Color.FromArgb("#CBD5E1"),
            StrokeThickness=1,
            BackgroundColor=Color.FromArgb("#F8FAFC"),
            Padding=new Thickness(6,3),
        };
        var headerGrid = new Grid{
            ColumnDefinitions = {
                new ColumnDefinition { Width = new GridLength(CpuLogicalCheckColumnWidth) },
                new ColumnDefinition { Width = new GridLength(24) },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = new GridLength(24) },
            },
            ColumnSpacing=4,
        };
        var sectionCheck = this.CreateCpuCheckBox();
        sectionCheck.InputTransparent=true;
        sectionCheck.SetBinding(SfCheckBox.IsCheckedProperty,new Binding(nameof(CpuTableSection.State),BindingMode.OneWay,converter:new CpuSelectionStateToBoolConverter()));
        var badge = this.CreateCoreBadge();
        badge.SetBinding(BackgroundColorProperty,nameof(CpuTableSection.BadgeBackgroundColor));
        if(badge.Content is Label badgeLabel){
            badgeLabel.SetBinding(Label.TextProperty,nameof(CpuTableSection.BadgeText));
            badgeLabel.SetBinding(Label.TextColorProperty,nameof(CpuTableSection.BadgeTextColor));
        }
        var title = new Label{
            FontSize=12,
            FontAttributes=FontAttributes.Bold,
            TextColor=Color.FromArgb("#0F172A"),
            VerticalTextAlignment=TextAlignment.Center,
        };
        title.SetBinding(Label.TextProperty,nameof(CpuTableSection.Title));
        var state = new Label{
            FontSize=12,
            FontAttributes=FontAttributes.Bold,
            TextColor=Color.FromArgb("#0F766E"),
            HorizontalTextAlignment=TextAlignment.Center,
            VerticalTextAlignment=TextAlignment.Center,
        };
        state.SetBinding(Label.TextProperty,nameof(CpuTableSection.StateText));
        headerGrid.Add(sectionCheck,0,0);
        headerGrid.Add(badge,1,0);
        headerGrid.Add(title,2,0);
        headerGrid.Add(state,3,0);
        header.Content=headerGrid;
        var headerTap = new TapGestureRecognizer();
        headerTap.Tapped+=(_,_) => {
            if(header.BindingContext is CpuTableSection section){
                this.viewModel?.ToggleCpuSection(section);
            }
        };
        header.GestureRecognizers.Add(headerTap);

        StartupDiagnostics.WriteSyncfusionAssemblies("CreateCpuSectionView before SfDataGrid");
        var grid = new SfDataGrid{
            AutoGenerateColumnsMode=AutoGenerateColumnsMode.None,
            RowHeight=24,
            HeaderRowHeight=30,
            AllowTriStateSorting=false,
            SortingMode=DataGridSortingMode.None,
            DefaultColumnWidth=56,
        };
        grid.SetBinding(SfDataGrid.ItemsSourceProperty,nameof(CpuTableSection.Rows));
        grid.BindingContextChanged+=(_,_) => {
            if(grid.BindingContext is not CpuTableSection section){
                return;
            }

            this.RebuildCpuDataGridColumns(grid,section);
            grid.HeightRequest=CpuSectionGridHeaderHeight+(section.Rows.Count*CpuSectionRowHeight)+CpuSectionGridExtraHeight;
            if(section.CoreKind==CpuCoreKind.Performance){
                var maxWidth = this.GetCpuRightEdgeWidth(section);
                grid.MaximumWidthRequest=maxWidth;
                grid.WidthRequest=maxWidth;
                grid.HorizontalOptions=LayoutOptions.Start;
            }else{
                grid.HorizontalOptions=LayoutOptions.Fill;
            }
        };

        return new VerticalStackLayout{
            Spacing=3,
            Children = {
                header,
                grid,
            },
        };
    }

    private void RebuildCpuDataGridColumns(SfDataGrid grid,CpuTableSection section){
        grid.Columns.Clear();
        grid.Columns.Add(this.CreateCpuCoreColumn(section));
        foreach(var column in section.Columns){
            grid.Columns.Add(this.CreateCpuLogicalColumn(column));
        }
    }

    private DataGridTemplateColumn CreateCpuCoreColumn(CpuTableSection section){
        return new DataGridTemplateColumn{
            MappingName=nameof(CpuTableRow.Label),
            HeaderText="Core",
            Width=this.GetCpuCoreColumnWidth(section),
            AllowSorting=false,
            HeaderTemplate=this.CreateCpuSimpleHeaderTemplate("Core"),
            CellTemplate=new DataTemplate(() => {
                var layout = new Grid{
                    ColumnDefinitions = {
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
                if(badge.Content is Label badgeLabel){
                    badgeLabel.SetBinding(Label.TextProperty,nameof(CpuTableRow.BadgeText));
                    badgeLabel.SetBinding(Label.TextColorProperty,nameof(CpuTableRow.BadgeTextColor));
                }
                var label = new Label{
                    FontSize=10,
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
                    if(layout.BindingContext is CpuTableRow row){
                        this.viewModel?.ToggleCpuRow(row);
                    }
                };
                layout.GestureRecognizers.Add(tap);
                return layout;
            }),
        };
    }

    private double GetCpuCoreColumnWidth(CpuTableSection section){
        var labelWidth = section.Rows.Count==0
            ? 24
            : section.Rows.Max(static row => row.Label.Length)*7;
        var baseWidth=Math.Max(180,CpuLogicalCheckColumnWidth+24+labelWidth+42);
        return section.CoreKind switch{
            CpuCoreKind.Performance or CpuCoreKind.Efficiency => Math.Max(1,baseWidth*0.5),
            _ => baseWidth,
        };
    }

    private double GetCpuRightEdgeWidth(CpuTableSection section){
        return this.GetCpuCoreColumnWidth(section)
            +section.Columns.Count*CpuLogicalColumnWidth
            +2*CpuLogicalCheckColumnWidth;
    }

    private DataGridTemplateColumn CreateCpuLogicalColumn(CpuTableColumn column){
        var cellPath = nameof(CpuTableRow.GridCells)+"["+column.Index+"]";
        return new DataGridTemplateColumn{
            MappingName="Cpu"+column.Index,
            HeaderText=column.HeaderText,
            Width=CpuLogicalColumnWidth,
            AllowSorting=false,
            HeaderTemplate=this.CreateCpuColumnHeaderTemplate(column),
            CellTemplate=new DataTemplate(() => {
                var layout = new Grid{
                    ColumnDefinitions = {
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
                layout.SetBinding(IsVisibleProperty,new Binding(cellPath,converter:new NullToBoolConverter()));
                var check = this.CreateCpuCheckBox();
                check.SetBinding(SfCheckBox.IsCheckedProperty,new Binding(cellPath+"."+nameof(CpuTableCell.IsSelected),BindingMode.TwoWay));
                var spinner = new ActivityIndicator{
                    WidthRequest=14,
                    HeightRequest=14,
                    Color=Color.FromArgb("#2563EB"),
                    VerticalOptions=LayoutOptions.Center,
                    HorizontalOptions=LayoutOptions.Center,
                };
                spinner.SetBinding(ActivityIndicator.IsRunningProperty,new Binding(cellPath+"."+nameof(CpuTableCell.IsRunningJob)));
                spinner.SetBinding(IsVisibleProperty,new Binding(cellPath+"."+nameof(CpuTableCell.IsRunningJob)));
                var badge = this.CreateCoreBadge();
                badge.SetBinding(BackgroundColorProperty,cellPath+"."+nameof(CpuTableCell.BadgeBackgroundColor));
                if(badge.Content is Label badgeLabel){
                    badgeLabel.SetBinding(Label.TextProperty,cellPath+"."+nameof(CpuTableCell.CoreKindLabel));
                    badgeLabel.SetBinding(Label.TextColorProperty,cellPath+"."+nameof(CpuTableCell.BadgeTextColor));
                }
                var numberLabel = new Label{
                    FontSize=10,
                    TextColor=Color.FromArgb("#0F172A"),
                    HorizontalTextAlignment=TextAlignment.Start,
                    VerticalTextAlignment=TextAlignment.Center,
                    LineBreakMode=LineBreakMode.TailTruncation,
                };
                numberLabel.SetBinding(Label.TextProperty,cellPath+"."+nameof(CpuTableCell.LogicalProcessorText));
                numberLabel.SetBinding(ToolTipProperties.TextProperty,cellPath+"."+nameof(CpuTableCell.LogicalProcessorText));
                var statusLabel = new Label{
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
                layout.Add(spinner,1,0);
                layout.Add(badge,2,0);
                layout.Add(numberLabel,3,0);
                layout.Add(statusLabel,4,0);
                return layout;
            }),
        };
    }

    private DataTemplate CreateCpuSimpleHeaderTemplate(string text){
        return new DataTemplate(() => new Label{
            Text=text,
            FontSize=10,
            FontAttributes=FontAttributes.Bold,
            TextColor=Color.FromArgb("#0F172A"),
            BackgroundColor=Color.FromArgb("#F1F5F9"),
            HorizontalTextAlignment=TextAlignment.Center,
            VerticalTextAlignment=TextAlignment.Center,
            Padding=new Thickness(2,0),
        });
    }

    private DataTemplate CreateCpuColumnHeaderTemplate(CpuTableColumn column){
        return new DataTemplate(() => {
            var layout = new Grid{
                ColumnDefinitions = {
                    new ColumnDefinition { Width = new GridLength(CpuLogicalCheckColumnWidth) },
                    new ColumnDefinition { Width = GridLength.Star },
                },
                HorizontalOptions=LayoutOptions.Fill,
                VerticalOptions=LayoutOptions.Fill,
                BackgroundColor=Color.FromArgb("#F1F5F9"),
            };
            var check = this.CreateCpuCheckBox();
            check.InputTransparent=true;
            check.SetBinding(SfCheckBox.IsCheckedProperty,new Binding(nameof(CpuTableColumn.IsChecked),BindingMode.OneWay,source:column));
            var checkHost = new Grid{
                BackgroundColor=Colors.Transparent,
                HorizontalOptions=LayoutOptions.Fill,
                VerticalOptions=LayoutOptions.Fill,
            };
            checkHost.Add(check,0,0);
            layout.Add(checkHost,0,0);
            layout.Add(new Label{
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
                if(column.Section is { } section){
                    this.viewModel?.ToggleCpuColumn(section,column.Index);
                }
            };
            checkHost.GestureRecognizers.Add(tap);
            return layout;
        });
    }

    private SfCheckBox CreateCpuCheckBox() =>
        this.CreateSfCheckBox(CpuCheckBoxWidth,CpuCheckBoxHeight);

    private SfCheckBox CreateSfCheckBox(double width,double height,bool isThreeState = false){
        return new SfCheckBox{
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

    private void ConfigureDataGridScrollBars(SfDataGrid grid){
        void Apply(){
            var visualContainer = grid.GetVisualContainer();
            if(visualContainer is null){
                return;
            }

            this.ApplyDataGridScrollBarSize(visualContainer);
            this.TryInvokeMember(visualContainer,"UpdateScrollBars");
            visualContainer.InvalidateMeasure();
        }

        void ApplySafely(){
            try{
                Apply();
            }catch(Exception ex){
                StartupDiagnostics.Write("Failed to apply benchmark grid scroll bar size.",ex);
            }
        }

        async void ApplyNowAndLater(){
            ApplySafely();
            await Task.Delay(100);
            MainThread.BeginInvokeOnMainThread(ApplySafely);
        }

        grid.Loaded+=(_,_) => ApplyNowAndLater();
        grid.HandlerChanged+=(_,_) => MainThread.BeginInvokeOnMainThread(ApplyNowAndLater);
        grid.SizeChanged+=(_,_) => MainThread.BeginInvokeOnMainThread(ApplySafely);
    }

    private void ApplyDataGridScrollBarSize(object visualContainer){
        this.TrySetMemberValue(visualContainer,"ScrollerWidth",BenchmarkScrollBarSize);
        this.TrySetMemberValue(visualContainer,"ScrollerHeight",BenchmarkScrollBarSize);

        if(this.TryGetMemberValue(visualContainer,"HorizontalScrollbar") is { } horizontalScrollbar){
            this.TrySetMemberValue(horizontalScrollbar,"Height",BenchmarkScrollBarSize);
            this.TrySetMemberValue(horizontalScrollbar,"HeightRequest",BenchmarkScrollBarSize);
            this.TrySetMemberValue(horizontalScrollbar,"MinimumHeightRequest",BenchmarkScrollBarSize);
            this.TrySetMemberValue(horizontalScrollbar,"ScrollerHeight",BenchmarkScrollBarSize);
        }

        if(this.TryGetMemberValue(visualContainer,"VerticalScrollbar") is { } verticalScrollbar){
            this.TrySetMemberValue(verticalScrollbar,"Width",BenchmarkScrollBarSize);
            this.TrySetMemberValue(verticalScrollbar,"WidthRequest",BenchmarkScrollBarSize);
            this.TrySetMemberValue(verticalScrollbar,"MinimumWidthRequest",BenchmarkScrollBarSize);
            this.TrySetMemberValue(verticalScrollbar,"ScrollerWidth",BenchmarkScrollBarSize);
        }
    }

    private object? TryGetMemberValue(object target,string name){
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
        var property = target.GetType().GetProperty(name,flags);
        if(property is not null&&property.CanRead){
            return property.GetValue(target);
        }

        return target.GetType().GetField(name,flags)?.GetValue(target);
    }

    private void TrySetMemberValue(object target,string name,double value){
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
        var property = target.GetType().GetProperty(name,flags);
        if(property is not null&&property.CanWrite){
            property.SetValue(target,this.ConvertMemberValue(value,property.PropertyType));
            return;
        }

        var field = target.GetType().GetField(name,flags);
        field?.SetValue(target,this.ConvertMemberValue(value,field.FieldType));
    }

    private object ConvertMemberValue(double value,Type type){
        var targetType = Nullable.GetUnderlyingType(type)??type;
        if(targetType==typeof(double)){
            return value;
        }

        if(targetType==typeof(float)){
            return (float)value;
        }

        if(targetType==typeof(int)){
            return (int)value;
        }

        return Convert.ChangeType(value,targetType);
    }

    private void TryInvokeMember(object target,string name){
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
        target.GetType().GetMethod(name,flags)?.Invoke(target,null);
    }

    private Border CreateCoreBadge(){
        return new Border{
            WidthRequest=20,
            HeightRequest=18,
            Stroke=Color.FromArgb("#CBD5E1"),
            StrokeThickness=1,
            Padding=new Thickness(0),
            Content=new Label{
                FontSize=10,
                FontAttributes=FontAttributes.Bold,
                HorizontalTextAlignment=TextAlignment.Center,
                VerticalTextAlignment=TextAlignment.Center,
            },
        };
    }

    private Label CreateSelectionStateBox(){
        return new Label{
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

    private View CreateResultsView(){
        var resultList = new CollectionView{
            SelectionMode=SelectionMode.None,
            ItemTemplate=new DataTemplate(this.CreateResultRow),
            ItemsLayout=new LinearItemsLayout(ItemsLayoutOrientation.Vertical){
                ItemSpacing=0,
            },
        };
        resultList.SetBinding(ItemsView.ItemsSourceProperty,nameof(BenchmarkRunnerViewModel.VisibleResultItems));
        resultList.IsVisible=false;
        return resultList;
    }

    private View CreateResultRow(){
        var grid = new Grid{
            Padding=new Thickness(0),
            HeightRequest=22,
            ColumnDefinitions = {
                new ColumnDefinition { Width = new GridLength(2.2,GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(0.8,GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(1.1,GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(1.0,GridUnitType.Star) },
            },
            ColumnSpacing=6,
        };
        grid.Add(this.BoundCell(nameof(BenchmarkResultItem.Name),11,TextAlignment.Start),0,0);
        grid.Add(this.BoundCell(nameof(BenchmarkResultItem.Status),11,TextAlignment.Start),1,0);
        grid.Add(this.BoundCell(nameof(BenchmarkResultItem.Mean),11,TextAlignment.End),2,0);
        grid.Add(this.BoundCell(nameof(BenchmarkResultItem.AllocatedBytes),11,TextAlignment.End),3,0);
        return grid;
    }

    private Label BoundCell(string propertyName,double fontSize,TextAlignment alignment){
        var label = new Label{
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

    private View CreateChartView(){
        var chart = new SfCartesianChart{
            IsTransposed=true,
            Legend=new ChartLegend(),
            TooltipBehavior=new ChartTooltipBehavior{
                FontSize=12,
                Duration=4,
            },
            VerticalOptions=LayoutOptions.Fill,
        };
        chart.XAxes.Add(new CategoryAxis{
            LabelPlacement=LabelPlacement.BetweenTicks,
        });
        chart.YAxes.Add(new NumericalAxis{
            Minimum=0,
            Maximum=100,
        });

        var leftSeries = new StackingColumn100Series{
            XBindingPath=nameof(ChartComparisonPoint.DisplayName),
            YBindingPath=nameof(ChartComparisonPoint.LeftValue),
            ShowDataLabels=true,
            EnableTooltip=true,
            Spacing=0,
            Width=0.88,
            Fill=new SolidColorBrush(Color.FromArgb("#2563EB")),
            DataLabelSettings=new CartesianDataLabelSettings{
                BarAlignment=DataLabelAlignment.Middle,
                LabelPlacement=DataLabelPlacement.Inner,
            },
        };
        leftSeries.SetBinding(ChartSeries.ItemsSourceProperty,nameof(BenchmarkRunnerViewModel.ChartPoints));
        leftSeries.LabelTemplate=this.CreateDataLabelTemplate(nameof(ChartComparisonPoint.LeftText));
        leftSeries.TooltipTemplate=this.CreateTooltipTemplate();

        var rightSeries = new StackingColumn100Series{
            XBindingPath=nameof(ChartComparisonPoint.DisplayName),
            YBindingPath=nameof(ChartComparisonPoint.RightValue),
            ShowDataLabels=true,
            EnableTooltip=true,
            Spacing=0,
            Width=0.88,
            Fill=new SolidColorBrush(Color.FromArgb("#F97316")),
            DataLabelSettings=new CartesianDataLabelSettings{
                BarAlignment=DataLabelAlignment.Middle,
                LabelPlacement=DataLabelPlacement.Inner,
            },
        };
        rightSeries.SetBinding(ChartSeries.ItemsSourceProperty,nameof(BenchmarkRunnerViewModel.ChartPoints));
        rightSeries.LabelTemplate=this.CreateDataLabelTemplate(nameof(ChartComparisonPoint.RightText));
        rightSeries.TooltipTemplate=this.CreateTooltipTemplate();
        this.BindChartSeriesLabels(leftSeries,rightSeries);

        chart.Series.Add(leftSeries);
        chart.Series.Add(rightSeries);

        var emptyText = new Label{
            Text="Run benchmarks or load BenchmarkComparison.csv.",
            HorizontalOptions=LayoutOptions.Center,
            VerticalOptions=LayoutOptions.Center,
            TextColor=Color.FromArgb("#64748B"),
        };

        var chartContent = new Grid{
            RowDefinitions = {
                new RowDefinition { Height = GridLength.Star },
            },
            VerticalOptions=LayoutOptions.Fill,
            Children = {
                new ScrollView{
                    Orientation=ScrollOrientation.Vertical,
                    VerticalOptions=LayoutOptions.Fill,
                    Content=chart,
                },
            },
        };

        var grid = new Grid{
            Children = {
                chartContent,
                emptyText,
            },
        };
        emptyText.SetBinding(IsVisibleProperty,new Binding(nameof(BenchmarkRunnerViewModel.ChartPoints)+".Count",converter:new CountIsZeroConverter()));
        return grid;
    }

    private void BindChartSeriesLabels(StackingColumn100Series leftSeries,StackingColumn100Series rightSeries){
        void Apply(){
            if(this.viewModel is null){
                return;
            }

            leftSeries.Label=this.viewModel.ChartLeftLegendLabel;
            rightSeries.Label=this.viewModel.ChartRightLegendLabel;
        }

        Apply();
        if(this.viewModel is not null){
            this.viewModel.PropertyChanged+=(_,args) => {
                if(args.PropertyName==nameof(BenchmarkRunnerViewModel.SelectedComparisonPair)){
                    MainThread.BeginInvokeOnMainThread(Apply);
                }
            };
        }
    }


    private DataTemplate CreateDataLabelTemplate(string textPropertyName){
        return new DataTemplate(() => {
            var label = new Label{
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

    private DataTemplate CreateTooltipTemplate(){
        return new DataTemplate(() => {
            var label = new Label{
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

public sealed class CountIsZeroConverter:IValueConverter{
    public object Convert(object? value,Type targetType,object? parameter,System.Globalization.CultureInfo culture){
        return value is int count&&count==0;
    }

    public object ConvertBack(object? value,Type targetType,object? parameter,System.Globalization.CultureInfo culture){
        throw new NotSupportedException();
    }
}

public sealed class NullToBoolConverter:IValueConverter{
    public object Convert(object? value,Type targetType,object? parameter,System.Globalization.CultureInfo culture){
        return value is not null;
    }

    public object ConvertBack(object? value,Type targetType,object? parameter,System.Globalization.CultureInfo culture){
        throw new NotSupportedException();
    }
}

public sealed class CpuSelectionStateToBoolConverter:IValueConverter{
    public object Convert(object? value,Type targetType,object? parameter,System.Globalization.CultureInfo culture){
        return value is CpuSelectionState.Checked;
    }

    public object ConvertBack(object? value,Type targetType,object? parameter,System.Globalization.CultureInfo culture){
        throw new NotSupportedException();
    }
}
