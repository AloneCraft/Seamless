using System.Buffers;

namespace Seamless.Database.Client;

/// <summary>MemoryPack 1.xの外側要求を復元前に検査する。引数内部の型別検査は別に行う。</summary>
public static class NativeQueryRequestWire{
    private static readonly NativeWireLimits Limits=new(){MaximumArrayElements=NativeFrame.MaximumBytes-1,MaximumStringCharacters=1024,MaximumValueNodes=16};
    static NativeQueryRequestWire(){_=NativeWireValidation.For<NativeQueryRequest>();}
    public static void Validate(ReadOnlySequence<byte> payload){
        if(payload.Length>Limits.MaximumBytes){throw Invalid();}
        var reader=new NativeWireValidation.Reader(payload,Limits);var members=reader.Byte();if(members is <5 or >8){throw Invalid();}
        for(var index=0;index<4;index++){if(!reader.String()){throw Invalid();}}
        var length=reader.Integer();reader.ArrayCount(length);reader.TakeProduct(length,1);
        if(members>=6){reader.Skip(16);}if(members>=7){reader.String();}if(members>=8){reader.String();}reader.Finish();
    }
    private static InvalidDataException Invalid()=>new("The native request violates its metadata or argument receive limits.");
}
