namespace Tsavorite.ParameterSweep.UI;

public sealed class MainPage:ContentPage {
    private readonly ParameterSweepViewModel viewModel = new();

    public MainPage() {
        this.Title="Tsavorite Parameter Sweep";
        this.BindingContext=this.viewModel;
        this.BackgroundColor=Color.FromArgb("#F8FAFC");
        this.Content=this.CreateContent();
    }

    private View CreateContent() {
        var runButton = new Button { Text="Run sweep",HeightRequest=34,Padding=new Thickness(12,4) };
        runButton.SetBinding(IsEnabledProperty,nameof(ParameterSweepViewModel.CanRun));
        runButton.Clicked+=async (_,_) => await this.viewModel.RunFullAsync();

        var smokeButton = new Button { Text="Smoke",HeightRequest=34,Padding=new Thickness(12,4) };
        smokeButton.SetBinding(IsEnabledProperty,nameof(ParameterSweepViewModel.CanRun));
        smokeButton.Clicked+=async (_,_) => await this.viewModel.RunSmokeAsync();

        var refreshButton = new Button { Text="Refresh runs",HeightRequest=34,Padding=new Thickness(12,4) };
        refreshButton.Clicked+=(_,_) => this.viewModel.RefreshRuns();

        var maxCountEntry = new Entry { WidthRequest=110,Keyboard=Keyboard.Numeric };
        maxCountEntry.SetBinding(Entry.TextProperty,nameof(ParameterSweepViewModel.MaxSuccessfulCountText));

        var stepsEntry = new Entry { WidthRequest=64,Keyboard=Keyboard.Numeric };
        stepsEntry.SetBinding(Entry.TextProperty,nameof(ParameterSweepViewModel.MaxSweepStepsText));

        var runPicker = new Picker {
            WidthRequest=260,
            ItemDisplayBinding=new Binding(nameof(ParameterSweepRunSummary.RunId)),
        };
        runPicker.SetBinding(Picker.ItemsSourceProperty,nameof(ParameterSweepViewModel.RunHistory));
        runPicker.SetBinding(Picker.SelectedItemProperty,nameof(ParameterSweepViewModel.SelectedBaselineRun));

        var toolbar = new Grid {
            Padding=new Thickness(10,8),
            ColumnSpacing=8,
            ColumnDefinitions= {
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
        };
        toolbar.Add(runButton,0,0);
        toolbar.Add(smokeButton,1,0);
        toolbar.Add(refreshButton,2,0);
        toolbar.Add(CreateLabel("MaxCount"),3,0);
        toolbar.Add(maxCountEntry,4,0);
        toolbar.Add(CreateLabel("Steps"),5,0);
        toolbar.Add(stepsEntry,6,0);
        toolbar.Add(runPicker,7,0);

        var status = new Label {
            FontSize=12,
            TextColor=Color.FromArgb("#334155"),
            Padding=new Thickness(10,0,10,8),
        };
        status.SetBinding(Label.TextProperty,nameof(ParameterSweepViewModel.StatusText));

        var header = this.CreateHeaderRow();
        var collection = new CollectionView {
            ItemSizingStrategy=ItemSizingStrategy.MeasureFirstItem,
            SelectionMode=SelectionMode.None,
            ItemTemplate=new DataTemplate(CreateResultRow),
        };
        collection.SetBinding(ItemsView.ItemsSourceProperty,nameof(ParameterSweepViewModel.Rows));
        var scroll = new ScrollView {
            Orientation=ScrollOrientation.Horizontal,
            Content=new Grid {
                RowDefinitions= {
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = GridLength.Star },
                },
                Children= {
                    header,
                    collection,
                },
            },
        };
        Grid.SetRow(collection,1);

        return new Grid {
            RowDefinitions= {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star },
            },
            Children= {
                toolbar,
                status,
                scroll,
            },
        };
    }

    private Grid CreateHeaderRow() {
        var grid = CreateResultGrid();
        grid.BackgroundColor=Color.FromArgb("#E2E8F0");
        AddHeader(grid,"Collection",0);
        AddHeader(grid,"Generic",1);
        AddHeader(grid,"Backend",2);
        AddHeader(grid,"Sweep",3);
        AddHeader(grid,"Index",4);
        AddHeader(grid,"Page",5);
        AddHeader(grid,"Segment",6);
        AddHeader(grid,"Memory",7);
        AddHeader(grid,"MF",8);
        AddHeader(grid,"MaxCount",9);
        AddHeader(grid,"Failure",10);
        AddHeader(grid,"Add",11);
        AddHeader(grid,"Lookup",12);
        AddHeader(grid,"CountDelta",13);
        AddHeader(grid,"AddRatio",14);
        AddHeader(grid,"LookupRatio",15);
        AddHeader(grid,"FailureChanged",16);
        return grid;
    }

    private static Grid CreateResultRow() {
        var grid = CreateResultGrid();
        grid.BackgroundColor=Colors.White;
        AddBound(grid,nameof(ParameterSweepComparisonViewRow.Collection),0);
        AddBound(grid,nameof(ParameterSweepComparisonViewRow.Generic),1);
        AddBound(grid,nameof(ParameterSweepComparisonViewRow.Backend),2);
        AddBound(grid,nameof(ParameterSweepComparisonViewRow.Sweep),3);
        AddBound(grid,nameof(ParameterSweepComparisonViewRow.IndexSize),4);
        AddBound(grid,nameof(ParameterSweepComparisonViewRow.PageSize),5);
        AddBound(grid,nameof(ParameterSweepComparisonViewRow.SegmentSize),6);
        AddBound(grid,nameof(ParameterSweepComparisonViewRow.MemorySize),7);
        AddBound(grid,nameof(ParameterSweepComparisonViewRow.MutableFraction),8);
        AddBound(grid,nameof(ParameterSweepComparisonViewRow.MaxSuccessfulCount),9);
        AddBound(grid,nameof(ParameterSweepComparisonViewRow.FailureKind),10);
        AddBound(grid,nameof(ParameterSweepComparisonViewRow.AddElapsed),11);
        AddBound(grid,nameof(ParameterSweepComparisonViewRow.LookupMean),12);
        AddBound(grid,nameof(ParameterSweepComparisonViewRow.MaxCountDelta),13);
        AddBound(grid,nameof(ParameterSweepComparisonViewRow.AddElapsedRatio),14);
        AddBound(grid,nameof(ParameterSweepComparisonViewRow.LookupRatio),15);
        AddBound(grid,nameof(ParameterSweepComparisonViewRow.FailureChanged),16);
        return grid;
    }

    private static Grid CreateResultGrid() =>
        new() {
            HeightRequest=30,
            ColumnDefinitions= {
                new ColumnDefinition { Width = 86 },
                new ColumnDefinition { Width = 160 },
                new ColumnDefinition { Width = 72 },
                new ColumnDefinition { Width = 96 },
                new ColumnDefinition { Width = 70 },
                new ColumnDefinition { Width = 70 },
                new ColumnDefinition { Width = 76 },
                new ColumnDefinition { Width = 78 },
                new ColumnDefinition { Width = 48 },
                new ColumnDefinition { Width = 96 },
                new ColumnDefinition { Width = 110 },
                new ColumnDefinition { Width = 86 },
                new ColumnDefinition { Width = 86 },
                new ColumnDefinition { Width = 86 },
                new ColumnDefinition { Width = 78 },
                new ColumnDefinition { Width = 86 },
                new ColumnDefinition { Width = 116 },
            },
        };

    private static Label CreateLabel(string text) =>
        new() {
            Text=text,
            VerticalOptions=LayoutOptions.Center,
            FontSize=12,
            TextColor=Color.FromArgb("#334155"),
        };

    private static void AddHeader(Grid grid,string text,int column) {
        var label = new Label {
            Text=text,
            Padding=new Thickness(6,4),
            FontSize=11,
            FontAttributes=FontAttributes.Bold,
            TextColor=Color.FromArgb("#0F172A"),
            VerticalTextAlignment=TextAlignment.Center,
        };
        grid.Add(label,column,0);
    }

    private static void AddBound(Grid grid,string path,int column) {
        var label = new Label {
            Padding=new Thickness(6,4),
            FontSize=11,
            TextColor=Color.FromArgb("#1E293B"),
            VerticalTextAlignment=TextAlignment.Center,
            LineBreakMode=LineBreakMode.TailTruncation,
        };
        label.SetBinding(Label.TextProperty,path);
        grid.Add(label,column,0);
    }
}
