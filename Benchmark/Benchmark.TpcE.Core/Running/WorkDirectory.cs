namespace Benchmark.TpcE.Core;
/// <summary>出力先(既定は リポジトリ/Work/TpcE。Work/ は git 管理外)。</summary>
public static class WorkDirectory{
    /// <summary>実行ファイルの場所から親へたどって Seamless.slnx のあるフォルダを探す。無ければ今のフォルダ。</summary>
    public static string RepositoryRoot(){
        var directory=new DirectoryInfo(AppContext.BaseDirectory);
        while(directory is not null){
            if(File.Exists(Path.Combine(directory.FullName,"Seamless.slnx"))){
                return directory.FullName;
            }
            directory=directory.Parent;
        }
        return Environment.CurrentDirectory;
    }
    public static string Resolve(string? configured){
        if(!string.IsNullOrWhiteSpace(configured)){
            return Path.GetFullPath(configured);
        }
        var testRoot=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT");
        if(!string.IsNullOrWhiteSpace(testRoot)){
            if(!Path.IsPathFullyQualified(testRoot)){throw new InvalidOperationException("SEAMLESS_TEST_DATA_ROOT must be an absolute path.");}
            return Path.Combine(testRoot,"TpcE");
        }
        return Path.Combine(RepositoryRoot(),"Work","TpcE");
    }
    /// <summary>実行 ID(例 20261010-153012-s1-run)。同じ秒に重なったら連番を足す。</summary>
    public static string CreateRunDirectory(string work,RunOptions options){
        var stamp=DateTime.Now.ToString("yyyyMMdd-HHmmss",System.Globalization.CultureInfo.InvariantCulture);
        var name=$"{stamp}-s{options.Seed}-{options.Command}";
        var path=Path.Combine(work,name);
        var suffix=1;
        while(Directory.Exists(path)){
            suffix++;
            path=Path.Combine(work,name+"-"+suffix.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        Directory.CreateDirectory(path);
        return path;
    }
}
