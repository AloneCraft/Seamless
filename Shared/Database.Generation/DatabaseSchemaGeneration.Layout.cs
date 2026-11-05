namespace Shared.Database.Generation;

internal sealed partial class DatabaseSchemaGeneration{
    private static string Field(IPropertySymbol property,int index,bool optimized){
        if(optimized){return "_F.@"+property.Name;}
        return "_value"+index;
    }

    private static void AppendFields(StringBuilder builder,ImmutableArray<IPropertySymbol> properties,string indent){
        // 論理順・コンストラクター順・formatterの順は変えず、生成器所有の実体だけを並べ替える。
        var fields=properties.Select((property,index)=>(Property:property,Index:index,Layout:Storage(property.Type,0)))
            .OrderBy(static field=>field.Layout.References)
            .ThenByDescending(static field=>field.Layout.Size)
            .ThenBy(static field=>field.Index);
        builder.AppendLine($"{indent}    [global::System.Runtime.InteropServices.StructLayout(global::System.Runtime.InteropServices.LayoutKind.Sequential)]");
        builder.AppendLine($"{indent}    private struct F {{");
        foreach(var field in fields){builder.AppendLine($"{indent}        public {TypeName(field.Property.Type)} @{field.Property.Name};");}
        builder.AppendLine($"{indent}    }}");
        builder.AppendLine($"{indent}    private F _F;");
    }

    // 配置順の候補を決めるためのサイズ。実際のABI・GC配置とサイズはCLRが決める。
    // 不明な型をunmanagedと推測せず、参照を含む値型も参照グループへ置く。
    private static (int Size,int Alignment,bool References) Storage(ITypeSymbol type,int depth){
        if(type.IsReferenceType||type is IArrayTypeSymbol){return(8,8,true);}
        if(type is INamedTypeSymbol{TypeKind:TypeKind.Enum,EnumUnderlyingType:{} underlying}){return Storage(underlying,depth+1);}
        switch(type.SpecialType){
            case SpecialType.System_Boolean:case SpecialType.System_Byte:case SpecialType.System_SByte:return(1,1,false);
            case SpecialType.System_Char:case SpecialType.System_Int16:case SpecialType.System_UInt16:return(2,2,false);
            case SpecialType.System_Int32:case SpecialType.System_UInt32:case SpecialType.System_Single:return(4,4,false);
            case SpecialType.System_Int64:case SpecialType.System_UInt64:case SpecialType.System_Double:case SpecialType.System_IntPtr:case SpecialType.System_UIntPtr:return(8,8,false);
            case SpecialType.System_Decimal:return(16,8,false);
        }
        if(depth>=16){return(8,8,!type.IsUnmanagedType);}
        if(type is INamedTypeSymbol{OriginalDefinition.SpecialType:SpecialType.System_Nullable_T} nullable){
            var value=Storage(nullable.TypeArguments[0],depth+1);
            return(Align(value.Size+value.Alignment,value.Alignment),value.Alignment,value.References);
        }
        var members=type.GetMembers().OfType<IFieldSymbol>().Where(static field=>!field.IsStatic).Select(field=>Storage(field.Type,depth+1)).ToArray();
        if(members.Length==0){return(8,8,!type.IsUnmanagedType);}
        var size=0;var alignment=1;var references=false;
        foreach(var field in members){size=Align(size,field.Alignment)+field.Size;alignment=Math.Max(alignment,field.Alignment);references|=field.References;}
        return(Align(size,alignment),alignment,references);
    }
    private static int Align(int size,int alignment)=>(size+alignment-1)/alignment*alignment;
}
