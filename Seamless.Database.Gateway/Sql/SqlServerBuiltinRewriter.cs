using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Seamless.Database.Gateway.Sql;

// 共通エンジンが式として評価できない SQL Server 固有の組み込みを、文の中で値や式に置き換える。
// SERVERPROPERTY('名前') は値のリテラル、@@SERVICENAME などは定数、sysname は nvarchar(128) にする。
// ERROR_NUMBER() などは CATCH ブロックの中だけ値を持つので、実行器が渡す引数(@__seamless_error_*)に置き換える。
internal static class SqlServerBuiltinRewriter{
    internal const string ErrorNumber="@__seamless_error_number";
    internal const string ErrorMessage="@__seamless_error_message";
    internal const string ErrorSeverity="@__seamless_error_severity";
    internal const string ErrorState="@__seamless_error_state";
    internal const string ErrorLine="@__seamless_error_line";
    internal const string ErrorProcedure="@__seamless_error_procedure";

    // 置き換えたものが無ければ元の文をそのまま返す。
    internal static string Rewrite(TSqlFragment fragment,string sql){
        var visitor=new Visitor(sql);fragment.Accept(visitor);
        var start=fragment.StartOffset;var text=sql.Substring(start,fragment.FragmentLength);
        foreach(var change in visitor.Changes.OrderByDescending(static c=>c.Offset)){
            text=text.Remove(change.Offset-start,change.Length).Insert(change.Offset-start,change.Text);
        }
        return text;
    }

    private sealed class Visitor(string sql):TSqlFragmentVisitor{
        private readonly string sql=sql;
        internal readonly List<(int Offset,int Length,string Text)> Changes=[];
        public override void ExplicitVisit(FunctionCall node){
            if(node.CallTarget is null){
                var name=node.FunctionName.Value.ToUpperInvariant();
                string? replacement=name switch{
                    "ERROR_NUMBER" when node.Parameters.Count==0=>ErrorNumber,
                    "ERROR_MESSAGE" when node.Parameters.Count==0=>ErrorMessage,
                    "ERROR_SEVERITY" when node.Parameters.Count==0=>ErrorSeverity,
                    "ERROR_STATE" when node.Parameters.Count==0=>ErrorState,
                    "ERROR_LINE" when node.Parameters.Count==0=>ErrorLine,
                    "ERROR_PROCEDURE" when node.Parameters.Count==0=>ErrorProcedure,
                    // 全文検索はインストールされていない扱い。本物でも未インストールなら 0 を返す。
                    "FULLTEXTSERVICEPROPERTY" when node.Parameters.Count==1=>"0",
                    _=>null,
                };
                if(name=="SERVERPROPERTY"&&node.Parameters.Count==1&&node.Parameters[0] is StringLiteral property){
                    replacement=SystemCatalogEmulator.PropertySqlLiteral(property.Value);
                }
                if(replacement is not null){
                    this.Changes.Add((node.StartOffset,node.FragmentLength,replacement));
                    return;
                }
            }
            base.ExplicitVisit(node);
        }
        // IIF(条件, 真, 偽) は共通エンジンが持たないので CASE に直す。中の式も同じ規則で置き換える。
        public override void ExplicitVisit(IIfCall node){
            this.Changes.Add((node.StartOffset,node.FragmentLength,"CASE WHEN "+Rewrite(node.Predicate,this.sql)+" THEN "+Rewrite(node.ThenExpression,this.sql)+" ELSE "+Rewrite(node.ElseExpression,this.sql)+" END"));
        }
        public override void ExplicitVisit(GlobalVariableExpression node){
            string? replacement=node.Name.ToUpperInvariant() switch{
                "@@SERVICENAME"=>"N'MSSQLSERVER'",
                "@@MAX_PRECISION"=>"CAST(38 AS tinyint)",
                _=>null,
            };
            if(replacement is not null){this.Changes.Add((node.StartOffset,node.FragmentLength,replacement));}
        }
        public override void ExplicitVisit(UserDataTypeReference node){
            if(node.Name.BaseIdentifier.Value.Equals("sysname",StringComparison.OrdinalIgnoreCase)&&node.Name.SchemaIdentifier is null){
                this.Changes.Add((node.StartOffset,node.FragmentLength,"nvarchar(128)"));
            }
        }
    }
}
