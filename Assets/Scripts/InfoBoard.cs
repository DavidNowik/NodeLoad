using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The resource board: one card per item with its price and how to get it (mined on which tiles, or which recipe makes it).
// The frame is in the scene (HUD/InfoBoard); the cards are rebuilt from the game data every time the board opens.
public class InfoBoard : MonoBehaviour
{
    public RectTransform content;   // where the cards go
    public Sprite round;            // rounded 9-slice sprite for cards and pills
    public Sprite hex;              // hexagon sprite for the icons
    public enum Page { Resources, Nodes }
    public Page page;               // Resources: every item; Nodes: every building with its production ratios

    static readonly Color Cream = new Color(1f, .95f, .85f);
    static readonly Color Edge = new Color(.36f, .26f, .22f);
    static readonly Color Dim = new Color(.62f, .68f, .76f);
    static readonly Color CardColor = new Color(.17f, .21f, .28f);
    static readonly Color Gold = new Color(1f, .85f, .3f);
    static readonly Color Pill = new Color(.27f, .33f, .45f);

    Font font;
    bool wrapLabels;   // the Nodes page has long sentences: they wrap instead of running under the price

    void OnEnable()
    {
        if (World.Instance == null || content == null) return;
        Rebuild();
    }

    void Rebuild()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
        wrapLabels = false;
        if (page == Page.Nodes) { BuildNodeCards(); return; }

        var items = new List<ItemDef>(Resources.LoadAll<ItemDef>("Data/Items"));
        items.Sort((a, b) => a.tier != b.tier ? a.tier.CompareTo(b.tier) : a.price.CompareTo(b.price));
        int tier = -1;
        foreach (var item in items)
        {
            if (item.tier != tier) { tier = item.tier; TierHeader(tier); }   // the items are sorted into tiers: edges can let only up to a tier through
            BuildCard(item);
        }
    }

    void TierHeader(int tier)
    {
        var t = Label(content, "TIER " + tier + (tier == 0 ? "  ·  raw" : "  ·  refined"), 30, Gold, FontStyle.Bold);
        var le = t.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = 56;
        le.flexibleWidth = 1;
        t.alignment = TextAnchor.LowerLeft;
    }

    void BuildCard(ItemDef item)
    {
        var data = World.Instance.data;

        var card = Box(content, "Card " + item.name, round, CardColor);
        var le = card.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = 136;
        le.flexibleWidth = 1;
        var h = card.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(20, 20, 14, 14);
        h.spacing = 22;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = false;

        HexIcon(card, 96, item.color, item);

        // name, kind and how to get it
        var info = new GameObject("Info", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        info.transform.SetParent(card, false);
        info.GetComponent<LayoutElement>().flexibleWidth = 1;
        var v = info.GetComponent<VerticalLayoutGroup>();
        v.spacing = 10;
        v.childAlignment = TextAnchor.MiddleLeft;
        v.childControlWidth = v.childControlHeight = true;
        v.childForceExpandWidth = v.childForceExpandHeight = false;

        var tiles = data.tiles.FindAll(t => t.yields == item);
        bool raw = tiles.Count > 0;

        var title = Row(info.transform, 14);
        Label(title, Cap(item.name), 36, Cream, FontStyle.Bold);
        Tag(title, raw ? "RAW" : "REFINED");
        if (item.burnWeight > 0) Tag(title, "BURNS " + R(item.burnWeight));   // an Oven fills its heat meter by this much

        if (raw)
        {
            var row = Row(info.transform, 10);
            Label(row, "Mined by a Miner on", 24, Dim, FontStyle.Normal);
            foreach (var t in tiles)
            {
                Chip(row, t.fill, t.name + " tiles");
            }
            foreach (var n in data.nodes)
                if (n.behaviour == NodeBehaviour.Extractor) Step(row, n.cycleTime.ToString("0.#") + " s each");
        }

        bool anyRecipe = false;
        foreach (var n in data.nodes)
        {
            if (n.recipes == null) continue;
            foreach (var r in n.recipes)
            {
                if (r.output != item) continue;
                anyRecipe = true;
                var row = Row(info.transform, 12);
                for (int i = 0; i < r.inputs.Length; i++)
                {
                    if (i > 0) Label(row, "+", 28, Dim, FontStyle.Bold);
                    var inp = r.inputs[i];
                    Chip(row, inp.item.color, (inp.count > 1 ? inp.count + " × " : "") + inp.item.name, inp.item);
                }
                Label(row, "→", 30, Dim, FontStyle.Bold);
                if (r.outputCount > 1) Label(row, r.outputCount + " ×", 24, Dim, FontStyle.Bold);
                Step(row, n.displayName + "  ·  " + r.verb + "  ·  " + r.time.ToString("0.#") + " s");
            }
        }
        if (!raw && !anyRecipe) Label(Row(info.transform, 8), "No source yet", 24, Dim, FontStyle.Italic);

        // price
        var price = Box(card, "Price", round, Gold);
        var pl = price.gameObject.AddComponent<LayoutElement>();
        pl.preferredWidth = 150;
        pl.preferredHeight = 72;
        var pt = Label(price, "€" + NumFmt.Short(Meta.SellPrice(item)), 38, Edge, FontStyle.Bold);
        pt.alignment = TextAnchor.MiddleCenter;
        Fill(pt.rectTransform);
    }

    // ---- the Nodes page: what every building does, and the production ratios ----
    static string R(float v) { return v.ToString("0.##"); }

    // the smallest whole-number ratio for `m` workers per machine, e.g. 1.5 -> 3 workers for 2 machines
    static void Ratio(float m, out int workers, out int machines, out bool exact)
    {
        for (int r = 1; r <= 12; r++)
        {
            float w = m * r;
            if (Mathf.Abs(w - Mathf.Round(w)) < .02f)
            {
                workers = Mathf.Max(1, Mathf.RoundToInt(w));
                machines = r;
                exact = true;
                return;
            }
        }
        workers = Mathf.Max(1, Mathf.RoundToInt(m));
        machines = 1;
        exact = false;
    }

    static Color NodeColor(NodeDef n, GameData data)
    {
        switch (n.behaviour)
        {
            case NodeBehaviour.Extractor:
                foreach (var t in data.tiles) if (t.yields != null) return t.yields.color;
                return Cream;
            case NodeBehaviour.Processor: return n.recipes != null && n.recipes.Length > 0 ? n.recipes[0].output.color : Cream;
            case NodeBehaviour.Seller: return new Color(1f, .85f, .3f);
            case NodeBehaviour.Quest: return new Color(.82f, .72f, 1f);
            case NodeBehaviour.Oven: return new Color(1f, .6f, .25f);
            case NodeBehaviour.Turbine: return new Color(.6f, .8f, 1f);
            default: return new Color(.74f, .85f, 1f);
        }
    }

    void BuildNodeCards()
    {
        wrapLabels = true;
        var data = World.Instance.data;
        NodeDef miner = data.nodes.Find(n => n.behaviour == NodeBehaviour.Extractor);
        NodeDef market = data.nodes.Find(n => n.behaviour == NodeBehaviour.Seller);
        foreach (var n in data.nodes)
        {
            var card = Box(content, "Card " + n.displayName, round, CardColor);
            var le = card.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = 1;
            var h = card.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(20, 20, 14, 14);
            h.spacing = 22;
            h.childAlignment = TextAnchor.UpperLeft;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = false;

            HexIcon(card, 96, NodeColor(n, data));

            var info = new GameObject("Info", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            info.transform.SetParent(card, false);
            var infoLayout = info.GetComponent<LayoutElement>();
            infoLayout.flexibleWidth = 1;
            infoLayout.preferredWidth = 0;   // the column takes only the space left beside the icon and the price, and its text wraps
            infoLayout.minWidth = 0;
            var v = info.GetComponent<VerticalLayoutGroup>();
            v.spacing = 10;
            v.childAlignment = TextAnchor.UpperLeft;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = v.childForceExpandHeight = false;

            var title = Row(info.transform, 14);
            Label(title, n.displayName, 36, Cream, FontStyle.Bold);
            Tag(title, n.behaviour.ToString().ToUpper());

            switch (n.behaviour)
            {
                case NodeBehaviour.Extractor:
                    Line(info.transform, "Mines what the forest or stone tile under it yields.");
                    Line(info.transform, "1 item every " + R(n.cycleTime) + " s  =  " + R(1f / n.cycleTime) + " items per second");
                    break;
                case NodeBehaviour.Processor:
                    ProcessorLines(info.transform, n, miner, market);
                    break;
                case NodeBehaviour.Seller:
                    Line(info.transform, "Sells everything that arrives, at once, up to €" + R(Meta.MarketCap(n.incomeCap)) + " per second on average (checked over " + R(n.capWindow) + " s, so a short burst above it is fine).");
                    Line(info.transform, "What is over goes to another connected Market, or is lost.");
                    break;
                case NodeBehaviour.Quest:
                    Line(info.transform, "Takes what the level asks for. Anything else (or too much) passes on.");
                    break;
                case NodeBehaviour.Oven:
                    Line(info.transform, "Burns items into a heat meter of " + R(n.maxHeat) + ". The meter loses " + R(n.burnRate) + " per second.");
                    Line(info.transform, "Items burn at once as long as they fit into the meter; the rest waits (up to " + n.itemSlots + "), so it stays full without waste.");
                    var burnRow = Row(info.transform, 12);
                    Label(burnRow, "Burns:", 24, Dim, FontStyle.Normal);
                    foreach (var it in Resources.LoadAll<ItemDef>("Data/Items"))
                        if (it.burnWeight > 0) Chip(burnRow, it.color, R(it.burnWeight) + " × " + it.name, it);
                    Line(info.transform, "Drag an edge out of it to a Steam Turbine: that makes a heat edge. Any other edge carries what it makes.");
                    ProcessorLines(info.transform, n, miner, market);
                    BuildCostRow(info.transform, n);
                    break;
                case NodeBehaviour.Turbine:
                    Line(info.transform, "Takes heat from the Ovens linked to it, and " + R(n.feedRate) + " " + (n.feedItem != null ? n.feedItem.name : "water") + " per second.");
                    Line(info.transform, "While it has both, everything within " + R(n.effectRadius) + " units that mines, refines or stores works faster: x1 with no heat, x" + R(n.maxBoost) + " at full heat.");
                    Line(info.transform, "It draws " + R(n.heatDraw) + " heat per second from its Ovens while it works. Turbines stack by multiplying: x2 and x2 is x4.");
                    BuildCostRow(info.transform, n);
                    break;
                case NodeBehaviour.Storage:
                    Line(info.transform, "Keeps up to " + n.capacity + " of one item: the first item that arrives is asked about.");
                    Line(info.transform, "Drag an edge out of it: a chest edge hands the item to one building, " + R(n.outputRate) + " per second.");
                    if (n.buildCost != null && n.buildCost.Length > 0)
                    {
                        var row = Row(info.transform, 12);
                        Label(row, "Must be built first:", 24, Dim, FontStyle.Normal);
                        foreach (var c in n.buildCost) Chip(row, c.item.color, c.count + " × " + c.item.name, c.item);
                    }
                    break;
            }

            var price = Box(card, "Price", round, Gold);
            var pl = price.gameObject.AddComponent<LayoutElement>();
            pl.preferredWidth = 150;
            pl.preferredHeight = 72;
            var pt = Label(price, n.baseCost == 0 || n.firstFree ? "free" : "€" + NumFmt.Short(n.baseCost), 38, Edge, FontStyle.Bold);
            pt.alignment = TextAnchor.MiddleCenter;
            Fill(pt.rectTransform);
        }
    }

    void Line(Transform parent, string text) { Label(Row(parent, 8), text, 24, Dim, FontStyle.Normal); }

    void BuildCostRow(Transform info, NodeDef n)
    {
        if (n.buildCost == null || n.buildCost.Length == 0) return;
        var row = Row(info, 12);
        Label(row, "Must be built first:", 24, Dim, FontStyle.Normal);
        foreach (var c in n.buildCost) Chip(row, c.item.color, c.count + " × " + c.item.name, c.item);
    }

    // one recipe: what it takes and makes per second, how many Miners keep it busy, how many fit one Market
    void ProcessorLines(Transform info, NodeDef n, NodeDef miner, NodeDef market)
    {
        string who = n.displayName, whoPlural = who.EndsWith("y") ? who.Substring(0, who.Length - 1) + "ies" : who + "s";
        if (n.behaviour == NodeBehaviour.Oven) Line(info, "While it has heat it also makes (it stops when the meter is empty):");
        else if (n.anyRecipe) Line(info, "Takes any of these inputs, whichever arrives. Everything else passes through.");
        else Line(info, "One recipe, decided by the first item that arrives. Everything else passes through.");
        foreach (var r in n.recipes)
        {
            var row = Row(info, 12);
            for (int i = 0; i < r.inputs.Length; i++)
            {
                if (i > 0) Label(row, "+", 28, Dim, FontStyle.Bold);
                Chip(row, r.inputs[i].item.color, (r.inputs[i].count > 1 ? r.inputs[i].count + " × " : "") + r.inputs[i].item.name, r.inputs[i].item);
            }
            Label(row, "→", 30, Dim, FontStyle.Bold);
            Chip(row, r.output.color, (r.outputCount > 1 ? r.outputCount + " × " : "") + r.output.name, r.output);
            Step(row, r.verb + "  ·  " + R(r.time) + " s");

            float outPerSec = r.outputCount / r.time;
            string takes = "";
            foreach (var inp in r.inputs) takes += (takes.Length > 0 ? ", " : "") + R(inp.count / r.time) + " " + inp.item.name + "/s";
            Label(Row(info, 8), "per " + who + ":  takes " + takes + "  ·  makes " + R(outPerSec) + " " + r.output.name + "/s", 24, Cream, FontStyle.Normal);

            // how many Miners keep one Refinery exactly busy (only for inputs a Miner mines)
            if (miner != null && r.inputs.Length == 1 && IsRaw(r.inputs[0].item))
            {
                float minersPer = (r.inputs[0].count / r.time) * miner.cycleTime;
                Ratio(minersPer, out int w, out int m, out bool exact);
                Step(Row(info, 8), (exact ? "" : "about ") + w + (w == 1 ? " Miner keeps " : " Miners keep ") + m + " " + (m == 1 ? who : whoPlural) + " exactly busy");
            }
            // how many Refineries one Market can sell the output of
            if (market != null)
            {
                float money = outPerSec * Meta.SellPrice(r.output);
                Label(Row(info, 8), "Selling it:  €" + R(money) + "/s per " + who + "  ·  one Market takes " + R(Meta.MarketCap(market.incomeCap) / money) + " " + whoPlural, 22, Dim, FontStyle.Normal);
            }
        }
    }

    // an item a Miner can mine (it is the yield of some tile)
    static bool IsRaw(ItemDef item)
    {
        foreach (var t in World.Instance.data.tiles) if (t.yields == item) return true;
        return false;
    }

    // ---- small UI builders ----
    static string Cap(string s) { return s.Length == 0 ? s : char.ToUpper(s[0]) + s.Substring(1); }

    RectTransform Box(Transform parent, string name, Sprite sprite, Color c)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.type = sprite == round ? Image.Type.Sliced : Image.Type.Simple;
        img.color = c;
        img.raycastTarget = false;
        return (RectTransform)go.transform;
    }

    Text Label(Transform parent, string text, int size, Color c, FontStyle style)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = c;
        t.alignment = TextAnchor.MiddleLeft;
        t.horizontalOverflow = wrapLabels ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        t.text = text;
        return t;
    }

    static void Fill(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    Transform Row(Transform parent, float spacing)
    {
        var go = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        go.transform.SetParent(parent, false);
        var h = go.GetComponent<HorizontalLayoutGroup>();
        h.spacing = spacing;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = false;
        return go.transform;
    }

    // the node look: dark outline, coloured band, dark inner outline, pale body
    RectTransform HexIcon(Transform parent, float size, Color color, ItemDef item = null)
    {
        var root = new GameObject("Icon", typeof(RectTransform), typeof(LayoutElement));
        root.transform.SetParent(parent, false);
        var le = root.GetComponent<LayoutElement>();
        le.preferredWidth = le.minWidth = size;
        le.preferredHeight = le.minHeight = size;
        Layer(root.transform, size, Edge);
        Layer(root.transform, size * .9f, color);
        Layer(root.transform, size * .64f, Edge);
        Layer(root.transform, size * .58f, Color.Lerp(color, Cream, .7f));
        PixelIcon(root.transform, item, size * .5f);
        return (RectTransform)root.transform;
    }

    // the item's pixel icon in the middle of a hexagon icon, when it has one
    void PixelIcon(Transform parent, ItemDef item, float size)
    {
        var sprite = PixelArt.ForItem(item, 1f);
        if (sprite == null) return;
        var rt = Box(parent, "Pixels", sprite, Color.white);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = Vector2.zero;
    }

    void Layer(Transform parent, float size, Color c)
    {
        var rt = Box(parent, "Layer", hex, c);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = Vector2.zero;
    }

    // small icon with a label, e.g. "2 x wood"
    void Chip(Transform parent, Color color, string label, ItemDef item = null)
    {
        var row = Row(parent, 8);
        HexIcon(row, 46, color, item);
        Label(row, label, 26, Cream, FontStyle.Normal);
    }

    // rounded capsule with a step description, e.g. "Refinery - Sawing - 2 s"
    void Step(Transform parent, string text)
    {
        var pill = Box(parent, "Step", round, Pill);
        var h = pill.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(16, 16, 8, 8);
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = false;
        Label(pill, text, 22, Cream, FontStyle.Normal);
    }

    void Tag(Transform parent, string text)
    {
        var pill = Box(parent, "Tag", round, Pill);
        var h = pill.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(12, 12, 4, 4);
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = false;
        Label(pill, text, 16, Dim, FontStyle.Bold);
    }
}
