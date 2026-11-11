using System;
using System.Reflection;
using System.Globalization;

namespace Overlayer.Utils;

public static class StringConverter {
    public static sbyte ToInt8(string s) =>
        sbyte.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out sbyte value) ? value : (sbyte)0;
    public static string FromInt8(sbyte s) => s.ToString();
    public static short ToInt16(string s) =>
        short.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out short value) ? value : (short)0;
    public static string FromInt16(short s) => s.ToString();
    public static int ToInt32(string s) =>
        int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : (int)0;
    public static string FromInt32(int s) => s.ToString();
    public static long ToInt64(string s) =>
        long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out long value) ? value : (long)0;
    public static string FromInt64(long s) => s.ToString();
    public static byte ToUInt8(string s) =>
        byte.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out byte value) ? value : (byte)0;
    public static string FromUInt8(byte s) => s.ToString();
    public static ushort ToUInt16(string s) =>
        ushort.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out ushort value) ? value : (ushort)0;
    public static string FromUInt16(ushort s) => s.ToString();
    public static uint ToUInt32(string s) =>
        uint.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint value) ? value : (uint)0;
    public static string FromUInt32(uint s) => s.ToString();
    public static ulong ToUInt64(string s) =>
        ulong.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong value) ? value : (ulong)0;
    public static string FromUInt64(ulong s) => s.ToString();
    public static double ToDouble(string s) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : 0;
    public static string FromDouble(double s) => s.ToString(CultureInfo.InvariantCulture);
    public static float ToFloat(string s) =>
        float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ? value : 0;
    public static string FromFloat(float s) => s.ToString(CultureInfo.InvariantCulture);
    public static T ToEnum<T>(string s) where T : Enum => EnumHelper<T>.Parse(s);
    public static string FromEnum<T>(T e) where T : Enum => e.ToString();
    public static bool ToBoolean(string s) => string.Equals(s, "true", StringComparison.OrdinalIgnoreCase);
    public static string FromBoolean(bool b) => b.ToString();
    public static MethodInfo GetToConverter(Type numType) {
        if(numType == typeof(sbyte)) {
            return TInt8;
        } else if(numType == typeof(short)) {
            return TInt16;
        } else if(numType == typeof(int)) {
            return TInt32;
        } else if(numType == typeof(long)) {
            return TInt64;
        } else if(numType == typeof(byte)) {
            return TUInt8;
        } else if(numType == typeof(ushort)) {
            return TUInt16;
        } else if(numType == typeof(uint)) {
            return TUInt32;
        } else if(numType == typeof(ulong)) {
            return TUInt64;
        } else if(numType == typeof(float)) {
            return TFloat;
        } else if(numType == typeof(double)) {
            return TDouble;
        } else if(numType == typeof(bool)) {
            return TBool;
        } else if(typeof(Enum).IsAssignableFrom(numType)) {
            return TEnum.MakeGenericMethod(numType);
        } else {
            return null;
        }
    }
    public static MethodInfo GetFromConverter(Type numType) {
        if(numType == typeof(sbyte)) {
            return FInt8;
        } else if(numType == typeof(short)) {
            return FInt16;
        } else if(numType == typeof(int)) {
            return FInt32;
        } else if(numType == typeof(long)) {
            return FInt64;
        } else if(numType == typeof(byte)) {
            return FUInt8;
        } else if(numType == typeof(ushort)) {
            return FUInt16;
        } else if(numType == typeof(uint)) {
            return FUInt32;
        } else if(numType == typeof(ulong)) {
            return FUInt64;
        } else if(numType == typeof(float)) {
            return FFloat;
        } else if(numType == typeof(double)) {
            return FDouble;
        } else if(numType == typeof(bool)) {
            return FBool;
        } else if(typeof(Enum).IsAssignableFrom(numType)) {
            return FEnum.MakeGenericMethod(numType);
        } else {
            return FObject;
        }
    }
    public static string FromObject(object s) => s?.ToString();
    public static string ToObject(string s) => s;
    public static readonly MethodInfo TInt8 = typeof(StringConverter).GetMethod("ToInt8");
    public static readonly MethodInfo TInt16 = typeof(StringConverter).GetMethod("ToInt16");
    public static readonly MethodInfo TInt32 = typeof(StringConverter).GetMethod("ToInt32");
    public static readonly MethodInfo TInt64 = typeof(StringConverter).GetMethod("ToInt64");
    public static readonly MethodInfo TUInt8 = typeof(StringConverter).GetMethod("ToUInt8");
    public static readonly MethodInfo TUInt16 = typeof(StringConverter).GetMethod("ToUInt16");
    public static readonly MethodInfo TUInt32 = typeof(StringConverter).GetMethod("ToUInt32");
    public static readonly MethodInfo TUInt64 = typeof(StringConverter).GetMethod("ToUInt64");
    public static readonly MethodInfo TFloat = typeof(StringConverter).GetMethod("ToFloat");
    public static readonly MethodInfo TDouble = typeof(StringConverter).GetMethod("ToDouble");
    public static readonly MethodInfo TEnum = typeof(StringConverter).GetMethod("ToEnum");
    public static readonly MethodInfo TObject = typeof(StringConverter).GetMethod("ToObject");
    public static readonly MethodInfo TBool = typeof(StringConverter).GetMethod("ToBoolean");
    public static readonly MethodInfo FInt8 = typeof(StringConverter).GetMethod("FromInt8");
    public static readonly MethodInfo FInt16 = typeof(StringConverter).GetMethod("FromInt16");
    public static readonly MethodInfo FInt32 = typeof(StringConverter).GetMethod("FromInt32");
    public static readonly MethodInfo FInt64 = typeof(StringConverter).GetMethod("FromInt64");
    public static readonly MethodInfo FUInt8 = typeof(StringConverter).GetMethod("FromUInt8");
    public static readonly MethodInfo FUInt16 = typeof(StringConverter).GetMethod("FromUInt16");
    public static readonly MethodInfo FUInt32 = typeof(StringConverter).GetMethod("FromUInt32");
    public static readonly MethodInfo FUInt64 = typeof(StringConverter).GetMethod("FromUInt64");
    public static readonly MethodInfo FFloat = typeof(StringConverter).GetMethod("FromFloat");
    public static readonly MethodInfo FDouble = typeof(StringConverter).GetMethod("FromDouble");
    public static readonly MethodInfo FEnum = typeof(StringConverter).GetMethod("FromEnum");
    public static readonly MethodInfo FObject = typeof(StringConverter).GetMethod("FromObject");
    public static readonly MethodInfo FBool = typeof(StringConverter).GetMethod("FromBoolean");
}
