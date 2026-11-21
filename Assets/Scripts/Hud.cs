using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Hud : MonoBehaviour
{
    public World world;
    public BuildTool tool;
    Text cashText;

    class Slot
    {
        public Image image;
        public NodeDef def;
        public Color normal;
    }
    readonly List<Slot> slots = new List<Slot>();

    // an entry of the tile menu; which offer it stands for is decided every frame from the current level
    class TileEntry
    {
        public GameObject go;
        public Image image;
        public Text label;
        public Color normal;
    }
    readonly List<TileEntry> tileEntries = new List<TileEntry>();

    readonly List<GameObject> menus = new List<GameObject>();
    readonly List<KeyValuePair<Text, NodeDef>> costs = new List<KeyValuePair<Text, NodeDef>>();

    public const string MainMenuScene = "MainMenu", GameScene = "SampleScene";

    readonly List<GameObject> boards = new List<GameObject>();   // B1: resources, B2: nodes and their ratios
    GameObject pause;        // the Esc menu
    public EdgePanel edgePanel;   // settings of a special edge
    public NodeInspector inspector;   // opened by a right click on a building
    Text edgesLabel;               // the A8 button's second line: "locked" until a level introduces special edges
    class EdgeEntry { public Button button; public Image image; public Text label; public EdgeDef def; public Color normal; }
    readonly List<EdgeEntry> edgeEntries = new List<EdgeEntry>();
    GameObject dialog;
    Text dialogText;
    System.Action onYes, onNo;
    RectTransform dialogPanel;
    Image dialogDim;
    Color dialogDimColor;

    public bool DialogOpen { get { return (pause != null && pause.activeSelf) || (dialog != null && dialog.activeSelf) || (success != null && success.activeSelf); } }

    // ---- level complete screen: a dimmed overlay with a title that pops in, falling confetti and a button for the next level ----
    GameObject success;
    Text successButton;                 // the label of the button on that screen
    System.Action successAction;        // what the button does: lead to the level menu, or try again
    Text timerText;                     // the level's time limit, in the top bar
    RectTransform successPanel;
    Text successTitle, successSub;
    float successT;
    class Bit { public RectTransform rt; public Vector2 vel; public float spin; }
    readonly List<Bit> confetti = new List<Bit>();

    void BindSuccess(Transform hudT)
    {
        var s = hudT != null ? hudT.Find("LevelComplete") : null;
        if (s == null) return;
        success = s.gameObject;
        successPanel = (RectTransform)s.Find("Panel");
        successTitle = s.Find("Panel/Title").GetComponent<Text>();
        successSub = s.Find("Panel/Sub").GetComponent<Text>();
        successButton = s.Find("Panel/Next/Label").GetComponent<Text>();
        s.Find("Panel/Next").GetComponent<Button>().onClick.AddListener(() => successAction());
        success.SetActive(false);
    }

    public void ShowLevelComplete(LevelDef done, LevelDef next)
    {
        if (success == null) return;
        successTitle.text = "LEVEL COMPLETE!";
        successSub.text = "Delivered " + done.GoalText + "\n+1 Star";
        successButton.text = "Levels";
        successAction = GoToLevelSelect;
        Time.timeScale = 0f;   // the game stands still behind the screen
        successT = 0;
        success.SetActive(true);
        var r = ((RectTransform)success.transform).rect;
        foreach (var b in confetti) Destroy(b.rt.gameObject);
        confetti.Clear();
        for (int i = 0; i < 80; i++)
        {
            var go = new GameObject("Confetti", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(success.transform, false);
            go.transform.SetAsFirstSibling();
            var img = go.GetComponent<Image>();
            img.color = Color.HSVToRGB(Random.value, .6f, 1f);
            img.raycastTarget = false;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
            rt.sizeDelta = new Vector2(Random.Range(10f, 22f), Random.Range(16f, 30f));
            rt.anchoredPosition = new Vector2(Random.Range(-r.width / 2, r.width / 2), Random.Range(0, r.height / 2 + 200f));
            confetti.Add(new Bit { rt = rt, vel = new Vector2(Random.Range(-40f, 40f), -Random.Range(120f, 320f)), spin = Random.Range(-240f, 240f) });
        }
    }

    // the time ran out: the same screen, with a button to try the level again on a fresh map
    public void ShowLevelFailed(LevelDef level, string progress)
    {
        if (success == null) return;
        successTitle.text = "TIME'S UP!";
        successSub.text = progress + "\nnot delivered in time.";
        successButton.text = "Try again";
        successAction = Retry;
        Time.timeScale = 0f;
        successT = 0;
        foreach (var b in confetti) Destroy(b.rt.gameObject);
        confetti.Clear();
        success.SetActive(true);
    }

    void Retry()
    {
        Time.timeScale = 1f;
        success.SetActive(false);
        world.ResetLevel();
    }

    // the success screen leads to the level menu of the main menu
    void GoToLevelSelect()
    {
        SaveSystem.OpenLevelSelect = true;
        GoToMainMenu();
    }

    void AnimateSuccess()
    {
        float dt = Time.unscaledDeltaTime;
        successT += dt;
        float k = Mathf.Clamp01(successT / .5f) - 1f;
        successPanel.localScale = Vector3.one * (1f + 2.7f * k * k * k + 1.7f * k * k);   // pops in with an overshoot
        float h = ((RectTransform)success.transform).rect.height / 2 + 40f;
        foreach (var b in confetti)
        {
            var p = b.rt.anchoredPosition + b.vel * dt;
            if (p.y < -h) p.y = h;
            b.rt.anchoredPosition = p;
            b.rt.Rotate(0, 0, b.spin * dt);
        }
    }

    // asks a yes/no question; without the dialog in the scene the action just runs
    // focus: a point in the world the question is about. The screen is not dimmed then, so it can be seen (the building also marks itself),
    // and the question moves out of the way if it would cover that point.
    public void Ask(string text, System.Action yes, System.Action no = null, Vector3? focus = null)
    {
        if (dialog == null) { yes(); return; }
        onYes = yes;
        onNo = no;
        dialogDim.color = focus.HasValue ? new Color(dialogDimColor.r, dialogDimColor.g, dialogDimColor.b, 0f) : dialogDimColor;
        dialogPanel.anchoredPosition = Vector2.zero;
        if (focus.HasValue && Camera.main != null)
        {
            Vector3 sp = Camera.main.WorldToScreenPoint(focus.Value);
            float scale = dialog.GetComponentInParent<Canvas>().scaleFactor;
            var half = dialogPanel.sizeDelta * .5f * scale + new Vector2(60, 60) * scale;
            var c = new Vector2(Screen.width, Screen.height) * .5f;
            if (Mathf.Abs(sp.x - c.x) < half.x && Mathf.Abs(sp.y - c.y) < half.y)
                dialogPanel.anchoredPosition = new Vector2(0, sp.y >= c.y ? -300 : 300);
        }
        dialogText.text = text;
        dialog.SetActive(true);
    }

    void CloseDialog()
    {
        var no = onNo;   // closing in any way but "Yes" counts as no
        onYes = null;
        onNo = null;
        dialog.SetActive(false);
        if (no != null) no();
    }

    // bottom bar from the scene: A1 Miner, A2 Refinery, A3 Market and A4 Quest node select that building directly, A5 expands the tile menu
    void Start()
    {
        var top = GameObject.Find("TopBar");
        if (top != null) cashText = Label(top.transform, "Cash");
        if (top != null) timerText = Label(top.transform, "Timer");

        var hudObject = GameObject.Find("HUD");
        var d = hudObject != null ? hudObject.transform.Find("ConfirmDialog") : null;
        if (d != null)
        {
            dialog = d.gameObject;
            dialogText = d.Find("Panel/Question").GetComponent<Text>();
            dialogPanel = (RectTransform)d.Find("Panel");
            dialogDim = d.GetComponent<Image>();
            dialogDimColor = dialogDim.color;
            d.Find("Panel/Yes").GetComponent<Button>().onClick.AddListener(() =>
            {
                var a = onYes;
                onNo = null;
                CloseDialog();
                if (a != null) a();
            });
            d.Find("Panel/No").GetComponent<Button>().onClick.AddListener(CloseDialog);
            dialog.SetActive(false);
        }
        Bind("A1", 0);
        BindNodeMenu("A2", n => n.behaviour == NodeBehaviour.Processor);   // Refinery, Crusher
        Bind("A3", 2);
        Bind("A4", 3);
        BindNodeMenu("A7", n => n.buildCost != null && n.buildCost.Length > 0);
        BindEdges("A8");
        BindEdgePanel(hudObject != null ? hudObject.transform : null);
        var insp = hudObject != null ? hudObject.transform.Find("Inspector") : null;
        if (insp != null)
        {
            inspector = insp.GetComponent<NodeInspector>();
            if (inspector != null) inspector.Setup(world);
        }
        BindTiles("A5");
        BindSuccess(hudObject != null ? hudObject.transform : null);
        BindPause(hudObject != null ? hudObject.transform : null);

        // B1 opens the resource board and B2 the node board; the dimmed background, the X and Esc close them
        var debugCash = GameObject.Find("DebugCash");
        if (debugCash != null) debugCash.GetComponent<Button>().onClick.AddListener(() => world.state.cash += 1000);
        BindBoard("B1", "InfoBoard");
        BindBoard("B2", "NodeBoard");

        // B6, the last button, starts the current level again on a new map (after a question)
        var b6 = GameObject.Find("B6");
        if (b6 != null) b6.GetComponent<Button>().onClick.AddListener(() => Ask("Reset this level? Everything you built is lost and you start again with €" + NumFmt.Short(Meta.StartCash()) + ".", world.ResetLevel));

        // B3 goes back to the main menu (after a question)
        var b3 = GameObject.Find("B3");
        if (b3 != null) b3.GetComponent<Button>().onClick.AddListener(() => Ask("Back to the main menu?\nYour progress in this level is lost.", GoToMainMenu));

        // A6 opens the whole next ring of "?" tiles at a discount (after a question)
        var a6 = GameObject.Find("A6");
        if (a6 != null)
        {
            ringCost = Label(a6.transform, "Cost");
            a6.GetComponent<Button>().onClick.AddListener(AskRing);
        }

        var auto = GameObject.Find("AutoEdge");
        if (auto != null)
        {
            autoImage = auto.GetComponent<Image>();
            autoImage.color = AutoOff;
            auto.GetComponent<Button>().onClick.AddListener(() => world.autoEdge = !world.autoEdge);
        }
    }

    Text ringCost;

    void AskRing()
    {
        int count = world.NextRingTiles().Count;
        if (count == 0) return;
        world.NextRingCost(out int full, out int cost);
        Ask("Unlock all " + count + " tiles of the next ring for €" + NumFmt.Short(cost) + "?\nOne by one they would cost €" + NumFmt.Short(full) + " (" + Mathf.RoundToInt(World.RingDiscount * 100) + "% off).", world.UnlockNextRing);
    }

    Image autoImage;
    static readonly Color AutoOff = new Color(.17f, .21f, .27f), AutoOn = new Color(.24f, .6f, .45f);

    static Text Label(Transform parent, string child)
    {
        var t = parent.Find(child);
        return t != null ? t.GetComponent<Text>() : null;
    }

    void Bind(string buttonName, int nodeIndex)
    {
        var go = GameObject.Find(buttonName);
        if (go == null || nodeIndex >= world.data.nodes.Count) return;
        var b = go.GetComponent<Button>();
        if (b == null) return;
        var d = world.data.nodes[nodeIndex];

        var costText = go.transform.Find("Cost");
        if (costText != null) costs.Add(new KeyValuePair<Text, NodeDef>(costText.GetComponent<Text>(), d));

        b.onClick.AddListener(() => { tool.Select(d); CloseMenus(); });
        AddSlot(go.GetComponent<Image>(), d);
    }

    // the entries <button>_1, _2, ... are the tile offers of the current level (World.Offers), in that order
    void BindTiles(string buttonName)
    {
        var go = GameObject.Find(buttonName);
        if (go == null) return;
        var menu = go.transform.Find("Menu");
        if (menu == null) return;
        menus.Add(menu.gameObject);
        go.GetComponent<Button>().onClick.AddListener(() => ToggleMenu(menu.gameObject));
        for (int i = 0; ; i++)
        {
            var entry = menu.Find(buttonName + "_" + (i + 1));
            if (entry == null) break;
            int index = i;
            entry.GetComponent<Button>().onClick.AddListener(() =>
            {
                var offers = world.Offers();
                if (index >= offers.Count) return;
                tool.Select(offers[index].def, offers[index].starter);
                CloseMenus();
            });
            var img = entry.GetComponent<Image>();
            var label = entry.Find("Label");
            tileEntries.Add(new TileEntry { go = entry.gameObject, image = img, label = label != null ? label.GetComponent<Text>() : null, normal = img.color });
        }
    }

    // one line of text per fact, so the two kinds of one tile are easy to tell apart
    // A8 is the menu of special edges: Tier, Filter and Chest. Each entry stays greyed out ("locked") until a level introduces it.
    void BindEdges(string buttonName)
    {
        var go = GameObject.Find(buttonName);
        if (go == null) return;
        var menu = go.transform.Find("Menu");
        if (menu == null) return;
        menus.Add(menu.gameObject);
        go.GetComponent<Button>().onClick.AddListener(() => ToggleMenu(menu.gameObject));
        var cost = go.transform.Find("Cost");
        if (cost != null) edgesLabel = cost.GetComponent<Text>();
        for (int i = 0; i < world.data.edges.Count; i++)
        {
            var entry = menu.Find(buttonName + "_" + (i + 1));
            if (entry == null) break;
            var d = world.data.edges[i];
            var button = entry.GetComponent<Button>();
            button.onClick.AddListener(() => { tool.Select(d); CloseMenus(); });
            var img = entry.GetComponent<Image>();
            var label = entry.Find("Label");
            edgeEntries.Add(new EdgeEntry { button = button, image = img, label = label != null ? label.GetComponent<Text>() : null, def = d, normal = img.color });
        }
        // entries beyond the edges that exist are placeholders
        for (int i = world.data.edges.Count; ; i++)
        {
            var entry = menu.Find(buttonName + "_" + (i + 1));
            if (entry == null) break;
            entry.GetComponent<Button>().interactable = false;
        }
    }

    void BindEdgePanel(Transform hud)
    {
        var p = hud != null ? hud.Find("EdgePanel") : null;
        if (p == null) return;
        edgePanel = p.GetComponent<EdgePanel>();
        if (edgePanel == null) return;
        edgePanel.Setup(world);
    }

    void UpdateEdges()
    {
        if (edgesLabel != null)
        {
            edgesLabel.text = world.AnyEdgeUnlocked() ? "" : "locked";
            edgesLabel.color = new Color(.85f, .88f, .92f);
        }
        foreach (var e in edgeEntries)
        {
            bool open = world.EdgeUnlocked(e.def);
            int price = world.EdgeCost(e.def);
            e.button.interactable = open;
            e.image.color = tool.edgeTool == e.def ? new Color(.24f, .6f, .45f) : e.normal;
            if (e.label != null)
                e.label.text = "<b>" + e.def.displayName + "</b>\n" + (open ? "€" + NumFmt.Short(price) + "\n" + world.EdgeOwned(e.def) + " drawn" : "locked");
        }
    }

    // the entries of the node menus (A2, A7): name, price of the next one, and the materials it needs (all on one line, to fit)
    readonly List<KeyValuePair<Text, NodeDef>> buildLabels = new List<KeyValuePair<Text, NodeDef>>();

    void UpdateBuildLabels()
    {
        foreach (var kv in buildLabels)
        {
            var d = kv.Value;
            int price = world.NewCost(d);
            string needs = "";
            foreach (var c in d.buildCost) needs += (needs.Length > 0 ? " + " : "") + c.count + " " + c.item.name;
            kv.Key.text = "<b>" + d.displayName + "</b>\n" + (price > 0 ? "€" + NumFmt.Short(price) + "\n" : "") + needs;
        }
    }

    // a button that opens a menu of the nodes that match: A2 the Processors, A7 the Build Nodes. Entries without a node are placeholders.
    void BindNodeMenu(string buttonName, System.Predicate<NodeDef> filter)
    {
        var go = GameObject.Find(buttonName);
        if (go == null) return;
        var menu = go.transform.Find("Menu");
        if (menu == null) return;
        menus.Add(menu.gameObject);
        go.GetComponent<Button>().onClick.AddListener(() => ToggleMenu(menu.gameObject));
        var list = world.data.nodes.FindAll(filter);
        for (int i = 0; ; i++)
        {
            var entry = menu.Find(buttonName + "_" + (i + 1));
            if (entry == null) break;
            var button = entry.GetComponent<Button>();
            var label = entry.Find("Label");
            if (i >= list.Count) { button.interactable = false; continue; }
            var d = list[i];
            button.onClick.AddListener(() => { tool.Select(d); CloseMenus(); });
            AddSlot(entry.GetComponent<Image>(), d);
            if (label != null) buildLabels.Add(new KeyValuePair<Text, NodeDef>(label.GetComponent<Text>(), d));   // filled in by UpdateBuildLabels, since the price changes
        }
    }

    string Describe(TileOffer o)
    {
        string cost = "€" + NumFmt.Short(world.ConvertCost(o.def, o.starter));
        if (!o.starter) return "<b>" + o.def.name + "</b>\n" + cost + "\nnext to " + o.def.name;
        int limit = Meta.StarterLimit(o.def);
        return "<b>Starter " + o.def.name + "</b>\n" + cost + " anywhere\n" + (limit == 0 ? "needs upgrade" : world.StarterUsed(o.def) + "/" + limit + " placed");
    }

    void AddSlot(Image img, NodeDef d) { slots.Add(new Slot { image = img, def = d, normal = img.color }); }

    void ToggleMenu(GameObject menu)
    {
        bool open = !menu.activeSelf;
        CloseMenus();
        menu.SetActive(open);
    }

    void CloseMenus() { foreach (var m in menus) m.SetActive(false); }

    void GoToMainMenu()
    {
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene(MainMenuScene);
    }

    // Esc menu: the game stands still while it is open
    void BindPause(Transform hud)
    {
        var p = hud != null ? hud.Find("PauseMenu") : null;
        if (p == null) return;
        pause = p.gameObject;
        pause.SetActive(false);
        var panel = p.Find("Panel");
        panel.Find("Resume").GetComponent<Button>().onClick.AddListener(ClosePause);
        panel.Find("MainMenu").GetComponent<Button>().onClick.AddListener(() => Ask("Back to the main menu?\nYour progress in this level is lost.", GoToMainMenu));
        panel.Find("Quit").GetComponent<Button>().onClick.AddListener(() => Ask("Quit the game?", Application.Quit));
    }

    void OpenPause() { pause.SetActive(true); Time.timeScale = 0f; }
    void ClosePause() { pause.SetActive(false); Time.timeScale = 1f; }

    void OnDestroy() { Time.timeScale = 1f; }

    void Update()
    {
        // Esc opens the Esc menu, unless it was needed to close something else or to put the building in hand away
        if (pause != null)
        {
            bool free = !AnyOverlayOpen() && tool.tool == null && tool.tileTool == null && tool.edgeTool == null && tool.escFrame != Time.frameCount;
            if (pause.activeSelf) { if (Input.GetKeyDown(KeyCode.Escape) && !(dialog != null && dialog.activeSelf)) ClosePause(); }
            else if (Input.GetKeyDown(KeyCode.Escape) && free) OpenPause();
        }
        foreach (var s in slots) s.image.color = tool.tool == s.def ? new Color(.24f, .6f, .45f) : s.normal;
        foreach (var c in costs)
        {
            int price = world.NewCost(c.Value);
            bool quest = c.Value.behaviour == NodeBehaviour.Quest;
            c.Key.text = quest ? world.QuestShort : "€" + NumFmt.Short(price);
            c.Key.color = quest || world.state.cash >= price ? new Color(.85f, .88f, .92f) : new Color(1f, .5f, .5f);
        }
        if (ringCost != null)
        {
            bool any = world.NextRingTiles().Count > 0;
            world.NextRingCost(out _, out int rc);
            ringCost.text = any ? "€" + NumFmt.Short(rc) : "all open";
            ringCost.color = !any || world.state.cash >= rc ? new Color(.85f, .88f, .92f) : new Color(1f, .5f, .5f);
        }
        var offers = world.Offers();
        for (int i = 0; i < tileEntries.Count; i++)
        {
            var e = tileEntries[i];
            bool has = i < offers.Count;
            if (e.go.activeSelf != has) e.go.SetActive(has);   // a level that does not offer stone yet hides its entries
            if (!has) continue;
            var o = offers[i];
            if (e.label != null) e.label.text = Describe(o);
            bool on = tool.tileTool == o.def && tool.tileStarter == o.starter;
            e.image.color = on ? new Color(.24f, .6f, .45f) : e.normal;
        }
        if (cashText != null) cashText.text = "€ " + NumFmt.Short(world.state.cash);
        if (timerText != null)
        {
            string tt = world.QuestTimeText;
            timerText.text = tt != null ? "Time left  " + tt : "";
            timerText.color = world.questTimeLeft < 10 ? new Color(1f, .4f, .35f) : Color.white;
        }
        if (dialog != null && dialog.activeSelf && Input.GetKeyDown(KeyCode.Escape)) CloseDialog();
        if (success != null && success.activeSelf)
        {
            AnimateSuccess();
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Return)) successAction();
        }
        if (Input.GetKeyDown(KeyCode.Escape)) foreach (var b in boards) b.SetActive(false);
        if (edgePanel != null && edgePanel.IsOpen && Input.GetKeyDown(KeyCode.Escape)) edgePanel.Close();
        if (inspector != null && inspector.IsOpen && Input.GetKeyDown(KeyCode.Escape)) inspector.Close();
        UpdateEdges();
        UpdateBuildLabels();
        if (autoImage != null) autoImage.color = world.autoEdge ? AutoOn : AutoOff;
        if (Input.GetKeyDown(KeyCode.Escape) || (Input.GetMouseButtonUp(1) && !CameraController.rightDragged)) CloseMenus();
        else if (Input.GetMouseButtonDown(0) && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())) CloseMenus();
    }

    void BindBoard(string buttonName, string boardName)
    {
        var hud = GameObject.Find("HUD");
        var t = hud != null ? hud.transform.Find(boardName) : null;
        var button = GameObject.Find(buttonName);
        if (t == null || button == null) return;
        var board = t.gameObject;
        boards.Add(board);
        board.GetComponent<Button>().onClick.AddListener(() => board.SetActive(false));
        var close = t.Find("Panel/Close");
        if (close != null) close.GetComponent<Button>().onClick.AddListener(() => board.SetActive(false));
        button.GetComponent<Button>().onClick.AddListener(() =>
        {
            bool open = !board.activeSelf;
            foreach (var b in boards) b.SetActive(false);
            board.SetActive(open);
        });
        board.SetActive(false);
    }

    bool AnyBoardOpen()
    {
        foreach (var b in boards) if (b.activeSelf) return true;
        return false;
    }

    bool AnyOverlayOpen()
    {
        if ((dialog != null && dialog.activeSelf) || (success != null && success.activeSelf) || AnyBoardOpen() || (edgePanel != null && edgePanel.IsOpen) || (inspector != null && inspector.IsOpen)) return true;
        foreach (var m in menus) if (m.activeSelf) return true;
        return false;
    }

    public bool IsOver(Vector2 screen)
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
