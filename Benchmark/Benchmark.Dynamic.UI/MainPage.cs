using Benchmark.Dynamic.Core;
using Syncfusion.Maui.Core.Internals;
using Syncfusion.Maui.DataGrid;
using Syncfusion.Maui.Toolkit.TabView;
using System.ComponentModel;
using UI;

namespace Benchmark.Dynamic.UI;

public sealed class MainPage:ContentPage {
    private readonly Shared.Benchmark.History.測定設定画面 測定設定=new();
    private readonly 動的ベンチマーク画面モデル 画面モデル;
    private readonly 動的ライブ検証画面 ライブ検証;
    private readonly 固定理想比較画面 理想比較;
    private readonly Grid ルート;
    private readonly Grid 詳細領域;
    private readonly List<VisualElement> 設定入力一覧=[];
    private readonly List<VisualElement> 実行中無効化一覧=[];

    public MainPage(動的ベンチマーク画面モデル 画面モデル,動的ライブ検証サービス ライブ検証サービス) {
        this.画面モデル=画面モデル;
        this.ライブ検証=new 動的ライブ検証画面(ライブ検証サービス);
        this.理想比較=new(()=>this.測定設定.最適化フラグ);
        this.BindingContext=画面モデル;
        this.Title="Dynamic LINQ Benchmark";
        this.BackgroundColor=Color.FromArgb("#F4F7FB");

        this.ルート=new Grid {
            RowDefinitions={
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(6),
                new RowDefinition(画面モデル.詳細領域高さ),
            },
            RowSpacing=0,
            Padding=new Thickness(10),
        };

        this.ルート.Add(this.設定ストリップを作成(),0,0);
        this.ルート.Add(this.選択進捗ヘッダーを作成(),0,1);

        var 結果領域=this.結果領域を作成();
        this.ルート.Add(結果領域,0,2);

        var 詳細分割=new 動的GridSplitter(動的分割方向.水平,差分=>{
            if(!this.画面モデル.詳細領域折畳済みか) {
                this.画面モデル.詳細領域高さ-=差分;
            }
        });
        this.ルート.Add(詳細分割,0,3);

        this.詳細領域=this.詳細領域を作成();
        this.ルート.Add(this.詳細領域,0,4);

        this.画面モデル.測定設定を取得=()=>this.ストレージ設定を作る();
        this.ライブ検証.測定設定を取得=()=>this.ストレージ設定を作る();
        this.Content=this.ルート;
        this.画面モデル.PropertyChanged+=this.画面モデル変更時;
        this.画面状態を適用();
    }

    private (int 最適化,WorkerStorageSettings 設定) ストレージ設定を作る(){
        var 明示=this.測定設定.明示設定;
        return (this.測定設定.最適化フラグ,new WorkerStorageSettings(this.測定設定.プロファイル,this.測定設定.拡張,this.測定設定.保存,
            IndexBytes:明示.IndexBytes,MemoryBytes:明示.LogMemoryBytes,MemoryLimitBytes:this.測定設定.メモリ予算,DiskLimitBytes:this.測定設定.ディスク予算,ProfileCatalogJSON:this.測定設定.プロファイルJSON,IOMode:this.測定設定.入出力,AccessPattern:this.測定設定.アクセス));
    }

    public void ライブ検証を終了する() {
        this.ライブ検証.終了する();
        this.理想比較.終了する();
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
            new SfTabItem {
                Header="ライブ検証",
                Content=this.ライブ検証,
            },
            new SfTabItem{Header="理想コード",Content=this.理想比較},
            new SfTabItem{Header="測定設定",Content=this.測定設定},
            new SfTabItem{Header="共有履歴・比較",Content=new Shared.Benchmark.History.履歴比較画面(Benchmark.Dynamic.Infrastructure.DynamicDatabasePathResolver.Resolve())},
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
                HeaderContent=this.ソースタブ見出しを作成(画面,対象.GetSourceKind().ToString()),
                Content=this.結果グリッドを作成(画面),
            });
        }
        タブ.Items=項目一覧;
        タブ.SelectedIndex=0;
        return タブ;
    }

    private View ソースタブ見出しを作成(動的ソース結果画面モデル 画面,string ラベル) {
        var 進行=new ベンチマーク円形進行ビュー {
            WidthRequest=16,
            HeightRequest=16,
            VerticalOptions=LayoutOptions.Center,
            HorizontalOptions=LayoutOptions.Center,
        };
        進行.SetBinding(ベンチマーク円形進行ビュー.進行プロパティ,nameof(動的ソース結果画面モデル.進行割合));
        進行.SetBinding(ベンチマーク円形進行ビュー.輪郭色プロパティ,nameof(動的ソース結果画面モデル.進行外枠色));
        進行.SetBinding(IsVisibleProperty,nameof(動的ソース結果画面モデル.進行リング表示中か));

        var 完了アイコン=new Image {
            WidthRequest=13,
            HeightRequest=13,
            InputTransparent=true,
            VerticalOptions=LayoutOptions.Center,
            HorizontalOptions=LayoutOptions.Center,
        };
        完了アイコン.SetBinding(Image.SourceProperty,new Binding(nameof(動的ソース結果画面モデル.完了アイコンソース),converter: new 画像ファイルソースコンバーター()));
        完了アイコン.SetBinding(IsVisibleProperty,nameof(動的ソース結果画面モデル.完了アイコン表示中か));

        var スピナー=new ActivityIndicator {
            WidthRequest=16,
            HeightRequest=16,
            Color=Color.FromArgb("#22C55E"),
        };
        スピナー.SetBinding(ActivityIndicator.IsRunningProperty,nameof(動的ソース結果画面モデル.実行スピナー表示中か));
        スピナー.SetBinding(IsVisibleProperty,nameof(動的ソース結果画面モデル.実行スピナー表示中か));

        var 進行ホスト=new Grid {
            WidthRequest=17,
            HeightRequest=17,
            Margin=new Thickness(0,0,3,0),
            Children={進行,完了アイコン,スピナー},
        };
        進行ホスト.SetBinding(IsVisibleProperty,nameof(動的ソース結果画面モデル.進行ありか));

        return new HorizontalStackLayout {
            Spacing=2,
            VerticalOptions=LayoutOptions.Center,
            BindingContext=画面,
            Children={進行ホスト,new Label {Text=ラベル,VerticalTextAlignment=TextAlignment.Center}},
        };
    }

    private SfDataGrid 結果グリッドを作成(動的ソース結果画面モデル 画面) {
        var グリッド=new SfDataGrid {
            AutomationId="動的結果.グリッド."+画面.対象,
            AutoGenerateColumnsMode=AutoGenerateColumnsMode.None,
            ItemsSource=画面.行一覧,
            RowHeight=30,
            HeaderRowHeight=36,
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
            MinimumWidth=55,
            HeaderLineBreakMode=LineBreakMode.NoWrap,
            AllowSorting=false,
            HeaderTemplate=this.選択列見出しテンプレートを作成(画面.対象),
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

    private View 設定ストリップを作成() {
        var ルート=new Grid {
            AutomationId="動的設定.領域",
            BackgroundColor=Colors.White,
            RowDefinitions={
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
            },
            Padding=new Thickness(8,4),
            RowSpacing=4,
        };
        AutomationProperties.SetIsInAccessibleTree(ルート,true);

        var Source行=new HorizontalStackLayout {Spacing=12};
        Source行.Children.Add(new Label {Text="SourceTarget",FontAttributes=FontAttributes.Bold,VerticalTextAlignment=TextAlignment.Center});
        foreach(var 項目 in this.画面モデル.ソース選択一覧) {
            var チェック=new CheckBox {
                AutomationId="動的設定.Source."+項目.対象,
                BindingContext=項目,
            };
            チェック.SetBinding(CheckBox.IsCheckedProperty,nameof(動的ソース選択項目.選択済みか),BindingMode.TwoWay);
            var 項目行=new HorizontalStackLayout {Spacing=2};
            項目行.Children.Add(チェック);
            項目行.Children.Add(new Label {Text=項目.表示名,VerticalTextAlignment=TextAlignment.Center,FontSize=12});
            Source行.Children.Add(項目行);
            this.設定入力一覧.Add(チェック);
        }
        ルート.Add(Source行,0,0);

        var 設定行=new HorizontalStackLayout {Spacing=14};
        設定行.Children.Add(this.整数入力欄を作成("Query数",nameof(動的ベンチマーク画面モデル.Query数)));
        設定行.Children.Add(this.整数入力欄を作成("N",nameof(動的ベンチマーク画面モデル.N)));
        設定行.Children.Add(this.整数入力欄を作成("入力要素数",nameof(動的ベンチマーク画面モデル.入力要素数)));
        設定行.Children.Add(this.整数入力欄を作成("最大深度",nameof(動的ベンチマーク画面モデル.最大深度)));

        var Seed行=new HorizontalStackLayout {Spacing=4};
        Seed行.Children.Add(new Label {Text="Seed",VerticalTextAlignment=TextAlignment.Center,FontSize=12});
        var Seed入力=new Entry {
            AutomationId="動的設定.Seed",
            FontFamily="Consolas",
            FontSize=12,
            WidthRequest=180,
        };
        Seed入力.SetBinding(Entry.TextProperty,nameof(動的ベンチマーク画面モデル.Seed文字列),BindingMode.TwoWay);
        Seed行.Children.Add(Seed入力);
        var Seed生成=new Button {
            Text="新規",
            AutomationId="動的設定.Seed新規",
            Command=this.画面モデル.新規Seedコマンド,
            Padding=new Thickness(8,2),
        };
        Seed行.Children.Add(Seed生成);
        設定行.Children.Add(Seed行);
        this.設定入力一覧.Add(Seed入力);
        this.設定入力一覧.Add(Seed生成);

        var モード行=new HorizontalStackLayout {Spacing=8};
        var Common=new RadioButton {Content="Common",GroupName="動的演算子集合",AutomationId="動的設定.Common"};
        Common.SetBinding(RadioButton.IsCheckedProperty,nameof(動的ベンチマーク画面モデル.Common選択か),BindingMode.TwoWay);
        var Maximum=new RadioButton {Content="Maximum",GroupName="動的演算子集合",AutomationId="動的設定.Maximum"};
        Maximum.SetBinding(RadioButton.IsCheckedProperty,nameof(動的ベンチマーク画面モデル.Maximum選択か),BindingMode.TwoWay);
        モード行.Children.Add(Common);
        モード行.Children.Add(Maximum);
        設定行.Children.Add(モード行);
        this.設定入力一覧.Add(Common);
        this.設定入力一覧.Add(Maximum);

        var 要素型行=new HorizontalStackLayout {Spacing=4};
        要素型行.Children.Add(new Label {Text="要素型",VerticalTextAlignment=TextAlignment.Center,FontSize=12});
        var 要素型選択=new Picker {
            AutomationId="動的設定.要素型",
            FontSize=12,
            WidthRequest=150,
            ItemsSource=this.画面モデル.要素型一覧.ToList(),
        };
        要素型選択.SetBinding(Picker.SelectedItemProperty,nameof(動的ベンチマーク画面モデル.要素型),BindingMode.TwoWay);
        要素型行.Children.Add(要素型選択);
        設定行.Children.Add(要素型行);
        this.設定入力一覧.Add(要素型選択);

        var 実行=new Button {
            Text="実行",
            AutomationId="動的設定.実行",
            Command=this.画面モデル.実行コマンド,
            BackgroundColor=Color.FromArgb("#2563EB"),
            TextColor=Colors.White,
            Padding=new Thickness(12,2),
        };
        設定行.Children.Add(実行);
        var キャンセル=new Button {
            Text="キャンセル",
            AutomationId="動的設定.キャンセル",
            Command=this.画面モデル.キャンセルコマンド,
            Padding=new Thickness(12,2),
        };
        設定行.Children.Add(キャンセル);
        ルート.Add(設定行,0,1);

        return ルート;
    }

    private View 選択進捗ヘッダーを作成() {
        var ルート=new Grid {
            AutomationId="動的結果.選択進捗ヘッダー",
            BackgroundColor=Colors.White,
            Padding=new Thickness(8,4),
            ColumnDefinitions={
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
            },
        };
        AutomationProperties.SetIsInAccessibleTree(ルート,true);

        var 全選択チェック=new CheckBox {
            InputTransparent=true,
            AutomationId="動的結果.全選択チェック",
        };
        全選択チェック.SetBinding(CheckBox.IsCheckedProperty,nameof(動的ベンチマーク画面モデル.全選択済みか),BindingMode.OneWay);
        var 全選択ホスト=new Grid {
            WidthRequest=30,
            BackgroundColor=Colors.Transparent,
            AutomationId="動的結果.全選択チェックホスト",
            Children={全選択チェック},
        };
        var 全選択タップ=new TapGestureRecognizer();
        全選択タップ.Tapped+=(_,_)=>this.画面モデル.全選択を切り替える();
        全選択ホスト.GestureRecognizers.Add(全選択タップ);
        ルート.Add(全選択ホスト,0,0);

        var 進捗行=new HorizontalStackLayout {Spacing=16,VerticalOptions=LayoutOptions.Center};
        var 進捗=new Label {FontAttributes=FontAttributes.Bold,VerticalTextAlignment=TextAlignment.Center};
        進捗.SetBinding(Label.TextProperty,nameof(動的ベンチマーク画面モデル.進捗文字列));
        進捗行.Children.Add(進捗);
        進捗行.Children.Add(this.進捗数行を作成("完了",nameof(動的ベンチマーク画面モデル.完了数)));
        進捗行.Children.Add(this.進捗数行を作成("コンパイル失敗",nameof(動的ベンチマーク画面モデル.コンパイル失敗数)));
        進捗行.Children.Add(this.進捗数行を作成("意味不一致",nameof(動的ベンチマーク画面モデル.意味不一致数)));
        進捗行.Children.Add(this.進捗数行を作成("非収束",nameof(動的ベンチマーク画面モデル.非収束数)));
        進捗行.Children.Add(this.進捗数行を作成("実行例外",nameof(動的ベンチマーク画面モデル.実行例外数)));
        ルート.Add(進捗行,1,0);

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
            new SfTabItem {Header="CPU",Content=this.CPUタブを作成()},
            this.詳細タブを作成("Summary",nameof(動的結果行.概要)),
            this.詳細タブを作成("Query",nameof(動的結果行.クエリ式)),
            this.詳細タブを作成("Source",nameof(動的結果行.生成Source)),
            this.詳細タブを作成("IR",nameof(動的結果行.IR)),
            this.詳細タブを作成("Results",nameof(動的結果行.結果)),
            this.詳細タブを作成("Diagnostics",nameof(動的結果行.診断)),
            this.最適化Traceタブを作成(),
            this.改善プロンプトタブを作成("速度改善プロンプト",nameof(動的結果行.速度改善プロンプト),"動的詳細.速度改善プロンプト"),
            this.改善プロンプトタブを作成("メモリ改善プロンプト",nameof(動的結果行.メモリ改善プロンプト),"動的詳細.メモリ改善プロンプト"),
        ];
        タブ.SelectedIndex=0;
        ルート.Add(タブ,0,1);
        ルート.Resources.Add("タブ",タブ);
        return ルート;
    }

    private View CPUタブを作成() {
        var 一覧=ベンチマークCPUトポロジープロバイダー.読み込む()
            .OrderBy(static CPU=>CPU.論理プロセッサインデックス)
            .Select(static CPU=>new 動的CPU行 {
                論理プロセッサインデックス=CPU.論理プロセッサインデックス,
                コア種別=CPU.コア種別,
                物理コアインデックス=CPU.物理コアインデックス,
                コアラベル=CPU.コアラベル,
            })
            .ToArray();
        var グリッド=new SfDataGrid {
            AutomationId="動的CPU.グリッド",
            AutoGenerateColumnsMode=AutoGenerateColumnsMode.None,
            ItemsSource=一覧,
            RowHeight=28,
            HeaderRowHeight=30,
            AllowResizingColumns=true,
            SelectionMode=DataGridSelectionMode.None,
            GridLinesVisibility=GridLinesVisibility.Both,
            DefaultColumnWidth=110,
        };
        this.列を追加(グリッド,"論理CPU",nameof(動的CPU行.論理プロセッサインデックス),90);
        this.列を追加(グリッド,"コア種別",nameof(動的CPU行.コア種別),100);
        this.列を追加(グリッド,"物理コア",nameof(動的CPU行.物理コアインデックス),90);
        this.列を追加(グリッド,"ラベル",nameof(動的CPU行.コアラベル),160);
        this.列を追加(グリッド,"状態",nameof(動的CPU行.実行中表示),90);
        this.画面モデル.CPU活動を検知した+=論理CPU=>{
            var 行=一覧.FirstOrDefault(行=>行.論理プロセッサインデックス==論理CPU);
            if(行 is not null) {
                MainThread.BeginInvokeOnMainThread(()=>行.実行中か=true);
            }
        };
        this.画面モデル.CPU活動をクリア+=()=>{
            MainThread.BeginInvokeOnMainThread(()=>{
                foreach(var 行 in 一覧) {
                    行.実行中か=false;
                }
            });
        };
        return グリッド;
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

    private SfTabItem 改善プロンプトタブを作成(string 見出し,string プロパティ,string AutomationId) {
        var 編集欄=new Editor {
            IsReadOnly=true,
            FontFamily="Consolas",
            FontSize=13,
            AutoSize=EditorAutoSizeOption.Disabled,
            BackgroundColor=Color.FromArgb("#F8FAFC"),
            Placeholder="Queryを選択してください",
        };
        編集欄.SetBinding(Editor.TextProperty,"選択結果."+プロパティ);
        var コピー=new Button {
            Text="コピー",
            AutomationId=AutomationId+".コピー",
            HorizontalOptions=LayoutOptions.End,
            Padding=new Thickness(12,2),
        };
        コピー.Clicked+=async(_,_)=>{
            if(!string.IsNullOrWhiteSpace(編集欄.Text)) {
                await Clipboard.Default.SetTextAsync(編集欄.Text);
            }
        };
        var 内容=new Grid {
            RowDefinitions={
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto),
            },
            RowSpacing=4,
            Padding=new Thickness(6),
            AutomationId=AutomationId,
        };
        内容.Add(編集欄,0,0);
        内容.Add(コピー,0,1);
        return new SfTabItem {Header=見出し,Content=内容};
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

    private View 整数入力欄を作成(string 見出し,string プロパティ) {
        var 行=new HorizontalStackLayout {Spacing=4,VerticalOptions=LayoutOptions.Center};
        行.Children.Add(new Label {Text=見出し,FontSize=12,VerticalTextAlignment=TextAlignment.Center});
        var 入力=new Entry {
            AutomationId="動的設定."+見出し,
            Keyboard=Keyboard.Numeric,
            WidthRequest=64,
            FontSize=12,
            HorizontalTextAlignment=TextAlignment.End,
        };
        入力.SetBinding(Entry.TextProperty,new Binding(プロパティ,BindingMode.TwoWay));
        行.Children.Add(入力);
        this.設定入力一覧.Add(入力);
        return 行;
    }

    private View 進捗数行を作成(string 見出し,string プロパティ) {
        var 行=new HorizontalStackLayout {Spacing=4,VerticalOptions=LayoutOptions.Center};
        行.Children.Add(new Label {Text=見出し+":",FontSize=12,VerticalTextAlignment=TextAlignment.Center});
        var 値=new Label {FontSize=12,FontAttributes=FontAttributes.Bold,VerticalTextAlignment=TextAlignment.Center};
        値.SetBinding(Label.TextProperty,プロパティ);
        行.Children.Add(値);
        return 行;
    }

    private DataTemplate 選択列見出しテンプレートを作成(SourceTarget 対象) {
        return new DataTemplate(()=>{
            var チェック=new CheckBox {
                InputTransparent=true,
                AutomationId="動的結果.タブ選択チェック."+対象,
            };
            チェック.IsChecked=this.画面モデル.タブ選択状態を取得(対象);
            EventHandler ハンドラー=(_,_)=>{
                var 値=this.画面モデル.タブ選択状態を取得(対象);
                MainThread.BeginInvokeOnMainThread(()=>チェック.IsChecked=値);
            };
            this.画面モデル.選択状態変更時+=ハンドラー;
            チェック.Unloaded+=(_,_)=>this.画面モデル.選択状態変更時-=ハンドラー;
            var ホスト=new Grid {
                WidthRequest=28,
                BackgroundColor=Colors.Transparent,
                Children={チェック},
            };
            var タップ=new TapGestureRecognizer();
            タップ.Tapped+=(_,_)=>this.画面モデル.全選択を切り替える(対象);
            ホスト.GestureRecognizers.Add(タップ);
            return ホスト;
        });
    }

    private void 列を追加(SfDataGrid グリッド,string 見出し,string プロパティ,double 幅) {
        グリッド.Columns.Add(new DataGridTemplateColumn {
            HeaderText=見出し,
            MappingName=プロパティ,
            Width=幅,
            MinimumWidth=幅,
            HeaderLineBreakMode=LineBreakMode.NoWrap,
            LineBreakMode=LineBreakMode.TailTruncation,
            HeaderPadding=new Thickness(4,0),
            AllowSorting=true,
            CellTemplate=セルテンプレートを作成(プロパティ),
        });
    }

    private static DataTemplate セルテンプレートを作成(string プロパティ) {
        return new DataTemplate(() => {
            var ラベル=new Label {
                FontSize=12,
                LineBreakMode=LineBreakMode.TailTruncation,
                MaxLines=1,
                VerticalTextAlignment=TextAlignment.Center,
                Padding=new Thickness(4,0),
            };
            ラベル.SetBinding(Label.TextProperty,プロパティ);
            return ラベル;
        });
    }

    private void 画面モデル変更時(object? 送信元,PropertyChangedEventArgs 引数) {
        if(引数.PropertyName is nameof(動的ベンチマーク画面モデル.詳細領域高さ)
            or nameof(動的ベンチマーク画面モデル.詳細領域折畳済みか)
            or nameof(動的ベンチマーク画面モデル.設定入力可能か)) {
            this.画面状態を適用();
        }
    }

    private void 画面状態を適用() {
        this.ルート.RowDefinitions[4].Height=this.画面モデル.詳細領域折畳済みか?40:this.画面モデル.詳細領域高さ;
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
