using System.Text.Json;
namespace Benchmark.Tpc.Server;
/// <summary>
/// ストアと一緒に保存される「再実行ログのどこまでが保存済みか」。チェックポイント(Store.Save)と同時に確定するので、
/// 開き直したら、この番号より後のログだけを当てればよい。
/// </summary>
public sealed class RedoCheckpoint:Seamless.Execution.IStoreComponent{
    public string Name=>"~redo-checkpoint";
    /// <summary>保存済みの状態に含まれる最後の取引の通し番号。</summary>
    public long LastSequence{get;set;}
    public string Save()=>JsonSerializer.Serialize(this.LastSequence);
    public void Flush(){
    }
    public void Load(string state){
        this.LastSequence=JsonSerializer.Deserialize<long>(state);
    }
    public void Dispose(){
    }
}
