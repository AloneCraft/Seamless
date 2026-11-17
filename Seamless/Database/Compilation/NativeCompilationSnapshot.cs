using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Seamless.Database.Rdbms;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

namespace Seamless.Database.Compilation;

public sealed record NativeSourceFile(string Path,string Content);
public sealed record NativeAssemblyArtifact(string SourceFingerprint,ImmutableArray<byte>AssemblyBytes,ImmutableArray<NativeDiagnostic>Diagnostics);

/// <summary>ファイルとシステムDBのどちらからも供給できる、版を固定した解析入力。</summary>
public sealed class NativeCompilationSnapshot{
    private NativeCompilationSnapshot(string revision,IEnumerable<NativeSourceFile>sources,IEnumerable<string>references,IEnumerable<string>defines,bool checkOverflow,string?originSnapshotHash){
        ArgumentException.ThrowIfNullOrWhiteSpace(revision);
        this.Revision=revision;
        this.Defines=defines.Order(StringComparer.Ordinal).ToImmutableArray();
        this.CheckOverflow=checkOverflow;
        this.OriginSnapshotHash=originSnapshotHash;
        this.Sources=sources.OrderBy(s=>s.Path,StringComparer.Ordinal).ToImmutableArray();
        if(this.Sources.Length==0||this.Sources.Select(s=>s.Path).Distinct(StringComparer.Ordinal).Count()!=this.Sources.Length){throw new ArgumentException("Source paths must be nonempty and unique.",nameof(sources));}
        using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        void Add(string text){var bytes=Encoding.UTF8.GetBytes(text);hash.AppendData(BitConverter.GetBytes(bytes.Length));hash.AppendData(bytes);}
        Add("Seamless.NativeSnapshot.V1|CSharp14|NullableEnable");Add(revision);Add(checkOverflow.ToString());Add(originSnapshotHash??"");
        foreach(var symbol in this.Defines){Add(symbol);}
        foreach(var source in this.Sources){ArgumentException.ThrowIfNullOrWhiteSpace(source.Path);ArgumentNullException.ThrowIfNull(source.Content);Add(source.Path);Add(source.Content);}
        var images=ImmutableArray.CreateBuilder<ImmutableArray<byte>>();
        foreach(var path in references.Select(global::System.IO.Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase)){
            var bytes=File.ReadAllBytes(path);Add(global::System.IO.Path.GetFileName(path));hash.AppendData(SHA256.HashData(bytes));images.Add(ImmutableArray.CreateRange(bytes));
        }
        this.ReferenceImages=images.ToImmutable();
        this.Fingerprint=Convert.ToHexString(hash.GetHashAndReset());
    }

    public string Revision{get;}
    public string Fingerprint{get;}
    public string?OriginSnapshotHash{get;}
    public bool CheckOverflow{get;}
    public ImmutableArray<string>Defines{get;}
    public ImmutableArray<NativeSourceFile>Sources{get;}
    public ImmutableArray<ImmutableArray<byte>>ReferenceImages{get;}

    public static NativeCompilationSnapshot Create(string revision,IEnumerable<NativeSourceFile>sources,IEnumerable<string>?references=null,IEnumerable<string>?defines=null,bool checkOverflow=false,string?originSnapshotHash=null){
        var platform=global::Seamless.Execution.CompilationReferences.Paths;
        return new(revision,sources,platform.Concat([global::Seamless.Execution.CompilationReferences.PathFor(typeof(global::Seamless.Database.DatabaseContainer).Assembly),global::Seamless.Execution.CompilationReferences.PathFor(typeof(global::Seamless.Collections.FixedText<>).Assembly)]).Concat(references??[]),defines??[],checkOverflow,originSnapshotHash);
    }
}
