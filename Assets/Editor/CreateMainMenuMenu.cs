using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Builds the main menu scene (Assets/Scenes/MainMenu.unity) as real hierarchy objects, and the first upgrade asset.
// The cards inside the lists are built at runtime by MainMenu.cs from the game data.
public static class CreateMainMenuMenu
{
    const string ScenePath = "Assets/Scenes/MainMenu.unity";
    const string GameScenePath = "Assets/Scenes/SampleScene.unity";
    const string BuffFolder = "Assets/Resources/Data/Buffs";

    static readonly Color Bg = new Color(.07f, .09f, .13f);
    static readonly Color Panel = new Color(.12f, .15f, .21f);
    static readonly Color Cream = new Color(1f, .95f, .85f);
    static readonly Color Gold = new Color(1f, .84f, .3f);

    // the upgrades that exist: asset name = id in the save file
    public static void EnsureBuffs()
    {
        Directory.CreateDirectory(BuffFolder);
        Make("Head Start", "Every level starts with €50 instead of €10.", 1, BuffEffect.StartCash, 50);
        var forest = AssetDatabase.LoadAssetAtPath<TileDef>("Assets/Resources/Data/Tiles/forest.asset");
        var stone = AssetDatabase.LoadAssetAtPath<TileDef>("Assets/Resources/Data/Tiles/stone.asset");
        Make("Raw Value", "Raw resources (tier 0: wood, stone, water) sell for double.", 1, BuffEffect.SellValue, 2, null, 0);
        Make("Market Boost", "Every Market can earn up to €20 per second instead of €10.", 2, BuffEffect.MarketCap, 20);
        Make("Market Boost II", "Every Market can earn up to €50 per second instead of €20.", 2, BuffEffect.MarketCap, 50);
        Make("Starter Forest", "You may place 1 starter forest tile per level, anywhere on the open map.", 1, BuffEffect.StarterTiles, 1, forest);
        Make("Starter Stone", "You may place 1 starter stone tile per level, anywhere on the open map.", 1, BuffEffect.StarterTiles, 1, stone);
        AssetDatabase.SaveAssets();
    }

    static void Make(string name, string description, int cost, BuffEffect effect, float amount, TileDef tile = null, int tier = 0)
    {
        string path = BuffFolder + "/" + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<BuffDef>(path);
        if (existing != null)
        {
            // the price is set here, so a change of price in this list reaches the asset that already exists
            existing.cost = cost;
            EditorUtility.SetDirty(existing);
            return;
        }
        AssetDatabase.CreateAsset(BuffDef.Make(name, description, cost, effect, amount, tile, tier), path);
    }

    [MenuItem("NodeLoad/Create Main Menu Scene")]
    static void Run()
    {
        if (Application.isPlaying) { Debug.LogWarning("Main menu: leave Play mode first."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EnsureBuffs();

        string previous = SceneManager.GetActiveScene().path;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Build();
        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettings();
        if (!string.IsNullOrEmpty(previous) && previous != ScenePath) EditorSceneManager.OpenScene(previous);
        Debug.Log("Main menu scene saved: " + ScenePath + ". Open it and press Play to see it.");
    }

    static void AddToBuildSettings()
    {
        var list = new System.Collections.Generic.List<EditorBuildSettingsScene>();
        list.Add(new EditorBuildSettingsScene(ScenePath, true));
        list.Add(new EditorBuildSettingsScene(GameScenePath, true));
        EditorBuildSettings.scenes = list.ToArray();
    }

    static void Build()
    {
        var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cam.tag = "MainCamera";
        var c = cam.GetComponent<Camera>();
        c.orthographic = true;
        c.clearFlags = CameraClearFlags.SolidColor;
        c.backgroundColor = Bg;

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(MainMenu));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;
        var menu = canvasGo.GetComponent<MainMenu>();

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var round = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        var hex = CreateHudMenu.HexSprite();
        menu.round = round;
        menu.hex = hex;

        // the title, the star count and the main buttons
        var root = canvasGo.transform;
        var title = Text(root, font, "Title", "NodeLoad", 120, Cream, FontStyle.Bold, TextAnchor.MiddleCenter);
        Place(title, new Vector2(.5f, 1), new Vector2(0, -150), new Vector2(900, 160));
        var sub = Text(root, font, "Subtitle", "build it. connect it. sell it.", 34, new Color(.6f, .66f, .75f), FontStyle.Normal, TextAnchor.MiddleCenter);
        Place(sub, new Vector2(.5f, 1), new Vector2(0, -250), new Vector2(900, 50));

        var stars = Text(root, font, "Stars", "Stars  0", 44, Gold, FontStyle.Bold, TextAnchor.MiddleRight);
        Place(stars, new Vector2(1, 1), new Vector2(-50, -50), new Vector2(400, 60), new Vector2(1, 1));
        menu.starsText = stars;

        var buttons = Box(root, "Menu", null, Color.clear, false);
        Place(buttons, new Vector2(.5f, .5f), new Vector2(0, -90), new Vector2(520, 700));
        string[] names = { "Play", "Resources", "Upgrades", "Quit" };
        for (int i = 0; i < names.Length; i++)
        {
            var b = MenuButton(buttons, font, round, names[i], i == 0);
            Place(b, new Vector2(.5f, 1), new Vector2(0, -i * 128 - (i > 0 ? 24 : 0) - 55), new Vector2(480, i == 0 ? 110 : 96));
        }

        // debug helpers while there is little to earn yet
        var debug = Box(root, "Debug", null, Color.clear, false);
        Place(debug, new Vector2(0, 0), new Vector2(40, 40), new Vector2(640, 70), new Vector2(0, 0));
        var add = SmallButton(debug, font, round, "AddStar", "+1 Star (test)");
        Place(add, new Vector2(0, .5f), new Vector2(0, 0), new Vector2(250, 60), new Vector2(0, .5f));
        var reset = SmallButton(debug, font, round, "ResetSave", "Reset ALL progress");
        Place(reset, new Vector2(1, .5f), new Vector2(0, 0), new Vector2(330, 60), new Vector2(1, .5f));

        menu.levelsPanel = Window(root, font, round, "LevelsPanel", "Choose a level", "Finished levels are green, the next one can be played. More levels will follow.", 170, 170, 7, out menu.levelsContent);
        menu.resourcesPanel = Window(root, font, round, "ResourcesPanel", "Resources", "Every item in the game and what it sells for.", 400, 118, 3, out menu.resourcesContent);
        menu.upgradesPanel = Window(root, font, round, "UpgradesPanel", "Upgrades", "Buy permanent upgrades with stars. You get 1 star for every level you complete.", 400, 150, 3, out menu.upgradesContent);
        EditorUtility.SetDirty(menu);
    }

    // a full-screen dimmed panel (clicking outside the window closes it) with a title, a close button and a scrolling grid
    static GameObject Window(Transform root, Font font, Sprite round, string name, string title, string hint, float cellW, float cellH, int columns, out RectTransform content)
    {
        var panel = Box(root, name, null, new Color(0, 0, 0, .7f), false);
        Stretch(panel);
        panel.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;

        var win = Box(panel, "Window", round, Panel, true);
        Place(win, new Vector2(.5f, .5f), Vector2.zero, new Vector2(1400, 880));
        win.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;   // swallows clicks so the window does not close itself

        var t = Text(win, font, "Title", title, 60, Cream, FontStyle.Bold, TextAnchor.MiddleLeft);
        Place(t, new Vector2(0, 1), new Vector2(40, -20), new Vector2(700, 80), new Vector2(0, 1));
        var h = Text(win, font, "Hint", hint, 24, new Color(.6f, .66f, .75f), FontStyle.Normal, TextAnchor.MiddleLeft);
        Place(h, new Vector2(0, 1), new Vector2(40, -96), new Vector2(1000, 36), new Vector2(0, 1));

        var close = SmallButton(win, font, round, "Close", "Close");
        Place(close, new Vector2(1, 1), new Vector2(-30, -30), new Vector2(160, 60), new Vector2(1, 1));

        var scrollGo = Box(win, "Scroll", null, new Color(0, 0, 0, .18f), false);
        var srt = scrollGo;
        srt.anchorMin = Vector2.zero;
        srt.anchorMax = Vector2.one;
        srt.offsetMin = new Vector2(20, 20);
        srt.offsetMax = new Vector2(-20, -150);
        var scroll = scrollGo.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.scrollSensitivity = 40;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        var viewport = Box(scrollGo, "Viewport", null, new Color(0, 0, 0, 0), false);
        Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();

        content = Box(viewport, "Content", null, Color.clear, false);
        content.GetComponent<Image>().raycastTarget = false;
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(.5f, 1);
        content.offsetMin = content.offsetMax = Vector2.zero;
        var grid = content.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(cellW, cellH);
        grid.spacing = new Vector2(14, 14);
        grid.padding = new RectOffset(14, 14, 14, 14);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport;
        scroll.content = content;

        panel.gameObject.SetActive(false);
        return panel.gameObject;
    }

    // ---- small builders ----
    static RectTransform Box(Transform parent, string name, Sprite sprite, Color c, bool sliced)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        img.color = c;
        return (RectTransform)go.transform;
    }

    static Text Text(Transform parent, Font font, string name, string text, int size, Color c, FontStyle style, TextAnchor align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = c;
        t.alignment = align;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.raycastTarget = false;
        t.text = text;
        return t;
    }

    static void Place(Graphic g, Vector2 anchor, Vector2 pos, Vector2 size) { Place((RectTransform)g.transform, anchor, pos, size, anchor); }
    static void Place(Graphic g, Vector2 anchor, Vector2 pos, Vector2 size, Vector2 pivot) { Place((RectTransform)g.transform, anchor, pos, size, pivot); }
    static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size) { Place(rt, anchor, pos, size, anchor); }

    static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size, Vector2 pivot)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static RectTransform MenuButton(Transform parent, Font font, Sprite round, string name, bool primary)
    {
        var box = Box(parent, name, round, primary ? new Color(.95f, .72f, .2f) : new Color(.2f, .26f, .36f), true);
        var b = box.gameObject.AddComponent<Button>();
        b.targetGraphic = box.GetComponent<Image>();
        var cols = b.colors;
        cols.highlightedColor = new Color(1.12f, 1.12f, 1.12f);
        cols.pressedColor = new Color(.8f, .8f, .8f);
        b.colors = cols;
        var t = Text(box, font, "Label", name, primary ? 52 : 42, primary ? new Color(.2f, .12f, .02f) : Cream, FontStyle.Bold, TextAnchor.MiddleCenter);
        Stretch((RectTransform)t.transform);
        return box;
    }

    static RectTransform SmallButton(Transform parent, Font font, Sprite round, string name, string label)
    {
        var box = Box(parent, name, round, new Color(.25f, .3f, .42f), true);
        var b = box.gameObject.AddComponent<Button>();
        b.targetGraphic = box.GetComponent<Image>();
        var t = Text(box, font, "Label", label, 28, Cream, FontStyle.Bold, TextAnchor.MiddleCenter);
        Stretch((RectTransform)t.transform);
        return box;
    }
}
