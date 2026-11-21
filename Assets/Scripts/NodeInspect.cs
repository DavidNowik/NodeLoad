using System.Collections.Generic;
using UnityEngine;

// One line of the inspection window (see NodeInspector): a heading, a fact, a note, or a meter.
public struct InspectRow
{
    public enum Kind { Header, Pair, Note, Bar }
    public Kind kind;
    public string label, value;
    public float fill, extra;     // Bar: how full it is, and a paler part on top for what is still on its way (0 to 1)
    public int segments;          // Bar: dividers, one per item for small stores
    public Color color;
    public ItemDef item;          // Bar: its picture is shown beside the label

    public static InspectRow Head(string text, Color accent) { return new InspectRow { kind = Kind.Header, label = text, color = accent }; }
    public static InspectRow Pair(string label, string value) { return new InspectRow { kind = Kind.Pair, label = label, value = value }; }
    public static InspectRow Note(string text) { return new InspectRow { kind = Kind.Note, label = text }; }
    public static InspectRow Bar(string label, string value, float fill, Color color, ItemDef item = null, float extra = 0, int segments = 0)
    {
        return new InspectRow { kind = Kind.Bar, label = label, value = value, fill = Mathf.Clamp01(fill), extra = Mathf.Clamp01(extra), color = color, item = item, segments = segments };
    }
}

// What a building tells the inspection window: its state, what it makes, and every store it has as a meter.
public partial class Node
{
    // the palette: one colour per kind of thing, so the same meter always looks the same
    static readonly Color PalWork = new Color(.45f, .88f, .55f);
    static readonly Color PalBuild = new Color(.45f, .65f, 1f);
    static readonly Color PalGoal = new Color(.72f, .6f, 1f);
    static readonly Color PalMoney = new Color(1f, .82f, .3f);
    static readonly Color PalHeat = new Color(1f, .78f, .3f), PalHot = new Color(1f, .36f, .14f);
    static readonly Color PalSteam = new Color(.7f, .86f, 1f);

    public Color InspectColor
    {
        get
        {
            if (Building) return PalBuild;
            if (Seller) return PalMoney;
            if (def.behaviour == NodeBehaviour.Quest) return PalGoal;
            if (Oven) return PalHeat;
            if (Turbine) return PalSteam;
            if (Storage) return stored != null ? stored.color : PalBuild;
            var main = MainItem();
            return main != null ? main.color : PalWork;
        }
    }

    public string InspectState
    {
        get
        {
            if (Building) return "Under construction";
            if (Seller) return "Selling";
            if (def.behaviour == NodeBehaviour.Quest) return "Collecting for the level";
            if (Storage) return stored == null ? (candidate != null ? "Asking what to keep" : "Empty, waiting for an item") : "Keeping " + stored.name;
            if (Turbine) return steamLeft > 0 ? "Running" : heat <= 0 ? "No heat" : "No water";
            if (blocked) return "Blocked: nowhere to send it";
            if (phase == 1) return Verb + "...";
            if (Oven && heat <= 0) return "Cold: needs heat to work";
            if (Oven) return "Hot";
            if (def.behaviour == NodeBehaviour.Extractor) return outs.Count == 0 ? "No output edge" : "Waiting";
            return "Waiting for input";
        }
    }

    static string Num(float v) { return v.ToString("0.##"); }

    // dividers for a meter: one per item for a small store, one per ten for a store of 100
    static int Seg(int max) { return max > 1 && max <= 24 ? max : max % 10 == 0 && max / 10 <= 12 ? 10 : 0; }

    static Color HeatColor(float p) { return Color.Lerp(PalHeat, PalHot, p); }

    // items the node holds that are not part of its own storage plan: relayed on their way somewhere else
    void PassingRows(List<InspectRow> r, HashSet<ItemDef> known)
    {
        bool head = false;
        foreach (var item in new List<ItemDef>(queue.Keys))
        {
            if (known.Contains(item) || Get(queue, item) + Get(inflight, item) == 0) continue;
            if (!head) { r.Add(InspectRow.Head("Passing through", Soft)); head = true; }
            int n = Get(queue, item);
            r.Add(InspectRow.Bar(item.name, n + (Get(inflight, item) > 0 ? "  +" + Get(inflight, item) : ""), (float)n / World.QueueCap, item.color, item, (float)(n + Get(inflight, item)) / World.QueueCap, World.QueueCap));
        }
    }

    void RecipeRows(List<InspectRow> r, RecipeDef rec)
    {
        float time = rec.time / speed;
        r.Add(InspectRow.Pair("Recipe", rec.Describe()));
        r.Add(InspectRow.Pair("Takes", TakesText(rec, time)));
        r.Add(InspectRow.Pair("Makes", Num(rec.outputCount / time) + " " + rec.output.name + "/s"));
    }

    static string TakesText(RecipeDef rec, float time)
    {
        string s = "";
        foreach (var i in rec.inputs) s += (s.Length > 0 ? ", " : "") + Num(i.count / time) + " " + i.item.name + "/s";
        return s;
    }

    public void Inspect(List<InspectRow> r)
    {
        var accent = InspectColor;
        r.Add(InspectRow.Head("State", accent));
        r.Add(InspectRow.Pair("Doing", InspectState));
        if (speed > 1.001f && !Sink) r.Add(InspectRow.Pair("Speed", "x" + Num(speed) + "  (Steam Turbines in reach)"));
        r.Add(InspectRow.Pair("Edges", "in " + InCount() + "  ·  out " + outs.Count));

        if (Building) { BuildRows(r); return; }

        if (def.behaviour == NodeBehaviour.Extractor)
        {
            var yields = ground != null ? ground.yields : null;
            r.Add(InspectRow.Head("Production", accent));
            if (yields == null) r.Add(InspectRow.Note("It stands on nothing it can mine."));
            else
            {
                float cycle = def.cycleTime / speed;
                r.Add(InspectRow.Pair("Mines", yields.name + " from " + ground.name + " tiles"));
                r.Add(InspectRow.Pair("Output", Num(1f / cycle) + " " + yields.name + "/s  (one every " + Num(cycle) + " s)"));
                r.Add(InspectRow.Bar("Next " + yields.name, phase == 1 ? Num(t) + " / " + Num(def.cycleTime) + " s" : "-", phase == 1 ? t / def.cycleTime : 0, PalWork, yields));
                r.Add(InspectRow.Pair("Mined so far", Get(produced, yields).ToString()));
            }
            PassingRows(r, new HashSet<ItemDef>());
        }
        else if (Refinery || Oven)
        {
            r.Add(InspectRow.Head("Production", accent));
            var shown = new HashSet<ItemDef>();
            if (Picks || recipe == null && !Oven)
            {
                r.Add(InspectRow.Note(Picks ? "Takes the input of any of these recipes." : "The first item that arrives decides its one recipe."));
                foreach (var rec in def.recipes) RecipeRows(r, rec);
            }
            else RecipeRows(r, recipe);
            if (recipe != null && !Picks)
            {
                int need = 0;
                foreach (var i in recipe.inputs) need += i.count;
                float batch = phase == 1 ? t / CycleTime : need > 0 ? (float)Total(held) / need : 0;
                string txt = phase == 1 ? Num(t) + " / " + Num(CycleTime) + " s" : Total(held) + " / " + need + " collected";
                r.Add(InspectRow.Bar(Oven && heat <= 0 ? "Batch (paused, no heat)" : "Current batch", txt, batch, PalWork));
            }
            else if (Picks) r.Add(InspectRow.Bar("Current batch", phase == 1 ? Num(t) + " / " + Num(CycleTime) + " s" : "-", phase == 1 ? t / CycleTime : 0, PalWork));
            foreach (var rec in ActiveRecipes()) r.Add(InspectRow.Pair("Made so far", Get(produced, rec.output) + " " + rec.output.name));

            if (Oven) OvenRows(r, shown);
            r.Add(InspectRow.Head("Storage", accent));
            foreach (var rec in ActiveRecipes())
                foreach (var i in rec.inputs)
                {
                    if (!shown.Add(i.item)) continue;
                    int have = Get(queue, i.item) + Get(held, i.item), way = Get(inflight, i.item);
                    int max = Oven ? def.itemSlots : World.QueueCap;
                    r.Add(InspectRow.Bar(i.item.name, have + "/" + max + (way > 0 ? "  +" + way + " coming" : ""), (float)have / max, i.item.color, i.item, (float)(have + way) / max, Seg(max)));
                }
            PassingRows(r, shown);
        }
        else if (Storage)
        {
            r.Add(InspectRow.Head("Chest", accent));
            if (stored != null)
            {
                int n = Get(queue, stored), way = Get(inflight, stored);
                r.Add(InspectRow.Bar(stored.name, n + "/" + def.capacity + (way > 0 ? "  +" + way + " coming" : ""), (float)n / def.capacity, stored.color, stored, (float)(n + way) / def.capacity, Seg(def.capacity)));
                r.Add(InspectRow.Pair("Worth", "€" + NumFmt.Short(n * Meta.SellPrice(stored))));
            }
            else r.Add(InspectRow.Note(candidate != null ? "Waiting for your answer: keep " + candidate.name + "?" : "Takes the first item that arrives, after asking."));
            if (refused.Count > 0)
            {
                string no = "";
                foreach (var i in refused) no += (no.Length > 0 ? ", " : "") + i.name;
                r.Add(InspectRow.Pair("Never keeps", no));
            }
            r.Add(InspectRow.Pair("Hands out", chest != null ? Num(def.outputRate * speed) + "/s to " + chest.to.def.displayName : "no chest edge yet"));
        }
        else if (Turbine) TurbineRows(r);
        else if (def.behaviour == NodeBehaviour.Quest) QuestRows(r);
        else if (Seller)
        {
            r.Add(InspectRow.Head("Market", accent));
            float used = IncomeBudget > 0 ? Mathf.Clamp01((IncomeBudget - income) / IncomeBudget) : 0;
            r.Add(InspectRow.Bar("Earned in this window", "€" + NumFmt.Short(Mathf.Max(0, IncomeBudget - income)) + " / €" + NumFmt.Short(IncomeBudget), used, PalMoney));
            r.Add(InspectRow.Pair("Cap", "€" + Num(IncomeCap) + " per second, over " + Num(def.capWindow) + " s"));
            r.Add(InspectRow.Note("Unused seconds are credited, so a burst is fine. What is over the cap goes to another connected Market, or is lost."));
        }
    }

    int InCount()
    {
        int n = 0;
        foreach (var o in world.nodes) if (o.outs.Contains(this)) n++;
        return n;
    }

    void BuildRows(List<InspectRow> r)
    {
        r.Add(InspectRow.Head("Construction", PalBuild));
        r.Add(InspectRow.Bar("Overall", BuildHave + " / " + BuildTotal, (float)BuildHave / BuildTotal, PalBuild));
        foreach (var c in def.buildCost)
        {
            int have = Mathf.Min(c.count, Get(progress, c.item));
            r.Add(InspectRow.Bar(c.item.name, have + "/" + c.count, (float)have / c.count, c.item.color, c.item, 0, Seg(c.count)));
        }
        r.Add(InspectRow.Note("Deliver these items to it over an edge; it works once all are in."));
    }

    void OvenRows(List<InspectRow> r, HashSet<ItemDef> shown)
    {
        r.Add(InspectRow.Head("Heat", PalHeat));
        float p = HeatFraction;
        r.Add(InspectRow.Bar("Heat meter", Mathf.RoundToInt(p * def.maxHeat) + " / " + Mathf.RoundToInt(def.maxHeat), p, HeatColor(p), null, 0, 10));
        r.Add(InspectRow.Pair("Cools by itself", Num(def.burnRate) + " per second"));
        int fuel = 0;
        foreach (var kv in queue) if (kv.Key.burnWeight > 0) fuel += kv.Value;
        r.Add(InspectRow.Bar("Fuel slots", fuel + " / " + def.itemSlots, (float)fuel / def.itemSlots, PalHeat, null, 0, def.itemSlots));
        foreach (var kv in queue)
        {
            if (kv.Key.burnWeight <= 0 || kv.Value == 0) continue;
            shown.Add(kv.Key);
            r.Add(InspectRow.Bar(kv.Key.name + "  (burns " + Num(kv.Key.burnWeight) + " each)", kv.Value + " waiting", (float)kv.Value / def.itemSlots, kv.Key.color, kv.Key, 0, def.itemSlots));
        }
        int links = 0;
        foreach (var l in world.links) if (l.IsHeat && l.from == this) links++;
        r.Add(InspectRow.Pair("Heat edges", links + " to Steam Turbines"));
    }

    void TurbineRows(List<InspectRow> r)
    {
        r.Add(InspectRow.Head("Steam", PalSteam));
        float p = HeatFraction;
        r.Add(InspectRow.Bar("Heat from Ovens", Mathf.RoundToInt(p * def.maxHeat) + " / " + Mathf.RoundToInt(def.maxHeat), p, HeatColor(p), null, 0, 10));
        if (def.feedItem != null)
        {
            int w = Get(queue, def.feedItem), way = Get(inflight, def.feedItem);
            r.Add(InspectRow.Bar(def.feedItem.name, w + "/" + def.itemSlots + (way > 0 ? "  +" + way + " coming" : ""), (float)w / def.itemSlots, def.feedItem.color, def.feedItem, (float)(w + way) / def.itemSlots, def.itemSlots));
        }
        float cycle = 1f / def.feedRate;
        r.Add(InspectRow.Bar("Steam in the boiler", steamLeft > 0 ? Num(steamLeft) + " s left" : "empty", steamLeft / cycle, PalSteam));
        int ovens = world.HeatSources(this).Count, boosted = 0;
        foreach (var n in world.nodes) if (n.Boostable && Vector2.Distance(n.transform.position, transform.position) <= def.effectRadius) boosted++;
        r.Add(InspectRow.Head("Effect", PalSteam));
        r.Add(InspectRow.Pair("Boost now", "x" + Num(Boost) + "  (x1 cold, x" + Num(def.maxBoost) + " at full heat)"));
        r.Add(InspectRow.Pair("Draws", Num(def.heatDraw) + " heat/s and " + Num(def.feedRate) + " " + (def.feedItem != null ? def.feedItem.name : "water") + "/s while it works"));
        r.Add(InspectRow.Pair("Linked Ovens", ovens.ToString()));
        r.Add(InspectRow.Pair("Reach", Num(def.effectRadius) + " units, " + boosted + " buildings inside"));
        r.Add(InspectRow.Note("Several Turbines multiply: x2 and x2 is x4."));
    }

    void QuestRows(List<InspectRow> r)
    {
        var goal = world.Goal;
        r.Add(InspectRow.Head("Goals", PalGoal));
        if (goal == null || goal.goals == null) { r.Add(InspectRow.Note("No level goal.")); return; }
        foreach (var g in goal.goals)
        {
            int have = Mathf.Min(g.amount, world.state.DeliveredOf(g.item.name));
            r.Add(InspectRow.Bar(g.item.name, have + " / " + g.amount, (float)have / g.amount, g.item.color, g.item, 0, Seg(g.amount)));
        }
        if (world.QuestTimeText != null) r.Add(InspectRow.Pair("Time left", world.QuestTimeText));
    }
}
