using Seamless.Execution;
using Seamless.Execution.Codecs;

namespace Seamless.Collections.Tsavorite;

/// <summary>操作オブジェクトとコールバックで同じ所有ストアの予算を使う。</summary>
internal readonly struct StoreSerializer<T,TStrategy> where TStrategy:global::Seamless.Collections.Serializers.IStrategy<T>{
    private static readonly Func<ResourceBudget,IAdapter<T>>? Factory=CreateFactory();
    private readonly ResourceBudget? budget;
    private readonly IAdapter<T>? codec;

    internal StoreSerializer(ResourceBudget? budget){
        this.budget=budget;this.codec=null;
        if(budget is not null&&Factory is not null){this.codec=Factory(budget);}
    }

    private static Func<ResourceBudget,IAdapter<T>>? CreateFactory(){
        if(!typeof(IResourceSerializer<T>).IsAssignableFrom(typeof(TStrategy))){return null;}
        return typeof(StoreSerializer<T,TStrategy>).GetMethod(nameof(CreateCodec),BindingFlags.Static|BindingFlags.NonPublic)!
            .MakeGenericMethod(typeof(TStrategy)).CreateDelegate<Func<ResourceBudget,IAdapter<T>>>();
    }
    private static IAdapter<T> CreateCodec<TResourceSerializer>(ResourceBudget budget) where TResourceSerializer:IResourceSerializer<T>{return TResourceSerializer.CreateCodec(budget);}

    internal SerializedStoreValue Serialize(ref T value){
        if(this.codec is null){return new(TStrategy.Serialize(ref value),null);}
        var length=this.GetEncodedLength(ref value);
        var reservation=this.budget!.ReserveMemory(checked(64L+length));
        try{
            var bytes=new byte[length];this.codec.Encode(value,bytes);return new(bytes,reservation);
        }catch{reservation.Dispose();throw;}
    }

    internal int GetEncodedLength(ref T value){
        if(this.codec is null){return TStrategy.Serialize(ref value).Length;}
        var length=this.codec.GetEncodedLength(value);ArgumentOutOfRangeException.ThrowIfNegative(length);return length;
    }

    internal T Deserialize(ReadOnlySpan<byte> bytes){
        if(this.codec is null){return TStrategy.Deserialize(bytes);}
        return this.codec.Decode(bytes);
    }
}

/// <summary>ログへのコピーと Pending 完了まで符号化バッファの予約を保持する。</summary>
internal readonly ref struct SerializedStoreValue(ReadOnlySpan<byte> bytes,ResourceReservation? reservation){
    internal ReadOnlySpan<byte> Bytes{get;}=bytes;
    public void Dispose()=>reservation?.Dispose();
}
