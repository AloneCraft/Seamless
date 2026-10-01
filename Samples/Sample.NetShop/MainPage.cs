using Microsoft.Maui.Controls.Shapes;
using Sample.NetShop.Controls;
using Sample.NetShop.Core;

namespace Sample.NetShop;

/// <summary>
/// 3列の画面。左=物語の進行、中央=説明と実行中のコード、右=動くデータとグラフ。
/// 起動すると自動で再生を始め、最後の章の後は最初から繰り返す。操作は任意。
/// </summary>
public sealed class MainPage:ContentPage{
    private const int ログの最大行数=14;
    private static readonly (string 表示,double 速度)[]速度一覧=[("0.5倍",0.5),("1倍",1),("2倍",2),("4倍",4),("待たない",0)];

    private readonly CancellationTokenSource 取消=new();
    private readonly List<(Border 枠,Label 印)>章の行一覧=[];
    private readonly Dictionary<string,(Label 値,ProgressBar 棒)>メーター一覧=[];
    private readonly Dictionary<string,Label>指標一覧=[];
    private readonly Dictionary<string,(GraphicsView 表示,棒グラフ描画 描画)>グラフ一覧=[];
    private readonly List<Label>コード行一覧=[];
    private readonly Label 周回表示=new(){TextColor=配色.薄い文字,VerticalOptions=LayoutOptions.Center};
    private readonly Label 状態表示=new(){TextColor=配色.薄い文字,VerticalOptions=LayoutOptions.Center};
    private readonly Label 章見出し=new(){FontSize=24,FontAttributes=FontAttributes.Bold,TextColor=配色.文字};
    private readonly Label 機能表示=new(){FontSize=13,TextColor=配色.アクセント};
    private readonly Label 説明表示=new(){FontSize=15,TextColor=配色.文字,LineHeight=1.3};
    private readonly VerticalStackLayout コード領域=new(){Spacing=0};
    private readonly VerticalStackLayout メーター領域=new(){Spacing=8};
    private readonly VerticalStackLayout 指標領域=new(){Spacing=2};
    private readonly VerticalStackLayout グラフ領域=new(){Spacing=10};
    private readonly VerticalStackLayout ログ領域=new(){Spacing=2};
    private readonly Button 一時停止ボタン=new(){Text="⏸ 一時停止",WidthRequest=120};
    private readonly Picker 速度選択=new(){WidthRequest=110};
    private 物語の再生器? 再生器;
    private Task? 再生タスク;
    private int 表示中の章=-1;

    public MainPage(){
        this.BackgroundColor=配色.背景;
        try{
            var 既定のデータフォルダ=System.IO.Path.Combine(AppContext.BaseDirectory,"NetShopData");
            var 設定=実行設定.引数から作る(Environment.GetCommandLineArgs().Skip(1).ToArray(),既定のデータフォルダ);
            this.再生器=new 物語の再生器(設定);
        }catch(ArgumentException 例外){
            this.Content=new Label{Text=例外.Message,TextColor=配色.失敗,Margin=24,FontSize=16};
            return;
        }
        this.Content=this.画面を作る(this.再生器);
        this.再生器.章状態変化時+=状態=>MainThread.BeginInvokeOnMainThread(()=>this.章の状態を表示する(状態));
        this.再生器.報告時+=(番号,報告)=>MainThread.BeginInvokeOnMainThread(()=>this.報告を表示する(番号,報告));
        this.再生器.周回開始時+=周回=>MainThread.BeginInvokeOnMainThread(()=>this.周回表示.Text=$"{周回} 周目");
        this.Loaded+=this.読み込み時;
    }

    /// <summary>再生を止め、商店DBが閉じられるまで待つ。</summary>
    public void 終了する(){
        this.取消.Cancel();
        try{
            this.再生タスク?.Wait(TimeSpan.FromSeconds(15));
        }catch(AggregateException){
            // 取り消しによる終了。失敗は再生中に画面へ出している。
        }
    }

    private void 読み込み時(object? 送り手,EventArgs 引数){
        if(this.再生器 is null||this.再生タスク is not null){
            return;
        }
        var 再生器=this.再生器;
        this.再生タスク=Task.Run(async()=>{
            try{
                await 再生器.再生Async(null,this.取消.Token);
            }catch(OperationCanceledException) when(this.取消.IsCancellationRequested){
            }catch(Exception 例外){
                MainThread.BeginInvokeOnMainThread(()=>{
                    this.状態表示.Text=$"再生を続けられません: {例外.Message}";
                    this.状態表示.TextColor=配色.失敗;
                });
            }
        });
    }

    // ---- 画面の組み立て ----

    private View 画面を作る(物語の再生器 再生器){
        var 全体=new Grid{
            RowDefinitions=[new RowDefinition(GridLength.Auto),new RowDefinition(GridLength.Star)],
            ColumnDefinitions=[new ColumnDefinition(new GridLength(250)),new ColumnDefinition(GridLength.Star),new ColumnDefinition(new GridLength(460))],
            Padding=12,
            RowSpacing=12,
            ColumnSpacing=12,
        };
        var 上部=this.上部を作る(再生器);
        全体.Add(上部,0,0);
        Grid.SetColumnSpan(上部,3);
        全体.Add(this.左列を作る(再生器),0,1);
        全体.Add(this.中央列を作る(),1,1);
        全体.Add(this.右列を作る(),2,1);
        return 全体;
    }

    private View 上部を作る(物語の再生器 再生器){
        foreach(var 項目 in 速度一覧){
            this.速度選択.Items.Add(項目.表示);
        }
        this.速度選択.SelectedIndex=1;
        this.速度選択.SelectedIndexChanged+=(_,_)=>{
            if(this.速度選択.SelectedIndex>=0){
                再生器.速度=速度一覧[this.速度選択.SelectedIndex].速度;
            }
        };
        this.一時停止ボタン.Clicked+=(_,_)=>{
            if(再生器.一時停止中か){
                再生器.再開();
                this.一時停止ボタン.Text="⏸ 一時停止";
            }else{
                再生器.一時停止();
                this.一時停止ボタン.Text="▶ 再開";
            }
        };
        var 題名=new Label{Text="小さなネット商店",FontSize=22,FontAttributes=FontAttributes.Bold,TextColor=配色.文字,VerticalOptions=LayoutOptions.Center};
        var 副題=new Label{Text="Seamless のコレクション・DB・LINQ を使うサンプル。操作しなくても自動で進みます。",TextColor=配色.薄い文字,VerticalOptions=LayoutOptions.Center};
        var 並び=new HorizontalStackLayout{Spacing=16};
        並び.Add(題名);
        並び.Add(副題);
        並び.Add(this.周回表示);
        並び.Add(this.状態表示);
        var 操作=new HorizontalStackLayout{Spacing=8,HorizontalOptions=LayoutOptions.End};
        操作.Add(new Label{Text="速度",TextColor=配色.薄い文字,VerticalOptions=LayoutOptions.Center});
        操作.Add(this.速度選択);
        操作.Add(this.一時停止ボタン);
        var 行=new Grid{ColumnDefinitions=[new ColumnDefinition(GridLength.Star),new ColumnDefinition(GridLength.Auto)]};
        行.Add(並び,0,0);
        行.Add(操作,1,0);
        return 行;
    }

    private View 左列を作る(物語の再生器 再生器){
        var 並び=new VerticalStackLayout{Spacing=6};
        並び.Add(new Label{Text="物語",FontSize=16,FontAttributes=FontAttributes.Bold,TextColor=配色.文字});
        for(var 番号=0;番号<再生器.章一覧.Count;番号++){
            var 章=再生器.章一覧[番号];
            var 印=new Label{Text="○",FontSize=16,WidthRequest=22,TextColor=配色.薄い文字,VerticalOptions=LayoutOptions.Center};
            var 文字=new VerticalStackLayout();
            文字.Add(new Label{Text=$"{番号+1}. {章.題名}",FontSize=15,TextColor=配色.文字});
            文字.Add(new Label{Text=章.使う機能,FontSize=11,TextColor=配色.薄い文字,LineBreakMode=LineBreakMode.WordWrap,MaximumWidthRequest=190});
            var 中身=new HorizontalStackLayout{Spacing=6};
            中身.Add(印);
            中身.Add(文字);
            var 枠=new Border{Content=中身,Padding=new Thickness(8,6),StrokeThickness=1,Stroke=配色.枠,BackgroundColor=配色.面,StrokeShape=new RoundRectangle{CornerRadius=6}};
            var 移動先=番号;
            var 押下=new TapGestureRecognizer();
            押下.Tapped+=(_,_)=>再生器.章へ移動(移動先);
            枠.GestureRecognizers.Add(押下);
            this.章の行一覧.Add((枠,印));
            並び.Add(枠);
        }
        並び.Add(new Label{Text="章を押すとその章へ移動します。",FontSize=11,TextColor=配色.薄い文字,Margin=new Thickness(0,6,0,0)});
        return new ScrollView{Content=並び};
    }

    private View 中央列を作る(){
        var 並び=new VerticalStackLayout{Spacing=10,Padding=16};
        並び.Add(this.章見出し);
        並び.Add(this.機能表示);
        並び.Add(this.説明表示);
        並び.Add(new Label{Text="実行しているコード",FontSize=13,TextColor=配色.薄い文字,Margin=new Thickness(0,8,0,0)});
        並び.Add(new Border{
            Content=this.コード領域,
            Padding=new Thickness(0,10),
            BackgroundColor=配色.コード背景,
            StrokeThickness=0,
            StrokeShape=new RoundRectangle{CornerRadius=6},
        });
        return 面を作る(new ScrollView{Content=並び});
    }

    private View 右列を作る(){
        var 並び=new VerticalStackLayout{Spacing=12,Padding=14};
        並び.Add(見出し("使用量"));
        並び.Add(this.メーター領域);
        並び.Add(見出し("数値"));
        並び.Add(this.指標領域);
        並び.Add(this.グラフ領域);
        並び.Add(見出し("記録"));
        並び.Add(this.ログ領域);
        return 面を作る(new ScrollView{Content=並び});
    }

    private static Label 見出し(string 文字)=>new(){Text=文字,FontSize=13,FontAttributes=FontAttributes.Bold,TextColor=配色.薄い文字};

    private static Border 面を作る(View 中身)=>new(){
        Content=中身,
        BackgroundColor=配色.面,
        Stroke=配色.枠,
        StrokeThickness=1,
        StrokeShape=new RoundRectangle{CornerRadius=8},
    };

    // ---- 再生器からの知らせ(UI スレッドで呼ぶ) ----

    private void 章の状態を表示する(章の状態 状態){
        var (枠,印)=this.章の行一覧[状態.番号];
        switch(状態.進行){
            case 章の進行.実行中:
                印.Text="▶";
                印.TextColor=配色.アクセント;
                枠.BackgroundColor=配色.実行中の章;
                this.章を表示する(状態.番号);
                break;
            case 章の進行.完了:
                印.Text="✔";
                印.TextColor=配色.成功;
                枠.BackgroundColor=配色.面;
                break;
            case 章の進行.失敗:
                印.Text="✖";
                印.TextColor=配色.失敗;
                枠.BackgroundColor=配色.面;
                break;
            default:
                印.Text="○";
                印.TextColor=配色.薄い文字;
                枠.BackgroundColor=配色.面;
                break;
        }
    }

    private void 章を表示する(int 番号){
        var 章=this.再生器!.章一覧[番号];
        this.表示中の章=番号;
        this.章見出し.Text=$"{番号+1}. {章.題名}";
        this.機能表示.Text=章.使う機能;
        this.説明表示.Text=章.説明;
        this.コード領域.Clear();
        this.コード行一覧.Clear();
        foreach(var 行 in 章.コード行){
            var ラベル=new Label{Text=行,FontFamily="Consolas",FontSize=13,TextColor=配色.コード文字,Padding=new Thickness(12,1)};
            this.コード行一覧.Add(ラベル);
            this.コード領域.Add(ラベル);
        }
        this.メーター領域.Clear();
        this.メーター一覧.Clear();
        this.指標領域.Clear();
        this.指標一覧.Clear();
        this.グラフ領域.Clear();
        this.グラフ一覧.Clear();
        this.ログ領域.Clear();
    }

    private void 報告を表示する(int 番号,章の報告 報告){
        if(番号!=this.表示中の章){
            return;
        }
        switch(報告){
            case ログ報告 ログ:
                this.ログを足す(ログ);
                break;
            case 指標報告 指標:
                this.指標を出す(指標);
                break;
            case メーター報告 メーター:
                this.メーターを出す(メーター);
                break;
            case 棒グラフ報告 グラフ:
                this.グラフを出す(グラフ);
                break;
            case コード強調報告 強調:
                for(var 行番号=0;行番号<this.コード行一覧.Count;行番号++){
                    if(行番号==強調.行番号){
                        this.コード行一覧[行番号].BackgroundColor=配色.コード強調;
                    }else{
                        this.コード行一覧[行番号].BackgroundColor=Colors.Transparent;
                    }
                }
                break;
        }
    }

    private void ログを足す(ログ報告 ログ){
        this.ログ領域.Insert(0,new Label{Text=ログ.文,FontSize=13,TextColor=配色.ログ(ログ.種別)});
        while(this.ログ領域.Count>ログの最大行数){
            this.ログ領域.RemoveAt(this.ログ領域.Count-1);
        }
    }

    private void 指標を出す(指標報告 指標){
        if(this.指標一覧.TryGetValue(指標.名前,out var 値ラベル)){
            値ラベル.Text=指標.値;
            return;
        }
        値ラベル=new Label{Text=指標.値,FontSize=14,FontAttributes=FontAttributes.Bold,TextColor=配色.文字,HorizontalTextAlignment=TextAlignment.End};
        var 行=new Grid{ColumnDefinitions=[new ColumnDefinition(GridLength.Star),new ColumnDefinition(GridLength.Auto)]};
        行.Add(new Label{Text=指標.名前,FontSize=14,TextColor=配色.薄い文字},0,0);
        行.Add(値ラベル,1,0);
        this.指標一覧.Add(指標.名前,値ラベル);
        this.指標領域.Add(行);
    }

    private void メーターを出す(メーター報告 メーター){
        var 割合=0.0;
        if(メーター.上限>0){
            割合=Math.Clamp((double)メーター.値/メーター.上限,0,1);
        }
        var 文字=$"{表示.バイト(メーター.値)} / {表示.バイト(メーター.上限)}";
        if(this.メーター一覧.TryGetValue(メーター.名前,out var 既存)){
            既存.値.Text=文字;
            既存.棒.Progress=割合;
            return;
        }
        var 値ラベル=new Label{Text=文字,FontSize=12,TextColor=配色.文字,HorizontalTextAlignment=TextAlignment.End};
        var 棒=new ProgressBar{Progress=割合,ProgressColor=配色.アクセント,HeightRequest=10};
        var 見出し行=new Grid{ColumnDefinitions=[new ColumnDefinition(GridLength.Star),new ColumnDefinition(GridLength.Auto)]};
        見出し行.Add(new Label{Text=メーター.名前,FontSize=13,TextColor=配色.薄い文字},0,0);
        見出し行.Add(値ラベル,1,0);
        var 塊=new VerticalStackLayout{Spacing=2};
        塊.Add(見出し行);
        塊.Add(棒);
        this.メーター一覧.Add(メーター.名前,(値ラベル,棒));
        this.メーター領域.Add(塊);
    }

    private void グラフを出す(棒グラフ報告 グラフ){
        if(!this.グラフ一覧.TryGetValue(グラフ.題名,out var 既存)){
            var 描画=new 棒グラフ描画();
            var 表示=new GraphicsView{Drawable=描画,HeightRequest=190};
            既存=(表示,描画);
            this.グラフ一覧.Add(グラフ.題名,既存);
            this.グラフ領域.Add(表示);
        }
        既存.描画.更新する(グラフ);
        既存.表示.Invalidate();
    }
}
