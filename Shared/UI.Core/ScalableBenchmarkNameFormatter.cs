namespace UI;

internal static class ScalableBenchmarkNameFormatter{
    private static readonly string[] KnownTypeTokens = [
        "UnmanagedStruct",
        "ManagedStruct",
        "ManagedClass",
        "Boolean",
        "Decimal",
        "Double",
        "Single",
        "String",
        "UInt64",
        "UInt32",
        "UInt16",
        "Int64",
        "Int32",
        "Int16",
        "Byte",
        "SByte",
        "Char",
    ];

    public static bool IsScalableSourceTab(BenchmarkSourceTab sourceTab) =>
        sourceTab is BenchmarkSourceTab.ScalableSet
            or BenchmarkSourceTab.ScalableDictionary
            or BenchmarkSourceTab.ScalablePersistedList;

    public static string CreateBenchmarkName(
        BenchmarkSourceTabDefinition sourceTab,
        string typeFullName,
        string methodName,
        int elementCount = 0){
        if(!IsScalableSourceTab(sourceTab.Tab)){
            return methodName;
        }

        var genericText = CreateGenericTypeText(typeFullName);
        if(string.IsNullOrWhiteSpace(genericText)){
            return methodName;
        }

        var suffix = elementCount>0
            ? "/"+elementCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : string.Empty;
        return CreateCanonicalProject(sourceTab)+"<"+genericText+">."+methodName+suffix;
    }

    public static string CreateGenericTypeText(string typeFullName){
        var typeName = ShortTypeName(typeFullName);
        if(typeName.StartsWith("Dictionary",StringComparison.Ordinal)){
            return string.Join(",",SplitTypeTokens(typeName["Dictionary".Length..]).Select(FormatTypeToken));
        }
        if(typeName.StartsWith("List",StringComparison.Ordinal)){
            return FormatTypeToken(typeName["List".Length..]);
        }
        if(typeName.StartsWith("Set",StringComparison.Ordinal)){
            return FormatTypeToken(typeName["Set".Length..]);
        }

        return string.Empty;
    }

    public static string ShortTypeName(string typeFullName){
        if(string.IsNullOrWhiteSpace(typeFullName)){
            return string.Empty;
        }

        var index = typeFullName.LastIndexOf('.');
        return index<0||index==typeFullName.Length-1
            ? typeFullName
            : typeFullName[(index+1)..];
    }

    private static string CreateCanonicalProject(BenchmarkSourceTabDefinition sourceTab) =>
        sourceTab.Tab==BenchmarkSourceTab.ScalablePersistedList
            ? sourceTab.LeftProject
            : sourceTab.RightProject;

    private static IEnumerable<string> SplitTypeTokens(string text){
        for(var index=0;index<text.Length;){
            var token = KnownTypeTokens.FirstOrDefault(candidate => text[index..].StartsWith(candidate,StringComparison.Ordinal));
            if(token is not null){
                yield return token;
                index+=token.Length;
                continue;
            }

            var next=index+1;
            yield return text[index..next];
            index=next;
        }
    }

    private static string FormatTypeToken(string token) =>
        token switch{
            "String" => "string",
            "Int32" => "int",
            "Decimal" => "decimal",
            "Boolean" => "System.Boolean",
            "Double" => "System.Double",
            "Single" => "System.Single",
            "UInt64" => "System.UInt64",
            "UInt32" => "System.UInt32",
            "UInt16" => "System.UInt16",
            "Int64" => "System.Int64",
            "Int16" => "System.Int16",
            "Byte" => "System.Byte",
            "SByte" => "System.SByte",
            "Char" => "System.Char",
            "ManagedClass" => "ManagedClass",
            "ManagedStruct" => "ManagedStruct",
            "UnmanagedStruct" => "UnmanagedStruct",
            _ => token,
        };
}
