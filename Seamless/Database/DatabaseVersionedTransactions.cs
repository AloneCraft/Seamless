using Tsavorite.core;
using System.Buffers.Binary;
using System.Security.Cryptography;
using Seamless.Collections.Tsavorite;
using Seamless.Transactions.Collections;
namespace Seamless.Database;

public abstract partial class DatabaseContainer{
    private VersionedTransactionalTable<TKey,TRecord> CreateVersionedParticipant<TKey,TRecord>(Func<TRecord,TKey> keySelector,string name) where TKey:notnull{
        this.EnsureSharedStorage();var sizing=this.ResolveTableStorage(name);var identity=this.TransactionCodecIdentity<TKey,TRecord>(name);
        this.CheckRecordGraphContract(name,identity);
        var encoding=VersionedRecordEncoding.Auto;if(this.Options.RecordSerialization==DatabaseRecordSerialization.CanonicalV1){encoding=VersionedRecordEncoding.Canonical;}
        if(this.Options.RecordSerialization==DatabaseRecordSerialization.RecordGraphV1){encoding=VersionedRecordEncoding.RecordGraphV1;}
        var storageIdentity=VersionedTransactionalTable<TKey,TRecord>.StorageIdentity(identity,encoding);
        var data=this.sharedStore!.CreateTsavoriteHistory(name,new TsavoriteTuning{IndexSize=checked(sizing.IndexSize*64),PageSize=sizing.PageSize,SegmentSize=sizing.SegmentSize,LogMemorySize=sizing.MemorySize},storageIdentity);
        var id=BinaryPrimitives.ReadInt32LittleEndian(SHA256.HashData(Encoding.UTF8.GetBytes(name)))&int.MaxValue;if(id==0){id=1;}
        VersionedTransactionalTable<TKey,TRecord> participant;
        if(this.Options.GatewayVersionedBinding is{} binding){participant=new(binding.Group.Partition,id,identity,data,this.executionResources,this.Options.TransactionMemoryLimit,keySelector,encoding,binding.Route(name));}
        else{participant=new(this.Options.GatewayProfile?.DatabaseId??0,id,identity,data,this.executionResources,this.Options.TransactionMemoryLimit,keySelector,encoding);}
        try{if(this.Options.GatewayVersionedBinding is{} owner){owner.AddPending(participant);}else{this.sharedCoordinator!.Register(participant);}this.sharedParticipants.Add(name,participant);this.sharedHandles.Add((participant.CloseForMaintenance,participant.ReopenAfterMaintenance));return participant;}
        catch{participant.Dispose();throw;}
    }
}
