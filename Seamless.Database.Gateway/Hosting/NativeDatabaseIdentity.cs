using System.Reflection;
using System.Security.Cryptography;

namespace Seamless.Database.Gateway.Hosting;

internal static class NativeDatabaseIdentity{
    internal static string Fingerprint(Assembly assembly){
        var fingerprints=assembly.DefinedTypes.Select(static type=>type.GetField("GeneratedSchemaFingerprint",BindingFlags.Public|BindingFlags.Static|BindingFlags.DeclaredOnly))
            .Where(static field=>field is{IsLiteral:true}&&field.FieldType==typeof(string)).Select(static field=>field!.GetRawConstantValue() as string)
            .OfType<string>().Distinct(StringComparer.Ordinal).ToArray();
        if(fingerprints.Length>1){throw new InvalidDataException("A database definition assembly has multiple schema fingerprints.");}
        if(fingerprints.Length==1){return fingerprints[0];}
        // 手書きDBにもDLLの版を固定する。生成SQLスキーマの識別は上で引き継ぐ。
        using var file=File.OpenRead(assembly.Location);
        return "assembly-sha256:"+Convert.ToHexString(SHA256.HashData(file));
    }
}
