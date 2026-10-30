using static Seamless.Database.Generator.GeneratorHelpers;

namespace Seamless.Database.Generator;

[Generator]
public sealed class DatabaseContainerGenerator:IIncrementalGenerator {
    private const string DatabaseNamespace = "Seamless.Database";
    private const string ContainerBaseFullName = "Seamless.Database.DatabaseContainer";
    private const string SchemaBaseFullName = "Seamless.Database.DatabaseSchema";
    private const string ReservedCatalogName = "DatabaseObjects";

    private static readonly DiagnosticDescriptor DuplicateName = new(
        id: "SDB001",
        title: "Duplicate database object name",
        messageFormat: "Database object name '{0}' is duplicated",
        category: "Seamless.Database.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
    private static readonly DiagnosticDescriptor ReservedName = new(
        id: "SDB002",
        title: "Reserved database object name",
        messageFormat: "Database object name '{0}' is reserved for the system catalog",
        category: "Seamless.Database.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context) {
        var types = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node,_) => node is TypeDeclarationSyntax,
                static (syntaxContext,_) => CreateTypeTarget(syntaxContext))
            .Where(static target => target is not null)
            .Select(static (target,_) => target!);

        context.RegisterSourceOutput(types.Collect(),static (productionContext,targets) =>
            Generate(productionContext,targets));
    }

    private static TypeTarget? CreateTypeTarget(GeneratorSyntaxContext context) {
        var declaration = (TypeDeclarationSyntax)context.Node;
        if(context.SemanticModel.GetDeclaredSymbol(declaration) is not INamedTypeSymbol symbol) {
            return null;
        }
        return new(symbol,declaration.Modifiers.Any(SyntaxKind.PartialKeyword));
    }

    private static void Generate(SourceProductionContext context,ImmutableArray<TypeTarget> targets) {
        if(targets.IsDefaultOrEmpty) {
            return;
        }

        var uniqueTypes = targets
            .GroupBy(static target => target.Symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),StringComparer.Ordinal)
            .Select(static group => new TypeTarget(group.First().Symbol,group.Any(static target => target.IsPartial)))
            .ToArray();
        var markerNamespaces = new HashSet<string>(
            uniqueTypes
                .Where(static target => target.Symbol.Name=="DatabaseGenerator")
                .Select(static target => NamespaceName(target.Symbol.ContainingNamespace)),
            StringComparer.Ordinal);
        if(markerNamespaces.Count==0) {
            return;
        }

        foreach(var target in uniqueTypes
            .Where(static target => IsContainer(target))
            .OrderBy(static target => target.Symbol.ToDisplayString(),StringComparer.Ordinal)) {
            var namespaceName = NamespaceName(target.Symbol.ContainingNamespace);
            if(!markerNamespaces.Contains(namespaceName)) {
                continue;
            }

            var declarations = CollectObjects(target.Symbol);
            var hasErrors = ReportDiagnostics(context,declarations);
            if(hasErrors) {
                continue;
            }

            var source = BuildSource(target.Symbol,declarations);
            context.AddSource($"{target.Symbol.ToDisplayString()}.Container.g.cs",SourceText.From(source,Encoding.UTF8));
        }
    }

    private static bool IsContainer(TypeTarget target) {
        var symbol = target.Symbol;
        if(!target.IsPartial||symbol.TypeKind!=TypeKind.Class||symbol.IsGenericType||symbol.ContainingType is not null) {
            return false;
        }
        for(var baseType=symbol.BaseType;baseType is not null;baseType=baseType.BaseType) {
            if(baseType.ToDisplayString()==ContainerBaseFullName) {
                return true;
            }
        }
        return false;
    }

    private static ImmutableArray<ObjectDeclaration> CollectObjects(INamedTypeSymbol symbol) {
        var builder = ImmutableArray.CreateBuilder<ObjectDeclaration>();
        CollectObjects(symbol,string.Empty,builder,new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default));
        return builder
            .OrderBy(static declaration => declaration.Name,StringComparer.Ordinal)
            .ThenBy(static declaration => declaration.Kind,StringComparer.Ordinal)
            .ToImmutableArray();
    }

    private static void CollectObjects(
        INamedTypeSymbol symbol,
        string prefix,
        ImmutableArray<ObjectDeclaration>.Builder builder,
        HashSet<INamedTypeSymbol> visited) {
        if(!visited.Add(symbol)) {
            return;
        }
        foreach(var member in symbol.GetMembers()) {
            if(member.IsStatic||member.IsImplicitlyDeclared) {
                continue;
            }

            var path=prefix.Length==0?member.Name:$"{prefix}.{member.Name}";
            switch(member) {
                case IPropertySymbol property when TryGetKeyedTableRecordType(property.Type,out var keyedTableRecord):
                    builder.Add(CreateObject("Table",path,keyedTableRecord,"Table",property));
                    break;
                case IPropertySymbol property when TryGetRecordType(property.Type,"Table",out var tableRecord):
                    builder.Add(CreateObject("Table",path,tableRecord,"Table",property));
                    break;
                case IPropertySymbol property when TryGetMaterializedRecordType(property.Type,out var materializedRecord):
                    builder.Add(CreateObject("View",path,materializedRecord,"MaterializedView",property));
                    break;
                case IPropertySymbol property when TryGetRecordType(property.Type,"View",out var viewRecord):
                    builder.Add(CreateObject("View",path,viewRecord,null,property));
                    break;
                case IMethodSymbol { MethodKind: MethodKind.Ordinary } method
                    when TryGetRecordType(method.ReturnType,"View",out var viewRecord):
                    builder.Add(CreateObject("View",path,viewRecord,null,method));
                    break;
                case IMethodSymbol { MethodKind: MethodKind.Ordinary } method
                    when TryGetRecordType(method.ReturnType,"TableFunction",out var functionRecord):
                    builder.Add(CreateObject("TableFunction",path,functionRecord,null,method));
                    break;
                case IMethodSymbol { MethodKind:MethodKind.Ordinary } method
                    when TryGetRecordType(method.ReturnType,"ScalarFunction",out var scalarResult):
                    builder.Add(CreateObject("ScalarFunction",path,scalarResult,null,method));
                    break;
                case IMethodSymbol { MethodKind:MethodKind.Ordinary } method
                    when TryGetRecordType(method.ReturnType,"StoredProcedure",out var procedureResult):
                    builder.Add(CreateObject("StoredProcedure",path,procedureResult,null,method));
                    break;
                case IPropertySymbol property when property.Type is INamedTypeSymbol schema&&IsSchema(schema):
                    CollectObjects(schema,path,builder,visited);
                    break;
            }
        }
        visited.Remove(symbol);
    }

    private static bool IsSchema(INamedTypeSymbol symbol) {
        for(var current=symbol;current is not null;current=current.BaseType) {
            if(current.ToDisplayString()==SchemaBaseFullName) {
                return true;
            }
        }
        return false;
    }

    private static ObjectDeclaration CreateObject(
        string kind,string name,ITypeSymbol recordType,string? importKind,ISymbol member) {
        var location = member.Locations.FirstOrDefault(static location => location.IsInSource)??Location.None;
        var typeName = recordType
            .WithNullableAnnotation(NullableAnnotation.None)
            .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        string? keyType=null;
        if(member is IPropertySymbol property&&property.Type is INamedTypeSymbol {TypeArguments.Length:2} named){keyType=named.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);}
        return new(kind,name,typeName,importKind,location,keyType);
    }

    private static bool TryGetMaterializedRecordType(ITypeSymbol type,out ITypeSymbol recordType) {
        if(type is INamedTypeSymbol { IsGenericType: true } named
            &&named.OriginalDefinition.Name=="MaterializedView"
            &&named.OriginalDefinition.ContainingNamespace.ToDisplayString()==DatabaseNamespace
            &&named.TypeArguments.Length==2) {
            recordType=named.TypeArguments[1];
            return true;
        }
        recordType=type;
        return false;
    }

    private static bool TryGetKeyedTableRecordType(ITypeSymbol type,out ITypeSymbol recordType) {
        if(type is INamedTypeSymbol { IsGenericType:true } named
            &&named.OriginalDefinition.Name=="Table"
            &&named.OriginalDefinition.ContainingNamespace.ToDisplayString()==DatabaseNamespace
            &&named.TypeArguments.Length==2) {
            recordType=named.TypeArguments[1];
            return true;
        }
        recordType=type;
        return false;
    }

    private static bool TryGetRecordType(ITypeSymbol type,string genericName,out ITypeSymbol recordType) {
        if(type is INamedTypeSymbol { IsGenericType: true } named
            &&named.OriginalDefinition.Name==genericName
            &&named.OriginalDefinition.ContainingNamespace.ToDisplayString()==DatabaseNamespace
            &&named.TypeArguments.Length==1) {
            recordType=named.TypeArguments[0];
            return true;
        }
        recordType=type;
        return false;
    }

    private static bool ReportDiagnostics(SourceProductionContext context,ImmutableArray<ObjectDeclaration> declarations) {
        var hasErrors = false;
        foreach(var declaration in declarations.Where(static declaration => declaration.Name==ReservedCatalogName)) {
            context.ReportDiagnostic(Diagnostic.Create(ReservedName,declaration.Location,declaration.Name));
            hasErrors=true;
        }
        foreach(var group in declarations.GroupBy(static declaration => declaration.Name,StringComparer.Ordinal).Where(static group => group.Count()>1)) {
            foreach(var declaration in group) {
                context.ReportDiagnostic(Diagnostic.Create(DuplicateName,declaration.Location,declaration.Name));
            }
            hasErrors=true;
        }
        return hasErrors;
    }

    private static string BuildSource(INamedTypeSymbol symbol,ImmutableArray<ObjectDeclaration> declarations) {
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated/>");
        builder.AppendLine("#nullable enable");
        var namespaceName = NamespaceName(symbol.ContainingNamespace);
        var indent = string.Empty;
        if(namespaceName.Length>0) {
            builder.AppendLine($"namespace {namespaceName} {{");
            indent="    ";
        }

        var className = Identifier(symbol.Name);
        builder.AppendLine($"{indent}partial class {className} {{");
        builder.AppendLine($"{indent}    private static readonly global::Seamless.Database.System.SystemTable<global::Seamless.Database.System.Keys.DatabaseObject,global::Seamless.Database.System.Records.DatabaseObject> GeneratedDatabaseObjects=");
        builder.AppendLine($"{indent}        CreateSystemTable<global::Seamless.Database.System.Keys.DatabaseObject,global::Seamless.Database.System.Records.DatabaseObject>(");
        builder.AppendLine($"{indent}            [");
        builder.AppendLine($"{indent}                new(new(\"DatabaseObjects\"),global::Seamless.Database.System.DatabaseObjectKind.SystemTable,typeof(global::Seamless.Database.System.Records.DatabaseObject)),");
        foreach(var declaration in declarations) {
            builder.AppendLine($"{indent}                new(new({Literal(declaration.Name)}),global::Seamless.Database.System.DatabaseObjectKind.{declaration.Kind},typeof({declaration.RecordTypeName})),");
        }
        builder.AppendLine($"{indent}            ],");
        builder.AppendLine($"{indent}            static record=>record.Key);");
        builder.AppendLine($"{indent}    public override global::Seamless.Database.System.SystemTable<global::Seamless.Database.System.Keys.DatabaseObject,global::Seamless.Database.System.Records.DatabaseObject> DatabaseObjects=>GeneratedDatabaseObjects;");

        var tables = declarations.Where(static declaration => declaration.ImportKind is not null).ToImmutableArray();
        builder.AppendLine($"{indent}    public void EnsureTablesCreated() {{");
        foreach(var table in tables) {
            builder.AppendLine($"{indent}        _=this.{MemberAccess(table.Name)};");
        }
        builder.AppendLine($"{indent}    }}");

        builder.AppendLine($"{indent}    public void ImportTables(global::Seamless.Database.IDatabaseImportSource source) {{");
        foreach(var table in tables) {
            builder.AppendLine($"{indent}        foreach(var row in source.ReadTable<{table.RecordTypeName}>(new global::Seamless.Database.System.Keys.DatabaseObject({Literal(table.Name)}))) {{");
            if(table.ImportKind=="Table") {
                builder.AppendLine($"{indent}            _=this.{MemberAccess(table.Name)}.Add(row);");
            } else {
                builder.AppendLine($"{indent}            _=AddMaterializedViewRow(this.{MemberAccess(table.Name)},row);");
            }
            builder.AppendLine($"{indent}        }}");
        }
        builder.AppendLine($"{indent}    }}");
        AppendTransactions(builder,indent,className,declarations);
        builder.AppendLine($"{indent}}}");

        if(namespaceName.Length>0) {
            builder.AppendLine("}");
        }
        return builder.ToString();
    }

    private static string NamespaceName(INamespaceSymbol symbol)=>
        symbol.IsGlobalNamespace ? string.Empty : symbol.ToDisplayString();

    private static string Identifier(string name)=>
        SyntaxFacts.GetKeywordKind(name)==SyntaxKind.None ? name : $"@{name}";

    private static string MemberAccess(string path)=>
        string.Join(".",path.Split('.').Select(Identifier));

    private static string Literal(string value)=>
        Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(value,quote: true);

    private sealed class TypeTarget(INamedTypeSymbol symbol,bool isPartial) {
        public INamedTypeSymbol Symbol { get; }=symbol;
        public bool IsPartial { get; }=isPartial;
    }

    private sealed class ObjectDeclaration(
        string kind,string name,string recordTypeName,string? importKind,Location location,string? keyTypeName) {
        public string Kind { get; }=kind;
        public string Name { get; }=name;
        public string RecordTypeName { get; }=recordTypeName;
        public string? ImportKind { get; }=importKind;
        public Location Location { get; }=location;
        public string? KeyTypeName{get;}=keyTypeName;
    }
    private static void AppendTransactions(StringBuilder builder,string indent,string className,ImmutableArray<ObjectDeclaration> declarations){
        var tables=declarations.Where(static d=>d.ImportKind is "Table" or "MaterializedView").ToArray();
        builder.AppendLine($"{indent}    protected override void EnsureTransactionSchema()=>this.EnsureTablesCreated();");
        builder.Append($"{indent}    protected override string TransactionCatalogIdentity=>{Literal(className+"|typed-v2")}");
        foreach(var table in tables){builder.Append($"+\"|\"+this.TransactionCodecIdentity<{table.KeyTypeName??table.RecordTypeName},{table.RecordTypeName}>({Literal(table.Name)})");}
        builder.AppendLine(";");
        builder.AppendLine($"{indent}    public global::System.Threading.Tasks.ValueTask<TypedTransaction> BeginTransactionAsync(global::System.Threading.CancellationToken cancellationToken=default){{");
        builder.AppendLine($"{indent}        var operation=this.BeginTransactionScopeAsync(cancellationToken);");
        builder.AppendLine($"{indent}        return Wrap(operation);");
        builder.AppendLine($"{indent}        async global::System.Threading.Tasks.ValueTask<TypedTransaction> Wrap(global::System.Threading.Tasks.ValueTask<global::Seamless.Transactions.TransactionScope> pending)=>new(this,await pending.ConfigureAwait(false));");
        builder.AppendLine($"{indent}    }}");
        builder.AppendLine($"{indent}    public sealed class TypedTransaction:global::System.IAsyncDisposable{{");
        builder.AppendLine($"{indent}        private readonly {className} owner;");
        builder.AppendLine($"{indent}        private readonly global::Seamless.Transactions.TransactionScope scope;");
        builder.AppendLine($"{indent}        internal TypedTransaction({className} owner,global::Seamless.Transactions.TransactionScope scope){{this.owner=owner;this.scope=scope;}}");
        builder.AppendLine($"{indent}        public global::System.Guid Id=>this.scope.Id;");
        builder.AppendLine($"{indent}        public global::System.Threading.Tasks.ValueTask<TypedTransaction> BeginTransactionAsync()=>global::System.Threading.Tasks.ValueTask.FromResult(new TypedTransaction(this.owner,this.scope.BeginNested()));");
        builder.AppendLine($"{indent}        public global::System.Threading.Tasks.ValueTask<global::Seamless.Transactions.TransactionCommitResult> CommitAsync()=>this.scope.CommitAsync();");
        builder.AppendLine($"{indent}        public global::System.Threading.Tasks.ValueTask RollbackAsync()=>this.scope.RollbackAsync();");
        builder.AppendLine($"{indent}        public global::System.Threading.Tasks.ValueTask DisposeAsync()=>this.scope.DisposeAsync();");
        var number=0;
        AppendTransactionProperties(builder,indent+"        ",className,tables,"",ref number);
        builder.AppendLine($"{indent}    }}");
    }
    private static void AppendTransactionProperties(StringBuilder builder,string indent,string className,ObjectDeclaration[] tables,string prefix,ref int number){
        foreach(var table in tables.Where(t=>!t.Name[prefix.Length..].Contains('.'))){
            var name=Identifier(table.Name[prefix.Length..]);
            var collection="TransactionCollection";if(table.ImportKind=="MaterializedView"){collection="ReadOnlyTransactionCollection";}
            builder.AppendLine($"{indent}public global::Seamless.Transactions.Collections.{collection}<{table.KeyTypeName??table.RecordTypeName},{table.RecordTypeName}> {name}=>this.owner.GetTransactionCollection(this.scope,this.owner.{MemberAccess(table.Name)});");
        }
        foreach(var group in tables.Where(t=>t.Name[prefix.Length..].Contains('.')).GroupBy(t=>t.Name[prefix.Length..].Split('.')[0],StringComparer.Ordinal)){
            var type="SchemaView"+(number++).ToString(global::System.Globalization.CultureInfo.InvariantCulture);
            builder.AppendLine($"{indent}public {type} {Identifier(group.Key)}=>new(this.owner,this.scope);");
            builder.AppendLine($"{indent}public sealed class {type}{{");
            builder.AppendLine($"{indent}    private readonly {className} owner;");builder.AppendLine($"{indent}    private readonly global::Seamless.Transactions.TransactionScope scope;");
            builder.AppendLine($"{indent}    internal {type}({className} owner,global::Seamless.Transactions.TransactionScope scope){{this.owner=owner;this.scope=scope;}}");
            AppendTransactionProperties(builder,indent+"    ",className,group.ToArray(),prefix+group.Key+".",ref number);
            builder.AppendLine($"{indent}}}");
        }
    }
}
