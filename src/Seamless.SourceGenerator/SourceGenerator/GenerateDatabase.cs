using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Seamless.SourceGenerator;

public sealed partial class SeamlessIncrementalGenerator {
    // DatabaseGenerator を起点にして schema struct を生成する。
    private static string データベースソース構築(ImmutableArray<型対象型> typeTargets) {
        var builder = new StringBuilder();
        var rootNamespaces = typeTargets
            .Where(static target => target.Symbol.Name=="DatabaseGenerator")
            .Select(static target => 名前空間テキスト取得(target.Symbol.ContainingNamespace))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if(rootNamespaces.Length==0) {
            return string.Empty;
        }
        builder.AppendLine("#pragma warning disable CS0164, CS8601, CS8981");

        foreach(var rootNamespace in rootNamespaces) {
            var keys = typeTargets
                .Where(target => target.IsPartial
                    &&target.Symbol.TypeKind==TypeKind.Struct
                    &&名前空間一致判定(target.Symbol.ContainingNamespace.ToDisplayString(),rootNamespace,"Keys"))
                .Select(target => target.Symbol)
                .GroupBy(static symbol => symbol.ToDisplayString(完全修飾形式),StringComparer.Ordinal)
                .Select(static group => group.First())
                .OrderBy(static symbol => symbol.Name,StringComparer.Ordinal)
                .ToArray();
            var types = typeTargets
                .Where(target => target.IsPartial
                    &&target.Symbol.TypeKind==TypeKind.Struct
                    &&名前空間一致判定(target.Symbol.ContainingNamespace.ToDisplayString(),rootNamespace,"Records"))
                .Select(target => target.Symbol)
                .GroupBy(static symbol => symbol.ToDisplayString(完全修飾形式),StringComparer.Ordinal)
                .Select(static group => group.First())
                .OrderBy(static symbol => symbol.Name,StringComparer.Ordinal)
                .ToArray();
            var keysNamespace = string.IsNullOrEmpty(rootNamespace)
                ? "Keys"
                : $"{rootNamespace}.Keys";
            var recordsNamespace = string.IsNullOrEmpty(rootNamespace)
                ? "Records"
                : $"{rootNamespace}.Records";

            if(keys.Length>0) {
                builder.AppendLine($"// Generated: schema key records declared under {keysNamespace}.");
                builder.AppendLine($"namespace {keysNamespace} {{");
                foreach(var key in keys) {
                    レコードソース本体追記(builder,key,"    ");
                }
                builder.AppendLine("}");
                builder.AppendLine();
            }

            if(types.Length>0) {
                builder.AppendLine($"// Generated: schema row/value records declared under {recordsNamespace}.");
                builder.AppendLine($"namespace {recordsNamespace} {{");
                foreach(var type in types) {
                    レコードソース本体追記(builder,type,"    ");
                }
                builder.AppendLine("}");
                builder.AppendLine();
            }
        }

        return builder.ToString();
    }

    // Key / Record の partial struct を、MemoryPack で保存できる完全な schema record にする。
    // unmanaged フィールドは _u、managed フィールドは _m に寄せ、backend から扱いやすい形へ整える。
    private static void レコードソース本体追記(StringBuilder builder,INamedTypeSymbol symbol,string indent) {
        var properties = 生成対象プロパティ取得(symbol);
        if(properties.Length==0) {
            return;
        }

        var symbolTypeName = symbol.ToDisplayString(完全修飾形式);

        var unmanagedProperties = properties.Where(static property => property.Type.IsUnmanagedType).ToImmutableArray();
        var managedProperties = properties.Where(static property => !property.Type.IsUnmanagedType).ToImmutableArray();

        var innerBuilder = new StringBuilder();
        {
            var indentedBuilder = innerBuilder;
            indentedBuilder.AppendLine($"// Generated: completed schema record for {symbolTypeName}. unmanaged data lives in _u and managed data lives in _m.");
            indentedBuilder.AppendLine($"partial struct {symbol.Name}:global::MemoryPack.IMemoryPackable<{symbolTypeName}> {{");
            indentedBuilder.AppendLine($"    public {symbol.Name}({string.Join(", ",properties.Select(static property => $"{property.Type.ToDisplayString(完全修飾形式)} {パラメーター名取得(property)}"))}) {{");
            if(unmanagedProperties.Length>0) {
                indentedBuilder.AppendLine($"        this._u=new({string.Join(", ",unmanagedProperties.Select(static property => パラメーター名取得(property)))});");
            }
            else {
                indentedBuilder.AppendLine("        this._u=new();");
            }
            if(managedProperties.Length>0) {
                indentedBuilder.AppendLine($"        this._m=new({string.Join(", ",managedProperties.Select(static property => パラメーター名取得(property)))});");
            }
            else {
                indentedBuilder.AppendLine("        this._m=new();");
            }
            indentedBuilder.AppendLine("    }");
            indentedBuilder.AppendLine();
            非管理レコードソース追記(indentedBuilder,unmanagedProperties);
            indentedBuilder.AppendLine("    private _U _u;");
            indentedBuilder.AppendLine();
            管理レコードソース追記(indentedBuilder,managedProperties);
            indentedBuilder.AppendLine("    private _M _m;");
            indentedBuilder.AppendLine();
            foreach(var property in properties) {
                indentedBuilder.AppendLine($"    public partial {property.Type.ToDisplayString(完全修飾形式)} {property.Name} => this.{格納先名取得(property)}.{property.Name};");
            }
            indentedBuilder.AppendLine();
            indentedBuilder.AppendLine($"    static {symbol.Name}() {{");
            indentedBuilder.AppendLine($"        global::MemoryPack.MemoryPackFormatterProvider.Register<{symbolTypeName}>();");
            indentedBuilder.AppendLine("    }");
            indentedBuilder.AppendLine();
            indentedBuilder.AppendLine("    static void global::MemoryPack.IMemoryPackFormatterRegister.RegisterFormatter() {");
            indentedBuilder.AppendLine($"        if(!global::MemoryPack.MemoryPackFormatterProvider.IsRegistered<{symbolTypeName}>()) {{");
            indentedBuilder.AppendLine($"            global::MemoryPack.MemoryPackFormatterProvider.Register(new global::MemoryPack.Formatters.MemoryPackableFormatter<{symbolTypeName}>());");
            indentedBuilder.AppendLine("        }");
            indentedBuilder.AppendLine($"        if(!global::MemoryPack.MemoryPackFormatterProvider.IsRegistered<{symbolTypeName}[]>()) {{");
            indentedBuilder.AppendLine($"            global::MemoryPack.MemoryPackFormatterProvider.Register(new global::MemoryPack.Formatters.ArrayFormatter<{symbolTypeName}>());");
            indentedBuilder.AppendLine("        }");
            indentedBuilder.AppendLine("    }");
            indentedBuilder.AppendLine();
            indentedBuilder.AppendLine($"    static void global::MemoryPack.IMemoryPackable<{symbolTypeName}>.Serialize<TBufferWriter>(ref global::MemoryPack.MemoryPackWriter<TBufferWriter> writer,scoped ref {symbolTypeName} value) {{");
            indentedBuilder.AppendLine("        writer.WriteUnmanaged(value._u);");
            indentedBuilder.AppendLine("        writer.WritePackable(value._m);");
            indentedBuilder.AppendLine("    }");
            indentedBuilder.AppendLine();
            indentedBuilder.AppendLine($"    static void global::MemoryPack.IMemoryPackable<{symbolTypeName}>.Deserialize(ref global::MemoryPack.MemoryPackReader reader,scoped ref {symbolTypeName} value) {{");
            indentedBuilder.AppendLine("        reader.ReadUnmanaged(out value._u);");
            indentedBuilder.AppendLine("        reader.ReadPackable(ref value._m);");
            indentedBuilder.AppendLine("    }");
            indentedBuilder.AppendLine("}");
        }
        インデント付き追記(builder,innerBuilder.ToString(),indent);
        builder.AppendLine();
    }

    private static ImmutableArray<IPropertySymbol> 生成対象プロパティ取得(INamedTypeSymbol symbol) {
        var builder = ImmutableArray.CreateBuilder<IPropertySymbol>();
        foreach(var property in symbol.GetMembers().OfType<IPropertySymbol>()) {
            if(プロパティ生成対象判定(property)) {
                builder.Add(property);
            }
        }
        return builder.ToImmutable();
    }

    private static bool プロパティ生成対象判定(IPropertySymbol property) {
        foreach(var syntaxReference in property.DeclaringSyntaxReferences) {
            if(syntaxReference.GetSyntax() is not PropertyDeclarationSyntax syntax) {
                continue;
            }
            if(!syntax.Modifiers.Any(SyntaxKind.PartialKeyword)) {
                continue;
            }
            if(syntax.ExpressionBody is not null) {
                continue;
            }
            if(syntax.AccessorList is null||syntax.AccessorList.Accessors.Count!=1) {
                continue;
            }
            var accessor = syntax.AccessorList.Accessors[0];
            if(accessor.Kind()!=SyntaxKind.GetAccessorDeclaration) {
                continue;
            }
            if(accessor.Body is not null||accessor.ExpressionBody is not null) {
                continue;
            }
            return true;
        }
        return false;
    }

    private static string パラメーター名取得(IPropertySymbol property) {
        return パラメーター名取得(property.Name);
    }

    private static string パラメーター名取得(string name) {
        if(string.IsNullOrEmpty(name)) {
            return "value";
        }
        if(name.Length==1) {
            return char.ToLowerInvariant(name[0]).ToString();
        }
        return char.ToLowerInvariant(name[0])+name.Substring(1);
    }

    private static bool 名前空間一致判定(string candidateNamespace,string rootNamespace,string childNamespace) {
        return string.Equals(candidateNamespace,$"{rootNamespace}.{childNamespace}",StringComparison.OrdinalIgnoreCase);
    }

    // unmanaged フィールドは record struct にまとめて一括シリアライズしやすくする。
    private static void 非管理レコードソース追記(StringBuilder builder,ImmutableArray<IPropertySymbol> unmanagedProperties) {
        if(unmanagedProperties.Length>0) {
            builder.AppendLine($"    private readonly record struct _U({string.Join(", ",unmanagedProperties.Select(static property => $"{property.Type.ToDisplayString(完全修飾形式)} {property.Name}"))});");
            return;
        }

        builder.AppendLine("    private struct _U {");
        builder.AppendLine("    }");
    }

    // managed フィールドは MemoryPackable な _M 側へ寄せ、型別の serialize / deserialize 文をここで組み立てる。
    private static void 管理レコードソース追記(StringBuilder builder,ImmutableArray<IPropertySymbol> managedProperties) {
        builder.AppendLine("    private struct _M:global::MemoryPack.IMemoryPackable<_M> {");
        foreach(var property in managedProperties) {
            builder.AppendLine($"        public {property.Type.ToDisplayString(完全修飾形式)} {property.Name};");
        }
        if(managedProperties.Length>0) {
            builder.AppendLine();
            builder.AppendLine($"        public _M({string.Join(", ",managedProperties.Select(static property => $"{property.Type.ToDisplayString(完全修飾形式)} {パラメーター名取得(property)}"))}) {{");
            foreach(var property in managedProperties) {
                builder.AppendLine($"            this.{property.Name}={パラメーター名取得(property)};");
            }
            builder.AppendLine("        }");
            builder.AppendLine();
        }
        builder.AppendLine("        static void global::MemoryPack.IMemoryPackFormatterRegister.RegisterFormatter() {");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        static void global::MemoryPack.IMemoryPackable<_M>.Serialize<TBufferWriter>(ref global::MemoryPack.MemoryPackWriter<TBufferWriter> writer,scoped ref _M value) {");
        foreach(var property in managedProperties) {
            builder.AppendLine($"            {管理シリアライズ文構築(property)}");
        }
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        static void global::MemoryPack.IMemoryPackable<_M>.Deserialize(ref global::MemoryPack.MemoryPackReader reader,scoped ref _M value) {");
        foreach(var property in managedProperties) {
            builder.AppendLine($"            {管理デシリアライズ文構築(property)}");
        }
        builder.AppendLine("        }");
        builder.AppendLine("    }");
    }

    private static string 管理シリアライズ文構築(IPropertySymbol property) {
        var propertyAccess = $"value.{property.Name}";
        if(property.Type.SpecialType==SpecialType.System_String) {
            return $"writer.WriteString({propertyAccess});";
        }
        return $"writer.WritePackable({propertyAccess});";
    }

    private static string 管理デシリアライズ文構築(IPropertySymbol property) {
        if(property.Type.SpecialType==SpecialType.System_String) {
            return $"value.{property.Name}=reader.ReadString()!;";
        }
        return $"reader.ReadPackable(ref value.{property.Name});";
    }

    private static string 格納先名取得(IPropertySymbol property) {
        return property.Type.IsUnmanagedType ? "_u" : "_m";
    }
}
