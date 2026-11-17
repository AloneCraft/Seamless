using Seamless.Management;

namespace Seamless.Management.Components;

public partial class 実行計画ビュー{
    private enum 計画描画方式{DAG,Sankey,Gantt,Heat,Tree,DOT}
    private 計画描画方式 描画方式;
    private string 共通検索="";
    private データサンキー? 保存用サンキー;
    private 実行Gantt? 保存用ガント;
    private 資源ヒートガント? 保存用ヒート;
    private PlanSnapshotViewState 保存状態を取得()=>new(this.描画方式.ToString(),this.共通検索,this.選択ノード?.Id){Sankey=this.保存用サンキー?.保存設定??new(),Heat=this.保存用ヒート?.保存設定??new(),GanttZoom=this.保存用ガント?.保存倍率??1};
    private bool サンキー作成済みか,ガント作成済みか,ヒート作成済みか;
    private string? 可視化案内;
    private ExecutionPlanDocument? 可視化元計画;
    private QueryPlanResult? 可視化元対象;
    private ObjectExecutionStatistics? 可視化元実測,観測由来実測;
    private QueryExecutionObservation? 可視化元観測,変換元観測;
    private Guid? 可視化元実行;
    private ObjectExecutionProfile? 可視化元ノード実測;
    private QueryVisualizationData? 可視化キャッシュ;
    private Guid? 現在実行ID=>this.トレース実行ID??this.対象適合観測?.ExecutionId??this.対象適合実測?.ExecutionId??this.対象適合資源?.ExecutionId??this.対象適合ノード実測?.ExecutionId;
    private ObjectExecutionProfile? 対象適合ノード実測{get{if(this.取得結果 is{FromDraft:false} 対象&&this.ノード実測 is{} 記録&&記録.DefinitionId==対象.DefinitionId&&記録.ObjectId==対象.Selection.ObjectId&&記録.Version==対象.Version&&記録.ArtifactHash==対象.ArtifactHash){return 記録;}return null;}}
    private ObjectExecutionProfile? 適合ノード実測{get{var 記録=this.対象適合ノード実測;if(記録?.ExecutionId==this.現在実行ID){return 記録;}return null;}}
    private ResourceHeatTrace? 対象適合資源{get{if(this.取得結果 is{FromDraft:false} 対象&&this.資源記録 is{IdentityResolved:true} 記録&&記録.DefinitionId==対象.DefinitionId&&記録.ObjectId==対象.Selection.ObjectId&&記録.Version==対象.Version&&記録.ArtifactHash==対象.ArtifactHash){return 記録;}return null;}}
    private ResourceHeatTrace? 適合資源{get{if(this.資源閲覧拒否か){return null;}if(this.適合保存資源 is{} 保存){return 保存;}var 値=this.対象適合資源;if(値?.ExecutionId==this.現在実行ID){return 値;}return null;}}
    private PlanNodeRef? 共通選択{get{if(this.選択計画 is{} 計画&&this.選択ノード is{} ノード){return new(QueryVisualizationData.PlanKey(計画),ノード.Id);}return null;}}
    private QueryVisualizationData? 共通可視化{
        get{
            var 計画=this.選択計画;var 対象=this.取得結果;var 実測=this.適合実測;var 観測=this.適合観測;var 実行=this.現在実行ID;var ノード実測=this.適合ノード実測;
            if(ReferenceEquals(this.可視化元計画,計画)&&ReferenceEquals(this.可視化元対象,対象)&&ReferenceEquals(this.可視化元実測,実測)&&ReferenceEquals(this.可視化元観測,観測)&&ReferenceEquals(this.可視化元ノード実測,ノード実測)&&this.可視化元実行==実行){return this.可視化キャッシュ;}
            this.可視化元計画=計画;this.可視化元対象=対象;this.可視化元実測=実測;this.可視化元観測=観測;this.可視化元ノード実測=ノード実測;this.可視化元実行=実行;this.可視化キャッシュ=null;this.可視化案内=null;
            if(計画 is null||対象 is null){return null;}
            try{
                var 値=QueryVisualizationData.FromPlan(対象,計画,実行);
                if(実測 is not null){値=実行時間モデル.実測を投影(値,実測,観測);}
                if(ノード実測 is not null){値=QueryProfileProjection.Apply(値,ノード実測);}
                this.可視化キャッシュ=値;
            }catch(InvalidDataException 例外){this.可視化案内=例外.Message;}
            return this.可視化キャッシュ;
        }
    }
    private ObjectExecutionStatistics 観測から実測(QueryExecutionObservation 観測){
        if(!ReferenceEquals(this.変換元観測,観測)){
            this.変換元観測=観測;this.観測由来実測=new(観測.ExecutionId,観測.DefinitionId,観測.ObjectId,観測.Version!.Value,観測.ArtifactHash!,観測.StartedAtUtc,観測.CompletedAtUtc,観測.Elapsed,観測.DatabaseGateWait,観測.TimeToFirstOutput,観測.ObservedOutputCount,観測.ReturnedOutputCount,観測.IsPartial){GateWaitStartedOffset=観測.GateWaitStartedOffset};
        }
        return this.観測由来実測!;
    }
    private void 表示選択時(string 値){
        if(!Enum.TryParse<計画描画方式>(値,out var 方式)){return;}
        this.描画方式=方式;
        if(方式==計画描画方式.Sankey){this.サンキー作成済みか=true;}
        if(方式==計画描画方式.Gantt){this.ガント作成済みか=true;}
        if(方式==計画描画方式.Heat){this.ヒート作成済みか=true;}
    }
    private void 検索を共有する時(string 値)=>this.共通検索=値;
    private Task 共有ノード選択時Async(PlanNodeRef? 参照){
        if(参照 is null){return this.ノード選択時Async(null);}
        if(this.選択計画 is not{} 計画||参照.PlanKey!=QueryVisualizationData.PlanKey(計画)){return Task.CompletedTask;}
        return this.ノード選択時Async(計画.Nodes.SingleOrDefault(ノード=>ノード.Id==参照.NodeId));
    }
    private void 読取を停止(){
        this.資源読込を止める();
        this.読込世代++;this.選択世代++;this.生成世代++;
        this.読込取消?.Cancel();this.生成取消?.Cancel();this.読込中か=false;this.生成取得中か=false;
    }
    private static string 表示方式名(計画描画方式 値)=>値 switch{計画描画方式.Sankey=>"データサンキー",計画描画方式.Gantt=>"実行タイムライン",計画描画方式.Heat=>"資源使用ヒートガント",計画描画方式.Tree=>"ノードツリー",計画描画方式.DOT=>"DOT グラフ",_=>"実行DAG"};
    private static string 表示アイコン(計画描画方式 値)=>値 switch{計画描画方式.Tree=>"ノードツリー",計画描画方式.DOT=>"DOT グラフ",計画描画方式.Gantt=>"実行",_=>"グラフ"};
}
