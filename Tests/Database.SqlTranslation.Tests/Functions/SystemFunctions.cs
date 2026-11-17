using System.Text;
using F=Seamless.Database.SqlServer.Functions.SystemFunctions;
using static Database.SqlServer.Functions.Tests.SqlServerOracle;
namespace Database.SqlServer.Functions.Tests;
public sealed class SystemFunctions{
    [SqlServerFact,Trait("Category","SqlServer")]public void MatchesSqlServer(){
        Check(
            ("DECOMPRESS(COMPRESS(N'abc'))",()=>F.Decompress(Gzip(Encoding.Unicode.GetBytes("abc")))),
            ("DECOMPRESS(0x0102)",()=>F.Decompress([1,2])),
            ("HASHBYTES('SHA2_256',N'abc')",()=>F.HashBytes("SHA2_256",Encoding.Unicode.GetBytes("abc"))),
            ("HASHBYTES('MD5','abc')",()=>F.HashBytes("MD5",Encoding.ASCII.GetBytes("abc"))),
            ("HASHBYTES('SHA1','abc')",()=>F.HashBytes("SHA1",Encoding.ASCII.GetBytes("abc"))),
            ("HASHBYTES('SHA2_512','abc')",()=>F.HashBytes("SHA2_512",Encoding.ASCII.GetBytes("abc"))),
            ("HASHBYTES('NOPE','abc')",()=>F.HashBytes("NOPE",Encoding.ASCII.GetBytes("abc")))
        );
    }
    private static byte[] Gzip(byte[] data){
        using var output=new MemoryStream();
        using(var gzip=new System.IO.Compression.GZipStream(output,System.IO.Compression.CompressionLevel.Optimal)){
            gzip.Write(data);
        }
        return output.ToArray();
    }
}
