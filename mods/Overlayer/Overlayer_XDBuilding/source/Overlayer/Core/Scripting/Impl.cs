using DG.Tweening;
using Discord;
using HarmonyLib;
using Jint;
using Jint.Native;
using Jint.Native.Function;
using Jint.Runtime.Interop;
using Jint.Runtime.Interop.Attributes;
using Newtonsoft.Json.Linq;
using Overlayer.Core.Patches;
using Overlayer.Core.Scripting.JSNet.API;
using Overlayer.Core.Scripting.JSNet.Utils;
using Overlayer.Models;
using Overlayer.Tags.Attributes;
using Overlayer.Unity;
using Overlayer.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using TMPro;
using UnityEngine;
using Expression = Overlayer.Tags.Expression;

namespace Overlayer.Core.Scripting;

public static class Impl {
    public static void Initialize() {
        InitializeWrapperAssembly();
        alreadyExecutedScripts = [];
        jsTypes = [];
        harmony = new Harmony("Overlayer.Scripting.Impl");
        globalVariables = [];
        registeredCustomTags = [];
        customJudgeTexts = [];
        customJudgeConfigs = [];
        judgeTextShowPatchInjected = false;
        anyKeyCallbacks = [];
        anyKeyDownCallbacks = [];
        keyCallbacks = [];
        keyUpCallbacks = [];
        keyDownCallbacks = [];
    }
    public static void Release() {
        if(registeredCustomTags != null) {
            if(Scripting.JSApi != null) {
                var registeredNames = new HashSet<string>(
                    registeredCustomTags
                );

                Scripting.JSApi.Methods.RemoveAll(
                    method => registeredNames.Contains(
                        method.Item1.Name
                    )
                );
            }

            registeredCustomTags.ForEach(
                TagManager.RemoveTag
            );
        }

        registeredCustomTags = null;
        globalVariables = null;
        customJudgeTexts = null;
        customJudgeConfigs = null;
        judgeTextShowPatchInjected = false;
        anyKeyCallbacks = null;
        anyKeyDownCallbacks = null;
        keyCallbacks = null;
        keyUpCallbacks = null;
        keyDownCallbacks = null;
        harmony?.UnpatchAll(harmony.Id);
        harmony = null;
        jsTypes = null;
        Adofai.autoTextInjected = false;
        alreadyExecutedScripts = null;
        DisposeWrapperAssembly();
    }
    public static void Reload() {
        Release();
        Initialize();
    }
    #region Impl APIs
    [Api("use")]
    public static void Use(Engine engine, params string[] tagsOrProxies) {
        string currentScript = Path.GetFileName(Scripting.CurrentExecutingScriptPath);
        for(int i = 0; i < tagsOrProxies.Length; i++) {
            var tagOrProxy = tagsOrProxies[i];
            if(TagManager.GetTag(tagOrProxy) is OverlayerTag tag) {
                LazyPatchManager.PatchAll(tagOrProxy).ForEach(lp => lp.Locked = true);
                engine.SetValue(
                    tagOrProxy,
                    Api.CreateMethodFunction(
                        engine,
                        tagOrProxy,
                        tag.Tag.GetterOriginal
                    )
                );
                Main.Logger.Log($"[{currentScript}] Using '{tagOrProxy}' Tag.");
            } else {
                bool isUri = false;
                if(!isUri) {
                    if(!tagOrProxy.EndsWith(".js")) {
                        tagOrProxy += ".js";
                    }

                    if(!File.Exists(tagOrProxy)) {
                        tagOrProxy = Path.Combine(Scripting.ScriptPath, tagOrProxy);
                    }

                    if(!File.Exists(tagOrProxy)) {
                        throw new FileNotFoundException(tagsOrProxies[i]);
                    }
                }
                var name = Path.GetFileName(tagOrProxy);
                var isProxy = false;
                var time = MiscUtils.MeasureTime(() => {
                    var code = File.ReadAllText(tagOrProxy);
                    if(isProxy = code.StartsWith("// [Overlayer.Scripting JS Wrapper]")) {
                        foreach(var (alias, member) in Scripting.ImportJSProxy(code)) {
                            if(member is Type t) {
                                engine.SetValue(
                                    alias,
                                    TypeReference.CreateTypeReference(
                                        engine,
                                        t
                                    )
                                );
                            }
                            else if(member is MethodInfo m) {
                                engine.SetValue(
                                    alias,
                                    Api.CreateMethodFunction(
                                        engine,
                                        alias,
                                        m
                                    )
                                );
                            }
                            else {
                                Main.Logger.Log(
                                    $"[{currentScript}] Proxy member " +
                                    $"'{alias}' could not be resolved; skipped."
                                );
                            }
                        }
                    } else {
                        engine.Execute(JSUtils.RemoveImports(code));
                        alreadyExecutedScripts.Add(tagOrProxy);
                    }
                });
                if(isProxy) {
                    Main.Logger.Log($"[{currentScript}] Using '{Path.GetFileName(tagOrProxy)}' Proxy. ({time.TotalMilliseconds}ms)");
                } else {
                    Main.Logger.Log($"Force Executed \"{name}\" Script Successfully. ({time.TotalMilliseconds}ms)");
                }
            }
        }
    }
    [Api("exportTexts")]
    public static void ExportTexts(string path, OverlayerText[] texts) => File.WriteAllBytes(path, Scripting.ExportTexts(texts));
    [Api("importTexts")]
    public static OverlayerText[] ImportTexts(byte[] rawTexts, OverlayerProfile profile = null) => Scripting.ImportTexts(rawTexts, profile).ToArray();
    [Api("deleteThis")]
    public static void DeleteThis(Engine engine) => File.Delete(Scripting.CurrentExecutingScriptPath);
    [Api("generateProxy")]
    public static void GenerateProxy(string fileName, Type[] types, MethodInfo[] methods) {
        var generated = Scripting.GenerateJSProxy(
            proxyTypes: types?.Select(t => (t.Name, t)),
            proxyStaticMethods: methods?.Select(m => (m.Name, m)));
        File.WriteAllText(fileName, generated);
    }
    [Api("generateProxyWithAlias")]
    public static void GenerateProxyWithAlias(string fileName, Type[] types, string[] typeAliases, MethodInfo[] methods, string[] methodAliases) {
        List<(string, Type)> tt = [];
        List<(string, MethodInfo)> mm = [];
        for(int i = 0; i < types.Length; i++) {
            tt.Add((typeAliases[i], types[i]));
        }

        for(int i = 0; i < methods.Length; i++) {
            mm.Add((methodAliases[i], methods[i]));
        }

        var generated = Scripting.GenerateJSProxy(
            proxyTypes: tt,
            proxyStaticMethods: mm);
        File.WriteAllText(fileName, generated);
    }
    [RawReturn]
    [Api("resolveClrType")]
    public static Type ResolveType(Engine engine, string clrType) => MiscUtils.TypeByName(clrType);
    [RawReturn]
    [Api("resolveClrMethod")]
    public static MethodInfo ResolveMethod(Engine engine, string clrType, string name) => MiscUtils.TypeByName(clrType)?.GetMethod(name, (BindingFlags)15420);

    [Api("resolve")]
    public static TypeReference Resolve(Engine engine, string clrType) {
        if(jsTypes.TryGetValue(engine, out var dict)) {
            return dict.TryGetValue(clrType, out var t)
                ? t
                : (dict[clrType] = TypeReference.CreateTypeReference(engine, MiscUtils.TypeByName(clrType)));
        }

        dict = jsTypes[engine] = [];
        return dict[clrType] = TypeReference.CreateTypeReference(engine, MiscUtils.TypeByName(clrType));
    }
    [Api("getAttr")]
    public static object GetAttr(object obj, string accessor = "") => OverlayerTag.RuntimeAccess(obj, accessor);
    [Api("setAttr")]
    public static bool SetAttr(object obj, string accessor = "", object value = null) {
        if(obj == null) {
            return false;
        }

        Type objType = obj is Type t ? t : obj.GetType();
        accessor = accessor.TrimEnd('.');
        object result = obj;
        string[] accessors = accessor.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
        if(accessors.Length < 1) {
            return false;
        }

        MemberInfo lastMember = null;
        Type type = objType;
        for(int i = 0; i < accessors.Length; i++) {
            MemberInfo[] members = type.GetMembers((BindingFlags)15420);
            var ignoreCase = type.GetCustomAttribute<IgnoreCaseAttribute>() != null;
            var foundMembers = ignoreCase ? members.Where(m => m.Name.Equals(accessors[i], StringComparison.OrdinalIgnoreCase)) : members.Where(m => m.Name == accessors[i]);
            lastMember = foundMembers.Where(m => m.MemberType is MemberTypes.Field or MemberTypes.Property).FirstOrDefault();
            if(i != accessors.Length - 1) {
                result = lastMember is FieldInfo f ? f.GetValue(result) : lastMember is PropertyInfo p ? p.GetValue(result) : null;
            } else {
                if(lastMember is FieldInfo f) {
                    if(value != null && value.GetType() != f.FieldType) {
                        value = Convert.ChangeType(value, f.FieldType);
                    }

                    f.SetValue(result, value);
                    return true;
                } else if(lastMember is PropertyInfo p && p.GetSetMethod(true) != null) {
                    if(value != null && value.GetType() != p.PropertyType) {
                        value = Convert.ChangeType(value, p.PropertyType);
                    }

                    p.SetValue(result, value);
                    return true;
                }
                return false;
            }
            if(result == null) {
                return false;
            }

            type = result.GetType();
        }
        return false;
    }
    [Api("wrapToJSObject")]
    public static JsValue WrapToJSObject(Engine engine, object obj) => JsValue.FromObject(engine, obj);
    [Api("unwrapFromJSObject")]
    public static object UnwrapFromJSObject(JsValue value) => value.ToObject();
    [Api("getScriptPath")]
    public static string GetScriptPath(string extra = "") => Path.Combine(Scripting.ScriptPath, extra);
    [Api("getClrGenericTypeName")]
    public static string GetGenericClrTypeString(Engine engine, string genericType, string[] genericArgs) {
        static string AggregateGenericArgs(Type[] types) {
            StringBuilder sb = new();
            int length = types.Length;
            for(int i = 0; i < length; i++) {
                Type type = types[i];
                sb.Append($"[{type?.FullName}, {type?.Assembly.GetName().Name}]");
                if(i < length - 1) {
                    sb.Append(',');
                }
            }
            return sb.ToString();
        }
        var t = MiscUtils.TypeByName(genericType);
        var args = genericArgs.Select(MiscUtils.TypeByName);
        return $"{t?.FullName}[{AggregateGenericArgs(args.ToArray())}]";
    }
    [Api("getGlobalVariable")]
    public static JsValue GetGlobalVariable(
        Engine engine,
        string name
    ) {
        if(!globalVariables.TryGetValue(
            name,
            out object value
        )) {
            return JsValue.Undefined;
        }

        return value is JsValue jsValue
            ? jsValue
            : JsValue.FromObject(engine, value);
    }

    [Api("setGlobalVariable")]
    public static JsValue SetGlobalVariable(
        Engine engine,
        string name,
        JsValue value
    ) {
        globalVariables[name] = value;
        return value;
    }
    [Api("registerTag")]
    public static void RegisterTag(Engine engine, string name, JsValue func, bool notplaying, string tooltip) {
        if(func is not Function fi) {
            return;
        }

        FIWrapper wrapper = new(fi);
        var tagWrapper = GenerateTagWrapper(wrapper);
        var tuple = (new ApiAttribute(name), tagWrapper);
        Scripting.JSApi.Methods.Add(tuple);
        Expression.expressions.Clear();
        string pathOrScript = Scripting.CurrentExecutingScriptPath == "Sandbox.js" ? Scripting.CurrentExecutingScript : Scripting.CurrentExecutingScriptPath;
        TagManager.SetTag(new ScriptTag(pathOrScript, tagWrapper, new Tags.Attributes.TagAttribute(name) { NotPlaying = notplaying }));
        StaticCoroutine.Queue(StaticCoroutine.SyncRunner(ProfileManager.Refresh));
        registeredCustomTags.Add(name);
        if(tooltip != null) {
            TagDesc.Desc[name.ToUpperInvariant()] = tooltip;
        }
        Main.Logger.Log($"Registered Tag \"{name}\" (NotPlaying:{notplaying})");
    }
    [Api("unregisterTag")]
    public static void UnregisterTag(Engine engine, string name) {
        Scripting.JSApi.Methods.RemoveAll(t => t.Item1.Name == name);
        Expression.expressions.Clear();
        TagManager.RemoveTag(name);
        TagDesc.Desc.Remove(name.ToUpperInvariant());
        StaticCoroutine.Queue(StaticCoroutine.SyncRunner(ProfileManager.Refresh));
    }
    /*[Api("prefix")]
    public static bool Prefix(Engine engine, string typeColonMethodName, JsValue patch)
    {
        if (!(patch is Function func)) return false;
        var typemethod = typeColonMethodName.Split2(':');
        var target = MiscUtils.TypeByName(typemethod[0]).GetMethod(typemethod[1], (BindingFlags)15422);
        target ??= MiscUtils.TypeByName(typemethod[0]).GetMethod(typemethod[1], (BindingFlags)15420);
        if (target == null)
        {
            Main.Logger.Log($"{typeColonMethodName} Cannot Be Found.");
            return false;
        }
        var wrap = func.Wrap(target, true);
        if (wrap == null)
            return false;
        harmony.Patch(target, new HarmonyMethod(wrap));
        return true;
    }
    [Api("postfix")]
    public static bool Postfix(Engine engine, string typeColonMethodName, JsValue patch)
    {
        if (!(patch is Function func)) return false;
        var typemethod = typeColonMethodName.Split2(':');
        var target = MiscUtils.TypeByName(typemethod[0]).GetMethod(typemethod[1], (BindingFlags)15422);
        target ??= MiscUtils.TypeByName(typemethod[0]).GetMethod(typemethod[1], (BindingFlags)15420);
        if (target == null)
        {
            Main.Logger.Log($"{typeColonMethodName} Cannot Be Found.");
            return false;
        }
        var wrap = func.Wrap(target, false);
        if (wrap == null)
            return false;
        harmony.Patch(target, postfix: new HarmonyMethod(wrap));
        return true;
    }
    [Api("transpiler")]
    public static bool Transpiler(Engine engine, string typeColonMethodName, JsValue patch)
    {
        if (!(patch is Function func)) return false;
        var typemethod = typeColonMethodName.Split2(':');
        var target = MiscUtils.TypeByName(typemethod[0]).GetMethod(typemethod[1], (BindingFlags)15422);
        target ??= MiscUtils.TypeByName(typemethod[0]).GetMethod(typemethod[1], (BindingFlags)15420);
        if (target == null)
        {
            Main.Logger.Log($"{typeColonMethodName} Cannot Be Found.");
            return false;
        }
        var wrap = func.WrapTranspiler();
        if (wrap == null)
            return false;
        harmony.Patch(target, transpiler: new HarmonyMethod(wrap));
        return true;
    }
    [Api("prefixWithArgs")]
    public static bool PrefixWithArgs(Engine engine, string typeColonMethodName, string[] argumentClrTypes, JsValue patch)
    {
        if (!(patch is Function func)) return false;
        var typemethod = typeColonMethodName.Split2(':');
        var argTypes = argumentClrTypes.Select(MiscUtils.TypeByName).ToArray();
        var target = MiscUtils.TypeByName(typemethod[0]).GetMethod(typemethod[1], (BindingFlags)15422, null, argTypes, null);
        target ??= MiscUtils.TypeByName(typemethod[0]).GetMethod(typemethod[1], (BindingFlags)15420, null, argTypes, null);
        if (target == null)
        {
            Main.Logger.Log($"{typeColonMethodName} Cannot Be Found.");
            return false;
        }
        var wrap = func.Wrap(target, true);
        if (wrap == null)
            return false;
        harmony.Patch(target, new HarmonyMethod(wrap));
        return true;
    }
    [Api("postfixWithArgs")]
    public static bool PostfixWithArgs(Engine engine, string typeColonMethodName, string[] argumentClrTypes, JsValue patch)
    {
        if (!(patch is Function func)) return false;
        var typemethod = typeColonMethodName.Split2(':');
        var argTypes = argumentClrTypes.Select(MiscUtils.TypeByName).ToArray();
        var target = MiscUtils.TypeByName(typemethod[0]).GetMethod(typemethod[1], (BindingFlags)15422, null, argTypes, null);
        target ??= MiscUtils.TypeByName(typemethod[0]).GetMethod(typemethod[1], (BindingFlags)15420, null, argTypes, null);
        if (target == null)
        {
            Main.Logger.Log($"{typeColonMethodName} Cannot Be Found.");
            return false;
        }
        var wrap = func.Wrap(target, false);
        if (wrap == null)
            return false;
        harmony.Patch(target, postfix: new HarmonyMethod(wrap));
        return true;
    }
    [Api("transpilerWithArgs")]
    public static bool TranspilerWithArgs(Engine engine, string typeColonMethodName, string[] argumentClrTypes, JsValue patch)
    {
        if (!(patch is Function func)) return false;
        var typemethod = typeColonMethodName.Split2(':');
        var argTypes = argumentClrTypes.Select(MiscUtils.TypeByName).ToArray();
        var target = MiscUtils.TypeByName(typemethod[0]).GetMethod(typemethod[1], (BindingFlags)15422, null, argTypes, null);
        target ??= MiscUtils.TypeByName(typemethod[0]).GetMethod(typemethod[1], (BindingFlags)15420, null, argTypes, null);
        if (target == null)
        {
            Main.Logger.Log($"{typeColonMethodName} Cannot Be Found.");
            return false;
        }
        var wrap = func.WrapTranspiler();
        if (wrap == null)
            return false;
        harmony.Patch(target, transpiler: new HarmonyMethod(wrap));
        return true;
    }*/
    [Api("getLanguage", RequireTypes = [typeof(SystemLanguage)])]
    public static SystemLanguage GetLanguage(Engine engine) => RDString.language;
    [Api("ease", RequireTypes = [typeof(Ease)])]
    public static float EasedValue(Engine engine, Ease ease, float lifetime) => DOVirtual.EasedValue(0, 1, lifetime, ease);
    [Api("easeColor", RequireTypes = [typeof(Color)])]
    public static Color EasedColor(Engine engine, Color color, Ease ease, float lifetime) => color * DOVirtual.EasedValue(0, 1, lifetime, ease);
    [Api("easeColorFromTo")]
    public static Color EasedColor(Engine engine, Color from, Color to, Ease ease, float lifetime) => from + ((to - from) * DOVirtual.EasedValue(0, 1, lifetime, ease));
    [Api("colorFromHexRGB")]
    public static Color FromHexRGB(Engine engine, string rgbHex) => ColorUtility.TryParseHtmlString('#' + rgbHex, out var color) ? color : Color.clear;
    [Api("colorFromHexRGBA")]
    public static Color FromHexRGBA(Engine engine, string rgbaHex) => ColorUtility.TryParseHtmlString('#' + rgbaHex, out var color) ? color : Color.clear;
    [Api("rgbToHSV")]
    public static float[] RgbToHSV(Color color) {
        float[] values = new float[3];
        Color.RGBToHSV(color, out values[0], out values[1], out values[2]);
        return values;
    }
    [Api("colorToHexRGB")]
    public static string ToHexRGB(Engine engine, Color color) => ColorUtility.ToHtmlStringRGB(color);
    [Api("colorToHexRGBA")]
    public static string ToHexRGBA(Engine engine, Color color) => ColorUtility.ToHtmlStringRGBA(color);
    [Api("getTagValueSafe")]
    public static string GetTagValueSafe(
        Engine engine,
        string tagName,
        params string[] args
    ) {
        OverlayerTag overlayerTag =
            TagManager.GetTag(tagName);

        if(overlayerTag == null) {
            return "";
        }

        args ??= Array.Empty<string>();

        object[] callArgs =
            new object[overlayerTag.Tag.ArgumentCount];

        for(int i = 0; i < callArgs.Length; i++) {
            callArgs[i] =
                i < args.Length
                    ? args[i]
                    : "";
        }

        return overlayerTag.Tag.GetterDelegate
            .DynamicInvoke(callArgs)
            ?.ToString() ?? "";
    }
    [Api("parseFastInt")]
    public static int ParseFastInt(string str) => StringConverter.ToInt32(str);
    [Api("parseFastFloat")]
    public static double ParseFastFloat(string str) => StringConverter.ToDouble(str);
    [Api("getText")]
    public static OverlayerText GetText(int index, OverlayerProfile profile = null) {
        profile ??= ProfileManager.Profiles.FirstOrDefault(p => p.Config.Active);
        if(profile == null) {
            return null;
        }
        object obj = index < 0 || index >= profile.ObjectManager.Count ? null : profile.ObjectManager.Get(index);
        return obj as OverlayerText;
    }
    [Api("getTextByName")]
    public static OverlayerText GetTextByName(string name, OverlayerProfile profile = null) {
        profile ??= ProfileManager.Profiles.FirstOrDefault(p => p.Config.Active);
        if(profile == null) {
            return null;
        }
        for(int i = 0; i < profile.ObjectManager.Count; i++) {
            object obj = profile.ObjectManager.Get(i);
            if(obj is OverlayerText text && text.Config.Name == name) {
                return text;
            }
        }
        return null;
    }
    [Api("createText")]
    public static OverlayerText CreateText(OverlayerProfile profile = null) {
        profile ??= ProfileManager.Profiles.FirstOrDefault(p => p.Config.Active);
        object obj = profile?.ObjectManager.Create(new TextConfig());
        return obj as OverlayerText;
    }

    [Api("createTextFromJson")]
    public static OverlayerText CreateTextFromJson(string json, OverlayerProfile profile = null) {
        profile ??= ProfileManager.Profiles.FirstOrDefault(p => p.Config.Active);
        if(profile == null) {
            return null;
        }

        var token = JToken.Parse(json);
        var config = TextConfigImporter.Import(token);
        return profile.ObjectManager.Create(config);
    }
    [Api("createTexture", RequireTypes = [typeof(Texture2D)])]
    public static Texture2D CreateTexture(string imagePath) {
        if(!File.Exists(imagePath)) {
            return null;
        }

        Texture2D texture = new(1, 1);
        texture.LoadImage(File.ReadAllBytes(imagePath));
        return texture;
    }
    [Api("createTextureRaw")]
    public static Texture2D CreateTextureRaw(byte[] raw) {
        Texture2D texture = new(1, 1);
        texture.LoadImage(raw);
        return texture;
    }
    [Api("createSprite")]
    public static Sprite CreateSprite(Texture2D texture) {
        if(!spriteCache.TryGetValue(texture, out Sprite sprite)) {
            sprite = spriteCache[texture] = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        }

        return sprite;
    }
    [Api("playSound")]
    public static void PlaySound(string path) {
        var sound = new Sound {
            sound = path
        };
        AudioPlayer.Play(sound);
    }
    [Api("loadAudio", Comment =
    [
        "Load Audio(UnityEngine.AudioClip) With Callback (.mp3, .ogg, .aiff, .wav)"
    ], RequireTypes = [typeof(AudioClip)])]
    public static void LoadAudio(string path, JsValue callback) {
        if(callback is not Function func) {
            return;
        }

        FIWrapper fi = new(func);
        AudioPlayer.LoadAudio(path, ac => fi.Call(ac));
    }
    [Api("setAudio", Comment =
    [
        "Set Audio(UnityEngine.AudioClip) With Callback (.mp3, .ogg, .aiff, .wav)"
    ], RequireTypes = [typeof(AudioSource)])]
    public static void SetAudio(string path, AudioSource source) => AudioPlayer.LoadAudio(path, clip => source.clip = clip);
    private static void InvokeEventCallback(
        FIWrapper wrapper,
        string eventName,
        params object[] args
    ) {
        try {
            wrapper.Call(args);
        }
        catch(Exception e) {
            Main.Logger.Log(
                $"[Scripting] {eventName} callback failed:" +
                $"\n{e}"
            );
        }
    }

    private static void RegisterInputCallback(
        Dictionary<KeyCode, List<FIWrapper>> callbacks,
        KeyCode key,
        FIWrapper wrapper
    ) {
        if(!callbacks.TryGetValue(key, out var list)) {
            list = [];
            callbacks[key] = list;
        }

        list.Add(wrapper);
    }

    private static void InvokeInputCallbacks(
        FIWrapper[] callbacks,
        string eventName
    ) {
        for(int i = 0; i < callbacks.Length; i++) {
            InvokeEventCallback(
                callbacks[i],
                eventName
            );
        }
    }

    public static void UpdateInputCallbacks() {
        if(anyKeyCallbacks == null ||
            anyKeyDownCallbacks == null ||
            keyCallbacks == null ||
            keyUpCallbacks == null ||
            keyDownCallbacks == null) {
            return;
        }

        if(Input.anyKey && anyKeyCallbacks.Count > 0) {
            InvokeInputCallbacks(
                anyKeyCallbacks.ToArray(),
                "anyKey"
            );
        }

        if(Input.anyKeyDown && anyKeyDownCallbacks.Count > 0) {
            InvokeInputCallbacks(
                anyKeyDownCallbacks.ToArray(),
                "anyKeyDown"
            );
        }

        foreach(var pair in keyCallbacks.ToArray()) {
            if(Input.GetKey(pair.Key)) {
                InvokeInputCallbacks(
                    pair.Value.ToArray(),
                    $"key:{pair.Key}"
                );
            }
        }

        foreach(var pair in keyUpCallbacks.ToArray()) {
            if(Input.GetKeyUp(pair.Key)) {
                InvokeInputCallbacks(
                    pair.Value.ToArray(),
                    $"keyUp:{pair.Key}"
                );
            }
        }

        foreach(var pair in keyDownCallbacks.ToArray()) {
            if(Input.GetKeyDown(pair.Key)) {
                InvokeInputCallbacks(
                    pair.Value.ToArray(),
                    $"keyDown:{pair.Key}"
                );
            }
        }
    }

    public class On {
        [Api("rewind", Comment =
        [
            "On ADOFAI Rewind (Level Start, Scene Moved, etc..)"
        ])]
        public static void Rewind(Engine engine, JsValue func) {
            if(func is not Function fi) {
                return;
            }

            FIWrapper wrapper = new(fi);
                                                         
            foreach(var target in MiscUtils.MethodsByNames("scrController:Awake_Rewind", "scrPlayer:Awake_Rewind")) {
                harmony.Postfix(target, new Action(() => InvokeEventCallback(wrapper, "rewind")));
            }
        }
        [Api("hit", Comment =
        [
            "On Tile Hit"
        ])]
        public static void Hit(Engine engine, JsValue func) {
            if(func is not Function fi) {
                return;
            }

            FIWrapper wrapper = new(fi);
                                                                
            foreach(var target in MiscUtils.MethodsByNames("scrController:Hit", "scrPlayer:Hit")) {
                harmony.Postfix(target, new Action(() => InvokeEventCallback(wrapper, "hit")));
            }
        }
        [Api("dead", Comment =
        [
            "On Dead"
        ])]
        public static void Dead(Engine engine, JsValue func) {
            if(func is not Function fi) {
                return;
            }

            FIWrapper wrapper = new(fi);
                                                                    
            foreach(var target in MiscUtils.MethodsByNames("scrController:FailAction", "scrPlayer:FailAction")) {
                harmony.Postfix(target, new Action(() => {
                    if(!VersionSafe.IsNoFail(scrController.instance)) {
                        InvokeEventCallback(wrapper, "dead");
                    }
                }));
            }
        }
        [Api("fail", Comment =
        [
            "On Fail"
        ])]
        public static void Fail(Engine engine, JsValue func) {
            if(func is not Function fi) {
                return;
            }

            FIWrapper wrapper = new(fi);
            foreach(var target in MiscUtils.MethodsByNames("scrController:FailAction", "scrPlayer:FailAction")) {
                harmony.Postfix(target, new Action(() => InvokeEventCallback(wrapper, "fail")));
            }
        }
        [Api("clear", Comment =
        [
            "On Clear"
        ])]
        public static void Clear(Engine engine, JsValue func) {
            if(func is not Function fi) {
                return;
            }

            FIWrapper wrapper = new(fi);
            foreach(var target in MiscUtils.MethodsByNames("scrController:OnLandOnPortal", "scrPlayer:OnLandOnPortal")) {
                harmony.Postfix(target, new Action(() => {
                    if(VersionSafe.IsGameWorld(scrController.instance)) {
                        InvokeEventCallback(wrapper, "clear");
                    }
                }));
            }
        }
        #region KeyEvents
        [Api("anyKey", Comment =
        [
            "On Any Key Pressed"
        ])]
        public static void AnyKey(Engine engine, JsValue func) {
            if(func is Function fi) {
                anyKeyCallbacks.Add(new FIWrapper(fi));
            }
        }
        [Api("anyKeyDown", Comment =
        [
            "On Any Key Down"
        ])]
        public static void AnyKeyDown(Engine engine, JsValue func) {
            if(func is Function fi) {
                anyKeyDownCallbacks.Add(new FIWrapper(fi));
            }
        }
        [Api("key", Comment =
        [
            "On Key Pressed"
        ])]
        public static void Key(Engine engine, KeyCode key, JsValue func) {
            if(func is Function fi) {
                RegisterInputCallback(
                    keyCallbacks,
                    key,
                    new FIWrapper(fi)
                );
            }
        }
        [Api("keyUp", Comment =
        [
            "On Key Up"
        ])]
        public static void KeyUp(Engine engine, KeyCode key, JsValue func) {
            if(func is Function fi) {
                RegisterInputCallback(
                    keyUpCallbacks,
                    key,
                    new FIWrapper(fi)
                );
            }
        }
        [Api("keyDown", Comment =
        [
            "On Key Down"
        ])]
        public static void KeyDown(Engine engine, KeyCode key, JsValue func) {
            if(func is Function fi) {
                RegisterInputCallback(
                    keyDownCallbacks,
                    key,
                    new FIWrapper(fi)
                );
            }
        }
        #endregion
    }
    [Api(Comment =
        [
            "These Methods Are Recommended To Use In 'On.rewind' Callback."
        ],
    RequireTypes =
        [
            typeof(SpriteRenderer),
        typeof(scrHitTextMesh),
        typeof(HitMargin),
        typeof(SfxSound),
        typeof(HitSound)
        ])]
    public class Adofai {
        [Api("getPlanetRenderer", ReturnComment = "UnityEngine.SpriteRenderer (Planet SpriteRenderer)")]
        public static SpriteRenderer GetPlanetRenderer(
            scrPlanet planet,
            PlanetRenderer planetrenderer
        ) {
            if(!planet || !planetrenderer) {
                Main.Logger.Log(
                    "[Scripting] getPlanetRenderer skipped: " +
                    "planet or PlanetRenderer is not ready."
                );
                return null;
            }

            try {
                return planet.GetOrAddRenderer(planetrenderer);
            }
            catch(Exception e) {
                Main.Logger.Log(
                    "[Scripting] getPlanetRenderer failed: " +
                    $"{e.GetType().Name}: {e.Message}"
                );
                return null;
            }
        }
        [Api("scalePlanet")]
        public static void ScalePlanet(PlanetRenderer planetrender, Vector2 vec) {
            if(!planetrender) {
                Main.Logger.Log(
                    "[Scripting] scalePlanet skipped: " +
                    "PlanetRenderer is not ready."
                );
                return;
            }

            ScaleAll([
                FindPlanetBaseRenderer(planetrender)?.transform,
                planetrender.coreParticles?.transform,
                planetrender.tailParticles?.transform,
                planetrender.sparks?.transform,
                planetrender.ring?.transform,
                planetrender.glow?.transform,
                planetrender.deathExplosion?.transform,
                planetrender.faceSprite?.transform,
                planetrender.faceDetails?.transform,
                planetrender.faceHolder?.transform,
                planetrender.samuraiSprite?.transform
            ], vec);
        }
        private static T FindLoadedComponent<T>() where T : Component {
            return Resources
                .FindObjectsOfTypeAll<T>()
                .FirstOrDefault(component =>
                    component &&
                    component.gameObject &&
                    component.gameObject.scene.IsValid()
                );
        }

        [Api("setDiscordRp")]
        public static void SetDiscordRp(
            string title,
            string state,
            string details
        ) {
            static string Validate(string value) {
                value ??= "";
                return value.Length <= 60
                    ? value
                    : value.Substring(0, 57) + "...";
            }

            try {
                var controller = DiscordController.instance;
                FieldInfo discordField = typeof(DiscordController)
                    .GetField("discord", (BindingFlags)15420);

                var discord = discordField?.GetValue(controller)
                    as Discord.Discord;

                if(discord == null) {
                    Main.Logger.Log(
                        "[Scripting] setDiscordRp skipped: " +
                        "Discord is not initialized."
                    );
                    return;
                }

                Activity activity = default;
                activity.State = Validate(state);
                activity.Details = Validate(details);
                activity.Assets.LargeImage = "planets_icon_stars";
                activity.Assets.LargeText = Validate(title);

                discord
                    .GetActivityManager()
                    .UpdateActivity(activity, delegate { });
            }
            catch(Exception e) {
                Main.Logger.Log(
                    "[Scripting] setDiscordRp failed: " +
                    $"{e.GetType().Name}: {e.Message}"
                );
            }
        }

        [Api("setBuildText")]
        public static void SetBuildText(string text) {
            var betaObject = FindLoadedComponent<scrEnableIfBeta>();
            if(!betaObject) {
                Main.Logger.Log(
                    "[Scripting] setBuildText skipped: " +
                    "scrEnableIfBeta is not loaded in the current scene."
                );
                return;
            }

            TMP_Text label =
                betaObject.GetComponent<TMP_Text>() ??
                betaObject.GetComponentInChildren<TMP_Text>(true);

            if(!label) {
                Main.Logger.Log(
                    "[Scripting] setBuildText skipped: " +
                    "no TMP_Text was found."
                );
                return;
            }

            betaObject.gameObject.SetActive(true);
            label.text = text ?? "";
        }

        private static UnityEngine.UI.Text FindAutoText() {
            var debugObject = FindLoadedComponent<scrShowIfDebug>();
            if(!debugObject) {
                return null;
            }

            debugObject.gameObject.SetActive(true);
            return
                debugObject.GetComponent<UnityEngine.UI.Text>() ??
                debugObject.GetComponentInChildren<UnityEngine.UI.Text>(true);
        }

        [Api("setAutoText")]
        public static void SetAutoText(string text) {
            InjectAutoTextUpdate();

            UnityEngine.UI.Text label = FindAutoText();
            if(!label) {
                Main.Logger.Log(
                    "[Scripting] setAutoText skipped: " +
                    "scrShowIfDebug or its Text component is not loaded."
                );
                return;
            }

            label.text = text ?? "";
        }

        [Api("configAutoText", ParamComment =
        [
            "UnityEngine.UI.Text Callback"
        ])]
        public static void ConfigAutoText(JsValue configFunc) {
            if(configFunc is not Function func) {
                return;
            }

            InjectAutoTextUpdate();

            UnityEngine.UI.Text label = FindAutoText();
            if(!label) {
                Main.Logger.Log(
                    "[Scripting] configAutoText skipped: " +
                    "scrShowIfDebug or its Text component is not loaded."
                );
                return;
            }

            InvokeEventCallback(
                new FIWrapper(func),
                "configAutoText",
                label
            );
        }
        [Api("setTileSprite")]
        public static void SetTileSprite(Sprite sprite, float scale) {
            foreach(var floor in UnityEngine.Object.FindObjectsOfType<scrFloor>()) {
                floor.floorRenderer.sprite = sprite;
                floor.floorRenderer.transform.localScale = new Vector2(scale, scale);
            }
        }
        private const string ScriptTileIconObjectName = "OverlayerScriptTileIcon";

        private static bool HasIconName(string value) =>
            !string.IsNullOrWhiteSpace(value) &&
            value.IndexOf("icon", StringComparison.OrdinalIgnoreCase) >= 0;

        private static void AddTileIconRenderer(
            object value,
            HashSet<SpriteRenderer> renderers
        ) {
            if(value == null || renderers == null) {
                return;
            }

            if(value is SpriteRenderer spriteRenderer) {
                if(spriteRenderer) {
                    renderers.Add(spriteRenderer);
                }
                return;
            }

            if(value is GameObject gameObject) {
                foreach(var renderer in gameObject.GetComponentsInChildren<SpriteRenderer>(true)) {
                    if(renderer) {
                        renderers.Add(renderer);
                    }
                }
                return;
            }

            if(value is Component component) {
                foreach(var renderer in component.GetComponentsInChildren<SpriteRenderer>(true)) {
                    if(renderer) {
                        renderers.Add(renderer);
                    }
                }
                return;
            }

            if(value is Array array) {
                foreach(object item in array) {
                    AddTileIconRenderer(item, renderers);
                }
            }
        }

        private static void ApplyTileIconMemberValues(
            scrFloor floor,
            Sprite sprite,
            float scale,
            HashSet<SpriteRenderer> renderers
        ) {
            const BindingFlags flags = (BindingFlags)15420;
            Type type = floor.GetType();

            foreach(FieldInfo field in type.GetFields(flags)) {
                if(field.IsStatic || !HasIconName(field.Name)) {
                    continue;
                }

                try {
                    if(field.FieldType == typeof(Sprite) && !field.IsInitOnly) {
                        field.SetValue(floor, sprite);
                    }
                    else if(
                        field.Name.IndexOf("scale", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        !field.IsInitOnly
                    ) {
                        if(field.FieldType == typeof(float)) {
                            field.SetValue(floor, scale);
                        }
                        else if(field.FieldType == typeof(Vector2)) {
                            field.SetValue(floor, new Vector2(scale, scale));
                        }
                        else if(field.FieldType == typeof(Vector3)) {
                            field.SetValue(floor, new Vector3(scale, scale, 1f));
                        }
                    }

                    AddTileIconRenderer(field.GetValue(floor), renderers);
                }
                catch {
                                                                                           
                }
            }

            foreach(PropertyInfo property in type.GetProperties(flags)) {
                if(
                    !HasIconName(property.Name) ||
                    property.GetIndexParameters().Length != 0
                ) {
                    continue;
                }

                try {
                    MethodInfo setter = property.GetSetMethod(true);
                    if(setter != null && !setter.IsStatic) {
                        if(property.PropertyType == typeof(Sprite)) {
                            property.SetValue(floor, sprite);
                        }
                        else if(
                            property.Name.IndexOf("scale", StringComparison.OrdinalIgnoreCase) >= 0
                        ) {
                            if(property.PropertyType == typeof(float)) {
                                property.SetValue(floor, scale);
                            }
                            else if(property.PropertyType == typeof(Vector2)) {
                                property.SetValue(floor, new Vector2(scale, scale));
                            }
                            else if(property.PropertyType == typeof(Vector3)) {
                                property.SetValue(floor, new Vector3(scale, scale, 1f));
                            }
                        }
                    }

                    MethodInfo getter = property.GetGetMethod(true);
                    if(getter != null && !getter.IsStatic) {
                        AddTileIconRenderer(property.GetValue(floor), renderers);
                    }
                }
                catch {
                                                                                           
                }
            }
        }

        private static SpriteRenderer GetOrCreateTileIconRenderer(scrFloor floor) {
            Transform iconTransform = floor.transform.Find(ScriptTileIconObjectName);
            GameObject iconObject;

            if(iconTransform) {
                iconObject = iconTransform.gameObject;
            }
            else {
                iconObject = new GameObject(ScriptTileIconObjectName);
                iconObject.transform.SetParent(floor.transform, false);
                iconObject.transform.localPosition = Vector3.zero;
            }

            SpriteRenderer renderer = iconObject.GetComponent<SpriteRenderer>();
            if(!renderer) {
                renderer = iconObject.AddComponent<SpriteRenderer>();
            }

            SpriteRenderer floorRenderer =
                floor.floorRenderer == null
                    ? null
                    : floor.floorRenderer.transform.GetComponent<SpriteRenderer>();
            if(floorRenderer) {
                renderer.sortingLayerID = floorRenderer.sortingLayerID;
                renderer.sortingOrder = floorRenderer.sortingOrder + 1;
            }

            return renderer;
        }

        private static void ConfigureTileIconRenderer(
            SpriteRenderer renderer,
            SpriteRenderer floorRenderer,
            Sprite sprite,
            float scale
        ) {
            if(!renderer || renderer == floorRenderer) {
                return;
            }

            renderer.sprite = sprite;
            renderer.enabled = sprite != null;
            renderer.transform.localScale = new Vector3(scale, scale, 1f);
        }

        [Api("setTileIcon")]
        public static void SetTileIcon(Sprite sprite, float scale) {
            int floorsSeen = 0;
            int renderersUpdated = 0;
            int nativeFailures = 0;

            foreach(var floor in UnityEngine.Object.FindObjectsOfType<scrFloor>()) {
                if(!floor) {
                    continue;
                }

                floorsSeen++;
                SpriteRenderer floorRenderer =
                    floor.floorRenderer == null
                        ? null
                        : floor.floorRenderer.transform.GetComponent<SpriteRenderer>();
                var renderers = new HashSet<SpriteRenderer>();

                try {
                    floor.SetIconSprite(sprite);
                    floor.SetIconScale(scale);
                }
                catch {
                    nativeFailures++;
                }

                ApplyTileIconMemberValues(
                    floor,
                    sprite,
                    scale,
                    renderers
                );

                SpriteRenderer[] children = floor.GetComponentsInChildren<SpriteRenderer>(true);
                foreach(SpriteRenderer renderer in children) {
                    if(!renderer || renderer == floorRenderer) {
                        continue;
                    }

                    if(
                        HasIconName(renderer.name) ||
                        HasIconName(renderer.transform.parent?.name)
                    ) {
                        renderers.Add(renderer);
                    }
                }

                if(renderers.Count == 0) {
                    renderers.Add(GetOrCreateTileIconRenderer(floor));
                }

                foreach(SpriteRenderer renderer in renderers) {
                    ConfigureTileIconRenderer(
                        renderer,
                        floorRenderer,
                        sprite,
                        scale
                    );

                    if(renderer && renderer != floorRenderer) {
                        renderersUpdated++;
                    }
                }
            }

            Main.Logger.Log(
                "[Scripting] setTileIcon: " +
                $"updated {renderersUpdated} renderer(s) across {floorsSeen} floor(s); " +
                $"native setter failures={nativeFailures}."
            );
        }
        [Api("configTiles")]
        public static void ConfigTiles(JsValue configFunc) {
            if(configFunc is not Function func) {
                return;
            }

            var levelMaker = scrLevelMaker.instance;
            var list = levelMaker?.listFloors;
            if(list == null) {
                Main.Logger.Log(
                    "[Scripting] configTiles skipped: level floors are not ready."
                );
                return;
            }

            FIWrapper wrapper = new(func);
            for(int i = 0; i < list.Count; i++) {
                try {
                    wrapper.Call(
                        wrapper.args.Length == 1
                            ? [list[i]]
                            : [i, list[i]]
                    );
                }
                catch(Exception e) {
                    Main.Logger.Log(
                        $"[Scripting] configTiles callback failed at tile {i}: " +
                        $"{e.GetType().Name}: {e.Message}"
                    );
                }
            }
        }
        [Api("setJudgeText")]
        public static void SetJudgeText(HitMargin hitMargin, string text) {
            InjectJudgeTextShowPatch();

            customJudgeTexts[hitMargin] = text ?? "";
            Main.Logger.Log($"[Scripting] setJudgeText: registered replacement for {hitMargin}");

                                                        
                                                                
            StaticCoroutine.Run(StaticCoroutine.SyncRunner(() => {
                int applied = 0;
                foreach(var textMesh in Resources.FindObjectsOfTypeAll<scrHitTextMesh>()) {
                    if(ApplyRegisteredJudgeText(textMesh, null)) {
                        applied++;
                    }
                }

                Main.Logger.Log($"[Scripting] setJudgeText: refreshed {applied} currently loaded hit text object(s) for registered replacements");
            }));
        }

        [Api("configJudgeText", ParamComment =
        [
            "scrHitTextMesh Callback"
        ])]
        public static void ConfigJudgeText(HitMargin hitMargin, JsValue configFunc) {
            if(configFunc is not Function func) {
                return;
            }

            FIWrapper wrapper = new(func);
            InjectJudgeTextShowPatch();

            if(!customJudgeConfigs.TryGetValue(hitMargin, out var wrappers)) {
                wrappers = customJudgeConfigs[hitMargin] = [];
            }

            wrappers.Add(wrapper);

                                                           
            StaticCoroutine.Run(StaticCoroutine.SyncRunner(() => {
                foreach(var t in Resources.FindObjectsOfTypeAll<scrHitTextMesh>()) {
                    if(TryGetHitTextMargin(t, null, out var actualMargin) && actualMargin == hitMargin) {
                        InvokeJudgeTextConfig(wrapper, hitMargin, t);
                    }
                }
            }));
        }
        [Api("setSfxSound")]
        public static bool SetSfxSound(SfxSound sfx, string audio) {
            if(string.IsNullOrWhiteSpace(audio)) {
                Main.Logger.Log(
                    "[Scripting] setSfxSound skipped: audio path is empty."
                );
                return false;
            }

            AudioPlayer.LoadAudio(audio, clip => {
                try {
                    if(!clip) {
                        Main.Logger.Log(
                            "[Scripting] setSfxSound skipped: audio clip is null."
                        );
                        return;
                    }

                    var constants = Tags.ADOFAI.RDC;
                    if(constants == null || constants.soundEffects == null) {
                        Main.Logger.Log(
                            "[Scripting] setSfxSound skipped: " +
                            "RDConstants soundEffects is not ready."
                        );
                        return;
                    }

                    int index = (int)sfx;
                    if(index < 0 || index >= constants.soundEffects.Length) {
                        Main.Logger.Log(
                            "[Scripting] setSfxSound skipped: " +
                            $"enum index {index} is outside soundEffects length {constants.soundEffects.Length}."
                        );
                        return;
                    }

                    constants.soundEffects[index] = clip;
                    Main.Logger.Log(
                        "[Scripting] setSfxSound: " +
                        $"replaced {sfx} at index {index} with '{clip.name}'."
                    );
                }
                catch(Exception e) {
                    Main.Logger.Log(
                        "[Scripting] setSfxSound callback failed:" +
                        $"\n{e}"
                    );
                }
            });

            return true;
        }

        [Api("setHitSound")]
        public static bool SetHitSound(HitSound hit, string audio) {
            if(string.IsNullOrWhiteSpace(audio)) {
                Main.Logger.Log(
                    "[Scripting] setHitSound skipped: audio path is empty."
                );
                return false;
            }

            AudioPlayer.LoadAudio(audio, clip => {
                try {
                    if(!clip) {
                        Main.Logger.Log(
                            "[Scripting] setHitSound skipped: audio clip is null."
                        );
                        return;
                    }

                    var manager = AudioManager.Instance;
                    if(manager == null || manager.audioLib == null) {
                        Main.Logger.Log(
                            "[Scripting] setHitSound skipped: " +
                            "AudioManager audioLib is not ready."
                        );
                        return;
                    }

                    string key = $"snd{hit}";
                    manager.audioLib[key] = clip;
                    Main.Logger.Log(
                        "[Scripting] setHitSound: " +
                        $"replaced '{key}' with '{clip.name}'."
                    );
                }
                catch(Exception e) {
                    Main.Logger.Log(
                        "[Scripting] setHitSound callback failed:" +
                        $"\n{e}"
                    );
                }
            });

            return true;
        }
        [Api("setLobbyBgm")]
        public static void SetLobbyBgm(string audio) => AudioPlayer.LoadAudio(audio, clip => {
            var lobbySource = scrConductor.instance.GetComponentsInChildren<AudioSource>()?.FirstOrDefault(a => a.clip?.name == "1-X-wav");
            if(lobbySource != null) {
                lobbySource.clip = clip;
            }
        });
        [Api("getAngleFromFloor")]
        public static double GetAngleFromFloor(scrFloor floor) => Math.Abs(floor.entryangle - floor.exitangle) * Mathf.Rad2Deg;
        private static Dictionary<HitMargin, scrHitTextMesh[]> GetCachedHitTexts() {
            object value =
                VersionSafe.FirstMemberValue(Tags.ADOFAI.Controller, "cachedHitTexts", "hitTexts") ??
                VersionSafe.FirstMemberValue(VersionSafe.GetPlayerOne(Tags.ADOFAI.Controller), "cachedHitTexts", "hitTexts");

            return value as Dictionary<HitMargin, scrHitTextMesh[]>;
        }

                                                       
        internal static IEnumerable<Dictionary<HitMargin, scrHitTextMesh[]>> GetAllCachedHitTexts() {
            HashSet<object> seen = [];
            if(VersionSafe.FirstMemberValue(Tags.ADOFAI.Controller, "cachedHitTexts", "hitTexts")
                is Dictionary<HitMargin, scrHitTextMesh[]> fromController && seen.Add(fromController)) {
                yield return fromController;
            }

            for(int i = 0; i < VersionSafe.GetMaxPlayerCount(); i++) {
                object player = VersionSafe.GetPlayerByIndex(i);
                if(player == null) {
                    break;
                }

                if(VersionSafe.FirstMemberValue(player, "cachedHitTexts", "hitTexts")
                    is Dictionary<HitMargin, scrHitTextMesh[]> fromPlayer && seen.Add(fromPlayer)) {
                    yield return fromPlayer;
                }
            }

                                                                   
            if(VersionSafe.FirstMemberValue(VersionSafe.GetPlayerOne(Tags.ADOFAI.Controller), "cachedHitTexts", "hitTexts")
                is Dictionary<HitMargin, scrHitTextMesh[]> fromPlayerOne && seen.Add(fromPlayerOne)) {
                yield return fromPlayerOne;
            }
        }

        private static void InjectJudgeTextShowPatch() {
            if(judgeTextShowPatchInjected) {
                return;
            }

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            MethodInfo postfixMethod = typeof(Adofai).GetMethod(nameof(AfterHitTextShow), flags);
            if(postfixMethod == null) {
                Main.Logger.Log("[Scripting] setJudgeText: failed to find AfterHitTextShow postfix method");
                return;
            }

            var postfix = new HarmonyMethod(postfixMethod);
            int patched = 0;

            foreach(var method in typeof(scrHitTextMesh).GetMethods(flags).Where(m => m.Name == "Show")) {
                try {
                    harmony.Patch(method, postfix: postfix);
                    patched++;
                }
                catch(Exception e) {
                    Main.Logger.Log($"[Scripting] setJudgeText: failed to patch scrHitTextMesh.Show overload: {e.GetType().Name}: {e.Message}");
                }
            }

            judgeTextShowPatchInjected = patched > 0;
            Main.Logger.Log($"[Scripting] setJudgeText: patched {patched} scrHitTextMesh.Show overload(s)");
        }

        private static void AfterHitTextShow(scrHitTextMesh __instance, object[] __args) {
            ApplyRegisteredJudgeText(__instance, __args);
            ApplyRegisteredJudgeConfigs(__instance, __args);
        }

        private static void ApplyRegisteredJudgeConfigs(scrHitTextMesh hitText, object[] args) {
            if(!hitText || customJudgeConfigs == null || customJudgeConfigs.Count <= 0) {
                return;
            }

            if(!TryGetHitTextMargin(hitText, args, out var margin)) {
                return;
            }

            if(!customJudgeConfigs.TryGetValue(margin, out var wrappers)) {
                return;
            }

            foreach(var wrapper in wrappers.ToArray()) {
                InvokeJudgeTextConfig(wrapper, margin, hitText);
            }
        }

        private static void InvokeJudgeTextConfig(
            FIWrapper wrapper,
            HitMargin margin,
            scrHitTextMesh hitText
        ) {
            try {
                wrapper.Call(
                    wrapper.args.Length == 1
                        ? new object[] { hitText }
                        : new object[] { margin, hitText }
                );
            }
            catch(Exception e) {
                Main.Logger.Log(
                    $"[Scripting] configJudgeText callback failed for {margin}: " +
                    $"{e.GetType().Name}: {e.Message}"
                );
            }
        }

        private static bool ApplyRegisteredJudgeText(scrHitTextMesh hitText, object[] args) {
            if(!hitText || customJudgeTexts == null || customJudgeTexts.Count <= 0) {
                return false;
            }

            if(!TryGetHitTextMargin(hitText, args, out var margin)) {
                return false;
            }

            if(!customJudgeTexts.TryGetValue(margin, out var replacement)) {
                return false;
            }

            return SetHitTextString(hitText, replacement);
        }

        private static bool TryGetHitTextMargin(scrHitTextMesh hitText, object[] args, out HitMargin margin) {
            margin = default;

            if(TryGetHitMarginFromArgs(args, out margin)) {
                return true;
            }

            object value = VersionSafe.FirstMemberValue(hitText, "hitMargin", "margin");
            return TryConvertToHitMargin(value, out margin);
        }

        private static bool TryGetHitMarginFromArgs(object[] args, out HitMargin margin) {
            margin = default;

            if(args == null) {
                return false;
            }

            foreach(var arg in args) {
                if(TryConvertToHitMargin(arg, out margin)) {
                    return true;
                }
            }

            return false;
        }

        private static bool TryConvertToHitMargin(object value, out HitMargin margin) {
            margin = default;

            if(value == null) {
                return false;
            }

            if(value is HitMargin direct) {
                margin = direct;
                return true;
            }

            Type type = value.GetType();
            if(type.IsEnum) {
                string name = value.ToString();
                if(Enum.TryParse(name, true, out margin)) {
                    return true;
                }

                                                           
                                                                 
                if(type == typeof(HitMargin) || type.Name.IndexOf("Margin", StringComparison.OrdinalIgnoreCase) >= 0) {
                    int raw = Convert.ToInt32(value);
                    if(Enum.IsDefined(typeof(HitMargin), raw)) {
                        margin = (HitMargin)raw;
                        return true;
                    }
                }

                return false;
            }

            if(value is string str) {
                return Enum.TryParse(str, true, out margin);
            }

            if(value is int i && Enum.IsDefined(typeof(HitMargin), i)) {
                margin = (HitMargin)i;
                return true;
            }

            return false;
        }

                                                         
        internal static bool SetHitTextString(scrHitTextMesh hitText, string str) {
            if(hitText == null) {
                return false;
            }

            if(GetHitTextMeshText(hitText) is TextMesh mesh) {
                mesh.text = str;
                return true;
            }

            var tmp = FindHitTextTMP(hitText);
            if(tmp != null) {
                tmp.text = str;
                return true;
            }

            return false;
        }

        internal static TMP_Text FindHitTextTMP(scrHitTextMesh hitText) {
            if(hitText == null) {
                return null;
            }

            if(VersionSafe.FirstMemberValue(hitText, "mainText", "text", "textMesh") is TMP_Text directText) {
                return directText;
            }

                                          
            foreach(var f in hitText.GetType().GetFields((BindingFlags)15420)) {
                if(typeof(TMP_Text).IsAssignableFrom(f.FieldType) && f.GetValue(hitText) is TMP_Text fieldText) {
                    return fieldText;
                }
            }

            return hitText.GetComponentInChildren<TMP_Text>(true);
        }

        private static TextMesh GetHitTextMeshText(scrHitTextMesh hitTextMesh) =>
            (VersionSafe.FirstMemberValue(hitTextMesh, "text", "textMesh", "mesh") as TextMesh)
            ?? hitTextMesh?.GetComponentInChildren<TextMesh>(true);
        internal static bool autoTextInjected = false;
        private static void InjectAutoTextUpdate() {
            if(autoTextInjected) {
                return;
            }

            MethodInfo target = typeof(scrShowIfDebug)
                .GetMethod("Update", (BindingFlags)15420);
            MethodInfo prefixMethod = EmitUtils.Wrap(() => false);

            if(target == null || prefixMethod == null) {
                Main.Logger.Log(
                    "[Scripting] auto text patch skipped: " +
                    "scrShowIfDebug.Update is unavailable."
                );
                return;
            }

            try {
                harmony.Patch(
                    target,
                    prefix: new HarmonyMethod(prefixMethod)
                );
                autoTextInjected = true;
            }
            catch(Exception e) {
                Main.Logger.Log(
                    "[Scripting] auto text patch failed: " +
                    $"{e.GetType().Name}: {e.Message}"
                );
            }
        }
        private static void ScaleAll(Transform[] t, Vector2 vec) {
            for(int i = 0; i < t.Length; i++) {
                if(t[i] != null) {
                    t[i].localScale = vec;
                }
            }
        }
        public delegate R RefFunc<T, R>(ref T val);
    }
    #endregion
    static Harmony harmony;
    static Dictionary<HitMargin, string> customJudgeTexts;
    static Dictionary<HitMargin, List<FIWrapper>> customJudgeConfigs;
    static bool judgeTextShowPatchInjected;
    static List<FIWrapper> anyKeyCallbacks;
    static List<FIWrapper> anyKeyDownCallbacks;
    static Dictionary<KeyCode, List<FIWrapper>> keyCallbacks;
    static Dictionary<KeyCode, List<FIWrapper>> keyUpCallbacks;
    static Dictionary<KeyCode, List<FIWrapper>> keyDownCallbacks;
    public static HashSet<string> alreadyExecutedScripts;
    public static List<string> registeredCustomTags;
    public static Dictionary<string, object> globalVariables;
    public static Dictionary<Texture2D, Sprite> spriteCache = [];
    static Dictionary<Engine, Dictionary<string, TypeReference>> jsTypes;
    [Obsolete("Internal Only!", true)]
    public static object[] StrArrayToObjArray(string[] arr) {
        object[] newArr = new object[arr.Length];
        for(int i = 0; i < arr.Length; i++) {
            newArr[i] = arr[i];
        }

        return newArr;
    }
    static int uniqueId = 0;
    static bool wrapperInitialized = false;
    static MethodInfo satoa = typeof(Impl).GetMethod(nameof(StrArrayToObjArray), (BindingFlags)15420);
    static MethodInfo call_fi = typeof(FIWrapper).GetMethod("Call");
    static MethodInfo transpilerAdapter = typeof(Impl).GetMethod("TranspilerAdapter", AccessTools.all);
    static AssemblyBuilder ApiAssembly;
    static ModuleBuilder ApiModule;
    static SpriteRenderer FindPlanetBaseRenderer(
        PlanetRenderer planetrender
    ) {
        if(!planetrender) {
            return null;
        }

        try {
            object spriteObject = VersionSafe.FirstMemberValue(
                planetrender,
                "sprite"
            );

            if(spriteObject is SpriteRenderer directRenderer) {
                return directRenderer;
            }

            if(spriteObject != null) {
                object nestedRenderer = VersionSafe.FirstMemberValue(
                    spriteObject,
                    "meshRenderer",
                    "spriteRenderer",
                    "renderer"
                );

                if(nestedRenderer is SpriteRenderer reflectedRenderer) {
                    return reflectedRenderer;
                }

                if(spriteObject is Component component) {
                    return
                        component.GetComponent<SpriteRenderer>() ??
                        component.GetComponentInChildren<SpriteRenderer>(true);
                }
            }

            return
                planetrender.GetComponent<SpriteRenderer>() ??
                planetrender.GetComponentInChildren<SpriteRenderer>(true);
        }
        catch(Exception e) {
            Main.Logger.Log(
                "[Scripting] planet base renderer lookup failed: " +
                $"{e.GetType().Name}: {e.Message}"
            );
            return null;
        }
    }

    // From PlanetTweaks By tjwogud
    public static SpriteRenderer GetOrAddRenderer(
        this scrPlanet planet,
        PlanetRenderer planetrender
    ) {
        if(!planet || !planetrender) {
            return null;
        }

        SpriteRenderer renderer = planet.transform
            .Find("PlanetTweaksRenderer")
            ?.GetComponent<SpriteRenderer>();

        if(renderer) {
            return renderer;
        }

        GameObject obj = new("PlanetTweaksRenderer");
        obj.transform.SetParent(planet.transform, false);
        obj.transform.localPosition = Vector3.zero;

        renderer = obj.AddComponent<SpriteRenderer>();

        SpriteRenderer sourceRenderer =
            FindPlanetBaseRenderer(planetrender);

        if(sourceRenderer) {
            renderer.sortingOrder =
                sourceRenderer.sortingOrder + 1;
            renderer.sortingLayerID =
                sourceRenderer.sortingLayerID;
            renderer.sortingLayerName =
                sourceRenderer.sortingLayerName;
        }

        obj.AddComponent<RendererController>();
        return renderer;
    }
    static MethodInfo GenerateTagWrapper(FIWrapper wrapper) {
        Type[] paramTypes = wrapper.args.Select(t => typeof(string)).ToArray();
        var name = wrapper.fi.ToString().Replace("function ", string.Empty).Replace("() { [native code] }", string.Empty);
        if(string.IsNullOrWhiteSpace(name)) {
            name = "Anonymous";
        }

        TypeBuilder wrapperType = ApiModule.DefineType($"{name}_WrapperType${uniqueId++}", TypeAttributes.Public);
        MethodBuilder wrapperMethod = wrapperType.DefineMethod($"{name}_WrapperMethod", MethodAttributes.Public | MethodAttributes.Static, typeof(object), paramTypes);
        FieldBuilder wrapperField = wrapperType.DefineField("wrapper", typeof(FIWrapper), FieldAttributes.Public | FieldAttributes.Static);
        for(int i = 0; i < wrapper.args.Length; i++) {
            wrapperMethod.DefineParameter(i + 1, ParameterAttributes.None, wrapper.args[i]);
        }

        ILGenerator il = wrapperMethod.GetILGenerator();
        if(paramTypes.Length > 0) {
            LocalBuilder strArray = il.MakeArray<string>(paramTypes.Length);
            for(int i = 0; i < paramTypes.Length; i++) {
                il.Emit(OpCodes.Ldloc, strArray);
                il.Emit(OpCodes.Ldc_I4, i);
                il.Emit(OpCodes.Ldarg, i);
                il.Emit(OpCodes.Stelem_Ref);
            }
            il.Emit(OpCodes.Ldsfld, wrapperField);
            il.Emit(OpCodes.Ldloc, strArray);
            il.Emit(OpCodes.Call, satoa);
        } else {
            il.Emit(OpCodes.Ldsfld, wrapperField);
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Newarr, typeof(object));
        }
        il.Emit(OpCodes.Call, call_fi);
        il.Emit(OpCodes.Ret);
        var resultT = wrapperType.CreateType();
        resultT.GetField("wrapper").SetValue(null, wrapper);
        return resultT.GetMethod($"{name}_WrapperMethod");
    }
    public static MethodInfo WrapTranspiler(this Function func) {
        if(func == null) {
            return null;
        }

        FIWrapper holder = new(func);

        TypeBuilder type = EmitUtils.NewType();
        MethodBuilder methodB = type.DefineMethod("Wrapper_Transpiler", MethodAttributes.Public | MethodAttributes.Static, typeof(IEnumerable<CodeInstruction>),
            new[] { typeof(IEnumerable<CodeInstruction>), typeof(MethodBase), typeof(ILGenerator) });
        FieldBuilder holderfld = type.DefineField("holder", typeof(FIWrapper), FieldAttributes.Public | FieldAttributes.Static);

        var il = methodB.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldarg_2);
        il.Emit(OpCodes.Ldsfld, holderfld);
        il.Emit(OpCodes.Call, transpilerAdapter);
        il.Emit(OpCodes.Ret);

        Type t = type.CreateType();
        t.GetField("holder").SetValue(null, holder);
        return t.GetMethod("Wrapper_Transpiler");
    }
    [Obsolete("Internal Only!", true)]
    public static IEnumerable<CodeInstruction> TranspilerAdapter(IEnumerable<CodeInstruction> instructions, MethodBase original, ILGenerator il, FIWrapper func) {
        object[] args = new object[func.args.Length];
        for(int i = 0; i < args.Length; i++) {
            var argName = func.args[i];
            args[i] = argName.StartsWith("il")
                ? il
                : argName.StartsWith("o") ||
                argName.StartsWith("m")
                    ? original
                    : argName.StartsWith("ins") ? instructions.ToArray() : JsValue.Undefined;
        }
        var result = func.CallRaw(args);
        return JSUtils.IsNull(result) ? Enumerable.Empty<CodeInstruction>() : result.AsArray().Select(v => (CodeInstruction)v.ToObject());
    }
    public static void InitializeWrapperAssembly() {
        if(wrapperInitialized) {
            return;
        }

        uniqueId = 0;
        ApiAssembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("Overlayer.Scripting.ImplAss"), AssemblyBuilderAccess.RunAndCollect);
        ApiModule = ApiAssembly.DefineDynamicModule("Overlayer.Scripting.ImplAss");
        wrapperInitialized = true;
    }
    public static void DisposeWrapperAssembly() {
        ApiAssembly = null;
        ApiModule = null;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, false);
        wrapperInitialized = false;
    }
    private static MethodInfo Postfix<T>(this Harmony harmony, MethodBase target, T del) where T : Delegate {
                                          
                                                 
        if(target == null) {
            Main.Logger.Log("[Scripting] Patch target not found (game version mismatch?), callback skipped.");
            return null;
        }
        return harmony.Patch(target, postfix: new HarmonyMethod(del.Wrap()));
    }
    private static MethodInfo Prefix<T>(this Harmony harmony, MethodBase target, T del) where T : Delegate {
        if(target == null) {
            Main.Logger.Log("[Scripting] Patch target not found (game version mismatch?), callback skipped.");
            return null;
        }
        return harmony.Patch(target, new HarmonyMethod(del.Wrap()));
    }
    // From PlanetTweaks By tjwogud
    public class RendererController : MonoBehaviour {
        private scrPlanet planet;
        private PlanetRenderer planetrender;
        private SpriteRenderer renderer;

        private void Awake() {
            RefreshReferences();
        }

        private void RefreshReferences() {
            if(!planet) {
                planet = GetComponentInParent<scrPlanet>();
            }

            if(!planetrender) {
                planetrender =
                    GetComponentInParent<PlanetRenderer>();
            }

            if(!renderer) {
                renderer = GetComponent<SpriteRenderer>();
            }
        }

        private void Update() {
            RefreshReferences();

            if(!planet || !planetrender || !renderer) {
                return;
            }

            if(planet.dummyPlanets) {
                Destroy(gameObject);
                return;
            }

            SpriteRenderer sourceRenderer =
                FindPlanetBaseRenderer(planetrender);

            if(sourceRenderer) {
                renderer.enabled = sourceRenderer.enabled;
            }
        }
    }
}
