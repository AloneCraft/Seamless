using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Query;

namespace Seamless.Database.Gateway.Sql;

internal static partial class GatewayDdlCompiler{
    private static GatewaySchemaCatalog ResolveComputed(GatewaySchemaCatalog original,GatewaySchemaCatalog target){
        foreach(var owner in original.Definition.Schemas){foreach(var table in owner.Tables){var next=target.Definition.Schemas.FirstOrDefault(schema=>EqualName(schema.Name,owner.Name))?.Tables.FirstOrDefault(item=>EqualName(item.Name,table.Name));if(next is null){continue;}foreach(var column in table.Columns.Where(static column=>column.ComputedDefinition is not null)){if(!next.Columns.Any(item=>EqualName(item.Name,column.Name))){continue;}var expression=GatewayComputedExpressions.Rewrite(column.ComputedDefinition!,table);foreach(var input in expression.Inputs){var changed=next.Columns.FirstOrDefault(item=>EqualName(item.Name,input.Name));if(changed is null||changed.Type!=input.Type){throw new SqlQueryException("A computed column depends on the altered column.",5074);}}}}}
        var catalog=new GatewayExpressionCatalog(target.Definition);var queries=new SqlQueryEngine(catalog,0);var schemas=target.Definition.Schemas.Select(owner=>new Seamless.Database.Rdbms.SchemaCatalog(owner.Name,owner.Tables.Select(table=>{
            var columns=table.Columns.Select(column=>{if(column.ComputedDefinition is not{} expression){return column;}var rewritten=GatewayComputedExpressions.Rewrite(expression,table);var result=queries.Prepare(rewritten.Sql,target.Definition.Name,GatewayComputedExpressions.Parameters(rewritten.Inputs)).Columns.Single();var required=!column.Type.IsNullable||table.PrimaryKey?.Columns.Any(key=>EqualName(key.Name,column.Name))==true;if(column.IsComputedPersisted||table.PrimaryKey?.Columns.Any(key=>EqualName(key.Name,column.Name))==true||table.UniqueKeys.Any(key=>key.Columns.Any(item=>EqualName(item.Name,column.Name)))){GatewayComputedExpressions.ValidatePersisted(expression,target.Definition);}return column with{Type=GatewayComputedExpressions.Infer(result.Type,!required&&result.IsNullable)};}).ToArray();return WithColumns(table,columns);
        }).ToArray(),owner.Views,owner.Routines)).ToArray();return target with{Definition=new(target.Definition.Name,schemas,target.Definition.DefaultCollation)};
    }
}
