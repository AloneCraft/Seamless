namespace Benchmark.TpcE.Compliance.Tests;

internal sealed class EGenFactAttribute:FactAttribute{
    public EGenFactAttribute(){
        if(Environment.GetEnvironmentVariable("TPCE_REQUIRE_EGEN")!="1"
            &&(!File.Exists(Path.Combine(EGenFixture.BuildDirectory,"EGenLoader.exe"))
                ||!File.Exists(Path.Combine(EGenFixture.BuildDirectory,"EGenHarness.dll"))
                ||!Directory.Exists(EGenFixture.InputDirectory))){
            this.Skip="EGenの実データ生成器・入力・ネイティブハーネスが未設定のため未検証です。";
        }
    }
}

internal sealed class NativeHarnessFactAttribute:FactAttribute{
    public NativeHarnessFactAttribute(){
        if(Environment.GetEnvironmentVariable("TPCE_REQUIRE_EGEN")!="1"
            &&!File.Exists(Path.Combine(EGenFixture.BuildDirectory,"EGenHarness.dll"))){
            this.Skip="EGenのネイティブハーネスが未設定のため未検証です。";
        }
    }
}

internal sealed class EGenTheoryAttribute:TheoryAttribute{
    public EGenTheoryAttribute(){this.Skip=new EGenFactAttribute().Skip;}
}
