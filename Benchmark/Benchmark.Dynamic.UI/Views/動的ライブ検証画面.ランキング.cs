using Benchmark.Dynamic.Core;
using System.Globalization;

namespace Benchmark.Dynamic.UI;

public sealed partial class 動的ライブ検証画面{
    private const int ページ件数=25;
    private readonly VerticalStackLayout 対象設定=new(){Spacing=4,IsVisible=false};
    private readonly Dictionary<SourceTarget,CheckBox> 入力元チェック=[];
    private readonly Dictionary<QueryElementType,CheckBox> 要素型チェック=[];
    private readonly Button 対象ボタン=new(){AutomationId="動的ライブ.検証対象"};
    private readonly Label 対象説明=new(){FontSize=12};
    private readonly Entry 入力件数=new(){Text="16",WidthRequest=65,Keyboard=Keyboard.Numeric};
    private readonly Entry 演算子数=new(){Text="4",WidthRequest=55,Keyboard=Keyboard.Numeric};
    private readonly Entry 最大深度=new(){Text="4",WidthRequest=55,Keyboard=Keyboard.Numeric};
    private readonly Entry 探索件数=new(){Placeholder="停止まで",WidthRequest=100,Keyboard=Keyboard.Numeric,AutomationId="動的ライブ.探索件数"};
    private readonly CheckBox ai検証出力チェック=new(){IsChecked=true,AutomationId="動的ライブ.AI検証出力"};
    private readonly Picker ai検証分割Picker=new(){ItemsSource=new[]{"100件","500件","1000件"},SelectedIndex=0,WidthRequest=90,AutomationId="動的ライブ.AI検証分割"};
    private readonly Picker ai検証フィルタPicker=new(){ItemsSource=new[]{"ランダム探索のみ","全段階","要改善のみ"},SelectedIndex=0,WidthRequest=160,AutomationId="動的ライブ.AI検証フィルタ"};

    private readonly Picker 段階絞り込み=new(){Title="段階"};
    private readonly Picker 入力元絞り込み=new(){Title="入力元",AutomationId="動的ライブ.Source"};
    private readonly Picker 要素型絞り込み=new(){Title="要素型",AutomationId="動的ライブ.要素型"};
    private readonly Picker 比較実装=new(){ItemsSource=new[]{"FLinq","FLinq最適化なし","ZLinq"},SelectedIndex=0};
    private readonly Picker 指標=new(){ItemsSource=new[]{"速度比","メモリ割り当て"},SelectedIndex=0};
    private readonly CollectionView 順位一覧=new(){SelectionMode=SelectionMode.Single,AutomationId="動的ライブ.ランキング"};
    private readonly Label 集計=new(){FontSize=12,AutomationId="動的ライブ.集計"};
    private readonly Label ページ表示=new(){FontSize=12};
    private readonly Button 前ページ=new(){Text="前へ"};
    private readonly Button 次ページ=new(){Text="次へ"};
    private readonly 動的ライブ棒グラフ 集計描画=new();
    private GraphicsView? 集計グラフ;
    private readonly List<順位項目> 順位データ=[];
    private 動的ライブ対象[] 実行対象=[];
    private 動的ライブ段階 開始段階;
    private readonly HashSet<(動的ライブ段階,動的ライブ対象)> 開始済み対象=[];
    private readonly HashSet<(動的ライブ段階,動的ライブ対象)> 完了対象=[];
    private readonly Button 追従再開=new(){Text="最新結果を追う",IsEnabled=false};
    private bool ライブ追従中=true;
    private int ページ;
    private int 選択要求番号;
    private DateTime 更新日時;

    public Action<動的結果行>? 詳細選択{get;set;}

    private sealed record 順位項目(
        動的ライブ段階? 段階,SourceTarget 入力元,QueryElementType 要素型,int 試行,
        string 実行ID,string クエリID,string 式,WorkerExecutionStatus 状態,bool 一致,
        IReadOnlyList<動的ライブ計測> 計測,bool ZLinq対応,動的結果行? 再実行詳細=null);
    private sealed record 順位表示(順位項目 項目,double? 比率,double 差,string 比率表示);

    private static bool 整数を読む(Entry 欄,int 最小,int 最大,out int 値)=>
        int.TryParse(欄.Text,NumberStyles.Integer,CultureInfo.InvariantCulture,out 値)&&値>=最小&&値<=最大;

    private 動的ライブ対象[] 選択対象を取得()=>動的ライブ検証サービス.対応対象(
        this.入力元チェック.Where(static 項目=>項目.Value.IsChecked).Select(static 項目=>項目.Key),
        this.要素型チェック.Where(static 項目=>項目.Value.IsChecked).Select(static 項目=>項目.Key));

    private View 検証設定を作る(){
        var 入力元欄=new FlexLayout{Wrap=Microsoft.Maui.Layouts.FlexWrap.Wrap};
        foreach(var 入力元 in Enum.GetValues<SourceTarget>()){
            var 欄=new CheckBox{IsChecked=true,AutomationId="動的ライブ.対象."+入力元};
            this.入力元チェック.Add(入力元,欄);
            欄.CheckedChanged+=(_,_)=>this.対象表示を更新する();
            入力元欄.Children.Add(対象の選択欄を作る(欄,入力元.ToString()));
        }
        var 要素型欄=new FlexLayout{Wrap=Microsoft.Maui.Layouts.FlexWrap.Wrap};
        foreach(var 型 in Enum.GetValues<QueryElementType>()){
            var 欄=new CheckBox{IsChecked=true,AutomationId="動的ライブ.型."+型};
            this.要素型チェック.Add(型,欄);
            欄.CheckedChanged+=(_,_)=>this.対象表示を更新する();
            要素型欄.Children.Add(対象の選択欄を作る(欄,型.ToString()));
        }
        this.対象設定.Add(見出し("入力元"));
        this.対象設定.Add(ラベル("入力元はクエリに渡すデータの形です。同じ入力元で System.Linq、FLinq、対応する ZLinq を比較します。"));
        this.対象設定.Add(入力元欄);
        this.対象設定.Add(見出し("要素型"));
        this.対象設定.Add(要素型欄);
        this.対象設定.Add(this.対象説明);
        var 詳細設定=new FlexLayout{Wrap=Microsoft.Maui.Layouts.FlexWrap.Wrap};
        foreach(var (説明,欄) in new (string,View)[]{
            ("入力要素数",this.入力件数),("演算子数",this.演算子数),("最大深度",this.最大深度),
            ("特殊な値",this.特殊値チェック),("各入力元・要素型のクエリ数",this.探索件数),
            ("Seed",this.シード入力),("試行番号",this.試行番号入力),("表示速度",this.速度選択)}){
            詳細設定.Children.Add(new HorizontalStackLayout{Spacing=5,Margin=new Thickness(0,0,12,0),Children={ラベル(説明),欄}});
        }
        詳細設定.Children.Add(new HorizontalStackLayout{Spacing=5,Margin=new Thickness(0,0,12,0),Children={ラベル("AI検証テキスト出力"),this.ai検証出力チェック}});
        詳細設定.Children.Add(new HorizontalStackLayout{Spacing=5,Margin=new Thickness(0,0,12,0),Children={ラベル("AI分割件数"),this.ai検証分割Picker}});
        詳細設定.Children.Add(new HorizontalStackLayout{Spacing=5,Margin=new Thickness(0,0,12,0),Children={ラベル("AI出力対象"),this.ai検証フィルタPicker}});
        詳細設定.Children.Add(this.シード再現ボタン);
        this.対象設定.Add(詳細設定);
        this.対象ボタン.Clicked+=(_,_)=>this.対象設定.IsVisible=!this.対象設定.IsVisible;
        this.探索件数.TextChanged+=(_,_)=>this.対象表示を更新する();
        this.対象表示を更新する();
        return new VerticalStackLayout{Children={this.対象ボタン,this.対象設定}};
    }

    private static View 対象の選択欄を作る(CheckBox 欄,string 表示){
        欄.HandlerChanged+=(_,_)=>{
            if(欄.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.CheckBox ネイティブ欄){
                ネイティブ欄.MinWidth=0;
                ネイティブ欄.Content=表示;
            }
        };
        欄.Margin=new Thickness(0,0,12,0);
        return 欄;
    }

    public void 設定を復元する(動的実行条件Snapshot 条件,string Seed){
        foreach(var (入力元,欄) in this.入力元チェック){欄.IsChecked=条件.SourceTargets.Contains(入力元);}
        foreach(var (型,欄) in this.要素型チェック){欄.IsChecked=型==条件.ElementType;}
        this.入力件数.Text=条件.InputElementCount.ToString(CultureInfo.InvariantCulture);
        this.演算子数.Text=条件.OperatorCount.ToString(CultureInfo.InvariantCulture);
        this.最大深度.Text=条件.MaximumDepth.ToString(CultureInfo.InvariantCulture);
        this.探索件数.Text=条件.QueryCount.ToString(CultureInfo.InvariantCulture);
        this.特殊値チェック.IsChecked=条件.IncludeSpecialValues;
        this.シード入力.Text=Seed;
        this.試行番号入力.Text="0";
        this.対象設定.IsVisible=true;
    }

    private void 対象表示を更新する(){
        var 入力元数=this.入力元チェック.Count(static 項目=>項目.Value.IsChecked);
        var 型数=this.要素型チェック.Count(static 項目=>項目.Value.IsChecked);
        var 組数=this.選択対象を取得().Length;
        var 予定=整数を読む(this.探索件数,1,int.MaxValue,out var 件数)?$" / 探索予定 {(long)件数*組数:N0}件":"";
        this.対象ボタン.Text=$"検証対象と詳細設定 ▾  入力元 {入力元数} / 要素型 {型数} / 有効 {組数}組{予定}";
        this.対象説明.Text=$"対象外 {入力元数*型数-組数}組。Range・Persisted・TsavoriteはIntのみ対応。";
        this.ボタン状態を更新する();
    }

    private View ランキングを作る(){
        this.段階絞り込み.ItemsSource=new[]{"全段階","ペア全組み合わせ","3段全組み合わせ","ランダム探索","再実行"};
        this.入力元絞り込み.ItemsSource=new[]{"全入力元"}.Concat(Enum.GetNames<SourceTarget>()).ToArray();
        this.要素型絞り込み.ItemsSource=new[]{"全要素型"}.Concat(Enum.GetNames<QueryElementType>()).ToArray();
        foreach(var 欄 in new[]{this.段階絞り込み,this.入力元絞り込み,this.要素型絞り込み,this.比較実装,this.指標}){
            欄.SelectedIndex=0;
            欄.SelectedIndexChanged+=(_,_)=>{this.ページ=0;this.ランキングを更新する();};
        }
        this.順位一覧.ItemTemplate=new DataTemplate(()=>{
            var 行=new Label{FontSize=12,Padding=new Thickness(5,3),LineBreakMode=LineBreakMode.TailTruncation};
            行.SetBinding(Label.TextProperty,nameof(順位表示.比率表示));
            return 行;
        });
        this.順位一覧.SelectionChanged+=async(_,変更)=>{
            if(変更.CurrentSelection.FirstOrDefault() is 順位表示 行){await this.順位を選択Async(行.項目);}
        };
        this.前ページ.Clicked+=(_,_)=>{this.ページ--;this.ランキングを更新する();};
        this.次ページ.Clicked+=(_,_)=>{this.ページ++;this.ランキングを更新する();};
        this.追従再開.Clicked+=(_,_)=>{
            this.選択要求番号++;
            this.ライブ追従中=true;
            this.追従再開.IsEnabled=false;
        };
        var 操作=new HorizontalStackLayout{Spacing=6,Children={this.段階絞り込み,this.入力元絞り込み,
            this.要素型絞り込み,this.比較実装,this.指標,this.前ページ,this.次ページ,this.ページ表示,this.追従再開}};
        this.集計グラフ=new GraphicsView{Drawable=this.集計描画,HeightRequest=125,AutomationId="動的ライブ.集計チャート"};
        var 表示=new Grid{ColumnDefinitions=[new ColumnDefinition(GridLength.Star),new ColumnDefinition(new GridLength(430))],
            RowDefinitions=[new RowDefinition(GridLength.Auto),new RowDefinition(GridLength.Star)],ColumnSpacing=8};
        表示.Add(new ScrollView{Content=操作,Orientation=ScrollOrientation.Horizontal},0,0);
        表示.Add(this.順位一覧,0,1);
        var 集計欄=new ScrollView{Content=new VerticalStackLayout{Children={this.集計,this.集計グラフ}}};
        表示.Add(集計欄,1,0);
        Grid.SetRowSpan(集計欄,2);
        this.ランキングを更新する();
        return 面(表示);
    }

    private void ランキングを初期化(動的ライブ対象[] 対象,動的ライブ段階 段階){
        this.ライブ追従中=true;
        this.追従再開.IsEnabled=false;
        this.順位データ.Clear();
        this.開始済み対象.Clear();
        this.完了対象.Clear();
        this.実行対象=対象;
        this.開始段階=段階;
        this.ページ=0;
        this.選択要求番号++;
        this.更新日時=default;
        this.ランキングを更新する();
    }

    private void ランキングへ追加(動的ライブ結果 結果){
        this.開始済み対象.Add((結果.段階,new(結果.対象,結果.要素型)));
        this.順位データ.Add(new(結果.段階,結果.対象,結果.要素型,結果.通番,
            結果.実行ID,結果.QueryId,結果.式,結果.状態,結果.合格か,結果.計測一覧,結果.ZLinq対応));
        if(DateTime.UtcNow-this.更新日時>=TimeSpan.FromMilliseconds(750)){this.ランキングを更新する();}
    }

    public void 再実行結果を追加(動的結果行 行){
        if(!Enum.TryParse<WorkerExecutionStatus>(行.状態,out var 状態)){return;}
        QueryIrDocument? IR=null;
        if(!string.IsNullOrEmpty(行.IR)){IR=QueryIrJson.Deserialize(行.IR);}
        var 計測=動的ライブ検証サービス.計測を読む(行.計測詳細);
        this.順位データ.Add(new(null,行.対象,IR?.ElementType??QueryElementType.Int,行.QueryIndex,行.実行ID,行.QueryID,
            行.クエリ式,状態,状態==WorkerExecutionStatus.Succeeded&&行.意味一致=="一致",計測,
            IR is null||QueryZLinqSupport.IsSupported(IR),行));
        this.ランキングを更新する();
    }

    private bool 対象に一致する(SourceTarget 入力元,QueryElementType 型)=>
        (this.入力元絞り込み.SelectedIndex<=0||this.入力元絞り込み.SelectedItem?.ToString()==入力元.ToString())
        &&(this.要素型絞り込み.SelectedIndex<=0||this.要素型絞り込み.SelectedItem?.ToString()==型.ToString());

    private bool 段階に一致する(動的ライブ段階? 段階)=>
        this.段階絞り込み.SelectedIndex<=0||this.段階絞り込み.SelectedIndex==(段階 is{} 値?(int)値+1:4);

    private void ランキングを更新する(){
        this.更新日時=DateTime.UtcNow;
        var メモリか=this.指標.SelectedIndex==1;
        var 実装=this.比較実装.SelectedItem?.ToString()??"FLinq";
        var 対象行=this.順位データ.Where(項目=>this.段階に一致する(項目.段階)&&this.対象に一致する(項目.入力元,項目.要素型)).ToArray();
        var 比較一覧=対象行.Select(項目=>比を求める(項目,実装,メモリか)).ToArray();
        var 順位=比較一覧.OrderBy(static 行=>行.比率??double.PositiveInfinity)
            .ThenBy(static 行=>行.比率 is null?1:0).ThenByDescending(static 行=>行.差).ToArray();
        var ページ数=Math.Max(1,(順位.Length+ページ件数-1)/ページ件数);
        this.ページ=Math.Clamp(this.ページ,0,ページ数-1);
        this.順位一覧.ItemsSource=順位.Skip(this.ページ*ページ件数).Take(ページ件数).ToArray();
        this.ページ表示.Text=$"{this.ページ+1}/{ページ数} · {順位.Length:N0}件";
        this.前ページ.IsEnabled=this.ページ>0;
        this.次ページ.IsEnabled=this.ページ+1<ページ数;
        var 棒=new List<動的ライブ棒>();
        var 概要=new List<string>();
        foreach(var グループ in 比較一覧.GroupBy(static 行=>行.項目.段階).OrderBy(static 組=>組.Key)){
            var 有効=グループ.Where(static 行=>行.比率.HasValue).ToArray();
            var 名=グループ.Key?.ToString()??"再実行";
            if(有効.Length==0){概要.Add($"{名}　有効計測なし");continue;}
            var 悪化=有効.Count(static 行=>行.比率<1);
            var 最悪=有効.Min(static 行=>行.比率!.Value);
            if(メモリか){
                var 基準=有効.Average(static 行=>行.項目.計測.Single(値=>値.実装=="System.Linq").割当バイト);
                var 比較=有効.Average(行=>行.項目.計測.Single(値=>値.実装==実装).割当バイト);
                var 比率=動的ライブ集計.比率(基準,比較,true)!.Value;
                概要.Add($"{名}　{基準:N2} → {比較:N2} B/回 · {比率の表示(比率)} · 悪化 {悪化}/{有効.Length}");
                if(double.IsFinite(比率)){棒.Add(new(名,比率,動的ライブ配色.アクセント));}
            }else{
                var 幾何平均=動的ライブ集計.速度の幾何平均(有効.Select(static 行=>(
                    new 動的ライブ対象(行.項目.入力元,行.項目.要素型),行.比率!.Value)));
                概要.Add($"{名}　幾何平均 {幾何平均:N2}倍 · 1倍未満 {悪化}/{有効.Length} · 最悪 {最悪:N2}倍");
                棒.Add(new(名,幾何平均,動的ライブ配色.アクセント));
            }
        }
        var 計画対象=(from 段階 in Enum.GetValues<動的ライブ段階>()
                  where 段階>=this.開始段階&&this.段階に一致する(段階)
                  from 対象 in this.実行対象
                  where this.対象に一致する(対象.入力元,対象.要素型)
                  select (段階,対象)).ToArray();
        var 未実行=計画対象.Count(組=>!this.開始済み対象.Contains(組)&&!this.完了対象.Contains(組));
        var 進行中=計画対象.Length-未実行-計画対象.Count(this.完了対象.Contains);
        var コンパイル失敗=対象行.Count(static 項目=>項目.状態==WorkerExecutionStatus.CompilationFailed);
        var 不一致=対象行.Count(static 項目=>項目.状態==WorkerExecutionStatus.SemanticMismatch||項目.状態==WorkerExecutionStatus.Succeeded&&!項目.一致);
        var 未対応=実装=="ZLinq"?対象行.Count(static 項目=>!項目.ZLinq対応):0;
        var 欠測=比較一覧.Count(行=>行.項目.一致&&行.比率 is null&&(実装!="ZLinq"||行.項目.ZLinq対応));
        this.集計.Text=string.Join(Environment.NewLine,new[]{
            $"{(未実行+進行中>0?"暫定集計":"集計")}　未実行 {未実行}組 / 開始済み未完了 {進行中}組",
            $"不一致 {不一致} / コンパイル失敗 {コンパイル失敗} / 欠測 {欠測} / 未対応 {未対応}",
        }.Concat(概要));
        this.集計描画.更新する(メモリか?"平均割り当て比":"速度比の幾何平均","倍",棒);
        this.集計グラフ?.Invalidate();
    }

    private static 順位表示 比を求める(順位項目 項目,string 実装,bool メモリか){
        var 基準=項目.計測.FirstOrDefault(static 値=>値.実装=="System.Linq");
        var 比較=項目.計測.FirstOrDefault(値=>値.実装==実装);
        double? 比率=null;
        var 差=0d;
        if(項目.一致&&基準 is not null&&比較 is not null){
            var 元=メモリか?基準.割当バイト:基準.中央値ナノ秒;
            var 先=メモリか?比較.割当バイト:比較.中央値ナノ秒;
            比率=動的ライブ集計.比率(元,先,メモリか);
            if(比率.HasValue){差=先-元;}
        }
        var 表示=比率 is{} 倍?$"{比率の表示(倍)}  {差:+0.00;-0.00;0.00} {(メモリか?"B/回":"ns/回")}":"—";
        return new(項目,比率,差,$"{表示}   [{項目.段階?.ToString()??"再実行"}] {項目.入力元}/{項目.要素型} #{項目.試行} {項目.状態}  {項目.式}");
    }

    private static string 比率の表示(double 比率)=>double.IsPositiveInfinity(比率)?"割り当て解消":比率==1?"1.00倍（同等）":$"{比率:N2}倍";

    private async Task 順位を選択Async(順位項目 項目){
        var 番号=++this.選択要求番号;
        this.ライブ追従中=false;
        this.追従再開.IsEnabled=true;
        if(項目.再実行詳細 is{} 既存){
            _=Seed256.TryParse(既存.BaseSeed,out var Seed);
            _=int.TryParse(既存.入力要素数,out var 要素数);
            this.結果を表示する(new(動的ライブ段階.ランダム探索,項目.試行,項目.クエリID,項目.入力元,項目.要素型,
                Seed,要素数,項目.状態,項目.式,既存.生成Source,既存.展開Source,既存.診断,項目.一致,
                "",項目.計測),false);
            this.詳細選択?.Invoke(既存);
            return;
        }
        try{
            var 結果=await Task.Run(()=>this.サービス.詳細を読むAsync(項目.実行ID,項目.クエリID));
            if(番号!=this.選択要求番号||結果 is null){return;}
            this.結果を表示する(結果,false);
            if(結果.詳細 is{} 詳細){this.詳細選択?.Invoke(詳細);}
            else{this.詳細選択?.Invoke(new 動的結果行{
                実行ID=結果.実行ID,QueryID=結果.QueryId,対象=結果.対象,QueryIndex=結果.通番,
                短縮式=結果.式,N=0,深度=0,生成Source=結果.生成コード,診断=結果.診断,
                状態=結果.状態.ToString(),BaseSeed=結果.基準シード.ToString(),入力要素数=結果.入力要素数.ToString(),
                概要=$"{結果.要素型} / 生成器 {結果.生成器版} / コード {結果.コード識別子}",
            });}
        }catch(Exception 例外){
            if(番号==this.選択要求番号){this.状態表示.Text="詳細の読み込みに失敗しました。"+例外.Message;}
        }
    }
}
