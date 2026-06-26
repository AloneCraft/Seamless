using System.ComponentModel;
using Syncfusion.Maui.Buttons;
using Syncfusion.Maui.DataGrid;
using Syncfusion.Maui.Toolkit.Calendar;
using Syncfusion.Maui.Toolkit.Charts;

namespace Benchmark;

public sealed class MainPage:ContentPage{
    private const double CpuCheckBoxWidth = 40;
    private const double CpuCheckBoxHeight = 28;
    private const double BenchmarkSelectionColumnWidth = 52;
    private const double BenchmarkAggregateCheckBoxSize = 32;
    private const double BenchmarkCellCheckBoxWidth = 40;
    private const double BenchmarkCellCheckBoxHeight = 28;
    private const double BenchmarkCellCheckSlotWidth = 46;
    private const double BenchmarkHeaderCheckSlotWidth = 38;
    private const double BenchmarkScrollBarSize = 20;
    private const double CpuLogicalColumnWidth = 220;
    private const double CpuLogicalCheckColumnWidth = 46;
    private const double CpuLogicalNumberWidth = 164;
    private BenchmarkRunnerViewModel? viewModel;

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
            "保存して閉じますか？",
            "保存して閉じる",
            "保存せずに閉じる");
        if(shouldSave){
            this.viewModel.SaveUiState();
            return true;
        }

        var shouldDiscard = await this.DisplayAlert(
            "保存せずに閉じる",
            "保存せずに閉じますか？",
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
        var refreshButton = this.CommandButton("Refresh",nameof(BenchmarkRunnerViewModel.RefreshCommand));
        var runButton = this.CommandButton("Run",nameof(BenchmarkRunnerViewModel.RunCommand));
        var cancelButton = this.CommandButton("Cancel",nameof(BenchmarkRunnerViewModel.CancelCommand));
        var exportButton = this.CommandButton("Save CSV",nameof(BenchmarkRunnerViewModel.ExportCsvCommand));

        var thresholdPicker = new Picker{
            WidthRequest=96,
            HeightRequest=30,
            ItemDisplayBinding=new Binding(nameof(ThresholdOption.DisplayName)),
            Margin=new Thickness(0),
        };
        thresholdPicker.SetBinding(Picker.ItemsSourceProperty,nameof(BenchmarkRunnerViewModel.ThresholdOptions));
        thresholdPicker.SetBinding(Picker.SelectedItemProperty,nameof(BenchmarkRunnerViewModel.SelectedThreshold));

        var elementCountLabel = new Label{
            FontSize=18,
            FontAttributes=FontAttributes.Bold,
            TextColor=Color.FromArgb("#0F172A"),
            HorizontalTextAlignment=TextAlignment.Center,
            VerticalTextAlignment=TextAlignment.Center,
            MinimumWidthRequest=92,
            HeightRequest=30,
            Padding=new Thickness(8,0),
        };
        elementCountLabel.SetBinding(Label.TextProperty,nameof(BenchmarkRunnerViewModel.CurrentElementCountText));

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
        grid.Add(this.ToolbarLabel("計測時間"),1,0);
        grid.Add(thresholdPicker,2,0);
        grid.Add(this.ToolbarLabel("要素数"),3,0);
        grid.Add(elementCountLabel,4,0);
        grid.Add(runButton,5,0);
        grid.Add(cancelButton,6,0);
        grid.Add(exportButton,7,0);
        grid.Add(this.ToolbarLabel("CSV"),8,0);
        grid.Add(csvDatePicker,9,0);
        grid.Add(statusLabel,10,0);
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
            HeaderRowHeight=30,
            AllowTriStateSorting=false,
            SortingMode=DataGridSortingMode.None,
            SelectionMode=DataGridSelectionMode.Extended,
            SelectionUnit=DataGridSelectionUnit.Row,
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
        var systemMappings = new[] {
            this.AddProjectColumns(grid,"FLinq",nameof(BenchmarkTableRow.SystemFLinqCell),BenchmarkGridSortColumn.SystemFLinqTime,BenchmarkGridSortColumn.SystemFLinqAllocated),
            this.AddProjectColumns(grid,"ZLinq",nameof(BenchmarkTableRow.SystemZLinqCell),BenchmarkGridSortColumn.SystemZLinqTime,BenchmarkGridSortColumn.SystemZLinqAllocated),
        };
        this.AddBenchmarkSelectionGroup(categoryHeaderRow,this.GetBenchmarkSelectionGroup("System"),systemMappings);
        this.AddBenchmarkProjectSelectionGroup(projectHeaderRow,this.GetBenchmarkProjectSelectionGroup(BenchmarkComparisonFormatter.SystemFLinq,"FLinq"),systemMappings[0]);
        this.AddBenchmarkProjectSelectionGroup(projectHeaderRow,this.GetBenchmarkProjectSelectionGroup(BenchmarkComparisonFormatter.SystemZLinq,"ZLinq"),systemMappings[1]);
        var persistedMappings = new[] {
            this.AddProjectColumns(grid,"FLinq",nameof(BenchmarkTableRow.PersistedFLinqCell),BenchmarkGridSortColumn.PersistedFLinqTime,BenchmarkGridSortColumn.PersistedFLinqAllocated),
            this.AddProjectColumns(grid,"Linq",nameof(BenchmarkTableRow.PersistedLinqCell),BenchmarkGridSortColumn.PersistedLinqTime,BenchmarkGridSortColumn.PersistedLinqAllocated),
        };
        this.AddBenchmarkSelectionGroup(categoryHeaderRow,this.GetBenchmarkSelectionGroup("Persisted"),persistedMappings);
        this.AddBenchmarkProjectSelectionGroup(projectHeaderRow,this.GetBenchmarkProjectSelectionGroup(BenchmarkComparisonFormatter.PersistedFLinq,"FLinq"),persistedMappings[0]);
        this.AddBenchmarkProjectSelectionGroup(projectHeaderRow,this.GetBenchmarkProjectSelectionGroup(BenchmarkComparisonFormatter.PersistedLinq,"Linq"),persistedMappings[1]);
        var tsavoriteMappings = new[] {
            this.AddProjectColumns(grid,"FLinq",nameof(BenchmarkTableRow.TsavoriteFLinqCell),BenchmarkGridSortColumn.TsavoriteFLinqTime,BenchmarkGridSortColumn.TsavoriteFLinqAllocated),
            this.AddProjectColumns(grid,"Linq",nameof(BenchmarkTableRow.TsavoriteLinqCell),BenchmarkGridSortColumn.TsavoriteLinqTime,BenchmarkGridSortColumn.TsavoriteLinqAllocated),
        };
        this.AddBenchmarkSelectionGroup(categoryHeaderRow,this.GetBenchmarkSelectionGroup("Tsavorite"),tsavoriteMappings);
        this.AddBenchmarkProjectSelectionGroup(projectHeaderRow,this.GetBenchmarkProjectSelectionGroup(BenchmarkComparisonFormatter.TsavoriteFLinq,"FLinq"),tsavoriteMappings[0]);
        this.AddBenchmarkProjectSelectionGroup(projectHeaderRow,this.GetBenchmarkProjectSelectionGroup(BenchmarkComparisonFormatter.TsavoriteLinq,"Linq"),tsavoriteMappings[1]);
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
        grid.Columns.Add(this.CreateProjectTextColumn("Time",timeMapping,cellPath,cellPath+"."+nameof(BenchmarkProjectCell.TimeText),timeSortColumn,96,TextAlignment.End));
        grid.Columns.Add(this.CreateProjectTextColumn("Allocated",allocatedMapping,cellPath,cellPath+"."+nameof(BenchmarkProjectCell.AllocatedText),allocatedSortColumn,96,TextAlignment.End));
        return [checkMapping,timeMapping,allocatedMapping];
    }

    private string GetProjectFromCellPath(string cellPath){
        return cellPath switch{
            nameof(BenchmarkTableRow.SystemFLinqCell) => BenchmarkComparisonFormatter.SystemFLinq,
            nameof(BenchmarkTableRow.SystemZLinqCell) => BenchmarkComparisonFormatter.SystemZLinq,
            nameof(BenchmarkTableRow.PersistedFLinqCell) => BenchmarkComparisonFormatter.PersistedFLinq,
            nameof(BenchmarkTableRow.PersistedLinqCell) => BenchmarkComparisonFormatter.PersistedLinq,
            nameof(BenchmarkTableRow.TsavoriteFLinqCell) => BenchmarkComparisonFormatter.TsavoriteFLinq,
            nameof(BenchmarkTableRow.TsavoriteLinqCell) => BenchmarkComparisonFormatter.TsavoriteLinq,
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
            checkTap.Tapped+=(_,_) => this.viewModel?.ToggleAllBenchmarkSelection();
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
            checkTap.Tapped+=(_,_) => this.viewModel?.ToggleBenchmarkColumnGroup(group);
            checkHost.GestureRecognizers.Add(checkTap);
            var body = this.CreateBenchmarkHeaderBody(group.HeaderText);
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
            body.Add(new Label{
                Text=text,
                FontSize=12,
                FontAttributes=FontAttributes.Bold,
                TextColor=Color.FromArgb("#0F172A"),
                InputTransparent=true,
                HorizontalTextAlignment=TextAlignment.Start,
                VerticalTextAlignment=TextAlignment.Center,
                LineBreakMode=LineBreakMode.TailTruncation,
            },0,0);
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
            HeaderText=string.Empty,
            Width=166,
            AllowSorting=false,
            HeaderTemplate=new DataTemplate(() => new Label{
                Text=string.Empty,
                FontSize=12,
                FontAttributes=FontAttributes.Bold,
                TextColor=Color.FromArgb("#0F172A"),
                BackgroundColor=Color.FromArgb("#F1F5F9"),
                HorizontalTextAlignment=TextAlignment.Center,
                VerticalTextAlignment=TextAlignment.Center,
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
                    if(layout.BindingContext is BenchmarkTableRow row){
                        this.viewModel?.ToggleBenchmarkCellCheck(row,this.GetProjectFromCellPath(cellPath));
                    }
                };
                checkHost.GestureRecognizers.Add(checkTap);
                var detail = new Label{
                    FontSize=9,
                    TextColor=Color.FromArgb("#334155"),
                    InputTransparent=true,
                    LineBreakMode=LineBreakMode.TailTruncation,
                    VerticalTextAlignment=TextAlignment.Center,
                };
                detail.SetBinding(Label.TextProperty,cellPath+"."+nameof(BenchmarkProjectCell.ExecutionText));
                var detailHost = new Grid{
                    BackgroundColor=Colors.Transparent,
                    HorizontalOptions=LayoutOptions.Fill,
                    VerticalOptions=LayoutOptions.Fill,
                };
                detailHost.Add(detail,0,0);
                var detailTap = new TapGestureRecognizer();
                detailTap.Tapped+=(_,_) => {
                    if(layout.BindingContext is BenchmarkTableRow row){
                        this.viewModel?.SelectBenchmarkCell(row,this.GetProjectFromCellPath(cellPath),CreateBenchmarkSelectionGesture());
                    }
                };
                detailHost.GestureRecognizers.Add(detailTap);
                content.Add(checkHost,0,0);
                content.Add(detailHost,1,0);
                layout.Add(content,1,1);
                this.AddBenchmarkUiSelectionBorder(layout,cellPath);
                return layout;
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
                grid.Add(runningBackground,0,0);
                Grid.SetColumnSpan(runningBackground,3);
                Grid.SetRowSpan(runningBackground,3);
                grid.Add(label,0,0);
                Grid.SetColumnSpan(label,3);
                Grid.SetRowSpan(label,3);
                this.AddMetricCompletedBorder(grid,cellPath);
                this.AddBenchmarkUiSelectionBorder(grid,cellPath);
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
            BackgroundColor=Color.FromArgb("#DBEAFE"),
            Opacity=0.52,
            InputTransparent=true,
        };
        background.SetBinding(IsVisibleProperty,cellPath+"."+nameof(BenchmarkProjectCell.IsUiSelected));
        grid.Add(background,0,0);
        Grid.SetColumnSpan(background,3);
        Grid.SetRowSpan(background,3);
    }

    private void AddMetricCompletedBorder(Grid grid,string cellPath){
        var color = Color.FromArgb("#22C55E");
        var top = this.CreateMetricBorderSegment(color);
        var bottom = this.CreateMetricBorderSegment(color);
        var left = this.CreateMetricBorderSegment(color);
        var right = this.CreateMetricBorderSegment(color);
        top.SetBinding(IsVisibleProperty,cellPath+"."+nameof(BenchmarkProjectCell.IsCompletedVisual));
        bottom.SetBinding(IsVisibleProperty,cellPath+"."+nameof(BenchmarkProjectCell.IsCompletedVisual));
        left.SetBinding(IsVisibleProperty,cellPath+"."+nameof(BenchmarkProjectCell.IsCompletedVisual));
        right.SetBinding(IsVisibleProperty,cellPath+"."+nameof(BenchmarkProjectCell.IsCompletedVisual));
        grid.Add(top,0,0);
        Grid.SetColumnSpan(top,3);
        grid.Add(bottom,0,2);
        Grid.SetColumnSpan(bottom,3);
        grid.Add(left,0,0);
        Grid.SetRowSpan(left,3);
        grid.Add(right,2,0);
        Grid.SetRowSpan(right,3);
    }

    private void AddBenchmarkUiSelectionBorder(Grid grid,string cellPath){
        var color = Color.FromArgb("#2563EB");
        var top = this.CreateMetricBorderSegment(color);
        var bottom = this.CreateMetricBorderSegment(color);
        var left = this.CreateMetricBorderSegment(color);
        var right = this.CreateMetricBorderSegment(color);
        top.SetBinding(IsVisibleProperty,cellPath+"."+nameof(BenchmarkProjectCell.IsUiSelected));
        bottom.SetBinding(IsVisibleProperty,cellPath+"."+nameof(BenchmarkProjectCell.IsUiSelected));
        left.SetBinding(IsVisibleProperty,cellPath+"."+nameof(BenchmarkProjectCell.IsUiSelected));
        right.SetBinding(IsVisibleProperty,cellPath+"."+nameof(BenchmarkProjectCell.IsUiSelected));
        grid.Add(top,0,0);
        Grid.SetColumnSpan(top,3);
        grid.Add(bottom,0,2);
        Grid.SetColumnSpan(bottom,3);
        grid.Add(left,0,0);
        Grid.SetRowSpan(left,3);
        grid.Add(right,2,0);
        Grid.SetRowSpan(right,3);
    }

    private BoxView CreateMetricBorderSegment(Color color){
        return new BoxView{
            BackgroundColor=color,
            InputTransparent=true,
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
                if(property==nameof(BenchmarkTableRow.BenchmarkName)){
                    var tap = new TapGestureRecognizer();
                    tap.Tapped+=(_,_) => {
                        if(label.BindingContext is BenchmarkTableRow row){
                            this.viewModel?.SelectBenchmarkRow(row,CreateBenchmarkSelectionGesture());
                        }
                    };
                    label.GestureRecognizers.Add(tap);
                }
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
        var cpuButton = this.CommandTabButton("CPU",nameof(BenchmarkRunnerViewModel.ShowCpuCommand));
        var chartButton = this.CommandTabButton("比率チャート",nameof(BenchmarkRunnerViewModel.ShowChartCommand));
        this.BindTabButtonState(cpuButton,nameof(BenchmarkRunnerViewModel.SelectedTab),static viewModel => viewModel.SelectedTab==BenchmarkTab.Cpu);
        this.BindTabButtonState(chartButton,nameof(BenchmarkRunnerViewModel.SelectedTab),static viewModel => viewModel.SelectedTab==BenchmarkTab.Chart);
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

    private Button CommandTabButton(string text,string commandPath){
        var button = this.CreateTabButton(text);
        button.SetBinding(Button.CommandProperty,commandPath);
        return button;
    }

    private Button CreateTabButton(string text){
        var button = new Button{
            Text=text,
            CornerRadius=0,
            BorderColor=Color.FromArgb("#CBD5E1"),
            BorderWidth=1,
            Margin=new Thickness(0),
            FontSize=12,
            MinimumHeightRequest=28,
        };
        this.ApplyTabButtonStyle(button,false);
        return button;
    }

    private void BindTabButtonState(Button button,string propertyName,Func<BenchmarkRunnerViewModel,bool> isSelected){
        void Apply(){
            if(this.viewModel is not null){
                this.ApplyTabButtonStyle(button,isSelected(this.viewModel));
            }
        }

        Apply();
        if(this.viewModel is not null){
            this.viewModel.PropertyChanged+=(_,args) => {
                if(args.PropertyName==propertyName){
                    MainThread.BeginInvokeOnMainThread(Apply);
                }
            };
        }
    }

    private void ApplyTabButtonStyle(Button button,bool isSelected){
        button.BackgroundColor=isSelected ? Colors.White : Color.FromArgb("#EAF3FF");
        button.TextColor=Color.FromArgb("#0F172A");
        button.Padding=isSelected ? new Thickness(12,4) : new Thickness(10,3);
        button.HeightRequest=isSelected ? 32 : 28;
        button.FontAttributes=isSelected ? FontAttributes.Bold : FontAttributes.None;
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
        var topGrid = new Grid{
            ColumnDefinitions = {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = new GridLength(260) },
            },
            ColumnSpacing=8,
            Children = {
                new BoxView{
                    BackgroundColor=Colors.Transparent,
                },
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
        return Math.Max(126,CpuLogicalCheckColumnWidth+24+labelWidth+22);
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
            check.SetBinding(SfCheckBox.IsCheckedProperty,new Binding(nameof(CpuTableColumn.IsChecked),BindingMode.OneWay,source:column));
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
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star },
                new RowDefinition { Height = GridLength.Auto },
            },
            RowSpacing=6,
            Children = {
                this.CreateChartElementSlider(),
                this.CreateChartPairTabs(),
                new ScrollView{
                    Orientation=ScrollOrientation.Vertical,
                    Content=chart,
                },
                warningList,
            },
        };
        chartContent.SetRow(chartContent.Children[1],1);
        chartContent.SetRow(chartContent.Children[2],2);
        chartContent.SetRow(warningList,3);

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

    private View CreateChartElementSlider(){
        var label = new Label{
            Text="N",
            FontSize=11,
            TextColor=Color.FromArgb("#475569"),
            VerticalTextAlignment=TextAlignment.Center,
        };
        var slider = new Slider{
            Minimum=0,
            WidthRequest=240,
            HorizontalOptions=LayoutOptions.Fill,
        };
        slider.SetBinding(Slider.MaximumProperty,nameof(BenchmarkRunnerViewModel.ChartElementCountSliderMaximum));
        slider.SetBinding(Slider.ValueProperty,new Binding(nameof(BenchmarkRunnerViewModel.SelectedResultElementCountIndex),BindingMode.TwoWay));
        var valueLabel = new Label{
            FontSize=16,
            FontAttributes=FontAttributes.Bold,
            TextColor=Color.FromArgb("#0F172A"),
            MinimumWidthRequest=76,
            HorizontalTextAlignment=TextAlignment.End,
            VerticalTextAlignment=TextAlignment.Center,
        };
        valueLabel.SetBinding(Label.TextProperty,nameof(BenchmarkRunnerViewModel.SelectedChartElementCountText));
        var grid = new Grid{
            ColumnDefinitions = {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto },
            },
            ColumnSpacing=8,
            Padding=new Thickness(0,2,0,0),
            Children = {
                label,
                slider,
                valueLabel,
            },
        };
        grid.SetColumn(slider,1);
        grid.SetColumn(valueLabel,2);
        grid.SetBinding(IsVisibleProperty,nameof(BenchmarkRunnerViewModel.IsChartElementSliderVisible));
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

    private View CreateChartPairTabs(){
        var layout = new HorizontalStackLayout{
            Spacing=4,
        };
        foreach(var option in this.viewModel?.ComparisonPairs??[]){
            var button = this.CreateTabButton(option.Key);
            this.BindTabButtonState(
                button,
                nameof(BenchmarkRunnerViewModel.SelectedComparisonPair),
                viewModel => viewModel.SelectedComparisonPair==option);
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
