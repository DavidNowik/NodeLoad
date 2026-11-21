using UnityEngine;

// Mouse input: place buildings, turn standard tiles into special ones, unlock "?" tiles, trigger / link / move / cut / sell buildings,
// click a line to remove it.
public class BuildTool : MonoBehaviour
{
    public World world;
    public NodeDef tool;       // building being placed
    public TileDef tileTool;   // tile type to turn standard tiles into (stays selected so several can be changed)
    public bool tileStarter;   // the limited, pricier "starter unique" version that needs no adjacent tile
    public EdgeDef edgeTool;   // special edge drawn by dragging between buildings (stays selected)

    Node dragFrom;
    GameObject ghost;
    Mesh ghostMesh;
    TextMesh ghostText;
    LineRenderer rangeRing, reachRing;
    float reachShown;
    GameObject tileGhost;
    Mesh tileGhostMesh;
    TextMesh tileGhostText;
    LineRenderer dragLine, holdRing, moveRing;

    public void Select(NodeDef d)
    {
        tool = tool == d ? null : d;
        tileTool = null;
        edgeTool = null;
    }

    public void Select(TileDef t, bool starter)
    {
        bool same = tileTool == t && tileStarter == starter;
        tileTool = same ? null : t;
        tileStarter = starter;
        tool = null;
        edgeTool = null;
    }

    public void Select(EdgeDef e)
    {
        edgeTool = edgeTool == e ? null : e;
        tool = null;
        tileTool = null;
    }

    void Start()
    {
        ghost = Gfx.Hex(transform, "Ghost", World.NodeRadius, Color.white, Layers.Ghost);
        ghostMesh = ghost.GetComponent<MeshFilter>().sharedMesh;
        ghostText = Gfx.Text(ghost.transform, "Label", new Vector2(0, 1.2f), .22f, Color.white, Layers.Ghost + 1);
        // the reach of automatic edges, shown around the ghost while "auto edge" is on
        rangeRing = Gfx.Line(ghost.transform, "Range", new Color(1f, 1f, 1f, .4f), .05f, Layers.Ghost - 1, false);
        rangeRing.loop = true;
        rangeRing.positionCount = 64;
        for (int i = 0; i < 64; i++)
        {
            float a = i * Mathf.PI * 2 / 64;
            rangeRing.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * World.MaxLinkRange);
        }
        // how far a Steam Turbine reaches, shown around the ghost while one is placed or carried
        reachRing = Gfx.Line(ghost.transform, "Reach", new Color(1f, .55f, .2f, .75f), .07f, Layers.Ghost - 1, false);
        reachRing.loop = true;
        reachRing.positionCount = 72;
        reachRing.enabled = false;
        ghost.SetActive(false);
        tileGhost = Gfx.Hex(transform, "TileGhost", Tile.Radius, Color.white, Layers.TileGhost);
        tileGhostMesh = tileGhost.GetComponent<MeshFilter>().sharedMesh;
        tileGhostText = Gfx.Text(tileGhost.transform, "Label", new Vector2(0, 1.2f), .22f, Color.white, Layers.Ghost + 1);
        tileGhost.SetActive(false);
        dragLine = Gfx.Line(transform, "Drag", Color.white, .06f, Layers.Ghost, true);
        dragLine.positionCount = 2;
        dragLine.enabled = false;
        holdRing = Gfx.Line(transform, "Hold", new Color(1f, .4f, .35f), .12f, Layers.Ghost, false);
        holdRing.enabled = false;
        moveRing = Gfx.Line(transform, "Move", new Color(.45f, .75f, 1f), .14f, Layers.Ghost, false);
        moveRing.enabled = false;
    }

    // the edge drawn from this node: the selected special edge, or for a finished Storage (which can only send over a chest edge) the Chest edge
    EdgeDef BrushFor(Node from, Node to)
    {
        if (from.IsChest) return world.data.chestEdge;   // whatever is selected: a chest can only send over its chest edge
        if (from.IsOven && (to == null || to.IsTurbine)) return world.data.heatEdge;   // an Oven sends heat to a Turbine; anywhere else its glass goes over a normal edge
        return edgeTool;
    }

    // a Steam Turbine's ghost shows the circle its boost reaches; any other building hides it
    void ShowReach(NodeDef d)
    {
        bool on = d.behaviour == NodeBehaviour.Turbine;
        reachRing.enabled = on;
        if (!on || Mathf.Approximately(reachShown, d.effectRadius)) return;
        reachShown = d.effectRadius;
        for (int i = 0; i < 72; i++)
        {
            float a = i * Mathf.PI * 2 / 72;
            reachRing.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * d.effectRadius);
        }
    }

    void ShowTile(Tile t, bool ok, string label)
    {
        tileGhost.SetActive(true);
        tileGhost.transform.position = t.transform.position;
        Gfx.Tint(tileGhostMesh, ok ? new Color(.3f, .9f, .5f, .4f) : new Color(1f, .3f, .3f, .4f));
        tileGhostText.text = label;
    }

    // left button on a node: a click shorter than DragTime triggers it, held longer it draws an edge,
    // held still for MoveTime it is picked up and moves with the mouse
    // right button on a node: held for CutTime cuts its edges (snapped neighbours stay), held for SellTime sells it
    const float DoubleClickTime = .3f;
    Tile pendingTile;    // a "?" tile that was clicked once and waits to see if a second click follows
    float pendingT;
    public int escFrame = -1;   // the frame in which Esc put the building in hand away, so the Esc menu does not open as well

    const float DragTime = .2f, MoveTime = 1f, CutTime = .5f, SellTime = 1f;
    Node holdNode;
    float holdT;
    bool holdCut;
    Vector2 pressPos;     // where the left button went down on a node
    float pressT;
    bool pressMoved;
    bool moving;          // the pressed node is picked up

    void HoldToCut(Vector2 m, bool overHud)
    {
        if (Input.GetMouseButtonDown(1))
        {
            holdNode = overHud || tool != null || tileTool != null ? null : world.NodeAt(m);
            holdT = 0;
            holdCut = false;
        }
        // a short right click (no hold, no camera drag) opens the inspection window of that building
        if (Input.GetMouseButtonUp(1) && holdNode != null && holdT < CutTime && !CameraController.rightDragged && world.hud.inspector != null) world.hud.inspector.Open(holdNode);
        bool holding = Input.GetMouseButton(1) && holdNode != null && !CameraController.rightDragged;
        if (holding)
        {
            holdT += Time.deltaTime;
            if (!holdCut && holdT >= CutTime)
            {
                world.ClearEdges(holdNode, true);
                holdNode.ResetChoice();
                holdCut = true;
            }
            if (holdT >= SellTime)
            {
                world.DeleteNode(holdNode);
                holdNode = null;
                holding = false;
            }
        }
        holdRing.enabled = holding;
        if (holding)
        {
            holdRing.transform.position = holdNode.transform.position;
            holdRing.startColor = holdRing.endColor = holdCut ? new Color(.9f, .15f, .15f) : new Color(1f, .75f, .35f);   // amber: edges next, red: sell next
            Gfx.HexArc(holdRing, World.NodeRadius + .12f, holdT / SellTime);
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && (tool != null || edgeTool != null || tileTool != null)) escFrame = Time.frameCount;
        if (Input.GetKeyDown(KeyCode.Escape) || (Input.GetMouseButtonUp(1) && !CameraController.rightDragged && !holdCut)) { tool = null; tileTool = null; edgeTool = null; dragFrom = null; moving = false; }

        if (pendingTile != null && (tool != null || tileTool != null)) pendingTile = null;
        if (pendingTile != null && Time.time - pendingT > DoubleClickTime)
        {
            var tile = pendingTile;
            pendingTile = null;
            world.hud.Ask("Do you want to unlock this tile for €" + NumFmt.Short(world.UnlockCost()) + "?", () => world.Unlock(tile));
        }

        if (world.hud.DialogOpen) return;

        Vector3 w = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 m = new Vector2(w.x, w.y);
        bool overHud = world.hud.IsOver(Input.mousePosition);
        HoldToCut(m, overHud);
        var hover = overHud ? null : world.TileAt(m);

        tileGhost.SetActive(false);

        // converting a standard tile: the hovered tile lights up green when the change is allowed, red when not (the label says why)
        if (tileTool != null)
        {
            if (hover != null)
            {
                bool ok = world.CanConvert(hover, tileTool, tileStarter, out string why);
                string cost = "€" + NumFmt.Short(world.ConvertCost(tileTool, tileStarter));
                ShowTile(hover, ok, tileTool.name + (tileStarter ? " (starter)" : "") + " " + cost + (ok ? "" : "\n" + why));
                if (Input.GetMouseButtonDown(0) && ok) world.Convert(hover, tileTool, tileStarter);
            }
            dragLine.enabled = false;
            return;
        }

        ghost.SetActive(tool != null && !overHud);
        if (tool != null)
        {
            Vector2 at = world.PlaceAt(tool, m);
            var g = world.GroundFor(tool, at);
            bool ok = world.CanPlace(tool, at) && world.state.cash >= world.NewCost(tool);
            ghost.transform.position = at;
            rangeRing.enabled = world.autoEdge;
            ShowReach(tool);
            Gfx.Tint(ghostMesh, ok ? new Color(.24f, .86f, .59f, .6f) : new Color(1f, .36f, .36f, .6f));
            string extra = tool.behaviour == NodeBehaviour.Extractor ? " (" + (g != null && g.yields != null ? g.yields.name : "needs a forest or stone tile") + ")" : g == null || !g.hosts ? " (needs a grass tile)" : "";
            if (world.TouchesLocked(at)) extra = " (reaches over a locked tile)";
            else if (world.Overlaps(tool, at)) extra = " (too close, keep a gap)";
            else if (world.EdgeRunsThrough(at)) extra = " (an edge runs through here)";
            ghostText.text = tool.displayName + extra;
            if (Input.GetMouseButtonDown(0) && !overHud && ok)
            {
                world.Place(tool, at);
                // Miners and Processors stay in hand while there is money for another one (Esc or right click puts it away)
                bool repeat = tool.behaviour == NodeBehaviour.Extractor || tool.behaviour == NodeBehaviour.Processor;
                if (!repeat || world.state.cash < world.NewCost(tool)) tool = null;
            }
            return;
        }

        // nothing selected: hovering a "?" tile shows what unlocking it costs, clicking unlocks it
        if (hover != null && hover.locked && world.NodeAt(m) == null)
        {
            bool ok = world.CanUnlock(hover, out string why);
            ShowTile(hover, ok, "Unlock €" + NumFmt.Short(world.UnlockCost()) + (ok ? "" : "\n" + why));
            if (Input.GetMouseButtonDown(0) && ok)
            {
                // a second click on the same tile within the double-click time unlocks it at once; otherwise the question comes after the wait
                if (pendingTile == hover) { world.Unlock(hover); pendingTile = null; }
                else { pendingTile = hover; pendingT = Time.time; }
            }
        }

        if (Input.GetMouseButtonDown(0) && !overHud)
        {
            var n = world.NodeAt(m);
            if (n != null)
            {
                dragFrom = n;
                pressPos = m;
                pressT = 0;
                pressMoved = false;
            }
            else
            {
                // the badge of a special edge opens its settings; any other click on an edge removes it
                var badge = world.BadgeAt(m);
                if (badge != null && world.hud.edgePanel != null) world.hud.edgePanel.Open(badge);
                else
                {
                    var l = world.LinkAt(m, .15f);
                    if (l != null) world.Remove(l);
                }
            }
        }

        // held still on a node: a ring fills up, and when it is full the node is picked up
        moveRing.enabled = false;
        if (dragFrom != null && Input.GetMouseButton(0) && !pressMoved && !moving)
        {
            if (Vector2.Distance(m, pressPos) > .2f) pressMoved = true;
            if (pressMoved) pressT = Mathf.Max(pressT, DragTime);
            else
            {
                pressT += Time.deltaTime;
                if (pressT > DragTime)
                {
                    moveRing.enabled = true;
                    moveRing.transform.position = dragFrom.transform.position;
                    Gfx.HexArc(moveRing, World.NodeRadius + .12f, (pressT - DragTime) / (MoveTime - DragTime));
                }
                if (pressT >= MoveTime) moving = true;
            }
        }

        // carrying a node: it snaps and is checked like a new building, and is dropped on release
        if (moving)
        {
            dragLine.enabled = false;
            Vector2 at = world.PlaceAt(dragFrom.def, m, dragFrom);
            bool ok = world.CanPlace(dragFrom.def, at, dragFrom);
            ghost.SetActive(true);
            ghost.transform.position = at;
            rangeRing.enabled = false;
            ShowReach(dragFrom.def);
            Gfx.Tint(ghostMesh, ok ? new Color(.24f, .86f, .59f, .6f) : new Color(1f, .36f, .36f, .6f));
            ghostText.text = dragFrom.def.displayName + (ok ? "  (edges are cut when it moves)" : "  (cannot go here)");
            if (Input.GetMouseButtonUp(0))
            {
                if (ok) world.MoveNode(dragFrom, at);
                ghost.SetActive(false);
                dragFrom = null;
                moving = false;
            }
            return;
        }

        // an edge is only drawn once the button has been held for DragTime; a quicker click triggers the building once instead
        if (dragFrom != null)
        {
            dragLine.enabled = pressT >= DragTime;
            dragLine.SetPosition(0, dragFrom.transform.position);
            dragLine.SetPosition(1, m);
            var over = world.NodeAt(m);
            var brush = BrushFor(dragFrom, over);
            bool chestEdge = brush != null && (brush.kind == EdgeKind.Chest || brush.kind == EdgeKind.Heat);   // chest and heat edges have no range and no collision
            bool far = !chestEdge && over != null && over != dragFrom && !(world.CanLink(dragFrom, over) || World.Adjacent(dragFrom, over));
            dragLine.startColor = dragLine.endColor = far ? new Color(1f, .35f, .35f) : brush != null ? brush.color : Color.white;   // red: too far, or it would cross a building or another edge
            if (Input.GetMouseButtonUp(0))
            {
                var target = world.NodeAt(m);
                if (pressT < DragTime) world.Trigger(dragFrom);
                else if (target != null && target != dragFrom)
                {
                    if (brush == null)
                    {
                        if (dragFrom.def.IsSink) FloatText.Spawn(target.transform.position + Vector3.up, "nothing leaves a " + dragFrom.def.displayName, new Color(1f, .5f, .45f));
                        else world.Toggle(dragFrom, target);
                    }
                    else
                    {
                        var link = world.AddSpecial(dragFrom, target, brush, out string why);
                        if (link == null && why != null) FloatText.Spawn(target.transform.position + Vector3.up, why, new Color(1f, .5f, .45f));
                        else if (link != null && link.HasBadge && link.def.kind != EdgeKind.Chest && world.hud.edgePanel != null) world.hud.edgePanel.Open(link);   // set it up right away
                    }
                }
                dragFrom = null;
                dragLine.enabled = false;
            }
        }
    }
}
