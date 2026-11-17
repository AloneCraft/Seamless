using System.Globalization;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using Seamless.Database.Gateway.Hosting;

namespace Seamless.Database.Gateway.Sql;

internal static class SqlTransactionExpressions{
    internal static bool TryRewrite(TSqlFragment fragment,string sql,SessionContext session,out string rewritten,Func<string,decimal?>? identityCurrent=null,IReadOnlyDictionary<string,object?>? values=null,Func<byte[]>? rowVersion=null){
        var visitor=new Visitor(session,identityCurrent,values,rowVersion);fragment.Accept(visitor);rewritten=sql;
        if(visitor.Changes.Count==0){return false;}
        foreach(var change in visitor.Changes.OrderByDescending(static change=>change.Offset)){rewritten=rewritten.Remove(change.Offset,change.Length).Insert(change.Offset,change.Value);}
        return true;
    }
    private sealed class Visitor(SessionContext session,Func<string,decimal?>? identityCurrent,IReadOnlyDictionary<string,object?>? values,Func<byte[]>? rowVersion):TSqlFragmentVisitor{
        internal List<(int Offset,int Length,string Value)> Changes{get;}=[];
        private static string Identity(decimal? value)=>"CAST("+(value?.ToString(CultureInfo.InvariantCulture)??"NULL")+" AS decimal(38,0))";
        public override void ExplicitVisit(GlobalVariableExpression node){
            if(node.Name.Equals("@@TRANCOUNT",StringComparison.OrdinalIgnoreCase)){this.Changes.Add((node.StartOffset,node.FragmentLength,session.TransactionCount.ToString(CultureInfo.InvariantCulture)));}
            if(node.Name.Equals("@@IDENTITY",StringComparison.OrdinalIgnoreCase)){this.Changes.Add((node.StartOffset,node.FragmentLength,Identity(session.LastIdentity)));}
            if(node.Name.Equals("@@DBTS",StringComparison.OrdinalIgnoreCase)&&rowVersion is not null){this.Changes.Add((node.StartOffset,node.FragmentLength,"CAST(0x"+Convert.ToHexString(rowVersion())+" AS varbinary(8))"));}
        }
        public override void ExplicitVisit(FunctionCall node){
            if(node.CallTarget is null&&node.Parameters.Count==0&&node.FunctionName.Value.Equals("XACT_STATE",StringComparison.OrdinalIgnoreCase)){this.Changes.Add((node.StartOffset,node.FragmentLength,"CAST("+session.TransactionState.ToString(CultureInfo.InvariantCulture)+" AS smallint)"));return;}
            if(node.CallTarget is null&&node.Parameters.Count==0&&node.FunctionName.Value.Equals("SCOPE_IDENTITY",StringComparison.OrdinalIgnoreCase)){this.Changes.Add((node.StartOffset,node.FragmentLength,Identity(session.CurrentIdentityScope.Value)));return;}
            if(node.CallTarget is null&&node.Parameters.Count==1&&node.FunctionName.Value.Equals("IDENT_CURRENT",StringComparison.OrdinalIgnoreCase)&&identityCurrent is not null){
                string? name;if(node.Parameters[0] is StringLiteral literal){name=literal.Value;}else if(node.Parameters[0] is NullLiteral){name=null;}else if(node.Parameters[0] is VariableReference variable&&values is not null&&values.TryGetValue(variable.Name,out var value)){name=Convert.ToString(value,CultureInfo.InvariantCulture);}else{base.ExplicitVisit(node);return;}
                decimal? current=null;if(name is not null){current=identityCurrent(name);}this.Changes.Add((node.StartOffset,node.FragmentLength,Identity(current)));return;
            }
            base.ExplicitVisit(node);
        }
    }
}
