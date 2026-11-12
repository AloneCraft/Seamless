namespace Seamless.Management.Components;

public partial class 管理ワークスペース{
    private QueryExecutionObservation? 選択実績;
    private Guid? 選択トレース実行ID;
    private void トレース実行時(Guid? ID){this.選択トレース実行ID=ID;}
    private void 軽量実測完了時(ObjectExecutionStatistics 実測){this.最新実測=実測;this.選択実績=null;}
    private void 実績を選ぶ時(QueryExecutionObservation 記録){
        if(!記録.IdentityResolved||記録.Version is not{} 版||記録.ArtifactHash is null){
            this.案内=表示文言.取得("この実績は実行版が未確定のため、計画へ対応付けできません。");return;
        }
        this.計画対象=new(記録.DefinitionId,記録.ObjectId,版){HistoricalVersion=版};
        this.計画対象名=記録.ObjectId;this.選択表名=null;
        this.選択実績=記録;
        this.選択トレース実行ID=記録.ExecutionId;
        this.最新実測=new(記録.ExecutionId,記録.DefinitionId,記録.ObjectId,版,記録.ArtifactHash,記録.StartedAtUtc,記録.CompletedAtUtc,
            記録.Elapsed,記録.DatabaseGateWait,記録.TimeToFirstOutput,記録.ObservedOutputCount,記録.ReturnedOutputCount,記録.IsPartial){GateWaitStartedOffset=記録.GateWaitStartedOffset};
        this.表示=表示種別.計画;
    }
}
