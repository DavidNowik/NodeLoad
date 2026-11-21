using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The settings of one special edge: a Tier edge picks a tier and "up to" or "exactly", a Filter edge picks its items and allow or deny,
// every special edge can be removed here. The frame (HUD/EdgePanel) is in the scene; the tier and item buttons are made from the game data.
public class EdgePanel : MonoBehaviour
{
    static readonly Color Normal = new Color(.2f, .26f, .36f);
    static readonly Color On = new Color(.24f, .6f, .45f);
    static readonly Color Cream = new Color(1f, .95f, .85f);

    Link link;
    World world;
    Font font;
    Sprite round;
    Text title, hint;
    GameObject tierGroup, filterGroup;
    Button upTo, exactly, deny, allow;
    RectTransform tiers, itemsBox;

    public bool IsOpen { get { return gameObject.activeSelf; } }

    // called once by the HUD; finds its parts by name
    public void Setup(World w)
    {
        world = w;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        title = transform.Find("Title").GetComponent<Text>();
        hint = transform.Find("Hint").GetComponent<Text>();
        var close = transform.Find("Close").GetComponent<Button>();
        round = close.GetComponent<Image>().sprite;
        close.onClick.AddListener(Close);
        transform.Find("Remove").GetComponent<Button>().onClick.AddListener(() =>
        {
            if (link != null) world.Remove(link);
            Close();
        });

        tierGroup = transform.Find("Tier").gameObject;
        upTo = tierGroup.transform.Find("UpTo").GetComponent<Button>();
        exactly = tierGroup.transform.Find("Exactly").GetComponent<Button>();
        tiers = (RectTransform)tierGroup.transform.Find("Tiers");
        upTo.onClick.AddListener(() => { link.exact = false; Apply(); });
        exactly.onClick.AddListener(() => { link.exact = true; Apply(); });

        filterGroup = transform.Find("Filter").gameObject;
        deny = filterGroup.transform.Find("Deny").GetComponent<Button>();
        allow = filterGroup.transform.Find("Allow").GetComponent<Button>();
        itemsBox = (RectTransform)filterGroup.transform.Find("Items");
        deny.onClick.AddListener(() => { link.deny = true; Apply(); });
        allow.onClick.AddListener(() => { link.deny = false; Apply(); });
        gameObject.SetActive(false);
    }

    public void Open(Link l)
    {
        link = l;
        gameObject.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        link = null;
        gameObject.SetActive(false);
    }

    void Update()
    {
        // the edge may have been removed or the level reset while the panel is open
        if (link == null) { Close(); return; }
    }

    void Apply()
    {
        link.Changed();
        Refresh();
    }

    void Refresh()
    {
        if (link == null) return;
        var kind = link.def.kind;
        title.text = link.def.displayName;
        tierGroup.SetActive(kind == EdgeKind.Tier);
        filterGroup.SetActive(kind == EdgeKind.Filter);

        if (kind == EdgeKind.Tier)
        {
            hint.text = link.exact ? "Only tier " + link.tier + " items travel here." : "Tiers 0 to " + link.tier + " travel here.";
            Mark(upTo, !link.exact);
            Mark(exactly, link.exact);
            Fill(tiers, MaxTier() + 1, i => "Tier " + i, i => link.tier == i, i => { link.tier = i; Apply(); }, null);
        }
        else if (kind == EdgeKind.Filter)
        {
            hint.text = link.deny ? "Everything travels here except the marked items." : "Only the marked items travel here.";
            Mark(deny, link.deny);
            Mark(allow, !link.deny);
            var items = new List<ItemDef>(Resources.LoadAll<ItemDef>("Data/Items"));
            items.Sort((a, b) => a.tier != b.tier ? a.tier.CompareTo(b.tier) : a.price.CompareTo(b.price));
            Fill(itemsBox, items.Count, i => items[i].name, i => link.items.Contains(items[i]), i =>
            {
                if (!link.items.Remove(items[i])) link.items.Add(items[i]);
                Apply();
            }, i => items[i].color);
        }
        else hint.text = "Hands its item to exactly this building, and nowhere else.\nDraw a new one to replace it.";
    }

    static int MaxTier()
    {
        int m = 0;
        foreach (var i in Resources.LoadAll<ItemDef>("Data/Items")) m = Mathf.Max(m, i.tier);
        return m;
    }

    static void Mark(Button b, bool on) { b.GetComponent<Image>().color = on ? On : Normal; }

    // one button per entry, rebuilt every time; marked ones are green
    void Fill(RectTransform box, int count, System.Func<int, string> label, System.Func<int, bool> marked, System.Action<int> click, System.Func<int, Color> tint)
    {
        for (int i = box.childCount - 1; i >= 0; i--) Destroy(box.GetChild(i).gameObject);
        for (int i = 0; i < count; i++)
        {
            int index = i;
            var go = new GameObject(label(i), typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(box, false);
            var img = go.GetComponent<Image>();
            img.sprite = round;
            img.type = Image.Type.Sliced;
            img.color = marked(i) ? On : Normal;
            go.GetComponent<Button>().targetGraphic = img;
            go.GetComponent<Button>().onClick.AddListener(() => click(index));

            var t = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            t.transform.SetParent(go.transform, false);
            var rt = (RectTransform)t.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            t.font = font;
            t.fontSize = 22;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = tint != null ? Color.Lerp(Cream, tint(i), .6f) : Cream;
            t.raycastTarget = false;
            t.text = (marked(i) && tint != null ? (link.deny ? "x " : "+ ") : "") + label(i);
        }
    }
}
