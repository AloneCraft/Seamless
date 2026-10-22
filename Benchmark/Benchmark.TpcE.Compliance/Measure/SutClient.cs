using System.Net.Sockets;
using System.Text;
using System.Text.Json;
namespace Benchmark.TpcE.Compliance;
/// <summary>SUT へ要求を 1 件送って応答を読む(試験用の小さなクライアント)。</summary>
internal static class SutClient{
    public static JsonElement Call(int port,string json){
        using var client=new TcpClient("127.0.0.1",port);
        var stream=client.GetStream();
        Framing.Write(stream,Encoding.UTF8.GetBytes(json));
        var body=Framing.Read(stream) ?? throw new IOException("The SUT closed the connection.");
        using var document=JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }
    public static JsonElement Admin(int port,string command)=>Call(port,$"{{\"admin\":\"{command}\"}}");
}
