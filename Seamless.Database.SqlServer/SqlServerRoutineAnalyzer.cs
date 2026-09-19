using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Seamless.Database.SqlServer;

public sealed record SqlServerRoutineAnalysis(
    bool HasMutation,
    int ResultSetCount,
    int ReturnCount,
    global::Seamless.Database.Rdbms.RelationalAlgebraPlan RelationalAlgebra);

public static class SqlServerRoutineAnalyzer {
    public static SqlServerRoutineAnalysis Analyze(string definition) {
        ArgumentException.ThrowIfNullOrWhiteSpace(definition);
        var parser=new TSql180Parser(initialQuotedIdentifiers:true);
        using var reader=new StringReader(definition);
        var fragment=parser.Parse(reader,out var errors);
        if(errors.Count>0) {
            var message=string.Join(
                Environment.NewLine,
                errors.Select(static error=>
                    $"Line {error.Line}, column {error.Column}: {error.Message}"));
            throw new FormatException(message);
        }
        var visitor=new BehaviorVisitor();
        fragment.Accept(visitor);
        if(visitor.UnsupportedFeature is not null) {
            throw new NotSupportedException(
                $"SQL Server routine contains unsupported construct '{visitor.UnsupportedFeature}'.");
        }
        return new(
            visitor.HasMutation,
            visitor.ResultSetCount,
            visitor.ReturnCount,
            new(visitor.Operations.ToArray()));
    }

    private sealed class BehaviorVisitor:TSqlFragmentVisitor {
        public bool HasMutation { get; private set; }
        public int ResultSetCount { get; private set; }
        public int ReturnCount { get; private set; }
        public string? UnsupportedFeature { get; private set; }
        public List<global::Seamless.Database.Rdbms.RelationalAlgebraNode> Operations { get; }=[];

        public override void ExplicitVisit(InsertStatement node) {
            this.HasMutation=true;
            this.Operations.Add(this.Operation(
                global::Seamless.Database.Rdbms.RelationalAlgebraOperator.Insert,node));
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(UpdateStatement node) {
            this.HasMutation=true;
            this.Operations.Add(this.Operation(
                global::Seamless.Database.Rdbms.RelationalAlgebraOperator.Update,node));
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(DeleteStatement node) {
            this.HasMutation=true;
            this.Operations.Add(this.Operation(
                global::Seamless.Database.Rdbms.RelationalAlgebraOperator.Delete,node));
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(MergeStatement node) {
            this.HasMutation=true;
            this.Operations.Add(this.Operation(
                global::Seamless.Database.Rdbms.RelationalAlgebraOperator.Merge,node));
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(SelectStatement node) {
            this.ResultSetCount++;
            this.Operations.Add(this.Operation(
                global::Seamless.Database.Rdbms.RelationalAlgebraOperator.Select,node));
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(ReturnStatement node) {
            this.ReturnCount++;
            this.Operations.Add(this.Operation(
                global::Seamless.Database.Rdbms.RelationalAlgebraOperator.Return,node));
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(ExecuteStatement node) {
            if(node.ExecuteSpecification.ExecutableEntity is ExecutableProcedureReference executable
                && executable.ProcedureReference.ProcedureReference is ProcedureReference procedure) {
                var relation=string.Join(
                    ".",
                    procedure.Name.Identifiers.Select(static identifier=>identifier.Value));
                if(relation.Length==0) {
                    this.UnsupportedFeature ??=nameof(ExecuteStatement);
                }
                else {
                    this.Operations.Add(new(
                        global::Seamless.Database.Rdbms.RelationalAlgebraOperator.Call,
                        relation,
                        []));
                }
            }
            else {
                this.UnsupportedFeature ??=nameof(ExecuteStatement);
            }
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(ExecuteAsStatement node) {
            base.ExplicitVisit(node);
        }

        private global::Seamless.Database.Rdbms.RelationalAlgebraNode Operation(
            global::Seamless.Database.Rdbms.RelationalAlgebraOperator @operator,
            TSqlFragment statement) {
            var relations=new RelationVisitor();
            statement.Accept(relations);
            var scans=relations.Relations
                .Select(static relation=>new global::Seamless.Database.Rdbms.RelationalAlgebraNode(
                    global::Seamless.Database.Rdbms.RelationalAlgebraOperator.Scan,relation,[]))
                .ToArray();
            return new(@operator,relations.Relations.FirstOrDefault(),scans);
        }

        private sealed class RelationVisitor:TSqlFragmentVisitor {
            public List<string> Relations { get; }=[];

            public override void ExplicitVisit(NamedTableReference node) {
                var relation=string.Join(".",node.SchemaObject.Identifiers.Select(static identifier=>identifier.Value));
                if(relation.Length>0&&!this.Relations.Contains(relation,StringComparer.Ordinal)) {
                    this.Relations.Add(relation);
                }
                base.ExplicitVisit(node);
            }
        }
    }
}
