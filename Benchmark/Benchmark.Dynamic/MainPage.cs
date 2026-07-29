using Benchmark.Dynamic.Core;
using Syncfusion.Maui.Core.Internals;
using Syncfusion.Maui.DataGrid;
using Syncfusion.Maui.Toolkit.TabView;
using System.ComponentModel;

namespace Benchmark.Dynamic;

public sealed class MainPage:ContentPage {
    private readonly 動的ベンチマーク画面モデル 画面モデル;
    private readonly Grid ルート;
    private readonly Grid 設定領域;
    private readonly Grid 詳細領域;
    private readonly List<VisualElement> 設定入力一覧=[];
    private readonly List<VisualElement> 実行中無効化一覧=[];

    public MainPage(動的ベンチマーク画面モデル 画面モデル) {
        this.画面モデル=画面モデル;
        this.BindingContext=画面モデル;
        this.Title="Dynamic LINQ Benchmark";
        this.BackgroundColor=Color.FromArgb("#F4F7FB");

        this.ルート=new Grid {
            ColumnDefinitions={
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(6),
                new ColumnDefinition(画面モデル.設定領域幅),
            },
            RowDefinitions={
                new RowDefinition(GridLength.Star),
                new RowDefinition(6),
                new RowDefinition(画面モデル.詳細領域高さ),
            },
            RowSpacing=0,
            ColumnSpacing=0,
            Padding=new Thickness(10),
        };

        var 結果領域=this.結果領域を作成();
        this.ルート.Add(結果領域,0,0);
        var 設定分割=new 動的GridSplitter(動的分割方向.垂直,差分=>{
            if(!this.画面モデル.設定領域折畳済みか) {
                this.画面モデル.設定領域幅-=差分;
            }
        });
        Grid.SetRowSpan(設定分割,3);
        this.ルート.Add(設定分割,1,0);

        this.設定領域=this.設定領域を作成();
        Grid.SetRowSpan(this.設定領域,3);
        this.ルート.Add(this.設定領域,2,0);

        var 詳細分割=new 動的GridSplitter(動的分割方向.水平,差分=>{
            if(!this.画面モデル.詳細領域折畳済みか) {
                this.画面モデル.詳細領域高さ-=差分;
            }
        });
        this.ルート.Add(詳細分割,0,1);

        this.詳細領域=this.詳細領域を作成();
        this.ルート.Add(this.詳細領域,0,2);

        this.Content=this.ルート;
        this.画面モデル.PropertyChanged+=this.画面モデル変更時;
        this.画面状態を適用();
    }

    private View 結果領域を作成() {
        var タブ=new SfTabView {
            AutomationId="動的結果.上位タブ",
            TabBarHeight=34,
            EnableSwiping=false,
            ContentTransitionDuration=0,
            HeaderItemSpacing=1,
            TabBarBackground=Color.FromArgb("#E2E8F0"),
            BackgroundColor=Colors.White,
        };
        AutomationProperties.SetIsInAccessibleTree(タブ,true);
        タブ.Items=[
            new SfTabItem {
                Header="System",
                Content=this.ソース群タブを作成([
                    SourceTarget.SystemArray,
                    SourceTarget.SystemIEnumerable,
                    SourceTarget.SystemRange,
                    SourceTarget.SystemList,
                    SourceTarget.SystemPersisted,
                    SourceTarget.SystemTsavorite,
                ]),
            },
            new SfTabItem {
                Header="Seamless",
                Content=this.ソース群タブを作成([
                    SourceTarget.SeamlessPersisted,
                    SourceTarget.SeamlessTsavorite,
                ]),
            },
            new SfTabItem {
                Header="履歴",
                Content=this.履歴グリッドを作成(false),
            },
            new SfTabItem {
                Header="ごみ箱",
                Content=this.履歴グリッドを作成(true),
            },
        ];
        タブ.SelectedIndex=0;
        return タブ;
    }

    private SfTabView ソース群タブを作成(IReadOnlyList<SourceTarget> 対象一覧) {
        var タブ=new SfTabView {
            TabBarHeight=30,
            EnableSwiping=false,
            ContentTransitionDuration=0,
            HeaderItemSpacing=1,
            TabBarBackground=Color.FromArgb("#F8FAFC"),
            BackgroundColor=Colors.White,
        };
        var 項目一覧=new TabItemCollection();
        foreach(var 対象 in 対象一覧) {
            var 画面=this.画面モデル.ソース画面一覧[対象];
            項目一覧.Add(new SfTabItem {
                Header=対象.GetSourceKind().ToString(),
                Content=this.結果グリッドを作成(画面),
            });
        }
        タブ.Items=項目一覧;
        タブ.SelectedIndex=0;
        return タブ;
    }

    private SfDataGrid 結果グリッドを作成(動的ソース結果画面モデル 画面) {
        var グリッド=new SfDataGrid {
            AutomationId="動的結果.グリッド."+画面.対象,
            AutoGenerateColumnsMode=AutoGenerateColumnsMode.None,
            ItemsSource=画面.行一覧,
            RowHeight=30,
            HeaderRowHeight=34,
            AllowResizingColumns=true,
            SelectionMode=DataGridSelectionMode.Single,
            SelectionUnit=DataGridSelectionUnit.Row,
            NavigationMode=DataGridNavigationMode.Row,
            GridLinesVisibility=GridLinesVisibility.Both,
            HorizontalScrollBarVisibility=ScrollBarVisibility.Always,
            VerticalScrollBarVisibility=ScrollBarVisibility.Always,
            DefaultColumnWidth=90,
        };
        AutomationProperties.SetIsInAccessibleTree(グリッド,true);
        グリッド.SelectionChanged+=(_,_)=>{
            var 行=グリッド.SelectedRows.Cast<object>().Select(選択行を取得<動的結果行>).FirstOrDefault();
            if(行 is not null) {
                this.画面モデル.選択結果=行;
            }
        };
        グリッド.Columns.Add(new DataGridTemplateColumn {
            HeaderText="選択",
            MappingName=nameof(動的結果行.選択済みか),
            Width=55,
            AllowSorting=false,
            CellTemplate=new DataTemplate(()=>{
                var チェック=new CheckBox {
                    HorizontalOptions=LayoutOptions.Center,
                    VerticalOptions=LayoutOptions.Center,
                };
                チェック.SetBinding(CheckBox.IsCheckedProperty,nameof(動的結果行.選択済みか),BindingMode.TwoWay);
                return チェック;
            }),
        });
        this.列を追加(グリッド,"Status",nameof(動的結果行.状態),105);
        this.列を追加(グリッド,"Query",nameof(動的結果行.QueryIndex),90);
        this.列を追加(グリッド,"短縮式",nameof(動的結果行.短縮式),210);
        this.列を追加(グリッド,"Query式",nameof(動的結果行.クエリ式),360);
        this.列を追加(グリッド,"N",nameof(動的結果行.N),45);
        this.列を追加(グリッド,"Depth",nameof(動的結果行.深度),85);
        this.列を追加(グリッド,"生成",nameof(動的結果行.生成時間),90);
        this.列を追加(グリッド,"Compile",nameof(動的結果行.コンパイル時間),90);
        this.列を追加(グリッド,"System ns/op",nameof(動的結果行.System中央値),130);
        this.列を追加(グリッド,"System alloc",nameof(動的結果行.System割当),130);
        this.列を追加(グリッド,"FLinq ns/op",nameof(動的結果行.FLinq中央値),130);
        this.列を追加(グリッド,"FLinq alloc",nameof(動的結果行.FLinq割当),130);
        this.列を追加(グリッド,"FLinq比",nameof(動的結果行.FLinq比),95);
        this.列を追加(グリッド,"ZLinq ns/op",nameof(動的結果行.ZLinq中央値),130);
        this.列を追加(グリッド,"ZLinq alloc",nameof(動的結果行.ZLinq割当),130);
        this.列を追加(グリッド,"ZLinq比",nameof(動的結果行.ZLinq比),95);
        this.列を追加(グリッド,"意味一致",nameof(動的結果行.意味一致),100);
        this.列を追加(グリッド,"Rewrite",nameof(動的結果行.RewriteCount),110);
        this.列を追加(グリッド,"FixedPoint",nameof(動的結果行.FixedPoint),130);
        return グリッド;
    }

    private View 履歴グリッドを作成(bool ごみ箱か) {
        var レイアウト=new Grid {
            RowDefinitions={
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto),
            },
            Padding=new Thickness(8),
            RowSpacing=8,
        };
        var グリッド=new SfDataGrid {
            AutomationId=ごみ箱か?"動的履歴.ごみ箱":"動的履歴.一覧",
            AutoGenerateColumnsMode=AutoGenerateColumnsMode.None,
            ItemsSource=ごみ箱か?this.画面モデル.ごみ箱一覧:this.画面モデル.履歴一覧,
            SelectionMode=DataGridSelectionMode.Single,
            GridLinesVisibility=GridLinesVisibility.Both,
            AllowResizingColumns=true,
        };
        this.列を追加(グリッド,"Run ID",nameof(動的履歴項目.実行ID),260);
        this.列を追加(グリッド,"開始",nameof(動的履歴項目.開始日時),180);
        this.列を追加(グリッド,"Status",nameof(動的履歴項目.状態),110);
        this.列を追加(グリッド,"条件",nameof(動的履歴項目.条件),360);
        レイアウト.Add(グリッド,0,0);

        var ボタン列=new HorizontalStackLayout {
            Spacing=8,
            HorizontalOptions=LayoutOptions.End,
        };
        if(ごみ箱か) {
            var 復元=new Button {Text="復元",AutomationId="動的履歴.復元"};
            復元.Clicked+=(_,_)=>{
                var 項目=グリッド.SelectedRows.Cast<object>().Select(選択行を取得<動的履歴項目>).FirstOrDefault();
                if(項目 is not null&&this.画面モデル.復元コマンド.CanExecute(項目)) {
                    this.画面モデル.復元コマンド.Execute(項目);
                }
            };
            var 完全削除=new Button {Text="完全削除",AutomationId="動的履歴.完全削除"};
            完全削除.Clicked+=(_,_)=>{
                var 項目=グリッド.SelectedRows.Cast<object>().Select(選択行を取得<動的履歴項目>).FirstOrDefault();
                if(項目 is not null&&this.画面モデル.完全削除コマンド.CanExecute(項目)) {
                    this.画面モデル.完全削除コマンド.Execute(項目);
                }
            };
            ボタン列.Children.Add(復元);
            ボタン列.Children.Add(完全削除);
            this.実行中無効化一覧.Add(復元);
            this.実行中無効化一覧.Add(完全削除);
        }else{
            var 再実行=new Button {Text="Run再実行",AutomationId="動的履歴.Run再実行"};
            再実行.Clicked+=(_,_)=>{
                var 項目=グリッド.SelectedRows.Cast<object>().Select(選択行を取得<動的履歴項目>).FirstOrDefault();
                if(項目 is not null&&this.画面モデル.Run再実行コマンド.CanExecute(項目)) {
                    this.画面モデル.Run再実行コマンド.Execute(項目);
                }
            };
            var 設定復元=new Button {Text="設定復元＋新Seed",AutomationId="動的履歴.設定復元Seed再生成"};
            設定復元.Clicked+=(_,_)=>{
                var 項目=グリッド.SelectedRows.Cast<object>().Select(選択行を取得<動的履歴項目>).FirstOrDefault();
                if(項目 is not null&&this.画面モデル.設定復元Seed再生成コマンド.CanExecute(項目)) {
                    this.画面モデル.設定復元Seed再生成コマンド.Execute(項目);
                }
            };
            var 削除=new Button {Text="ごみ箱へ",AutomationId="動的履歴.ごみ箱へ"};
            削除.Clicked+=(_,_)=>{
                var 項目=グリッド.SelectedRows.Cast<object>().Select(選択行を取得<動的履歴項目>).FirstOrDefault();
                if(項目 is not null&&this.画面モデル.ごみ箱へ移動コマンド.CanExecute(項目)) {
                    this.画面モデル.ごみ箱へ移動コマンド.Execute(項目);
                }
            };
            ボタン列.Children.Add(再実行);
            ボタン列.Children.Add(設定復元);
            ボタン列.Children.Add(削除);
            this.実行中無効化一覧.Add(再実行);
            this.実行中無効化一覧.Add(設定復元);
            this.実行中無効化一覧.Add(削除);
        }
        レイアウト.Add(ボタン列,0,1);
        return レイアウト;
    }

    private Grid 設定領域を作成() {
        var ルート=new Grid {
            AutomationId="動的設定.領域",
            BackgroundColor=Colors.White,
            RowDefinitions={
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
            },
            Padding=new Thickness(10),
            RowSpacing=8,
        };
        AutomationProperties.SetIsInAccessibleTree(ルート,true);
        var 見出し=new Grid {
            ColumnDefinitions={
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
            },
        };
        見出し.Add(new Label {
            Text="実行設定",
            FontSize=18,
            FontAttributes=FontAttributes.Bold,
            VerticalTextAlignment=TextAlignment.Center,
        },0,0);
        var 折畳=new Button {
            Text="≡",
            WidthRequest=38,
            HeightRequest=34,
            Padding=0,
            AutomationId="動的設定.折畳",
            Command=this.画面モデル.設定領域折畳コマンド,
        };
        見出し.Add(折畳,1,0);
        ルート.Add(見出し,0,0);

        var 内容=new VerticalStackLayout {
            AutomationId="動的設定.内容",
            Spacing=10,
        };
        内容.Children.Add(new Label {Text="SourceTarget",FontAttributes=FontAttributes.Bold});
        foreach(var 項目 in this.画面モデル.ソース選択一覧) {
            var チェック=new CheckBox {
                AutomationId="動的設定.Source."+項目.対象,
                BindingContext=項目,
            };
            チェック.SetBinding(CheckBox.IsCheckedProperty,nameof(動的ソース選択項目.選択済みか),BindingMode.TwoWay);
            var 行=new HorizontalStackLayout {Spacing=4};
            行.Children.Add(チェック);
            行.Children.Add(new Label {Text=項目.表示名,VerticalTextAlignment=TextAlignment.Center});
            内容.Children.Add(行);
            this.設定入力一覧.Add(チェック);
        }
        内容.Children.Add(this.整数入力行を作成("Query数",nameof(動的ベンチマーク画面モデル.Query数),1,1000));
        内容.Children.Add(this.整数入力行を作成("N",nameof(動的ベンチマーク画面モデル.N),1,64));
        内容.Children.Add(this.整数入力行を作成("入力要素数",nameof(動的ベンチマーク画面モデル.入力要素数),1,100000));
        内容.Children.Add(this.整数入力行を作成("最大深度",nameof(動的ベンチマーク画面モデル.最大深度),1,32));

        var Seed見出し=new Label {Text="Seed",FontAttributes=FontAttributes.Bold};
        内容.Children.Add(Seed見出し);
        var Seed行=new Grid {
            ColumnDefinitions={
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
            },
            ColumnSpacing=6,
        };
        var Seed入力=new Entry {
            AutomationId="動的設定.Seed",
            FontFamily="Consolas",
            FontSize=12,
        };
        Seed入力.SetBinding(Entry.TextProperty,nameof(動的ベンチマーク画面モデル.Seed文字列),BindingMode.TwoWay);
        Seed行.Add(Seed入力,0,0);
        var Seed生成=new Button {
            Text="新規",
            AutomationId="動的設定.Seed新規",
            Command=this.画面モデル.新規Seedコマンド,
        };
        Seed行.Add(Seed生成,1,0);
        内容.Children.Add(Seed行);
        this.設定入力一覧.Add(Seed入力);
        this.設定入力一覧.Add(Seed生成);

        内容.Children.Add(new Label {Text="Operator Set",FontAttributes=FontAttributes.Bold});
        var モード行=new HorizontalStackLayout {Spacing=12};
        var Common=new RadioButton {Content="Common",GroupName="動的演算子集合",AutomationId="動的設定.Common"};
        Common.SetBinding(RadioButton.IsCheckedProperty,nameof(動的ベンチマーク画面モデル.Common選択か),BindingMode.TwoWay);
        var Maximum=new RadioButton {Content="Maximum",GroupName="動的演算子集合",AutomationId="動的設定.Maximum"};
        Maximum.SetBinding(RadioButton.IsCheckedProperty,nameof(動的ベンチマーク画面モデル.Maximum選択か),BindingMode.TwoWay);
        モード行.Children.Add(Common);
        モード行.Children.Add(Maximum);
        内容.Children.Add(モード行);
        this.設定入力一覧.Add(Common);
        this.設定入力一覧.Add(Maximum);

        var 実行行=new Grid {
            ColumnDefinitions={
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
            },
            ColumnSpacing=8,
        };
        var 実行=new Button {
            Text="実行",
            AutomationId="動的設定.実行",
            Command=this.画面モデル.実行コマンド,
            BackgroundColor=Color.FromArgb("#2563EB"),
            TextColor=Colors.White,
        };
        実行行.Add(実行,0,0);
        var キャンセル=new Button {
            Text="キャンセル",
            AutomationId="動的設定.キャンセル",
            Command=this.画面モデル.キャンセルコマンド,
        };
        実行行.Add(キャンセル,1,0);
        内容.Children.Add(実行行);

        内容.Children.Add(new BoxView {HeightRequest=1,Color=Color.FromArgb("#CBD5E1")});
        var 進捗=new Label {FontAttributes=FontAttributes.Bold,LineBreakMode=LineBreakMode.WordWrap};
        進捗.SetBinding(Label.TextProperty,nameof(動的ベンチマーク画面モデル.進捗文字列));
        内容.Children.Add(進捗);
        内容.Children.Add(this.進捗数行を作成("完了",nameof(動的ベンチマーク画面モデル.完了数)));
        内容.Children.Add(this.進捗数行を作成("コンパイル失敗",nameof(動的ベンチマーク画面モデル.コンパイル失敗数)));
        内容.Children.Add(this.進捗数行を作成("意味不一致",nameof(動的ベンチマーク画面モデル.意味不一致数)));
        内容.Children.Add(this.進捗数行を作成("非収束",nameof(動的ベンチマーク画面モデル.非収束数)));
        内容.Children.Add(this.進捗数行を作成("実行例外",nameof(動的ベンチマーク画面モデル.実行例外数)));

        var スクロール=new ScrollView {Content=内容};
        ルート.Add(スクロール,0,1);
        ルート.PropertyChanged+=(_,引数)=>{
            if(引数.PropertyName==nameof(VisualElement.IsVisible)) {
                スクロール.IsVisible=ルート.IsVisible;
            }
        };
        ルート.Resources.Add("内容",スクロール);
        return ルート;
    }

    private Grid 詳細領域を作成() {
        var ルート=new Grid {
            AutomationId="動的詳細.領域",
            BackgroundColor=Colors.White,
            RowDefinitions={
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
            },
        };
        var 見出し=new Grid {
            ColumnDefinitions={
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto),
            },
            Padding=new Thickness(8,2),
        };
        見出し.Add(new Label {
            Text="Query詳細",
            FontAttributes=FontAttributes.Bold,
            VerticalTextAlignment=TextAlignment.Center,
        },0,0);
        var Query再実行=new Button {
            Text="Query再実行",
            AutomationId="動的詳細.Query再実行",
            Padding=new Thickness(10,2),
        };
        Query再実行.Clicked+=(_,_)=>{
            var 行=this.画面モデル.選択結果;
            if(行 is not null&&this.画面モデル.Query再実行コマンド.CanExecute(行)) {
                this.画面モデル.Query再実行コマンド.Execute(行);
            }
        };
        見出し.Add(Query再実行,1,0);
        this.実行中無効化一覧.Add(Query再実行);
        var Query縮小=new Button {
            Text="失敗を縮小",
            AutomationId="動的詳細.Query縮小",
            Padding=new Thickness(10,2),
        };
        Query縮小.Clicked+=(_,_)=>{
            var 行=this.画面モデル.選択結果;
            if(行 is not null&&this.画面モデル.Query縮小コマンド.CanExecute(行)) {
                this.画面モデル.Query縮小コマンド.Execute(行);
            }
        };
        見出し.Add(Query縮小,2,0);
        this.実行中無効化一覧.Add(Query縮小);
        var 選択Query再実行=new Button {
            Text="選択を再実行",
            AutomationId="動的詳細.選択Query再実行",
            Padding=new Thickness(10,2),
        };
        選択Query再実行.Clicked+=(_,_)=>{
            var 対象一覧=this.画面モデル.結果一覧.Where(static 行=>行.選択済みか).ToList();
            if(this.画面モデル.選択Query再実行コマンド.CanExecute(対象一覧)) {
                this.画面モデル.選択Query再実行コマンド.Execute(対象一覧);
            }
        };
        見出し.Add(選択Query再実行,3,0);
        this.実行中無効化一覧.Add(選択Query再実行);
        var コピー=new Button {
            Text="Sourceをコピー",
            AutomationId="動的詳細.Sourceコピー",
            Padding=new Thickness(10,2),
        };
        コピー.Clicked+=async(_,_)=>{
            if(this.画面モデル.選択結果?.生成Source is {Length:>0} Source) {
                await Clipboard.Default.SetTextAsync(Source);
            }
        };
        見出し.Add(コピー,4,0);
        var 折畳=new Button {
            Text="⌄",
            AutomationId="動的詳細.折畳",
            Command=this.画面モデル.詳細領域折畳コマンド,
            Padding=new Thickness(10,2),
        };
        見出し.Add(折畳,5,0);
        ルート.Add(見出し,0,0);

        var タブ=new SfTabView {
            TabBarHeight=30,
            EnableSwiping=false,
            ContentTransitionDuration=0,
            HeaderItemSpacing=1,
            TabBarBackground=Color.FromArgb("#E2E8F0"),
        };
        タブ.Items=[
            this.詳細タブを作成("Summary",nameof(動的結果行.概要)),
            this.詳細タブを作成("Query",nameof(動的結果行.クエリ式)),
            this.詳細タブを作成("Source",nameof(動的結果行.生成Source)),
            this.詳細タブを作成("IR",nameof(動的結果行.IR)),
            this.詳細タブを作成("Results",nameof(動的結果行.結果)),
            this.詳細タブを作成("Diagnostics",nameof(動的結果行.診断)),
            this.最適化Traceタブを作成(),
            this.詳細タブを作成("Measurements",nameof(動的結果行.計測詳細)),
        ];
        タブ.SelectedIndex=0;
        ルート.Add(タブ,0,1);
        ルート.Resources.Add("タブ",タブ);
        return ルート;
    }

    private SfTabItem 詳細タブを作成(string 見出し,string プロパティ) {
        var 編集欄=new Editor {
            IsReadOnly=true,
            FontFamily="Consolas",
            FontSize=15,
            AutoSize=EditorAutoSizeOption.Disabled,
            BackgroundColor=Color.FromArgb("#F8FAFC"),
        };
        編集欄.SetBinding(Editor.TextProperty,"選択結果."+プロパティ);
        return new SfTabItem {Header=見出し,Content=編集欄};
    }

    private SfTabItem 最適化Traceタブを作成() {
        var 一覧=new CollectionView {
            AutomationId="動的詳細.OptimizationTraceTree",
            SelectionMode=SelectionMode.None,
            ItemsLayout=new LinearItemsLayout(ItemsLayoutOrientation.Vertical){ItemSpacing=2},
            BackgroundColor=Color.FromArgb("#F8FAFC"),
            ItemTemplate=new DataTemplate(static()=>{
                var 外枠=new Border {
                    Stroke=Color.FromArgb("#CBD5E1"),
                    StrokeThickness=1,
                    Padding=new Thickness(6,3),
                    BackgroundColor=Colors.White,
                };
                外枠.SetBinding(View.MarginProperty,nameof(動的最適化Trace表示行.左余白));
                var 内容=new Grid {
                    RowDefinitions={
                        new RowDefinition(GridLength.Auto),
                        new RowDefinition(GridLength.Auto),
                        new RowDefinition(GridLength.Auto),
                    },
                    ColumnDefinitions={
                        new ColumnDefinition(GridLength.Star),
                        new ColumnDefinition(GridLength.Star),
                    },
                    ColumnSpacing=6,
                };
                var 見出し=new Label {FontAttributes=FontAttributes.Bold,FontSize=12};
                見出し.SetBinding(Label.TextProperty,nameof(動的最適化Trace表示行.見出し));
                内容.Add(見出し,0,0);
                Grid.SetColumnSpan(見出し,2);
                var 詳細=new Label {FontFamily="Consolas",FontSize=10,TextColor=Color.FromArgb("#475569")};
                詳細.SetBinding(Label.TextProperty,nameof(動的最適化Trace表示行.詳細));
                内容.Add(詳細,0,1);
                Grid.SetColumnSpan(詳細,2);
                var Before=new Label {
                    FontFamily="Consolas",
                    FontSize=10,
                    BackgroundColor=Color.FromArgb("#FEF2F2"),
                    Padding=new Thickness(4),
                    LineBreakMode=LineBreakMode.WordWrap,
                };
                Before.SetBinding(Label.TextProperty,nameof(動的最適化Trace表示行.Before),stringFormat:"before\n{0}");
                Before.SetBinding(VisualElement.IsVisibleProperty,nameof(動的最適化Trace表示行.差分あり));
                内容.Add(Before,0,2);
                var After=new Label {
                    FontFamily="Consolas",
                    FontSize=10,
                    BackgroundColor=Color.FromArgb("#F0FDF4"),
                    Padding=new Thickness(4),
                    LineBreakMode=LineBreakMode.WordWrap,
                };
                After.SetBinding(Label.TextProperty,nameof(動的最適化Trace表示行.After),stringFormat:"after\n{0}");
                After.SetBinding(VisualElement.IsVisibleProperty,nameof(動的最適化Trace表示行.差分あり));
                内容.Add(After,1,2);
                外枠.Content=内容;
                return 外枠;
            }),
        };
        一覧.SetBinding(ItemsView.ItemsSourceProperty,"選択結果."+nameof(動的結果行.最適化Trace表示一覧));
        return new SfTabItem {Header="Optimization Trace",Content=一覧};
    }

    private View 整数入力行を作成(string 見出し,string プロパティ,int 最小値,int 最大値) {
        var 行=new Grid {
            ColumnDefinitions={
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(110),
            },
            ColumnSpacing=8,
        };
        行.Add(new Label {
            Text=$"{見出し} ({最小値:N0}–{最大値:N0})",
            VerticalTextAlignment=TextAlignment.Center,
        },0,0);
        var 入力=new Entry {
            AutomationId="動的設定."+見出し,
            Keyboard=Keyboard.Numeric,
            HorizontalTextAlignment=TextAlignment.End,
        };
        入力.SetBinding(Entry.TextProperty,new Binding(プロパティ,BindingMode.TwoWay));
        行.Add(入力,1,0);
        this.設定入力一覧.Add(入力);
        return 行;
    }

    private View 進捗数行を作成(string 見出し,string プロパティ) {
        var 行=new Grid {
            ColumnDefinitions={
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
            },
        };
        行.Add(new Label {Text=見出し,FontSize=12},0,0);
        var 値=new Label {FontSize=12,HorizontalTextAlignment=TextAlignment.End};
        値.SetBinding(Label.TextProperty,プロパティ);
        行.Add(値,1,0);
        return 行;
    }

    private void 列を追加(SfDataGrid グリッド,string 見出し,string プロパティ,double 幅) {
        グリッド.Columns.Add(new DataGridTemplateColumn {
            HeaderText=見出し,
            MappingName=プロパティ,
            Width=幅,
            AllowSorting=true,
            CellTemplate=セルテンプレートを作成(プロパティ),
        });
    }

    private static DataTemplate セルテンプレートを作成(string プロパティ) {
        return new DataTemplate(() => {
            var ラベル=new Label {
                FontSize=12,
                LineBreakMode=LineBreakMode.TailTruncation,
                VerticalTextAlignment=TextAlignment.Center,
                Padding=new Thickness(4,0),
            };
            ラベル.SetBinding(Label.TextProperty,プロパティ);
            return ラベル;
        });
    }

    private void 画面モデル変更時(object? 送信元,PropertyChangedEventArgs 引数) {
        if(引数.PropertyName is nameof(動的ベンチマーク画面モデル.設定領域幅)
            or nameof(動的ベンチマーク画面モデル.詳細領域高さ)
            or nameof(動的ベンチマーク画面モデル.設定領域折畳済みか)
            or nameof(動的ベンチマーク画面モデル.詳細領域折畳済みか)
            or nameof(動的ベンチマーク画面モデル.設定入力可能か)) {
            this.画面状態を適用();
        }
    }

    private void 画面状態を適用() {
        this.ルート.ColumnDefinitions[2].Width=this.画面モデル.設定領域折畳済みか?44:this.画面モデル.設定領域幅;
        this.ルート.RowDefinitions[2].Height=this.画面モデル.詳細領域折畳済みか?40:this.画面モデル.詳細領域高さ;
        if(this.設定領域.Resources.TryGetValue("内容",out var 設定内容)&&設定内容 is VisualElement 設定要素) {
            設定要素.IsVisible=!this.画面モデル.設定領域折畳済みか;
        }
        if(this.詳細領域.Resources.TryGetValue("タブ",out var 詳細内容)&&詳細内容 is VisualElement 詳細要素) {
            詳細要素.IsVisible=!this.画面モデル.詳細領域折畳済みか;
        }
        foreach(var 入力 in this.設定入力一覧) {
            入力.IsEnabled=this.画面モデル.設定入力可能か;
        }
        foreach(var 操作 in this.実行中無効化一覧) {
            操作.IsEnabled=this.画面モデル.設定入力可能か;
        }
    }

    private static T? 選択行を取得<T>(object? 行) where T:class {
        return 行 switch {
            T 値=>値,
            DataGridRowInfo {RowData:T 値}=>値,
            DataRowBase {RowData:T 値}=>値,
            _=>null,
        };
    }
}
