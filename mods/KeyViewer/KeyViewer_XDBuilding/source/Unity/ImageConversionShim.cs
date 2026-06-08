using System;
using System.Linq;
using System.Reflection;

namespace UnityEngine;

public static class KeyViewerImageConversionShim {
    private static MethodInfo loadImageWithReadableFlag;
    private static MethodInfo loadImage;

    public static bool LoadImage(this Texture2D texture, byte[] data) {
        if(texture == null) throw new ArgumentNullException(nameof(texture));
        if(data == null) throw new ArgumentNullException(nameof(data));

        Type imageConversionType = GetImageConversionType();
        if(imageConversionType == null) throw new MissingMethodException("UnityEngine.ImageConversion", "LoadImage");

        loadImageWithReadableFlag ??= imageConversionType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == "LoadImage"
                && m.GetParameters().Length == 3
                && m.GetParameters()[0].ParameterType == typeof(Texture2D)
                && m.GetParameters()[1].ParameterType == typeof(byte[])
                && m.GetParameters()[2].ParameterType == typeof(bool));

        if(loadImageWithReadableFlag != null) {
            return (bool)loadImageWithReadableFlag.Invoke(null, new object[] { texture, data, false });
        }

        loadImage ??= imageConversionType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == "LoadImage"
                && m.GetParameters().Length == 2
                && m.GetParameters()[0].ParameterType == typeof(Texture2D)
                && m.GetParameters()[1].ParameterType == typeof(byte[]));

        if(loadImage == null) throw new MissingMethodException("UnityEngine.ImageConversion", "LoadImage");
        return (bool)loadImage.Invoke(null, new object[] { texture, data });
    }

    private static Type GetImageConversionType() {
        Type direct = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule", false);
        if(direct != null) return direct;

        return AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("UnityEngine.ImageConversion", false))
            .FirstOrDefault(t => t != null);
    }
}
