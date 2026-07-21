using System;

namespace Jint.Runtime.Interop.Attributes;

             
                                                                 
   
                                                                             
                                                                               
                                                                               
                                                                                 
              
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class RawReturnAttribute : Attribute {
}

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum, AllowMultiple = false, Inherited = false)]
public sealed class AliasAttribute : Attribute {
    public string Name { get; }

    public AliasAttribute(string name) => Name = name;
}

