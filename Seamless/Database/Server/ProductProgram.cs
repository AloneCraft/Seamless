namespace Seamless.Database.Server;

internal static class ProductProgram{
    public static async Task<int> Main(string[] args){
        if(args.Length>0&&args[0]=="database"){
            return await global::Seamless.Database.Tool.Program.Main(args);
        }
        if(args.Length==0||args[0] is "--gateway" or "--generation"){
            await Program.Main(args);
            return Environment.ExitCode;
        }
        return await global::Seamless.Management.Server.Program.Main(args);
    }
}
