using System.Security.Cryptography;
using System.Text;

namespace Seamless.Database.Rdbms;

public static class CSharpDatabaseGenerator {
    public static GeneratedDatabase Generate(DatabaseCatalog catalog) {
        ArgumentNullException.ThrowIfNull(catalog);
        Validate(catalog);
        var databaseName = CSharpIdentifier.Normalize(catalog.Name);
        var schemas = CreateNames(catalog,databaseName);
        var fingerprint = CreateFingerprint(catalog);
        var files = new List<GeneratedSourceFile> {
            new($"{FileName(databaseName)}.cs",CreateDatabaseSource(databaseName,fingerprint,schemas)),
        };
        foreach(var schema in schemas) {
            foreach(var table in schema.Tables) {
                files.Add(new(
                    $"Records/{FileName(schema.Name)}/{FileName(table.Name)}.cs",
                    CreateRecordSource(databaseName,schema,table)));
                files.Add(new(
                    $"Keys/{FileName(schema.Name)}/{FileName(table.Name)}.cs",
                    CreateKeySource(databaseName,schema,table)));
            }
            foreach(var view in schema.Views) {
                files.Add(new(
                    $"Records/{FileName(schema.Name)}/{FileName(view.Name)}.cs",
                    CreateRecordSource(databaseName,schema,view)));
                files.Add(new(
                    $"Keys/{FileName(schema.Name)}/{FileName(view.Name)}.cs",
                    CreateKeySource(databaseName,schema,view)));
            }
            foreach(var routine in schema.Routines.Where(static routine=>routine.Source.ResultColumns.Count>0)) {
                files.Add(new(
                    $"Records/{FileName(schema.Name)}/{FileName(routine.TypeName)}.cs",
                    CreateRecordSource(databaseName,schema,routine)));
            }
        }
        var tables=schemas.SelectMany(schema=>
            schema.Tables.Select(table=>new GeneratedTableBinding(
                $"{FileName(schema.Name)}.{FileName(table.Name)}",
                schema.Source.Name,
                table.Source.Name,
                table.Columns.Select(static column=>new GeneratedColumnBinding(
                    column.Source.Name,
                    FileName(column.Name),
                    column.Source.Type)).ToArray(),
                GeneratedRelationKind.Table))
             .Concat(schema.Views.Select(view=>new GeneratedTableBinding(
                 $"{FileName(schema.Name)}.{FileName(view.Name)}",
                schema.Source.Name,
                view.Source.Name,
                view.Columns.Select(static column=>new GeneratedColumnBinding(
                    column.Source.Name,
                    FileName(column.Name),
                    column.Source.Type)).ToArray(),
                 GeneratedRelationKind.View)))).ToArray();
        return new(catalog.Name,FileName(databaseName),fingerprint,files.AsReadOnly(),tables);
    }

    private static IReadOnlyList<SchemaName> CreateNames(DatabaseCatalog catalog,string databaseName) {
        var schemaScope = new CSharpIdentifierScope();
        schemaScope.Reserve(databaseName);
        var typeScope = new CSharpIdentifierScope();
        var schemas = new List<SchemaName>(catalog.Schemas.Count);
        foreach(var schema in catalog.Schemas.OrderBy(static value=>value.Name,StringComparer.Ordinal)) {
            var schemaName = schemaScope.Get(schema.Name);
            var relationScope = new CSharpIdentifierScope();
            relationScope.Reserve(schemaName);
            var tables = new List<TableName>(schema.Tables.Count);
            foreach(var table in schema.Tables.OrderBy(static value=>value.Name,StringComparer.Ordinal)) {
                var tableName=relationScope.Get(table.Name);
                var typeName=typeScope.Get(table.Name,$"{schema.Name}.{table.Name}");
                var columnScope = new CSharpIdentifierScope();
                columnScope.Reserve(tableName);
                var columns = table.Columns
                    .OrderBy(static value=>value.Ordinal)
                    .Select(column=>new ColumnName(column,columnScope.Get(column.Name)))
                    .ToArray();
                tables.Add(new(table,tableName,typeName,columns));
            }
            var views=new List<ViewName>(schema.Views.Count);
            foreach(var view in schema.Views.OrderBy(static value=>value.Name,StringComparer.Ordinal)) {
                var viewName=relationScope.Get(view.Name);
                var typeName=typeScope.Get(view.Name,$"{schema.Name}.{view.Name}");
                var columnScope=new CSharpIdentifierScope();
                columnScope.Reserve(viewName);
                var columns=view.Columns
                    .OrderBy(static value=>value.Ordinal)
                    .Select(column=>new ColumnName(column,columnScope.Get(column.Name)))
                    .ToArray();
                views.Add(new(view,viewName,typeName,columns));
            }
            var routines=new List<RoutineName>(schema.Routines.Count);
            foreach(var routine in schema.Routines.OrderBy(static value=>value.Name,StringComparer.Ordinal)) {
                var routineName=relationScope.Get(routine.Name);
                var typeName=typeScope.Get(routine.Name,$"{schema.Name}.{routine.Name}");
                var columnScope=new CSharpIdentifierScope();
                columnScope.Reserve(routineName);
                var columns=routine.ResultColumns
                    .OrderBy(static value=>value.Ordinal)
                    .Select(column=>new ColumnName(column,columnScope.Get(column.Name)))
                    .ToArray();
                routines.Add(new(routine,routineName,typeName,columns));
            }
            schemas.Add(new(schema,schemaName,tables,views,routines));
        }
        return schemas;
    }

    private static string CreateDatabaseSource(
        string databaseName,
        string fingerprint,
        IReadOnlyList<SchemaName> schemas) {
        var builder = Header();
        builder.AppendLine($"namespace {databaseName} {{");
        builder.AppendLine();
        builder.AppendLine("internal static class DatabaseGenerator;");
        builder.AppendLine();
        builder.AppendLine($"public sealed partial class {databaseName}:global::Seamless.Database.DatabaseContainer {{");
        builder.AppendLine($"    public {databaseName}(global::Seamless.Database.DatabaseContainerOptions? options=null):base(options) {{");
        foreach(var schema in schemas) {
            builder.AppendLine($"        this.{schema.Name}=new(this);");
        }
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine($"    public const string GeneratedSchemaFingerprint=\"{fingerprint}\";");
        foreach(var schema in schemas) {
            builder.AppendLine($"    public global::{databaseName}.Schemas.{schema.Name} {schema.Name} {{ get; }}");
        }
        builder.AppendLine("}");
        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine($"namespace {databaseName}.Schemas {{");
        foreach(var schema in schemas) {
            builder.AppendLine($"    public sealed class {schema.Name}(global::Seamless.Database.DatabaseContainer database):global::Seamless.Database.DatabaseSchema(database,\"{Escape(schema.Source.Name)}\") {{");
            foreach(var table in schema.Tables) {
                var keyArguments=string.Join(",",TableKeyColumns(table).Select(static column=>$"record.{column.Name}"));
                builder.AppendLine($"        public global::Seamless.Database.Table<global::{databaseName}.Keys.{table.TypeName},global::{databaseName}.Records.{table.TypeName}> {table.Name}=>this.Table<global::{databaseName}.Keys.{table.TypeName},global::{databaseName}.Records.{table.TypeName}>(static record=>new({keyArguments}));");
            }
            foreach(var view in schema.Views) {
                var keyArguments=string.Join(",",ViewKeyColumns(view).Select(static column=>$"record.{column.Name}"));
                builder.AppendLine($"        public global::Seamless.Database.MaterializedView<global::{databaseName}.Keys.{view.TypeName},global::{databaseName}.Records.{view.TypeName}> {view.Name}=>this.MaterializedView<global::{databaseName}.Keys.{view.TypeName},global::{databaseName}.Records.{view.TypeName}>(static record=>new({keyArguments}));");
            }
            foreach(var routine in schema.Routines) {
                AppendRoutineSource(builder,databaseName,routine);
            }
            builder.AppendLine("    }");
        }
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static string CreateRecordSource(string databaseName,SchemaName schema,TableName table) {
        var builder = Header();
        builder.AppendLine($"namespace {databaseName}.Records;");
        builder.AppendLine();
        builder.AppendLine($"public partial struct {table.TypeName} {{");
        foreach(var column in table.Columns) {
            builder.AppendLine($"    public partial {TypeName(column.Source.Type)} {column.Name} {{ get; }}");
        }
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static string CreateRecordSource(string databaseName,SchemaName schema,ViewName view) {
        var builder=Header();
        builder.AppendLine($"namespace {databaseName}.Records;");
        builder.AppendLine();
        builder.AppendLine($"public partial struct {view.TypeName} {{");
        foreach(var column in view.Columns) {
            builder.AppendLine($"    public partial {TypeName(column.Source.Type)} {column.Name} {{ get; }}");
        }
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static string CreateRecordSource(string databaseName,SchemaName schema,RoutineName routine) {
        var builder=Header();
        builder.AppendLine($"namespace {databaseName}.Records;");
        builder.AppendLine();
        builder.AppendLine($"public partial struct {routine.TypeName} {{");
        foreach(var column in routine.Columns) {
            builder.AppendLine($"    public partial {TypeName(column.Source.Type)} {column.Name} {{ get; }}");
        }
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static void AppendRoutineSource(StringBuilder builder,string databaseName,RoutineName routine) {
        var parameters=string.Join(",",routine.Source.Parameters.OrderBy(static value=>value.Ordinal)
            .Select(parameter=>$"{(parameter.IsOutput?"out ":string.Empty)}{TypeName(parameter.Type)} {ParameterName(parameter.Name)}"));
        var outputParameters=routine.Source.Parameters.Where(static parameter=>parameter.IsOutput).ToArray();
        switch(routine.Source.Kind) {
            case RoutineKind.TableFunction:
                if(routine.Columns.Count==0) {
                    throw new NotSupportedException($"Table function '{routine.Source.SchemaName}.{routine.Source.Name}' has no result columns.");
                }
                AppendRoutineMethod(builder,
                    $"        public global::Seamless.Database.TableFunction<global::{databaseName}.Records.{routine.TypeName}> {routine.Name}({parameters})",
                    $"this.TableFunction<global::{databaseName}.Records.{routine.TypeName}>(()=>throw new global::System.NotSupportedException({Literal($"Imported SQL Server table function '{routine.Source.SchemaName}.{routine.Source.Name}' requires relational-algebra execution.")}))",
                    outputParameters);
                break;
            case RoutineKind.ScalarFunction:
                var scalarType=routine.Source.ReturnType is null
                    ?"object?"
                    :TypeName(routine.Source.ReturnType);
                AppendRoutineMethod(builder,
                    $"        public global::Seamless.Database.ScalarFunction<{scalarType}> {routine.Name}({parameters})",
                    $"this.ScalarFunction<{scalarType}>(()=>throw new global::System.NotSupportedException({Literal($"Imported SQL Server scalar function '{routine.Source.SchemaName}.{routine.Source.Name}' requires relational-algebra execution.")}))",
                    outputParameters);
                break;
            case RoutineKind.StoredProcedure:
                var resultType=routine.Columns.Count==0
                    ?"object?"
                    :$"global::System.Collections.Generic.IReadOnlyList<global::{databaseName}.Records.{routine.TypeName}>";
                AppendRoutineMethod(builder,
                    $"        public global::Seamless.Database.StoredProcedure<{resultType}> {routine.Name}({parameters})",
                    $"this.StoredProcedure<{resultType}>(()=>throw new global::System.NotSupportedException({Literal($"Imported SQL Server stored procedure '{routine.Source.SchemaName}.{routine.Source.Name}' requires relational-algebra execution.")}))",
                    outputParameters);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private static void AppendRoutineMethod(
        StringBuilder builder,
        string signature,
        string expression,
        IReadOnlyList<ParameterCatalog> outputParameters) {
        if(outputParameters.Count==0) {
            builder.AppendLine($"{signature}=>{expression};");
            return;
        }
        builder.AppendLine($"{signature} {{");
        foreach(var parameter in outputParameters) {
            builder.AppendLine($"            {ParameterName(parameter.Name)}=default!;");
        }
        builder.AppendLine($"            return {expression};");
        builder.AppendLine("        }");
    }

    private static string CreateKeySource(string databaseName,SchemaName schema,TableName table) {
        var builder = Header();
        builder.AppendLine($"namespace {databaseName}.Keys;");
        builder.AppendLine();
        builder.AppendLine($"public partial struct {table.TypeName} {{");
        foreach(var column in TableKeyColumns(table)) {
            builder.AppendLine($"    public partial {TypeName(column.Source.Type)} {column.Name} {{ get; }}");
        }
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static IReadOnlyList<ColumnName> TableKeyColumns(TableName table)=>table.Source.PrimaryKey is null
        ?table.Columns
        :table.Source.PrimaryKey.Columns
            .Select(key=>table.Columns.Single(column=>column.Source.Name==key.Name))
            .ToArray();

    private static string CreateKeySource(string databaseName,SchemaName schema,ViewName view) {
        var builder=Header();
        builder.AppendLine($"namespace {databaseName}.Keys;");
        builder.AppendLine();
        builder.AppendLine($"public partial struct {view.TypeName} {{");
        foreach(var column in ViewKeyColumns(view)) {
            builder.AppendLine($"    public partial {TypeName(column.Source.Type)} {column.Name} {{ get; }}");
        }
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static IReadOnlyList<ColumnName> ViewKeyColumns(ViewName view)=>view.Source.UniqueKey is null
        ?view.Columns
        :view.Source.UniqueKey.Columns
            .Select(key=>view.Columns.Single(column=>column.Source.Name==key.Name))
            .ToArray();

    private static string CreateFingerprint(DatabaseCatalog catalog) {
        var canonical = new StringBuilder();
        Append(canonical,catalog.Name);
        foreach(var schema in catalog.Schemas.OrderBy(static value=>value.Name,StringComparer.Ordinal)) {
            Append(canonical,schema.Name);
            foreach(var table in schema.Tables.OrderBy(static value=>value.Name,StringComparer.Ordinal)) {
                Append(canonical,table.Name);
                foreach(var column in table.Columns.OrderBy(static value=>value.Ordinal)) {
                    Append(canonical,column.Ordinal.ToString(global::System.Globalization.CultureInfo.InvariantCulture));
                    Append(canonical,column.Name);
                    AppendType(canonical,column.Type);
                    Append(canonical,column.IsIdentity.ToString());
                    Append(canonical,column.DefaultDefinition??string.Empty);
                    Append(canonical,column.ComputedDefinition??string.Empty);
                }
                Append(canonical,table.PrimaryKey?.Name??string.Empty);
                if(table.PrimaryKey is not null) {
                    foreach(var column in table.PrimaryKey.Columns) {
                        Append(canonical,column.Name);
                        Append(canonical,column.IsDescending.ToString());
                    }
                }
                Append(canonical,"UniqueKeys");
                foreach(var key in table.UniqueKeys.OrderBy(static value=>value.Name,StringComparer.Ordinal)) {
                    AppendKey(canonical,key);
                }
                Append(canonical,"ForeignKeys");
                foreach(var key in table.ForeignKeys.OrderBy(static value=>value.Name,StringComparer.Ordinal)) {
                    Append(canonical,key.Name);
                    Append(canonical,key.ReferencedSchema);
                    Append(canonical,key.ReferencedTable);
                    Append(canonical,key.DeleteAction);
                    Append(canonical,key.UpdateAction);
                    foreach(var column in key.Columns) {
                        Append(canonical,column.Column);
                        Append(canonical,column.ReferencedColumn);
                    }
                }
                Append(canonical,"Checks");
                foreach(var constraint in table.CheckConstraints.OrderBy(static value=>value.Name,StringComparer.Ordinal)) {
                    Append(canonical,constraint.Name);
                    Append(canonical,constraint.Definition);
                    Append(canonical,constraint.IsDisabled.ToString());
                }
                Append(canonical,"Triggers");
                foreach(var trigger in table.Triggers.OrderBy(static value=>value.Name,StringComparer.Ordinal)) {
                    Append(canonical,trigger.Name);
                    Append(canonical,trigger.Definition);
                    Append(canonical,trigger.IsInsteadOf.ToString());
                    Append(canonical,trigger.IsDisabled.ToString());
                }
            }
            Append(canonical,"Views");
            foreach(var view in schema.Views.OrderBy(static value=>value.Name,StringComparer.Ordinal)) {
                Append(canonical,view.Name);
                foreach(var column in view.Columns.OrderBy(static value=>value.Ordinal)) {
                    Append(canonical,column.Ordinal.ToString(global::System.Globalization.CultureInfo.InvariantCulture));
                    Append(canonical,column.Name);
                    AppendType(canonical,column.Type);
                }
                Append(canonical,view.UniqueKey?.Name??string.Empty);
                if(view.UniqueKey is not null) {
                    foreach(var column in view.UniqueKey.Columns) {
                        Append(canonical,column.Name);
                        Append(canonical,column.IsDescending.ToString());
                    }
                }
                Append(canonical,view.Definition??string.Empty);
                foreach(var dependency in (view.Dependencies??[]).OrderBy(static value=>value,StringComparer.Ordinal)) {
                    Append(canonical,dependency);
                }
            }
            Append(canonical,"Routines");
            foreach(var routine in schema.Routines.OrderBy(static value=>value.Name,StringComparer.Ordinal)
                .ThenBy(static value=>value.Kind)) {
                Append(canonical,routine.Name);
                Append(canonical,routine.Kind.ToString());
                AppendType(canonical,routine.ReturnType);
                foreach(var parameter in routine.Parameters.OrderBy(static value=>value.Ordinal)) {
                    Append(canonical,parameter.Ordinal.ToString(global::System.Globalization.CultureInfo.InvariantCulture));
                    Append(canonical,parameter.Name);
                    AppendType(canonical,parameter.Type);
                    Append(canonical,parameter.IsOutput.ToString());
                    Append(canonical,parameter.IsTableValued.ToString());
                }
                foreach(var column in routine.ResultColumns.OrderBy(static value=>value.Ordinal)) {
                    Append(canonical,column.Ordinal.ToString(global::System.Globalization.CultureInfo.InvariantCulture));
                    Append(canonical,column.Name);
                    AppendType(canonical,column.Type);
                    Append(canonical,column.IsIdentity.ToString());
                }
                Append(canonical,routine.Definition??string.Empty);
                Append(canonical,routine.HasMutation.ToString());
                Append(canonical,routine.ResultSetCount.ToString(global::System.Globalization.CultureInfo.InvariantCulture));
                Append(canonical,routine.ReturnCount.ToString(global::System.Globalization.CultureInfo.InvariantCulture));
                if(routine.RelationalAlgebra is not null) {
                    foreach(var operation in routine.RelationalAlgebra.Operations) {
                        AppendRelationalAlgebra(canonical,operation);
                    }
                }
            }
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private static void AppendType(StringBuilder builder,DatabaseType? type) {
        if(type is null) {
            Append(builder,string.Empty);
            return;
        }
        Append(builder,type.Kind.ToString());
        Append(builder,type.IsNullable.ToString());
        Append(builder,type.MaximumLength?.ToString(global::System.Globalization.CultureInfo.InvariantCulture)??string.Empty);
        Append(builder,type.Precision?.ToString(global::System.Globalization.CultureInfo.InvariantCulture)??string.Empty);
        Append(builder,type.Scale?.ToString(global::System.Globalization.CultureInfo.InvariantCulture)??string.Empty);
        Append(builder,type.RuntimeType?.FullName??string.Empty);
    }

    private static void AppendKey(StringBuilder builder,UniqueKeyCatalog key) {
        Append(builder,key.Name);
        foreach(var column in key.Columns) {
            Append(builder,column.Name);
            Append(builder,column.IsDescending.ToString());
        }
    }

    private static void AppendRelationalAlgebra(
        StringBuilder builder,
        RelationalAlgebraNode operation) {
        Append(builder,operation.Operator.ToString());
        Append(builder,operation.RelationName??string.Empty);
        foreach(var input in operation.Inputs) {
            AppendRelationalAlgebra(builder,input);
        }
    }

    private static void Append(StringBuilder builder,string value) {
        builder.Append(value.Length);
        builder.Append(':');
        builder.Append(value);
        builder.Append(';');
    }

    private static void Validate(DatabaseCatalog catalog) {
        ArgumentException.ThrowIfNullOrWhiteSpace(catalog.Name);
        var schemaNames=catalog.Schemas.Select(static schema=>schema.Name).ToHashSet(StringComparer.Ordinal);
        if(schemaNames.Count!=catalog.Schemas.Count) {
            throw new ArgumentException("The catalog contains duplicate schema names.",nameof(catalog));
        }
        foreach(var schema in catalog.Schemas) {
            ArgumentException.ThrowIfNullOrWhiteSpace(schema.Name);
            var relationNames=schema.Tables.Select(static table=>table.Name)
                .Concat(schema.Views.Select(static view=>view.Name))
                .Concat(schema.Routines.Select(static routine=>routine.Name))
                .ToHashSet(StringComparer.Ordinal);
            if(relationNames.Count!=schema.Tables.Count+schema.Views.Count+schema.Routines.Count) {
                throw new ArgumentException($"Schema '{schema.Name}' has duplicate relation names.",nameof(catalog));
            }
            foreach(var table in schema.Tables) {
                ArgumentException.ThrowIfNullOrWhiteSpace(table.Name);
                if(table.Columns.Count==0) {
                    throw new ArgumentException($"Table '{schema.Name}.{table.Name}' has no columns.",nameof(catalog));
                }
                var names = table.Columns.Select(static column=>column.Name).ToHashSet(StringComparer.Ordinal);
                if(names.Count!=table.Columns.Count) {
                    throw new ArgumentException($"Table '{schema.Name}.{table.Name}' has duplicate columns.",nameof(catalog));
                }
                if(table.PrimaryKey is not null&&table.PrimaryKey.Columns.Any(column=>!names.Contains(column.Name))) {
                    throw new ArgumentException($"Table '{schema.Name}.{table.Name}' has an invalid primary key.",nameof(catalog));
                }
                foreach(var key in table.UniqueKeys) {
                    if(key.Columns.Count==0||key.Columns.Any(column=>!names.Contains(column.Name))) {
                        throw new ArgumentException($"Table '{schema.Name}.{table.Name}' has an invalid unique key '{key.Name}'.",nameof(catalog));
                    }
                }
                foreach(var foreignKey in table.ForeignKeys) {
                    if(foreignKey.Columns.Count==0
                        ||foreignKey.Columns.Any(column=>!names.Contains(column.Column))) {
                        throw new ArgumentException($"Table '{schema.Name}.{table.Name}' has an invalid foreign key '{foreignKey.Name}'.",nameof(catalog));
                    }
                    var referencedTable=catalog.Schemas
                        .SingleOrDefault(value=>value.Name==foreignKey.ReferencedSchema)?
                        .Tables.SingleOrDefault(value=>value.Name==foreignKey.ReferencedTable);
                    if(referencedTable is null
                        ||foreignKey.Columns.Any(column=>!referencedTable.Columns.Any(value=>value.Name==column.ReferencedColumn))) {
                        throw new ArgumentException($"Table '{schema.Name}.{table.Name}' has an unresolved foreign key '{foreignKey.Name}'.",nameof(catalog));
                    }
                }
                if(table.CheckConstraints.Any(static constraint=>string.IsNullOrWhiteSpace(constraint.Name)
                    ||string.IsNullOrWhiteSpace(constraint.Definition))) {
                    throw new ArgumentException($"Table '{schema.Name}.{table.Name}' has an invalid check constraint.",nameof(catalog));
                }
                if(table.Triggers.Any(static trigger=>string.IsNullOrWhiteSpace(trigger.Name))) {
                    throw new ArgumentException($"Table '{schema.Name}.{table.Name}' has an invalid trigger.",nameof(catalog));
                }
            }
            foreach(var view in schema.Views) {
                ArgumentException.ThrowIfNullOrWhiteSpace(view.Name);
                if(view.Columns.Count==0) {
                    throw new ArgumentException($"View '{schema.Name}.{view.Name}' has no columns.",nameof(catalog));
                }
                var names=view.Columns.Select(static column=>column.Name).ToHashSet(StringComparer.Ordinal);
                if(names.Count!=view.Columns.Count) {
                    throw new ArgumentException($"View '{schema.Name}.{view.Name}' has duplicate columns.",nameof(catalog));
                }
                if(view.UniqueKey is not null
                    &&(view.UniqueKey.Columns.Count==0||view.UniqueKey.Columns.Any(column=>!names.Contains(column.Name)))) {
                    throw new ArgumentException($"View '{schema.Name}.{view.Name}' has an invalid unique key.",nameof(catalog));
                }
            }
            foreach(var routine in schema.Routines) {
                ArgumentException.ThrowIfNullOrWhiteSpace(routine.Name);
                if(routine.Kind==RoutineKind.TableFunction&&routine.ResultColumns.Count==0) {
                    throw new ArgumentException($"Table function '{schema.Name}.{routine.Name}' has no result columns.",nameof(catalog));
                }
                var names=routine.ResultColumns.Select(static column=>column.Name).ToHashSet(StringComparer.Ordinal);
                if(names.Count!=routine.ResultColumns.Count) {
                    throw new ArgumentException($"Routine '{schema.Name}.{routine.Name}' has duplicate result columns.",nameof(catalog));
                }
            }
        }
    }

    private static StringBuilder Header() => new("// <auto-generated/>\n#nullable enable\n\n");
    private static string Escape(string value)=>value.Replace("\\","\\\\",StringComparison.Ordinal).Replace("\"","\\\"",StringComparison.Ordinal);
    private static string Literal(string value)=>$"\"{Escape(value)}\"";
    private static string FileName(string identifier)=>identifier.TrimStart('@');
    private static string ParameterName(string name) {
        var normalized=name.StartsWith('@')?name[1..]:name;
        return CSharpIdentifier.Normalize(normalized);
    }
    private static string TypeName(DatabaseType type) {
        var name=type.RuntimeType is null
            ?type.Kind switch {
            DatabaseTypeKind.Boolean=>"bool",
            DatabaseTypeKind.Byte=>"byte",
            DatabaseTypeKind.Int16=>"short",
            DatabaseTypeKind.Int32=>"int",
            DatabaseTypeKind.Int64=>"long",
            DatabaseTypeKind.Decimal=>"decimal",
            DatabaseTypeKind.Single=>"float",
            DatabaseTypeKind.Double=>"double",
            DatabaseTypeKind.String or DatabaseTypeKind.Xml=>"string",
            DatabaseTypeKind.Binary=>"byte[]",
            DatabaseTypeKind.Guid=>"global::System.Guid",
            DatabaseTypeKind.Date=>"global::System.DateOnly",
            DatabaseTypeKind.Time=>"global::System.TimeOnly",
            DatabaseTypeKind.DateTime=>"global::System.DateTime",
            DatabaseTypeKind.DateTimeOffset=>"global::System.DateTimeOffset",
            DatabaseTypeKind.Object=>"object",
                _=>throw new ArgumentOutOfRangeException(nameof(type)),
            }
            :$"global::{type.RuntimeType.FullName!.Replace('+','.')}";
        return type.IsNullable?$"{name}?":name;
    }

    private sealed record ColumnName(ColumnCatalog Source,string Name);
    private sealed record TableName(TableCatalog Source,string Name,string TypeName,IReadOnlyList<ColumnName> Columns);
    private sealed record ViewName(ViewCatalog Source,string Name,string TypeName,IReadOnlyList<ColumnName> Columns);
    private sealed record RoutineName(RoutineCatalog Source,string Name,string TypeName,IReadOnlyList<ColumnName> Columns);
    private sealed record SchemaName(
        SchemaCatalog Source,
        string Name,
        IReadOnlyList<TableName> Tables,
        IReadOnlyList<ViewName> Views,
        IReadOnlyList<RoutineName> Routines);
}
