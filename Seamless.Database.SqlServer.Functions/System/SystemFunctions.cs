using System.IO.Compression;
using System.Security.Cryptography;
namespace Seamless.Database.SqlServer.Functions;
/// <summary>システムの関数。</summary>
/// <remarks>
/// CHECKSUM・BINARY_CHECKSUM・RAND は計算の仕方が公開されておらず、本物と同じ値を作れないので実装しない。
/// COMPRESS は圧縮の結果のバイト列が本物と一致する保証が無いので実装しない(DECOMPRESS は一致する)。
/// </remarks>
public static class SystemFunctions{
    /// <summary>NEWID。</summary>
    [SqlFunction("NEWID")]
    public static Guid NewId()=>Guid.NewGuid();
    /// <summary>DECOMPRESS。GZIP で圧縮されたバイト列を戻す。GZIP でなければ 9826。</summary>
    [SqlFunction("DECOMPRESS")]
    public static byte[]? Decompress(byte[]? value){
        if(value is null){
            return null;
        }
        try{
            using var input=new MemoryStream(value,false);
            using var gzip=new GZipStream(input,CompressionMode.Decompress);
            using var output=new MemoryStream();
            gzip.CopyTo(output);
            return output.ToArray();
        }catch(InvalidDataException){
            throw new SqlFunctionException("The input to the DECOMPRESS function is not valid compressed data.",9826);
        }
    }
    /// <summary>
    /// HASHBYTES。MD5・SHA・SHA1・SHA2_256・SHA2_512。文字列は翻訳器が <see cref="ConversionFunctions.StringToBinary"/> でバイト列にしてから渡す。
    /// MD2・MD4 は .NET に無いので NotSupportedException。知らない名前は NULL(本物と同じ)。
    /// </summary>
    [SqlFunction("HASHBYTES")]
    public static byte[]? HashBytes(string? algorithm,byte[]? input){
        if(algorithm is null||input is null){
            return null;
        }
        return algorithm.ToUpperInvariant() switch{
            "MD5"=>MD5.HashData(input),
            "SHA" or "SHA1"=>SHA1.HashData(input),
            "SHA2_256"=>SHA256.HashData(input),
            "SHA2_512"=>SHA512.HashData(input),
            "MD2" or "MD4"=>throw new NotSupportedException($"HASHBYTES の {algorithm} は .NET に無いので実装していない。"),
            _=>null
        };
    }
}
