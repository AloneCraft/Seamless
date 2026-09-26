namespace Benchmark.Tsavorite.ParameterSweep.UI;

public sealed class MainPage:ContentPage{
    private readonly パラメータスイープビューモデル ビューモデル=new();
    private IDispatcherTimer? 実行予測更新タイマー;

    public MainPage(){
        this.Title="Tsavorite Parameter Sweep";
        this.BindingContext=this.ビューモデル;
        this.BackgroundColor=Color.FromArgb("#F8FAFC");
        this.Content=this.内容を作成();
        this.実行予測更新タイマーを開始();
        this.Unloaded+=this.アンロード時;
    }

    private void アンロード時(object? 送信元,EventArgs イベント引数){
        this.実行予測更新タイマー?.Stop();
    }

    private void 実行予測更新タイマーを開始(){
        this.実行予測更新タイマー=this.Dispatcher.CreateTimer();
        this.実行予測更新タイマー.Interval=TimeSpan.FromSeconds(1);
        this.実行予測更新タイマー.Tick+=(_,_)=>this.ビューモデル.実行予測を更新();
        this.実行予測更新タイマー.Start();
    }

    private View 内容を作成(){
        var 実行ボタン=new Button{Text="スイープ実行",AutomationId="パラメータスイープ.実行",HeightRequest=34,Padding=new Thickness(12,4)};
        実行ボタン.SetBinding(IsEnabledProperty,nameof(パラメータスイープビューモデル.実行可能か));
        実行ボタン.Clicked+=async(_,_)=>await this.ビューモデル.全件実行Async();

        var スモークボタン=new Button{Text="スモーク",AutomationId="パラメータスイープ.スモーク",HeightRequest=34,Padding=new Thickness(12,4)};
        スモークボタン.SetBinding(IsEnabledProperty,nameof(パラメータスイープビューモデル.実行可能か));
        スモークボタン.Clicked+=async(_,_)=>await this.ビューモデル.スモーク実行Async();

        var 更新ボタン=new Button{Text="実行履歴を更新",AutomationId="パラメータスイープ.実行履歴更新",HeightRequest=34,Padding=new Thickness(12,4)};
        更新ボタン.Clicked+=(_,_)=>this.ビューモデル.実行履歴を更新();

        var 最大Count入力=new Entry{AutomationId="パラメータスイープ.最大Count",WidthRequest=110,Keyboard=Keyboard.Numeric};
        最大Count入力.SetBinding(Entry.TextProperty,nameof(パラメータスイープビューモデル.最大成功Count表示));
        var 手順数入力=new Entry{AutomationId="パラメータスイープ.手順数",WidthRequest=64,Keyboard=Keyboard.Numeric};
        手順数入力.SetBinding(Entry.TextProperty,nameof(パラメータスイープビューモデル.最大スイープ手順数表示));

        var 実行選択=new Picker{AutomationId="パラメータスイープ.前回実行",WidthRequest=260,ItemDisplayBinding=new Binding(nameof(global::UI.Storage.UI実行記録.表示名))};
        実行選択.SetBinding(Picker.ItemsSourceProperty,nameof(パラメータスイープビューモデル.実行履歴));
        実行選択.SetBinding(Picker.SelectedItemProperty,nameof(パラメータスイープビューモデル.選択前回実行));

        var ツールバー=new Grid{
            Padding=new Thickness(10,8),ColumnSpacing=8,
            ColumnDefinitions={
                new ColumnDefinition{Width=GridLength.Auto},new ColumnDefinition{Width=GridLength.Auto},new ColumnDefinition{Width=GridLength.Auto},
                new ColumnDefinition{Width=GridLength.Auto},new ColumnDefinition{Width=GridLength.Auto},new ColumnDefinition{Width=GridLength.Auto},
                new ColumnDefinition{Width=GridLength.Auto},new ColumnDefinition{Width=GridLength.Auto},new ColumnDefinition{Width=GridLength.Star},
            },
        };
        ツールバー.Add(実行ボタン,0,0);ツールバー.Add(スモークボタン,1,0);ツールバー.Add(更新ボタン,2,0);
        ツールバー.Add(ラベルを作成("最大Count"),3,0);ツールバー.Add(最大Count入力,4,0);
        ツールバー.Add(ラベルを作成("手順数"),5,0);ツールバー.Add(手順数入力,6,0);ツールバー.Add(実行選択,7,0);

        var 状態=new Label{AutomationId="パラメータスイープ.状態",FontSize=12,TextColor=Color.FromArgb("#334155"),Padding=new Thickness(10,0,10,8)};
        状態.SetBinding(Label.TextProperty,nameof(パラメータスイープビューモデル.状態表示));
        var 予測=new Label{
            FontSize=12,
            TextColor=Colors.Gray,
            VerticalTextAlignment=TextAlignment.Center,
            AutomationId=nameof(パラメータスイープビューモデル.実行予測概要文字列),
        };
        予測.SetBinding(Label.TextProperty,nameof(パラメータスイープビューモデル.実行予測概要文字列));
        var 状態行=new HorizontalStackLayout{Spacing=8,Padding=new Thickness(10,0,10,8)};
        状態行.Add(状態);
        状態行.Add(予測);

        var ヘッダー=this.ヘッダー行を作成();
        var 一覧=new CollectionView{AutomationId="パラメータスイープ.結果一覧",ItemSizingStrategy=ItemSizingStrategy.MeasureFirstItem,SelectionMode=SelectionMode.None,ItemTemplate=new DataTemplate(結果行を作成)};
        一覧.SetBinding(ItemsView.ItemsSourceProperty,nameof(パラメータスイープビューモデル.行一覧));
        var スクロール=new ScrollView{Orientation=ScrollOrientation.Horizontal,Content=new Grid{
            RowDefinitions={new RowDefinition{Height=GridLength.Auto},new RowDefinition{Height=GridLength.Star}},Children={ヘッダー,一覧},
        }};
        Grid.SetRow(一覧,1);
        var ルート=new Grid{RowDefinitions={new RowDefinition{Height=GridLength.Auto},new RowDefinition{Height=GridLength.Auto},new RowDefinition{Height=GridLength.Star}},Children={ツールバー,状態行,スクロール}};
        Grid.SetRow(状態行,1);
        Grid.SetRow(スクロール,2);
        return ルート;
    }

    private Grid ヘッダー行を作成(){
        var グリッド=結果グリッドを作成();グリッド.BackgroundColor=Color.FromArgb("#E2E8F0");
        ヘッダーを追加(グリッド,"ケース名",0);ヘッダーを追加(グリッド,"コレクション",1);ヘッダーを追加(グリッド,"ジェネリック",2);
        ヘッダーを追加(グリッド,"バックエンド",3);ヘッダーを追加(グリッド,"スイープ",4);ヘッダーを追加(グリッド,"インデックスサイズ",5);
        ヘッダーを追加(グリッド,"ページサイズ",6);ヘッダーを追加(グリッド,"セグメントサイズ",7);ヘッダーを追加(グリッド,"メモリサイズ",8);
        ヘッダーを追加(グリッド,"可変率",9);ヘッダーを追加(グリッド,"最大Count",10);ヘッダーを追加(グリッド,"失敗種別",11);
        ヘッダーを追加(グリッド,"追加時間",12);ヘッダーを追加(グリッド,"検索時間",13);ヘッダーを追加(グリッド,"Count差分",14);
        ヘッダーを追加(グリッド,"追加時間シェア",15);ヘッダーを追加(グリッド,"検索時間シェア",16);ヘッダーを追加(グリッド,"失敗変更",17);
        ヘッダーを追加(グリッド,"索引バケット数",18);ヘッダーを追加(グリッド,"溢れバケット数",19);
        ヘッダーを追加(グリッド,"メモリログバイト数",20);ヘッダーを追加(グリッド,"ディスクログバイト数",21);
        return グリッド;
    }

    private static Grid 結果行を作成(){
        var グリッド=結果グリッドを作成();グリッド.BackgroundColor=Colors.White;
        バインドを追加(グリッド,nameof(パラメータスイープ比較表示行.ケース名),0);バインドを追加(グリッド,nameof(パラメータスイープ比較表示行.コレクション),1);
        バインドを追加(グリッド,nameof(パラメータスイープ比較表示行.ジェネリック),2);バインドを追加(グリッド,nameof(パラメータスイープ比較表示行.バックエンド),3);
        バインドを追加(グリッド,nameof(パラメータスイープ比較表示行.スイープ),4);バインドを追加(グリッド,nameof(パラメータスイープ比較表示行.Indexサイズ),5);
        バインドを追加(グリッド,nameof(パラメータスイープ比較表示行.Pageサイズ),6);バインドを追加(グリッド,nameof(パラメータスイープ比較表示行.Segmentサイズ),7);
        バインドを追加(グリッド,nameof(パラメータスイープ比較表示行.Memoryサイズ),8);バインドを追加(グリッド,nameof(パラメータスイープ比較表示行.可変Fraction),9);
        バインドを追加(グリッド,nameof(パラメータスイープ比較表示行.最大成功Count),10);バインドを追加(グリッド,nameof(パラメータスイープ比較表示行.失敗種別),11);
        バインドを追加(グリッド,nameof(パラメータスイープ比較表示行.追加経過),12);バインドを追加(グリッド,nameof(パラメータスイープ比較表示行.検索平均),13);
        バインドを追加(グリッド,nameof(パラメータスイープ比較表示行.最大Count差分),14);
        シェアバインドを追加(グリッド,nameof(パラメータスイープ比較表示行.追加時間シェア表示),nameof(パラメータスイープ比較表示行.今回追加時間ゲージ幅),nameof(パラメータスイープ比較表示行.前回追加時間ゲージ幅),15);
        シェアバインドを追加(グリッド,nameof(パラメータスイープ比較表示行.検索時間シェア表示),nameof(パラメータスイープ比較表示行.今回検索時間ゲージ幅),nameof(パラメータスイープ比較表示行.前回検索時間ゲージ幅),16);
        バインドを追加(グリッド,nameof(パラメータスイープ比較表示行.失敗種別変更表示),17);
        バインドを追加(グリッド,nameof(パラメータスイープ比較表示行.索引バケット数),18);バインドを追加(グリッド,nameof(パラメータスイープ比較表示行.溢れバケット数),19);
        バインドを追加(グリッド,nameof(パラメータスイープ比較表示行.メモリログバイト数),20);バインドを追加(グリッド,nameof(パラメータスイープ比較表示行.ディスクログバイト数),21);
        return グリッド;
    }

    private static Grid 結果グリッドを作成()=>new(){HeightRequest=34,ColumnDefinitions={
        new ColumnDefinition{Width=360},new ColumnDefinition{Width=96},new ColumnDefinition{Width=160},new ColumnDefinition{Width=96},new ColumnDefinition{Width=116},
        new ColumnDefinition{Width=70},new ColumnDefinition{Width=70},new ColumnDefinition{Width=76},new ColumnDefinition{Width=78},new ColumnDefinition{Width=64},
        new ColumnDefinition{Width=96},new ColumnDefinition{Width=110},new ColumnDefinition{Width=86},new ColumnDefinition{Width=86},new ColumnDefinition{Width=86},
        new ColumnDefinition{Width=108},new ColumnDefinition{Width=108},new ColumnDefinition{Width=88},
        new ColumnDefinition{Width=120},new ColumnDefinition{Width=120},new ColumnDefinition{Width=150},new ColumnDefinition{Width=160},
    }};

    private static Label ラベルを作成(string 文字列)=>new(){Text=文字列,VerticalOptions=LayoutOptions.Center,FontSize=12,TextColor=Color.FromArgb("#334155")};
    private static void ヘッダーを追加(Grid グリッド,string 文字列,int 列){var ラベル=new Label{Text=文字列,Padding=new Thickness(6,4),FontSize=11,FontAttributes=FontAttributes.Bold,TextColor=Color.FromArgb("#0F172A"),VerticalTextAlignment=TextAlignment.Center};グリッド.Add(ラベル,列,0);}
    private static void バインドを追加(Grid グリッド,string パス,int 列){var ラベル=new Label{Padding=new Thickness(6,4),FontSize=11,TextColor=Color.FromArgb("#1E293B"),VerticalTextAlignment=TextAlignment.Center,LineBreakMode=LineBreakMode.TailTruncation};ラベル.SetBinding(Label.TextProperty,パス);グリッド.Add(ラベル,列,0);}
    private static void シェアバインドを追加(Grid グリッド,string 表示パス,string 今回幅パス,string 前回幅パス,int 列){
        var 表示=new Label{FontSize=10,HorizontalTextAlignment=TextAlignment.Center,TextColor=Color.FromArgb("#1E293B")};表示.SetBinding(Label.TextProperty,表示パス);
        var 今回=new BoxView{HeightRequest=4,Color=Color.FromArgb("#2563EB")};今回.SetBinding(VisualElement.WidthRequestProperty,今回幅パス);
        var 前回=new BoxView{HeightRequest=4,Color=Color.FromArgb("#F59E0B")};前回.SetBinding(VisualElement.WidthRequestProperty,前回幅パス);
        var ゲージ=new HorizontalStackLayout{Spacing=0,HorizontalOptions=LayoutOptions.Center,Children={今回,前回}};
        var セル=new Grid{Padding=new Thickness(4,1),RowDefinitions={new RowDefinition{Height=GridLength.Star},new RowDefinition{Height=4}},Children={表示,ゲージ}};Grid.SetRow(ゲージ,1);グリッド.Add(セル,列,0);
    }
}
