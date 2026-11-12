using System.Collections.Immutable;
using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;
using Seamless.Database.Rdbms;

namespace Database.Verification;

public sealed record TableDataVerification(string Schema,string Table,RowComparison Native,RowComparison Reconstructed);
public sealed record DataVerificationReport(string SourceFingerprint,ImmutableArray<TableDataVerification>Tables){
    public bool Equal=>this.Tables.All(t=>t.Native.Equal&&t.Reconstructed.Equal);
}

/// <summary>一致検証専用。定義変換やDB作成を内部で実行しない。</summary>
public sealed class DatabaseConsistencyVerifier{
    public async Task<DataVerificationReport>VerifyDataAsync(DbConnection source,SqlVendor sourceVendor,DbConnection reconstructed,SqlVendor targetVendor,NativeDatabaseDefinition definition,Func<NativeTable,IAsyncEnumerable<object?[]>>nativeRows,string temporaryRoot,CancellationToken cancellationToken=default){
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(reconstructed);ArgumentNullException.ThrowIfNull(definition);ArgumentNullException.ThrowIfNull(nativeRows);
        if(ReferenceEquals(source,reconstructed)){throw new ArgumentException("Source and reconstructed connections must be different.");}
        await using var sourceTransaction=await source.BeginTransactionAsync(IsolationLevel.Serializable,cancellationToken).ConfigureAwait(false);
        await using var targetTransaction=await reconstructed.BeginTransactionAsync(IsolationLevel.Serializable,cancellationToken).ConfigureAwait(false);
        var results=ImmutableArray.CreateBuilder<TableDataVerification>();
        var comparer=new RowBagVerifier();
        foreach(var table in definition.Tables){
            var sourceSql=TableSelect(sourceVendor,table);var targetSql=TableSelect(targetVendor,table);
            var native=await comparer.CompareAsync(ReadAsync(source,sourceSql,sourceTransaction,cancellationToken:cancellationToken),nativeRows(table),temporaryRoot,cancellationToken:cancellationToken).ConfigureAwait(false);
            var target=await comparer.CompareAsync(ReadAsync(source,sourceSql,sourceTransaction,cancellationToken:cancellationToken),ReadAsync(reconstructed,targetSql,targetTransaction,cancellationToken:cancellationToken),temporaryRoot,cancellationToken:cancellationToken).ConfigureAwait(false);
            results.Add(new(table.Schema,table.Name,native,target));
        }
        await sourceTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);await targetTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new(definition.SourceFingerprint,results.ToImmutable());
    }

    public async Task<RowComparison>VerifyQueryAsync(DbConnection reference,string referenceSql,DbConnection actual,string actualSql,IReadOnlyDictionary<string,object?>parameters,string temporaryRoot,bool ordered=false,CancellationToken cancellationToken=default){
        var expected=ReadAsync(reference,referenceSql,parameters:parameters,cancellationToken:cancellationToken);var observed=ReadAsync(actual,actualSql,parameters:parameters,cancellationToken:cancellationToken);
        var verifier=new RowBagVerifier();
        if(ordered){return await verifier.CompareOrderedAsync(expected,observed,cancellationToken:cancellationToken).ConfigureAwait(false);}
        return await verifier.CompareAsync(expected,observed,temporaryRoot,cancellationToken:cancellationToken).ConfigureAwait(false);
    }

    public static string TableSelect(SqlVendor vendor,NativeTable table){
        string Quote(string name){if(vendor==SqlVendor.SqlServer){return "["+name.Replace("]","]]",StringComparison.Ordinal)+"]";}return "`"+name.Replace("`","``",StringComparison.Ordinal)+"`";}
        var relation=Quote(table.Name);
        if(vendor==SqlVendor.SqlServer){relation=Quote(table.Schema)+"."+relation;}else if(table.Schema!="dbo"){relation=Quote(table.Schema+"__"+table.Name);}
        return "SELECT "+string.Join(",",table.Columns.Select(c=>Quote(c.Name)))+" FROM "+relation;
    }

    public static async IAsyncEnumerable<object?[]>ReadAsync(DbConnection connection,string sql,DbTransaction?transaction=null,IReadOnlyDictionary<string,object?>?parameters=null,[EnumeratorCancellation]CancellationToken cancellationToken=default){
        await using var command=connection.CreateCommand();command.CommandText=sql;command.Transaction=transaction;
        foreach(var pair in parameters??ImmutableDictionary<string,object?>.Empty){var parameter=command.CreateParameter();parameter.ParameterName="@"+pair.Key.TrimStart('@');parameter.Value=pair.Value??DBNull.Value;command.Parameters.Add(parameter);}
        await using var reader=await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess,cancellationToken).ConfigureAwait(false);
        while(await reader.ReadAsync(cancellationToken).ConfigureAwait(false)){
            var row=new object?[reader.FieldCount];for(var index=0;index<row.Length;index++){if(!await reader.IsDBNullAsync(index,cancellationToken).ConfigureAwait(false)){row[index]=reader.GetValue(index);}}
            yield return row;
        }
        if(await reader.NextResultAsync(cancellationToken).ConfigureAwait(false)){throw new NotSupportedException("Multiple result sets require result-set-aware verification; they are not flattened into one result.");}
    }
}
