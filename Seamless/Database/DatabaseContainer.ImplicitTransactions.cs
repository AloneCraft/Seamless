using Seamless.Collections.Tsavorite;
using Tsavorite.core;
using Seamless.Transactions;
using Seamless.Transactions.Collections;
namespace Seamless.Database;

/// <summary>
/// db 側の表(保存された表)への書き込み(Add・Remove・添字の set・関係代入)を、取引の外では1回ごとに1つの暗黙の取引で確定する。
/// 2026-11-17 利用者決定(Document/refactoring-designs/20261117-関係代入.md)。
/// 取引(tx)を開いている流れで db 側の表に触ると例外にする。どの取引の状態を読み書きしているかが曖昧にならないようにするため。
/// </summary>
public abstract partial class DatabaseContainer{
    private readonly global::System.Threading.AsyncLocal<TransactionScope?> implicitScope=new();

    // 従来の取引(DatabaseTransaction)・取込中の直接書き込み・Gateway は今までの経路のまま。
    private bool UsesImplicitTransaction=>this.Options.GatewayProfile is null&&this.ambientTransaction.Value is null&&this.ambientWriteDepth.Value==0;

    internal bool TryWriteImplicitly<TKey,TRecord,TResult>(StoredTable<TKey,TRecord> table,Func<TransactionCollection<TKey,TRecord>,TResult> write,out TResult result)
        where TKey:struct where TRecord:struct{
        if(!this.UsesImplicitTransaction){result=default!;return false;}
        result=this.RunImplicitTransaction(scope=>write(this.GetTransactionCollection(scope,table)));
        return true;
    }

    internal bool TryWriteImplicitly<TRecord,TResult>(StoredTable<TRecord> table,Func<TransactionCollection<TRecord,TRecord>,TResult> write,out TResult result)
        where TRecord:notnull{
        if(!this.UsesImplicitTransaction){result=default!;return false;}
        result=this.RunImplicitTransaction(scope=>write(this.GetTransactionCollection(scope,table)));
        return true;
    }

    /// <summary>暗黙の取引の中なら、その取引の表を返す。関係代入の右辺が db 側の表を読むときに使う。</summary>
    internal bool TryGetImplicitCollection<TKey,TRecord>(StoredTable<TKey,TRecord> table,out TransactionCollection<TKey,TRecord> collection)
        where TKey:struct where TRecord:struct{
        if(this.implicitScope.Value is{} scope){collection=this.GetTransactionCollection(scope,table);return true;}
        this.ThrowIfExplicitTransactionOpen();
        collection=null!;return false;
    }

    internal bool TryGetImplicitCollection<TRecord>(StoredTable<TRecord> table,out TransactionCollection<TRecord,TRecord> collection) where TRecord:notnull{
        if(this.implicitScope.Value is{} scope){collection=this.GetTransactionCollection(scope,table);return true;}
        this.ThrowIfExplicitTransactionOpen();
        collection=null!;return false;
    }

    /// <summary>読んでから書く操作(Update・Merge・Replace・MergeWith など)を、取引の外なら1つの暗黙の取引で行う。</summary>
    internal TResult InImplicitTransaction<TResult>(Func<TResult> body){
        if(!this.UsesImplicitTransaction){return body();}
        return this.RunImplicitTransaction(_=>body());
    }

    private void ThrowIfExplicitTransactionOpen(){
        if(this.Options.GatewayProfile is null&&this.ambientTransaction.Value is null&&this.sharedCoordinator?.HasCurrentTransaction==true){
            throw new InvalidOperationException("A transaction is open on this flow. Use the transaction's tables (tx.…) instead of the database's tables (db.…).");
        }
    }

    private TResult RunImplicitTransaction<TResult>(Func<TransactionScope,TResult> body){
        if(this.implicitScope.Value is{} current){return body(current);}
        this.EnsureAvailable();
        this.ThrowIfExplicitTransactionOpen();
        var scope=this.BeginTransactionScopeAsync().AsTask().GetAwaiter().GetResult();
        this.implicitScope.Value=scope;
        try{
            var result=body(scope);
            scope.CommitAsync().AsTask().GetAwaiter().GetResult();
            return result;
        }finally{
            this.implicitScope.Value=null;
            scope.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}
