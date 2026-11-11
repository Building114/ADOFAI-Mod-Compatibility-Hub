using Overlayer.Core;
using Overlayer.Core.TextReplacing;
using Overlayer.Tags;
using RapidGUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

namespace Overlayer.Utils;

internal sealed class RichTextGuidePopup : MonoBehaviour {
    private const int MaxPreviewCharacters = 200;
    private const float PreviewIntervalSeconds = 0.125f;

    private sealed class GuideRow {
        internal readonly string Tag, TitleZh, TitleEn, DescZh, DescEn, NoteZh, NoteEn, Example;
        internal GuideRow(string tag, string titleZh, string titleEn, string descZh, string descEn,
                          string noteZh, string noteEn, string example) {
            Tag = tag; TitleZh = titleZh; TitleEn = titleEn;
            DescZh = descZh; DescEn = descEn;
            NoteZh = noteZh; NoteEn = noteEn; Example = example;
        }
    }

    private sealed class GuideGroup {
        internal readonly string Zh, En;
        internal readonly GuideRow[] Rows;
        internal GuideGroup(string zh, string en, GuideRow[] rows) {
            Zh = zh; En = en; Rows = rows;
        }
    }

    private sealed class RuleRow {
        internal readonly string TitleZh, TitleEn, Zh, En, Example;
        internal RuleRow(string titleZh, string titleEn, string zh, string en, string example) {
            TitleZh = titleZh; TitleEn = titleEn;
            Zh = zh; En = en; Example = example;
        }
    }

    private static readonly RuleRow[] Rules = {
        new RuleRow("先认这两套括号", "Two kinds of tags", "尖括号 <...> 是 TextMeshPro 富文本，它会管理颜色字号这些外观，花括号 {...} 是 Overlayer 的Tag，管理数值和动态内容。游戏先处理Overlayer Tag再把结果给TextMeshPro。", "<...> is TextMeshPro rich text for colors, size, and other styling. {...} is an Overlayer Tag for values and dynamic text. Overlayer handles its Tags first, then passes the result to TextMeshPro.", "<b><color=#FFCC66>{Progress;0.0}%</color></b>"),
        new RuleRow("注意：整段代码放一行！", "Important: keep the code on one line!", "像这种多层套娃复制时必须连成一行。分成多行只是方便查看嵌套，但实际会多出换行。所以别在 <color=...>、<size=...> 中间按回车。", "When you copy nested tags, keep the whole thing on one line. Breaking it up may help you read the nesting, but those line breaks become real text. Don't press Enter in the middle of <color=...> or <size=...>.", "<color=#{ColorPulse(FFAA55,5599FF,1200)}><size={Wave(85,125,1200)}%>动态文字</size></color>"),
        new RuleRow("最简单的 Overlayer Tag", "Basic Overlayer Tags", "{Progress} 读取当前进度，{Calc:1+2*3} 可以计算数学表达式（当然支持嵌套Tag），{Wave(0,100,1200)} 用三个参数控制动态数值（当然这里只是一些示例）。参数不合适时可能返回默认值。", "{Progress} reads your current progress. {Calc:1+2*3} does math (nested Tags work too), and {Wave(0,100,1200)} generates a changing value. These are just examples. Bad arguments may produce defaults.", "{Progress} / {Calc:1+2*3} / {Wave(0,100,1200)}"),
        new RuleRow("数字格式化", "Number formatting", "数值Tag后面可以加分号和格式，例如 ;0.00 保留两位小数。至于 :.1f，我还没确认这种写法能不能用，暂时别照抄。文本Tag就不能这么格式化。", "Numeric Tags can take formats such as ;0.00 for two decimal places. I'm not sure about :.1f, so don't copy that without checking. Text Tags don't use this numeric formatting.", "{Progress;0.00}%"),
        new RuleRow("富文本可以一层套一层", "Rich text can nest", "外面加粗里面改颜色没问题。但是闭合顺序要反着来：先关color再关b（实际上你不关也能显示，只是样式可能影响后面的文字）", "Bold on the outside, color on the inside. Close color before b. Leaving tags unclosed may still render, but the style can leak into later text.", "<b><color=#FFCC66>粗体金色~</color></b>"),
        new RuleRow("动态颜色", "Animated colors", "ColorPulse在两种颜色之间来回变化。色值通常不用额外加#。周期单位是毫秒，例如1200为1.2秒。", "ColorPulse moves between two colors. You usually don't add a second #. The period is in milliseconds, so 1200 is 1.2 seconds.", "<color=#{ColorPulse(FFAA55,5599FF,1200)}>变色</color>"),
        new RuleRow("动态字号", "Animated size", "Wave负责生成变化的数字，后面接%给size（注意只是示例！不一定只能给Wave）。", "Wave generates a moving number, and % makes it a size percentage. Just an example: you don't have to use Wave.", "<size={Wave(85,125,1200)}%>放大缩小</size>"),
        new RuleRow("Tag 参数里还能套 Tag（嵌套我们喜欢你）", "Tags inside Tags (we love nesting)", "ColorRange的第一个参数可以写{Wave(...)}，内层会先算出数字，外层再转成颜色。", "ColorRange can take {Wave(...)} as its first argument. The inner Tag gets the number; the outer Tag turns it into a color.", "<color=#{ColorRange({Wave(0,100,1200)},0,100,FF6600,00AACC)}>颜色渐变</color>"),
        new RuleRow("高亮加动态文字", "Highlight and live text", "mark给字加背景，#FFFF0088最后两位是Alpha，即透明度。", "mark adds a background. The final two digits of #FFFF0088 are alpha, meaning transparency.", "<mark=#FFFF0088><b>{Calc:1+2*3}</b></mark>"),
        new RuleRow("让文字上下动（也许你更想用MovingMan）", "Move text up and down (or use MovingMan)", "voffset可以接PingPong的动态数值（只是示例！），负数向下、正数向上。可以把单位em写在属性里（但其实也不一定）。", "voffset can take a moving PingPong value (just an example). Negative goes down; positive goes up. You can use em for the unit, but it isn't the only option.", "<voffset={PingPong(-0.3,0.3,1500)}em>上下移动</voffset>"),
        new RuleRow("想显示花括号原文？", "Want literal braces?", "在左花括号前面加反斜杠：\\{Progress}，就会显示{Progress}而不读取数据。连续两个左花括号{{也会显示一个字面的{。{{Progress}}就不是一个正常Tag。", "Put a backslash before the opening brace: \\{Progress} displays {Progress} without reading the value. Two opening braces {{ also display one literal {. {{Progress}} isn't a normal Tag.", "\\{Progress}  |  {{"),
        new RuleRow("noparse 管不到 Overlayer（笑哭）", "noparse won't stop Overlayer", "<noparse>只会关掉TextMeshPro对<...>的识别。Overlayer的{...}会优先处理完成，若要保留字面量请用反斜杠。", "<noparse> stops TextMeshPro from parsing <...>. Overlayer processes {...} earlier. Use a backslash for literal Overlayer Tags.", "<noparse><b>{Progress}</b></noparse>"),
        new RuleRow("条件 Tag 别随便嵌套", "Watch conditional Tags", "一般的参数会先算内部Tag。但Coalesce、IfExpr、IfText的某些参数会延迟处理！请看对应说明。", "Most Tag arguments evaluate nested Tags first, but some Coalesce, IfExpr and IfText arguments are deferred. Check their own help.", "{Coalesce:{Progress}:0}"),
        new RuleRow("资源标签有条件", "Assets required", "font、gradient、sprite、style需要有对应的资源。link只是标记，不会打开网页，而page要用到分页模式。这里能不能预览我都不敢保证……", "font, gradient, sprite and style need the relevant resources. link is just a marker, and page needs Page overflow mode. I can't promise these will preview here.", "<sprite name=\"star\"> <style=\"Title\">例子</style>"),
        new RuleRow("实验区", "The experiment", "示例不会保存！预览使用独立文字框，效果可能与真实配置有差别。", "Examples aren't saved! This uses a separate preview text object, so the result may differ from your actual setup.", "<b><mark=#FFFF0088>试试改这里的文字</mark></b>")
    };

    private static readonly GuideGroup[] TagGroups = {
        new GuideGroup("文字样式", "Text Styles", new[] {
            new GuideRow("<b>", "粗体", "Bold", "把这段文字加粗。", "Makes the enclosed text bold.", "字库没有粗字重时，变化可能很小。", "A font without a bold face may show little difference.", "<b>Bold</b>"),
            new GuideRow("<i>", "斜体", "Italic", "把文字倾斜一点。", "Tilts the enclosed text.", "部分字体没有真正的斜体，会模拟倾斜。", "Some fonts fake the italic slant.", "<i>Italic</i>"),
            new GuideRow("<u>", "下划线", "Underline", "给文字画下划线。", "Draws a line under text.", "颜色通常跟随当前文字颜色。", "The line generally follows text color.", "<u>Underline</u>"),
            new GuideRow("<s>", "删除线", "Strikethrough", "给文字加删除线。", "Strikes out the enclosed text.", "也能写成长标签<strikethrough>。谁那么闲", "The long <strikethrough> form works too. Who even types all that?", "<s>Crossed out</s>"),
            new GuideRow("<strikethrough>", "删除线（长写法）", "Strike (long form)", "作用和<s>一样。", "Same behavior as <s>.", "只是换了名字，不改变强度。", "It's an alias, not a stronger strike.", "<strikethrough>Old</strikethrough>"),
            new GuideRow("<color>", "文字颜色", "Text color", "用#RRGGBB或#RRGGBBAA指定颜色。", "Set a color using #RRGGBB or #RRGGBBAA.", "只写<#FF6600>也行。其实不闭合也可能显示，只是样式会继续影响后文。", "You can use <#FF6600> too. Leaving tags open may still render, but can affect later text.", "<color=#FFCC66>Gold</color>"),
            new GuideRow("<alpha>", "透明度", "Opacity", "用十六进制00到FF指定透明度。", "Set alpha from 00 to FF in hexadecimal.", "例如#88约半透明，会影响后面的文字。", "#88 is roughly half-transparent and affects following text.", "<alpha=#88>Half-transparent"),
            new GuideRow("<mark>", "文字高亮", "Highlight", "给文字加一层背景色。", "Adds a background color behind text.", "建议写8位颜色值，比如#FFFF0088，末尾两位是透明度。", "Use eight digits such as #FFFF0088. The last two digits set alpha.", "<mark=#FFFF0088>Marked</mark>"),
            new GuideRow("<size>", "字号", "Font size", "可以写具体数值、百分比或em（一种单位）。", "Use a number, percentage, or em unit.", "百分比相对于当前字体大小。", "Percentages are relative to current font size.", "<size=130%>Bigger</size>"),
            new GuideRow("<font-weight>", "字重", "Font weight", "用100到900一类的值改变字重。", "Changes weight using values such as 100 through 900.", "字库需要有对应字重，缺失时可能看不出效果。", "The font needs the corresponding weights.", "<font-weight=700>Heavy</font-weight>"),
            new GuideRow("<sub>", "下标", "Subscript", "把局部文字缩小并下沉。", "Shrinks and lowers a bit of text.", "例如H₂O里的2。化学式都来了（？？？）", "Like the 2 in H₂O. Yes, chemical formulas, apparently.", "H<sub>2</sub>O"),
            new GuideRow("<sup>", "上标", "Superscript", "把局部文字缩小并抬高。", "Shrinks and raises a bit of text.", "例如m²里的2。好吧这还算有点用。", "Like the 2 in m². Okay, this is useful.", "m<sup>2</sup>"),
        }),
        new GuideGroup("大小写与字形", "Letter Case", new[] {
            new GuideRow("<uppercase>", "全部大写", "Uppercase", "把里面的英文转成大写。", "Converts letters to uppercase.", "中文字符不会凭空变成大写。", "Scripts without uppercase forms stay unchanged.", "<uppercase>hello</uppercase>"),
            new GuideRow("<allcaps>", "全部大写（别名）", "All caps (alias)", "作用和<uppercase>类似。", "Works like <uppercase>.", "另一种写法而已。", "It's another spelling for the same effect.", "<allcaps>hello</allcaps>"),
            new GuideRow("<lowercase>", "全部小写", "Lowercase", "把英文大写字母变成小写。", "Converts letters to lowercase.", "受字符集和字形限制。", "Results depend on available glyphs.", "<lowercase>HELLO</lowercase>"),
            new GuideRow("<smallcaps>", "小型大写", "Small caps", "小写字母显示成较小的大写字形。", "Displays lowercase letters as small capitals.", "实际效果取决于字体字形，geometrydash还是太有用了。", "Depends on the font. Geometry Dash really does come in handy.", "<smallcaps>Hello</smallcaps>"),
        }),
        new GuideGroup("排版与位置", "Layout & Spacing", new[] {
            new GuideRow("<align>", "行对齐", "Alignment", "可以用left、center、right等值。", "Supports left, center, right, and other values.", "文字对象的大小和对齐设置会影响结果。", "The text box geometry affects the result.", "<align=center>Centered</align>"),
            new GuideRow("<br>", "强制换行", "Line break", "从这里换到下一行。", "Starts a new line at this point.", "这和把一个动态Tag拆成两行是两回事。", "It's different from splitting a Tag across lines.", "First<br>Second"),
            new GuideRow("<cspace>", "字距", "Character spacing", "调节字符之间的额外距离。", "Adds space between characters.", "带em单位更容易看懂。", "Using em makes the amount easier to judge.", "<cspace=0.2em>W I D E</cspace>"),
            new GuideRow("<mspace>", "等宽占位", "Fixed character width", "让字符按固定宽度前进。", "Uses a fixed advance for each character.", "这不会把字体真的换成等宽字体。真要用自己去下。", "It won't magically install a monospaced font. Go get one if you need it.", "<mspace=1em>12345</mspace>"),
            new GuideRow("<line-height>", "行高", "Line height", "修改多行之间的距离。", "Changes the height of lines.", "只有一行时基本看不出来。", "A single line won't show much difference.", "<line-height=120%>One<br>Two"),
            new GuideRow("<line-indent>", "换行后的缩进", "Line indent", "控制新行开始的位置。", "Indents lines after hard breaks.", "与<br>一起用才明显一些。", "Use it with <br> to see the effect.", "<line-indent=15%>One<br>Two"),
            new GuideRow("<indent>", "段落缩进", "Indent", "把后续文字的起点往右挪。", "Moves the following text's starting point.", "它会受当前行宽影响。", "The available text width affects the result.", "<indent=15%>Indented</indent>"),
            new GuideRow("<margin>", "左右边距", "Margins", "缩进文字区域的左右两侧。", "Changes the left and right edges.", "还可以分别写margin-left和margin-right。", "margin-left and margin-right are also available.", "<margin=5%>Content</margin>"),
            new GuideRow("<pos>", "水平定位", "Horizontal position", "从当前行的指定位置开始放字。", "Sets a position on the current line.", "不会移动整个Overlayer文字对象。", "Does not move the entire Overlayer object.", "Left<pos=50%>Middle"),
            new GuideRow("<space>", "水平空白", "Horizontal space", "在文字之间插入固定空白。", "Inserts a fixed amount of horizontal space.", "空白是距离而非普通空格字符！", "It's a distance, not an ordinary space character.", "Left<space=2em>Right"),
            new GuideRow("<voffset>", "基线偏移", "Baseline offset", "把局部字往上或往下挪。", "Moves some text above or below the baseline.", "值可以为负。这有什么好注意的。", "Negative values are allowed. What else would you expect?", "<voffset=0.5em>Up</voffset>Normal"),
            new GuideRow("<width>", "文字区域宽度", "Text width", "限制后续内容的排版宽度。", "Constrains the text layout width.", "实际可用空间仍受对象大小限制。", "It's still limited by the text object's size.", "<width=60%>Narrow column</width>"),
            new GuideRow("<nobr>", "禁止自动换行", "Keep on one line", "让某一段尽量保持在同一行。", "Prevents automatic wrapping within a fragment.", "它管自动换行，窗口实在太窄，文字仍可能溢出。", "A very narrow box may still overflow. That's what no-wrap means.", "<nobr>Keep this text together</nobr>"),
            new GuideRow("<rotate>", "旋转字符", "Rotate glyphs", "把里面的字符逐个旋转。", "Rotates the enclosed characters.", "注意是字符旋转，不是整个文字对象旋转。", "Rotates glyphs, not the entire object.", "<rotate=15>Rotate</rotate>"),
        }),
        new GuideGroup("依赖资源的功能", "Assets & Special Cases", new[] {
            new GuideRow("<font>", "切换字体资源", "Font asset", "用已加载的TMP Font Asset名称切换字体。", "Switches to a loaded TMP Font Asset.", "没安装对应资源就不会生效（实际上我都没试过这个）。", "Needs the matching resource. I haven't actually tried this one in-game.", "<font=\"Some SDF Font\">Font</font>"),
            new GuideRow("<gradient>", "渐变预设", "Gradient preset", "使用已存在的顶点渐变预设。", "Uses a named vertex-gradient preset.", "需要项目提供同名资源。", "Requires a matching asset; an invented name won't work.", "<gradient=\"MyGradient\">Text</gradient>"),
            new GuideRow("<sprite>", "内嵌图片", "Inline sprite", "用TMP Sprite Asset插入图标（其实我们有图片对象的，你要不试试那个）。", "Inserts an icon from a TMP Sprite Asset. We also have an image object, you know.", "没有可用的Sprite Asset时会无法显示。", "Requires a matching Sprite Asset or it won't display.", "<sprite name=\"star\">"),
            new GuideRow("<style>", "命名样式", "Named style", "应用TMP Style Sheet里定义好的样式。", "Uses an entry in the TMP Style Sheet.", "必须先有同名样式。", "The named style must already exist.", "<style=\"Title\">Heading</style>"),
            new GuideRow("<link>", "链接标记", "Link marker", "给文字附加一个可识别的link ID。", "Attaches a link ID to text.", "这不会自动变成能点开的网页。bro做梦呢爆紫自动启动云原神。", "It won't open a website automatically. Bro, imagine it launches Cloud Genshin on its own.", "<link=\"item1\">Open</link>"),
            new GuideRow("<page>", "分页符", "Page break", "在分页模式中切换到下一页。", "Starts a new page in Page overflow mode.", "Overlayer默认文字框并未确认启用分页模式。这到底是什么东西……", "The Overlayer text object hasn't been confirmed to use that mode. What even is this thing?", "Page 1<page>Page 2"),
            new GuideRow("<noparse>", "保留TMP标签原文", "Literal TMP markup", "里面的<...>会当普通文本显示。", "Displays enclosed TMP tags literally.", "只管TMP标签，挡不住更早处理的Overlayer{...}。", "It does not prevent earlier Overlayer {...} substitution.", "<noparse><b>Literal tag</b></noparse>")
        })
    };

    private static readonly HashSet<string> PreviewTagNames = new(StringComparer.Ordinal) {
        "Progress", "BestProgress", "Calc", "ExprText", "Wave", "PingPong", "ColorPulse", "ColorRange"
    };

    private static readonly Regex InlineTags = new(@"\{([A-Za-z_][A-Za-z_0-9]*)", RegexOptions.Compiled);
    private static readonly Regex MathTag = new(@"\{(?:Calc|ExprText)[:(]([^{}]*)\}", RegexOptions.Compiled);
    private static readonly Regex SafeMath = new(@"^[0-9+\-*/%^().,: \t]+$", RegexOptions.Compiled);
    private static readonly Regex UnsafeColorRangeSource =
        new(@"\{ColorRange\(\s*[A-Za-z_][A-Za-z_0-9]*", RegexOptions.Compiled);

    private sealed class PreviewSurface : IDisposable {
        private readonly GameObject root;
        private readonly Camera camera;
        private readonly TextMeshProUGUI text;
        internal readonly RenderTexture Texture;

        internal PreviewSurface() {
            root = new GameObject("Overlayer Guide Preview");
            UnityEngine.Object.DontDestroyOnLoad(root);
            try {
                GameObject cameraObject = new GameObject("Preview Camera");
                cameraObject.layer = 31;
                cameraObject.transform.SetParent(root.transform, false);
                camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.11f, 0.12f, 0.15f, 1f);
                camera.cullingMask = 1 << 31;
                camera.orthographic = true;
                camera.orthographicSize = 1.1f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 40f;
                camera.transform.position = new Vector3(0f, 0f, -10f);

                Texture = new RenderTexture(768, 192, 16, RenderTextureFormat.ARGB32);
                Texture.Create();
                camera.targetTexture = Texture;

                GameObject canvasObject = new GameObject("Preview Canvas", typeof(RectTransform));
                canvasObject.layer = 31;
                canvasObject.transform.SetParent(root.transform, false);
                RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
                canvasRect.sizeDelta = new Vector2(768f, 192f);
                canvasRect.localScale = Vector3.one * 0.01f;

                Canvas canvas = canvasObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = camera;

                GameObject textObject = new GameObject("Preview Text", typeof(RectTransform));
                textObject.layer = 31;
                textObject.transform.SetParent(canvasObject.transform, false);
                RectTransform rect = textObject.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(20f, 10f);
                rect.offsetMax = new Vector2(-20f, -10f);

                text = textObject.AddComponent<TextMeshProUGUI>();
                TMP_FontAsset defaultFont = FontManager.Initialized
                    ? FontManager.GetFontSafe("Default").fontTMP
                    : null;
                text.font = defaultFont ? defaultFont : TMP_Settings.defaultFontAsset;
                if(!text.font) {
                    throw new InvalidOperationException("No TextMeshPro font asset is available.");
                }
                text.fontSize = 100f;
                text.color = Color.white;
                text.alignment = TextAlignmentOptions.MidlineLeft;
                text.richText = true;
                text.textWrappingMode = TextWrappingModes.Normal;
                text.enableAutoSizing = false;
                text.raycastTarget = false;
            } catch {
                Dispose();
                throw;
            }
        }

        internal void Render(string source) {
            if(!camera || !text) {
                return;
            }
            text.text = source ?? string.Empty;
            text.ForceMeshUpdate(true, true);
            Canvas.ForceUpdateCanvases();
            camera.Render();
        }

        public void Dispose() {
            if(camera) {
                camera.targetTexture = null;
            }
            if(Texture) {
                Texture.Release();
                UnityEngine.Object.Destroy(Texture);
            }
            if(root) {
                UnityEngine.Object.Destroy(root);
            }
        }
    }

    private Rect windowRect;
    private Vector2 scrollPosition;
    private bool showingRules = true;
    private int lastScreenWidth;
    private int lastScreenHeight;
    private int activeExample = -1;
    private string exampleText = string.Empty;
    private string draftText = string.Empty;
    private string previewMessage = string.Empty;
    private bool previewDirty;
    private bool previewFailed;
    private float nextPreviewTime;
    private PreviewSurface preview;

    private static bool Chinese => string.Equals(Main.Lang?.Language, "zh-CN", StringComparison.OrdinalIgnoreCase);
    private static string Local(string zh, string en) => Chinese ? zh : en;
    private static string TrimToLimit(string input) =>
        (input ?? string.Empty).Length <= MaxPreviewCharacters
            ? input ?? string.Empty
            : input.Substring(0, MaxPreviewCharacters);

    private void CalculateWindowRect() {
        float width = Mathf.Min(950f, Mathf.Max(220f, Screen.width * 0.88f));
        float height = Mathf.Min(720f, Mathf.Max(220f, Screen.height * 0.86f));
        windowRect = new Rect(
            (Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height
        );
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
    }

    private void OnGUI() {
        if(Main.Mod == null || Main.Lang == null) {
            return;
        }
        if(lastScreenWidth != Screen.width || lastScreenHeight != Screen.height) {
            CalculateWindowRect();
        }
        windowRect = GUI.Window(
            122, windowRect, DrawWindow,
            Local("富文本与Tag帮助", "Rich Text & Tags"),
            RGUIStyle.darkWindow
        );
    }

    private void DrawWindow(int id) {
        GUI.BringWindowToFront(id);
        GUIStyle body = new GUIStyle(GUI.skin.label) {
            richText = false,
            wordWrap = true,
            fontSize = 15
        };
        GUIStyle note = new GUIStyle(body) { fontSize = 13 };
        GUIStyle heading = new GUIStyle(body) { fontSize = 17, fontStyle = FontStyle.Bold };
        GUIStyle groupHeading = new GUIStyle(body) { fontSize = 19, fontStyle = FontStyle.Bold };
        GUIStyle sample = new GUIStyle(GUI.skin.textArea) {
            richText = false,
            wordWrap = true,
            fontSize = 14,
            alignment = TextAnchor.UpperLeft,
            padding = new RectOffset(10, 10, 7, 7)
        };

        GUILayout.BeginVertical();
        GUILayout.Space(8f);
        GUILayout.BeginHorizontal();
        if(Drawer.Button(Local("Overlayer规则", "Overlayer Rules"), GUILayout.Height(32f))) {
            SwitchSection(true);
        }
        if(Drawer.Button(Local("TMP富文本", "TMP Rich Text"), GUILayout.Height(32f))) {
            SwitchSection(false);
        }
        GUILayout.EndHorizontal();

        using(var scroll = new GUILayout.ScrollViewScope(
            scrollPosition, GUILayout.Height(Mathf.Max(120f, windowRect.height - 133f)))) {
            scrollPosition = scroll.scrollPosition;
            if(showingRules) {
                DrawRules(body, note, heading, sample);
            } else {
                DrawTags(body, note, heading, groupHeading, sample);
            }
        }

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if(Drawer.Button(Main.Lang.Get("DONE", "Done"), GUILayout.Width(130f), GUILayout.Height(36f))) {
            Destroy(gameObject);
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.Space(6f);
        GUILayout.EndVertical();

        GUI.DragWindow(new Rect(0f, 0f, windowRect.width, 24f));
    }

    private void DrawRules(GUIStyle body, GUIStyle note, GUIStyle heading, GUIStyle sample) {
        GUILayout.Space(6f);
        GUILayout.Label(Local(
            "随便试，不过复杂Tag和脚本不会在这里执行。",
            "Feel free to try things. Complex Tags and scripts won't run here."), body);
        GUILayout.Space(14f);

        for(int i = 0; i < Rules.Length; i++) {
            RuleRow row = Rules[i];
            DrawLesson(i, Local(row.TitleZh, row.TitleEn), Local(row.Zh, row.En),
                       string.Empty, row.Example, body, note, heading, sample);
        }
    }

    private void DrawTags(GUIStyle body, GUIStyle note, GUIStyle heading,
                          GUIStyle groupHeading, GUIStyle sample) {
        GUILayout.Space(6f);
        GUILayout.Label(Local(
            "这里列出37个富文本标签。当然不是每个都能实时预览或有用，因为部分需要字体、Sprite或样式资源。",
            "Here are 37 rich text tags. Of course, some won't preview or do much without the right font, Sprite, or style assets."), body);
        GUILayout.Space(14f);
        int id = Rules.Length;
        foreach(GuideGroup group in TagGroups) {
            GUILayout.Label(Local(group.Zh, group.En), groupHeading);
            GUILayout.Space(6f);
            foreach(GuideRow row in group.Rows) {
                DrawLesson(id++, row.Tag + "  " + Local(row.TitleZh, row.TitleEn),
                           Local(row.DescZh, row.DescEn), Local(row.NoteZh, row.NoteEn),
                           row.Example, body, note, heading, sample);
            }
            GUILayout.Space(10f);
        }
    }

    private void DrawLesson(int id, string headingText, string explanation, string noteText,
                            string example, GUIStyle body, GUIStyle note, GUIStyle heading,
                            GUIStyle sample) {
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Space(6f);
        GUILayout.BeginHorizontal();
        int number = showingRules ? id + 1 : id - Rules.Length + 1;
        GUILayout.Label($"{number:00}. {headingText}", heading, GUILayout.ExpandWidth(true));
        bool selected = activeExample == id;
        if(Drawer.Button(Local(selected ? "收起" : "试试", selected ? "Close" : "Try"),
                         GUILayout.Width(86f), GUILayout.Height(30f))) {
            if(selected) {
                CloseExperiment();
            } else {
                OpenExperiment(id, example);
            }
        }
        GUILayout.EndHorizontal();
        GUILayout.Space(5f);

        if(!string.IsNullOrEmpty(explanation)) {
            GUILayout.Label(explanation, body);
            GUILayout.Space(6f);
        }
        if(!string.IsNullOrEmpty(noteText)) {
            GUILayout.Label(Local("注意：", "Note: ") + noteText, note);
            GUILayout.Space(8f);
        }
        GUILayout.Label(Local("示例代码", "Example"), note);
        GUILayout.Label(example, sample, GUILayout.ExpandWidth(true));
        if(activeExample == id) {
            GUILayout.Space(10f);
            DrawExperiment(body, note, example);
        }
        GUILayout.Space(8f);
        GUILayout.EndVertical();
        GUILayout.Space(12f);
    }

    private void DrawExperiment(GUIStyle body, GUIStyle note, string example) {
        GUILayout.Label(Local(
            "试验输入（最多200字符，临时的，不会保存！）：",
            "Experiment (200 characters max, temporary, NOT saved!):"), note);

        GUIStyle editorStyle = new GUIStyle(GUI.skin.textArea) { wordWrap = true, fontSize = 14 };
        bool wide = windowRect.width >= 650f;
        if(wide) {
            GUILayout.BeginHorizontal();
        }
        GUILayout.BeginVertical(GUILayout.MinWidth(wide ? 260f : 120f));
        string typed = GUILayout.TextArea(draftText, MaxPreviewCharacters, editorStyle, GUILayout.Height(100f));
        typed = TrimToLimit(typed);
        if(typed != draftText) {
            draftText = typed;
            previewDirty = true;
        }
        GUILayout.BeginHorizontal();
        if(Drawer.Button(Local("还原", "Reset"), GUILayout.Width(68f))) {
            draftText = TrimToLimit(exampleText);
            previewDirty = true;
        }
        if(Drawer.Button(Local("复制", "Copy"), GUILayout.Width(68f))) {
            GUIUtility.systemCopyBuffer = draftText;
        }
        GUILayout.FlexibleSpace();
        GUILayout.Label($"{draftText.Length}/{MaxPreviewCharacters}", note);
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();

        GUILayout.BeginVertical(GUILayout.MinWidth(wide ? 260f : 120f));
        GUILayout.Label(Local("TMP预览", "TMP Preview"), note);
        Rect display = GUILayoutUtility.GetRect(240f, 113f, GUILayout.ExpandWidth(true));
        if(preview?.Texture) {
            GUI.DrawTexture(display, preview.Texture, ScaleMode.ScaleToFit);
        } else {
            GUI.Box(display, Local("预览尚未准备", "Preview not ready"));
        }
        if(!string.IsNullOrEmpty(previewMessage)) {
            GUILayout.Label(previewMessage, note);
        }
        GUILayout.EndVertical();
        if(wide) {
            GUILayout.EndHorizontal();
        }

        if(draftText.IndexOf('\n') >= 0 || draftText.IndexOf('\r') >= 0) {
            GUILayout.Label(Local(
                "注意：输入里存在换行（你最好知道你在干什么）。教程里的嵌套代码请合成一整行再放进正式文字框，否则会被错误的换行。",
                "Heads up: there's a line break here (you'd better know what you're doing). Keep nested examples on one line in the real text box or the extra line breaks can break the result."), body);
        }
        GUILayout.Label(Local(
            "这里只能运行少量简单Tag。涉及脚本、额外资源和游戏状态的效果，以真正的文字对象为准。",
            "Only a few simple Tags run here. For scripts, extra assets and game-dependent effects, check your actual text object."), note);
    }

    private void OpenExperiment(int id, string example) {
        activeExample = id;
        exampleText = TrimToLimit(example);
        draftText = exampleText;
        previewMessage = string.Empty;
        previewDirty = true;
        previewFailed = false;
        nextPreviewTime = 0f;
    }

    private void CloseExperiment() {
        activeExample = -1;
        exampleText = string.Empty;
        draftText = string.Empty;
        previewMessage = string.Empty;
        previewDirty = false;
        if(preview != null) {
            preview.Dispose();
            preview = null;
        }
    }

    private void SwitchSection(bool rules) {
        if(showingRules == rules) {
            return;
        }
        showingRules = rules;
        scrollPosition = Vector2.zero;
        CloseExperiment();
    }

    private static string EvaluatePreviewTags(string source, out string warning) {
        warning = string.Empty;
        if(!TagManager.Initialized || source.IndexOf('{') < 0) {
            return source;
        }

        foreach(Match match in InlineTags.Matches(source)) {
            string name = match.Groups[1].Value;
            if(!PreviewTagNames.Contains(name)) {
                warning = Local("这个Tag只显示原文（看起来不支持）。",
                                "This Tag is shown literally (looks unsupported here).");
                return source;
            }
        }

        if(UnsafeColorRangeSource.IsMatch(source) ||
           source.IndexOf("{ColorRange:", StringComparison.Ordinal) >= 0) {
            warning = Local("这里不执行按名称读取其它Tag的ColorRange，请在正式文字对象测试。",
                            "ColorRange that reads another Tag by name is disabled here. Test it on a real text object.");
            return source;
        }

        foreach(Match match in MathTag.Matches(source)) {
            if(!SafeMath.IsMatch(match.Groups[1].Value)) {
                warning = Local("这里的Calc/ExprText只接受简单数字算式。",
                                "Calc/ExprText only takes simple numeric expressions here.");
                return source;
            }
        }

        try {
            var tags = TagManager.All
                .Where(t => PreviewTagNames.Contains(t.Name) &&
                    (t.DeclaringType == typeof(Effect) ||
                     t.DeclaringType == typeof(ProgressStats) ||
                     t.DeclaringType == typeof(ExpressionTags)))
                .Select(t => t.Tag)
                .ToArray();
            using(var content = ReplaceableText.Create(source, tags)) {
                return content.Replace();
            }
        } catch(Exception ex) {
            Main.Logger?.Log("Guide preview Tag error: " + ex.Message);
            warning = Local("Tag解析失败。如果它确实没问题，你可以复制去正式文字框再试。",
                            "Tag parsing failed. If the code is actually fine, copy it to a real text box and try there.");
            return source;
        }
    }

    private void LateUpdate() {
        if(activeExample < 0 || !Main.IsShowGUI || previewFailed ||
           UnityEngine.Time.unscaledTime < nextPreviewTime) {
            return;
        }

        bool animated = draftText.Contains("{Wave") ||
                        draftText.Contains("{PingPong") ||
                        draftText.Contains("{ColorPulse") ||
                        draftText.Contains("{ColorRange");
        if(!previewDirty && !animated) {
            return;
        }

        nextPreviewTime = UnityEngine.Time.unscaledTime + PreviewIntervalSeconds;
        previewDirty = false;
        try {
            preview ??= new PreviewSurface();
            string renderText = EvaluatePreviewTags(draftText, out string warning);
            if(renderText.Length > 400) {
                renderText = renderText.Substring(0, 400);
                warning = Local("输出过长，只预览前400字。", "Output too long. Only the first 400 characters are previewed.");
            }
            preview.Render(renderText);
            previewMessage = warning;
        } catch(Exception ex) {
            Main.Logger?.Log("Guide TMP preview failed: " + ex);
            previewMessage = Local("预览启动失败，尝试复制到正式文字对象里试。",
                                   "Preview failed. Try copying the text to a real text object.");
            previewFailed = true;
            if(preview != null) {
                preview.Dispose();
                preview = null;
            }
        }
    }

    private void OnDestroy() {
        CloseExperiment();
    }
}
