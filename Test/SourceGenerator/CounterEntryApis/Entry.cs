using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Seamless.FLinq;
namespace Test.SourceGenerator.CounterEntryApis;
[LinqIntercept(false)]
public sealed class Entry{
    private const string InputSHA="3E28148ECAE999B6D602C07923D1552498813AA466D6A7A136FA8F2B50014A55";
    private const string CsvSHA="CCCD9D53A6A0EA557C6284BCF5864FABC54EC1EE0F7A8F36FF6156AF68C10DAD";
    private const string InputBase64="dXNpbmcgU3lzdGVtOwp1c2luZyBTeXN0ZW0uQ29sbGVjdGlvbnMuR2VuZXJpYzsKdXNpbmcgU2VhbWxlc3MuTGlucTsKbmFtZXNwYWNlIEVudHJ5UmVwcm87CnB1YmxpYyBzdGF0aWMgY2xhc3MgUHJvYmV7CiAgICBwdWJsaWMgc3RhdGljIG9iamVjdFtdIFJ1bihpbnQgaWQsYm9vbCBsZWdhY3kpewogICAgICAgIHVzaW5nIHZhciBzY29wZT1MaW5xQ291bnRlclNjb3BlLlN0YXJ0KCk7CiAgICAgICAgdmFyIHRyYWNlPW5ldyBMaXN0PHN0cmluZz4oKTsKICAgICAgICB2YXIgdmFsdWU9IiI7CiAgICAgICAgdmFyIGVycm9yPSIiOwogICAgICAgIGludCBCb2R5KGludCBpdGVtLGJvb2wgZmFpbCl7CiAgICAgICAgICAgIHRyYWNlLkFkZCgiQiIpOwogICAgICAgICAgICBpZihmYWlsKXt0aHJvdyBuZXcgSW52YWxpZE9wZXJhdGlvbkV4Y2VwdGlvbigiYm9keSIpO30KICAgICAgICAgICAgcmV0dXJuIGl0ZW07CiAgICAgICAgfQogICAgICAgIGludCBBcmd1bWVudChib29sIGZhaWwpewogICAgICAgICAgICB0cmFjZS5BZGQoIkEiKTsKICAgICAgICAgICAgaWYoZmFpbCl7dGhyb3cgbmV3IEFyZ3VtZW50RXhjZXB0aW9uKCJhcmd1bWVudCIpO30KICAgICAgICAgICAgcmV0dXJuIDM7CiAgICAgICAgfQogICAgICAgIEZ1bmM8aW50LGludD4/IENhbGxiYWNrKGJvb2wgYWJzZW50LGJvb2wgZmFpbCl7CiAgICAgICAgICAgIHRyYWNlLkFkZCgiRCIpOwogICAgICAgICAgICBpZihhYnNlbnQpe3JldHVybiBudWxsO30KICAgICAgICAgICAgcmV0dXJuIGl0ZW09PkJvZHkoaXRlbSxmYWlsKTsKICAgICAgICB9CiAgICAgICAgdHJ5ewogICAgICAgICAgICBpZihpZDw9MSl7CiAgICAgICAgICAgICAgICBpZihsZWdhY3kpe3ZhbHVlPXNjb3BlLkNvdW50ZXIuUmVjb3JkTG9naWNhbERlbGVnYXRlKEJvZHkoMyxpZD09MSkpLlRvU3RyaW5nKCk7fQogICAgICAgICAgICAgICAgZWxzZXt2YWx1ZT1zY29wZS5Db3VudGVyLkVudGVyTG9naWNhbERlbGVnYXRlKCkuUmV0dXJuVmFsdWUoQm9keSgzLGlkPT0xKSkuVG9TdHJpbmcoKTt9CiAgICAgICAgICAgIH1lbHNlIGlmKGlkPD01KXsKICAgICAgICAgICAgICAgIGlmKGxlZ2FjeSl7dmFsdWU9c2NvcGUuQ291bnRlci5SZWNvcmRQaHlzaWNhbERlbGVnYXRlKENhbGxiYWNrKGlkPT00LGlkPT0zKSEuSW52b2tlKEFyZ3VtZW50KGlkPT01KSkpLlRvU3RyaW5nKCk7fQogICAgICAgICAgICAgICAgZWxzZXt2YWx1ZT1zY29wZS5Db3VudGVyLkludm9rZVBoeXNpY2FsRGVsZWdhdGUoQ2FsbGJhY2soaWQ9PTQsaWQ9PTMpLEFyZ3VtZW50KGlkPT01KSkuVG9TdHJpbmcoKTt9CiAgICAgICAgICAgIH1lbHNlIGlmKGlkPT02KXsKICAgICAgICAgICAgICAgIEZ1bmM8aW50LGludCxpbnQ+IGNhbGxiYWNrPShmaXJzdCxzZWNvbmQpPT5Cb2R5KGZpcnN0K3NlY29uZCxmYWxzZSk7CiAgICAgICAgICAgICAgICBpZihsZWdhY3kpe3ZhbHVlPXNjb3BlLkNvdW50ZXIuUmVjb3JkUGh5c2ljYWxEZWxlZ2F0ZShjYWxsYmFjay5JbnZva2UoQXJndW1lbnQoZmFsc2UpLEFyZ3VtZW50KGZhbHNlKSkpLlRvU3RyaW5nKCk7fQogICAgICAgICAgICAgICAgZWxzZXt2YWx1ZT1zY29wZS5Db3VudGVyLkludm9rZVBoeXNpY2FsRGVsZWdhdGUoY2FsbGJhY2ssQXJndW1lbnQoZmFsc2UpLEFyZ3VtZW50KGZhbHNlKSkuVG9TdHJpbmcoKTt9CiAgICAgICAgICAgIH1lbHNlIGlmKGlkPT03KXsKICAgICAgICAgICAgICAgIEZ1bmM8aW50LGludCxpbnQsaW50PiBjYWxsYmFjaz0oZmlyc3Qsc2Vjb25kLHRoaXJkKT0+Qm9keShmaXJzdCtzZWNvbmQrdGhpcmQsZmFsc2UpOwogICAgICAgICAgICAgICAgaWYobGVnYWN5KXt2YWx1ZT1zY29wZS5Db3VudGVyLlJlY29yZFBoeXNpY2FsRGVsZWdhdGUoY2FsbGJhY2suSW52b2tlKEFyZ3VtZW50KGZhbHNlKSxBcmd1bWVudChmYWxzZSksQXJndW1lbnQoZmFsc2UpKSkuVG9TdHJpbmcoKTt9CiAgICAgICAgICAgICAgICBlbHNle3ZhbHVlPXNjb3BlLkNvdW50ZXIuSW52b2tlUGh5c2ljYWxEZWxlZ2F0ZShjYWxsYmFjayxBcmd1bWVudChmYWxzZSksQXJndW1lbnQoZmFsc2UpLEFyZ3VtZW50KGZhbHNlKSkuVG9TdHJpbmcoKTt9CiAgICAgICAgICAgIH1lbHNlIGlmKGlkPT04KXsKICAgICAgICAgICAgICAgIC8vIOefree1oeacquWIsOmBlOOBruacrOS9k+OBrzDjgIIKICAgICAgICAgICAgICAgIHZhciBjb25kaXRpb249ZmFsc2U7CiAgICAgICAgICAgICAgICBpZihjb25kaXRpb24pe3ZhbHVlPXNjb3BlLkNvdW50ZXIuRW50ZXJMb2dpY2FsRGVsZWdhdGUoKS5SZXR1cm5WYWx1ZShCb2R5KDMsdHJ1ZSkpLlRvU3RyaW5nKCk7fQogICAgICAgICAgICB9ZWxzZSBpZihpZD09OSl7CiAgICAgICAgICAgICAgICBpZihsZWdhY3kpe3ZhbHVlPXNjb3BlLkNvdW50ZXIuUmVjb3JkTG9naWNhbERlbGVnYXRlKG5ld3tWYWx1ZT1Cb2R5KDMsZmFsc2UpfSkuVmFsdWUuVG9TdHJpbmcoKTt9CiAgICAgICAgICAgICAgICBlbHNle3ZhbHVlPXNjb3BlLkNvdW50ZXIuRW50ZXJMb2dpY2FsRGVsZWdhdGUoKS5SZXR1cm5WYWx1ZShuZXd7VmFsdWU9Qm9keSgzLGZhbHNlKX0pLlZhbHVlLlRvU3RyaW5nKCk7fQogICAgICAgICAgICB9ZWxzZSBpZihpZD09MTApewogICAgICAgICAgICAgICAgaW50PyBvcHRpb25hbD1udWxsOwogICAgICAgICAgICAgICAgaWYobGVnYWN5KXt2YWx1ZT1zY29wZS5Db3VudGVyLlJlY29yZExvZ2ljYWxEZWxlZ2F0ZShvcHRpb25hbCkuSGFzVmFsdWUuVG9TdHJpbmcoKTt9CiAgICAgICAgICAgICAgICBlbHNle3ZhbHVlPXNjb3BlLkNvdW50ZXIuRW50ZXJMb2dpY2FsRGVsZWdhdGUoKS5SZXR1cm5WYWx1ZShvcHRpb25hbCkuSGFzVmFsdWUuVG9TdHJpbmcoKTt9CiAgICAgICAgICAgIH1lbHNlewogICAgICAgICAgICAgICAgLy8gcmVmIHN0cnVjdOOBrmlkZW50aXR55Z6L5LqS5o+b44CC5penUmVjb3Jk44Gr44Gv5rih44Gb44Gq44GE44Gf44KBdjLjgaDjgZHjga5jb21waWxlIGdhdGXjgIIKICAgICAgICAgICAgICAgIFJlYWRPbmx5U3BhbjxpbnQ+IHNwYW49bmV3W117M307CiAgICAgICAgICAgICAgICB2YWx1ZT1zY29wZS5Db3VudGVyLkVudGVyTG9naWNhbERlbGVnYXRlKCkuUmV0dXJuVmFsdWUoc3BhbilbMF0uVG9TdHJpbmcoKTsKICAgICAgICAgICAgfQogICAgICAgIH1jYXRjaChFeGNlcHRpb24gZXhjZXB0aW9uKXtlcnJvcj1leGNlcHRpb24uR2V0VHlwZSgpLk5hbWUrIjoiK2V4Y2VwdGlvbi5NZXNzYWdlO30KICAgICAgICByZXR1cm4gbmV3IG9iamVjdFtde3ZhbHVlLGVycm9yLHN0cmluZy5Kb2luKCIsIix0cmFjZSksc2NvcGUuQ291bnRlci5Mb2dpY2FsRGVsZWdhdGUsc2NvcGUuQ291bnRlci5QaHlzaWNhbERlbGVnYXRlfTsKICAgIH0KfQo=";
    private const string CsvBase64="SWQsU2NlbmFyaW8sVmFsdWUsRXhjZXB0aW9uLFRyYWNlLExlZ2FjeUxvZ2ljYWwsTGVnYWN5UGh5c2ljYWwsVjJMb2dpY2FsLFYyUGh5c2ljYWwKMCxsb2dpY2FsIHJldHVybiwzLCxCLDEsMCwxLDAKMSxsb2dpY2FsIHRocm93LCxJbnZhbGlkT3BlcmF0aW9uRXhjZXB0aW9uOmJvZHksQiwwLDAsMSwwCjIscGh5c2ljYWwgcmV0dXJuLDMsLCJELEEsQiIsMSwxLDEsMQozLHBoeXNpY2FsIGJvZHkgdGhyb3csLEludmFsaWRPcGVyYXRpb25FeGNlcHRpb246Ym9keSwiRCxBLEIiLDAsMCwxLDEKNCxudWxsIGNhbGxiYWNrLCxOdWxsUmVmZXJlbmNlRXhjZXB0aW9uLCJELEEiLDAsMCwwLDAKNSxhcmd1bWVudCB0aHJvdywsQXJndW1lbnRFeGNlcHRpb246YXJndW1lbnQsIkQsQSIsMCwwLDAsMAo2LHBoeXNpY2FsIGFyaXR5Miw2LCwiQSxBLEIiLDEsMSwxLDEKNyxwaHlzaWNhbCBhcml0eTMsOSwsIkEsQSxBLEIiLDEsMSwxLDEKOCx1bnJlYWNoZWQsLCwiIiwwLDAsMCwwCjksYW5vbnltb3VzIHJldHVybiwzLCxCLDEsMCwxLDAKMTAsbnVsbGFibGUgcmV0dXJuLEZhbHNlLCwiIiwxLDAsMSwwCjExLHJlZiBzdHJ1Y3QgaWRlbnRpdHksMywsIiIsdW5hdmFpbGFibGUsdW5hdmFpbGFibGUsMSwwCg==";
    private static string Hash(byte[] x)=>Convert.ToHexString(SHA256.HashData(x));
    private static void Save(string name,string text){var dir=Environment.GetEnvironmentVariable("SEAMLESS_COUNTER_ENTRY_API_ARTIFACT_DIR");if(string.IsNullOrWhiteSpace(dir)){throw new InvalidOperationException("SEAMLESS_COUNTER_ENTRY_API_ARTIFACT_DIR is required");}Directory.CreateDirectory(dir);File.WriteAllText(Path.Combine(dir,name),text,new UTF8Encoding(false));}
    private static string[] Fields(string line){
        var fields=new List<string>();var text=new StringBuilder();var quoted=false;
        for(var i=0;i<line.Length;i++){
            var c=line[i];
            if(c=='"'){
                if(quoted&&i+1<line.Length&&line[i+1]=='"'){text.Append('"');i++;}else{quoted=!quoted;}
            }else if(c==','&&!quoted){fields.Add(text.ToString());text.Clear();}else{text.Append(c);}
        }
        if(quoted){throw new FormatException("unclosed CSV quote");}fields.Add(text.ToString());return fields.ToArray();
    }
    [Fact]
    public void LegacyAndEntryApisUseTheSameNewRuntime(){
        var input=Convert.FromBase64String(InputBase64);var csv=Convert.FromBase64String(CsvBase64);Assert.Equal(InputSHA,Hash(input));Assert.Equal(CsvSHA,Hash(csv));
        var source=Encoding.UTF8.GetString(input);var csvText=Encoding.UTF8.GetString(csv);var lines=csvText.Split(['\r','\n'],StringSplitOptions.RemoveEmptyEntries);Assert.Equal(13,lines.Length);
        Assert.Equal("Id,Scenario,Value,Exception,Trace,LegacyLogical,LegacyPhysical,V2Logical,V2Physical",lines[0]);
        Save("input.cs",source);Save("oracle.csv",csvText);
        const string identity="namespace EntryRepro; public static class RuntimeIdentity { public static string Get()=>typeof(global::Seamless.Linq.LinqCounter).Assembly.Location; }";
        var compilation=global::Test.SourceGenerator.GeneratorTestHelpers.CreateCompilation([("EntryInput.cs",source),("RuntimeIdentity.cs",identity)]).WithAssemblyName("CounterEntryApiFixture");
        using var pe=new MemoryStream();var emit=compilation.Emit(pe);var diagnostics=compilation.GetDiagnostics().Concat(emit.Diagnostics).Select(x=>new{x.Id,Severity=x.Severity.ToString(),Text=x.ToString()}).ToArray();
        Save("compilation.json",JsonSerializer.Serialize(new{inputSHA=Hash(input),csvSHA=Hash(csv),emittedSHA=Hash(pe.ToArray()),testRuntimePath=typeof(global::Seamless.Linq.LinqCounter).Assembly.Location,testRuntimeSHA=Hash(File.ReadAllBytes(typeof(global::Seamless.Linq.LinqCounter).Assembly.Location)),generatorPath=typeof(global::Seamless.FLinq.Generator.FLinqIncrementalGenerator).Assembly.Location,generatorSHA=Hash(File.ReadAllBytes(typeof(global::Seamless.FLinq.Generator.FLinqIncrementalGenerator).Assembly.Location)),emit.Success,diagnostics}));
        Assert.True(emit.Success);Assert.DoesNotContain(diagnostics,x=>x.Severity=="Error");
        var failures=new List<string>();var records=new List<object>();var context=new global::System.Runtime.Loader.AssemblyLoadContext("CounterEntryApiFixture",isCollectible:true);
        try{
            pe.Position=0;var assembly=context.LoadFromStream(pe);var run=assembly.GetType("EntryRepro.Probe")!.GetMethod("Run")!;var identityMethod=assembly.GetType("EntryRepro.RuntimeIdentity")!.GetMethod("Get")!;
            var actualPath=(string)identityMethod.Invoke(null,null)!;var actualSHA=Hash(File.ReadAllBytes(actualPath));var testSHA=Hash(File.ReadAllBytes(typeof(global::Seamless.Linq.LinqCounter).Assembly.Location));
            if(actualSHA!=testSHA){failures.Add("emitted fixture loaded a different runtime");}
            for(var id=0;id<12;id++){
                var fields=Fields(lines[id+1]);Assert.Equal(9,fields.Length);Assert.Equal(id.ToString(global::System.Globalization.CultureInfo.InvariantCulture),fields[0]);
                var expectedError=fields[3];
                // CSVの型名に、同じruntimeの既定例外messageを組み合わせる。製品出力から期待値を作らない。
                if(id==4){
                    expectedError=typeof(NullReferenceException).Name+":"+new NullReferenceException().Message;
                    Save("resolved-null-callback-oracle.json",JsonSerializer.Serialize(new{ExpectedError=expectedError,Source="BCL default NullReferenceException"}));
                }
                object[]? legacy=null;
                if(id!=11){legacy=(object[])run.Invoke(null,[id,true])!;}
                var entry=(object[])run.Invoke(null,[id,false])!;
                for(var mode=0;mode<2;mode++){
                    if(mode==0&&legacy is null){continue;}
                    var actual=entry;if(mode==0){actual=legacy!;}
                    var start=failures.Count;
                    if(!Equals(actual[0],fields[2])||!Equals(actual[1],expectedError)||!Equals(actual[2],fields[4])){failures.Add($"case {id} mode {mode}: value/exception/trace differs from CSV");}
                    var logical=fields[7];var physical=fields[8];if(mode==0){logical=fields[5];physical=fields[6];}
                    if(logical!="unavailable"&&((long)actual[3]).ToString(global::System.Globalization.CultureInfo.InvariantCulture)!=logical){failures.Add($"case {id} mode {mode}: logical count differs");}
                    if(physical!="unavailable"&&((long)actual[4]).ToString(global::System.Globalization.CultureInfo.InvariantCulture)!=physical){failures.Add($"case {id} mode {mode}: physical count differs");}
                    if(legacy is not null&&(!Equals(entry[0],legacy[0])||!Equals(entry[1],legacy[1])||!Equals(entry[2],legacy[2]))){failures.Add($"case {id}: legacy/v2 value/exception/trace mismatch");}
                    records.Add(new{id,mode,scenario=fields[1],actual,fixtureRuntimePath=actualPath,fixtureRuntimeSHA=actualSHA,failedChecks=failures.Skip(start).ToArray()});
                }
            }
        }finally{context.Unload();Save("observations.json",JsonSerializer.Serialize(new{expected=23,actual=records.Count,records,failures},new JsonSerializerOptions{WriteIndented=true}));}
        Assert.Equal(23,records.Count);Assert.Empty(failures);
    }
}
