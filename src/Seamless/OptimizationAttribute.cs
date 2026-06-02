using System;

namespace Seamless.Common {

    [AttributeUsage(AttributeTargets.Assembly|AttributeTargets.Class|AttributeTargets.Method,AllowMultiple=false,Inherited=false)]
    public sealed class OptimizationAttribute:Attribute {
        public OptimizationAttribute(OptimizationMode Mode) {
            this.Mode=Mode;
        }
        public OptimizationMode Mode {get;}
    }
}
