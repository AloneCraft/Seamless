using System.Buffers.Binary;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Sample.NetShop.Core;
using Sample.NetShop.Core.Shop;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Tds;

namespace Seamless.Database.Gateway.Tests;

public sealed class GatewayServerTests{
    [Fact,Trait("Category","Integration")]
    public async Task HttpAndTdsIntegration(){
        var tempRoot=Environment.GetEnvironmentVariable("SEAMLESS_COMPARISON_TEMP_ROOT");
        if(string.IsNullOrWhiteSpace(tempRoot)){
            if(Directory.Exists(@"D:\")){tempRoot=@"D:\Seamless\Comparison\Temp";}else{tempRoot=Path.GetTempPath();}
        }
        var dbFolder=Path.Combine(tempRoot,"SeamlessTests_Gateway_"+Guid.NewGuid().ToString("N"));
        try{
            using var db=商店DB.開く(dbFolder,資源設定.標準);
            db.初期データを登録する();
            var adapter=new InProcessDatabaseAdapter(db);

            var tdsPort=14334;
            var httpPort=8089;
            var gatewayOptions=new GatewayServerOptions{
                TdsPort=tdsPort,
                HttpPort=httpPort,
                NativePort=0
            };

            using var server=new GatewayServer(adapter,gatewayOptions);
            server.Start();

            try{
                // 1. HTTP API の検証
                using var http=new HttpClient();

                {
                    // GET /api/tables
                    var res=await http.GetAsync($"http://localhost:{httpPort}/api/tables");
                    res.EnsureSuccessStatusCode();
                    var json=await res.Content.ReadAsStringAsync();
                    Assert.Contains("商品",json);
                    Assert.Contains("顧客",json);
                }

                {
                    // POST /api/sql (SELECT)
                    var jsonBody=JsonSerializer.Serialize(new{query="SELECT * FROM 商品;"});
                    var content=new StringContent(jsonBody,global::System.Text.Encoding.UTF8,"application/json");
                    var res=await http.PostAsync($"http://localhost:{httpPort}/api/sql",content);
                    res.EnsureSuccessStatusCode();
                    var json=await res.Content.ReadAsStringAsync();
                    using var document=JsonDocument.Parse(json);
                    var columns=document.RootElement.GetProperty("columns").EnumerateArray().Select(column=>column.GetString()).ToArray();
                    Assert.Contains("商品ID",columns);
                    Assert.Contains("単価",columns);
                }

                // 2. TDS PreLogin ハンドシェイクの検証 (生Socket通信)
                {
                    using var tcp=new TcpClient();
                    await tcp.ConnectAsync("127.0.0.1",tdsPort);
                    var stream=tcp.GetStream();

                    // TDS PreLogin パケット構築
                    var payload=new byte[]{
                        0x00, 0x00, 0x1A, 0x00, 0x06, // Version (offset 26)
                        0x01, 0x00, 0x20, 0x00, 0x01, // Encryption (offset 32)
                        0x02, 0x00, 0x21, 0x00, 0x01, // InstOpt (offset 33)
                        0x03, 0x00, 0x22, 0x00, 0x04, // ThreadId (offset 34)
                        0x04, 0x00, 0x26, 0x00, 0x01, // Mars (offset 38)
                        0xFF,                         // Terminator
                        0x10, 0x00, 0x03, 0xE8, 0x00, 0x06, // Version 16.0.1000.6
                        0x02,                         // Encryption Not Supported
                        0x00,
                        0x00, 0x00, 0x00, 0x00,
                        0x00
                    };
                    var header=new byte[8];
                    header[0]=TdsConstants.PacketTypePreLogin;
                    header[1]=TdsConstants.StatusEndOfMessage;
                    BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(2,2),(ushort)(payload.Length+8));

                    await stream.WriteAsync(header);
                    await stream.WriteAsync(payload);
                    await stream.FlushAsync();

                    // 応答パケット読み取り
                    var responseHeader=new byte[8];
                    var readCount=await stream.ReadAsync(responseHeader);
                    Assert.Equal(8,readCount);
                    Assert.Equal(TdsConstants.PacketTypeTabularResult,responseHeader[0]);
                    var respLen=BinaryPrimitives.ReadUInt16BigEndian(responseHeader.AsSpan(2,2));
                    Assert.True(respLen>8);

                    var respPayload=new byte[respLen-8];
                    var readPayload=await stream.ReadAsync(respPayload);
                    Assert.Equal(respPayload.Length,readPayload);
                    // 応答トークンに Version (0x00) と Encryption (0x01) が含まれていることを確認
                    Assert.Equal(0x00,respPayload[0]);
                }

                // 3. Microsoft.Data.SqlClient (SqlConnection) を使った接続とクエリ実行 (SELECT, INSERT, UPDATE, DELETE) の検証
                {
                    var connStr=$"Data Source=127.0.0.1,{tdsPort};Initial Catalog=商店DB;User Id=sa;Password=dummy;Encrypt=False;TrustServerCertificate=True;Connect Timeout=5;";
                    using var conn=new SqlConnection(connStr);
                    await conn.OpenAsync();

                    // SELECT
                    using var cmdSelect=new SqlCommand("SELECT * FROM 商品;",conn);
                    using var reader=await cmdSelect.ExecuteReaderAsync();
                    Assert.True(reader.HasRows);
                    var readAny=false;
                    while(await reader.ReadAsync()){
                        readAny=true;
                        var id=reader.GetInt32(0);
                        Assert.True(id>0);
                    }
                    Assert.True(readAny);
                    reader.Close();

                    // INSERT
                    using var cmdInsert=new SqlCommand("INSERT INTO 商品 (商品ID, 名前, 単価, 在庫数) VALUES (8888, 'SqlClientテスト商品', 1200, 25);",conn);
                    var affectedInsert=await cmdInsert.ExecuteNonQueryAsync();
                    Assert.Equal(1,affectedInsert);

                    // UPDATE
                    using var cmdUpdate=new SqlCommand("UPDATE 商品 SET 単価 = 1350 WHERE 商品ID = 8888;",conn);
                    var affectedUpdate=await cmdUpdate.ExecuteNonQueryAsync();
                    Assert.Equal(1,affectedUpdate);

                    // DELETE
                    using var cmdDelete=new SqlCommand("DELETE FROM 商品 WHERE 商品ID = 8888;",conn);
                    var affectedDelete=await cmdDelete.ExecuteNonQueryAsync();
                    Assert.Equal(1,affectedDelete);
                }
            }finally{
                await server.StopAsync();
            }
        }finally{
            if(Directory.Exists(dbFolder)){Directory.Delete(dbFolder,true);}
        }
    }
}
