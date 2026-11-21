using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class CreateHudMenu
{
    const int ButtonCount = 8;
    const int RightButtonCount = 6;
    const float MenuEntryW = 190, MenuEntryH = 150;   // the entries of the A5, A7 and A8 menus: big enough for three lines of text

    // a menu built with smaller entries is removed so that it is built again at the current size
    static void DropOutdatedMenu(Transform button)
    {
        var menu = button != null ? button.Find("Menu") : null;
        if (menu == null || menu.childCount == 0) return;
        var first = (RectTransform)menu.GetChild(0);
        if (!Mathf.Approximately(first.sizeDelta.y, MenuEntryH)) Undo.DestroyObjectImmediate(menu.gameObject);
    }
    const string HexPath = "Assets/Textures/HexIcon.png";

    // builds the HUD, or completes the existing one: every part is only added when it is missing
    public static void Ensure()
    {
        var hud = GameObject.Find("HUD");
        if (hud == null) { CreateHud(); return; }
        var bar = hud.transform.Find("BottomBar");
        AddMenus(bar);
        StyleButtons(bar);
        AddTopBar(hud.transform);
        AddTimer(hud.transform);
        AddDebugCash(hud.transform);
        AddConfirmDialog(hud.transform);
        AddPauseMenu(hud.transform);
        AddEdgePanel(hud.transform);
        AddInspector(hud.transform);
        AddLevelComplete(hud.transform);
        AddAutoEdge(bar);
        AddRightBar(hud.transform);
        LabelResetButton(hud.transform);
        AddInfoBoard(hud.transform);
        EditorSceneManager.MarkSceneDirty(hud.scene);
    }


    // the Esc menu: a dimmed screen with a panel of buttons. Resume, Main Menu and Quit work; Settings and Controls are placeholders.
    // Hud.cs binds the buttons by name and opens it with Esc.
    static void AddPauseMenu(Transform hud)
    {
        if (hud == null || hud.Find("PauseMenu") != null) return;
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var round = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        var root = new GameObject("PauseMenu", typeof(RectTransform), typeof(Image));
        Undo.RegisterCreatedObjectUndo(root, "Add pause menu");
        root.transform.SetParent(hud, false);
        var confirm = hud.Find("ConfirmDialog");
        if (confirm != null) root.transform.SetSiblingIndex(confirm.GetSiblingIndex());   // the "are you sure?" question must stay on top
        var rrt = (RectTransform)root.transform;
        rrt.anchorMin = Vector2.zero;
        rrt.anchorMax = Vector2.one;
        rrt.offsetMin = rrt.offsetMax = Vector2.zero;
        root.GetComponent<Image>().color = new Color(0, 0, 0, .62f);

        var panel = UiBox(root.transform, "Panel", round, new Color(.12f, .15f, .21f), true);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f);
        panel.sizeDelta = new Vector2(560, 720);

        var title = UiText(panel, font, "Paused", 64, new Color(1f, .95f, .85f), FontStyle.Bold);
        title.gameObject.name = "Title";
        title.alignment = TextAnchor.MiddleCenter;
        var trt = (RectTransform)title.transform;
        trt.anchorMin = new Vector2(0, 1);
        trt.anchorMax = new Vector2(1, 1);
        trt.pivot = new Vector2(.5f, 1);
        trt.sizeDelta = new Vector2(0, 110);
        trt.anchoredPosition = new Vector2(0, -20);

        string[] names = { "Resume", "Settings", "Controls", "MainMenu", "Quit" };
        string[] labels = { "Resume", "Settings (soon)", "Controls (soon)", "Main Menu", "Quit" };
        for (int i = 0; i < names.Length; i++)
        {
            bool live = names[i] != "Settings" && names[i] != "Controls";
            var color = i == 0 ? new Color(.95f, .72f, .2f) : live ? new Color(.2f, .26f, .36f) : new Color(.15f, .18f, .24f);
            var b = UiBox(panel, names[i], round, color, true);
            b.anchorMin = b.anchorMax = b.pivot = new Vector2(.5f, 1);
            b.sizeDelta = new Vector2(440, 96);
            b.anchoredPosition = new Vector2(0, -150 - i * 112);
            var btn = b.gameObject.AddComponent<Button>();
            btn.targetGraphic = b.GetComponent<Image>();
            btn.interactable = live;
            var l = UiText(b, font, labels[i], 38, i == 0 ? new Color(.2f, .12f, .02f) : live ? new Color(1f, .95f, .85f) : new Color(.5f, .55f, .63f), FontStyle.Bold);
            l.gameObject.name = "Label";
            l.alignment = TextAnchor.MiddleCenter;
            var lrt = (RectTransform)l.transform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        }
        root.SetActive(false);
        EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
    }

    // B6, the last right button, resets the level; B2 opens the node board. They get readable names instead of the placeholders
    static void LabelResetButton(Transform hud)
    {
        Relabel(hud, "B1", "Resources", 19, "B1");
        Relabel(hud, "B6", "Reset", 26, "B6");
        Relabel(hud, "B2", "Nodes", 26, "B2", "Reset");   // an earlier version had the reset button here
    }

    static void Relabel(Transform hud, string button, string text, int size, params string[] replaces)
    {
        var label = hud != null ? hud.Find("RightBar/" + button + "/Label") : null;
        if (label == null) return;
        var t = label.GetComponent<Text>();
        if (System.Array.IndexOf(replaces, t.text) < 0) return;
        t.text = text;
        t.fontSize = size;
        EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
    }

    // shown when a level is done: dimmed screen, a panel with the title, what was delivered and the next goal, and a button for the next level
    static void AddLevelComplete(Transform hud)
    {
        if (hud == null || hud.Find("LevelComplete") != null) return;
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var root = new GameObject("LevelComplete", typeof(RectTransform), typeof(Image));
        Undo.RegisterCreatedObjectUndo(root, "Add level complete screen");
        root.transform.SetParent(hud, false);
        var rrt = (RectTransform)root.transform;
        rrt.anchorMin = Vector2.zero;
        rrt.anchorMax = Vector2.one;
        rrt.offsetMin = rrt.offsetMax = Vector2.zero;
        root.GetComponent<Image>().color = new Color(0, 0, 0, .6f);

        var panel = UiBox(root.transform, "Panel", null, new Color(.16f, .2f, .27f), false);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f);
        panel.sizeDelta = new Vector2(760, 420);

        var title = UiText(panel, font, "LEVEL COMPLETE!", 64, new Color(1f, .85f, .3f), FontStyle.Bold);
        title.gameObject.name = "Title";
        title.alignment = TextAnchor.MiddleCenter;
        var trt = (RectTransform)title.transform;
        trt.anchorMin = new Vector2(0, .62f);
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;

        var sub = UiText(panel, font, "", 32, Color.white, FontStyle.Normal);
        sub.gameObject.name = "Sub";
        sub.alignment = TextAnchor.MiddleCenter;
        var srt = (RectTransform)sub.transform;
        srt.anchorMin = new Vector2(0, .3f);
        srt.anchorMax = new Vector2(1, .62f);
        srt.offsetMin = srt.offsetMax = Vector2.zero;

        DialogButton(panel, font, "Next", new Vector2(0, 0), new Color(.25f, .6f, .4f));
        root.SetActive(false);
    }

    static RectTransform UiBox(Transform parent, string name, Sprite sprite, Color c, bool sliced)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        img.color = c;
        return (RectTransform)go.transform;
    }

    static Text UiText(Transform parent, Font font, string text, int size, Color c, FontStyle style)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = c;
        t.alignment = TextAnchor.MiddleLeft;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.raycastTarget = false;
        t.text = text;
        return t;
    }

    // B1: the resource board (every item with its price and recipe). Only the frame is built here; InfoBoard.cs fills in the cards when it opens.
    static void AddInfoBoard(Transform hud)
    {
        AddBoard(hud, "InfoBoard", "Resources", "what everything sells for, and how it is made", InfoBoard.Page.Resources);
        AddBoard(hud, "NodeBoard", "Nodes", "what each building does, and how many workers keep it busy", InfoBoard.Page.Nodes);
    }

    static void AddBoard(Transform hud, string boardName, string titleText, string subText, InfoBoard.Page page)
    {
        if (hud == null || hud.Find(boardName) != null) return;
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var round = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        var hex = HexSprite();
        var cream = new Color(1f, .95f, .85f);
        var edge = new Color(.36f, .26f, .22f);

        // dimmed background: clicking it closes the board
        var root = UiBox(hud, boardName, null, new Color(0, 0, 0, .62f), false);
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Add info board");
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;
        var btn = root.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.targetGraphic = root.GetComponent<Image>();

        var border = UiBox(root, "PanelEdge", round, new Color(1f, .95f, .85f, .3f), true);
        border.anchorMin = border.anchorMax = border.pivot = new Vector2(.5f, .5f);
        border.sizeDelta = new Vector2(1138, 778);
        border.GetComponent<Image>().raycastTarget = false;

        var panel = UiBox(root, "Panel", round, new Color(.1f, .13f, .18f), true);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f);
        panel.sizeDelta = new Vector2(1120, 760);

        // header with the hexagon logo, title and subtitle
        var header = UiBox(panel, "Header", round, new Color(.16f, .2f, .27f), true);
        header.anchorMin = new Vector2(0, 1);
        header.anchorMax = new Vector2(1, 1);
        header.pivot = new Vector2(.5f, 1);
        header.offsetMin = new Vector2(14, -128);
        header.offsetMax = new Vector2(-14, -14);
        header.GetComponent<Image>().raycastTarget = false;

        var logo = new GameObject("Logo", typeof(RectTransform)).GetComponent<RectTransform>();
        logo.SetParent(header, false);
        logo.anchorMin = logo.anchorMax = logo.pivot = new Vector2(0, .5f);
        logo.sizeDelta = new Vector2(76, 76);
        logo.anchoredPosition = new Vector2(34, 0);
        Layer(logo, "Outer", 76, edge, hex);
        Layer(logo, "Band", 68, new Color(.45f, .88f, .55f), hex);
        Layer(logo, "Inner", 50, edge, hex);
        Layer(logo, "Body", 44, cream, hex);

        var title = UiText(header, font, titleText, 54, cream, FontStyle.Bold);
        title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0, .5f);
        title.rectTransform.pivot = new Vector2(0, .5f);
        title.rectTransform.sizeDelta = new Vector2(600, 64);
        title.rectTransform.anchoredPosition = new Vector2(136, 16);
        var sub = UiText(header, font, subText, 24, new Color(.62f, .68f, .76f), FontStyle.Normal);
        sub.rectTransform.anchorMin = sub.rectTransform.anchorMax = new Vector2(0, .5f);
        sub.rectTransform.pivot = new Vector2(0, .5f);
        sub.rectTransform.sizeDelta = new Vector2(700, 34);
        sub.rectTransform.anchoredPosition = new Vector2(138, -26);

        var close = UiBox(panel, "Close", round, new Color(.62f, .28f, .28f), true);
        close.anchorMin = close.anchorMax = close.pivot = new Vector2(1, 1);
        close.sizeDelta = new Vector2(74, 74);
        close.anchoredPosition = new Vector2(-34, -34);
        var cb = close.gameObject.AddComponent<Button>();
        cb.targetGraphic = close.GetComponent<Image>();
        var x = UiText(close, font, "X", 40, Color.white, FontStyle.Bold);
        x.alignment = TextAnchor.MiddleCenter;
        x.rectTransform.anchorMin = Vector2.zero;
        x.rectTransform.anchorMax = Vector2.one;
        x.rectTransform.offsetMin = x.rectTransform.offsetMax = Vector2.zero;

        // scrolling list of cards
        var scroll = UiBox(panel, "Scroll", null, new Color(0, 0, 0, 0), false);
        scroll.anchorMin = Vector2.zero;
        scroll.anchorMax = Vector2.one;
        scroll.offsetMin = new Vector2(26, 26);
        scroll.offsetMax = new Vector2(-26, -144);
        var viewport = UiBox(scroll, "Viewport", null, new Color(0, 0, 0, 0), false);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = viewport.offsetMax = Vector2.zero;
        viewport.gameObject.AddComponent<RectMask2D>();
        viewport.GetComponent<Image>().raycastTarget = false;

        var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
        content.SetParent(viewport, false);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(.5f, 1);
        content.offsetMin = content.offsetMax = Vector2.zero;
        var v = content.GetComponent<VerticalLayoutGroup>();
        v.spacing = 16;
        v.padding = new RectOffset(4, 4, 4, 8);
        v.childControlWidth = v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var sr = scroll.gameObject.AddComponent<ScrollRect>();
        sr.content = content;
        sr.viewport = viewport;
        sr.horizontal = false;
        sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 45;

        var board = root.gameObject.AddComponent<InfoBoard>();
        board.content = content;
        board.round = round;
        board.hex = hex;
        board.page = page;

        root.gameObject.SetActive(false);
        EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
    }

    // proxy bar on the right edge, between the top and the bottom bar: buttons B1..Bn, B1 opens the resource board
    static void AddRightBar(Transform hud)
    {
        if (hud == null || hud.Find("RightBar") != null) return;
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var bar = new GameObject("RightBar", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        Undo.RegisterCreatedObjectUndo(bar, "Add right bar");
        bar.transform.SetParent(hud, false);
        var rt = (RectTransform)bar.transform;
        rt.anchorMin = new Vector2(1, 0);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(1, .5f);
        rt.offsetMin = new Vector2(-132, 120);   // above the bottom bar
        rt.offsetMax = new Vector2(0, -64);      // below the top bar
        bar.GetComponent<Image>().color = new Color(.07f, .09f, .12f, .92f);
        var lay = bar.GetComponent<VerticalLayoutGroup>();
        lay.childAlignment = TextAnchor.MiddleCenter;
        lay.spacing = 12;
        lay.padding = new RectOffset(12, 12, 12, 12);
        lay.childControlWidth = lay.childControlHeight = false;
        lay.childForceExpandWidth = lay.childForceExpandHeight = false;

        for (int i = 1; i <= RightButtonCount; i++)
        {
            var b = new GameObject("B" + i, typeof(RectTransform), typeof(Image), typeof(Button));
            b.transform.SetParent(bar.transform, false);
            ((RectTransform)b.transform).sizeDelta = new Vector2(96, 96);
            var img = b.GetComponent<Image>();
            img.color = new Color(.17f, .21f, .27f);
            b.GetComponent<Button>().targetGraphic = img;

            var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(b.transform, false);
            var lr = (RectTransform)label.transform;
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = Vector2.one;
            lr.offsetMin = lr.offsetMax = Vector2.zero;
            var t = label.GetComponent<Text>();
            t.font = font;
            t.fontSize = 30;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.text = "B" + i;
        }
        EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
    }


    // a test button in the top bar: gives 1000 money (Hud.cs binds it by name)
    static void AddDebugCash(Transform hud)
    {
        var bar = hud != null ? hud.Find("TopBar") : null;
        if (bar == null || bar.Find("DebugCash") != null) return;
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var round = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        var b = UiBox(bar, "DebugCash", round, new Color(.35f, .3f, .15f), true);
        Undo.RegisterCreatedObjectUndo(b.gameObject, "Add debug cash");
        b.anchorMin = b.anchorMax = b.pivot = new Vector2(1, .5f);
        b.sizeDelta = new Vector2(230, 46);
        b.anchoredPosition = new Vector2(-150, 0);   // left of the right bar
        var btn = b.gameObject.AddComponent<Button>();
        btn.targetGraphic = b.GetComponent<Image>();
        var t = UiText(b, font, "+€1000 (test)", 24, new Color(1f, .95f, .6f), FontStyle.Bold);
        t.gameObject.name = "Label";
        t.alignment = TextAnchor.MiddleCenter;
        var lrt = (RectTransform)t.transform;
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
    }

    // the clock of a level with a time limit: in the middle of the top bar, empty while no clock runs
    static void AddTimer(Transform hud)
    {
        var bar = hud != null ? hud.Find("TopBar") : null;
        if (bar == null || bar.Find("Timer") != null) return;
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var go = new GameObject("Timer", typeof(RectTransform), typeof(Text));
        Undo.RegisterCreatedObjectUndo(go, "Add timer");
        go.transform.SetParent(bar, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(.5f, 0);
        rt.anchorMax = new Vector2(.5f, 1);
        rt.pivot = new Vector2(.5f, .5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(500, 0);
        var t = go.GetComponent<Text>();
        t.font = font;
        t.fontSize = 38;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.raycastTarget = false;
        EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
    }

    // top bar: the currency
    static void AddTopBar(Transform hud)
    {
        if (hud == null || hud.Find("TopBar") != null) return;
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var bar = new GameObject("TopBar", typeof(RectTransform), typeof(Image));
        Undo.RegisterCreatedObjectUndo(bar, "Add top bar");
        bar.transform.SetParent(hud, false);
        var rt = (RectTransform)bar.transform;
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(.5f, 1);
        rt.sizeDelta = new Vector2(0, 64);
        rt.anchoredPosition = Vector2.zero;
        bar.GetComponent<Image>().color = new Color(.07f, .09f, .12f, .92f);
        bar.GetComponent<Image>().raycastTarget = true;

        var go = new GameObject("Cash", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(bar.transform, false);
        var crt = (RectTransform)go.transform;
        crt.anchorMin = new Vector2(0, 0);
        crt.anchorMax = new Vector2(0, 1);
        crt.pivot = new Vector2(0, .5f);
        crt.anchoredPosition = new Vector2(24, 0);
        crt.sizeDelta = new Vector2(300, 0);
        var t = go.GetComponent<Text>();
        t.font = font;
        t.fontSize = 38;
        t.alignment = TextAnchor.MiddleLeft;
        t.color = Color.white;
        t.raycastTarget = false;
        EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
    }

    // small toggle left of A1: two hexagons joined by an edge. On = new buildings link themselves to nearby ones.
    static void AddAutoEdge(Transform bar)
    {
        if (bar == null || bar.Find("AutoEdge") != null) return;
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var hex = HexSprite();

        var b = new GameObject("AutoEdge", typeof(RectTransform), typeof(Image), typeof(Button));
        Undo.RegisterCreatedObjectUndo(b, "Add auto edge toggle");
        b.transform.SetParent(bar, false);
        b.transform.SetSiblingIndex(0);
        ((RectTransform)b.transform).sizeDelta = new Vector2(64, 64);
        var img = b.GetComponent<Image>();
        img.color = new Color(.17f, .21f, .27f);
        b.GetComponent<Button>().targetGraphic = img;

        var edge = new Color(.36f, .26f, .22f);
        var cream = new Color(1f, .95f, .85f);
        MiniHex(b.transform, hex, new Vector2(-15, 8), edge, cream);
        MiniHex(b.transform, hex, new Vector2(15, 8), edge, cream);
        var line = new GameObject("Edge", typeof(RectTransform), typeof(Image));
        line.transform.SetParent(b.transform, false);
        var lrt = (RectTransform)line.transform;
        lrt.anchorMin = lrt.anchorMax = lrt.pivot = new Vector2(.5f, .5f);
        lrt.sizeDelta = new Vector2(14, 4);
        lrt.anchoredPosition = new Vector2(0, 8);
        line.GetComponent<Image>().color = Color.white;
        line.GetComponent<Image>().raycastTarget = false;

        var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
        label.transform.SetParent(b.transform, false);
        var rt = (RectTransform)label.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = new Vector2(1, 0);
        rt.pivot = new Vector2(.5f, 0);
        rt.sizeDelta = new Vector2(0, 20);
        rt.anchoredPosition = new Vector2(0, 4);
        var t = label.GetComponent<Text>();
        t.font = font;
        t.fontSize = 15;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.raycastTarget = false;
        t.text = "auto edge";
        EditorSceneManager.MarkSceneDirty(bar.gameObject.scene);
    }

    static void MiniHex(Transform parent, Sprite hex, Vector2 pos, Color outer, Color inner)
    {
        var o = Layer(parent, "Hex Outer", 26, outer, hex);
        o.anchoredPosition = pos;
        var i = Layer(o, "Hex Inner", 20, inner, hex);
        i.anchoredPosition = Vector2.zero;
    }

    // yes/no question dialog: a dimmer over the whole screen with a panel in the middle; hidden until the game asks something
    static void AddConfirmDialog(Transform hud)
    {
        if (hud == null || hud.Find("ConfirmDialog") != null) return;
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var root = new GameObject("ConfirmDialog", typeof(RectTransform), typeof(Image));
        Undo.RegisterCreatedObjectUndo(root, "Add confirm dialog");
        root.transform.SetParent(hud, false);
        var rrt = (RectTransform)root.transform;
        rrt.anchorMin = Vector2.zero;
        rrt.anchorMax = Vector2.one;
        rrt.offsetMin = rrt.offsetMax = Vector2.zero;
        root.GetComponent<Image>().color = new Color(0, 0, 0, .5f);

        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(root.transform, false);
        var prt = (RectTransform)panel.transform;
        prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(.5f, .5f);
        prt.sizeDelta = new Vector2(520, 220);
        panel.GetComponent<Image>().color = new Color(.12f, .15f, .2f);

        var q = new GameObject("Question", typeof(RectTransform), typeof(Text));
        q.transform.SetParent(panel.transform, false);
        var qrt = (RectTransform)q.transform;
        qrt.anchorMin = new Vector2(0, .4f);
        qrt.anchorMax = Vector2.one;
        qrt.offsetMin = new Vector2(24, 0);
        qrt.offsetMax = new Vector2(-24, -16);
        var qt = q.GetComponent<Text>();
        qt.font = font;
        qt.fontSize = 30;
        qt.alignment = TextAnchor.MiddleCenter;
        qt.color = Color.white;
        qt.raycastTarget = false;
        qt.text = "Are you sure?";

        DialogButton(panel.transform, font, "Yes", new Vector2(-110, 40), new Color(.25f, .6f, .4f));
        DialogButton(panel.transform, font, "No", new Vector2(110, 40), new Color(.6f, .3f, .3f));

        root.SetActive(false);
        EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
    }

    static void DialogButton(Transform parent, Font font, string name, Vector2 pos, Color color)
    {
        var b = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        b.transform.SetParent(parent, false);
        var rt = (RectTransform)b.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(.5f, 0);
        rt.pivot = new Vector2(.5f, 0);
        rt.sizeDelta = new Vector2(180, 70);
        rt.anchoredPosition = new Vector2(pos.x, 24);
        var img = b.GetComponent<Image>();
        img.color = color;
        b.GetComponent<Button>().targetGraphic = img;

        var l = new GameObject("Label", typeof(RectTransform), typeof(Text));
        l.transform.SetParent(b.transform, false);
        var lrt = (RectTransform)l.transform;
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        var t = l.GetComponent<Text>();
        t.font = font;
        t.fontSize = 30;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.raycastTarget = false;
        t.text = name;
    }

    static Color ItemColor(string item)
    {
        var i = AssetDatabase.LoadAssetAtPath<ItemDef>("Assets/Resources/Data/Items/" + item + ".asset");
        return i != null ? i.color : Color.gray;
    }

    // A1..A3 look like the buildings they build: the node's hexagon (two outlines with a coloured band between), what it handles, its name and its cost
    static void StyleButtons(Transform bar)
    {
        if (bar == null) return;
        var hex = HexSprite();
        Style(bar.Find("A1"), "Miner", Color.Lerp(ItemColor("wood"), new Color(1f, .95f, .85f), .35f), ItemColor("wood"), ItemColor("stone"), null, hex);
        RestyleRefineries(bar, hex);
        Style(bar.Find("A3"), "Market", new Color(1f, .85f, .3f), Color.clear, Color.clear, "€", hex);
        Style(bar.Find("A4"), "Quest", new Color(.82f, .72f, 1f), ItemColor("plank"), ItemColor("cut stone"), "!", hex);
        RestyleBuildNodes(bar);
        StyleEdges(bar);
        Style(bar.Find("A5"), "Tiles", TileColor("grass"), TileColor("forest"), TileColor("stone"), null, hex);
        Style(bar.Find("A6"), "Open ring", new Color(1f, .95f, .85f), Color.clear, Color.clear, "?", hex);
        EditorSceneManager.MarkSceneDirty(bar.gameObject.scene);
    }


    // A2 opens a submenu of the Processors (Refinery, Crusher). Hud.cs fills in names and prices. The old A2 was the Refinery button itself.
    static void RestyleRefineries(Transform bar, Sprite hex)
    {
        var a2 = bar.Find("A2");
        if (a2 == null) return;
        var cost = a2.Find("Cost");
        if (cost != null) Undo.DestroyObjectImmediate(cost.gameObject);   // the prices are in the menu now
        Style(a2, "Processors", Color.Lerp(ItemColor("plank"), new Color(1f, .95f, .85f), .35f), ItemColor("plank"), ItemColor("cut stone"), null, hex);
        var label = a2.Find("Label");
        if (label != null)
        {
            var lt = label.GetComponent<Text>();
            lt.text = "Processors";
            lt.fontSize = 15;
        }
        BuildMenu(a2, "A2", new[] { "Refinery", "Crusher", "soon" }, new[] { false, false, false }, new[] { ItemColor("plank"), ItemColor("sand"), new Color(.3f, .34f, .42f) }, hex);
        EditorSceneManager.MarkSceneDirty(bar.gameObject.scene);
    }

    // A8 opens a menu of the special edges (Tier, Filter, Chest). Hud.cs fills in names and prices and greys out the ones not unlocked yet.
    static void StyleEdges(Transform bar)
    {
        var a8 = bar.Find("A8");
        DropOutdatedMenu(a8);
        if (a8 == null) return;
        // an earlier menu had a Chest edge entry: that edge is drawn by dragging out of a Storage now, so the menu is rebuilt
        var oldMenu = a8.Find("Menu/A8_3/Label");
        if (oldMenu != null && oldMenu.GetComponent<Text>().text.Contains("Chest")) Undo.DestroyObjectImmediate(a8.Find("Menu").gameObject);
        var hex = HexSprite();
        Style(a8, "Edges", new Color(.8f, .9f, 1f), Color.clear, Color.clear, "~", hex);
        BuildMenu(a8, "A8", new[] { "Tier edge", "Filter edge", "soon" }, new[] { false, false, false }, new[] { new Color(.55f, .78f, 1f), new Color(.7f, .9f, .5f), new Color(.3f, .34f, .42f) }, hex);
        EditorSceneManager.MarkSceneDirty(bar.gameObject.scene);
    }

    static Button PanelButton(Transform parent, Font font, Sprite round, string name, string label, Color color, Vector2 size, Vector2 pos, Vector2 anchor)
    {
        var rt = UiBox(parent, name, round, color, true);
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        var b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = rt.GetComponent<Image>();
        var t = UiText(rt, font, label, 24, new Color(1f, .95f, .85f), FontStyle.Bold);
        t.gameObject.name = "Label";
        t.alignment = TextAnchor.MiddleCenter;
        var lrt = (RectTransform)t.transform;
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        return b;
    }

    static RectTransform PanelGroup(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(.5f, 1);
        rt.anchoredPosition = new Vector2(0, -200);
        rt.sizeDelta = new Vector2(-40, 330);
        return rt;
    }

    static RectTransform PanelGrid(Transform parent, string name, Vector2 cell, int columns, float top)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(GridLayoutGroup));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(.5f, 1);
        rt.anchoredPosition = new Vector2(0, top);
        rt.sizeDelta = new Vector2(0, 250);
        var g = go.GetComponent<GridLayoutGroup>();
        g.cellSize = cell;
        g.spacing = new Vector2(8, 8);
        g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        g.constraintCount = columns;
        return rt;
    }

    // the settings panel of a special edge: tier or item buttons are added at runtime by EdgePanel.cs
    static void AddEdgePanel(Transform hud)
    {
        if (hud == null || hud.Find("EdgePanel") != null) return;
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var round = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        var root = UiBox(hud, "EdgePanel", round, new Color(.12f, .15f, .21f, .97f), true);
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Add edge panel");
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(1, .5f);
        root.sizeDelta = new Vector2(440, 640);
        root.anchoredPosition = new Vector2(-30, 20);
        root.gameObject.AddComponent<EdgePanel>();

        var title = UiText(root, font, "Edge", 36, new Color(1f, .95f, .85f), FontStyle.Bold);
        title.gameObject.name = "Title";
        var trt = (RectTransform)title.transform;
        trt.anchorMin = new Vector2(0, 1);
        trt.anchorMax = new Vector2(1, 1);
        trt.pivot = new Vector2(.5f, 1);
        trt.anchoredPosition = new Vector2(20, -16);
        trt.sizeDelta = new Vector2(-100, 50);

        var hint = UiText(root, font, "", 20, new Color(.7f, .76f, .84f), FontStyle.Normal);
        hint.gameObject.name = "Hint";
        hint.horizontalOverflow = HorizontalWrapMode.Wrap;
        hint.alignment = TextAnchor.UpperLeft;
        var hrt = (RectTransform)hint.transform;
        hrt.anchorMin = new Vector2(0, 1);
        hrt.anchorMax = new Vector2(1, 1);
        hrt.pivot = new Vector2(.5f, 1);
        hrt.anchoredPosition = new Vector2(0, -74);
        hrt.sizeDelta = new Vector2(-40, 80);

        PanelButton(root, font, round, "Close", "X", new Color(.35f, .22f, .22f), new Vector2(56, 50), new Vector2(-14, -14), new Vector2(1, 1));
        PanelButton(root, font, round, "Remove", "Remove edge", new Color(.6f, .3f, .3f), new Vector2(400, 60), new Vector2(0, 20), new Vector2(.5f, 0));

        var tier = PanelGroup(root, "Tier");
        PanelButton(tier, font, round, "UpTo", "Up to", new Color(.2f, .26f, .36f), new Vector2(190, 56), new Vector2(0, 0), new Vector2(0, 1));
        PanelButton(tier, font, round, "Exactly", "Exactly", new Color(.2f, .26f, .36f), new Vector2(190, 56), new Vector2(0, 0), new Vector2(1, 1));
        PanelGrid(tier, "Tiers", new Vector2(120, 56), 3, -76);

        var filter = PanelGroup(root, "Filter");
        PanelButton(filter, font, round, "Deny", "Block these", new Color(.2f, .26f, .36f), new Vector2(190, 56), new Vector2(0, 0), new Vector2(0, 1));
        PanelButton(filter, font, round, "Allow", "Only these", new Color(.2f, .26f, .36f), new Vector2(190, 56), new Vector2(0, 0), new Vector2(1, 1));
        PanelGrid(filter, "Items", new Vector2(190, 56), 2, -76);

        root.gameObject.SetActive(false);
        EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
    }
    // the inspection window (right click on a building): the frame is here, NodeInspector.cs fills it with rows
    static void AddInspector(Transform hud)
    {
        if (hud == null || hud.Find("Inspector") != null) return;
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var round = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        var root = UiBox(hud, "Inspector", round, new Color(.075f, .09f, .125f, .97f), true);
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Add inspector");
        root.anchorMin = new Vector2(0, 0);
        root.anchorMax = new Vector2(0, 1);
        root.pivot = new Vector2(0, .5f);
        root.sizeDelta = new Vector2(470, -330);
        root.anchoredPosition = new Vector2(20, 20);
        root.gameObject.AddComponent<NodeInspector>();
        var outline = root.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0, 0, 0, .9f);
        outline.effectDistance = new Vector2(2, -2);

        var title = UiText(root, font, "Building", 36, new Color(1f, .95f, .85f), FontStyle.Bold);
        title.gameObject.name = "Title";
        var trt = (RectTransform)title.transform;
        trt.anchorMin = new Vector2(0, 1);
        trt.anchorMax = new Vector2(1, 1);
        trt.pivot = new Vector2(.5f, 1);
        trt.anchoredPosition = new Vector2(20, -14);
        trt.sizeDelta = new Vector2(-100, 50);

        PanelButton(root, font, round, "Close", "X", new Color(.35f, .22f, .22f), new Vector2(56, 50), new Vector2(-14, -14), new Vector2(1, 1));

        // the rows scroll when there are more than fit
        var scroll = UiBox(root, "Scroll", null, new Color(0, 0, 0, 0), false);
        scroll.anchorMin = Vector2.zero;
        scroll.anchorMax = Vector2.one;
        scroll.offsetMin = new Vector2(20, 16);
        scroll.offsetMax = new Vector2(-20, -74);
        scroll.gameObject.AddComponent<RectMask2D>();
        var sr = scroll.gameObject.AddComponent<ScrollRect>();

        var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(scroll, false);
        var crt = (RectTransform)content.transform;
        crt.anchorMin = new Vector2(0, 1);
        crt.anchorMax = new Vector2(1, 1);
        crt.pivot = new Vector2(.5f, 1);
        crt.anchoredPosition = Vector2.zero;
        crt.sizeDelta = Vector2.zero;
        var v = content.GetComponent<VerticalLayoutGroup>();
        v.spacing = 4;
        v.childControlWidth = v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        sr.content = crt;
        sr.horizontal = false;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 30;

        root.gameObject.SetActive(false);
        EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
    }

    // A7 opens a submenu of Build Nodes (nodes that need materials after being placed). An older A7 that was a plain Storage button is restyled.
    static void RestyleBuildNodes(Transform bar)
    {
        var a7 = bar.Find("A7");
        DropOutdatedMenu(a7);
        if (a7 == null) return;
        var label = a7.Find("Label");
        if (a7.Find("Icon") != null && label != null && label.GetComponent<Text>().text != "Build Nodes")
        {
            foreach (var n in new[] { "Icon", "Cost", "Menu" })
            {
                var c = a7.Find(n);
                if (c != null) Undo.DestroyObjectImmediate(c.gameObject);
            }
        }
        var hex = HexSprite();
        Style(a7, "Build Nodes", new Color(.74f, .85f, 1f), Color.clear, Color.clear, "+", hex);
        var l = a7.Find("Label");
        if (l != null) l.GetComponent<Text>().fontSize = 15;
        var cost = a7.Find("Cost");
        if (cost != null) Undo.DestroyObjectImmediate(cost.gameObject);
        BuildMenu(a7, "A7", new[] { "Storage", "soon", "soon" }, new[] { false, false, false }, new[] { new Color(.74f, .85f, 1f), new Color(.3f, .34f, .42f), new Color(.3f, .34f, .42f) }, hex);
        EditorSceneManager.MarkSceneDirty(bar.gameObject.scene);
    }

    static RectTransform Layer(Transform parent, string name, float size, Color c, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = c;
        img.raycastTarget = false;
        return rt;
    }

    static void Style(Transform button, string title, Color body, Color dot1, Color dot2, string glyph, Sprite hex)
    {
        if (button == null || button.Find("Icon") != null) return;
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var edge = new Color(.36f, .26f, .22f);

        var icon = new GameObject("Icon", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(icon, "Style HUD button");
        icon.transform.SetParent(button, false);
        var irt = (RectTransform)icon.transform;
        irt.anchorMin = irt.anchorMax = irt.pivot = new Vector2(.5f, 1f);
        irt.sizeDelta = new Vector2(56, 56);
        irt.anchoredPosition = new Vector2(0, -5);
        Layer(irt, "Outline Outer", 56, edge, hex);
        Layer(irt, "Slider Band", 50, new Color(.45f, .88f, .55f), hex);
        Layer(irt, "Outline Inner", 38, edge, hex);
        Layer(irt, "Body", 34, body, hex);
        if (glyph != null)
        {
            var g = new GameObject("Glyph", typeof(RectTransform), typeof(Text));
            g.transform.SetParent(irt, false);
            var grt = (RectTransform)g.transform;
            grt.anchorMin = Vector2.zero;
            grt.anchorMax = Vector2.one;
            grt.offsetMin = grt.offsetMax = Vector2.zero;
            var gt = g.GetComponent<Text>();
            gt.font = font;
            gt.fontSize = 26;
            gt.alignment = TextAnchor.MiddleCenter;
            gt.color = edge;
            gt.text = glyph;
            gt.raycastTarget = false;
        }
        else
        {
            Layer(irt, "Item 1", 11, dot1, hex).anchoredPosition = new Vector2(-7, 0);
            Layer(irt, "Item 2", 11, dot2, hex).anchoredPosition = new Vector2(7, 0);
        }

        var label = button.Find("Label");
        if (label != null)
        {
            var lr = (RectTransform)label;
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = new Vector2(1, 0);
            lr.pivot = new Vector2(.5f, 0);
            lr.sizeDelta = new Vector2(0, 22);
            lr.anchoredPosition = new Vector2(0, 18);
            var t = label.GetComponent<Text>();
            t.fontSize = 18;
            t.text = title;
        }

        var cost = new GameObject("Cost", typeof(RectTransform), typeof(Text));
        cost.transform.SetParent(button, false);
        var crt = (RectTransform)cost.transform;
        crt.anchorMin = Vector2.zero;
        crt.anchorMax = new Vector2(1, 0);
        crt.pivot = new Vector2(.5f, 0);
        crt.sizeDelta = new Vector2(0, 18);
        crt.anchoredPosition = new Vector2(0, 2);
        var ct = cost.GetComponent<Text>();
        ct.font = font;
        ct.fontSize = 14;
        ct.alignment = TextAnchor.MiddleCenter;
        ct.color = new Color(.85f, .88f, .92f);
        ct.raycastTarget = false;
    }

    static Color TileColor(string tile)
    {
        var t = AssetDatabase.LoadAssetAtPath<TileDef>("Assets/Resources/Data/Tiles/" + tile + ".asset");
        return t != null ? t.fill : Color.gray;
    }

    // A5 (tiles) gets a "Menu" that expands upwards: the first entries are real buttons, the rest are proxies.
    // The building buttons A1 to A4 place their building directly and have no menu.
    static void AddMenus(Transform bar)
    {
        if (bar == null) return;
        // an older menu (two entries, both looking the same) is rebuilt
        var old = bar.Find("A5/Menu");
        if (old != null && old.Find("A5_6/Icon/Body") == null) Undo.DestroyObjectImmediate(old.gameObject);
        DropOutdatedMenu(bar.Find("A5"));
        var forest = TileColor("forest");
        var stone = TileColor("stone");
        var water = TileColor("water");
        BuildMenu(bar.Find("A5"), "A5", new[] { "forest", "forest", "stone", "stone", "water", "water" }, new[] { false, true, false, true, false, true }, new[] { forest, forest, stone, stone, water, water }, HexSprite());
        EditorSceneManager.MarkSceneDirty(bar.gameObject.scene);
    }

    // The tile menu: one entry per offer, bottom to top. The "starter" entries (anywhere, limited, pricier) have a gold frame and a gold-rimmed icon
    // so they cannot be mistaken for the normal ones. The game fills in the text (price, limit) and hides entries a level does not offer yet.
    static void BuildMenu(Transform button, string id, string[] titles, bool[] starter, Color[] tints, Sprite hex)
    {
        if (button == null || button.Find("Menu") != null) return;
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var menu = new GameObject("Menu", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(menu, "Add HUD menu");
        menu.transform.SetParent(button, false);
        var mrt = (RectTransform)menu.transform;
        mrt.anchorMin = mrt.anchorMax = new Vector2(.5f, 1f);
        mrt.pivot = new Vector2(.5f, 0f);
        mrt.anchoredPosition = new Vector2(0, 10);
        mrt.sizeDelta = new Vector2(MenuEntryW, 10);

        var gold = new Color(1f, .8f, .3f);
        for (int i = 1; i <= titles.Length; i++)
        {
            bool st = starter[i - 1];
            var e = new GameObject(id + "_" + i, typeof(RectTransform), typeof(Image), typeof(Button));
            e.transform.SetParent(menu.transform, false);
            var ert = (RectTransform)e.transform;
            ert.anchorMin = ert.anchorMax = new Vector2(.5f, 0f);
            ert.pivot = new Vector2(.5f, 0f);
            ert.sizeDelta = new Vector2(MenuEntryW, MenuEntryH);
            ert.anchoredPosition = new Vector2(0, (i - 1) * (MenuEntryH + 8));
            var img = e.GetComponent<Image>();
            img.color = st ? new Color(.3f, .24f, .12f) : new Color(.17f, .21f, .27f);
            e.GetComponent<Button>().targetGraphic = img;
            if (st)
            {
                var o = e.AddComponent<Outline>();
                o.effectColor = gold;
                o.effectDistance = new Vector2(3, 3);
            }

            var icon = new GameObject("Icon", typeof(RectTransform));
            icon.transform.SetParent(e.transform, false);
            var irt = (RectTransform)icon.transform;
            irt.anchorMin = irt.anchorMax = irt.pivot = new Vector2(.5f, 1f);
            irt.sizeDelta = new Vector2(56, 56);
            irt.anchoredPosition = new Vector2(0, -6);
            if (st) Layer(irt, "Rim", 56, gold, hex);
            Layer(irt, "Body", st ? 46 : 52, tints[i - 1], hex);

            var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(e.transform, false);
            var lr = (RectTransform)label.transform;
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = new Vector2(1, 0);
            lr.pivot = new Vector2(.5f, 0);
            lr.sizeDelta = new Vector2(-8, MenuEntryH - 64);
            lr.anchoredPosition = new Vector2(0, 3);
            var t = label.GetComponent<Text>();
            t.font = font;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.raycastTarget = false;
            t.fontSize = 15;
            t.text = "<b>" + (st ? "Starter " : "") + titles[i - 1] + "</b>";
        }
        menu.SetActive(false);
    }

    internal static Sprite HexSprite()
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(HexPath);
        if (s != null) return s;

        System.IO.Directory.CreateDirectory("Assets/Textures");
        const int n = 128;
        float r = 60f, a = r * .866f;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float ax = Mathf.Abs(x + .5f - n / 2f), ay = Mathf.Abs(y + .5f - n / 2f);
                float d = Mathf.Max(ax, ax * .5f + ay * .866f) - a;
                tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(.5f - d)));
            }
        System.IO.File.WriteAllBytes(HexPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(HexPath);
        var imp = (TextureImporter)AssetImporter.GetAtPath(HexPath);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = false;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(HexPath);
    }

    static void CreateHud()
    {
        var hud = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(hud, "Create HUD");
        hud.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = hud.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;

        var bar = new GameObject("BottomBar", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
        bar.transform.SetParent(hud.transform, false);
        var rt = (RectTransform)bar.transform;
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(1, 0);
        rt.pivot = new Vector2(.5f, 0);
        rt.sizeDelta = new Vector2(0, 120);
        rt.anchoredPosition = Vector2.zero;
        bar.GetComponent<Image>().color = new Color(.07f, .09f, .12f, .92f);
        var lay = bar.GetComponent<HorizontalLayoutGroup>();
        lay.childAlignment = TextAnchor.MiddleCenter;
        lay.spacing = 12;
        lay.padding = new RectOffset(16, 16, 12, 12);
        lay.childControlWidth = lay.childControlHeight = false;
        lay.childForceExpandWidth = lay.childForceExpandHeight = false;

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        for (int i = 1; i <= ButtonCount; i++)
        {
            var b = new GameObject("A" + i, typeof(RectTransform), typeof(Image), typeof(Button));
            b.transform.SetParent(bar.transform, false);
            ((RectTransform)b.transform).sizeDelta = new Vector2(96, 96);
            var img = b.GetComponent<Image>();
            img.color = new Color(.17f, .21f, .27f);
            b.GetComponent<Button>().targetGraphic = img;

            var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(b.transform, false);
            var lr = (RectTransform)label.transform;
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = Vector2.one;
            lr.offsetMin = lr.offsetMax = Vector2.zero;
            var t = label.GetComponent<Text>();
            t.font = font;
            t.fontSize = 30;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.text = "A" + i;
        }

        if (Object.FindObjectOfType<EventSystem>() == null)
            Undo.RegisterCreatedObjectUndo(new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule)), "Create EventSystem");

        AddMenus(bar.transform);
        StyleButtons(bar.transform);
        AddTopBar(hud.transform);
        AddTimer(hud.transform);
        AddDebugCash(hud.transform);
        AddConfirmDialog(hud.transform);
        AddPauseMenu(hud.transform);
        AddEdgePanel(hud.transform);
        AddInspector(hud.transform);
        AddLevelComplete(hud.transform);
        AddAutoEdge(bar.transform);
        AddRightBar(hud.transform);
        LabelResetButton(hud.transform);
        AddInfoBoard(hud.transform);
        Selection.activeGameObject = hud;
        EditorSceneManager.MarkSceneDirty(hud.scene);
    }
}
