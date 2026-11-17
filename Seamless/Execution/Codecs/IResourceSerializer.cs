using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
namespace Seamless.Execution.Codecs;

/// <summary>所有ストアの予算へ束縛するコーデックを作る静的 serializer 契約。</summary>
public interface IResourceSerializer<T>:global::Seamless.Collections.Serializers.IStrategy<T>{
    static abstract IAdapter<T> CreateCodec(ResourceBudget budget);
}
