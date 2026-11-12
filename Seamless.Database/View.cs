namespace Seamless.Database;

/// <summary>
/// コンテナの Table/View への通常 LINQ で定義する宣言ビュー。列挙のたびに定義を評価する。
/// 定義ラムダ内の LINQ はコンパイル時に FLinq が展開する。
/// </summary>
public sealed class View<TResult>:global::Seamless.Collections.ISet<TResult> {
    private readonly Func<IEnumerable<TResult>> definition;
    private readonly global::Seamless.Execution.ExecutionScope resources;
    private readonly DatabaseContainer? definitionOwner;
    private readonly global::System.Reflection.MethodInfo definitionMethod;
    internal View(Func<IEnumerable<TResult>> definition,string name,global::Seamless.Execution.ExecutionScope resources,DatabaseContainer? definitionOwner=null,global::System.Reflection.MethodInfo? definitionMethod=null) {
        this.definition=definition;
        this.Name=name;
        this.resources=resources;
        this.definitionOwner=definitionOwner;
        this.definitionMethod=definitionMethod??definition.Method;
    }
    /// <summary>宣言メンバー名。</summary>
    public string Name { get; }
    public int Count=>checked((int)this.GetLongCount());
    public long GetLongCount()=>global::System.Linq.Enumerable.LongCount(this);
    public bool Contains(TResult item)=>global::System.Linq.Enumerable.Contains(this,item,this.resources.Codecs.GetEqualityComparer<TResult>());
    public IEnumerator<TResult> GetEnumerator(){
        if(this.definitionOwner is null){return this.Enumerate().GetEnumerator();}
        return this.EnumerateDefinition(this.definition).GetEnumerator();
    }

    /// <summary>生成器が展開した宣言本体を、同じ所有者・集合・資源境界で列挙する。</summary>
    public IEnumerable<TResult> ExpandDefinition<TDatabase>(Func<TDatabase,IEnumerable<TResult>> definition) where TDatabase:DatabaseContainer{
        ArgumentNullException.ThrowIfNull(definition);
        if(this.definitionOwner is not TDatabase owner){return this;}
        return this.EnumerateDefinition(()=>definition(owner));
    }
    private IEnumerable<TResult> Enumerate(){
        using var scope=this.resources.CreateChild();
        foreach(var row in global::Seamless.Linq.ScopedQueryExtensions.Query(scope,this.definition()).Distinct()){
            yield return row;
        }
    }
    private IEnumerable<TResult> EnumerateDefinition(Func<IEnumerable<TResult>> definition){
        using var scope=this.resources.CreateChild();
        IEnumerator<TResult>? enumerator=null;
        try{
            while(true){
                TResult current;
                using(ViewDefinitionCalls.Enter(this.definitionOwner!,this.Name,this.definitionMethod)){
                    enumerator??=global::Seamless.Linq.ScopedQueryExtensions.Query(scope,definition()).Distinct().GetEnumerator();
                    if(!enumerator.MoveNext()){yield break;}
                    current=enumerator.Current;
                }
                yield return current;
            }
        }finally{
            enumerator?.Dispose();
        }
    }
    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
}
