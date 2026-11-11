
using Overlayer.Core;
using Overlayer.Core.TextReplacing;
using Overlayer.Models;
using Overlayer.Tags;
using Overlayer.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Overlayer.Unity;

public class OverlayerText : OverlayerObject, IPointerDownHandler, IPointerUpHandler, IDragHandler, IPointerEnterHandler, IPointerExitHandler {
    public static event Action<OverlayerText> OnApplyConfig = delegate { };
    public bool Initialized { get; private set; }

    public override ObjectConfig Config => TextConfig;
    public TextConfig TextConfig { get; private set; }
    public Replacer PlayingReplacer;
    public Replacer NotPlayingReplacer;
    public TextMeshProUGUI Text;
    private Material _mainMaterial;
    private TMP_FontAsset _sourceFont;
    private TMP_FontAsset _localFont;
    private string _fallbackSignature;

    private bool isDragging = false;
    private bool isPointing = false;
    private Vector2 initialObjectPosition;
    private Vector2 initialPointerLocal;

    public bool CanDrag => TextConfig.Drag && !TextConfig.Position.IsExpr;

    public static Shader sr_msdf;
    static OverlayerText() => sr_msdf = typeof(ShaderUtilities).GetProperty("ShaderRef_MobileSDF", (BindingFlags)15420)?.GetValue(null) as Shader;

    public void Init(OverlayerProfile profile, TextConfig config) {
        if(Initialized) {
            return;
        }

        Parent = profile;
        TextConfig = config;
        if(string.IsNullOrEmpty(config.Name)) {
            config.Name = $"Text {Parent.ObjectManager.Count + 1}";
        }
        PlayingReplacer = new Replacer(config.PlayingText, TagManager.All.Select(ot => ot.Tag));
        NotPlayingReplacer = new Replacer(config.NotPlayingText, TagManager.NP.Select(ot => ot.Tag));
        TagManager.OnLoadUnload += RefreshTags;
        try {
            DontDestroyOnLoad(gameObject);
            GameObject mainObject = gameObject;
            mainObject.transform.SetParent(Parent.ProfileCanvas.transform);
            mainObject.MakeFlexible();
            Text = mainObject.AddComponent<TextMeshProUGUI>();
            Text.enableVertexGradient = true;
            Text.color = Color.white;
            Text.enableAutoSizing = false;
            var rt = Text.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            config.Init();
            Initialized = true;
            ApplyConfig();
            config.OnDragChanged += (state) => Text.raycastTarget = state;
            Text.raycastTarget = config.Drag;
        } catch {
            TagManager.OnLoadUnload -= RefreshTags;
            PlayingReplacer?.Dispose();
            NotPlayingReplacer?.Dispose();
            TextConfig?.Release();
            throw;
        }
    }

    public void Update() {
        if(!Initialized || Text is null) {
            return;
        }

        string nextText = Main.IsPlaying ? PlayingReplacer?.Replace() ?? "" : NotPlayingReplacer?.Replace() ?? "";
        bool firstText = string.IsNullOrEmpty(Text.text) && !string.IsNullOrEmpty(nextText);
        if(Text.text != nextText) {
            Text.text = nextText;
        }
        EnsureMainMaterial();
        if(firstText) {
            Text.ForceMeshUpdate(true, true);
        }

        if(TextConfig.LineSpacing.GetExprValue(float.Parse, out var ls)) {
            Text.lineSpacing = ls;
        }
        if(TextConfig.LineSpacingAdj.GetExprValue(float.Parse, out var lsa)) {
            Text.lineSpacingAdjustment = lsa;
        }
        if(TextConfig.TextColor.GetExprValue(MiscUtils.ParseGColor, out var color)) {
            Text.colorGradient = color;
        }
        if(TextConfig.Pivot.GetExprValue(MiscUtils.ParseVec2, out var pivot)) {
            Text.rectTransform.pivot = pivot;
        }
        if(TextConfig.Scale.GetExprValue(MiscUtils.ParseVec2, out var scale)) {
            Text.rectTransform.localScale = scale;
        }
        if(TextConfig.Position.GetExprValue(MiscUtils.ParseVec2, out var pos)) {
            Text.rectTransform.anchoredPosition = (pos - new Vector2(0.5f, 0.5f)) * new Vector2(1920, 1080);
        }
        if(TextConfig.Rotation.GetExprValue(MiscUtils.ParseVec3, out var rot)) {
            Text.rectTransform.eulerAngles = rot;
        }
        if(TextConfig.FontSize.GetExprValue(float.Parse, out var fs)) {
            Text.fontSize = fs;
        }

        SyncMaterialAtlas();
        UpdateMaterialExpressions();

        if(isDragging && OverlayerProfile.DragObj is not null && OverlayerProfile.DragImage is not null) {
            OverlayerProfile.DragObj.transform.position = Text.gameObject.transform.position;
            OverlayerProfile.DragObj.transform.rotation = Text.gameObject.transform.rotation;
            OverlayerProfile.DragImage.rectTransform.pivot = Text.rectTransform.pivot;
            OverlayerProfile.DragImage.rectTransform.sizeDelta = new Vector2(Text.preferredWidth, Text.preferredHeight);
        }
    }

    private void RefreshTags() {
        if(Initialized && !isActiveAndEnabled) {
            PlayingReplacer?.Dispose();
            NotPlayingReplacer?.Dispose();
            return;
        }
        PlayingReplacer.UpdateTags(TagManager.All.Select(ot => ot.Tag));
        NotPlayingReplacer.UpdateTags(TagManager.NP.Select(ot => ot.Tag));
        PlayingReplacer.Compile();
        NotPlayingReplacer.Compile();
    }

    public override void ApplyConfig() {
        PlayingReplacer.Source = TextConfig.PlayingText;
        NotPlayingReplacer.Source = TextConfig.NotPlayingText;
        RefreshTags();
        if(Initialized && !isActiveAndEnabled) {
            TextConfig.SuspendExpressions();
        }
        TagManager.UpdatePatch();

        if(TextConfig.LineSpacing.GetNormalValue(out var ls)) {
            Text.lineSpacing = ls;
        }
        if(TextConfig.LineSpacingAdj.GetNormalValue(out var lsa)) {
            Text.lineSpacingAdjustment = lsa;
        }
        if(TextConfig.TextColor.GetNormalValue(out var color)) {
            Text.colorGradient = color;
        }
        if(TextConfig.Pivot.GetNormalValue(out var pivot)) {
            Text.rectTransform.pivot = pivot;
        }
        if(TextConfig.Scale.GetNormalValue(out var scale)) {
            Text.rectTransform.localScale = scale;
        }
        if(TextConfig.Position.GetNormalValue(out var pos)) {
            Text.rectTransform.anchoredPosition = (pos - new Vector2(0.5f, 0.5f)) * new Vector2(1920, 1080);
        }
        if(TextConfig.Rotation.GetNormalValue(out var rot)) {
            Text.rectTransform.eulerAngles = rot;
        }
        if(TextConfig.FontSize.GetNormalValue(out var fs)) {
            Text.fontSize = fs;
        }
        Text.alignment = TextConfig.Alignment;

        SetFont();
        if(Text != null) {
            Text.gameObject.SetActive(TextConfig.Active);
        }
        OnApplyConfig(this);
    }

    public string GetCurrentText() => Text.text;

    public void OnPointerDown(PointerEventData e) {
        if(!CanDrag || OverlayerProfile.IsDragging) {
            return;
        }

        isDragging = true;
        OverlayerProfile.IsDragging = true;

        RectTransform parentRect = Text.rectTransform.parent as RectTransform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect,
            e.position,
            e.pressEventCamera,
            out initialPointerLocal
        );

        initialObjectPosition = Text.rectTransform.anchoredPosition;
    }

    public void OnPointerUp(PointerEventData e) {
        if(!isDragging) {
            return;
        }
        isDragging = false;
        OverlayerProfile.IsDragging = false;
        if(OverlayerProfile.PointingCount == 0 && OverlayerProfile.DragObj) {
            OverlayerProfile.DragObj.SetActive(false);
        }
    }

    public void OnDrag(PointerEventData e) {
        if(!CanDrag || !isDragging) {
            return;
        }

        RectTransform parentRect = Text.rectTransform.parent as RectTransform;
        Vector2 currentPointerLocal;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect,
            e.position,
            e.pressEventCamera,
            out currentPointerLocal
        );

        Vector2 offset = currentPointerLocal - initialPointerLocal;
        Text.rectTransform.anchoredPosition = initialObjectPosition + offset;

        Vector2 canvasSize = new(1920, 1080);
        TextConfig.Position.Value = (Text.rectTransform.anchoredPosition / canvasSize) + new Vector2(0.5f, 0.5f);
    }

    public void OnPointerEnter(PointerEventData e) {
        if(!CanDrag || isPointing) {
            return;
        }
        isPointing = true;
        OverlayerProfile.PointingCount++;
        if(!OverlayerProfile.IsDragging) {
            OverlayerProfile.DragObj.transform.position = Text.gameObject.transform.position;
            OverlayerProfile.DragObj.transform.rotation = Text.gameObject.transform.rotation;
            OverlayerProfile.DragImage.rectTransform.pivot = Text.rectTransform.pivot;
            OverlayerProfile.DragImage.rectTransform.sizeDelta = new Vector2(Text.preferredWidth, Text.preferredHeight);
        }
        OverlayerProfile.DragObj.SetActive(true);
    }

    public void OnPointerExit(PointerEventData e) {
        if(!isPointing) {
            return;
        }
        isPointing = false;
        OverlayerProfile.PointingCount = System.Math.Max(0, OverlayerProfile.PointingCount - 1);
        if(OverlayerProfile.PointingCount == 0) {
            if(!OverlayerProfile.IsDragging) {
                OverlayerProfile.DragObj.SetActive(false);
            }
        }
    }

    private TMP_FontAsset BuildFallbackFont(TMP_FontAsset baseFont) {
        if(!TextConfig.EnableFallbackFonts || TextConfig.FallbackFonts == null) {
            return null;
        }

        List<TMP_FontAsset> fallback = new();
        foreach(var name in TextConfig.FallbackFonts) {
            if(string.IsNullOrWhiteSpace(name) || !FontManager.TryGetFont(name, out FontData font) ||
               !font.fontTMP || font.fontTMP == baseFont || fallback.Contains(font.fontTMP)) {
                continue;
            }
            fallback.Add(font.fontTMP);
        }

        if(fallback.Count == 0) {
            return null;
        }

        foreach(var inherited in baseFont.fallbackFontAssetTable ?? new List<TMP_FontAsset>()) {
            if(inherited && inherited != baseFont && !fallback.Contains(inherited)) {
                fallback.Add(inherited);
            }
        }

        TMP_FontAsset local = Instantiate(baseFont);
        if(local) {
            local.fallbackFontAssetTable = fallback;
        }
        return local;
    }

    private void SetFont() {
        TMP_FontAsset baseFont = FontManager.GetFontSafe(TextConfig.Font).fontTMP;
        if(!baseFont || !baseFont.material) {
            baseFont = TMP_Settings.defaultFontAsset;
        }
        if(!baseFont || !baseFont.material) {
            Main.Logger?.Log("[OverlayerText] No usable default font asset.");
            return;
        }

        string fallbackSignature = TextConfig.EnableFallbackFonts
            ? string.Join("\u001f", TextConfig.FallbackFonts ?? Array.Empty<string>())
            : "";

        bool changed = _sourceFont != baseFont || _fallbackSignature != fallbackSignature;
        TMP_FontAsset nextLocal = changed ? BuildFallbackFont(baseFont) : _localFont;
        TMP_FontAsset selected = nextLocal ? nextLocal : baseFont;
        TMP_FontAsset oldLocal = _localFont;

        if(changed || !_mainMaterial) {
            Material source = selected.material ? selected.material : baseFont.material;
            if(!_mainMaterial) {
                _mainMaterial = new Material(source);
            } else {
                _mainMaterial.shader = source.shader;
                _mainMaterial.CopyPropertiesFromMaterial(source);
            }
            if(sr_msdf) {
                _mainMaterial.shader = sr_msdf;
            }
            _mainMaterial.EnableKeyword(ShaderUtilities.Keyword_Outline);
            _mainMaterial.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        }

        if(changed) {
            _sourceFont = baseFont;
            _localFont = nextLocal;
            _fallbackSignature = fallbackSignature;
        }
        if(Text.font != selected) {
            Text.font = selected;
        }
        EnsureMainMaterial();
        UpdateMaterials();
        UpdateMaterialExpressions();

        if(changed) {
            Text.ForceMeshUpdate(true, true);
            if(oldLocal && oldLocal != nextLocal) {
                Destroy(oldLocal);
            }
        }
    }

    private void EnsureMainMaterial() {
        if(!Text || !_mainMaterial) {
            return;
        }
        TMP_FontAsset selected = _localFont ? _localFont : _sourceFont;
        bool fontChanged = selected && Text.font != selected;
        if(fontChanged) {
            Text.font = selected;
        }
        if(Text.fontSharedMaterial != _mainMaterial) {
            Text.fontSharedMaterial = _mainMaterial;
        }
        if(fontChanged) {
            Text.ForceMeshUpdate(true, true);
        }
    }

    private void SyncMaterialAtlas() {
        if(!_mainMaterial || !Text.font || !Text.font.atlasTexture ||
           !_mainMaterial.HasProperty(ShaderUtilities.ID_MainTex)) {
            return;
        }

        if(_mainMaterial.GetTexture(ShaderUtilities.ID_MainTex) != Text.font.atlasTexture) {
            _mainMaterial.SetTexture(ShaderUtilities.ID_MainTex, Text.font.atlasTexture);
            Text.SetVerticesDirty();
            Text.SetMaterialDirty();
        }
    }

    private static bool SetColorIfChanged(Material material, int property, Color value) {
        if(!material.HasProperty(property) || material.GetColor(property) == value) {
            return false;
        }
        material.SetColor(property, value);
        return true;
    }

    private static bool SetFloatIfChanged(Material material, int property, float value) {
        if(!material.HasProperty(property) || Mathf.Approximately(material.GetFloat(property), value)) {
            return false;
        }
        material.SetFloat(property, value);
        return true;
    }

    private void UpdateMaterials() {
        if(!_mainMaterial) {
            return;
        }

        bool changed = false;
        bool paddingChanged = false;
        Material mat = _mainMaterial;

        if(TextConfig.OutlineColor.GetNormalValue(out var oc)) {
            changed |= SetColorIfChanged(mat, ShaderUtilities.ID_OutlineColor, oc);
        }
        if(TextConfig.OutlineWidth.GetNormalValue(out var ow)) {
            bool updated = SetFloatIfChanged(mat, ShaderUtilities.ID_OutlineWidth, ow);
            changed |= updated;
            paddingChanged |= updated;
        }
        if(TextConfig.ShadowColor.GetNormalValue(out var sc)) {
            changed |= SetColorIfChanged(mat, ShaderUtilities.ID_UnderlayColor, sc);
        }
        if(TextConfig.ShadowOffset.GetNormalValue(out var so)) {
            bool updated = SetFloatIfChanged(mat, ShaderUtilities.ID_UnderlayOffsetX, so.x);
            updated |= SetFloatIfChanged(mat, ShaderUtilities.ID_UnderlayOffsetY, so.y);
            changed |= updated;
            paddingChanged |= updated;
        }
        if(TextConfig.ShadowDilate.GetNormalValue(out var sd)) {
            bool updated = SetFloatIfChanged(mat, ShaderUtilities.ID_UnderlayDilate, 1 - sd);
            changed |= updated;
            paddingChanged |= updated;
        }
        if(TextConfig.ShadowSoftness.GetNormalValue(out var ss)) {
            bool updated = SetFloatIfChanged(mat, ShaderUtilities.ID_UnderlaySoftness, 1 - ss);
            changed |= updated;
            paddingChanged |= updated;
        }

        if(changed) {
            if(paddingChanged) {
                Text.UpdateMeshPadding();
            }
            Text.SetMaterialDirty();
            if(Text.textInfo != null && Text.textInfo.materialCount > 1) {
                Text.SetVerticesDirty();
            }
        }
    }

    private void UpdateMaterialExpressions() {
        if(!_mainMaterial) {
            return;
        }
        bool changed = false;
        bool paddingChanged = false;
        Material mat = _mainMaterial;

        if(TextConfig.OutlineColor.GetExprValue(MiscUtils.ParseColor, out var oc)) {
            changed |= SetColorIfChanged(mat, ShaderUtilities.ID_OutlineColor, oc);
        }
        if(TextConfig.OutlineWidth.GetExprValue(float.Parse, out var ow)) {
            bool updated = SetFloatIfChanged(mat, ShaderUtilities.ID_OutlineWidth, ow);
            changed |= updated;
            paddingChanged |= updated;
        }
        if(TextConfig.ShadowColor.GetExprValue(MiscUtils.ParseColor, out var sc)) {
            changed |= SetColorIfChanged(mat, ShaderUtilities.ID_UnderlayColor, sc);
        }
        if(TextConfig.ShadowOffset.GetExprValue(MiscUtils.ParseVec2, out var so)) {
            bool updated = SetFloatIfChanged(mat, ShaderUtilities.ID_UnderlayOffsetX, so.x);
            updated |= SetFloatIfChanged(mat, ShaderUtilities.ID_UnderlayOffsetY, so.y);
            changed |= updated;
            paddingChanged |= updated;
        }
        if(TextConfig.ShadowDilate.GetExprValue(float.Parse, out var sd)) {
            bool updated = SetFloatIfChanged(mat, ShaderUtilities.ID_UnderlayDilate, 1 - sd);
            changed |= updated;
            paddingChanged |= updated;
        }
        if(TextConfig.ShadowSoftness.GetExprValue(float.Parse, out var ss)) {
            bool updated = SetFloatIfChanged(mat, ShaderUtilities.ID_UnderlaySoftness, 1 - ss);
            changed |= updated;
            paddingChanged |= updated;
        }

        if(changed) {
            if(paddingChanged) {
                Text.UpdateMeshPadding();
            }
            Text.SetMaterialDirty();
            if(Text.textInfo != null && Text.textInfo.materialCount > 1) {
                Text.SetVerticesDirty();
            }
        }
    }
    private void OnEnable() {
        if(!Initialized) {
            return;
        }

        TextConfig?.ResumeExpressions();
        EnsureMainMaterial();
        RefreshTags();
        if(TagManager.Initialized) {
            TagManager.UpdatePatch();
        }
    }

    private void OnDisable() {
        if(!Initialized) {
            return;
        }

        if(isDragging) {
            isDragging = false;
            OverlayerProfile.IsDragging = false;
        }
        if(isPointing) {
            isPointing = false;
            OverlayerProfile.PointingCount = System.Math.Max(0, OverlayerProfile.PointingCount - 1);
        }
        if(!OverlayerProfile.IsDragging && OverlayerProfile.PointingCount == 0 && OverlayerProfile.DragObj) {
            OverlayerProfile.DragObj.SetActive(false);
        }
        TextConfig?.SuspendExpressions();
        PlayingReplacer?.Dispose();
        NotPlayingReplacer?.Dispose();
        if(TagManager.Initialized) {
            TagManager.UpdatePatch();
        }
    }

    private void OnDestroy() {
        TagManager.OnLoadUnload -= RefreshTags;
        PlayingReplacer?.Dispose();
        NotPlayingReplacer?.Dispose();
        TextConfig?.Release();
        if(_localFont) {
            Destroy(_localFont);
        }
        if(_mainMaterial) {
            Destroy(_mainMaterial);
        }
    }
}
