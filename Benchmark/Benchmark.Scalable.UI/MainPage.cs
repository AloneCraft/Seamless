using Benchmark.Infrastructure;
using Benchmark.Scalable.Core;
using Benchmark.Tsavorite.ParameterSweep;
using Seamless.Execution;
using Shared.Benchmark.History;
using System.Text.Json;

namespace Benchmark.Scalable.UI;

public sealed class MainPage:TabbedPage{
    private readonly 測定設定画面 設定=new(false);
    private readonly Entry 件数=new(){Text="4096",Keyboard=Keyboard.Numeric,AutomationId="大規模.件数"};
    private readonly Entry シード=new(){Text="42",Keyboard=Keyboard.Numeric};
    private readonly Picker バックエンド=new(){ItemsSource=new[]{"Tsavorite","Persisted"},SelectedIndex=0};
    private readonly Picker 処理=new(){ItemsSource=Enum.GetNames<Workload>(),SelectedIndex=0};
    private readonly Picker 実装=new(){ItemsSource=Enum.GetNames<ScalableImplementation>(),SelectedIndex=1};
    private readonly CheckBox 自動調整=new();
    private readonly Label 進捗=new(){Text="待機中",AutomationId="大規模.進捗"};
    private readonly Editor 結果=new(){IsReadOnly=true,AutoSize=EditorAutoSizeOption.TextChanges,HeightRequest=360,AutomationId="大規模.結果"};
    private readonly Button 開始=new(){Text="測定開始",AutomationId="大規模.開始"};
    private readonly Button 探索=new(){Text="容量・設定の探索",AutomationId="大規模.探索"};
    private readonly CheckBox スモーク探索か=new(){IsChecked=true};
    private readonly Entry 探索最大件数=new(){Text="128",AutomationId="容量探索.最大件数"};
    private readonly Entry 探索手順数=new(){Text="2",AutomationId="容量探索.手順数"};
    private readonly Entry 探索制限秒=new(){Text="2"};
    private readonly Entry 探索デバイス容量=new(){Text="16777216"};
    private readonly Label 探索進捗=new(){Text="待機中"};
    private readonly Editor 探索結果=new(){IsReadOnly=true,HeightRequest=360};
    private CancellationTokenSource? 取消;
    private readonly BenchmarkRepository 記録;
    private readonly 大規模実行サービス 実行サービス;
    private readonly CancellationTokenSource 内部制御終了=new();
    private Task? 内部制御タスク;
    private アプリ操作ホスト? 内部制御;
    private Task? 手動実行タスク;
    private bool 内部制御初期化済みか;
    public MainPage(){
        this.Title="Scalable";
        var 場所=new DirectoryInfo(AppContext.BaseDirectory);
        while(場所 is not null&&!File.Exists(Path.Combine(場所.FullName,"Seamless.slnx"))){場所=場所.Parent;}
        var DBパス=BenchmarkDatabase.Resolve(場所?.FullName??Environment.CurrentDirectory);
        this.記録=new(new(DBパス));
        this.実行サービス=new(this.記録);
        var 説明=new Button{Text="設定と実行経路を説明",AutomationId="大規模.説明"};
        説明.Clicked+=(_,_)=>{
            try{
                var 情報=this.要求作成().Explain();var 採用=情報.Profile;
                this.結果.Text=$"要求: {採用.Requested} / 採用: {採用.Applied}\nID: {採用.ProfileID} / 版: {採用.ProfileVersion}\n設定: {JsonSerializer.Serialize(採用.Settings)}\n拡張: {採用.Growth.ID} / 版: {採用.Growth.Version}\n根拠Run ID: {string.Join(", ",採用.EvidenceRunIDs)}\n理由: {採用.Reason}\n不足条件: {string.Join("; ",採用.MissingConditions)}\n実装: {情報.Implementation}\n{情報.RuntimeDecisions}\n予約会計はプロセスの実メモリとは別の指標です。";
            }catch(Exception 例外){this.結果.Text=例外.Message;}
        };
        var 中止=new Button{Text="中止",AutomationId="大規模.中止"};中止.Clicked+=(_,_)=>this.取消?.Cancel();
        this.開始.Clicked+=async (_,_)=>await this.手動開始Async(false);
        this.探索.Clicked+=async (_,_)=>await this.手動開始Async(true);
        this.Children.Add(new ContentPage{Title="測定",Content=new ScrollView{Content=new VerticalStackLayout{Padding=16,Spacing=8,Children={
            new Label{Text="大規模ストリーミング測定",FontSize=24},new Label{Text="件数"},this.件数,new Label{Text="入力シード"},this.シード,this.バックエンド,this.処理,this.実装,
            new Label{Text="ScopedはSeamlessの予算付きLINQです。並べ替えと結合は必要に応じてディスクを使います。CollectionsはNativeのコレクションAPIで実行します。"},
            new HorizontalStackLayout{Children={this.自動調整,new Label{Text="中間区間で再構成し、内容を検証して切り替える"}}},
            new Label{Text="Persistedの辞書は追記・上書き型です。削除を含むCollections測定はTsavoriteで行います。比較時は処理の違いを確認してください。"},
            new HorizontalStackLayout{Children={this.開始,中止,説明}},this.進捗,this.結果
        }}}});
        var 探索中止=new Button{Text="中止"};探索中止.Clicked+=(_,_)=>this.取消?.Cancel();
        this.Children.Add(new ContentPage{Title="容量探索",Content=new ScrollView{Content=new VerticalStackLayout{Padding=16,Spacing=8,Children={
            new Label{Text="索引・ページ・セグメント・メモリの容量探索",FontSize=22},
            new HorizontalStackLayout{Children={this.スモーク探索か,new Label{Text="スモーク（外すと全コレクション・型の探索）"}}},
            new Label{Text="最大Count"},this.探索最大件数,new Label{Text="軸ごとの手順数"},this.探索手順数,
            new Label{Text="ケースごとの制限秒数"},this.探索制限秒,new Label{Text="ストレージデバイス容量（バイト）"},this.探索デバイス容量,
            new HorizontalStackLayout{Children={this.探索,探索中止}},this.探索進捗,this.探索結果
        }}}});
        this.Children.Add(new ContentPage{Title="設定",Content=this.設定});
        this.Children.Add(new ContentPage{Title="履歴・比較",Content=new 履歴比較画面(DBパス)});
    }
    private ScalableRequest 要求作成(){
        return new ScalableRequest(Count:long.Parse(this.件数.Text),Seed:int.Parse(this.シード.Text),Backend:this.バックエンド.SelectedItem.ToString()!,
                    Kind:Enum.Parse<StoreKind>(this.設定.保存),Workload:Enum.Parse<Workload>(this.処理.SelectedItem.ToString()!),Profile:Enum.Parse<Profile>(this.設定.プロファイル),Growth:this.設定.拡張,
                    ExplicitSettings:this.設定.明示設定,ProfileCatalogJSON:this.設定.プロファイルJSON,MemoryLimitBytes:this.設定.メモリ予算,DiskLimitBytes:this.設定.ディスク予算,Adaptive:this.自動調整.IsChecked,Implementation:Enum.Parse<ScalableImplementation>(this.実装.SelectedItem.ToString()!),IOMode:this.設定.入出力,AccessPattern:this.設定.アクセス);
    }
    private async Task 手動開始Async(bool 探索か){
        if(this.実行サービス.実行排他.実行中か){return;}
        var 実行=this.実行Async(探索か);
        this.手動実行タスク=実行;
        await 実行;
    }

    private async Task 実行Async(bool 探索か){
        if(this.取消 is not null){return;}
        using var 予約=this.実行サービス.実行排他.予約する();
        if(予約 is null){return;}
        using var 取消=new CancellationTokenSource();this.取消=取消;this.開始.IsEnabled=false;this.探索.IsEnabled=false;
        try{
            if(探索か){
                SystemResources.Check(true);
                var オプション=new パラメータスイープ実行オプション{最大スイープ手順数=int.Parse(this.探索手順数.Text),最大成功Count=int.Parse(this.探索最大件数.Text),検索試行Count=32,ケース別タイムアウト=TimeSpan.FromSeconds(double.Parse(this.探索制限秒.Text)),ストレージデバイス容量=long.Parse(this.探索デバイス容量.Text)};
                var 進捗=new Progress<パラメータスイープ結果>(項目=>this.探索進捗.Text=$"{項目.ケース名}: {項目.最大成功Count:N0}件 / {項目.失敗種別}");
                var 結果=await new CapacitySweepRunner(this.記録).RunAsync(オプション,smoke:this.スモーク探索か.IsChecked,progress:進捗,cancellationToken:取消.Token);
                this.探索結果.Text=JsonSerializer.Serialize(結果,new JsonSerializerOptions{WriteIndented=true});this.探索進捗.Text="完了: "+結果.Count+"ケース";
            }else{
                var 要求=this.要求作成();
                var 結果=await this.実行サービス.実行Async(要求,this.測定進捗を適用,取消.Token);
                this.結果.Text=JsonSerializer.Serialize(結果,new JsonSerializerOptions(BenchmarkRepository.JSON){WriteIndented=true});
            }
        }catch(Exception 例外){if(探索か){this.探索結果.Text=例外.ToString();}else{this.結果.Text=例外.ToString();}}
        finally{this.取消=null;this.開始.IsEnabled=true;this.探索.IsEnabled=true;}
    }

    private void 測定進捗を適用(ScalableProgress 進捗)=>MainThread.BeginInvokeOnMainThread(()=>this.進捗.Text=$"{進捗.Phase} {進捗.Sample}/{進捗.Total} {進捗.Completed:N0}/{進捗.Count:N0}");

    public async Task<アプリ操作結果> 外部実行Async(ScalableRequest 要求,CancellationToken 取消){
        using var 実行取消=CancellationTokenSource.CreateLinkedTokenSource(取消);
        this.取消=実行取消;this.開始.IsEnabled=false;this.探索.IsEnabled=false;
        try{
            var 測定=await this.実行サービス.実行Async(要求,this.測定進捗を適用,実行取消.Token);
            this.結果.Text=JsonSerializer.Serialize(測定,new JsonSerializerOptions(BenchmarkRepository.JSON){WriteIndented=true});
            var 合否="Unavailable";
            if(測定.Status=="Passed"){合否="Passed";}
            if(測定.Status=="Failed"){合否="Failed";}
            return new("Completed",合否,"run:"+測定.RunID);
        }catch(OperationCanceledException) when(実行取消.IsCancellationRequested){
            this.進捗.Text="キャンセル済み";
            // Runner は Worker を作らず、finally の Store.Dispose と取消履歴の保存後に再throwする。
            return new("Cancelled","Unavailable","");
        }catch(Exception 例外){
            this.結果.Text=例外.ToString();
            throw;
        }finally{
            this.取消=null;this.開始.IsEnabled=true;this.探索.IsEnabled=true;
        }
    }

    public void 終了を要求する(){this.内部制御終了.Cancel();this.取消?.Cancel();}

    public async Task 終了Async(){
        this.実行サービス.実行排他.終了を受け付ける();
        this.終了を要求する();
        await Task.WhenAll(this.手動実行タスク??Task.CompletedTask,this.内部制御タスク??Task.CompletedTask);
        if(this.内部制御 is{} 制御&&!制御.終了可能か){throw new InvalidOperationException("外部操作の停止・保存・receiptを確認できないため終了を保留しています。",制御.最終障害);}
    }

    public void 終了障害を表示(string 内容)=>this.進捗.Text="終了保留: "+内容;

    protected override void OnAppearing(){
        base.OnAppearing();
        if(this.内部制御初期化済みか){return;}
        this.内部制御初期化済みか=true;
        try{
            var 通信=アプリ操作メールボックス.環境から作成();
            if(通信 is null){return;}
            var ホスト=new アプリ操作ホスト("Benchmark.Scalable.UI",通信,new 大規模アプリ操作サービス(this,this.実行サービス));
            this.内部制御=ホスト;
            this.内部制御タスク=Task.Run(()=>ホスト.実行Async(this.内部制御終了.Token));
        }catch(Exception 例外){this.進捗.Text="アプリ内部操作 Unavailable: "+例外.Message;}
    }
}
