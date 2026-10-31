using System.Security.Cryptography;
using System.Text;

namespace Seamless.Database.Rdbms;

internal static class CSharpIdentifier {
    private static readonly HashSet<string> Keywords = [
        "abstract","as","base","bool","break","byte","case","catch","char","checked","class","const",
        "continue","decimal","default","delegate","do","double","else","enum","event","explicit","extern",
        "false","finally","fixed","float","for","foreach","goto","if","implicit","in","int","interface",
        "internal","is","lock","long","namespace","new","null","object","operator","out","override","params",
        "private","protected","public","readonly","ref","return","sbyte","sealed","short","sizeof","stackalloc",
        "static","string","struct","switch","this","throw","true","try","typeof","uint","ulong","unchecked",
        "unsafe","ushort","using","virtual","void","volatile","while",
    ];

    public static string Normalize(string value) {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var builder = new StringBuilder(value.Length+1);
        for(var index=0;index<value.Length;index++) {
            var character = value[index];
            var valid = index==0
                ? character=='_'||char.IsLetter(character)
                : character=='_'||char.IsLetterOrDigit(character);
            builder.Append(valid?character:'_');
        }
        if(builder.Length==0||char.IsDigit(builder[0])) {
            builder.Insert(0,'_');
        }
        var identifier = builder.ToString();
        return Keywords.Contains(identifier)?$"@{identifier}":identifier;
    }

    public static string HashSuffix(string value) {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash.AsSpan(0,4));
    }
}

internal sealed class CSharpIdentifierScope {
    private readonly Dictionary<string,string> names = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,string> originals = new(StringComparer.Ordinal);

    public void Reserve(string identifier) {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        this.names.TryAdd(identifier,$"\\0{identifier}");
    }

    public string Get(string original) {
        return Get(original,original);
    }

    public string Get(string original,string identity) {
        if(this.originals.TryGetValue(identity,out var existing)) {
            return existing;
        }
        var normalized = CSharpIdentifier.Normalize(original);
        var candidate = normalized;
        if(this.names.TryGetValue(normalized,out var owner)&&owner!=identity) {
            candidate=$"{normalized}_{CSharpIdentifier.HashSuffix(original)}";
            while(this.names.ContainsKey(candidate)) {
                candidate=$"{candidate}_{CSharpIdentifier.HashSuffix(candidate)}";
            }
        }
        this.names.Add(candidate,identity);
        this.originals.Add(identity,candidate);
        return candidate;
    }
}
