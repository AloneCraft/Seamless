using Syncfusion.Maui.DataGrid;
using Syncfusion.Maui.Toolkit.Calendar;
using Syncfusion.Maui.Toolkit.Charts;

namespace Benchmark;

public sealed class MainPage:ContentPage{
    private const double CpuCheckBoxWidth = 28;
    private const double CpuCheckBoxHeight = 22;
    private const double CpuLogicalColumnWidth = 220;
    private const double CpuLogicalCheckColumnWidth = 34;
    private const double CpuLogicalNumberWidth = 164;
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
        if(this.viewModel is null){
            return true;
        }

        if(this.viewModel.IsRunning){
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

        var shouldSave = await this.DisplayAlert(
            "Save benchmark state",
            "Save selected CSV date and Benchmark x Project checks before closing?",
            "Save",
            "Don't save");
        if(shouldSave){
            this.viewModel.SaveUiState();
        }
        return true;
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
        var refreshButton = this.CommandButton("Refresh",nameof(BenchmarkRunnerViewModel.RefreshCommand));
        var selectAllButton = this.CommandButton("Select all",nameof(BenchmarkRunnerViewModel.SelectAllCommand));
        var clearButton = this.CommandButton("Clear",nameof(BenchmarkRunnerViewModel.ClearSelectionCommand));
        var runButton = this.CommandButton("Run",nameof(BenchmarkRunnerViewModel.RunCommand));
        var cancelButton = this.CommandButton("Cancel",nameof(BenchmarkRunnerViewModel.CancelCommand));
        var exportButton = this.CommandButton("Save CSV",nameof(BenchmarkRunnerViewModel.ExportCsvCommand));

        var modePicker = new Picker{
            WidthRequest=98,
            HeightRequest=30,
            ItemDisplayBinding=new Binding(nameof(RunModeOption.DisplayName)),
            Margin=new Thickness(0),
        };
        modePicker.SetBinding(Picker.ItemsSourceProperty,nameof(BenchmarkRunnerViewModel.RunModes));
        modePicker.SetBinding(Picker.SelectedItemProperty,nameof(BenchmarkRunnerViewModel.SelectedRunMode));

        var thresholdPicker = new Picker{
            WidthRequest=96,
            HeightRequest=30,
            ItemDisplayBinding=new Binding(nameof(ThresholdOption.DisplayName)),
            Margin=new Thickness(0),
        };
        thresholdPicker.SetBinding(Picker.ItemsSourceProperty,nameof(BenchmarkRunnerViewModel.ThresholdOptions));
        thresholdPicker.SetBinding(Picker.SelectedItemProperty,nameof(BenchmarkRunnerViewModel.SelectedThreshold));

        var csvDatePicker = new Picker{
            WidthRequest=168,
            HeightRequest=30,
            ItemDisplayBinding=new Binding(nameof(CsvDateOption.DisplayName)),
            Margin=new Thickness(0),
        };
        csvDatePicker.SetBinding(Picker.ItemsSourceProperty,nameof(BenchmarkRunnerViewModel.CsvDateOptions));
        csvDatePicker.SetBinding(Picker.SelectedItemProperty,nameof(BenchmarkRunnerViewModel.SelectedCsvDateOption));

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
        grid.Add(this.ToolbarLabel("Limit"),5,0);
        grid.Add(thresholdPicker,6,0);
        grid.Add(runButton,7,0);
        grid.Add(cancelButton,8,0);
        grid.Add(exportButton,9,0);
        grid.Add(this.ToolbarLabel("CSV"),10,0);
        grid.Add(csvDatePicker,11,0);
        grid.Add(statusLabel,12,0);
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
            RowHeight=36,
            HeaderRowHeight=28,
            AllowTriStateSorting=false,
            SortingMode=DataGridSortingMode.None,
            SelectionMode=DataGridSelectionMode.Extended,
            SelectionUnit=DataGridSelectionUnit.Row,
            HorizontalScrollBarVisibility=ScrollBarVisibility.Always,
            VerticalScrollBarVisibility=ScrollBarVisibility.Always,
            DefaultColumnWidth=92,
        };
        grid.SetBinding(SfDataGrid.ItemsSourceProperty,nameof(BenchmarkRunnerViewModel.BenchmarkRows));

        grid.Columns.Add(this.CreateCheckColumn());
        grid.Columns.Add(this.CreateTextColumn("Benchmark",nameof(BenchmarkTableRow.BenchmarkName),BenchmarkGridSortColumn.BenchmarkName,240,TextAlignment.Start));
        this.AddMetricGroup(grid,"比(SystemFLinq/SystemZLinq)",("Time",nameof(BenchmarkTableRow.SystemRatioTimeText),BenchmarkGridSortColumn.SystemRatioTime),("Allocated",nameof(BenchmarkTableRow.SystemRatioAllocatedText),BenchmarkGridSortColumn.SystemRatioAllocated));
        this.AddProjectGroup(grid,"SystemFLinq",nameof(BenchmarkTableRow.SystemFLinqCell),BenchmarkGridSortColumn.SystemFLinqTime,BenchmarkGridSortColumn.SystemFLinqAllocated);
        this.AddProjectGroup(grid,"SystemZLinq",nameof(BenchmarkTableRow.SystemZLinqCell),BenchmarkGridSortColumn.SystemZLinqTime,BenchmarkGridSortColumn.SystemZLinqAllocated);
        this.AddMetricGroup(grid,"比(PersistedFLinq/PersistedLinq)",("Time",nameof(BenchmarkTableRow.PersistedRatioTimeText),BenchmarkGridSortColumn.PersistedRatioTime),("Allocated",nameof(BenchmarkTableRow.PersistedRatioAllocatedText),BenchmarkGridSortColumn.PersistedRatioAllocated));
        this.AddProjectGroup(grid,"PersistedFLinq",nameof(BenchmarkTableRow.PersistedFLinqCell),BenchmarkGridSortColumn.PersistedFLinqTime,BenchmarkGridSortColumn.PersistedFLinqAllocated);
        this.AddProjectGroup(grid,"PersistedLinq",nameof(BenchmarkTableRow.PersistedLinqCell),BenchmarkGridSortColumn.PersistedLinqTime,BenchmarkGridSortColumn.PersistedLinqAllocated);
        this.AddMetricGroup(grid,"比(TsavoriteFLinq/TsavoriteLinq)",("Time",nameof(BenchmarkTableRow.TsavoriteRatioTimeText),BenchmarkGridSortColumn.TsavoriteRatioTime),("Allocated",nameof(BenchmarkTableRow.TsavoriteRatioAllocatedText),BenchmarkGridSortColumn.TsavoriteRatioAllocated));
        this.AddProjectGroup(grid,"TsavoriteFLinq",nameof(BenchmarkTableRow.TsavoriteFLinqCell),BenchmarkGridSortColumn.TsavoriteFLinqTime,BenchmarkGridSortColumn.TsavoriteFLinqAllocated);
        this.AddProjectGroup(grid,"TsavoriteLinq",nameof(BenchmarkTableRow.TsavoriteLinqCell),BenchmarkGridSortColumn.TsavoriteLinqTime,BenchmarkGridSortColumn.TsavoriteLinqAllocated);
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

    private void AddProjectGroup(
        SfDataGrid grid,
        string groupTitle,
        string cellPath,
        BenchmarkGridSortColumn timeSortColumn,
        BenchmarkGridSortColumn allocatedSortColumn){
        var checkMapping = cellPath+"Check";
        var timeMapping = cellPath+"Time";
        var allocatedMapping = cellPath+"Allocated";
        grid.Columns.Add(this.CreateProjectCheckColumn(checkMapping,cellPath));
        grid.Columns.Add(this.CreateProjectTextColumn("Time",timeMapping,cellPath+"."+nameof(BenchmarkProjectCell.TimeText),timeSortColumn,96,TextAlignment.End));
        grid.Columns.Add(this.CreateProjectTextColumn("Allocated",allocatedMapping,cellPath+"."+nameof(BenchmarkProjectCell.AllocatedText),allocatedSortColumn,96,TextAlignment.End));
        var stackedRow = grid.StackedHeaderRows.FirstOrDefault();
        if(stackedRow is null){
            stackedRow=new DataGridStackedHeaderRow();
            grid.StackedHeaderRows.Add(stackedRow);
        }
        stackedRow.Columns.Add(new DataGridStackedColumn{
            ColumnMappingNames=checkMapping+","+timeMapping+","+allocatedMapping,
            Text=groupTitle,
        });
    }

    private DataGridTemplateColumn CreateProjectCheckColumn(string mappingName,string cellPath){
        return new DataGridTemplateColumn{
            MappingName=mappingName,
            HeaderText="☑",
            Width=154,
            AllowSorting=false,
            HeaderTemplate=new DataTemplate(() => new Label{
                Text="☑",
                FontSize=12,
                FontAttributes=FontAttributes.Bold,
                TextColor=Color.FromArgb("#0F172A"),
                BackgroundColor=Color.FromArgb("#F1F5F9"),
                HorizontalTextAlignment=TextAlignment.Center,
                VerticalTextAlignment=TextAlignment.Center,
            }),
            CellTemplate=new DataTemplate(() => {
                var layout = new Grid{
                    ColumnDefinitions = {
                        new ColumnDefinition { Width = new GridLength(28) },
                        new ColumnDefinition { Width = new GridLength(18) },
                        new ColumnDefinition { Width = new GridLength(18) },
                        new ColumnDefinition { Width = GridLength.Star },
                    },
                    ColumnSpacing=2,
                    Padding=new Thickness(1,0),
                    VerticalOptions=LayoutOptions.Center,
                };
                var check = new CheckBox{
                    WidthRequest=24,
                    HeightRequest=24,
                    Margin=new Thickness(0),
                    VerticalOptions=LayoutOptions.Center,
                    HorizontalOptions=LayoutOptions.Center,
                };
                check.SetBinding(CheckBox.IsCheckedProperty,new Binding(cellPath+"."+nameof(BenchmarkProjectCell.IsSelected),BindingMode.TwoWay));
                check.SetBinding(IsEnabledProperty,cellPath+"."+nameof(BenchmarkProjectCell.HasCase));
                var icon = new Label{
                    FontSize=12,
                    TextColor=Color.FromArgb("#0F172A"),
                    HorizontalTextAlignment=TextAlignment.Center,
                    VerticalTextAlignment=TextAlignment.Center,
                };
                icon.SetBinding(Label.TextProperty,cellPath+"."+nameof(BenchmarkProjectCell.StatusIcon));
                var spinner = new ActivityIndicator{
                    WidthRequest=14,
                    HeightRequest=14,
                    Color=Color.FromArgb("#2563EB"),
                    VerticalOptions=LayoutOptions.Center,
                    HorizontalOptions=LayoutOptions.Center,
                };
                spinner.SetBinding(ActivityIndicator.IsRunningProperty,cellPath+"."+nameof(BenchmarkProjectCell.IsRunning));
                spinner.SetBinding(IsVisibleProperty,cellPath+"."+nameof(BenchmarkProjectCell.IsRunning));
                var detail = new Label{
                    FontSize=9,
                    TextColor=Color.FromArgb("#334155"),
                    LineBreakMode=LineBreakMode.TailTruncation,
                    VerticalTextAlignment=TextAlignment.Center,
                };
                detail.SetBinding(Label.TextProperty,cellPath+"."+nameof(BenchmarkProjectCell.ExecutionText));
                layout.Add(check,0,0);
                layout.Add(icon,1,0);
                layout.Add(spinner,2,0);
                layout.Add(detail,3,0);
                return layout;
            }),
        };
    }

    private DataGridTemplateColumn CreateProjectTextColumn(string header,string mappingName,string bindingPath,BenchmarkGridSortColumn sortColumn,double width,TextAlignment alignment){
        return new DataGridTemplateColumn{
            MappingName=mappingName,
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
                label.SetBinding(Label.TextProperty,bindingPath);
                return label;
            }),
        };
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
        grid.Add(this.CreateChartView(),0,1);
        return grid;
    }

    private View CreateTabHeader(){
        var cpuButton = this.TabButton("CPU",nameof(BenchmarkRunnerViewModel.ShowCpuCommand));
        var chartButton = this.TabButton("比率チャート",nameof(BenchmarkRunnerViewModel.ShowChartCommand));
        var metricPicker = new Picker{
            WidthRequest=86,
            HeightRequest=30,
            ItemDisplayBinding=new Binding(nameof(MetricOption.DisplayName)),
        };
        metricPicker.SetBinding(Picker.ItemsSourceProperty,nameof(BenchmarkRunnerViewModel.Metrics));
        metricPicker.SetBinding(Picker.SelectedItemProperty,nameof(BenchmarkRunnerViewModel.SelectedMetric));

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
                chartButton,
                chartModePicker,
                metricPicker,
            },
        };
    }

    private Button TabButton(string text,string commandPath){
        var button = this.CommandButton(text,commandPath);
        button.BackgroundColor=Color.FromArgb("#E2E8F0");
        button.TextColor=Color.FromArgb("#0F172A");
        button.CornerRadius=0;
        button.BorderColor=Color.FromArgb("#CBD5E1");
        button.BorderWidth=1;
        return button;
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

        var topGrid = new Grid{
            ColumnDefinitions = {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = new GridLength(260) },
            },
            ColumnSpacing=8,
            Children = {
                nList,
                this.CreateCsvCalendar(),
            },
        };
        topGrid.SetColumn(topGrid.Children[1],1);

        var layout = new VerticalStackLayout{
            Spacing=8,
            Children = {
                topGrid,
                list,
            },
        };
        layout.SetBinding(IsVisibleProperty,nameof(BenchmarkRunnerViewModel.IsCpuVisible));
        return layout;
    }

    private SfCalendar CreateCsvCalendar(){
        var calendar = new SfCalendar{
            View=CalendarView.Month,
            HeightRequest=210,
            MonthView=new CalendarMonthView(),
        };
        calendar.SetBinding(SfCalendar.SelectedDateProperty,new Binding(nameof(BenchmarkRunnerViewModel.SelectedCalendarDate),BindingMode.TwoWay));
        this.ApplyCalendarSpecialDays(calendar);
        if(this.viewModel is not null){
            this.viewModel.CsvCalendarDates.CollectionChanged+=(_,_) => this.ApplyCalendarSpecialDays(calendar);
        }
        return calendar;
    }

    private void ApplyCalendarSpecialDays(SfCalendar calendar){
        calendar.MonthView ??= new CalendarMonthView();
        calendar.MonthView.SpecialDayPredicate=date => this.viewModel is not null
            &&this.viewModel.CsvCalendarDates.Any(item => item.Date==date.Date)
                ? new CalendarIconDetails{
                    Icon=CalendarIcon.Dot,
                    Fill=Color.FromArgb("#2563EB"),
                }
                : null!;
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
                new ColumnDefinition { Width = new GridLength(32) },
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
                        new ColumnDefinition { Width = new GridLength(32) },
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
        return Math.Max(112,32+24+labelWidth+22);
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
                        new ColumnDefinition { Width = new GridLength(CpuLogicalNumberWidth) },
                    },
                    ColumnSpacing=2,
                    WidthRequest=CpuLogicalCheckColumnWidth+CpuLogicalNumberWidth+22,
                    HorizontalOptions=LayoutOptions.Center,
                    VerticalOptions=LayoutOptions.Center,
                };
                layout.SetBinding(IsVisibleProperty,new Binding(cellPath,converter:new NullToBoolConverter()));
                var check = this.CreateCpuCheckBox();
                check.SetBinding(CheckBox.IsCheckedProperty,new Binding(cellPath+"."+nameof(CpuTableCell.IsSelected),BindingMode.TwoWay));
                var spinner = new ActivityIndicator{
                    WidthRequest=14,
                    HeightRequest=14,
                    Color=Color.FromArgb("#2563EB"),
                    VerticalOptions=LayoutOptions.Center,
                    HorizontalOptions=LayoutOptions.Center,
                };
                spinner.SetBinding(ActivityIndicator.IsRunningProperty,new Binding(cellPath+"."+nameof(CpuTableCell.IsRunningJob)));
                spinner.SetBinding(IsVisibleProperty,new Binding(cellPath+"."+nameof(CpuTableCell.IsRunningJob)));
                var label = new Label{
                    FontSize=10,
                    TextColor=Color.FromArgb("#0F172A"),
                    HorizontalTextAlignment=TextAlignment.Start,
                    VerticalTextAlignment=TextAlignment.Center,
                    LineBreakMode=LineBreakMode.TailTruncation,
                    WidthRequest=CpuLogicalNumberWidth,
                };
                label.SetBinding(Label.TextProperty,cellPath+"."+nameof(CpuTableCell.DisplayText));
                layout.Add(check,0,0);
                layout.Add(spinner,1,0);
                layout.Add(label,2,0);
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
            WidthRequest=CpuCheckBoxWidth,
            HeightRequest=CpuCheckBoxHeight,
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

        var warningList = new CollectionView{
            SelectionMode=SelectionMode.None,
            HeightRequest=90,
            ItemTemplate=new DataTemplate(() => {
                var label = new Label{
                    FontSize=11,
                    TextColor=Color.FromArgb("#B45309"),
                    LineBreakMode=LineBreakMode.TailTruncation,
                    Padding=new Thickness(4,1),
                };
                label.SetBinding(Label.TextProperty,".");
                return label;
            }),
        };
        warningList.SetBinding(ItemsView.ItemsSourceProperty,nameof(BenchmarkRunnerViewModel.ChartWarnings));

        var emptyText = new Label{
            Text="Run benchmarks or load BenchmarkComparison.csv.",
            HorizontalOptions=LayoutOptions.Center,
            VerticalOptions=LayoutOptions.Center,
            TextColor=Color.FromArgb("#64748B"),
        };

        var chartContent = new Grid{
            RowDefinitions = {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star },
                new RowDefinition { Height = GridLength.Auto },
            },
            RowSpacing=6,
            Children = {
                this.CreateChartPairTabs(),
                new ScrollView{
                    Orientation=ScrollOrientation.Vertical,
                    Content=chart,
                },
                warningList,
            },
        };
        chartContent.SetRow(chartContent.Children[1],1);
        chartContent.SetRow(warningList,2);

        var grid = new Grid{
            Children = {
                chartContent,
                emptyText,
            },
        };
        grid.SetBinding(IsVisibleProperty,nameof(BenchmarkRunnerViewModel.IsChartVisible));
        emptyText.SetBinding(IsVisibleProperty,new Binding(nameof(BenchmarkRunnerViewModel.ChartPoints)+".Count",converter:new CountIsZeroConverter()));
        return grid;
    }

    private View CreateChartPairTabs(){
        var layout = new HorizontalStackLayout{
            Spacing=4,
        };
        foreach(var option in this.viewModel?.ComparisonPairs??[]){
            var button = new Button{
                Text=option.Key,
                Padding=new Thickness(10,3),
                HeightRequest=28,
                FontSize=12,
                CornerRadius=0,
                BackgroundColor=Color.FromArgb("#E2E8F0"),
                TextColor=Color.FromArgb("#0F172A"),
                BorderColor=Color.FromArgb("#CBD5E1"),
                BorderWidth=1,
            };
            button.Clicked+=(_,_) => {
                if(this.viewModel is not null){
                    this.viewModel.SelectedComparisonPair=option;
                }
            };
            layout.Children.Add(button);
        }
        return layout;
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
