using static Seamless.SourceGenerator.GeneratorHelpers;

namespace Seamless.Database.SourceGenerator;

/// <summary>
/// DatabaseContainer 派生の partial クラス宣言から、
/// スキーマメタデータ(DatabaseObjects/DatabaseName)、全テーブル実体化(EnsureTablesCreated)、
/// 取り込み配線(ImportTables)を生成する。
/// 宣言の主形式は普通の C# プロパティ/メソッドで、[Database]/[Table]/[View]/[Function] 属性は
/// SQL Server 互換名を補う補助としてだけ使う。
/// </summary>
[Generator]
public sealed class DatabaseContainerGenerator:IIncrementalGenerator {
    private const string DatabaseNamespace = "Seamless.Database";
    private const string ContainerBaseFullName = "Seamless.Database.DatabaseContainer";

    public void Initialize(IncrementalGeneratorInitializationContext context) {
        var containers = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node,_)=>node is ClassDeclarationSyntax{BaseList:not null}declaration&&declaration.Modifiers.Any(SyntaxKind.PartialKeyword),
                static (syntaxContext,_)=>コンテナ対象生成(syntaxContext))
            .Where(static target=>target is not null)
            .Select(static (target,_)=>target!);

        context.RegisterSourceOutput(containers.Collect(),static (productionContext,targets)=>{
            if(targets.IsDefaultOrEmpty) {
                return;
            }
            var emitted = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach(var target in targets) {
                if(!emitted.Add(target.HintName)) {
                    continue;
                }
                productionContext.AddSource(target.HintName,SourceText.From(target.Source,Encoding.UTF8));
            }
        });
    }

    private sealed class コンテナ対象型(string hintName,string source) {
        public string HintName { get; } = hintName;
        public string Source { get; } = source;
    }

    private sealed class オブジェクト宣言(string kind,string memberName,string sqlName,string recordTypeName,bool isTable) {
        public string Kind { get; } = kind;
        public string MemberName { get; } = memberName;
        public string SqlName { get; } = sqlName;
        public string RecordTypeName { get; } = recordTypeName;
        public bool IsTable { get; } = isTable;
    }

    private static コンテナ対象型? コンテナ対象生成(GeneratorSyntaxContext context) {
        var declaration = (ClassDeclarationSyntax)context.Node;
        if(context.SemanticModel.GetDeclaredSymbol(declaration) is not INamedTypeSymbol symbol) {
            return null;
        }
        if(symbol.IsGenericType||symbol.ContainingType is not null) {
            return null;
        }
        if(!コンテナ基底判定(symbol)) {
            return null;
        }

        var databaseName = 属性名取得(symbol.GetAttributes(),"DatabaseAttribute");
        var declarations = オブジェクト宣言収集(symbol);
        var source = コンテナソース構築(symbol,databaseName,declarations);
        var hintName = $"{symbol.ToDisplayString()}.Container.g.cs";
        return new コンテナ対象型(hintName,source);
    }

    private static bool コンテナ基底判定(INamedTypeSymbol symbol) {
        for(var baseType = symbol.BaseType;baseType is not null;baseType=baseType.BaseType) {
            if(baseType.ToDisplayString()==ContainerBaseFullName) {
                return true;
            }
        }
        return false;
    }

    private static string? 属性名取得(ImmutableArray<AttributeData> attributes,string attributeMetadataName) {
        foreach(var attribute in attributes) {
            var attributeClass = attribute.AttributeClass;
            if(attributeClass is null) {
                continue;
            }
            if(attributeClass.Name!=attributeMetadataName) {
                continue;
            }
            if(attributeClass.ContainingNamespace.ToDisplayString()!=DatabaseNamespace) {
                continue;
            }
            if(attribute.ConstructorArguments.Length>0&&attribute.ConstructorArguments[0].Value is string name) {
                return name;
            }
        }
        return null;
    }

    private static ImmutableArray<オブジェクト宣言> オブジェクト宣言収集(INamedTypeSymbol symbol) {
        var builder = ImmutableArray.CreateBuilder<オブジェクト宣言>();
        foreach(var member in symbol.GetMembers()) {
            if(member.IsStatic||member.IsImplicitlyDeclared) {
                continue;
            }
            switch(member) {
                case IPropertySymbol property: {
                    if(データベース型一致(property.Type,"Table",out var tableRecord)) {
                        var sqlName = 属性名取得(property.GetAttributes(),"TableAttribute")??property.Name;
                        builder.Add(new("Table",property.Name,sqlName,tableRecord,isTable:true));
                    }
                    else if(データベース型一致(property.Type,"View",out var viewRecord)) {
                        var sqlName = 属性名取得(property.GetAttributes(),"ViewAttribute")??property.Name;
                        builder.Add(new("View",property.Name,sqlName,viewRecord,isTable:false));
                    }
                    break;
                }
                case IMethodSymbol{MethodKind:MethodKind.Ordinary}method: {
                    if(データベース型一致(method.ReturnType,"View",out var viewRecord)) {
                        var sqlName = 属性名取得(method.GetAttributes(),"ViewAttribute")??method.Name;
                        builder.Add(new("View",method.Name,sqlName,viewRecord,isTable:false));
                    }
                    else if(データベース型一致(method.ReturnType,"TableFunction",out var functionRecord)) {
                        var sqlName = 属性名取得(method.GetAttributes(),"FunctionAttribute")??method.Name;
                        builder.Add(new("TableFunction",method.Name,sqlName,functionRecord,isTable:false));
                    }
                    else if(属性名取得(method.GetAttributes(),"FunctionAttribute") is string scalarSqlName) {
                        builder.Add(new("ScalarFunction",method.Name,scalarSqlName,method.ReturnType.ToDisplayString(完全修飾形式),isTable:false));
                    }
                    break;
                }
            }
        }
        return builder.ToImmutable();
    }

    private static bool データベース型一致(ITypeSymbol type,string genericName,out string recordTypeName) {
        if(type is INamedTypeSymbol{IsGenericType:true}named
            &&named.OriginalDefinition.Name==genericName
            &&named.OriginalDefinition.ContainingNamespace.ToDisplayString()==DatabaseNamespace
            &&named.TypeArguments.Length==1) {
            recordTypeName=named.TypeArguments[0].ToDisplayString(完全修飾形式);
            return true;
        }
        recordTypeName=string.Empty;
        return false;
    }

    private static string コンテナソース構築(INamedTypeSymbol symbol,string? databaseName,ImmutableArray<オブジェクト宣言> declarations) {
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated/>");
        builder.AppendLine("#nullable enable");
        var namespaceName = 名前空間テキスト取得(symbol.ContainingNamespace);
        var indent = string.Empty;
        if(namespaceName.Length>0) {
            builder.AppendLine($"namespace {namespaceName} {{");
            indent="    ";
        }

        builder.AppendLine($"{indent}partial class {symbol.Name} {{");
        builder.AppendLine($"{indent}    private static readonly global::Seamless.Database.DatabaseObjectDeclaration[] GeneratedDatabaseObjects = [");
        foreach(var declaration in declarations) {
            builder.AppendLine($"{indent}        new(global::Seamless.Database.DatabaseObjectKind.{declaration.Kind},\"{declaration.MemberName}\",\"{declaration.SqlName}\",\"{declaration.RecordTypeName.Replace("\"","\\\"")}\"),");
        }
        builder.AppendLine($"{indent}    ];");
        if(databaseName is not null) {
            builder.AppendLine($"{indent}    public override string DatabaseName=>\"{databaseName}\";");
        }
        builder.AppendLine($"{indent}    public override global::System.Collections.Generic.IReadOnlyList<global::Seamless.Database.DatabaseObjectDeclaration> DatabaseObjects=>GeneratedDatabaseObjects;");

        var tables = declarations.Where(static declaration=>declaration.IsTable).ToImmutableArray();
        builder.AppendLine($"{indent}    /// <summary>全テーブル実体を生成する。スキーマ作成に相当する設計時操作。</summary>");
        builder.AppendLine($"{indent}    public void EnsureTablesCreated() {{");
        foreach(var table in tables) {
            builder.AppendLine($"{indent}        _=this.{table.MemberName};");
        }
        builder.AppendLine($"{indent}    }}");

        builder.AppendLine($"{indent}    /// <summary>取り込み元(将来の AdventureWorks2008R2 本物バックアップ等)から全テーブルへ行を投入する。</summary>");
        builder.AppendLine($"{indent}    public void ImportTables(global::Seamless.Database.IDatabaseImportSource source) {{");
        foreach(var table in tables) {
            builder.AppendLine($"{indent}        foreach(var row in source.ReadTable<{table.RecordTypeName}>(\"{table.SqlName}\")) {{");
            builder.AppendLine($"{indent}            this.{table.MemberName}.Add(row);");
            builder.AppendLine($"{indent}        }}");
        }
        builder.AppendLine($"{indent}    }}");
        builder.AppendLine($"{indent}}}");

        if(namespaceName.Length>0) {
            builder.AppendLine("}");
        }
        return builder.ToString();
    }
}
