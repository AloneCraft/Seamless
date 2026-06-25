using Microsoft.Maui.Layouts;
using Syncfusion.Maui.Toolkit.Charts;

namespace Benchmark;

public sealed class MainPage:ContentPage {
    private const double TableWidth = 1292;
    private const double SelectColumnWidth = 24;
    private const double NameColumnWidth = 260;
    private const double MetricColumnWidth = 112;

    private readonly BenchmarkRunnerViewModel viewModel;

    public MainPage() {
        this.Title="Seamless Benchmark";
        this.BackgroundColor=Color.FromArgb("#F8FAFC");
        this.viewModel=this.CreateViewModel();
        this.BindingContext=this.viewModel;

        var toolbar = this.CreateToolbar();
        var cpuPanel = this.CreateCpuPanel();
        var benchmarkTable = this.CreateBenchmarkTable();
        var resultsView = this.CreateResultsView();
        var chartView = this.CreateChartView();
        var rightPanel = new Grid {
            RowDefinitions = {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star },
            },
            Padding=new Thickness(0),
        };
        rightPanel.Add(this.CreateTabHeader(),0,0);
        rightPanel.Add(resultsView,0,1);
        rightPanel.Add(chartView,0,1);

        var contentGrid = new Grid {
            ColumnDefinitions = {
                new ColumnDefinition { Width = new GridLength(0.54,GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(0.46,GridUnitType.Star) },
            },
            RowDefinitions = {
                new RowDefinition { Height = GridLength.Star },
            },
            ColumnSpacing=1,
            BackgroundColor=Color.FromArgb("#CBD5E1"),
        };
        contentGrid.Add(this.WrapPanel("ベンチマーク",benchmarkTable),0,0);
        contentGrid.Add(this.WrapPanel("分析",rightPanel),1,0);

        this.Content = new Grid {
            RowDefinitions = {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star },
            },
            Children = {
                toolbar,
                cpuPanel,
                contentGrid,
            },
        };
        Grid.SetRow(cpuPanel,1);
        Grid.SetRow(contentGrid,2);
        this.Loaded+=async (_,_) => await this.viewModel.InitializeAsync();
    }

    private BenchmarkRunnerViewModel CreateViewModel() {
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

    private View CreateToolbar() {
        var refreshButton = this.CommandButton("更新",nameof(BenchmarkRunnerViewModel.更新コマンド));
        var selectAllButton = this.CommandButton("全選択",nameof(BenchmarkRunnerViewModel.全選択コマンド));
        var clearButton = this.CommandButton("選択解除",nameof(BenchmarkRunnerViewModel.選択解除コマンド));
        var runButton = this.CommandButton("実行",nameof(BenchmarkRunnerViewModel.実行コマンド));
        var cancelButton = this.CommandButton("中止",nameof(BenchmarkRunnerViewModel.中止コマンド));
        var exportButton = this.CommandButton("CSV保存",nameof(BenchmarkRunnerViewModel.CSV保存コマンド));
        var loadButton = this.CommandButton("CSV読込",nameof(BenchmarkRunnerViewModel.CSV読込コマンド));

        var scalePicker = new Picker {
            WidthRequest=78,
            HeightRequest=30,
            ItemDisplayBinding=new Binding(nameof(スケール選択肢.表示名)),
            Margin=new Thickness(0),
        };
        scalePicker.SetBinding(Picker.ItemsSourceProperty,nameof(BenchmarkRunnerViewModel.スケール一覧));
        scalePicker.SetBinding(Picker.SelectedItemProperty,nameof(BenchmarkRunnerViewModel.選択スケール));

        var statusLabel = new Label {
            FontSize=12,
            TextColor=Color.FromArgb("#475569"),
            LineBreakMode=LineBreakMode.TailTruncation,
            VerticalTextAlignment=TextAlignment.Center,
            HorizontalOptions=LayoutOptions.Fill,
        };
        statusLabel.SetBinding(Label.TextProperty,nameof(BenchmarkRunnerViewModel.状態テキスト表示));

        var grid = new Grid {
            Padding=new Thickness(10,8,10,4),
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
        grid.Add(this.ToolbarLabel("スケール"),3,0);
        grid.Add(scalePicker,4,0);
        grid.Add(runButton,5,0);
        grid.Add(cancelButton,6,0);
        grid.Add(exportButton,7,0);
        grid.Add(loadButton,8,0);
        grid.Add(statusLabel,9,0);
        return grid;
    }

    private View CreateCpuPanel() {
        var cpuPresetPicker = new Picker {
            WidthRequest=116,
            HeightRequest=28,
            ItemDisplayBinding=new Binding(nameof(CpuPresetOption.DisplayName)),
            Margin=new Thickness(0),
        };
        cpuPresetPicker.SetBinding(Picker.ItemsSourceProperty,nameof(BenchmarkRunnerViewModel.CPUプリセット一覧));
        cpuPresetPicker.SetBinding(Picker.SelectedItemProperty,nameof(BenchmarkRunnerViewModel.選択CPUプリセット));

        var cpuItems = new FlexLayout {
            Direction=FlexDirection.Row,
            Wrap=FlexWrap.Wrap,
            AlignItems=FlexAlignItems.Center,
            AlignContent=FlexAlignContent.Start,
            JustifyContent=FlexJustify.Start,
            Margin=new Thickness(0),
        };
        BindableLayout.SetItemsSource(cpuItems,this.viewModel.CPU一覧);
        BindableLayout.SetItemTemplate(cpuItems,new DataTemplate(this.CreateCpuCell));

        var grid = new Grid {
            Padding=new Thickness(10,0,10,5),
            BackgroundColor=Color.FromArgb("#F8FAFC"),
            ColumnDefinitions = {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
            },
            ColumnSpacing=6,
            Children = {
                this.ToolbarLabel("CPU"),
                cpuPresetPicker,
                cpuItems,
            },
        };
        Grid.SetColumn(cpuPresetPicker,1);
        Grid.SetColumn(cpuItems,2);
        return grid;
    }

    private Button CommandButton(string text,string commandPath) {
        var button = new Button {
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

    private Label ToolbarLabel(string text) {
        return new Label {
            Text=text,
            FontSize=12,
            VerticalTextAlignment=TextAlignment.Center,
            TextColor=Color.FromArgb("#334155"),
            Margin=new Thickness(0),
        };
    }

    private View WrapPanel(string title,View content) {
        var grid = new Grid {
            BackgroundColor=Colors.White,
            Padding=new Thickness(8),
            RowDefinitions = {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star },
            },
            RowSpacing=2,
            Children = {
                new Label { Text=title, FontSize=16, FontAttributes=FontAttributes.Bold, TextColor=Color.FromArgb("#0F172A") },
                content,
            },
        };
        Grid.SetRow(content,1);
        return grid;
    }

    private View CreateBenchmarkTable() {
        var rows = new CollectionView {
            SelectionMode=SelectionMode.None,
            ItemSizingStrategy=ItemSizingStrategy.MeasureFirstItem,
            ItemTemplate=new DataTemplate(this.CreateBenchmarkRow),
            ItemsLayout=new LinearItemsLayout(ItemsLayoutOrientation.Vertical) {
                ItemSpacing=0,
            },
            Margin=new Thickness(0),
        };
        rows.SetBinding(ItemsView.ItemsSourceProperty,nameof(BenchmarkRunnerViewModel.ベンチマーク一覧));

        var table = new Grid {
            WidthRequest=TableWidth,
            RowDefinitions = {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star },
            },
            RowSpacing=0,
            Children = {
                this.CreateBenchmarkHeader(),
                rows,
            },
        };
        Grid.SetRow(rows,1);
        return new ScrollView {
            Orientation=ScrollOrientation.Horizontal,
            Content=table,
            Margin=new Thickness(0),
        };
    }

    private View CreateBenchmarkHeader() {
        var grid = this.CreateBenchmarkGrid(34);
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(18) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(16) });
        grid.BackgroundColor=Color.FromArgb("#F1F5F9");
        grid.Add(this.HeaderLabel("選択"),0,0);
        grid.SetRowSpan(grid.Children[^1],2);
        grid.Add(this.HeaderLabel("ベンチマーク名"),1,0);
        grid.SetRowSpan(grid.Children[^1],2);
        this.AddMetricHeader(grid,2,"比率(FLinq/ZLinq)");
        this.AddMetricHeader(grid,3,"FLinq");
        this.AddMetricHeader(grid,4,"ZLinq");
        this.AddMetricHeader(grid,5,"比率(PersistedFLinq/PersistedLinq)");
        this.AddMetricHeader(grid,6,"PersistedFLinq");
        this.AddMetricHeader(grid,7,"PersistedLinq");
        this.AddMetricHeader(grid,8,"比率(TsavoriteFLinq/TsavoriteLinq)");
        this.AddMetricHeader(grid,9,"TsavoriteFLinq");
        this.AddMetricHeader(grid,10,"TsavoriteLinq");
        return grid;
    }

    private void AddMetricHeader(Grid grid,int column,string title) {
        grid.Add(this.HeaderLabel(title),column,0);
        var subGrid = new Grid {
            ColumnDefinitions = {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star },
            },
            ColumnSpacing=0,
            Padding=new Thickness(0),
            Children = {
                this.SubHeaderLabel("時間"),
                this.SubHeaderLabel("割当"),
            },
        };
        subGrid.SetColumn(subGrid.Children[1],1);
        grid.Add(subGrid,column,1);
    }

    private Grid CreateBenchmarkGrid(double rowHeight) {
        var grid = new Grid {
            HeightRequest=rowHeight,
            MinimumHeightRequest=rowHeight,
            Padding=new Thickness(0),
            Margin=new Thickness(0),
            ColumnSpacing=0,
            RowSpacing=0,
            ColumnDefinitions = {
                new ColumnDefinition { Width = new GridLength(SelectColumnWidth) },
                new ColumnDefinition { Width = new GridLength(NameColumnWidth) },
                new ColumnDefinition { Width = new GridLength(MetricColumnWidth) },
                new ColumnDefinition { Width = new GridLength(MetricColumnWidth) },
                new ColumnDefinition { Width = new GridLength(MetricColumnWidth) },
                new ColumnDefinition { Width = new GridLength(MetricColumnWidth) },
                new ColumnDefinition { Width = new GridLength(MetricColumnWidth) },
                new ColumnDefinition { Width = new GridLength(MetricColumnWidth) },
                new ColumnDefinition { Width = new GridLength(MetricColumnWidth) },
                new ColumnDefinition { Width = new GridLength(MetricColumnWidth) },
                new ColumnDefinition { Width = new GridLength(MetricColumnWidth) },
            },
        };
        return grid;
    }

    private View CreateBenchmarkRow() {
        var grid = this.CreateBenchmarkGrid(22);
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(22) });
        grid.Add(this.CreateBenchmarkCheckCell(),0,0);
        grid.Add(this.BoundCell(nameof(ベンチマーク行.ベンチマーク名),11,TextAlignment.Start),1,0);
        grid.Add(this.MetricCell(nameof(ベンチマーク行.比率System時間表示),nameof(ベンチマーク行.比率System割当表示)),2,0);
        grid.Add(this.MetricCell(nameof(ベンチマーク行.SystemFLinq時間表示),nameof(ベンチマーク行.SystemFLinq割当表示)),3,0);
        grid.Add(this.MetricCell(nameof(ベンチマーク行.SystemZLinq時間表示),nameof(ベンチマーク行.SystemZLinq割当表示)),4,0);
        grid.Add(this.MetricCell(nameof(ベンチマーク行.比率Persisted時間表示),nameof(ベンチマーク行.比率Persisted割当表示)),5,0);
        grid.Add(this.MetricCell(nameof(ベンチマーク行.PersistedFLinq時間表示),nameof(ベンチマーク行.PersistedFLinq割当表示)),6,0);
        grid.Add(this.MetricCell(nameof(ベンチマーク行.PersistedLinq時間表示),nameof(ベンチマーク行.PersistedLinq割当表示)),7,0);
        grid.Add(this.MetricCell(nameof(ベンチマーク行.比率Tsavorite時間表示),nameof(ベンチマーク行.比率Tsavorite割当表示)),8,0);
        grid.Add(this.MetricCell(nameof(ベンチマーク行.TsavoriteFLinq時間表示),nameof(ベンチマーク行.TsavoriteFLinq割当表示)),9,0);
        grid.Add(this.MetricCell(nameof(ベンチマーク行.TsavoriteLinq時間表示),nameof(ベンチマーク行.TsavoriteLinq割当表示)),10,0);
        return grid;
    }

    private View CreateBenchmarkCheckCell() {
        var border = this.CheckCellBorder();
        border.SetBinding(BindingContextProperty,new Binding("."));
        if(border.Content is Label checkLabel) {
            checkLabel.SetBinding(Label.TextProperty,nameof(ベンチマーク行.チェック表示));
        }
        var tap = new TapGestureRecognizer();
        tap.Tapped+=(_,args) => {
            if(border.BindingContext is ベンチマーク行 row) {
                row.選択中=!row.選択中;
            }
        };
        border.GestureRecognizers.Add(tap);
        return new Grid {
            Padding=new Thickness(0),
            HorizontalOptions=LayoutOptions.Center,
            VerticalOptions=LayoutOptions.Center,
            Children = {
                border,
            },
        };
    }

    private View CreateCpuCell() {
        var border = this.CheckCellBorder();
        border.SetBinding(BindingContextProperty,new Binding("."));
        if(border.Content is Label checkLabel) {
            checkLabel.SetBinding(Label.TextProperty,nameof(CpuSelectionItem.チェック表示));
        }
        var tap = new TapGestureRecognizer();
        tap.Tapped+=(_,_) => {
            if(border.BindingContext is CpuSelectionItem cpu) {
                cpu.IsSelected=!cpu.IsSelected;
            }
        };
        border.GestureRecognizers.Add(tap);

        var label = new Label {
            FontSize=10,
            VerticalTextAlignment=TextAlignment.Center,
            TextColor=Color.FromArgb("#334155"),
            Margin=new Thickness(1,0,4,0),
            LineBreakMode=LineBreakMode.TailTruncation,
        };
        label.SetBinding(Label.TextProperty,nameof(CpuSelectionItem.DisplayName));
        var grid = new Grid {
            WidthRequest=112,
            HeightRequest=18,
            Padding=new Thickness(0),
            Margin=new Thickness(0),
            ColumnDefinitions = {
                new ColumnDefinition { Width = new GridLength(13) },
                new ColumnDefinition { Width = GridLength.Star },
            },
            Children = {
                border,
                label,
            },
        };
        grid.SetColumn(label,1);
        return grid;
    }

    private Border CheckCellBorder() {
        return new Border {
            WidthRequest=12,
            HeightRequest=12,
            StrokeThickness=1,
            Stroke=Color.FromArgb("#64748B"),
            BackgroundColor=Colors.White,
            Padding=new Thickness(0),
            Margin=new Thickness(0),
            HorizontalOptions=LayoutOptions.Center,
            VerticalOptions=LayoutOptions.Center,
            Content=new Label {
                FontSize=10,
                FontAttributes=FontAttributes.Bold,
                TextColor=Color.FromArgb("#0F766E"),
                HorizontalTextAlignment=TextAlignment.Center,
                VerticalTextAlignment=TextAlignment.Center,
                Margin=new Thickness(0),
                Padding=new Thickness(0),
            },
        };
    }

    private Label HeaderLabel(string text) {
        return new Label {
            Text=text,
            FontSize=10,
            FontAttributes=FontAttributes.Bold,
            TextColor=Color.FromArgb("#0F172A"),
            LineBreakMode=LineBreakMode.TailTruncation,
            HorizontalTextAlignment=TextAlignment.Center,
            VerticalTextAlignment=TextAlignment.Center,
            Margin=new Thickness(0),
            Padding=new Thickness(1,0),
        };
    }

    private Label SubHeaderLabel(string text) {
        return new Label {
            Text=text,
            FontSize=9,
            TextColor=Color.FromArgb("#475569"),
            HorizontalTextAlignment=TextAlignment.Center,
            VerticalTextAlignment=TextAlignment.Center,
            Margin=new Thickness(0),
            Padding=new Thickness(0),
        };
    }

    private View MetricCell(string timeProperty,string allocatedProperty) {
        var grid = new Grid {
            ColumnDefinitions = {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star },
            },
            ColumnSpacing=0,
            Padding=new Thickness(0),
            Margin=new Thickness(0),
        };
        grid.Add(this.BoundCell(timeProperty,10,TextAlignment.End),0,0);
        grid.Add(this.BoundCell(allocatedProperty,10,TextAlignment.End),1,0);
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

    private View CreateTabHeader() {
        var resultsButton = this.CommandButton("結果",nameof(BenchmarkRunnerViewModel.結果表示コマンド));
        var chartButton = this.CommandButton("チャート",nameof(BenchmarkRunnerViewModel.チャート表示コマンド));
        var pairPicker = new Picker {
            WidthRequest=210,
            HeightRequest=30,
            ItemDisplayBinding=new Binding(nameof(ComparisonPairOption.DisplayName)),
        };
        pairPicker.SetBinding(Picker.ItemsSourceProperty,nameof(BenchmarkRunnerViewModel.比較軸一覧));
        pairPicker.SetBinding(Picker.SelectedItemProperty,nameof(BenchmarkRunnerViewModel.選択比較軸));

        var metricPicker = new Picker {
            WidthRequest=86,
            HeightRequest=30,
            ItemDisplayBinding=new Binding(nameof(指標選択肢.表示名)),
        };
        metricPicker.SetBinding(Picker.ItemsSourceProperty,nameof(BenchmarkRunnerViewModel.指標一覧));
        metricPicker.SetBinding(Picker.SelectedItemProperty,nameof(BenchmarkRunnerViewModel.選択指標));

        return new HorizontalStackLayout {
            Spacing=6,
            Padding=new Thickness(0,0,0,6),
            Children = {
                resultsButton,
                chartButton,
                pairPicker,
                metricPicker,
            },
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
        resultList.SetBinding(ItemsView.ItemsSourceProperty,nameof(BenchmarkRunnerViewModel.結果一覧));
        resultList.SetBinding(IsVisibleProperty,nameof(BenchmarkRunnerViewModel.結果表示中));
        return resultList;
    }

    private View CreateResultRow() {
        var grid = new Grid {
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

    private View CreateChartView() {
        var chart = new SfCartesianChart {
            IsTransposed=true,
            Legend=new ChartLegend(),
            TooltipBehavior=new ChartTooltipBehavior {
                FontSize=12,
                Duration=4,
            },
        };
        chart.SetBinding(HeightRequestProperty,nameof(BenchmarkRunnerViewModel.チャート高さ));
        chart.XAxes.Add(new CategoryAxis {
            LabelPlacement=LabelPlacement.BetweenTicks,
        });
        chart.YAxes.Add(new NumericalAxis {
            Minimum=0,
            Maximum=100,
        });

        var leftSeries = new StackingColumn100Series {
            XBindingPath=nameof(ChartComparisonPoint.DisplayName),
            YBindingPath=nameof(ChartComparisonPoint.LeftValue),
            Label="FLinq",
            ShowDataLabels=true,
            EnableTooltip=true,
            Spacing=0,
            Width=0.88,
            Fill=new SolidColorBrush(Color.FromArgb("#2563EB")),
            DataLabelSettings=new CartesianDataLabelSettings {
                BarAlignment=DataLabelAlignment.Middle,
                LabelPlacement=DataLabelPlacement.Inner,
            },
        };
        leftSeries.SetBinding(ChartSeries.ItemsSourceProperty,nameof(BenchmarkRunnerViewModel.チャート点一覧));
        leftSeries.LabelTemplate=this.CreateDataLabelTemplate(nameof(ChartComparisonPoint.LeftText));
        leftSeries.TooltipTemplate=this.CreateTooltipTemplate();

        var rightSeries = new StackingColumn100Series {
            XBindingPath=nameof(ChartComparisonPoint.DisplayName),
            YBindingPath=nameof(ChartComparisonPoint.RightValue),
            Label="ZLinq",
            ShowDataLabels=true,
            EnableTooltip=true,
            Spacing=0,
            Width=0.88,
            Fill=new SolidColorBrush(Color.FromArgb("#F97316")),
            DataLabelSettings=new CartesianDataLabelSettings {
                BarAlignment=DataLabelAlignment.Middle,
                LabelPlacement=DataLabelPlacement.Inner,
            },
        };
        rightSeries.SetBinding(ChartSeries.ItemsSourceProperty,nameof(BenchmarkRunnerViewModel.チャート点一覧));
        rightSeries.LabelTemplate=this.CreateDataLabelTemplate(nameof(ChartComparisonPoint.RightText));
        rightSeries.TooltipTemplate=this.CreateTooltipTemplate();

        chart.Series.Add(leftSeries);
        chart.Series.Add(rightSeries);

        var emptyText = new Label {
            Text="ベンチマークを実行するか BenchmarkComparison.csv を読み込んでください。",
            HorizontalOptions=LayoutOptions.Center,
            VerticalOptions=LayoutOptions.Center,
            TextColor=Color.FromArgb("#64748B"),
        };

        var grid = new Grid {
            Children = {
                new ScrollView {
                    Orientation=ScrollOrientation.Vertical,
                    Content=chart,
                },
                emptyText,
            },
        };
        grid.SetBinding(IsVisibleProperty,nameof(BenchmarkRunnerViewModel.チャート表示中));
        emptyText.SetBinding(IsVisibleProperty,new Binding(nameof(BenchmarkRunnerViewModel.チャート点一覧)+".Count",converter:new CountIsZeroConverter()));
        return grid;
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

public sealed class CountIsZeroConverter:IValueConverter {
    public object Convert(object? value,Type targetType,object? parameter,System.Globalization.CultureInfo culture) {
        return value is int count&&count==0;
    }

    public object ConvertBack(object? value,Type targetType,object? parameter,System.Globalization.CultureInfo culture) {
        throw new NotSupportedException();
    }
}
