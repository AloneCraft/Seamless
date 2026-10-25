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
    public MainPage(){
        this.Title="Scalable";
        var 場所=new DirectoryInfo(AppContext.BaseDirectory);
        while(場所 is not null&&!File.Exists(Path.Combine(場所.FullName,"Seamless.slnx"))){場所=場所.Parent;}
        var DBパス=BenchmarkDatabase.Resolve(場所?.FullName??Environment.CurrentDirectory);
        this.記録=new(new(DBパス));
        var 説明=new Button{Text="設定と実行経路を説明",AutomationId="大規模.説明"};
        説明.Clicked+=(_,_)=>{
            try{
                var 情報=this.要求作成().Explain();var 採用=情報.Profile;
                this.結果.Text=$"要求: {採用.Requested} / 採用: {採用.Applied}\nID: {採用.ProfileID} / 版: {採用.ProfileVersion}\n設定: {JsonSerializer.Serialize(採用.Settings)}\n拡張: {採用.Growth.ID} / 版: {採用.Growth.Version}\n根拠Run ID: {string.Join(", ",採用.EvidenceRunIDs)}\n理由: {採用.Reason}\n不足条件: {string.Join("; ",採用.MissingConditions)}\n実装: {情報.Implementation}\n{情報.RuntimeDecisions}\n予約会計はプロセスの実メモリとは別の指標です。";
            }catch(Exception 例外){this.結果.Text=例外.Message;}
        };
        var 中止=new Button{Text="中止",AutomationId="大規模.中止"};中止.Clicked+=(_,_)=>this.取消?.Cancel();
        this.開始.Clicked+=async (_,_)=>await this.実行Async(false);
        this.探索.Clicked+=async (_,_)=>await this.実行Async(true);
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
    private async Task 実行Async(bool 探索か){
        if(this.取消 is not null){return;}
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
                var 結果=await Task.Run(()=>new ScalableRunner(this.記録).Run(要求,進捗=>MainThread.BeginInvokeOnMainThread(()=>this.進捗.Text=$"{進捗.Phase} {進捗.Sample}/{進捗.Total} {進捗.Completed:N0}/{進捗.Count:N0}"),取消.Token));
                this.結果.Text=JsonSerializer.Serialize(結果,new JsonSerializerOptions(BenchmarkRepository.JSON){WriteIndented=true});
            }
        }catch(Exception 例外){if(探索か){this.探索結果.Text=例外.ToString();}else{this.結果.Text=例外.ToString();}}
        finally{this.取消=null;this.開始.IsEnabled=true;this.探索.IsEnabled=true;}
    }
}
