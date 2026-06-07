using System;

namespace Jint.Runtime.Interop.Attributes;

/// <summary>
/// Compatibility attributes for Overlayer 3.49.2 scripting code.
///
/// The uploaded 3.49.2 source used attributes from a Jint build that exposed
/// Jint.Runtime.Interop.Attributes. The normal NuGet Jint package used by this
/// merged tree does not expose that namespace, so these tiny marker attributes
/// keep the source compiling without reintroducing a broken local Jint.dll path.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class RawReturnAttribute : Attribute {
}

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum, AllowMultiple = false, Inherited = false)]
public sealed class AliasAttribute : Attribute {
    public string Name { get; }

    public AliasAttribute(string name) => Name = name;
}

