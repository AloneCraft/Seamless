using Seamless.Execution;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Seamless.Database.Client;

public interface INativeRecordQueryModule:INativeQueryModule{
    NativeRecordQueryContract GetContract(string queryId);
}

/// <summary>生成済みメソッドの引数・結果を、同じ型と版へ固定する。</summary>
public sealed class NativeRecordQueryContract{
    private delegate void Validator(ReadOnlySpan<byte> payload,ResourceBudget? budget,CancellationToken cancellationToken);
    private readonly Validator arguments,result;
    public Type ArgumentsType{get;}
    public Type ResultType{get;}
    public string ArgumentsContract{get;}
    public string ResultContract{get;}
    private NativeRecordQueryContract(Type argumentsType,Type resultType,string argumentsContract,string resultContract,Validator arguments,Validator result){
        this.ArgumentsType=argumentsType;this.ResultType=resultType;this.ArgumentsContract=argumentsContract;this.ResultContract=resultContract;this.arguments=arguments;this.result=result;
    }
    public static NativeRecordQueryContract For<TArguments,TResult>()=>Cache<TArguments,TResult>.Value;
    public static NativeRecordQueryContract For<TArguments,TResult,TContract>()=>BoundCache<TArguments,TResult,TContract>.Value;
    private static class Cache<TArguments,TResult>{
        internal static readonly NativeRecordQueryContract Value=new(typeof(TArguments),typeof(TResult),NativeRecordCodec<TArguments>.ContractId,NativeRecordCodec<TResult>.ContractId,
            static(payload,budget,token)=>NativeRecordCodec<TArguments>.Validate(payload,budget,token),static(payload,budget,token)=>NativeRecordCodec<TResult>.Validate(payload,budget,token));
    }
    private static class BoundCache<TArguments,TResult,TContract>{
        internal static readonly NativeRecordQueryContract Value=Bind(Cache<TArguments,TResult>.Value,typeof(TContract));
    }
    private static NativeRecordQueryContract Bind(NativeRecordQueryContract values,Type contract){
        if(!contract.IsInterface||contract.ContainsGenericParameters){throw new ArgumentException("A native public contract must be a closed interface.",nameof(contract));}
        // 同名・同じ表示版でも別ビルドを区別する。生成先のDLLはクライアントとサーバーで異なるため含めない。
        var identity="seamless-native-public-contract-v1|"+contract.Module.ModuleVersionId.ToString("N")+"/"+contract.MetadataToken.ToString("X8",CultureInfo.InvariantCulture)+"|"+contract.AssemblyQualifiedName
            +"|"+typeof(NativeRecordQueryContract).Module.ModuleVersionId.ToString("N")
            +"|"+typeof(RecordValueStrategy<>).Module.ModuleVersionId.ToString("N")
            +"|"+typeof(global::Seamless.Collections.ScalarNormalization).Module.ModuleVersionId.ToString("N");
        return new(values.ArgumentsType,values.ResultType,Hash(identity+"|arguments|"+values.ArgumentsContract),Hash(identity+"|result|"+values.ResultContract),values.arguments,values.result);
    }
    private static string Hash(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public NativeQueryDefinition Define(string id,string database,string schemaFingerprint,bool isReadOnly)=>new(id,database,schemaFingerprint,this.ArgumentsType.AssemblyQualifiedName!,this.ResultType.AssemblyQualifiedName!,isReadOnly){ArgumentsContract=this.ArgumentsContract,ResultContract=this.ResultContract};
    public void ValidateDefinition(NativeQueryDefinition definition){
        if(definition.ArgumentsContract!=this.ArgumentsContract||definition.ResultContract!=this.ResultContract){throw new InvalidDataException("The native record module contract differs from its definition.");}
    }
    public void ValidateArguments(ReadOnlySpan<byte> payload,ResourceBudget? budget=null,CancellationToken cancellationToken=default)=>this.arguments(payload,budget,cancellationToken);
    public void ValidateResult(ReadOnlySpan<byte> payload,ResourceBudget? budget=null,CancellationToken cancellationToken=default)=>this.result(payload,budget,cancellationToken);
}
