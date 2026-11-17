using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Seamless.Database.Gateway.Sql;

internal static class SqlServerCatalogProperties{
    internal static bool TryRewrite(SelectStatement select,string sql,out string result){
        result="";var visitor=new Properties();select.Accept(visitor);
        if(!visitor.HasCatalog||visitor.Calls.Count+visitor.Sysnames.Count==0){return false;}
        result=sql.Substring(select.StartOffset,select.FragmentLength);
        var changes=new List<(int Offset,int Length,string Text)>();
        foreach(var call in visitor.Calls){
            var property=(StringLiteral)call.Parameters[0];var literal=SystemCatalogEmulator.PropertySqlLiteral(property.Value);
            changes.Add((call.StartOffset,call.FragmentLength,literal));
        }
        foreach(var type in visitor.Sysnames){changes.Add((type.StartOffset,type.FragmentLength,"nvarchar(128)"));}
        foreach(var change in changes.OrderByDescending(c=>c.Offset)){result=result.Remove(change.Offset-select.StartOffset,change.Length).Insert(change.Offset-select.StartOffset,change.Text);}
        return true;
    }
    private sealed class Properties:TSqlFragmentVisitor{
        internal bool HasCatalog;internal readonly List<FunctionCall>Calls=[];
        internal readonly List<UserDataTypeReference>Sysnames=[];
        public override void ExplicitVisit(NamedTableReference table){if(table.SchemaObject.SchemaIdentifier?.Value.Equals("sys",StringComparison.OrdinalIgnoreCase)==true){this.HasCatalog=true;}}
        public override void ExplicitVisit(FunctionCall call){if(call.CallTarget is null&&call.FunctionName.Value.Equals("SERVERPROPERTY",StringComparison.OrdinalIgnoreCase)&&call.Parameters.Count==1&&call.Parameters[0] is StringLiteral){this.Calls.Add(call);}base.ExplicitVisit(call);}
        public override void ExplicitVisit(UserDataTypeReference type){if(type.Name.BaseIdentifier.Value.Equals("sysname",StringComparison.OrdinalIgnoreCase)&&(type.Name.Identifiers.Count==1||type.Name.Identifiers.Count==2&&type.Name.SchemaIdentifier.Value.Equals("sys",StringComparison.OrdinalIgnoreCase))){this.Sysnames.Add(type);}}
    }
}
