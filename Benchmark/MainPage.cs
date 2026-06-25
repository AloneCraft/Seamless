using Syncfusion.Maui.DataGrid;
using Syncfusion.Maui.Toolkit.Charts;

namespace Benchmark;

public sealed class MainPage:ContentPage{
    private BenchmarkRunnerViewModel? viewModel;

    public MainPage(){
        try{
            StartupDiagnostics.Write("MainPage constructor started.");
            this.Title="Seamless Benchmark";
            this.BackgroundColor=Color.FromArgb("#F8FAFC");
            this.viewModel=this.CreateViewModel();
            this.BindingContext=this.viewModel;

            var toolbar = this.CreateToolbar();
            var contentGrid = new Grid{
                ColumnDefinitions = {
                    new ColumnDefinition { Width = new GridLength(0.56,GridUnitType.Star) },
                    new ColumnDefinition { Width = new GridLength(0.44,GridUnitType.Star) },
                },
                RowDefinitions = {
                    new RowDefinition { Height = GridLength.Star },
                },
                ColumnSpacing=1,
                BackgroundColor=Color.FromArgb("#CBD5E1"),
            };
            contentGrid.Add(this.WrapPanel("Benchmarks",this.CreateBenchmarkGrid()),0,0);
            contentGrid.Add(this.WrapPanel("Analysis",this.CreateRightPanel()),1,0);

            this.Content=new Grid{
                RowDefinitions = {
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = GridLength.Star },
                },
                Children = {
                    toolbar,
                    contentGrid,
                },
            };
            Grid.SetRow(contentGrid,1);
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
        if(this.viewModel is null||!this.viewModel.IsRunning){
            return true;
        }

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
        return true;
    }

    private BenchmarkRunnerViewModel CreateViewModel(){
        var resultService = new BenchmarkResultService();
        var comparisonService = new BenchmarkComparisonService(resultService);
        var csvService = new BenchmarkCsvService();
        return new BenchmarkRunnerViewModel(
            new BenchmarkCatalogService(),
            new CpuTopologyService(),
            new BenchmarkRunService(
                new BenchmarkQueueService(),
                new BenchmarkWorkerProcessService(),
                csvService,
                comparisonService),
            csvService,
            comparisonService);
    }

    private View CreateToolbar(){
        var refreshButton = this.CommandButton("Refresh",nameof(BenchmarkRunnerViewModel.RefreshCommand));
        var selectAllButton = this.CommandButton("Select all",nameof(BenchmarkRunnerViewModel.SelectAllCommand));
        var clearButton = this.CommandButton("Clear",nameof(BenchmarkRunnerViewModel.ClearSelectionCommand));
        var runButton = this.CommandButton("Run",nameof(BenchmarkRunnerViewModel.RunCommand));
        var cancelButton = this.CommandButton("Cancel",nameof(BenchmarkRunnerViewModel.CancelCommand));
        var exportButton = this.CommandButton("Save CSV",nameof(BenchmarkRunnerViewModel.ExportCsvCommand));
        var loadButton = this.CommandButton("Load CSV",nameof(BenchmarkRunnerViewModel.LoadCsvCommand));

        var modePicker = new Picker{
            WidthRequest=98,
            HeightRequest=30,
            ItemDisplayBinding=new Binding(nameof(RunModeOption.DisplayName)),
            Margin=new Thickness(0),
        };
        modePicker.SetBinding(Picker.ItemsSourceProperty,nameof(BenchmarkRunnerViewModel.RunModes));
        modePicker.SetBinding(Picker.SelectedItemProperty,nameof(BenchmarkRunnerViewModel.SelectedRunMode));

        var thresholdEntry = new Entry{
            WidthRequest=64,
            HeightRequest=30,
            FontSize=12,
            Keyboard=Keyboard.Numeric,
            Margin=new Thickness(0),
        };
        thresholdEntry.SetBinding(Entry.TextProperty,nameof(BenchmarkRunnerViewModel.AdaptiveThresholdSecondsText));

        var statusLabel = new Label{
            FontSize=12,
            TextColor=Color.FromArgb("#475569"),
            LineBreakMode=LineBreakMode.TailTruncation,
            VerticalTextAlignment=TextAlignment.Center,
            HorizontalOptions=LayoutOptions.Fill,
        };
        statusLabel.SetBinding(Label.TextProperty,nameof(BenchmarkRunnerViewModel.StatusText));

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
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
            },
            ColumnSpacing=6,
        };
        grid.Add(refreshButton,0,0);
        grid.Add(selectAllButton,1,0);
        grid.Add(clearButton,2,0);
        grid.Add(this.ToolbarLabel("Mode"),3,0);
        grid.Add(modePicker,4,0);
        grid.Add(this.ToolbarLabel("Limit s"),5,0);
        grid.Add(thresholdEntry,6,0);
        grid.Add(runButton,7,0);
        grid.Add(cancelButton,8,0);
        grid.Add(exportButton,9,0);
        grid.Add(loadButton,10,0);
        grid.Add(statusLabel,11,0);
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

    private Label ToolbarLabel(string text){
        return new Label{
            Text=text,
            FontSize=12,
            VerticalTextAlignment=TextAlignment.Center,
            TextColor=Color.FromArgb("#334155"),
            Margin=new Thickness(0),
        };
    }

    private View WrapPanel(string title,View content){
        var grid = new Grid{
            BackgroundColor=Colors.White,
            Padding=new Thickness(8),
            RowDefinitions = {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star },
            },
            RowSpacing=4,
            Children = {
                new Label { Text=title, FontSize=16, FontAttributes=FontAttributes.Bold, TextColor=Color.FromArgb("#0F172A") },
                content,
            },
        };
        Grid.SetRow(content,1);
        return grid;
    }

    private View CreateBenchmarkGrid(){
        StartupDiagnostics.WriteSyncfusionAssemblies("CreateBenchmarkGrid before SfDataGrid");
        var grid = new SfDataGrid{
            AutoGenerateColumnsMode=AutoGenerateColumnsMode.None,
            RowHeight=24,
            HeaderRowHeight=30,
            AllowTriStateSorting=false,
            SortingMode=DataGridSortingMode.None,
            DefaultColumnWidth=92,
        };
        grid.SetBinding(SfDataGrid.ItemsSourceProperty,nameof(BenchmarkRunnerViewModel.BenchmarkRows));

        grid.Columns.Add(this.CreateCheckColumn());
        grid.Columns.Add(this.CreateTextColumn("Benchmark",nameof(BenchmarkTableRow.BenchmarkName),BenchmarkGridSortColumn.BenchmarkName,240,TextAlignment.Start));
        this.AddMetricGroup(grid,"System ratio",("Time",nameof(BenchmarkTableRow.SystemRatioTimeText),BenchmarkGridSortColumn.SystemRatioTime),("Allocated",nameof(BenchmarkTableRow.SystemRatioAllocatedText),BenchmarkGridSortColumn.SystemRatioAllocated));
        this.AddMetricGroup(grid,"System FLinq",("Time",nameof(BenchmarkTableRow.SystemFLinqTimeText),BenchmarkGridSortColumn.SystemFLinqTime),("Allocated",nameof(BenchmarkTableRow.SystemFLinqAllocatedText),BenchmarkGridSortColumn.SystemFLinqAllocated));
        this.AddMetricGroup(grid,"System ZLinq",("Time",nameof(BenchmarkTableRow.SystemZLinqTimeText),BenchmarkGridSortColumn.SystemZLinqTime),("Allocated",nameof(BenchmarkTableRow.SystemZLinqAllocatedText),BenchmarkGridSortColumn.SystemZLinqAllocated));
        this.AddMetricGroup(grid,"Persisted ratio",("Time",nameof(BenchmarkTableRow.PersistedRatioTimeText),BenchmarkGridSortColumn.PersistedRatioTime),("Allocated",nameof(BenchmarkTableRow.PersistedRatioAllocatedText),BenchmarkGridSortColumn.PersistedRatioAllocated));
        this.AddMetricGroup(grid,"Persisted FLinq",("Time",nameof(BenchmarkTableRow.PersistedFLinqTimeText),BenchmarkGridSortColumn.PersistedFLinqTime),("Allocated",nameof(BenchmarkTableRow.PersistedFLinqAllocatedText),BenchmarkGridSortColumn.PersistedFLinqAllocated));
        this.AddMetricGroup(grid,"Persisted Linq",("Time",nameof(BenchmarkTableRow.PersistedLinqTimeText),BenchmarkGridSortColumn.PersistedLinqTime),("Allocated",nameof(BenchmarkTableRow.PersistedLinqAllocatedText),BenchmarkGridSortColumn.PersistedLinqAllocated));
        this.AddMetricGroup(grid,"Tsavorite ratio",("Time",nameof(BenchmarkTableRow.TsavoriteRatioTimeText),BenchmarkGridSortColumn.TsavoriteRatioTime),("Allocated",nameof(BenchmarkTableRow.TsavoriteRatioAllocatedText),BenchmarkGridSortColumn.TsavoriteRatioAllocated));
        this.AddMetricGroup(grid,"Tsavorite FLinq",("Time",nameof(BenchmarkTableRow.TsavoriteFLinqTimeText),BenchmarkGridSortColumn.TsavoriteFLinqTime),("Allocated",nameof(BenchmarkTableRow.TsavoriteFLinqAllocatedText),BenchmarkGridSortColumn.TsavoriteFLinqAllocated));
        this.AddMetricGroup(grid,"Tsavorite Linq",("Time",nameof(BenchmarkTableRow.TsavoriteLinqTimeText),BenchmarkGridSortColumn.TsavoriteLinqTime),("Allocated",nameof(BenchmarkTableRow.TsavoriteLinqAllocatedText),BenchmarkGridSortColumn.TsavoriteLinqAllocated));
        return grid;
    }

    private DataGridTemplateColumn CreateCheckColumn(){
        return new DataGridTemplateColumn{
            MappingName=nameof(BenchmarkTableRow.IsSelected),
            HeaderText="",
            Width=36,
            AllowSorting=false,
            HeaderTemplate=new DataTemplate(() => new BoxView{
                BackgroundColor=Colors.Transparent,
            }),
            CellTemplate=new DataTemplate(() => {
                var check = new CheckBox{
                    HorizontalOptions=LayoutOptions.Center,
                    VerticalOptions=LayoutOptions.Center,
                    Margin=new Thickness(0),
                };
                check.SetBinding(CheckBox.IsCheckedProperty,new Binding(nameof(BenchmarkTableRow.IsSelected),BindingMode.TwoWay));
                return check;
            }),
        };
    }

    private void AddMetricGroup(
        SfDataGrid grid,
        string groupTitle,
        (string header,string property,BenchmarkGridSortColumn sortColumn) time,
        (string header,string property,BenchmarkGridSortColumn sortColumn) allocated){
        var first = this.CreateTextColumn(time.header,time.property,time.sortColumn,86,TextAlignment.End);
        var second = this.CreateTextColumn(allocated.header,allocated.property,allocated.sortColumn,86,TextAlignment.End);
        grid.Columns.Add(first);
        grid.Columns.Add(second);
        var stackedRow = grid.StackedHeaderRows.FirstOrDefault();
        if(stackedRow is null){
            stackedRow=new DataGridStackedHeaderRow();
            grid.StackedHeaderRows.Add(stackedRow);
        }
        stackedRow.Columns.Add(new DataGridStackedColumn{
            ColumnMappingNames=time.property+","+allocated.property,
            Text=groupTitle,
        });
    }

    private DataGridTemplateColumn CreateTextColumn(string header,string property,BenchmarkGridSortColumn sortColumn,double width,TextAlignment alignment){
        return new DataGridTemplateColumn{
            MappingName=property,
            HeaderText=header,
            Width=width,
            AllowSorting=false,
            HeaderTemplate=this.CreateHeaderTemplate(header,sortColumn),
            CellTemplate=new DataTemplate(() => {
                var label = new Label{
                    FontSize=10,
                    TextColor=Color.FromArgb("#1E293B"),
                    LineBreakMode=LineBreakMode.TailTruncation,
                    HorizontalTextAlignment=alignment,
                    VerticalTextAlignment=TextAlignment.Center,
                    Margin=new Thickness(0),
                    Padding=new Thickness(2,0),
                };
                label.SetBinding(Label.TextProperty,property);
                return label;
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

    private View CreateRightPanel(){
        var grid = new Grid{
            RowDefinitions = {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star },
            },
            Padding=new Thickness(0),
        };
        grid.Add(this.CreateTabHeader(),0,0);
        grid.Add(this.CreateCpuView(),0,1);
        grid.Add(this.CreateResultsView(),0,1);
        grid.Add(this.CreateChartView(),0,1);
        return grid;
    }

    private View CreateTabHeader(){
        var cpuButton = this.CommandButton("CPU",nameof(BenchmarkRunnerViewModel.ShowCpuCommand));
        var resultsButton = this.CommandButton("Results",nameof(BenchmarkRunnerViewModel.ShowResultsCommand));
        var chartButton = this.CommandButton("Chart",nameof(BenchmarkRunnerViewModel.ShowChartCommand));
        var pairPicker = new Picker{
            WidthRequest=210,
            HeightRequest=30,
            ItemDisplayBinding=new Binding(nameof(ComparisonPairOption.DisplayName)),
        };
        pairPicker.SetBinding(Picker.ItemsSourceProperty,nameof(BenchmarkRunnerViewModel.ComparisonPairs));
        pairPicker.SetBinding(Picker.SelectedItemProperty,nameof(BenchmarkRunnerViewModel.SelectedComparisonPair));

        var metricPicker = new Picker{
            WidthRequest=86,
            HeightRequest=30,
            ItemDisplayBinding=new Binding(nameof(MetricOption.DisplayName)),
        };
        metricPicker.SetBinding(Picker.ItemsSourceProperty,nameof(BenchmarkRunnerViewModel.Metrics));
        metricPicker.SetBinding(Picker.SelectedItemProperty,nameof(BenchmarkRunnerViewModel.SelectedMetric));

        var resultNPicker = new Picker{
            WidthRequest=116,
            HeightRequest=30,
            ItemDisplayBinding=new Binding(nameof(ResultElementCountOption.DisplayName)),
        };
        resultNPicker.SetBinding(Picker.ItemsSourceProperty,nameof(BenchmarkRunnerViewModel.ResultElementCountOptions));
        resultNPicker.SetBinding(Picker.SelectedItemProperty,nameof(BenchmarkRunnerViewModel.SelectedResultElementCount));

        var chartModePicker = new Picker{
            WidthRequest=126,
            HeightRequest=30,
            ItemDisplayBinding=new Binding(nameof(ChartModeOption.DisplayName)),
        };
        chartModePicker.SetBinding(Picker.ItemsSourceProperty,nameof(BenchmarkRunnerViewModel.ChartModes));
        chartModePicker.SetBinding(Picker.SelectedItemProperty,nameof(BenchmarkRunnerViewModel.SelectedChartMode));

        return new HorizontalStackLayout{
            Spacing=6,
            Padding=new Thickness(0,0,0,6),
            Children = {
                cpuButton,
                resultsButton,
                chartButton,
                resultNPicker,
                chartModePicker,
                pairPicker,
                metricPicker,
            },
        };
    }

    private View CreateCpuView(){
        var list = new CollectionView{
            SelectionMode=SelectionMode.None,
            ItemTemplate=new DataTemplate(this.CreateCpuSectionView),
            ItemsLayout=new LinearItemsLayout(ItemsLayoutOrientation.Vertical){
                ItemSpacing=10,
            },
        };
        list.SetBinding(ItemsView.ItemsSourceProperty,nameof(BenchmarkRunnerViewModel.CpuSections));
        var nList = new CollectionView{
            SelectionMode=SelectionMode.None,
            HeightRequest=84,
            ItemsLayout=new LinearItemsLayout(ItemsLayoutOrientation.Horizontal){
                ItemSpacing=8,
            },
            ItemTemplate=new DataTemplate(() => {
                var row = new HorizontalStackLayout{
                    Spacing=2,
                    Padding=new Thickness(2,0),
                };
                var check = new CheckBox{
                    WidthRequest=22,
                    HeightRequest=22,
                    Margin=new Thickness(0),
                };
                check.SetBinding(CheckBox.IsCheckedProperty,new Binding(nameof(ElementCountOption.IsSelected),BindingMode.TwoWay));
                var label = new Label{
                    FontSize=11,
                    VerticalTextAlignment=TextAlignment.Center,
                    TextColor=Color.FromArgb("#334155"),
                };
                label.SetBinding(Label.TextProperty,nameof(ElementCountOption.DisplayName));
                row.Children.Add(check);
                row.Children.Add(label);
                return row;
            }),
        };
        nList.SetBinding(ItemsView.ItemsSourceProperty,nameof(BenchmarkRunnerViewModel.ElementCountOptions));

        var layout = new VerticalStackLayout{
            Spacing=8,
            Children = {
                nList,
                list,
            },
        };
        layout.SetBinding(IsVisibleProperty,nameof(BenchmarkRunnerViewModel.IsCpuVisible));
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
                new ColumnDefinition { Width = new GridLength(24) },
                new ColumnDefinition { Width = new GridLength(24) },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = new GridLength(24) },
            },
            ColumnSpacing=4,
        };
        var sectionCheck = this.CreateCpuCheckBox();
        sectionCheck.InputTransparent=true;
        sectionCheck.SetBinding(CheckBox.IsCheckedProperty,new Binding(nameof(CpuTableSection.State),BindingMode.OneWay,converter:new CpuSelectionStateToBoolConverter()));
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
            grid.HeightRequest=30+(section.Rows.Count*24)+2;
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
                        new ColumnDefinition { Width = new GridLength(22) },
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
                check.SetBinding(CheckBox.IsCheckedProperty,new Binding(nameof(CpuTableRow.IsChecked),BindingMode.OneWay));
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
        return Math.Max(96,22+24+labelWidth+18);
    }

    private DataGridTemplateColumn CreateCpuLogicalColumn(CpuTableColumn column){
        var cellPath = nameof(CpuTableRow.GridCells)+"["+column.Index+"]";
        return new DataGridTemplateColumn{
            MappingName="Cpu"+column.Index,
            HeaderText=column.HeaderText,
            Width=48,
            AllowSorting=false,
            HeaderTemplate=this.CreateCpuColumnHeaderTemplate(column),
            CellTemplate=new DataTemplate(() => {
                var layout = new HorizontalStackLayout{
                    Spacing=0,
                    HorizontalOptions=LayoutOptions.Center,
                    VerticalOptions=LayoutOptions.Center,
                };
                layout.SetBinding(IsVisibleProperty,new Binding(cellPath,converter:new NullToBoolConverter()));
                var check = this.CreateCpuCheckBox();
                check.SetBinding(CheckBox.IsCheckedProperty,new Binding(cellPath+"."+nameof(CpuTableCell.IsSelected),BindingMode.TwoWay));
                var label = new Label{
                    FontSize=10,
                    TextColor=Color.FromArgb("#0F172A"),
                    HorizontalTextAlignment=TextAlignment.Start,
                    VerticalTextAlignment=TextAlignment.Center,
                    LineBreakMode=LineBreakMode.TailTruncation,
                    WidthRequest=18,
                };
                label.SetBinding(Label.TextProperty,cellPath+"."+nameof(CpuTableCell.DisplayText));
                layout.Children.Add(check);
                layout.Children.Add(label);
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
                HorizontalOptions=LayoutOptions.Fill,
                VerticalOptions=LayoutOptions.Fill,
                BackgroundColor=Color.FromArgb("#F1F5F9"),
            };
            var check = this.CreateCpuCheckBox();
            check.InputTransparent=true;
            check.SetBinding(CheckBox.IsCheckedProperty,new Binding(nameof(CpuTableColumn.IsChecked),BindingMode.OneWay,source:column));
            layout.Add(check,0,0);
            var tap = new TapGestureRecognizer();
            tap.Tapped+=(_,_) => {
                if(column.Section is { } section){
                    this.viewModel?.ToggleCpuColumn(section,column.Index);
                }
            };
            layout.GestureRecognizers.Add(tap);
            return layout;
        });
    }

    private CheckBox CreateCpuCheckBox(){
        return new CheckBox{
            WidthRequest=22,
            HeightRequest=22,
            HorizontalOptions=LayoutOptions.Center,
            VerticalOptions=LayoutOptions.Center,
            Margin=new Thickness(0),
        };
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
        resultList.SetBinding(IsVisibleProperty,nameof(BenchmarkRunnerViewModel.IsResultsVisible));
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
        };
        chart.SetBinding(HeightRequestProperty,nameof(BenchmarkRunnerViewModel.ChartHeight));
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
            Label="Left",
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
            Label="Right",
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

        chart.Series.Add(leftSeries);
        chart.Series.Add(rightSeries);

        var emptyText = new Label{
            Text="Run benchmarks or load BenchmarkComparison.csv.",
            HorizontalOptions=LayoutOptions.Center,
            VerticalOptions=LayoutOptions.Center,
            TextColor=Color.FromArgb("#64748B"),
        };

        var grid = new Grid{
            Children = {
                new ScrollView{
                    Orientation=ScrollOrientation.Vertical,
                    Content=chart,
                },
                emptyText,
            },
        };
        grid.SetBinding(IsVisibleProperty,nameof(BenchmarkRunnerViewModel.IsChartVisible));
        emptyText.SetBinding(IsVisibleProperty,new Binding(nameof(BenchmarkRunnerViewModel.ChartPoints)+".Count",converter:new CountIsZeroConverter()));
        return grid;
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
