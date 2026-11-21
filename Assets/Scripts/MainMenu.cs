using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The main menu scene: Play, a list of levels, a list of resources and a list of upgrades. The frame (buttons, panels, scroll areas) is in the scene;
// the cards are built here from the game data. Because it is a proof of concept, every list is padded with placeholder cards so you can see
// how it will look with many levels, resources and upgrades.
public class MainMenu : MonoBehaviour
{
    public Sprite round, hex;
    public RectTransform levelsContent, resourcesContent, upgradesContent;
    public GameObject levelsPanel, resourcesPanel, upgradesPanel;
    public Text starsText;
    public int placeholderLevels = 28, placeholderResources = 20, placeholderBuffs = 26;

    static readonly Color Cream = new Color(1f, .95f, .85f);
    static readonly Color Edge = new Color(.36f, .26f, .22f);
    static readonly Color Dim = new Color(.55f, .6f, .68f);
    static readonly Color CardColor = new Color(.17f, .21f, .28f);
    static readonly Color CardDim = new Color(.12f, .15f, .2f);
    static readonly Color Gold = new Color(1f, .84f, .3f);
    static readonly Color Green = new Color(.45f, .88f, .55f);

    // ideas for upgrades that could exist later; only the first real one (Head Start) does anything now
    static readonly string[] BuffNames =
    {
        "Cheaper Miners", "Swift Sawmills", "Deep Storage", "Long Edges", "Quick Hands", "Market Boost", "Lucky Trees", "Rich Stone",
        "Wide Start", "Cheap Tiles", "Free Market", "Double Reward", "Faster Items", "Sharp Saws", "Hardy Roots", "Bargain Ring",
        "Second Wind", "Gold Rush", "Tidy Edges", "Open Roads", "Patient Miners", "Heavy Loads", "Fresh Maps", "Master Builder",
        "Tool Belt", "Last Resort", "Early Bird", "Big Pockets"
    };

    Font font;

    void Start()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Wire("Menu/Play", () => Open(levelsPanel));   // Play opens the level select
        Wire("Menu/Resources", () => Open(resourcesPanel));
        Wire("Menu/Upgrades", () => Open(upgradesPanel));
        Wire("Menu/Quit", Application.Quit);
        Wire("Debug/AddStar", () => { SaveSystem.Data.stars++; SaveSystem.Save(); RefreshStars(); if (upgradesPanel.activeSelf) BuildUpgrades(); });
        Wire("Debug/ResetSave", () => { SaveSystem.Reset(); RefreshStars(); CloseAll(); });
        foreach (var p in new[] { levelsPanel, resourcesPanel, upgradesPanel })
        {
            var close = p.transform.Find("Window/Close");
            var panel = p;
            if (close != null) close.GetComponent<Button>().onClick.AddListener(() => panel.SetActive(false));
            p.GetComponent<Button>().onClick.AddListener(() => panel.SetActive(false));   // the dimmed background closes it too
        }
        CloseAll();
        if (SaveSystem.OpenLevelSelect) { SaveSystem.OpenLevelSelect = false; Open(levelsPanel); }   // coming from a finished level
        RefreshStars();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) CloseAll();
    }

    void Wire(string path, UnityEngine.Events.UnityAction action)
    {
        var t = transform.Find(path);
        if (t != null) t.GetComponent<Button>().onClick.AddListener(action);
    }

    void CloseAll()
    {
        levelsPanel.SetActive(false);
        resourcesPanel.SetActive(false);
        upgradesPanel.SetActive(false);
    }

    void Open(GameObject panel)
    {
        CloseAll();
        panel.SetActive(true);
        if (panel == levelsPanel) BuildLevels();
        else if (panel == resourcesPanel) BuildResources();
        else BuildUpgrades();
    }

    void RefreshStars() { if (starsText != null) starsText.text = "Stars  " + Meta.Stars; }

    // ---- the lists ----
    static void Clear(RectTransform content)
    {
        for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
    }

    // the level select: every level is a hexagon like the map tiles, in rows. Done = green, next = gold (playable), later = dark.
    void BuildLevels()
    {
        Clear(levelsContent);
        var levels = new List<LevelDef>(Resources.LoadAll<LevelDef>("Data/Levels"));
        levels.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        var save = SaveSystem.Data;
        int next = save.NextLevel;
        const float size = 170f;
        for (int i = 0; i < levels.Count + placeholderLevels; i++)
        {
            bool real = i < levels.Count;
            bool done = real && save.completed.Contains(i);
            bool playable = real && i <= next;
            var fill = done ? new Color(.25f, .5f, .36f) : playable ? new Color(.85f, .62f, .16f) : new Color(.15f, .18f, .24f);
            var rim = done ? new Color(.45f, .88f, .55f) : playable ? Gold : new Color(.24f, .28f, .36f);

            // the grid cell is only a holder; the hexagon inside keeps its own size, so it never stretches whatever the cell size is
            var go = new GameObject("Level " + (i + 1), typeof(RectTransform));
            go.transform.SetParent(levelsContent, false);
            var card = new GameObject("Hex", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            card.SetParent(go.transform, false);
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(.5f, .5f);
            card.sizeDelta = new Vector2(size, size);
            card.anchoredPosition = Vector2.zero;
            var img = card.GetComponent<Image>();
            img.sprite = hex;
            img.color = rim;
            Layer(card, size * .9f, fill);

            var ink = playable ? new Color(.2f, .12f, .02f) : done ? Cream : Dim;
            Text(card, (i + 1).ToString(), 56, ink, FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(0, -30), new Vector2(0, -100));
            if (real) Text(card, levels[i].goals.Length > 1 ? levels[i].goals[0].amount + " " + levels[i].goals[0].item.name + " +" + (levels[i].goals.Length - 1) : levels[i].GoalText, 20, ink, FontStyle.Normal, TextAnchor.MiddleCenter, new Vector2(0, -98), new Vector2(0, -124));
            string state = !real ? "soon" : done ? "DONE" : playable ? "PLAY" : "LOCKED";
            Text(card, state, 20, ink, FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(0, real ? -122 : -104), new Vector2(0, real ? -146 : -130));

            if (playable)
            {
                int index = i;
                var b = go.AddComponent<Button>();
                b.targetGraphic = img;
                b.onClick.AddListener(() => { SaveSystem.PendingLevel = index; SceneManager.LoadScene(Hud.GameScene); });
            }
        }
    }

    void BuildResources()
    {
        Clear(resourcesContent);
        var items = new List<ItemDef>(Resources.LoadAll<ItemDef>("Data/Items"));
        items.Sort((a, b) => a.price.CompareTo(b.price));
        for (int i = 0; i < items.Count + placeholderResources; i++)
        {
            bool real = i < items.Count;
            var card = Card(resourcesContent, real ? items[i].name : "???", real ? CardColor : CardDim);
            HexIcon(card, real ? items[i].color : Dim, real ? items[i] : null, 84, new Vector2(18, -18));
            Text(card, real ? Cap(items[i].name) : "???", 30, real ? Cream : Dim, FontStyle.Bold, TextAnchor.UpperLeft, new Vector2(116, -20), new Vector2(-14, -62));
            Text(card, real ? "sells for €" + NumFmt.Short(items[i].price) : "not discovered", 22, real ? new Color(.85f, .88f, .92f) : Dim, FontStyle.Normal, TextAnchor.UpperLeft, new Vector2(116, -64), new Vector2(-14, -100));
        }
    }

    void BuildUpgrades()
    {
        Clear(upgradesContent);
        // the cards are tall enough for a description of three lines above the price
        var grid = upgradesContent.GetComponent<GridLayoutGroup>();
        if (grid != null) grid.cellSize = new Vector2(grid.cellSize.x, 190);
        RefreshStars();
        var buffs = new List<BuffDef>(Meta.AllBuffs());
        buffs.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        // the "ideas" never repeat the name of an upgrade that really exists
        var ideas = new List<string>();
        foreach (var idea in BuffNames) if (!buffs.Exists(x => x.name == idea)) ideas.Add(idea);
        for (int i = 0; i < buffs.Count + placeholderBuffs; i++)
        {
            bool real = i < buffs.Count;
            var b = real ? buffs[i] : null;
            bool owned = real && Meta.Owns(b);
            var card = Card(upgradesContent, real ? b.name : "placeholder", owned ? new Color(.2f, .3f, .27f) : real ? CardColor : CardDim);
            string title = real ? b.name : ideas[(i - buffs.Count) % ideas.Count];
            Text(card, title, 30, real ? Cream : Dim, FontStyle.Bold, TextAnchor.UpperLeft, new Vector2(18, -14), new Vector2(-18, -56));
            Text(card, real ? b.description : "An idea for later.", 21, real ? new Color(.85f, .88f, .92f) : Dim, FontStyle.Normal, TextAnchor.UpperLeft, new Vector2(18, -58), new Vector2(-18, -128));

            string label = !real ? "coming soon" : owned ? "OWNED" : b.effect == BuffEffect.None ? "coming soon" : Meta.CanBuy(b) ? "BUY  " + b.cost + " star" + (b.cost == 1 ? "" : "s") : "NEED " + b.cost + " star" + (b.cost == 1 ? "" : "s");
            var color = owned ? Green : real && Meta.CanBuy(b) ? Gold : Dim;
            var pill = Pill(card, label, color);
            if (real && Meta.CanBuy(b))
            {
                var buff = b;
                var btn = pill.gameObject.AddComponent<Button>();
                btn.targetGraphic = pill.GetComponent<Image>();
                btn.onClick.AddListener(() => { if (Meta.TryBuy(buff)) BuildUpgrades(); });
                pill.GetComponent<Image>().raycastTarget = true;
            }
        }
    }

    // ---- small UI builders ----
    static string Cap(string s) { return s.Length == 0 ? s : char.ToUpper(s[0]) + s.Substring(1); }

    RectTransform Card(RectTransform parent, string name, Color color)
    {
        var go = new GameObject("Card " + name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = round;
        img.type = Image.Type.Sliced;
        img.color = color;
        return (RectTransform)go.transform;
    }

    Text Text(RectTransform parent, string text, int size, Color c, FontStyle style, TextAnchor align, Vector2 offsetMin, Vector2 offsetMax)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        // the two corners are given from the top left: y values are negative distances from the top edge
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(.5f, 1);
        rt.offsetMin = new Vector2(offsetMin.x, offsetMax.y);
        rt.offsetMax = new Vector2(offsetMax.x, offsetMin.y);
        var t = go.GetComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = c;
        t.alignment = align;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        t.text = text;
        return t;
    }

    // a rounded label at the bottom right of a card
    RectTransform Pill(RectTransform card, string text, Color color)
    {
        var go = new GameObject("Pill", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(card, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1, 0);
        rt.sizeDelta = new Vector2(190, 44);
        rt.anchoredPosition = new Vector2(-14, 14);
        var img = go.GetComponent<Image>();
        img.sprite = round;
        img.type = Image.Type.Sliced;
        img.color = new Color(color.r * .35f, color.g * .35f, color.b * .35f, 1f);
        img.raycastTarget = false;
        var t = Text(rt, text, 22, color, FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(0, 0), new Vector2(0, 0));
        var trt = (RectTransform)t.transform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        return rt;
    }

    // the node look: dark outline, coloured band, dark inner outline, pale body, with the item's pixel icon on it
    void HexIcon(RectTransform parent, Color color, ItemDef item, float size, Vector2 topLeft)
    {
        var root = new GameObject("Icon", typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(parent, false);
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(0, 1);
        root.sizeDelta = new Vector2(size, size);
        root.anchoredPosition = topLeft;
        Layer(root, size, item != null ? Edge : new Color(.2f, .24f, .3f));
        Layer(root, size * .9f, color);
        Layer(root, size * .64f, item != null ? Edge : new Color(.2f, .24f, .3f));
        Layer(root, size * .58f, item != null ? Color.Lerp(color, Cream, .7f) : new Color(.14f, .17f, .22f));
        var sprite = PixelArt.ForItem(item, 1f);
        if (sprite != null)
        {
            var px = new GameObject("Pixels", typeof(RectTransform), typeof(Image));
            px.transform.SetParent(root, false);
            var rt = (RectTransform)px.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
            rt.sizeDelta = new Vector2(size * .5f, size * .5f);
            var img = px.GetComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
        }
    }

    void Layer(RectTransform parent, float size, Color c)
    {
        var go = new GameObject("Layer", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
        rt.sizeDelta = new Vector2(size, size);
        var img = go.GetComponent<Image>();
        img.sprite = hex;
        img.color = c;
        img.raycastTarget = false;
    }
}
