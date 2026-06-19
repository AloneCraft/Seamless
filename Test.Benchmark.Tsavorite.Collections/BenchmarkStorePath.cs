namespace Test.Benchmark.Tsavorite.Collections;

internal static class BenchmarkStorePath {
    public static string Create<TBenchmark>(string segment) {
        var path = Path.Combine(Environment.CurrentDirectory,typeof(TBenchmark).Name,segment);
        if(Directory.Exists(path)) {
            try {
                Directory.Delete(path,recursive: true);
            }
            catch(IOException) {
            }
            catch(UnauthorizedAccessException) {
            }
        }

        Directory.CreateDirectory(path);
        return path;
    }
}
