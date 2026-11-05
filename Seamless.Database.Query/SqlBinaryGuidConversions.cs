using System.Buffers.Binary;

namespace Seamless.Database.Query;

internal static class SqlBinaryGuidConversions{
    internal static Guid GuidValue(object value,bool explicitConversion){
        if(value is Guid guid){return guid;}
        if(value is byte[] bytes){if(bytes.Length==16){return new Guid(bytes);}throw new SqlQueryException("Conversion from binary to uniqueidentifier requires sixteen bytes.",8169);}
        if(value is not string text){var number=206;if(explicitConversion){number=529;}throw new SqlQueryException("The source SQL type cannot be converted to uniqueidentifier.",number);}
        if(text.Length>=38&&text[0]=='{'&&text[37]=='}'&&Guid.TryParseExact(text.AsSpan(0,38),"B",out var braced)){return braced;}
        if(text.Length>=36&&Guid.TryParseExact(text.AsSpan(0,36),"D",out var parsed)){return parsed;}
        throw new SqlQueryException("Conversion failed when converting from a character string to uniqueidentifier.",8169);
    }
    internal static byte[] IntegerBytes(object value,SqlType type){
        Span<byte> source=stackalloc byte[8];int width;
        switch(value){case byte number:width=1;source[0]=number;break;case short number:width=2;BinaryPrimitives.WriteInt16BigEndian(source,number);break;case int number:width=4;BinaryPrimitives.WriteInt32BigEndian(source,number);break;case long number:width=8;BinaryPrimitives.WriteInt64BigEndian(source,number);break;default:throw new SqlQueryException("Unsupported integer binary conversion.",529);}
        var length=width;if(type.Length is{} requested&&requested>=0){if(type.Name=="binary"){length=requested;}else{length=Math.Min(width,requested);}}
        var result=new byte[length];var count=Math.Min(width,length);source.Slice(width-count,count).CopyTo(result.AsSpan(length-count));return result;
    }
}
