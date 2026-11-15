namespace Shared.Database.Generation;

internal sealed partial class DatabaseSchemaGeneration{
    private static void AppendSeparatedFields(StringBuilder builder,ImmutableArray<IPropertySymbol> properties,string indent){
        foreach(var references in new[]{true,false}){
            var fields=properties.Select((property,index)=>(Property:property,Index:index,Layout:Storage(property.Type,0,true)))
                .Where(field=>field.Layout.References==references)
                .OrderByDescending(static field=>field.Layout.Size).ThenBy(static field=>field.Index).ToArray();
            if(fields.Length==0){continue;}
            var group=references?"C":"S";
            builder.AppendLine($"{indent}[global::System.Runtime.InteropServices.StructLayout(global::System.Runtime.InteropServices.LayoutKind.Sequential)]");
            builder.AppendLine($"{indent}private struct {group}{{");
            foreach(var field in fields){builder.AppendLine($"{indent}    public {TypeName(field.Property.Type)} @{field.Property.Name};");}
            builder.AppendLine($"{indent}}}");
            builder.AppendLine($"{indent}private readonly {group} _{group};");
        }
    }

    // 配置順の候補を決めるためのサイズ。実際のABI・GC配置とサイズはCLRが決める。
    // 不明な型をunmanagedと推測せず、参照を含む値型も参照グループへ置く。
    private static (int Size,int Alignment,bool References) Storage(ITypeSymbol type,int depth,bool declaredProperties=false){
        if(type.IsReferenceType||type is IArrayTypeSymbol){return(8,8,true);}
        if(type is INamedTypeSymbol{TypeKind:TypeKind.Enum,EnumUnderlyingType:{} underlying}){return Storage(underlying,depth+1,declaredProperties);}
        switch(type.SpecialType){
            case SpecialType.System_Boolean:case SpecialType.System_Byte:case SpecialType.System_SByte:return(1,1,false);
            case SpecialType.System_Char:case SpecialType.System_Int16:case SpecialType.System_UInt16:return(2,2,false);
            case SpecialType.System_Int32:case SpecialType.System_UInt32:case SpecialType.System_Single:return(4,4,false);
            case SpecialType.System_Int64:case SpecialType.System_UInt64:case SpecialType.System_Double:case SpecialType.System_IntPtr:case SpecialType.System_UIntPtr:return(8,8,false);
            case SpecialType.System_Decimal:return(16,8,false);
        }
        if(depth>=16){return(8,8,declaredProperties||!type.IsUnmanagedType);}
        if(type is INamedTypeSymbol{OriginalDefinition.SpecialType:SpecialType.System_Nullable_T} nullable){
            var value=Storage(nullable.TypeArguments[0],depth+1,declaredProperties);
            return(Align(value.Size+value.Alignment,value.Alignment),value.Alignment,value.References);
        }
        var memberTypes=type.GetMembers().OfType<IFieldSymbol>().Where(static field=>!field.IsStatic).Select(static field=>field.Type);
        if(declaredProperties&&type is INamedTypeSymbol named){
            // 生成前の型には保存フィールドがまだない。partial宣言の列も参照の有無へ含める。
            memberTypes=memberTypes.Concat(GetProperties(named).Select(static property=>property.Type));
        }
        var members=memberTypes.Select(member=>Storage(member,depth+1,declaredProperties)).ToArray();
        if(members.Length==0){return(8,8,!type.IsUnmanagedType);}
        var size=0;var alignment=1;var references=false;
        foreach(var field in members){size=Align(size,field.Alignment)+field.Size;alignment=Math.Max(alignment,field.Alignment);references|=field.References;}
        return(Align(size,alignment),alignment,references);
    }
    private static int Align(int size,int alignment)=>(size+alignment-1)/alignment*alignment;
}
