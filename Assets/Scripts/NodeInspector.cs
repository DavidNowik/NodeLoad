using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The inspection window: a right click on a building shows its state, what it makes and every store it has as a meter.
// The frame (HUD/Inspector: Title, Close, Scroll/Content) is in the scene; the rows are made here and reused.
public class NodeInspector : MonoBehaviour
{
    // palette
    static readonly Color Cream = new Color(.96f, .93f, .86f);
    static readonly Color Dim = new Color(.62f, .68f, .78f);
    static readonly Color Outline = new Color(.02f, .02f, .03f);      // the clean black rim of every meter
    static readonly Color Track = new Color(.105f, .125f, .165f);     // the empty part of a meter
    const float Refresh = .12f, BarH = 18f;

    World world;
    Node node;
    Font font;
    Sprite round;
    Text title;
    RectTransform content;
    LineRenderer ring;
    readonly List<RowView> views = new List<RowView>();
    readonly List<InspectRow> rows = new List<InspectRow>();
    float next;

    public bool IsOpen { get { return gameObject.activeSelf; } }

    class RowView
    {
        public RectTransform root;
        public LayoutElement layout;
        public Image icon, line, outline, track, extra, fill, shine;
        public Text left, right;
        public readonly List<Image> dividers = new List<Image>();
    }

    public void Setup(World w)
    {
        world = w;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        title = transform.Find("Title").GetComponent<Text>();
        var close = transform.Find("Close").GetComponent<Button>();
        round = close.GetComponent<Image>().sprite;
        close.onClick.AddListener(Close);
        content = (RectTransform)transform.Find("Scroll/Content");
        gameObject.SetActive(false);
    }

    public void Open(Node n)
    {
        node = n;
        gameObject.SetActive(true);
        title.text = n.def.displayName;
        next = 0;
        Refill();
    }

    public void Close()
    {
        node = null;
        gameObject.SetActive(false);
        if (ring != null) ring.enabled = false;
    }

    void Update()
    {
        // the building may have been sold, or the level reset, while the window is open
        if (node == null) { Close(); return; }
        ShowRing();
        if (Time.unscaledTime < next) return;
        next = Time.unscaledTime + Refresh;
        Refill();
    }

    // a gold ring marks which building is being inspected
    void ShowRing()
    {
        if (ring == null)
        {
            ring = Gfx.Line(null, "Inspect Ring", new Color(1f, .84f, .28f), .1f, Layers.NodeBurst + 1, false);
            Gfx.HexLoop(ring, World.NodeRadius + .16f);
        }
        ring.enabled = true;
        ring.transform.position = node.transform.position;
        float pulse = .5f + .5f * Mathf.Sin(Time.unscaledTime * 5f);
        ring.startColor = ring.endColor = Color.Lerp(new Color(1f, .84f, .28f), Color.white, pulse * .5f);
    }

    void Refill()
    {
        rows.Clear();
        node.Inspect(rows);
        while (views.Count < rows.Count) views.Add(MakeRow());
        for (int i = 0; i < views.Count; i++)
        {
            views[i].root.gameObject.SetActive(i < rows.Count);
            if (i < rows.Count) Apply(views[i], rows[i]);
        }
    }

    // ---- one row ----
    void Apply(RowView v, InspectRow r)
    {
        bool bar = r.kind == InspectRow.Kind.Bar, head = r.kind == InspectRow.Kind.Header, pair = r.kind == InspectRow.Kind.Pair, note = r.kind == InspectRow.Kind.Note;
        v.icon.gameObject.SetActive(false);
        v.line.gameObject.SetActive(head);
        v.outline.gameObject.SetActive(bar);
        v.right.gameObject.SetActive(bar || pair);

        // heights: a heading has air above it, a note or fact grows with its text
        float h;
        if (head) h = 40;
        else if (bar) h = 28 + BarH + 10;
        else if (pair) h = 8 + 22 * Lines(r.value, 27);
        else h = 6 + 22 * Lines(r.label, 40);
        v.layout.preferredHeight = h;

        v.left.text = r.label;
        v.left.fontStyle = head || bar ? FontStyle.Bold : FontStyle.Normal;
        v.left.fontSize = head ? 22 : bar ? 20 : pair ? 19 : 18;
        v.left.color = head ? r.color : pair ? Dim : note ? Dim : Cream;
        v.left.horizontalOverflow = note ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
        v.left.alignment = head ? TextAnchor.LowerLeft : note ? TextAnchor.UpperLeft : bar ? TextAnchor.MiddleLeft : TextAnchor.UpperLeft;
        var l = v.left.rectTransform;
        if (bar)
        {
            l.anchorMin = new Vector2(0, 1); l.anchorMax = new Vector2(.62f, 1);
            l.pivot = new Vector2(0, 1);
            l.offsetMin = new Vector2(r.item != null ? 32 : 0, -26); l.offsetMax = new Vector2(0, 0);
        }
        else if (pair)
        {
            l.anchorMin = new Vector2(0, 0); l.anchorMax = new Vector2(.36f, 1);
            l.offsetMin = Vector2.zero; l.offsetMax = Vector2.zero;
        }
        else
        {
            l.anchorMin = Vector2.zero; l.anchorMax = Vector2.one;
            l.offsetMin = new Vector2(0, head ? 4 : 0); l.offsetMax = Vector2.zero;
        }

        if (head) v.line.color = new Color(r.color.r, r.color.g, r.color.b, .55f);

        if (pair || bar)
        {
            v.right.text = r.value;
            v.right.color = Cream;
            v.right.fontSize = pair ? 19 : 18;
            v.right.fontStyle = bar ? FontStyle.Bold : FontStyle.Normal;
            var rr = v.right.rectTransform;
            if (bar)
            {
                rr.anchorMin = new Vector2(.38f, 1); rr.anchorMax = new Vector2(1, 1);
                rr.pivot = new Vector2(1, 1);
                rr.offsetMin = new Vector2(0, -26); rr.offsetMax = Vector2.zero;
                v.right.alignment = TextAnchor.MiddleRight;
            }
            else
            {
                rr.anchorMin = new Vector2(.36f, 0); rr.anchorMax = new Vector2(1, 1);
                rr.offsetMin = Vector2.zero; rr.offsetMax = Vector2.zero;
                v.right.alignment = TextAnchor.UpperRight;
            }
        }

        if (!bar) return;
        var sprite = r.item != null ? PixelArt.ForItem(r.item, .5f) : null;
        v.icon.gameObject.SetActive(sprite != null);
        if (sprite != null) v.icon.sprite = sprite;

        v.fill.gameObject.SetActive(r.fill > .002f);
        v.extra.gameObject.SetActive(r.extra > r.fill + .002f);
        var fillColor = r.color;
        v.fill.color = fillColor;
        v.extra.color = new Color(fillColor.r, fillColor.g, fillColor.b, .32f);
        v.shine.color = new Color(1f, 1f, 1f, .2f);
        Anchor(v.fill.rectTransform, r.fill);
        Anchor(v.extra.rectTransform, r.extra);

        int seg = r.segments > 1 ? r.segments - 1 : 0;
        while (v.dividers.Count < seg) v.dividers.Add(MakeDivider(v));
        for (int i = 0; i < v.dividers.Count; i++)
        {
            v.dividers[i].gameObject.SetActive(i < seg);
            if (i >= seg) continue;
            float x = (i + 1f) / r.segments;
            var d = v.dividers[i].rectTransform;
            d.anchorMin = new Vector2(x, 0); d.anchorMax = new Vector2(x, 1);
            d.sizeDelta = new Vector2(2, 0);
            d.anchoredPosition = Vector2.zero;
        }
    }

    static void Anchor(RectTransform rt, float fill)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = new Vector2(Mathf.Clamp01(fill), 1);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static int Lines(string s, int perLine)
    {
        if (string.IsNullOrEmpty(s)) return 1;
        return Mathf.Max(1, Mathf.CeilToInt(s.Length / (float)perLine));
    }

    // ---- building the parts ----
    RowView MakeRow()
    {
        var v = new RowView();
        var go = new GameObject("Row", typeof(RectTransform), typeof(LayoutElement));
        go.transform.SetParent(content, false);
        v.root = (RectTransform)go.transform;
        v.layout = go.GetComponent<LayoutElement>();

        v.line = Box(v.root, "Line", null, Color.white);
        var lr = v.line.rectTransform;
        lr.anchorMin = Vector2.zero; lr.anchorMax = new Vector2(1, 0);
        lr.pivot = new Vector2(.5f, 0);
        lr.sizeDelta = new Vector2(0, 2);
        lr.anchoredPosition = Vector2.zero;

        v.left = Label(v.root, "Left");
        v.right = Label(v.root, "Right");

        v.icon = Box(v.root, "Icon", null, Color.white);
        var ir = v.icon.rectTransform;
        ir.anchorMin = ir.anchorMax = ir.pivot = new Vector2(0, 1);
        ir.sizeDelta = new Vector2(24, 24);
        ir.anchoredPosition = new Vector2(0, -1);
        v.icon.preserveAspect = true;

        // the meter: a black rim, a dark track inside it, then the paler "on its way" part, the fill, and a soft shine on the upper half of the fill
        v.outline = Box(v.root, "Meter", round, Outline, true);
        var or = v.outline.rectTransform;
        or.anchorMin = Vector2.zero; or.anchorMax = new Vector2(1, 0);
        or.pivot = new Vector2(.5f, 0);
        or.sizeDelta = new Vector2(0, BarH);
        or.anchoredPosition = new Vector2(0, 3);
        v.track = Box(v.outline.rectTransform, "Track", round, Track, true);
        Inset(v.track.rectTransform, 2.5f);
        v.extra = Box(v.track.rectTransform, "Coming", round, Color.white, true);
        v.fill = Box(v.track.rectTransform, "Fill", round, Color.white, true);
        v.shine = Box(v.fill.rectTransform, "Shine", round, Color.white, true);
        var sr = v.shine.rectTransform;
        sr.anchorMin = new Vector2(0, .5f); sr.anchorMax = new Vector2(1, 1);
        sr.offsetMin = new Vector2(2, 0); sr.offsetMax = new Vector2(-2, -2);
        return v;
    }

    Image MakeDivider(RowView v)
    {
        var d = Box(v.track.rectTransform, "Divider", null, new Color(0, 0, 0, .62f));
        return d;
    }

    static void Inset(RectTransform rt, float m)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(m, m); rt.offsetMax = new Vector2(-m, -m);
    }

    Image Box(Transform parent, string name, Sprite sprite, Color c, bool sliced = false)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        img.color = c;
        img.raycastTarget = false;
        return img;
    }

    Text Label(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = font;
        t.raycastTarget = false;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        return t;
    }
}
