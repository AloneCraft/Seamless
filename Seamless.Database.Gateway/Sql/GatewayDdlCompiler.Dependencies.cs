using Microsoft.SqlServer.TransactSql.ScriptDom;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Query;
using Seamless.Database.Rdbms;

namespace Seamless.Database.Gateway.Sql;

internal static partial class GatewayDdlCompiler{
    private static bool ConstraintReferencesColumn(string expression,string column){
        var parsed=new TSql160Parser(true).Parse(new StringReader("SELECT 1 WHERE "+expression),out var errors);if(errors.Count!=0){throw new SqlQueryException(errors[0].Message,102);}var visitor=new SchemaBindingReferences();parsed.Accept(visitor);return visitor.Columns.Any(reference=>reference.MultiPartIdentifier.Identifiers.Last().Value.Equals(column,StringComparison.OrdinalIgnoreCase));
    }
    private static void ValidateSchemaBinding(GatewaySchemaCatalog original,GatewaySchemaCatalog target){
        ValidateBoundFunctionDependencies(original,target);
        foreach(var definition in BoundDefinitions(target)){
            var script=new TSql160Parser(true).Parse(new StringReader(definition),out var errors);if(errors.Count!=0){throw new SqlQueryException(errors[0].Message,102);}var references=new SchemaBindingReferences();script.Accept(references);
            var tables=new List<(NamedTableReference Reference,TableCatalog Table,TableCatalog? Previous)>();
            foreach(var reference in references.Tables){var name=reference.SchemaObject;if(name.SchemaIdentifier is null||name.DatabaseIdentifier is not null||name.ServerIdentifier is not null){throw new SqlQueryException("Schema-bound references require two-part names in the same database.",4512);}TableCatalog? Find(GatewaySchemaCatalog schema)=>schema.Definition.Schemas.FirstOrDefault(owner=>EqualName(owner.Name,name.SchemaIdentifier.Value))?.Tables.FirstOrDefault(table=>EqualName(table.Name,name.BaseIdentifier.Value));var table=Find(target)??throw new SqlQueryException("A schema-bound function depends on the table.",3729);tables.Add((reference,table,Find(original)));}
            foreach(var column in references.Columns){var parts=column.MultiPartIdentifier.Identifiers.Select(static item=>item.Value).ToArray();if(parts.Length==0){continue;}var candidates=tables.Where(item=>parts.Length==1||EqualName(item.Reference.Alias?.Value??item.Table.Name,parts[^2])||EqualName(item.Table.Name,parts[^2])&&(parts.Length<3||EqualName(item.Reference.SchemaObject.SchemaIdentifier.Value,parts[^3]))).Where(item=>item.Table.Columns.Any(field=>EqualName(field.Name,parts[^1]))).ToArray();if(candidates.Length==0){throw new SqlQueryException("A schema-bound function depends on the column.",5074);}if(candidates.Length>1){throw new SqlQueryException("Ambiguous column in a schema-bound function.",209);}var selected=candidates[0];var field=selected.Table.Columns.Single(field=>EqualName(field.Name,parts[^1]));var previous=selected.Previous?.Columns.FirstOrDefault(field=>EqualName(field.Name,parts[^1]));if(previous is not null&&previous.Type!=field.Type){throw new SqlQueryException("A schema-bound function depends on the altered column.",5074);}}
            foreach(var function in references.Functions){if(function.CallTarget is not MultiPartIdentifierCallTarget call){continue;}var parts=call.MultiPartIdentifier.Identifiers;if(parts.Count!=1){throw new SqlQueryException("A schema-bound function requires a two-part function reference.",4512);}var dependency=target.Definition.Schemas.FirstOrDefault(owner=>EqualName(owner.Name,parts[0].Value))?.Routines.FirstOrDefault(item=>EqualName(item.Name,function.FunctionName.Value));if(dependency is null||!dependency.IsSchemaBound){throw new SqlQueryException("A referenced function is not schema-bound.",4513);}}
            if(references.HasStar){throw new SqlQueryException("A schema-bound function cannot contain SELECT *.",1054);}
        }
    }
    private static void ValidateBoundFunctionDependencies(GatewaySchemaCatalog original,GatewaySchemaCatalog target){
        foreach(var definition in BoundDefinitions(original)){
            var script=new TSql160Parser(true).Parse(new StringReader(definition),out _);var references=new SchemaBindingReferences();script.Accept(references);foreach(var function in references.Functions){if(function.CallTarget is not MultiPartIdentifierCallTarget call||call.MultiPartIdentifier.Identifiers.Count!=1){continue;}var schema=call.MultiPartIdentifier.Identifiers[0].Value;var before=original.Definition.Schemas.FirstOrDefault(owner=>EqualName(owner.Name,schema))?.Routines.FirstOrDefault(item=>EqualName(item.Name,function.FunctionName.Value));var after=target.Definition.Schemas.FirstOrDefault(owner=>EqualName(owner.Name,schema))?.Routines.FirstOrDefault(item=>EqualName(item.Name,function.FunctionName.Value));if(before is not null&&before!=after){throw new SqlQueryException("A schema-bound object depends on this function.",3729);}}
        }
    }
    private static IEnumerable<string> BoundDefinitions(GatewaySchemaCatalog schema){
        foreach(var routine in schema.Definition.Schemas.SelectMany(static owner=>owner.Routines).Where(static routine=>routine.IsSchemaBound)){yield return routine.Definition??throw new InvalidDataException("A schema-bound routine definition is missing.");}
        foreach(var view in schema.Definition.Schemas.SelectMany(static owner=>owner.Views)){
            if(view.Definition is not{} definition){throw new InvalidDataException("A persisted view definition is missing.");}var parsed=new TSql160Parser(true).Parse(new StringReader(definition),out var errors);if(errors.Count!=0){throw new SqlQueryException(errors[0].Message,102);}
            if(parsed is TSqlScript script&&script.Batches.SelectMany(static batch=>batch.Statements).OfType<ViewStatementBody>().Any(static body=>body.ViewOptions.Any(static option=>option.OptionKind==ViewOptionKind.SchemaBinding))){yield return definition;}
        }
    }
    private static bool EqualName(string first,string second)=>first.Equals(second,StringComparison.OrdinalIgnoreCase);
    private sealed class SchemaBindingReferences:TSqlFragmentVisitor{
        internal List<NamedTableReference> Tables{get;}=[];internal List<ColumnReferenceExpression> Columns{get;}=[];internal List<FunctionCall> Functions{get;}=[];internal bool HasStar{get;private set;}
        public override void ExplicitVisit(NamedTableReference node){this.Tables.Add(node);base.ExplicitVisit(node);}
        public override void ExplicitVisit(ColumnReferenceExpression node){if(node.MultiPartIdentifier is not null){this.Columns.Add(node);}base.ExplicitVisit(node);}
        public override void ExplicitVisit(FunctionCall node){this.Functions.Add(node);base.ExplicitVisit(node);}
        public override void ExplicitVisit(SelectStarExpression node){this.HasStar=true;base.ExplicitVisit(node);}
    }
}
