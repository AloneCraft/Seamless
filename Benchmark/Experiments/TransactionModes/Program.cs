namespace TransactionModes;

public static class Program{
    public static int Main(string[] args){
        try{
            if(args.Length<3){throw new ArgumentException("accept <data-root> <report.json>");}
            if(args[0]=="crash-child"){
                var mode=Enum.Parse<Mode>(args[2]);var phase=Enum.Parse<Phase>(args[3]);using var engine=new Engine(mode,args[1],true,64);
                using var tx=engine.Begin();engine.Inject=current=>{if(current==phase){File.WriteAllText(Path.Combine(args[1],"crash-marker.json"),System.Text.Json.JsonSerializer.Serialize(new{tx.State.Id,phase}));System.Diagnostics.Process.GetCurrentProcess().Kill();}};
                tx.Set(1,55);tx.Set(1,55,table:1);tx.Commit();throw new InvalidOperationException("Crash injection missed");
            }
            if(args[0]=="accept"){Acceptance.Run(args[1],args[2]);return 0;}
            if(args[0]=="bench"){Benchmarks.Run(args[1],args[2]);return 0;}
            throw new ArgumentException("Unknown command: "+args[0]);
        }catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
