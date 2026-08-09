namespace System {
    internal static class NetStandardStringExtensions {
        public static bool Contains(this string value,string comparisonValue,StringComparison comparisonType) =>
            value.IndexOf(comparisonValue,comparisonType)>=0;

        public static string Replace(this string value,string oldValue,string? newValue,StringComparison comparisonType) {
            if(oldValue is null) {
                throw new ArgumentNullException(nameof(oldValue));
            }
            if(oldValue.Length==0) {
                throw new ArgumentException("Replacement source cannot be empty.",nameof(oldValue));
            }
            var first=value.IndexOf(oldValue,comparisonType);
            if(first<0) {
                return value;
            }
            var replacement=newValue??string.Empty;
            var builder=new Text.StringBuilder(value.Length);
            var start=0;
            do {
                builder.Append(value,start,first-start);
                builder.Append(replacement);
                start=first+oldValue.Length;
                first=value.IndexOf(oldValue,start,comparisonType);
            } while(first>=0);
            builder.Append(value,start,value.Length-start);
            return builder.ToString();
        }
    }
}

namespace System.Runtime.CompilerServices {
    internal static class IsExternalInit {
    }

    [AttributeUsage(AttributeTargets.All,Inherited=false)]
    internal sealed class RequiredMemberAttribute:Attribute {
    }

    [AttributeUsage(AttributeTargets.All,Inherited=false)]
    internal sealed class CompilerFeatureRequiredAttribute:Attribute {
        public CompilerFeatureRequiredAttribute(string featureName) {
            this.FeatureName=featureName;
        }

        public string FeatureName { get; }
        public bool IsOptional { get; set; }
    }
}