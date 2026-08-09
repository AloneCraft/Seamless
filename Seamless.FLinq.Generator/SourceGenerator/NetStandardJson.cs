namespace Seamless.FLinq;

internal static class NetStandardJson {
    public static string Serialize(object? value) {
        var builder=new StringBuilder();
        AppendValue(builder,value);
        return builder.ToString();
    }

    private static void AppendValue(StringBuilder builder,object? value) {
        if(value is null) {
            builder.Append("null");
            return;
        }
        switch(value) {
            case string text:
                AppendString(builder,text);
                return;
            case char character:
                AppendString(builder,character.ToString());
                return;
            case bool boolean:
                builder.Append(boolean ? "true" : "false");
                return;
        }

        var type=value.GetType();
        if(type.IsEnum) {
            AppendString(builder,value.ToString()!);
            return;
        }
        if(value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal) {
            builder.Append(Convert.ToString(value,System.Globalization.CultureInfo.InvariantCulture));
            return;
        }
        if(value is System.Collections.IEnumerable enumerable) {
            builder.Append('[');
            var first=true;
            foreach(var item in enumerable) {
                if(!first) {
                    builder.Append(',');
                }
                AppendValue(builder,item);
                first=false;
            }
            builder.Append(']');
            return;
        }

        builder.Append('{');
        var properties=type.GetProperties(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public)
            .OrderBy(static property => property.MetadataToken)
            .ToArray();
        for(var index=0;index<properties.Length;index++) {
            if(index>0) {
                builder.Append(',');
            }
            AppendString(builder,properties[index].Name);
            builder.Append(':');
            AppendValue(builder,properties[index].GetValue(value,null));
        }
        builder.Append('}');
    }

    private static void AppendString(StringBuilder builder,string value) {
        builder.Append('"');
        foreach(var character in value) {
            switch(character) {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\b':
                    builder.Append("\\b");
                    break;
                case '\f':
                    builder.Append("\\f");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    if(character<32) {
                        builder.Append("\\u");
                        builder.Append(((int)character).ToString("x4",System.Globalization.CultureInfo.InvariantCulture));
                    }else{
                        builder.Append(character);
                    }
                    break;
            }
        }
        builder.Append('"');
    }
}