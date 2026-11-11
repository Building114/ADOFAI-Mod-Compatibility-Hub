using Overlayer.Core;
using Overlayer.Core.TextReplacing;
using Overlayer.Models;
using Overlayer.Tags;
using Overlayer.Utils;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Overlayer.Unity;

public class OverlayerImage : OverlayerObject, IPointerDownHandler, IPointerUpHandler, IDragHandler, IPointerEnterHandler, IPointerExitHandler {
    public bool Initialized { get; private set; }

    public override ObjectConfig Config => ImageConfig;
    public ImageConfig ImageConfig { get; private set; }

    public List<Sprite> Images = [];
    public List<ImageManager.ImageLoadState> ImageStates = [];
    private Image _mainImage;

    public Replacer PlayingReplacer;
    public Replacer NotPlayingReplacer;

    private bool isDragging = false;
    private bool isPointing = false;
    private Vector2 initialObjectPosition;
    private Vector2 initialPointerLocal;
    private Vector2 scaleforDrag = Vector2.zero;

    public bool CanDrag => ImageConfig.Drag && !ImageConfig.Position.IsExpr;

    public void Init(OverlayerProfile profile, ImageConfig config) {
        if(Initialized) {
            return;
        }

        Parent = profile;
        ImageConfig = config;
        DontDestroyOnLoad(gameObject);
        GameObject mainObject = gameObject;
        mainObject.transform.SetParent(Parent.ProfileCanvas.transform);
        mainObject.MakeFlexible();

        _mainImage = mainObject.AddComponent<Image>();
        _mainImage.raycastTarget = ImageConfig.Drag;

        PlayingReplacer = new Replacer(ImageConfig.PlayingCommand, TagManager.All.Select(t => t.Tag));
        NotPlayingReplacer = new Replacer(ImageConfig.NotPlayingCommand, TagManager.NP.Select(t => t.Tag));
        TagManager.OnLoadUnload += RefreshTags;
        try {
            PlayingReplacer.Compile();
            NotPlayingReplacer.Compile();

            config.Init();
            Initialized = true;
            ApplyConfig();
            _mainImage.raycastTarget = ImageConfig.Drag;
            _mainImage.rectTransform.anchorMin = Vector2.zero;
            _mainImage.rectTransform.anchorMax = Vector2.one;
            ApplyImages();
            config.OnDragChanged += (state) => _mainImage.raycastTarget = state;
        } catch {
            TagManager.OnLoadUnload -= RefreshTags;
            PlayingReplacer?.Dispose();
            NotPlayingReplacer?.Dispose();
            ImageConfig?.Release();
            throw;
        }
    }

    public void Update() {
        if(!Initialized || _mainImage is null || Images.Count == 0) {
            return;
        }

        string rawCommand = Main.IsPlaying ? PlayingReplacer?.Replace() ?? "" : NotPlayingReplacer?.Replace() ?? "";

        int idx;
        _mainImage.sprite = int.TryParse(rawCommand, out idx) && idx >= 0 && idx < Images.Count ? Images[idx] : ImageManager.DefaultSprite;

        if(ImageConfig.Color.GetExprValue(MiscUtils.ParseColor, out var color)) {
            _mainImage.color = color;
        }
        if(ImageConfig.Pivot.GetExprValue(MiscUtils.ParseVec2, out var pivot)) {
            _mainImage.rectTransform.pivot = pivot;
        }
        if(ImageConfig.Position.GetExprValue(MiscUtils.ParseVec2, out var pos)) {
            _mainImage.rectTransform.anchoredPosition = (pos - new Vector2(0.5f, 0.5f)) * new Vector2(1920, 1080);
        }
        if(ImageConfig.Scale.GetExprValue(MiscUtils.ParseVec2, out var scale)) {
            _mainImage.rectTransform.localScale = scale;
            scaleforDrag = scale;
        }
        if(ImageConfig.Rotation.GetExprValue(MiscUtils.ParseVec3, out var rot)) {
            _mainImage.rectTransform.rotation = Quaternion.Euler(rot);
        }

        if(isDragging && OverlayerProfile.DragObj is not null && OverlayerProfile.DragImage is not null) {
            OverlayerProfile.DragObj.transform.position = _mainImage.transform.position;
            OverlayerProfile.DragObj.transform.rotation = _mainImage.transform.rotation;
            OverlayerProfile.DragImage.rectTransform.pivot = _mainImage.rectTransform.pivot;
            OverlayerProfile.DragImage.rectTransform.sizeDelta = new Vector2(_mainImage.preferredWidth, _mainImage.preferredHeight) * scaleforDrag;
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
        PlayingReplacer.Source = ImageConfig.PlayingCommand;
        NotPlayingReplacer.Source = ImageConfig.NotPlayingCommand;
        RefreshTags();
        if(Initialized && !isActiveAndEnabled) {
            ImageConfig.SuspendExpressions();
        }
        TagManager.UpdatePatch();
        if(ImageConfig.Color.GetNormalValue(out var color)) {
            _mainImage.color = color;
        }
        if(ImageConfig.Pivot.GetNormalValue(out var pivot)) {
            _mainImage.rectTransform.pivot = pivot;
        }
        if(ImageConfig.Position.GetNormalValue(out var pos)) {
            _mainImage.rectTransform.anchoredPosition = (pos - new Vector2(0.5f, 0.5f)) * new Vector2(1920, 1080);
        }
        if(ImageConfig.Scale.GetNormalValue(out var scale)) {
            _mainImage.rectTransform.localScale = scale;
            scaleforDrag = scale;
        }
        if(ImageConfig.Rotation.GetNormalValue(out var rot)) {
            _mainImage.rectTransform.rotation = Quaternion.Euler(rot);
        }
        _mainImage.gameObject.SetActive(ImageConfig.Active);
    }

    public void ApplyImages(bool reload = false) {
        if(reload) {
            foreach(var path in ImageConfig.Images) {
                ImageManager.Invalidate(path);
            }
        }
        Images.Clear();
        ImageStates.Clear();
        _mainImage.sprite = ImageManager.DefaultSprite;
        foreach(var path in ImageConfig.Images) {
            ImageManager.TryGetSprite(path, out Sprite sprite, out var state);
            Images.Add(sprite);
            ImageStates.Add(state);
        }
    }

    public void OnPointerDown(PointerEventData e) {
        if(!CanDrag || OverlayerProfile.IsDragging) {
            return;
        }
        isDragging = true;
        OverlayerProfile.IsDragging = true;
        RectTransform parentRect = _mainImage.rectTransform.parent as RectTransform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, e.position, e.pressEventCamera, out initialPointerLocal);
        initialObjectPosition = _mainImage.rectTransform.anchoredPosition;
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
        RectTransform parentRect = _mainImage.rectTransform.parent as RectTransform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, e.position, e.pressEventCamera, out Vector2 currentPointerLocal);
        Vector2 offset = currentPointerLocal - initialPointerLocal;
        _mainImage.rectTransform.anchoredPosition = initialObjectPosition + offset;
        Vector2 canvasSize = new(1920, 1080);
        ImageConfig.Position.Value = (_mainImage.rectTransform.anchoredPosition / canvasSize) + new Vector2(0.5f, 0.5f);
    }

    public void OnPointerEnter(PointerEventData e) {
        if(!CanDrag || isPointing) {
            return;
        }
        isPointing = true;
        OverlayerProfile.PointingCount++;
        if(!OverlayerProfile.IsDragging) {
            OverlayerProfile.DragObj.transform.position = _mainImage.transform.position;
            OverlayerProfile.DragObj.transform.rotation = _mainImage.transform.rotation;
            OverlayerProfile.DragImage.rectTransform.pivot = _mainImage.rectTransform.pivot;
            OverlayerProfile.DragImage.rectTransform.sizeDelta = new Vector2(_mainImage.preferredWidth, _mainImage.preferredHeight) * scaleforDrag;
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
            if(!OverlayerProfile.IsDragging && OverlayerProfile.DragObj != null) {
                OverlayerProfile.DragObj.SetActive(false);
            }
        }
    }

    private void OnEnable() {
        if(!Initialized) {
            return;
        }

        ImageConfig?.ResumeExpressions();
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
        ImageConfig?.SuspendExpressions();
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
        ImageConfig?.Release();
    }
}