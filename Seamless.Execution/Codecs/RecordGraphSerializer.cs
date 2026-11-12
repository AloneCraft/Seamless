using System.Runtime.CompilerServices;
namespace Seamless.Execution.Codecs;

/// <summary>正規化と参照構造の保持を行う版付き形式。既存の保存形式とは別に選択する。</summary>
public readonly struct RecordGraphSerializer<T>:IResourceSerializer<T>{
    public static string FormatId=>"seamless-record-graph-v1|"+Convert.ToHexString(RecordGraphPolicy.Get(typeof(T)).Schema);
    public static IAdapter<T> CreateCodec(ResourceBudget budget)=>new RecordGraphCodec<T>(budget);

    public static ReadOnlySpan<byte> Serialize(ref T value)=>Serialize(value);

    /// <summary>構築中の出力も予算に含める。返却後の byte[] は呼び出し元が所有する。</summary>
    public static byte[] Serialize(T value,ResourceBudget? budget=null,CancellationToken cancellationToken=default){
        cancellationToken.ThrowIfCancellationRequested();
        budget??=RecordValueContext.DefaultBudget;
        var length=GetEncodedLength(value,budget,cancellationToken);
        using var output=budget.ReserveMemory(length);
        var bytes=new byte[length];
        Encode(value,bytes,budget,cancellationToken);
        return bytes;
    }

    /// <summary>符号化後も Dispose まで出力バイト列の予約を保持する。</summary>
    public static OwnedValue<byte[]> SerializeOwned(T value,ResourceBudget? budget=null,CancellationToken cancellationToken=default,int maximumBytes=int.MaxValue){
        ArgumentOutOfRangeException.ThrowIfNegative(maximumBytes);budget??=RecordValueContext.DefaultBudget;
        var length=GetEncodedLength(value,budget,cancellationToken);
        if(length>maximumBytes){throw new InvalidDataException("The record graph exceeds its output byte limit.");}
        var reservation=budget.ReserveMemory(checked(OwnedValue<byte[]>.HeaderBytes+40L+length));
        try{var bytes=new byte[length];Encode(value,bytes,budget,cancellationToken);return new(bytes,reservation);}
        catch{reservation.Dispose();throw;}
    }

    public static int GetEncodedLength(T value,ResourceBudget? budget=null,CancellationToken cancellationToken=default){
        using var state=new RecordGraphState(typeof(T),budget,cancellationToken);
        var writer=new RecordGraphWriter(default,state,true);
        writer.Header<T>();writer.Write(value);
        state.Reserve(writer.Length);
        return writer.Length;
    }

    /// <summary>destination の保持予算は呼び出し元が所有する。</summary>
    public static void Encode(T value,Span<byte> destination,ResourceBudget? budget=null,CancellationToken cancellationToken=default){
        using var state=new RecordGraphState(typeof(T),budget,cancellationToken);
        var writer=new RecordGraphWriter(destination,state,false);
        writer.Header<T>();writer.Write(value);
        if(writer.Length!=destination.Length){throw new InvalidDataException("The graph length changed between measurement and encoding.");}
    }

    public static T Deserialize(ReadOnlySpan<byte> bytes)=>Deserialize(bytes,null,default);

    /// <summary>値を復元せず、形式・型契約・参照と入力上限を検査する。</summary>
    public static void Validate(ReadOnlySpan<byte> bytes,ResourceBudget? budget=null,CancellationToken cancellationToken=default,RecordGraphValidationLimits? limits=null){
        cancellationToken.ThrowIfCancellationRequested();limits??=RecordGraphValidationLimits.Default;limits.Check();
        if(bytes.Length>limits.MaximumBytes){throw new InvalidDataException("The record graph exceeds the validation byte limit.");}
        using var state=new RecordGraphState(typeof(T),budget,cancellationToken);
        var reader=new RecordGraphReader(bytes,state,limits);reader.Header<T>();reader.Skip<T>();
        if(!reader.AtEnd){throw new InvalidDataException("Trailing record graph data.");}
    }

    public static long GetRetainedSize(T value,ResourceBudget? budget=null,CancellationToken cancellationToken=default){
        cancellationToken.ThrowIfCancellationRequested();_=RecordGraphPolicy.Get(typeof(T));
        var root=checked(OwnedValue<T>.HeaderBytes+(long)Unsafe.SizeOf<T>());
        if(!RuntimeHelpers.IsReferenceOrContainsReferences<T>()){return root;}
        using var state=new RecordGraphState(typeof(T),budget,cancellationToken);
        var writer=new RecordGraphWriter(default,state,true);writer.Write(value);
        return checked(root+state.OutputBytes);
    }

    public static OwnedValue<T> DeserializeOwned(ReadOnlySpan<byte> bytes,ResourceBudget? budget=null,CancellationToken cancellationToken=default){
        using var state=new RecordGraphState(typeof(T),budget,cancellationToken,true);
        var reader=new RecordGraphReader(bytes,state);reader.Header<T>();var value=reader.Read<T>();
        if(!reader.AtEnd){throw new InvalidDataException("Trailing record graph data.");}
        return state.TransferOutput(value);
    }

    /// <summary>復元中のコピーを予算に含める。返却した graph は呼び出し元が所有する。</summary>
    public static T Deserialize(ReadOnlySpan<byte> bytes,ResourceBudget? budget,CancellationToken cancellationToken=default){
        using var state=new RecordGraphState(typeof(T),budget,cancellationToken);
        var reader=new RecordGraphReader(bytes,state);
        reader.Header<T>();
        var value=reader.Read<T>();
        if(!reader.AtEnd){throw new InvalidDataException("Trailing record graph data.");}
        return value;
    }
}

/// <summary>ExecutionScope の所有バッファ・親予算・取消しへ接続する adapter。</summary>
public sealed class RecordGraphCodec<T>(ResourceBudget? budget=null,CancellationToken cancellationToken=default):IOwnedAdapter<T>,IEqualityComparer<T>{
    public string FormatId=>RecordGraphSerializer<T>.FormatId;
    public int GetEncodedLength(T value)=>RecordGraphSerializer<T>.GetEncodedLength(value,budget,cancellationToken);
    public void Encode(T value,Span<byte> destination)=>RecordGraphSerializer<T>.Encode(value,destination,budget,cancellationToken);
    public T Decode(ReadOnlySpan<byte> source)=>RecordGraphSerializer<T>.Deserialize(source,budget,cancellationToken);
    public long GetRetainedSize(T value)=>RecordGraphSerializer<T>.GetRetainedSize(value,budget,cancellationToken);
    public OwnedValue<T> DecodeOwned(ReadOnlySpan<byte> source)=>RecordGraphSerializer<T>.DeserializeOwned(source,budget,cancellationToken);
    public bool Equals(T? first,T? second)=>RecordValueStrategy<T>.Equals(first!,second!,budget,cancellationToken);
    public int GetHashCode(T value)=>RecordValueStrategy<T>.GetHashCode(value,budget,cancellationToken);
}
