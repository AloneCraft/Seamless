using Benchmark.Infrastructure;
using Seamless.Execution;
using System.Text.Json;

namespace Shared.Benchmark.History;

public sealed class 履歴比較画面:ContentView{
    private readonly BenchmarkRepository 記録;
    private readonly Picker 実行選択=new(){Title="実行履歴",AutomationId="共通履歴.実行"};
    private readonly Picker 基準選択=new(){Title="基準の計測",AutomationId="共通履歴.基準"};
    private readonly Picker 候補選択=new(){Title="候補の計測",AutomationId="共通履歴.候補"};
    private readonly CheckBox 実験指定=new();
    private readonly Entry 許容差=new(){Text="0.05",Keyboard=Keyboard.Numeric};
    private readonly Editor 詳細=new(){IsReadOnly=true,AutoSize=EditorAutoSizeOption.TextChanges,HeightRequest=320,AutomationId="共通履歴.詳細"};
    private readonly Entry 出力先;
    private readonly Entry プロファイル名=new(){Text="user-optimized",Placeholder="プロファイル識別子"};
    private IReadOnlyList<Run> 実行一覧=[];
    private readonly List<Measurement> 計測一覧=[];
    private int ページ;
    public 履歴比較画面(string DBパス){
        this.記録=new(new(DBパス));
        this.出力先=new(){Text=Path.Combine(Path.GetDirectoryName(DBパス)!,"profiles.json")};
        var 更新=new Button{Text="履歴を更新",AutomationId="共通履歴.更新"};更新.Clicked+=(_,_)=>this.実行を読む();
        var 次=new Button{Text="次の100件"};次.Clicked+=(_,_)=>{this.ページ++;this.実行を読む();};
        var 前=new Button{Text="最初へ"};前.Clicked+=(_,_)=>{this.ページ=0;this.実行を読む();};
        var 追加=new Button{Text="選択した実行の計測を選択肢に追加"};追加.Clicked+=(_,_)=>this.計測を読む();
        var 固定=new Button{Text="選択した基準を固定",AutomationId="共通履歴.固定"};固定.Clicked+=(_,_)=>this.操作(()=>{var 基準=this.選択(this.基準選択);this.記録.SetBaseline(基準.ID);this.詳細.Text="固定基準: "+基準.ID;});
        var 比較=new Button{Text="2回を比較して保存",AutomationId="共通履歴.比較"};比較.Clicked+=(_,_)=>this.操作(()=>{
            var モード=ComparisonMode.Regression;if(this.実験指定.IsChecked){モード=ComparisonMode.Experiment;}
            var 結果=this.記録.Compare(this.選択(this.基準選択).ID,this.選択(this.候補選択).ID,モード,double.Parse(this.許容差.Text,System.Globalization.CultureInfo.InvariantCulture));
            this.詳細.Text=JsonSerializer.Serialize(new{比較=結果,基準=条件の表示(this.選択(this.基準選択)),候補=条件の表示(this.選択(this.候補選択))},new JsonSerializerOptions(BenchmarkRepository.JSON){WriteIndented=true});
        });
        var 保存比較=new Button{Text="選択した実行の比較履歴・設定変更を表示"};保存比較.Clicked+=(_,_)=>this.操作(()=>{
            var 実行=this.実行一覧[this.実行選択.SelectedIndex];
            this.詳細.Text=JsonSerializer.Serialize(new{比較=this.記録.GetComparisons(実行.ID),変更=this.記録.GetChanges(実行.ID)},new JsonSerializerOptions{WriteIndented=true});
        });
        var 出力=new Button{Text="独立入力で検証した2回からプロファイルを出力",AutomationId="共通履歴.出力"};出力.Clicked+=(_,_)=>this.操作(()=>{
            this.記録.PublishProfile(this.プロファイル名.Text,this.選択(this.基準選択).ID,this.選択(this.候補選択).ID);
            File.WriteAllText(this.出力先.Text,this.記録.GetProfiles().ToJSON());this.詳細.Text="出力済み: "+this.出力先.Text;
        });
        this.基準選択.SelectedIndexChanged+=(_,_)=>this.条件を表示(this.基準選択);
        this.候補選択.SelectedIndexChanged+=(_,_)=>this.条件を表示(this.候補選択);
        this.Content=new ScrollView{Content=new VerticalStackLayout{Padding=12,Spacing=8,Children={
            new Label{Text="共有履歴・比較",FontSize=20},new HorizontalStackLayout{Children={更新,前,次}},this.実行選択,追加,
            this.基準選択,this.候補選択,固定,new HorizontalStackLayout{Spacing=12,Children={this.実験指定,new Label{Text="設定差を許す調整実験"},new Label{Text="悪化許容率"},this.許容差}},比較,保存比較,
            new Label{Text="中央値・ばらつき・割り当て量と、入力・設定の差を確認してください。7回未満や不一致を性能改善として採用しません。"},this.詳細,this.プロファイル名,this.出力先,出力
        }}};
        this.実行を読む();
    }
    private void 実行を読む()=>this.操作(()=>{
        this.実行一覧=this.記録.GetRuns(offset:this.ページ*100);
        this.実行選択.ItemsSource=this.実行一覧.Select(実行=>$"{実行.CreatedAt.LocalDateTime:g} {実行.Application} {実行.Status} {実行.ID}").ToArray();
    });
    private void 計測を読む()=>this.操作(()=>{
        if(this.実行選択.SelectedIndex<0){throw new InvalidOperationException("実行を選んでください。");}
        var 実行=this.実行一覧[this.実行選択.SelectedIndex];
        foreach(var 計測 in this.記録.GetMeasurements(実行.ID)){
            if(this.計測一覧.Any(既存=>既存.ID==計測.ID)){continue;}
            this.計測一覧.Add(計測);
            var 表示=$"{計測.Conditions.Backend}/{計測.Conditions.Implementation} {計測.Conditions.Count:N0}件 {計測.MedianNanoseconds:N1}ns ±{計測.StandardDeviation:N1} {計測.ID}";
            this.基準選択.Items.Add(表示);this.候補選択.Items.Add(表示);
        }
    });
    private Measurement 選択(Picker 選択){if(選択.SelectedIndex<0){throw new InvalidOperationException("計測を選んでください。");}return this.計測一覧[選択.SelectedIndex];}
    private static object 条件の表示(Measurement 計測)=>new{計測.ID,計測.RunID,計測.MedianNanoseconds,計測.StandardDeviation,条件=計測.Conditions,設定=JsonSerializer.Deserialize<JsonElement>(計測.Conditions.ParametersJSON),拡張=JsonSerializer.Deserialize<JsonElement>(計測.Conditions.GrowthJSON),測定値=計測.Samples};
    private void 条件を表示(Picker 選択)=>this.操作(()=>this.詳細.Text=JsonSerializer.Serialize(条件の表示(this.選択(選択)),new JsonSerializerOptions(BenchmarkRepository.JSON){WriteIndented=true}));
    private void 操作(Action 処理){try{処理();}catch(Exception 例外){this.詳細.Text=例外.Message;}}
}
