using System.Reflection;
using Seamless.Database.Rdbms;
using Seamless.Database.Query;

namespace Seamless.Database.Gateway.Hosting;

public sealed partial class InProcessDatabaseAdapter{
    internal void ConfigureGatewayChanges(GatewaySchemaCatalog schema){
        foreach(var owner in schema.Definition.Schemas){foreach(var table in owner.Tables){var binding=this.tables[new TableMetadata(owner.Name,table.Name,[]).IdentifierKey];var typed=binding.GetTable(this.database);var types=typed.GetType().GetGenericArguments();typeof(InProcessDatabaseAdapter).GetMethod(nameof(RegisterChanges),BindingFlags.Static|BindingFlags.NonPublic)!.MakeGenericMethod(types[0],types[1]).Invoke(null,[this,typed,binding,table]);}}
    }
    private static void RegisterChanges<TKey,TRow>(InProcessDatabaseAdapter adapter,object source,TableBinding binding,TableCatalog definition) where TKey:struct where TRow:struct{
        var identities=definition.Columns.Where(static column=>column.IsIdentity).ToArray();var columns=definition.Columns.Where(static column=>column.IsComputedPersisted).ToArray();var versions=definition.Columns.Where(static column=>column.Type.SqlTypeName is "rowversion" or "timestamp").ToArray();
        TRow Normalize(TRow row){
            if(versions.Length!=0){var session=DatabaseRegistry.CurrentSqlSession??throw new InvalidOperationException("A rowversion needs its owning connection.");var manager=adapter.GatewayTransactions??throw new InvalidOperationException("The rowversion has no Gateway owner.");var values=versions.ToDictionary(column=>column.Name,column=>(object?)manager.NextRowVersion(session,adapter));row=(TRow)binding.UpdateRecord(row,binding.MapValues(values));}
            if(columns.Length==0){return row;}var computed=columns.ToDictionary(column=>column.Name,column=>binding.ColumnGetters[column.Ordinal](row));return(TRow)binding.UpdateRecord(row,binding.MapValues(computed));
        }
        void Changed(TRow? before,TRow? after){
            var session=DatabaseRegistry.CurrentSqlSession??throw new InvalidOperationException("A Gateway mutation needs its owning connection context.");var manager=adapter.GatewayTransactions??throw new InvalidOperationException("The table has no Gateway transaction owner.");using var clock=session.BeginStatement();session.ConstraintStatementDepth++;try{
                if(after is{} inserted){foreach(var identity in identities){var value=binding.ColumnGetters[identity.Ordinal](inserted);if(before is{} old){if(!Equals(value,binding.ColumnGetters[identity.Ordinal](old))){throw new SqlQueryException("An identity column cannot be updated.",8102);}}else{manager.RecordInsertedIdentity(session,adapter,binding.Metadata,identity,value!);}}}
                if(before is{} previous){manager.ApplyReferentialActions(session,adapter,binding.Metadata,previous,after is{} next?next:null);}
            }finally{session.ConstraintStatementDepth--;}
        }
        adapter.database.ConfigureGatewayTableChanges((Table<TKey,TRow>)source,columns.Length!=0||versions.Length!=0?Normalize:null,Changed);
    }
}
