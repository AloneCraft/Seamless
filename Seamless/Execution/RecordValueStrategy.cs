using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace Seamless.Execution;

/// <summary>既定の値規則と参照構造を使うレコード全体の処理。</summary>
public static class RecordValueStrategy<T>{
    public static T Normalize(T value,ResourceBudget? budget=null,CancellationToken cancellationToken=default){
        cancellationToken.ThrowIfCancellationRequested();
        if(RecordScalarValue<T>.IsScalar){return RecordScalarValue<T>.Normalize(value);}
        if(RecordDirectStrategy<T>.Value is{} direct){return direct.Normalize(value);}
        using var context=new RecordValueContext(budget,cancellationToken);
        return context.Normalize(value);
    }

    public static bool Equals(T first,T second,ResourceBudget? budget=null,CancellationToken cancellationToken=default){
        cancellationToken.ThrowIfCancellationRequested();
        if(RecordScalarValue<T>.IsScalar){return RecordScalarValue<T>.Equal(first,second);}
        if(RecordDirectStrategy<T>.Value is{} direct){return direct.Equal(first,second);}
        using var context=new RecordValueContext(budget,cancellationToken);
        return context.Equal(first,second);
    }

    public static int GetHashCode(T value,ResourceBudget? budget=null,CancellationToken cancellationToken=default){
        cancellationToken.ThrowIfCancellationRequested();
        if(RecordScalarValue<T>.IsScalar){return RecordScalarValue<T>.Hash(value);}
        if(RecordDirectStrategy<T>.Value is{} direct){return direct.Hash(value);}
        using var context=new RecordValueContext(budget,cancellationToken);
        return context.Hash(value);
    }
}

/// <summary>一回の全体処理の参照対応とワーク予約を所有する。結果のコピーは呼び出し元が所有する。</summary>
internal sealed class RecordValueContext:IDisposable{
    internal static readonly ResourceBudget DefaultBudget=new(64*1024*1024,0);
    private readonly ResourceReservation memory;
    private readonly CancellationToken cancellationToken;
    private readonly Dictionary<object,object> normalized;
    private readonly Dictionary<object,object> compared;
    private readonly HashSet<object> comparedRight;
    private readonly Dictionary<object,int> hashed;

    internal RecordValueContext(ResourceBudget? budget,CancellationToken cancellationToken){
        this.cancellationToken=cancellationToken;
        this.memory=(budget??DefaultBudget).ReserveMemory(1024);
        try{
            this.normalized=new(ReferenceEqualityComparer.Instance);
            this.compared=new(ReferenceEqualityComparer.Instance);
            this.comparedRight=new(ReferenceEqualityComparer.Instance);
            this.hashed=new(ReferenceEqualityComparer.Instance);
        }catch{this.memory.Dispose();throw;}
    }

    internal void Check(){
        this.cancellationToken.ThrowIfCancellationRequested();
    }

    private void Reserve(long bytes)=>this.memory.Resize(checked(this.memory.Bytes+bytes));

    public T Normalize<T>(T value){
        this.Check();
        if(RecordScalarValue<T>.IsScalar){return RecordScalarValue<T>.Normalize(value);}
        if(RecordDirectStrategy<T>.Value is{} direct){return direct.Normalize(value);}
        RuntimeHelpers.EnsureSufficientExecutionStack();
        if(RecordScalarValue<T>.IsNull(value)){return value;}
        var reference=!typeof(T).IsValueType;
        if(!reference){this.Reserve(32L+Unsafe.SizeOf<T>());}
        object source=value!;
        if(source is string){return value;}
        if(reference&&this.normalized.TryGetValue(source,out var previous)){return(T)previous;}
        var policy=RecordTypePolicy.Get(source.GetType());
        if(reference){this.Reserve(checked(policy.EstimatedBytes(source)+256));}
        if(policy.IsScalar){
            var scalar=policy.NormalizeScalar!(source);
            if(reference){this.normalized.Add(source,scalar);}
            return(T)scalar;
        }
        var target=reference?RecordTypePolicy.Clone(source):source;
        if(reference){this.normalized.Add(source,target);}
        policy.NormalizeFields!(target,this);
        return(T)target;
    }

    public bool Equal<T>(T first,T second){
        this.Check();
        if(RecordScalarValue<T>.IsScalar){return RecordScalarValue<T>.Equal(first,second);}
        if(RecordDirectStrategy<T>.Value is{} direct){return direct.Equal(first,second);}
        RuntimeHelpers.EnsureSufficientExecutionStack();
        var firstNull=RecordScalarValue<T>.IsNull(first);
        var secondNull=RecordScalarValue<T>.IsNull(second);
        if(firstNull||secondNull){return firstNull&&secondNull;}
        var reference=!typeof(T).IsValueType;
        if(!reference){this.Reserve(2L*(32L+Unsafe.SizeOf<T>()));}
        object left=first!;
        object right=second!;
        if(left.GetType()!=right.GetType()){return false;}
        if(left is string text){return string.Equals(text,(string)right,StringComparison.Ordinal);}
        if(reference){
            if(this.compared.TryGetValue(left,out var previous)){return ReferenceEquals(previous,right);}
            if(this.comparedRight.Contains(right)){return false;}
            this.Reserve(256);
            this.compared.Add(left,right);
            this.comparedRight.Add(right);
        }
        var policy=RecordTypePolicy.Get(left.GetType());
        if(policy.IsScalar){return policy.EqualScalars!(left,right);}
        return policy.EqualFields!(left,right,this);
    }

    public int Hash<T>(T value){
        this.Check();
        if(RecordScalarValue<T>.IsScalar){return RecordScalarValue<T>.Hash(value);}
        if(RecordDirectStrategy<T>.Value is{} direct){return direct.Hash(value);}
        RuntimeHelpers.EnsureSufficientExecutionStack();
        if(RecordScalarValue<T>.IsNull(value)){return 0;}
        if(typeof(T).IsValueType){this.Reserve(32L+Unsafe.SizeOf<T>());}
        object source=value!;
        if(source is string text){return RecordHash.Text(text);}
        var policy=RecordTypePolicy.Get(source.GetType());
        var hash=RecordHash.Add(RecordHash.Seed,policy.TypeHash);
        if(!typeof(T).IsValueType){
            if(this.hashed.TryGetValue(source,out var previous)){return RecordHash.Add(hash,previous);}
            this.Reserve(256);
            this.hashed.Add(source,this.hashed.Count+1);
            hash=RecordHash.Add(hash,-1);
        }
        var fields=policy.IsScalar?policy.HashScalar!(source):policy.HashFields!(source,this);
        return RecordHash.Add(hash,fields);
    }

    public void Dispose()=>this.memory.Dispose();
}
