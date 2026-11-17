using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
namespace Seamless.Execution;

/// <summary>測定DBを必要としない、候補の証拠と利用側の互換条件。</summary>
public sealed record ProfileEvidence(string LibraryVersion,string CodecVersion,string MeasurementVersion,string ComparisonPolicyVersion){
    public static ProfileEvidence Current{get;}=new("execution-source-v3:"+ProfileBuildIdentity.Value,"1","scalable-content-v2","noise-v1");
}
