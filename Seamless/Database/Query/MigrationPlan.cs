using System.Collections.ObjectModel;

namespace Seamless.Database.Query;

public sealed record MigrationColumn(long Id,string MemberName,SqlType Type,bool IsNullable){public bool IsComputed{get;init;}}

/// <summary>永続列 ID による移行の対応表。CLR の member 名や列順を列の同一性にしない。</summary>
public sealed class MigrationPlan{
    public MigrationPlan(long tableId,IReadOnlyList<MigrationColumn> sourceColumns,IReadOnlyList<MigrationColumn> targetColumns){
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tableId);ArgumentNullException.ThrowIfNull(sourceColumns);ArgumentNullException.ThrowIfNull(targetColumns);
        this.TableId=tableId;this.SourceColumns=Validate(sourceColumns);this.TargetColumns=Validate(targetColumns);
    }
    public long TableId{get;}
    public ReadOnlyCollection<MigrationColumn> SourceColumns{get;}
    public ReadOnlyCollection<MigrationColumn> TargetColumns{get;}
    private static ReadOnlyCollection<MigrationColumn> Validate(IReadOnlyList<MigrationColumn> columns){
        var ids=new HashSet<long>();var names=new HashSet<string>(StringComparer.Ordinal);
        foreach(var column in columns){ArgumentNullException.ThrowIfNull(column);ArgumentOutOfRangeException.ThrowIfNegativeOrZero(column.Id);ArgumentException.ThrowIfNullOrWhiteSpace(column.MemberName);ArgumentNullException.ThrowIfNull(column.Type);if(!ids.Add(column.Id)||!names.Add(column.MemberName)){throw new ArgumentException("Migration columns must have unique IDs and member names.",nameof(columns));}}
        return Array.AsReadOnly(columns.ToArray());
    }
    internal TTarget ConvertColumn<TSource,TTarget>(TSource source,int ordinal)=>this.ConvertValue<TTarget>(source,this.TargetColumns[ordinal]);
    internal TTarget AddedColumn<TTarget>(int ordinal,MigrationContext context){
        var column=this.TargetColumns[ordinal];if(column.IsComputed){return default!;}return this.ConvertValue<TTarget>(context.EvaluateDefault(column.Id),column);
    }
    private TTarget ConvertValue<TTarget>(object? value,MigrationColumn column){
        if(value is null||value is DBNull){
            if(!column.IsNullable){throw new SqlQueryException("Cannot insert NULL into column '"+column.MemberName+"' during table migration.",515);}
            return default!;
        }
        try{
            // ALTER の文字・binary 縮小で黙って切り捨てない。数値の丸めは SQL の変換経路へ渡す。
            var explicitConversion=column.Type.ClrType!=typeof(string)&&column.Type.ClrType!=typeof(byte[]);
            var converted=SqlValues.ConvertTo(value,column.Type,explicitConversion);
            return(TTarget)converted!;
        }catch(OverflowException error){throw new SqlQueryException("Arithmetic overflow while migrating column '"+column.MemberName+"': "+error.Message,8115);}
        catch(FormatException error){throw new SqlQueryException("Conversion failed while migrating column '"+column.MemberName+"': "+error.Message,245);}
    }
}

public sealed class MigrationContext{
    private readonly Func<long,object?>? evaluateDefault;
    public MigrationContext(Func<long,object?>? evaluateDefault=null,CancellationToken cancellationToken=default){this.evaluateDefault=evaluateDefault;this.CancellationToken=cancellationToken;}
    public CancellationToken CancellationToken{get;}
    public object? EvaluateDefault(long columnId){this.ThrowIfCancellationRequested();return this.evaluateDefault?.Invoke(columnId);}
    public void ThrowIfCancellationRequested()=>this.CancellationToken.ThrowIfCancellationRequested();
}
