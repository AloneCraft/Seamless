using System.Reflection;
using Seamless.Collections;

namespace Seamless.Execution;

// 生成型のグラフを持たない処理を、共通入口からも同じ型固定ストラテジへ接続する。
internal sealed class RecordDirectStrategy<T>{
    internal static readonly RecordDirectStrategy<T>? Value=Create();
    internal readonly Func<T,T> Normalize;
    internal readonly Func<T,T,bool> Equal;
    internal readonly Func<T,int> Hash;

    private RecordDirectStrategy(Func<T,T> normalize,Func<T,T,bool> equal,Func<T,int> hash){
        this.Normalize=normalize;
        this.Equal=equal;
        this.Hash=hash;
    }

    private static RecordDirectStrategy<T>? Create(){
        var strategy=typeof(T).GetNestedType("Strategy",BindingFlags.Public);
        if(strategy is null||!typeof(T).IsValueType||!typeof(IRecordStrategy<T>).IsAssignableFrom(strategy)){return null;}
        return typeof(RecordDirectStrategy<T>).GetMethod(nameof(Bind),BindingFlags.Static|BindingFlags.NonPublic)!
            .MakeGenericMethod(strategy).CreateDelegate<Func<RecordDirectStrategy<T>?>>()();
    }

    private static RecordDirectStrategy<T>? Bind<TStrategy>() where TStrategy:IRecordStrategy<T>{
        if(!TStrategy.IsGraphFree){return null;}
        return new(static value=>TStrategy.Normalize(value),static (first,second)=>TStrategy.Equals(first,second),static value=>TStrategy.GetHashCode(value));
    }
}
