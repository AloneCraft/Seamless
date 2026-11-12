using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Seamless.Management;

namespace Seamless.Management.Components;

public partial class 計画グラフ{
    [Parameter,EditorRequired]public ExecutionPlanDocument 計画{get;set;}=default!;
    [Parameter]public EventCallback<ExecutionPlanNode?> ノード選択時{get;set;}
    [Parameter]public EventCallback<PlanSnapshotDisplaySettings> 表示設定変更時{get;set;}
    [Parameter]public IReadOnlyDictionary<string,string>? 対応コード一覧{get;set;}
    [Parameter]public string? 初期表示層{get;set;}
    [Parameter]public bool 選択制御か{get;set;}
    [Parameter]public string? 共有選択ID{get;set;}
    [Parameter]public string? 共有検索語{get;set;}
    [Parameter]public EventCallback<string> 共有検索変更時{get;set;}
    [Parameter]public QueryVisualizationData? 実測データ{get;set;}
    private string? 前初期表示層;
    private ExecutionPlanDocument? 前計画;
    private string? 計画識別,選択ID;
    private 計画表示層 表示層=計画表示層.処理;
    private 計画グラフ配置 配置=new();
    private string 検索="";
    private HashSet<string> 一致=new(StringComparer.Ordinal);
    private bool 補助線表示か=true,入力番号表示か,全体表示か=true,破棄済みか;
    private double 倍率=100;
    private readonly HashSet<string> 非表示接続=new(StringComparer.Ordinal);
    private ElementReference 画面;
    private IJSObjectReference? モジュール,操作;
    private readonly SemaphoreSlim 初期化=new(1,1);
    private readonly CancellationTokenSource 寿命=new();
    private readonly string 入力矢印ID="plan-input-"+Guid.NewGuid().ToString("N");
    private readonly string 集約記号ID="plan-summary-"+Guid.NewGuid().ToString("N");
    private const int 簡略倍率=20,初期詳細上限=64,詳細上限=256,周辺余白=200;
    private readonly Dictionary<string,計画配置ノード> 位置一覧=new(StringComparer.Ordinal);
    private 計画配置ノード[] 詳細ノード=[],簡略ノード=[],描画集約元=[];
    private 計画配置接続[] 描画接続=[];
    private 合成線[] 合成接続=[];
    private bool 簡略表示か;
    private 描画窓? 現在窓;
    private double 実表示倍率;
    private long 描画世代,通知済み世代,最終通知番号;
    private DotNetObjectReference<計画グラフ>? 参照;
    private bool 大規模か=>this.配置.ノード.Length>初期詳細上限||this.配置.接続.Length>128;
    private sealed record 合成線(string 種類,string 経路,int 件数);
    private readonly record struct 描画窓(double 左,double 上,double 右,double 下){
        internal bool 交差(double 左,double 上,double 右,double 下)=>右>=this.左&&左<=this.右&&下>=this.上&&上<=this.下;
    }
    private ExecutionPlanNode? 選択ノード=>this.計画?.Nodes.FirstOrDefault(ノード=>ノード.Id==this.選択ID);
    private IEnumerable<string> 利用接続種別=>this.配置.接続.Select(接続=>接続.種類).Distinct(StringComparer.Ordinal);
    private string 表示範囲=>$"0 0 {this.配置.幅} {this.配置.高さ}";
    private string 図寸法{
        get{
            if(this.全体表示か){return "width:100%;height:620px";}
            return FormattableString.Invariant($"width:{this.配置.幅*this.倍率/100.0}px;height:{this.配置.高さ*this.倍率/100.0}px");
        }
    }
    private string 倍率表示{
        get{
            if(this.全体表示か){return 表示文言.取得("全体表示");}
            return this.倍率.ToString("0.#",CultureInfo.CurrentCulture)+"%";
        }
    }
    protected override Task OnParametersSetAsync(){
        if(this.計画 is null){return Task.CompletedTask;}
        if(ReferenceEquals(this.計画,this.前計画)&&this.初期表示層==this.前初期表示層){this.共有状態を適用();return Task.CompletedTask;}
        var 識別=this.計画.MethodKey+"\n"+this.計画.SourceHash+"\n"+this.計画.GeneratedBodyHash;
        var 別計画か=this.計画識別!=識別;
        if(別計画か){
            this.選択ID=null;this.検索="";this.全体表示か=true;this.倍率=100;
            this.表示層=計画表示層.処理;
            if(!this.計画.Nodes.Any(ノード=>計画グラフ配置.層を取得(ノード)==this.表示層)){this.表示層=計画表示層.すべて;}
        }
        if(this.初期表示層!=this.前初期表示層||別計画か&&this.初期表示層 is not null){
            this.表示層=this.初期表示層 switch{"Original"=>計画表示層.元のクエリ,"Logical"=>計画表示層.論理処理,"OriginalExpression"=>計画表示層.数式,_=>計画表示層.処理};
        }
        this.前初期表示層=this.初期表示層;this.計画識別=識別;this.前計画=this.計画;this.配置を更新();
        this.共有状態を適用();
        return this.設定変更時Async();
    }
    private void 共有状態を適用(){
        var 更新か=false;
        if(this.選択制御か&&this.選択ID!=this.共有選択ID){this.選択ID=this.共有選択ID;更新か=true;}
        if(this.共有検索語 is not null&&this.検索!=this.共有検索語){this.検索=this.共有検索語;this.検索を更新();更新か=true;}
        if(更新か){this.描画を更新();}
    }
    private void 配置を更新(){
        this.配置=計画グラフ配置.作成(this.計画,this.表示層);this.位置一覧.Clear();
        foreach(var ノード in this.配置.ノード){this.位置一覧.Add(ノード.元.Id,ノード);}
        this.描画世代++;this.最終通知番号=0;this.現在窓=null;this.実表示倍率=0;this.検索を更新();this.描画を更新();
    }
    private void 描画を更新(){
        var カメラ有効か=this.現在窓 is not null;
        if(!this.全体表示か&&Math.Abs(this.実表示倍率-this.倍率)>0.01){カメラ有効か=false;}
        this.簡略表示か=this.大規模か&&(!カメラ有効か||this.実表示倍率<簡略倍率);
        var 窓=this.現在窓;
        var 候補=this.配置.ノード;
        if(this.大規模か&&!this.簡略表示か&&窓 is{} 候補範囲){候補=候補.Where(ノード=>候補範囲.交差(ノード.X,ノード.Y,ノード.X+計画配置ノード.幅,ノード.下端)).ToArray();}
        if(this.簡略表示か){this.詳細ノード=[];this.簡略ノード=候補;}
        else{
            this.詳細ノード=候補.Take(詳細上限).ToArray();
            if(this.選択ID is{} ID&&this.位置一覧.TryGetValue(ID,out var 選択)&&!this.詳細ノード.Any(項目=>項目.元.Id==ID)){this.詳細ノード=[..this.詳細ノード.Take(詳細上限-1),選択];}
            var 詳細=this.詳細ノード.Select(項目=>項目.元.Id).ToHashSet(StringComparer.Ordinal);
            this.簡略ノード=候補.Where(項目=>!詳細.Contains(項目.元.Id)).ToArray();
        }
        this.描画接続=this.配置.接続.Where(接続=>this.接続表示か(接続.種類)&&(this.簡略表示か||!this.大規模か||窓 is{} 接続範囲&&辺が交差か(接続.入力.中心X,接続.入力.下端,接続.出力.中心X,接続.出力.Y,接続範囲))).ToArray();
        this.合成接続=[];
        if(this.大規模か){
            this.合成接続=this.描画接続.GroupBy(接続=>接続.種類).Select(組=>{
                var 線=new StringBuilder();foreach(var 接続 in 組){線.Append(入力線(接続)).Append(' ');}return new 合成線(組.Key,線.ToString(),組.Count());
            }).ToArray();
        }
        this.描画集約元=this.配置.集約元.Where(元=>this.簡略表示か||!this.大規模か||窓 is{} 集約範囲&&辺が交差か(元.中心X,元.下端,this.配置.幅/2,this.配置.結果Y,集約範囲)).ToArray();
    }
    private static bool 辺が交差か(int 元X,int 元Y,int 先X,int 先Y,描画窓 窓){
        var 差=Math.Max(30,Math.Abs(先Y-元Y)/2);
        return 窓.交差(Math.Min(元X,先X),Math.Min(元Y,先Y-差),Math.Max(元X,先X),Math.Max(元Y+差,先Y));
    }
    [JSInvokable]public Task 表示範囲変更Async(long 世代,long 番号,double 左,double 上,double 幅,double 高さ,double 倍率){
        if(this.破棄済みか||世代!=this.描画世代||番号<=this.最終通知番号||!double.IsFinite(左)||!double.IsFinite(上)||!double.IsFinite(幅)||!double.IsFinite(高さ)||!double.IsFinite(左+幅)||!double.IsFinite(上+高さ)||!double.IsFinite(倍率)||幅<=0||高さ<=0||倍率<=0){return Task.CompletedTask;}
        return this.InvokeAsync(()=>{if(this.破棄済みか||世代!=this.描画世代||番号<=this.最終通知番号){return;}this.最終通知番号=番号;this.現在窓=new(左-周辺余白,上-周辺余白,左+幅+周辺余白,上+高さ+周辺余白);this.実表示倍率=倍率;this.描画を更新();this.StateHasChanged();});
    }
    private Task 表示層変更時Async(ChangeEventArgs 引数){
        if(Enum.TryParse<計画表示層>(引数.Value?.ToString(),out var 層)){this.表示層=層;this.配置を更新();}
        return this.設定変更時Async();
    }
    private async Task 検索変更時(ChangeEventArgs 引数){this.検索=引数.Value?.ToString()??"";this.検索を更新();await this.共有検索変更時.InvokeAsync(this.検索);}
    private void 検索を更新(){
        this.一致=this.配置.ノード.Where(配置=>this.検索一致か(配置.元)).Select(配置=>配置.元.Id).ToHashSet(StringComparer.Ordinal);
    }
    private bool 検索一致か(ExecutionPlanNode ノード){
        if(string.IsNullOrWhiteSpace(this.検索)){return false;}
        return ノード.Id.Contains(this.検索,StringComparison.OrdinalIgnoreCase)||ノード.Label.Contains(this.検索,StringComparison.OrdinalIgnoreCase)||ノード.Kind.Contains(this.検索,StringComparison.OrdinalIgnoreCase)||
            ノード.SourcePath?.Contains(this.検索,StringComparison.OrdinalIgnoreCase)==true||ノード.Properties.Any(項目=>項目.Key.Contains(this.検索,StringComparison.OrdinalIgnoreCase)||項目.Value.Contains(this.検索,StringComparison.OrdinalIgnoreCase));
    }
    private string 検索状態(string ID){
        if(string.IsNullOrWhiteSpace(this.検索)){return "none";}
        if(this.一致.Contains(ID)){return "match";}
        return "other";
    }
    private async Task 次の一致時Async(){
        var 候補=this.配置.ノード.Where(ノード=>this.一致.Contains(ノード.元.Id)).ToArray();
        if(候補.Length==0){return;}
        var 現在=Array.FindIndex(候補,ノード=>ノード.元.Id==this.選択ID);
        var 次=候補[(現在+1)%候補.Length].元;
        await this.ノード選択時Async(次);
        await this.ノードへ移動Async(次.Id);
    }
    private Task 検索キー時Async(KeyboardEventArgs 引数){
        if(引数.Key=="Enter"){return this.次の一致時Async();}
        return Task.CompletedTask;
    }
    private async Task ノード選択時Async(ExecutionPlanNode? ノード){this.選択ID=ノード?.Id;this.描画を更新();await this.ノード選択時.InvokeAsync(ノード);}
    private async Task ノードへ移動Async(string ID){
        if(this.操作 is not null&&this.位置一覧.TryGetValue(ID,out var 位置)){
            await this.操作.InvokeVoidAsync("ノードを表示",this.寿命.Token,ID,位置.X,位置.Y,計画配置ノード.幅,計画配置ノード.高さ);
        }
    }
    [JSInvokable]public Task キーボードノード移動Async(long 世代,int 方向){
        if(this.破棄済みか||世代!=this.描画世代||方向 is not(-1 or 1)||this.配置.ノード.Length==0){return Task.CompletedTask;}
        return this.InvokeAsync(async()=>{
            if(this.破棄済みか||世代!=this.描画世代){return;}
            var 現在=Array.FindIndex(this.配置.ノード,項目=>項目.元.Id==this.選択ID);
            var 次=Math.Clamp(現在+方向,0,this.配置.ノード.Length-1);
            var ID=this.配置.ノード[次].元.Id;await this.ノード選択時Async(this.配置.ノード[次].元);await this.ノードへ移動Async(ID);this.StateHasChanged();
        });
    }
    private async Task 倍率変更時Async(double 係数){
        var 基準=this.倍率;
        if(this.全体表示か&&this.操作 is not null){基準=await this.操作.InvokeAsync<double>("表示倍率を取得");}
        if(!double.IsFinite(基準)||基準<=0){基準=100;}
        this.倍率=Math.Clamp(基準*係数,Math.Min(1,基準),Math.Max(400,基準));this.全体表示か=false;this.描画を更新();
    }
    private void 標準倍率時(){this.倍率=100;this.全体表示か=false;this.描画を更新();}
    private void 全体表示時(){this.全体表示か=true;this.現在窓=null;this.描画を更新();}
    private string 集約経路=>string.Join(' ',this.描画集約元.Select(this.集約線));
    private string ノード説明(ExecutionPlanNode ノード){
        if(ノード.SourcePath is null||ノード.Length<=0||this.対応コード一覧 is null||!this.対応コード一覧.TryGetValue(ノード.Id,out var コード)){return ノード.Label;}
        return ノード.Label+"\n"+コード;
    }
    private bool 接続表示か(string 種類)=>!this.非表示接続.Contains(種類);
    private Task 接続表示変更時Async(string 種類,ChangeEventArgs 引数){
        if(引数.Value is true){this.非表示接続.Remove(種類);}else{this.非表示接続.Add(種類);}
        this.描画を更新();
        return this.設定変更時Async();
    }
    private Task 設定変更時Async(){
        var 層=this.表示層 switch{計画表示層.処理=>"Selected",計画表示層.元のクエリ=>"Original",計画表示層.数式=>"OriginalExpression",計画表示層.判断と条件=>"Optimization",計画表示層.その他=>"Unknown",計画表示層.論理処理=>"Logical",_=>"All"};
        return this.表示設定変更時.InvokeAsync(new(層,this.非表示接続.Order(StringComparer.Ordinal).ToArray(),this.入力番号表示か,true){ShowSummaryEdges=this.補助線表示か});
    }
    private static string 層名(計画表示層 層)=>層 switch{
        計画表示層.すべて=>"すべての層",
        計画表示層.処理=>"採用された処理",
        計画表示層.元のクエリ=>"元のクエリ",
        計画表示層.数式=>"元の数式",
        計画表示層.判断と条件=>"判断と条件",
        計画表示層.論理処理=>"採用処理と式",
        _=>"その他のノード"
    };
    private string 型表示(ExecutionPlanNode ノード){
        if(ノード.OutputType is{} 出力型){return 出力型;}
        if(ノード.Properties.TryGetValue("OutputType",out var 型)){return 型;}
        return 表示文言.取得("型の記録なし");
    }
    private static string NULL許容名(ExecutionPlanNode ノード)=>ノード.Nullability switch{
        "NotNullable"=>"NULL非許容",
        "Nullable"=>"NULL許容",
        _=>"NULL許容性不明"
    };
    private static string 計測状態名(ExecutionPlanNode ノード)=>ノード.Measurement switch{
        "NotIndependentlyMeasured"=>"独立計測なし",
        "NotCollected"=>"未収集",
        _=>"計測状態不明"
    };
    private static string 接続名(string 種類)=>種類 switch{
        "OriginalDataFlow"=>"元クエリのデータフロー",
        "ExpressionDependency"=>"元の数式の依存",
        "SelectedReference"=>"採用処理の入力参照",
        "SourceMapping"=>"ソース対応",
        _=>"種別未記録の接続"
    };
    private static string 接続クラス(string 種類)=>種類 switch{
        "OriginalDataFlow"=>"元データ接続",
        "ExpressionDependency"=>"数式依存接続",
        "SelectedReference"=>"採用入力接続",
        "SourceMapping"=>"ソース対応接続",
        _=>"不明種別接続"
    };
    private string 入力名(計画配置接続 接続){
        if(接続.入力役割 is{} 役割){return 役割;}
        return 表示文言.取得("入力{0}",接続.入力番号);
    }
    private static string 入力線(計画配置接続 接続)=>曲線(接続.入力.中心X,接続.入力.下端,接続.出力.中心X,接続.出力.Y);
    private string 集約線(計画配置ノード 元)=>曲線(元.中心X,元.下端,this.配置.幅/2,this.配置.結果Y);
    private static string 曲線(int 元X,int 元Y,int 先X,int 先Y){
        var 差=Math.Max(30,Math.Abs(先Y-元Y)/2);
        return FormattableString.Invariant($"M {元X} {元Y} C {元X} {元Y+差}, {先X} {先Y-差}, {先X} {先Y}");
    }
    protected override async Task OnAfterRenderAsync(bool firstRender){
        await this.初期化.WaitAsync();
        try{
            if(this.破棄済みか){return;}
            if(firstRender){
                this.参照=DotNetObjectReference.Create(this);
                this.モジュール=await this.スクリプト.InvokeAsync<IJSObjectReference>("import",this.寿命.Token,"./_content/Seamless.Management.Components/execution-plan.js");
                this.操作=await this.モジュール.InvokeAsync<IJSObjectReference>("計画グラフを初期化",this.寿命.Token,this.画面,this.参照,this.描画世代);
                this.通知済み世代=this.描画世代;
            }else if(this.操作 is not null&&this.通知済み世代!=this.描画世代){
                this.通知済み世代=this.描画世代;await this.操作.InvokeVoidAsync("設定を更新",this.寿命.Token,this.描画世代);
            }
        }catch(JSDisconnectedException){}
        catch(OperationCanceledException)when(this.寿命.IsCancellationRequested){}
        finally{this.初期化.Release();}
    }
    public async ValueTask DisposeAsync(){
        if(this.破棄済みか){return;}
        this.破棄済みか=true;this.描画世代++;this.寿命.Cancel();await this.初期化.WaitAsync();
        try{await スクリプト参照.解放Async(this.操作,this.モジュール);}
        finally{this.参照?.Dispose();this.初期化.Release();this.寿命.Dispose();}
    }
}
