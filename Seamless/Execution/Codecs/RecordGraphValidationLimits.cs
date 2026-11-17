using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
namespace Seamless.Execution.Codecs;

/// <summary>復元前の参照形式検査に適用する、データ全体の上限。</summary>
public sealed record RecordGraphValidationLimits{
    public static RecordGraphValidationLimits Default{get;}=new();
    public int MaximumBytes{get;init;}=(16<<20)-1;
    public int MaximumStringCharacters{get;init;}=65536;
    public int MaximumArrayElements{get;init;}=65536;
    public int MaximumNodes{get;init;}=1000000;
    public int MaximumDepth{get;init;}=64;
    internal void Check(){
        if(this.MaximumBytes<=0||this.MaximumStringCharacters<=0||this.MaximumArrayElements<=0||this.MaximumNodes<=0||this.MaximumDepth<=0){
            throw new ArgumentOutOfRangeException(nameof(RecordGraphValidationLimits));
        }
    }
}
