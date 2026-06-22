namespace Seamless.FLinq;

public static class GeneratorHelpers {
    public static readonly SymbolDisplayFormat 完全修飾形式 = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.ExpandNullable
            | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
            | SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    public static readonly SymbolDisplayFormat cref形式 = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    /// <summary>グローバル名前空間は空文字列、それ以外は表示文字列を返す。</summary>
    public static string 名前空間テキスト取得(INamespaceSymbol namespaceSymbol) {
        return namespaceSymbol.IsGlobalNamespace ? string.Empty : namespaceSymbol.ToDisplayString();
    }

    /// <summary>
    /// <paramref name="value"/> の各行先頭に <paramref name="indent"/> を付けて
    /// <paramref name="builder"/> に追記する。空行はインデントなしで出力する。
    /// CRLF・LF のどちらにも対応する。
    /// </summary>
    public static void インデント付き追記(StringBuilder builder, string value, string indent) {
        foreach(var line in value.Replace("\r\n", "\n").Split('\n')) {
            if(line.Length==0) {
                builder.AppendLine();
                continue;
            }

            builder.Append(indent);
            builder.AppendLine(line);
        }
    }

    /// <summary>
    /// 文字列から空白のみの行を除去して返す。
    /// データベーススキーマの生成ソース出力でコンパクト化のために使う。
    /// </summary>
    public static string 空行除去(string source) {
        var lines = source.Replace("\r\n", "\n").Split('\n');
        var builder = new StringBuilder(source.Length);
        var firstWritten = true;
        foreach(var line in lines) {
            if(string.IsNullOrWhiteSpace(line)) {
                continue;
            }
            if(!firstWritten) {
                builder.Append('\n');
            }
            builder.Append(line);
            firstWritten=false;
        }
        return builder.ToString();
    }
}
