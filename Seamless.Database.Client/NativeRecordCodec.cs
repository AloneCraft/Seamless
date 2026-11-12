using System.Security.Cryptography;
using System.Text;
using Seamless.Execution;
using Seamless.Execution.Codecs;

namespace Seamless.Database.Client;

internal static class NativeRecordResources{
    internal static readonly ResourceBudget DefaultBudget=new(64L<<20,0);
}

/// <summary>型付き Native の値形式と正規化規則を固定する。</summary>
public static class NativeRecordCodec<T>{
    public static string ContractId{get;}=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("seamless-native-record-v1|"+RecordGraphSerializer<T>.FormatId)));
    public static byte[] Serialize(T value,ResourceBudget? budget=null,CancellationToken cancellationToken=default,RecordGraphValidationLimits? limits=null){
        using var output=SerializeOwned(value,budget,cancellationToken,limits);return output.Value;
    }
    public static OwnedValue<byte[]> SerializeOwned(T value,ResourceBudget? budget=null,CancellationToken cancellationToken=default,RecordGraphValidationLimits? limits=null){
        budget??=NativeRecordResources.DefaultBudget;limits??=RecordGraphValidationLimits.Default;limits.CheckForNative();
        var output=RecordGraphSerializer<T>.SerializeOwned(value,budget,cancellationToken,limits.MaximumBytes);
        try{Validate(output.Value,budget,cancellationToken,limits);return output;}catch{output.Dispose();throw;}
    }
    public static void Validate(ReadOnlySpan<byte> bytes,ResourceBudget? budget=null,CancellationToken cancellationToken=default,RecordGraphValidationLimits? limits=null)=>
        RecordGraphSerializer<T>.Validate(bytes,budget??NativeRecordResources.DefaultBudget,cancellationToken,limits);
    public static T Deserialize(ReadOnlySpan<byte> bytes,ResourceBudget? budget=null,CancellationToken cancellationToken=default,RecordGraphValidationLimits? limits=null){
        budget??=NativeRecordResources.DefaultBudget;Validate(bytes,budget,cancellationToken,limits);
        return RecordGraphSerializer<T>.Deserialize(bytes,budget,cancellationToken);
    }
    public static OwnedValue<T> DeserializeOwned(ReadOnlySpan<byte> bytes,ResourceBudget? budget=null,CancellationToken cancellationToken=default){
        budget??=NativeRecordResources.DefaultBudget;Validate(bytes,budget,cancellationToken);
        return RecordGraphSerializer<T>.DeserializeOwned(bytes,budget,cancellationToken);
    }
}

internal static class NativeRecordLimits{
    internal static void CheckForNative(this RecordGraphValidationLimits limits){
        if(limits.MaximumBytes is <=0 or >=NativeFrame.MaximumBytes||limits.MaximumStringCharacters<=0||limits.MaximumArrayElements<=0||limits.MaximumNodes<=0||limits.MaximumDepth<=0){throw new ArgumentOutOfRangeException(nameof(limits));}
    }
}
