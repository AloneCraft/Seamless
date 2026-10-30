namespace TestSeamless.Tests;

internal static class TestTempDirectory{
    public static string NewPath(string prefix){
        var root=Environment.GetEnvironmentVariable("SEAMLESS_COMPARISON_TEMP_ROOT");
        if(string.IsNullOrWhiteSpace(root)){
            if(Directory.Exists(@"D:\")){root=@"D:\Seamless\Comparison\Temp";}else{root=Path.GetTempPath();}
        }
        return Path.Combine(root,prefix+Guid.NewGuid().ToString("N"));
    }
}
