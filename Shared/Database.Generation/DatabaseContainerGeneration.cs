using static Shared.Database.Generation.DatabaseGenerationHelpers;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Shared.Database.Generation;

internal sealed class DatabaseContainerGeneration:IIncrementalGenerator {
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
        messageFormat: "Database object name '{0}' conflicts with a generated or inherited member",
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
        return new(symbol,declaration.Modifiers.Any(SyntaxKind.PartialKeyword)){ViewRow=GeneratedViewRow.TryCreate(declaration,symbol,context.SemanticModel)};
    }

    private static void Generate(SourceProductionContext context,ImmutableArray<TypeTarget> targets) {
        if(targets.IsDefaultOrEmpty) {
            return;
        }

        var uniqueTypes = targets
            .GroupBy(static target => target.Symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),StringComparer.Ordinal)
            .Select(static group => new TypeTarget(group.First().Symbol,group.Any(static target => target.IsPartial)){ViewRow=group.Select(static target=>target.ViewRow).FirstOrDefault(static row=>row is not null)})
            .ToArray();
        var markerNamespaces = new HashSet<string>(
            uniqueTypes
                .Where(static target => target.Symbol.Name=="DatabaseGenerator")
                .Select(static target => NamespaceName(target.Symbol.ContainingNamespace)),
            StringComparer.Ordinal);
        var declaredRoots = uniqueTypes
            .Select(static target => NamespaceName(target.Symbol.ContainingNamespace))
            .Where(static name => name.Contains("Tables") || name.Contains("Views") || name.Contains("Work"))
            .Select(static name => {
                foreach (var kind in new[] { ".Tables", ".Views", ".Work" }) {
                    var idx = name.IndexOf(kind, StringComparison.Ordinal);
                    if (idx >= 0) return name.Substring(0, idx);
                }
                foreach (var kind in new[] { "Tables", "Views", "Work" }) {
                    if (name == kind || name.StartsWith(kind + ".", StringComparison.Ordinal)) return string.Empty;
                }
                return name;
            })
            .Distinct(StringComparer.Ordinal);
        foreach(var root in declaredRoots){markerNamespaces.Add(root);}
        if(markerNamespaces.Count==0) {
            return;
        }
        // [ビュー/生成] Query本体から推論した行型とRowsを出力する。推論できない形は元の本文位置へ診断する。
        foreach(var target in uniqueTypes.Where(static target=>target.ViewRow is not null)){
            var row=target.ViewRow!;
            if(row.Source is null){context.ReportDiagnostic(Diagnostic.Create(GeneratedViewRow.CannotInfer,row.Location,target.Symbol.Name,row.Failure));continue;}
            context.AddSource($"{target.Symbol.ToDisplayString()}.ViewRow.g.cs",SourceText.From(row.Source,Encoding.UTF8));
        }

        foreach(var target in uniqueTypes
            .Where(static target => IsContainer(target))
            .OrderBy(static target => target.Symbol.ToDisplayString(),StringComparer.Ordinal)) {
            var namespaceName = NamespaceName(target.Symbol.ContainingNamespace);
            if(!markerNamespaces.Contains(namespaceName)) {
                continue;
            }

            var declarations = CollectObjects(target.Symbol);
            var declaredObjects = CollectDeclaredObjects(uniqueTypes, namespaceName);
            var combined = declarations.Concat(declaredObjects).ToImmutableArray();
            var hasErrors = ReportDiagnostics(context,combined);
            var schemaNames=combined.Where(static item=>item.Name.Contains('.')).Select(static item=>item.Name.Substring(0,item.Name.IndexOf('.')))
                .Concat(target.Symbol.GetMembers().OfType<IPropertySymbol>().Where(static property=>property.Type is INamedTypeSymbol type&&IsSchema(type)).Select(static property=>property.Name))
                .Distinct(StringComparer.Ordinal).ToArray();
            foreach(var item in combined.Where(item=>schemaNames.Contains(item.Name,StringComparer.Ordinal))){
                context.ReportDiagnostic(Diagnostic.Create(DuplicateName,item.Location,item.Name));hasErrors=true;
            }
            foreach(var item in declaredObjects){
                var name=item.Name.Substring(item.Name.LastIndexOf('.')+1);
                var reserved=item.Name.Contains('.')&&name=="Database";
                if(!item.Name.Contains('.')){
                    reserved=name is "EnsureTablesCreated" or "ImportTables" or "ImportTablesAsync" or "__ImportTablesInTransaction"
                        ||schemaNames.Any(schema=>schema+"Schema"==name);
                    for(var type=target.Symbol.BaseType;type is not null;type=type.BaseType){
                        reserved|=type.GetMembers(name).Any(static member=>member.DeclaredAccessibility is Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal);
                    }
                }
                if(reserved){context.ReportDiagnostic(Diagnostic.Create(ReservedName,item.Location,item.Name));hasErrors=true;}
            }
            if(hasErrors) {
                continue;
            }

            var source = BuildSource(target.Symbol,combined,declaredObjects);
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

    private static ImmutableArray<ObjectDeclaration> CollectDeclaredObjects(TypeTarget[] targets,string root){
        var builder = ImmutableArray.CreateBuilder<ObjectDeclaration>();
        var tablePrefix = root.Length == 0 ? "Tables" : root + ".Tables";
        var viewPrefix = root.Length == 0 ? "Views" : root + ".Views";
        var workPrefix = root.Length == 0 ? "Work" : root + ".Work";

        foreach(var target in targets.Where(static t => t.Symbol.TypeKind == TypeKind.Struct && t.Symbol.ContainingType is null)){
            var symbol = target.Symbol;
            var ns = NamespaceName(symbol.ContainingNamespace);
            string? kind = null;
            string? schema = null;
            string? keyNs = null;

            if(ns == tablePrefix || ns.StartsWith(tablePrefix + ".", StringComparison.Ordinal)){
                kind = "Table";
                schema = ns == tablePrefix ? "" : ns.Substring(tablePrefix.Length + 1);
                keyNs = (root.Length == 0 ? "Keys" : root + ".Keys") + (schema.Length == 0 ? "" : "." + schema);
            } else if(ns == viewPrefix || ns.StartsWith(viewPrefix + ".", StringComparison.Ordinal)){
                kind = "View";
                schema = ns == viewPrefix ? "" : ns.Substring(viewPrefix.Length + 1);
                keyNs = (root.Length == 0 ? "Keys" : root + ".Keys") + (schema.Length == 0 ? "" : "." + schema);
            } else if(ns == workPrefix || ns.StartsWith(workPrefix + ".", StringComparison.Ordinal)){
                kind = "Table";
                schema = ns == workPrefix ? "" : ns.Substring(workPrefix.Length + 1);
                keyNs = (root.Length == 0 ? "Keys" : root + ".Keys") + (schema.Length == 0 ? "" : "." + schema);
            }

            if(kind is not null && keyNs is not null){
                var name = string.IsNullOrEmpty(schema) ? symbol.Name : $"{schema}.{symbol.Name}";
                var recordTypeName = "global::" + symbol.ToDisplayString();
                var keyTypeName = "global::" + keyNs + ".@" + symbol.Name;
                string? importKind=null;
                if(kind!="View"){importKind="Table";}
                builder.Add(new ObjectDeclaration(kind, name, recordTypeName, importKind, symbol.Locations.FirstOrDefault() ?? Location.None, keyTypeName){ViewMethod=target.ViewRow?.Source is null?"Query":GeneratedViewRow.RowsMethodName});
            }
        }
        return builder.ToImmutable();
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
                // [ビュー/書き方] ビューは Views 名前空間の partial 行型の Query だけで宣言する(CollectDeclaredObjects)。
                // コンテナのメンバーとして書く View<T>/ISet<T> の形は廃止した。
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
            &&(named.OriginalDefinition.ContainingNamespace.ToDisplayString()==DatabaseNamespace&&genericName!="ISet"
                ||named.OriginalDefinition.ContainingNamespace.ToDisplayString()=="Seamless.Collections"&&genericName=="ISet")
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

    private static string BuildSource(INamedTypeSymbol symbol,ImmutableArray<ObjectDeclaration> declarations,ImmutableArray<ObjectDeclaration> declaredObjects) {
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
        foreach(var group in declaredObjects.Where(static d => d.Name.Contains('.')).GroupBy(static d => d.Name.Split('.')[0], StringComparer.Ordinal)) {
            var schemaName = group.Key;
            if(symbol.GetMembers(schemaName).OfType<IPropertySymbol>().Any(static property=>property.Type is INamedTypeSymbol type&&IsSchema(type))){continue;}
            var schemaClass = Identifier(schemaName) + "Schema";
            builder.AppendLine($"{indent}    public sealed class {schemaClass}({className} database):global::Seamless.Database.DatabaseSchema(database,{Literal(schemaName)}) {{");
            builder.AppendLine($"{indent}        public new {className} Database => ({className})base.Database;");
            foreach(var item in group) {
                var propName = Identifier(item.Name.Substring(schemaName.Length + 1));
                if(item.Kind == "View") {
                    builder.AppendLine($"{indent}        public global::Seamless.Collections.ISet<{item.RecordTypeName}> {propName} => {item.RecordTypeName}.{item.ViewMethod}(this.Database);");
                } else {
                    builder.AppendLine($"{indent}        public global::Seamless.Database.Table<{item.KeyTypeName}, {item.RecordTypeName}> {propName} => this.Table<{item.KeyTypeName}, {item.RecordTypeName}>(static row=>row.Key);");
                }
            }
            builder.AppendLine($"{indent}    }}");
            builder.AppendLine($"{indent}    public {schemaClass} {Identifier(schemaName)} => new(this);");
        }
        foreach(var item in declaredObjects.Where(static d => !d.Name.Contains('.'))) {
            var propName = Identifier(item.Name);
            if(item.Kind == "View") {
                builder.AppendLine($"{indent}    public global::Seamless.Collections.ISet<{item.RecordTypeName}> {propName} => {item.RecordTypeName}.{item.ViewMethod}(this);");
            } else {
                builder.AppendLine($"{indent}    public global::Seamless.Database.Table<{item.KeyTypeName}, {item.RecordTypeName}> {propName} => this.Table<{item.KeyTypeName}, {item.RecordTypeName}>(static row=>row.Key);");
            }
        }
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
        builder.AppendLine($"{indent}        // [同期取込/互換性] 既存の同期writer内では従来の変更集合へ追加する。取引外のSharedV2だけが取込全体の取引を開始する。");
        builder.AppendLine($"{indent}        if(this.Options.GatewayProfile is null&&!this.IsLegacyWriteTransactionActive){{this.ImportTablesAsync(source).GetAwaiter().GetResult();return;}}");
        builder.AppendLine($"{indent}        using var relationBatch=this.BeginRelationImport();");
        foreach(var table in tables) {
            builder.AppendLine($"{indent}        foreach(var row in source.ReadTable<{table.RecordTypeName}>(new global::Seamless.Database.System.Keys.DatabaseObject({Literal(table.Name)}))) {{");
            if(table.ImportKind=="Table") {
                builder.AppendLine($"{indent}            _=this.{MemberAccess(table.Name)}.Add(row);");
            } else {
                builder.AppendLine($"{indent}            _=AddMaterializedViewRow(this.{MemberAccess(table.Name)},row);");
            }
            builder.AppendLine($"{indent}        }}");
        }
        builder.AppendLine($"{indent}        relationBatch?.Commit();");
        builder.AppendLine($"{indent}    }}");
        builder.AppendLine($"{indent}    // [取込/原子性] 全表と実体化ビューを同じ取引へ追加。入力を逐次読み、例外/取消時は全体を取消す。");
        builder.AppendLine($"{indent}    public async global::System.Threading.Tasks.Task ImportTablesAsync(global::Seamless.Database.IDatabaseImportSource source,global::System.Threading.CancellationToken cancellationToken=default){{");
        builder.AppendLine($"{indent}        global::System.ArgumentNullException.ThrowIfNull(source);cancellationToken.ThrowIfCancellationRequested();");
        builder.AppendLine($"{indent}        // [寿命/同期] DB入口は独立root。既存取引へ合成するときはTypedTransaction.ImportTablesAsyncを使う。");
        builder.AppendLine($"{indent}        await using var scope=await this.BeginTransactionScopeAsync(cancellationToken);");
        builder.AppendLine($"{indent}        this.__ImportTablesInTransaction(scope,source,cancellationToken);");
        builder.AppendLine($"{indent}        await scope.CommitAsync(global::System.ReadOnlyMemory<byte>.Empty,cancellationToken);");
        builder.AppendLine($"{indent}    }}");
        builder.AppendLine($"{indent}    private void __ImportTablesInTransaction(global::Seamless.Transactions.TransactionScope scope,global::Seamless.Database.IDatabaseImportSource source,global::System.Threading.CancellationToken cancellationToken){{");
        for(var index=0;index<tables.Length;index++){
            var table=tables[index];var getter="GetTransactionCollection";if(table.ImportKind!="Table"){getter="GetMaterializedViewImportCollection";}
            builder.AppendLine($"{indent}        var import{index}=this.{getter}(scope,this.{MemberAccess(table.Name)});");
            builder.AppendLine($"{indent}        // [資源] 入力を全件保持せず、私有変更の予算/退避を共通runtimeへ委譲。既存キーはAddと同じ扱い。");
            builder.AppendLine($"{indent}        foreach(var row in source.ReadTable<{table.RecordTypeName}>(new global::Seamless.Database.System.Keys.DatabaseObject({Literal(table.Name)}))){{cancellationToken.ThrowIfCancellationRequested();_=import{index}.Add(row);}}");
        }
        builder.AppendLine($"{indent}    }}");
        AppendTransactions(builder,indent,className,declarations);
        builder.AppendLine($"{indent}}}");

        if(namespaceName.Length>0) {
            builder.AppendLine("}");
        }
        foreach(var group in declaredObjects.Where(static item=>item.Name.Contains('.')).GroupBy(static item=>item.Name.Split('.')[0],StringComparer.Ordinal)){
            if(symbol.GetMembers(group.Key).OfType<IPropertySymbol>().SingleOrDefault()?.Type is not INamedTypeSymbol schema||!IsSchema(schema)){continue;}
            var schemaNamespace=NamespaceName(schema.ContainingNamespace);
            if(schemaNamespace.Length>0){builder.AppendLine($"namespace {schemaNamespace}{{");}
            builder.AppendLine($"partial class {Identifier(schema.Name)}{{");
            foreach(var item in group){
                var propertyName=Identifier(item.Name.Substring(group.Key.Length+1));
                if(item.Kind=="View"){
                    builder.AppendLine($"public global::Seamless.Collections.ISet<{item.RecordTypeName}> {propertyName}=>{item.RecordTypeName}.{item.ViewMethod}((global::{symbol.ToDisplayString()})this.Database);");
                }else{
                    builder.AppendLine($"public global::Seamless.Database.Table<{item.KeyTypeName},{item.RecordTypeName}> {propertyName}=>this.Table<{item.KeyTypeName},{item.RecordTypeName}>(static row=>row.Key);");
                }
            }
            builder.AppendLine("}");
            if(schemaNamespace.Length>0){builder.AppendLine("}");}
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
        public GeneratedViewRow? ViewRow{get;init;}
    }

    private sealed class ObjectDeclaration(
        string kind,string name,string recordTypeName,string? importKind,Location location,string? keyTypeName) {
        public string Kind { get; }=kind;
        public string Name { get; }=name;
        public string RecordTypeName { get; }=recordTypeName;
        public string? ImportKind { get; }=importKind;
        public Location Location { get; }=location;
        public string? KeyTypeName{get;}=keyTypeName;
        /// <summary>ビューのgetterが呼ぶ静的メソッド。行型を生成したビューはRows、それ以外はQuery。</summary>
        public string ViewMethod{get;init;}="Query";
    }
    private static void AppendTransactions(StringBuilder builder,string indent,string className,ImmutableArray<ObjectDeclaration> declarations){
        var tables=declarations.Where(static d=>d.ImportKind is "Table" or "MaterializedView").ToArray();
        builder.AppendLine($"{indent}    protected override void EnsureTransactionSchema()=>this.EnsureTablesCreated();");
        builder.Append($"{indent}    protected override string TransactionCatalogIdentity=>{Literal(className+"|typed-v2")}");
        foreach(var table in tables){builder.Append($"+\"|\"+this.TransactionCodecIdentity<{table.KeyTypeName??table.RecordTypeName},{table.RecordTypeName}>({Literal(table.Name)})");}
        builder.AppendLine(";");
        AppendTransactionEntry(builder,indent,"BeginTransactionAsync","BeginTransactionScopeAsync");
        AppendTransactionEntry(builder,indent,"BeginReadOnlyTransactionAsync","BeginReadOnlyTransactionScopeAsync");
        builder.AppendLine($"{indent}    // [入口] 読み書きの取引(同期)。usingで囲み、Commitを呼ばずに抜けると取り消す。基底の暗黙の取引(DatabaseTransaction)を隠す。");
        builder.AppendLine($"{indent}    public new TypedTransaction BeginTransaction(global::System.Threading.CancellationToken cancellationToken=default)=>new(this,this.BeginTransactionScopeAsync(cancellationToken).AsTask().GetAwaiter().GetResult());");
        AppendReaders(builder,indent,className,tables);
        builder.AppendLine("#if DEBUG");
        builder.AppendLine($"{indent}    [global::System.Diagnostics.DebuggerDisplayAttribute(\"Id={{Id}}\")]");
        builder.AppendLine("#endif");
        builder.AppendLine($"{indent}    public sealed class TypedTransaction:IReader,global::System.IAsyncDisposable,global::System.IDisposable{{");
        builder.AppendLine($"{indent}        // [所有権] owner/scopeを同じ取引の全表ビューで共有。表ごとに独立COMMITや別snapshotを作らない。");
        builder.AppendLine($"{indent}        private readonly {className} owner;");
        builder.AppendLine($"{indent}        private readonly global::Seamless.Transactions.TransactionScope scope;");
        builder.AppendLine($"{indent}        internal TypedTransaction({className} owner,global::Seamless.Transactions.TransactionScope scope){{this.owner=owner;this.scope=scope;}}");
        builder.AppendLine($"{indent}        // [区分] 開始時に確定したreadonly区分。読取操作だけを行った通常取引をreadonlyへ昇格/変更しない。");
        builder.AppendLine($"{indent}        public bool IsReadOnly=>this.scope.IsReadOnly;");
        builder.AppendLine($"{indent}        public global::System.Guid Id=>this.scope.Id;");
        builder.AppendLine($"{indent}        // [ネスト] 同じID/読取区分/snapshotへ合流。子のCommitは投票であり、WAL投入・設置・公開を行わない。");
        builder.AppendLine($"{indent}        public global::System.Threading.Tasks.ValueTask<TypedTransaction> BeginTransactionAsync()=>global::System.Threading.Tasks.ValueTask.FromResult(new TypedTransaction(this.owner,this.scope.BeginNested()));");
        builder.AppendLine($"{indent}        public TypedTransaction BeginTransaction()=>new(this.owner,this.scope.BeginNested());");
        builder.AppendLine($"{indent}        // [取込/合流] 呼出し元の明示取引へ子として合流。入力失敗/取消は全体を取消し、ここでは公開しない。");
        builder.AppendLine($"{indent}        public async global::System.Threading.Tasks.Task ImportTablesAsync(global::Seamless.Database.IDatabaseImportSource source,global::System.Threading.CancellationToken cancellationToken=default){{");
        builder.AppendLine($"{indent}            global::System.ArgumentNullException.ThrowIfNull(source);cancellationToken.ThrowIfCancellationRequested();await using var child=await this.BeginTransactionAsync();");
        builder.AppendLine($"{indent}            this.owner.__ImportTablesInTransaction(child.scope,source,cancellationToken);await child.CommitAsync(cancellationToken);");
        builder.AppendLine($"{indent}        }}");
        builder.AppendLine($"{indent}        // [確定キュー/同期] 共通coordinatorへ委譲。版付きrootは依存検証・予約・順序受付・全参加先設置・prefix公開を行い、永続構成だけWAL耐久化を待つ。");
        builder.AppendLine($"{indent}        public global::System.Threading.Tasks.ValueTask<global::Seamless.Transactions.TransactionCommitResult> CommitAsync()=>this.scope.CommitAsync();");
        builder.AppendLine($"{indent}        // [取消境界] 受付前の取消は中止。版付き取引ではWAL受付後に取消されても確定結果を撤回せず、業務処理を自動再実行しない。");
        builder.AppendLine($"{indent}        public global::System.Threading.Tasks.ValueTask<global::Seamless.Transactions.TransactionCommitResult> CommitAsync(global::System.Threading.CancellationToken cancellationToken)=>this.scope.CommitAsync(global::System.ReadOnlyMemory<byte>.Empty,cancellationToken);");
        builder.AppendLine($"{indent}        // [取消/所有権] 私有変更を破棄。子の取消はroot全体をrollback-onlyにする。");
        builder.AppendLine($"{indent}        public global::System.Threading.Tasks.ValueTask RollbackAsync()=>this.scope.RollbackAsync();");
        builder.AppendLine($"{indent}        // [解放] 未確定なら取消し、scopeが走査保護・バッファ・私有変更を一度だけ解放する。確定済みデータは取り消さない。");
        builder.AppendLine($"{indent}        public global::System.Threading.Tasks.ValueTask DisposeAsync()=>this.scope.DisposeAsync();");
        builder.AppendLine($"{indent}        // [同期入口] using/Commitを同期で書くための形。確定・解放の処理は非同期版と同じscopeへ委譲する。");
        builder.AppendLine($"{indent}        public global::Seamless.Transactions.TransactionCommitResult Commit()=>this.scope.CommitAsync().AsTask().GetAwaiter().GetResult();");
        builder.AppendLine($"{indent}        // [確定の付随情報] TransactionMetadataConsumerを設定したDBで、確定と同じWALの記録へ付随情報を残す。外側の取引だけが指定できる。");
        builder.AppendLine($"{indent}        public global::Seamless.Transactions.TransactionCommitResult Commit(global::System.ReadOnlyMemory<byte> metadata)=>this.scope.CommitAsync(metadata).AsTask().GetAwaiter().GetResult();");
        builder.AppendLine($"{indent}        public void Rollback()=>this.scope.RollbackAsync().AsTask().GetAwaiter().GetResult();");
        builder.AppendLine($"{indent}        public void Dispose()=>this.scope.DisposeAsync().AsTask().GetAwaiter().GetResult();");
        var number=0;
        AppendTransactionProperties(builder,indent+"        ",className,tables,"",ref number);
        builder.AppendLine($"{indent}    }}");
    }
    private static void AppendTransactionEntry(StringBuilder builder,string indent,string method,string begin){
        var access="更新可能。静的解析で不明な呼出しが残る場合も、この契約を保持";
        if(method=="BeginReadOnlyTransactionAsync"){access="読取専用。書込みを実行時にも拒否し、履歴予算圧迫時の中止候補になる";}
        builder.AppendLine($"{indent}    // [アクセス] {access}。生成された呼出し側のコメントに、取引範囲の解析結果を記録する。");
        builder.AppendLine($"{indent}    // [並行性] root同士はOptions.TransactionConcurrencyに従う。一つの取引への同時公開操作と、一query内部のworker分割は別の指定。");
        builder.AppendLine($"{indent}    public global::System.Threading.Tasks.ValueTask<TypedTransaction> {method}(global::System.Threading.CancellationToken cancellationToken=default){{");
        builder.AppendLine($"{indent}        // [同期/キュー] 非asyncの入口でAsyncLocal holderを設定。共通層が排他gateまたはsnapshot登録を行い、回収中は入場を待つ。");
        builder.AppendLine($"{indent}        var operation=this.{begin}(cancellationToken);");
        builder.AppendLine($"{indent}        // [寿命] 待機結果を型付きビューへ包む。表のhandleやnative epochをこの待機のために保持しない。");
        builder.AppendLine($"{indent}        return Wrap(operation);");
        builder.AppendLine($"{indent}        // [資源] scopeの所有権だけをTypedTransactionへ移す。Dispose/Commit/Rollbackは共通scopeの寿命管理へ戻す。");
        builder.AppendLine($"{indent}        async global::System.Threading.Tasks.ValueTask<TypedTransaction> Wrap(global::System.Threading.Tasks.ValueTask<global::Seamless.Transactions.TransactionScope> pending)=>new(this,await pending.ConfigureAwait(false));");
        builder.AppendLine($"{indent}    }}");
    }
    // 読み書きの入口が共有する読み取りの形。表と実体化ビューを読取専用の集合として持つ。スキーマは入れ子のインターフェース。
    private static string ReaderInterface(string prefix)=>prefix.Length==0?"IReader":"I"+string.Concat(prefix.TrimEnd('.').Split('.'))+"Reader";
    private static void AppendReaderInterfaces(StringBuilder builder,string indent,ObjectDeclaration[] tables,string prefix){
        builder.AppendLine($"{indent}    public interface {ReaderInterface(prefix)}{{");
        foreach(var table in tables.Where(t=>!t.Name.Substring(prefix.Length).Contains('.'))){
            builder.AppendLine($"{indent}        global::Seamless.Collections.ISet<{table.RecordTypeName}> {Identifier(table.Name.Substring(prefix.Length))}{{get;}}");
        }
        var groups=tables.Where(t=>t.Name.Substring(prefix.Length).Contains('.')).GroupBy(t=>t.Name.Substring(prefix.Length).Split('.')[0],StringComparer.Ordinal).ToArray();
        foreach(var group in groups){
            builder.AppendLine($"{indent}        {ReaderInterface(prefix+group.Key+".")} {Identifier(group.Key)}{{get;}}");
        }
        builder.AppendLine($"{indent}    }}");
        foreach(var group in groups){AppendReaderInterfaces(builder,indent,group.ToArray(),prefix+group.Key+".");}
    }
    private static void AppendReaders(StringBuilder builder,string indent,string className,ObjectDeclaration[] tables){
        AppendReaderInterfaces(builder,indent,tables,"");
        builder.AppendLine($"{indent}    // [入口] 読み込み専用。開始時点の確定済みの内容(スナップショット)に固定し、書き込みの入口を持たない。");
        builder.AppendLine($"{indent}    public Reader BeginRead(global::System.Threading.CancellationToken cancellationToken=default)=>new(this,this.BeginReadOnlyTransactionScopeAsync(cancellationToken).AsTask().GetAwaiter().GetResult());");
        builder.AppendLine($"{indent}    public async global::System.Threading.Tasks.ValueTask<Reader> BeginReadAsync(global::System.Threading.CancellationToken cancellationToken=default)=>new(this,await this.BeginReadOnlyTransactionScopeAsync(cancellationToken).ConfigureAwait(false));");
        builder.AppendLine($"{indent}    public sealed class Reader:IReader,global::System.IAsyncDisposable,global::System.IDisposable{{");
        builder.AppendLine($"{indent}        private readonly {className} owner;");
        builder.AppendLine($"{indent}        private readonly global::Seamless.Transactions.TransactionScope scope;");
        builder.AppendLine($"{indent}        internal Reader({className} owner,global::Seamless.Transactions.TransactionScope scope){{this.owner=owner;this.scope=scope;}}");
        builder.AppendLine($"{indent}        public global::System.Threading.Tasks.ValueTask DisposeAsync()=>this.scope.DisposeAsync();");
        builder.AppendLine($"{indent}        public void Dispose()=>this.scope.DisposeAsync().AsTask().GetAwaiter().GetResult();");
        var number=0;
        AppendReaderProperties(builder,indent+"        ",className,tables,"",ref number);
        builder.AppendLine($"{indent}    }}");
    }
    private static void AppendReaderProperties(StringBuilder builder,string indent,string className,ObjectDeclaration[] tables,string prefix,ref int number){
        foreach(var table in tables.Where(t=>!t.Name.Substring(prefix.Length).Contains('.'))){
            var name=Identifier(table.Name.Substring(prefix.Length));
            var key=table.KeyTypeName??table.RecordTypeName;
            var collection=$"this.owner.GetTransactionCollection(this.scope,this.owner.{MemberAccess(table.Name)})";
            if(table.ImportKind=="Table"){collection=$"new global::Seamless.Transactions.Collections.ReadOnlyTransactionCollection<{key},{table.RecordTypeName}>({collection})";}
            builder.AppendLine($"{indent}public global::Seamless.Transactions.Collections.ReadOnlyTransactionCollection<{key},{table.RecordTypeName}> {name}=>{collection};");
            builder.AppendLine($"{indent}global::Seamless.Collections.ISet<{table.RecordTypeName}> {ReaderInterface(prefix)}.{name}=>this.{name};");
        }
        foreach(var group in tables.Where(t=>t.Name.Substring(prefix.Length).Contains('.')).GroupBy(t=>t.Name.Substring(prefix.Length).Split('.')[0],StringComparer.Ordinal)){
            var type="SchemaReader"+(number++).ToString(global::System.Globalization.CultureInfo.InvariantCulture);
            var schemaPrefix=prefix+group.Key+".";
            builder.AppendLine($"{indent}public {type} {Identifier(group.Key)}=>new(this.owner,this.scope);");
            builder.AppendLine($"{indent}{ReaderInterface(schemaPrefix)} {ReaderInterface(prefix)}.{Identifier(group.Key)}=>this.{Identifier(group.Key)};");
            builder.AppendLine($"{indent}public sealed class {type}:{ReaderInterface(schemaPrefix)}{{");
            builder.AppendLine($"{indent}    private readonly {className} owner;");builder.AppendLine($"{indent}    private readonly global::Seamless.Transactions.TransactionScope scope;");
            builder.AppendLine($"{indent}    internal {type}({className} owner,global::Seamless.Transactions.TransactionScope scope){{this.owner=owner;this.scope=scope;}}");
            AppendReaderProperties(builder,indent+"    ",className,group.ToArray(),schemaPrefix,ref number);
            builder.AppendLine($"{indent}}}");
        }
    }
    private static void AppendTransactionProperties(StringBuilder builder,string indent,string className,ObjectDeclaration[] tables,string prefix,ref int number){
        foreach(var table in tables.Where(t=>!t.Name.Substring(prefix.Length).Contains('.'))){
            var name=Identifier(table.Name.Substring(prefix.Length));
            var collection="TransactionCollection";if(table.ImportKind=="MaterializedView"){collection="ReadOnlyTransactionCollection";}
            builder.AppendLine($"{indent}// [表アクセス] {table.Name}: 同じscopeの型付きビューを取得。キー・不存在・走査の依存と自己変更の反映は操作時に共通層で扱う。");
            builder.AppendLine($"{indent}public global::Seamless.Transactions.Collections.{collection}<{table.KeyTypeName??table.RecordTypeName},{table.RecordTypeName}> {name}=>this.owner.GetTransactionCollection(this.scope,this.owner.{MemberAccess(table.Name)});");
            var readerView=$"this.{name}";
            if(table.ImportKind=="Table"){readerView=$"new global::Seamless.Transactions.Collections.ReadOnlyTransactionCollection<{table.KeyTypeName??table.RecordTypeName},{table.RecordTypeName}>(this.{name})";}
            builder.AppendLine($"{indent}// [読み取りの形] IReader からは読取専用の集合として見せる。書ける表そのものは集合にしない(FLinqの取引用の経路を保つ)。");
            builder.AppendLine($"{indent}global::Seamless.Collections.ISet<{table.RecordTypeName}> {ReaderInterface(prefix)}.{name}=>{readerView};");
        }
        foreach(var group in tables.Where(t=>t.Name.Substring(prefix.Length).Contains('.')).GroupBy(t=>t.Name.Substring(prefix.Length).Split('.')[0],StringComparer.Ordinal)){
            var type="SchemaView"+(number++).ToString(global::System.Globalization.CultureInfo.InvariantCulture);
            var schemaPrefix=prefix+group.Key+".";
            builder.AppendLine($"{indent}public {type} {Identifier(group.Key)}=>new(this.owner,this.scope);");
            builder.AppendLine($"{indent}{ReaderInterface(schemaPrefix)} {ReaderInterface(prefix)}.{Identifier(group.Key)}=>this.{Identifier(group.Key)};");
            builder.AppendLine($"{indent}public sealed class {type}:{ReaderInterface(schemaPrefix)}{{");
            builder.AppendLine($"{indent}    private readonly {className} owner;");builder.AppendLine($"{indent}    private readonly global::Seamless.Transactions.TransactionScope scope;");
            builder.AppendLine($"{indent}    internal {type}({className} owner,global::Seamless.Transactions.TransactionScope scope){{this.owner=owner;this.scope=scope;}}");
            AppendTransactionProperties(builder,indent+"    ",className,group.ToArray(),schemaPrefix,ref number);
            builder.AppendLine($"{indent}}}");
        }
    }
}
