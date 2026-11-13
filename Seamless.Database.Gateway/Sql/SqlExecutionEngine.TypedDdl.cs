using Microsoft.SqlServer.TransactSql.ScriptDom;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Query;

namespace Seamless.Database.Gateway.Sql;

public sealed partial class SqlExecutionEngine{
    private QueryResult ExecuteTypedDdl(TSqlStatement statement,string sql,SessionContext session){
        using var statementClock=session.BeginStatement();
        var manager=this.registry.GatewayTransactions!;var targets=GatewayDdlCompiler.Databases(statement,session.DatabaseName).Select(this.registry.Resolve).ToArray();foreach(var target in targets){EnsureWritable(target);if(target.Adapter is not InProcessDatabaseAdapter){throw new SqlQueryException("Typed DDL requires an in-process Gateway database.",40515);}}
        var implicitTransaction=!session.IsInTransaction;var executing=false;if(implicitTransaction){manager.Begin(session);}var savepoint=implicitTransaction?null:"ddl/"+Guid.NewGuid().ToString("N");if(savepoint is not null){session.CreateSavepoint(savepoint);}
        try{
            manager.AcquireSchema(session,targets);
            foreach(var target in targets){
                var before=manager.DdlBaseSchema(session,target.Name);var plan=GatewayDdlCompiler.Apply(before,statement,sql,target.Name,session.DatabaseName,this.registry.SchemaResolution);manager.ReserveDdlIdentifiers(session,target.Name,before,plan.Schema);executing=true;
                if(statement is AlterTableAddTableElementStatement add){var table=add.SchemaObjectName.BaseIdentifier.Value;var schema=GatewayDdlCompiler.SchemaOf(add.SchemaObjectName,this.registry.SchemaResolution,before.Definition.Schemas,owner=>owner.Tables.Any(item=>item.Name.Equals(table,StringComparison.OrdinalIgnoreCase)));var original=before.Definition.Schemas.Single(owner=>owner.Name.Equals(schema,StringComparison.OrdinalIgnoreCase)).Tables.Single(item=>item.Name.Equals(table,StringComparison.OrdinalIgnoreCase));var changed=plan.Schema.Definition.Schemas.Single(owner=>owner.Name.Equals(schema,StringComparison.OrdinalIgnoreCase)).Tables.Single(item=>item.Name.Equals(table,StringComparison.OrdinalIgnoreCase));if(changed.Columns.Any(column=>!original.Columns.Any(old=>old.Name.Equals(column.Name,StringComparison.OrdinalIgnoreCase))&&!column.Type.IsNullable&&!column.IsIdentity&&column.ComputedDefinition is null&&column.DefaultDefinition is null&&column.Type.SqlTypeName is not("timestamp" or "rowversion"))&&this.queries.Prepare("SELECT TOP(1) 1 FROM ["+schema.Replace("]","]]",StringComparison.Ordinal)+"].["+table.Replace("]","]]",StringComparison.Ordinal)+"]",target.Name).Execute(options:manager.QueryExecutionOptions(session.CancellationToken)).Rows.Any()){throw new SqlQueryException("A nonempty table requires a DEFAULT, IDENTITY, or timestamp for a new NOT NULL column.",4901);}}
                object? Default(long columnId){var owner=plan.Schema.Tables.SingleOrDefault(table=>table.Columns.Any(column=>column.Id==columnId));if(owner is not null){var column=owner.Columns.Single(column=>column.Id==columnId);var definition=plan.Schema.Definition.Schemas.Single(schema=>schema.Name==owner.Schema).Tables.Single(table=>table.Name==owner.Name).Columns.Single(field=>field.Name==column.Name);if(definition.IsIdentity){return manager.NextIdentity(session,target.Name,owner,column,definition);}if(definition.Type.SqlTypeName is "rowversion" or "timestamp"){return manager.NextRowVersion(session,target.Name);}}if(!plan.FillDefaults.TryGetValue(columnId,out var expression)||expression is null){return null;}return this.queries.Prepare("SELECT "+expression,target.Name).Execute(options:manager.QueryExecutionOptions(session.CancellationToken,session.StatementTimestamp)).Rows.Single()[0];}
                manager.StageSchema(session,target.Name,plan.Schema,new(Default,session.CancellationToken));
                if(statement is FunctionStatementBody function){var generator=new Sql160ScriptGenerator();var arguments=function.Parameters.Select(parameter=>{generator.GenerateScript(parameter.DataType,out var type);return "CAST(NULL AS "+type+")";});this.queries.Prepare("SELECT ["+GatewayDdlCompiler.SchemaOf(function.Name,this.registry.SchemaResolution,plan.Schema.Definition.Schemas,owner=>owner.Routines.Any(routine=>routine.Name.Equals(function.Name.BaseIdentifier.Value,StringComparison.OrdinalIgnoreCase))).Replace("]","]]",StringComparison.Ordinal)+"].["+function.Name.BaseIdentifier.Value.Replace("]","]]",StringComparison.Ordinal)+"]("+string.Join(',',arguments)+")",target.Name);}
            }
            if(implicitTransaction){manager.Commit(session);}return QueryResult.NonQuery(0);
        }catch(Exception error){
            if(implicitTransaction&&session.IsInTransaction){manager.Rollback(session);}
            else if(session.IsInTransaction){
                if(savepoint is not null&&session.TransactionState>0){try{session.RollbackToSavepoint(savepoint);}catch{session.MarkRollbackOnly();throw;}}
                var aborting=error is SqlQueryException{Number:220 or 515 or 547 or 3729 or 4901 or 4902 or 4922 or 4924 or 5074 or 8115 or 8152 or 2628};
                var xactAbort=session.XactAbort&&(executing||aborting||error is OperationCanceledException||error is SqlQueryException{Number:1222});
                if(session.TryCatchDepth>0){if(xactAbort||aborting&&error is not SqlQueryException{Number:3729}){session.MarkRollbackOnly();}}
                else if(aborting||xactAbort){manager.Rollback(session);session.LastTransactionOperation=10;}
            }
            if(session.TryCatchDepth>0&&error is SqlQueryException{Number:5074} dependency){throw new SqlQueryException(dependency.Message,4922,dependency.Line);}
            if(error is SqlQueryException{Number:8115} overflow&&statement is AlterTableAlterColumnStatement{DataType:SqlDataTypeReference{SqlDataTypeOption:SqlDataTypeOption.TinyInt}}){throw new SqlQueryException(overflow.Message,220,overflow.Line);}
            throw;
        }finally{if(savepoint is not null&&session.IsInTransaction){try{session.ReleaseSavepoint(savepoint);}catch{session.MarkRollbackOnly();throw;}}}
    }
}
