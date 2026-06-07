using System;
using System.Reflection;

namespace UnityEngine;

/// <summary>
/// Compile-time shim for UnityEngine.ImageConversionModule.
///
/// Newer ADOFAI Unity assemblies can make UnityEngine.ImageConversionModule require
/// netstandard 2.1. Referencing that module directly from this .NET Framework mod
/// breaks the whole project with mscorlib/netstandard duplicate core types.
///
/// This shim keeps source calls such as texture.LoadImage(bytes),
/// ImageConversion.LoadImage(texture, bytes), and texture.EncodeToPNG() compiling.
/// At runtime it forwards to Unity's real ImageConversion methods by reflection.
/// </summary>
public static class ImageConversion {
    private static readonly Type RuntimeType =
        Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule") ??
        Type.GetType("UnityEngine.ImageConversion, UnityEngine.CoreModule") ??
        Type.GetType("UnityEngine.ImageConversion, UnityEngine");

    private static readonly MethodInfo LoadImage2 =
        FindMethod("LoadImage", typeof(bool), typeof(Texture2D), typeof(byte[]));

    private static readonly MethodInfo LoadImage3 =
        FindMethod("LoadImage", typeof(bool), typeof(Texture2D), typeof(byte[]), typeof(bool));

    private static readonly MethodInfo EncodeToPNGMethod =
        FindMethod("EncodeToPNG", typeof(byte[]), typeof(Texture2D));

    public static bool LoadImage(this Texture2D texture, byte[] data) =>
        LoadImage(texture, data, markNonReadable: false);

    public static bool LoadImage(this Texture2D texture, byte[] data, bool markNonReadable) {
        if(texture == null || data == null) {
            return false;
        }

        if(LoadImage3 != null) {
            return (bool)LoadImage3.Invoke(null, new object[] { texture, data, markNonReadable });
        }

        if(LoadImage2 != null) {
            return (bool)LoadImage2.Invoke(null, new object[] { texture, data });
        }

        throw new MissingMethodException(
            "UnityEngine.ImageConversion",
            "LoadImage(Texture2D, byte[])"
        );
    }

    public static byte[] EncodeToPNG(this Texture2D texture) {
        if(texture == null) {
            return null;
        }

        if(EncodeToPNGMethod != null) {
            return (byte[])EncodeToPNGMethod.Invoke(null, new object[] { texture });
        }

        throw new MissingMethodException(
            "UnityEngine.ImageConversion",
            "EncodeToPNG(Texture2D)"
        );
    }

    private static MethodInfo FindMethod(string name, Type returnType, params Type[] parameterTypes) {
        if(RuntimeType == null) {
            return null;
        }

        MethodInfo method = RuntimeType.GetMethod(
            name,
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: parameterTypes,
            modifiers: null
        );

        if(method == null || method.ReturnType != returnType) {
            return null;
        }

        return method;
    }
}
