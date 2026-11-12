using System.Text.Json;
namespace Benchmark.TpcE.Core;

/// <summary>比較driverの継続位置を、版付きの業務行と同じWAL確定へ結び付ける。</summary>
public static class VersionedDriverState{
    public static byte[] Encode(DriverState state)=>JsonSerializer.SerializeToUtf8Bytes(new Envelope("tpc-driver-v1",state),DriverJson.Options);
    public static DriverState? Restore(ITpcEEngine engine){
        var domain=engine.Database.TransactionDomain;if(domain is null){return null;}
        if(domain.RecoveredMetadata.IsEmpty){
            if(domain.Coordinator.AppliedLsn!=0){throw new InvalidDataException("Committed TPC rows have no driver continuation metadata.");}return null;
        }
        if(!domain.RecoveredMetadataCoversRows){throw new InvalidDataException("TPC rows advanced without matching driver continuation metadata.");}
        var value=JsonSerializer.Deserialize<Envelope>(domain.RecoveredMetadata.Span,DriverJson.Options)??throw new InvalidDataException("Missing TPC driver metadata.");
        if(value.Format!="tpc-driver-v1"||value.State is null||value.State.Version!=DriverState.FormatVersion){throw new InvalidDataException("Invalid TPC driver metadata.");}return value.State;
    }
    public static byte[] Preview(Driver driver,TxnRequest request,TxnResult result)=>Encode(driver.PreviewCompleted(request,result));
    public static void Checkpoint(ITpcEEngine engine,DriverState state){
        var db=engine.Database;if(db.TransactionDomain is not null){db.Log.Begin();try{db.Log.Commit(Encode(state));}finally{if(db.Log.IsRecording){db.Log.Rollback();}}}
        db.Checkpoint();
    }
    private sealed record Envelope(string Format,DriverState State);
}
