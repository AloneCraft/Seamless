using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
namespace Seamless.Execution.Growth;

public readonly struct Fixed:IStrategy<Fixed>{
    public static string ID=>"fixed";
    public static int Version=>1;
    public static Decision Decide(ref Fixed self,in Context context)=>new(Status.Keep,context.CurrentBytes,"The configured capacity is fixed.");
}
