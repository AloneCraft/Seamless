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

        var scalePicker = new Picker{
            WidthRequest=78,
            HeightRequest=30,
            ItemDisplayBinding=new Binding(nameof(ScaleOption.DisplayName)),
            Margin=new Thickness(0),
        };
        scalePicker.SetBinding(Picker.ItemsSourceProperty,nameof(BenchmarkRunnerViewModel.ScaleOptions));
        scalePicker.SetBinding(Picker.SelectedItemProperty,nameof(BenchmarkRunnerViewModel.SelectedScale));

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
                new ColumnDefinition { Width = GridLength.Star },
            },
            ColumnSpacing=6,
        };
        grid.Add(refreshButton,0,0);
        grid.Add(selectAllButton,1,0);
        grid.Add(clearButton,2,0);
        grid.Add(this.ToolbarLabel("Scale"),3,0);
        grid.Add(scalePicker,4,0);
        grid.Add(runButton,5,0);
        grid.Add(cancelButton,6,0);
        grid.Add(exportButton,7,0);
        grid.Add(loadButton,8,0);
        grid.Add(statusLabel,9,0);
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

        return new HorizontalStackLayout{
            Spacing=6,
            Padding=new Thickness(0,0,0,6),
            Children = {
                cpuButton,
                resultsButton,
                chartButton,
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
        list.SetBinding(IsVisibleProperty,nameof(BenchmarkRunnerViewModel.IsCpuVisible));
        return list;
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
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = new GridLength(24) },
            },
        };
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
        headerGrid.Add(title,0,0);
        headerGrid.Add(state,1,0);
        header.Content=headerGrid;
            var headerTap = new TapGestureRecognizer();
            headerTap.Tapped+=(_,_) => {
                if(header.BindingContext is CpuTableSection section){
                    this.viewModel?.ToggleCpuSection(section);
                }
            };
        header.GestureRecognizers.Add(headerTap);

        var columnHeader = new Grid{
            ColumnDefinitions = {
                new ColumnDefinition { Width = new GridLength(58) },
                new ColumnDefinition { Width = GridLength.Star },
            },
            ColumnSpacing=2,
            Padding=new Thickness(0,2,0,0),
        };
        columnHeader.Add(new Label{
            Text="Core",
            FontSize=10,
            TextColor=Color.FromArgb("#475569"),
            HorizontalTextAlignment=TextAlignment.Center,
            VerticalTextAlignment=TextAlignment.Center,
        },0,0);
        var columnItems = new HorizontalStackLayout{
            Spacing=2,
        };
        columnItems.SetBinding(BindableLayout.ItemsSourceProperty,new Binding(nameof(CpuTableSection.Columns)));
        BindableLayout.SetItemTemplate(columnItems,new DataTemplate(() => {
            var border = this.CpuCellBorder();
            var label = this.CpuCellLabel();
            label.FontSize=9;
            label.SetBinding(Label.TextProperty,nameof(CpuTableColumn.DisplayText));
            border.Content=label;
            var tap = new TapGestureRecognizer();
            tap.Tapped+=(_,_) => {
                if(border.BindingContext is CpuTableColumn column&&column.Section is { } section){
                    this.viewModel?.ToggleCpuColumn(section,column.Index);
                }
            };
            border.GestureRecognizers.Add(tap);
            return border;
        }));
        columnHeader.Add(columnItems,1,0);

        var rows = new VerticalStackLayout{
            Spacing=2,
        };
        rows.SetBinding(BindableLayout.ItemsSourceProperty,new Binding(nameof(CpuTableSection.Rows)));
        BindableLayout.SetItemTemplate(rows,new DataTemplate(() => {
            var rowGrid = new Grid{
                ColumnDefinitions = {
                    new ColumnDefinition { Width = new GridLength(58) },
                    new ColumnDefinition { Width = GridLength.Star },
                },
                ColumnSpacing=2,
            };
            var rowLabel = new Label{
                FontSize=10,
                TextColor=Color.FromArgb("#334155"),
                HorizontalTextAlignment=TextAlignment.Center,
                VerticalTextAlignment=TextAlignment.Center,
            };
            rowLabel.SetBinding(Label.TextProperty,nameof(CpuTableRow.Label));
            var cellItems = new HorizontalStackLayout{
                Spacing=2,
            };
            cellItems.SetBinding(BindableLayout.ItemsSourceProperty,new Binding(nameof(CpuTableRow.Cells)));
            BindableLayout.SetItemTemplate(cellItems,new DataTemplate(() => {
                var border = this.CpuCellBorder();
                var label = this.CpuCellLabel();
                label.SetBinding(Label.TextProperty,nameof(CpuTableCell.DisplayWithState));
                border.Content=label;
                var tap = new TapGestureRecognizer();
                tap.Tapped+=(_,_) => {
                    if(border.BindingContext is CpuTableCell cell){
                        this.viewModel?.ToggleCpu(cell.Cpu);
                    }
                };
                border.GestureRecognizers.Add(tap);
                return border;
            }));
            rowGrid.Add(rowLabel,0,0);
            rowGrid.Add(cellItems,1,0);
            return rowGrid;
        }));

        return new VerticalStackLayout{
            Spacing=2,
            Children = {
                header,
                columnHeader,
                rows,
            },
        };
    }

    private Border CpuCellBorder(){
        return new Border{
            WidthRequest=52,
            HeightRequest=24,
            Stroke=Color.FromArgb("#CBD5E1"),
            StrokeThickness=1,
            BackgroundColor=Colors.White,
            Padding=new Thickness(1,0),
        };
    }

    private Label CpuCellLabel(){
        return new Label{
            FontSize=10,
            TextColor=Color.FromArgb("#0F172A"),
            HorizontalTextAlignment=TextAlignment.Center,
            VerticalTextAlignment=TextAlignment.Center,
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
        resultList.SetBinding(ItemsView.ItemsSourceProperty,nameof(BenchmarkRunnerViewModel.ResultItems));
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
