using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Creates the data assets (items, recipes, tiles, buildings, levels) and the pre-built hexagon map in the open scene as a "Tiles" object.
public static class CreateMapMenu
{
    const string Dir = "Assets/Resources/Data";

    public static void EnsureTiles()
    {
        // the whole map is pre-built: 6 rings of standard tiles around the centre (127). The one starting forest is picked at random when you press Play.
        var existing = GameObject.Find("Tiles");
        if (existing != null && existing.transform.childCount == 127) return;
        if (existing != null) Undo.DestroyObjectImmediate(existing);

        var grass = AssetDatabase.LoadAssetAtPath<TileDef>(Dir + "/Tiles/grass.asset");
        var root = new GameObject("Tiles");
        Undo.RegisterCreatedObjectUndo(root, "Create Tiles");
        const int rings = 6;
        for (int q = -rings; q <= rings; q++)
            for (int r = -rings; r <= rings; r++)
            {
                if (Mathf.Max(Mathf.Abs(q), Mathf.Abs(r), Mathf.Abs(q + r)) > rings) continue;
                var go = new GameObject("Tile " + q + "," + r);
                go.transform.SetParent(root.transform, false);
                go.transform.position = new Vector3(q + r * .5f, r * .866f, 0) * Tile.Step;
                go.AddComponent<Tile>().def = grass;
            }
        EditorSceneManager.MarkSceneDirty(root.scene);
    }

    // creates the item, recipe, tile and building assets that do not exist yet
    public static void CreateData()
    {
        var wood = Save(ItemDef.Make("wood", new Color(.69f, .48f, .24f), 1f), "Items");
        var stone = Save(ItemDef.Make("stone", new Color(.73f, .76f, .8f), 1f), "Items");
        var plank = Save(ItemDef.Make("plank", new Color(.91f, .7f, .42f), 5f), "Items");
        var cutStone = Save(ItemDef.Make("cut stone", new Color(.87f, .91f, .93f), 6f), "Items");
        var water = Save(ItemDef.Make("water", new Color(.31f, .64f, .91f), 1f), "Items");   // a raw resource like wood and stone
        // what an Oven can burn: wood 2, planks 5
        wood.burnWeight = 2;
        plank.burnWeight = 5;
        EditorUtility.SetDirty(wood);
        EditorUtility.SetDirty(plank);
        var sand = Save(ItemDef.Make("sand", new Color(.93f, .82f, .52f), 3f), "Items");     // crushed stone or cut stone
        var glass = Save(ItemDef.Make("glass", new Color(.68f, .9f, .95f), 15f), "Items");   // sand worked in an active Oven
        // tiers: raw items 0, refined once 1, refined twice 2 (a tier edge lets only up to its tier through)
        foreach (var refined in new[] { plank, cutStone, sand })
        {
            refined.tier = 1;
            EditorUtility.SetDirty(refined);
        }
        glass.tier = 2;
        EditorUtility.SetDirty(glass);

        // the special edges; levels introduce them (LevelDef.unlocksEdges), so none is available until a level lists it
        Save(EdgeDef.Make("Tier Edge", EdgeKind.Tier, 15, 1.3f, new Color(.55f, .78f, 1f)), "Edges");
        Save(EdgeDef.Make("Filter Edge", EdgeKind.Filter, 20, 1.3f, new Color(.7f, .9f, .5f)), "Edges");
        Save(EdgeDef.Make("Chest Edge", EdgeKind.Chest, 0, 1f, new Color(1f, .8f, .3f)), "Edges");
        Save(EdgeDef.Make("Heat Edge", EdgeKind.Heat, 0, 1f, new Color(1f, .5f, .2f)), "Edges");   // dragged out of an Oven into a Steam Turbine

        var grass = Save(TileDef.Make("grass", new Color(.5f, .76f, .42f), null, "", TileDecor.Tufts, true, 10), "Tiles");
        if (!grass.isBase)
        {
            grass.isBase = true;
            EditorUtility.SetDirty(grass);
        }
        var forest = Save(TileDef.Make("forest", new Color(.24f, .52f, .3f), wood, "Chopping", TileDecor.Tree, false, 30), "Tiles");
        var rock = Save(TileDef.Make("stone", new Color(.64f, .68f, .74f), stone, "Quarrying", TileDecor.Rock, false, 60), "Tiles");
        var pond = Save(TileDef.Make("water", new Color(.27f, .55f, .86f), water, "Pumping", TileDecor.Ripples, false, 40), "Tiles");

        var saw = Save(RecipeDef.Make(new[] { new Ingredient(wood, 3) }, plank, 2f, "Sawing"), "Recipes");
        var cut = Save(RecipeDef.Make(new[] { new Ingredient(stone, 2) }, cutStone, 1.5f, "Cutting"), "Recipes");

        var crushStone = Save(RecipeDef.Make(new[] { new Ingredient(stone, 1) }, sand, 1f, "Crushing"), "Recipes");
        var crushCut = Save(RecipeDef.Make(new[] { new Ingredient(cutStone, 1) }, sand, 1f, "Crushing cut"), "Recipes");   // the asset needs its own name
        crushCut.verb = "Crushing";   // but the Crusher shows the same word for both
        EditorUtility.SetDirty(crushCut);
        var smelt = Save(RecipeDef.Make(new[] { new Ingredient(sand, 5) }, glass, 2.5f, "Smelting"), "Recipes");
        // 1 stone or cut stone -> 2 sand per second, and an Oven eats 5 sand per 2.5 s = 2 sand per second: one Crusher keeps one Oven exactly busy
        foreach (var crush in new[] { crushStone, crushCut }) { crush.outputCount = 2; crush.time = 1f; EditorUtility.SetDirty(crush); }
        smelt.inputs = new[] { new Ingredient(sand, 5) };
        smelt.time = 2.5f;
        EditorUtility.SetDirty(smelt);

        Save(NodeDef.Make("Miner", NodeBehaviour.Extractor, 10, 1f, null), "Nodes");
        var refinery = Save(NodeDef.Make("Refinery", NodeBehaviour.Processor, 25, 0f, new[] { saw, cut }), "Nodes");
        EditorUtility.SetDirty(refinery);
        // the Crusher takes stone or cut stone, no matter which, and makes 2 sand in 1 s
        var crusher = Save(NodeDef.Make("Crusher", NodeBehaviour.Processor, 50, 0f, new[] { crushStone, crushCut }), "Nodes");
        crusher.baseCost = 50;
        crusher.anyRecipe = true;
        EditorUtility.SetDirty(crusher);
        var market = NodeDef.Make("Market", NodeBehaviour.Seller, 1000, 1.5f, null);
        market.costGrowth = 10f;
        market.firstFree = true;
        var marketAsset = Save(market, "Nodes");
        marketAsset.capWindow = 3f;   // the income cap is checked over 3 seconds (unused seconds are credited)
        EditorUtility.SetDirty(marketAsset);
        // a Build Node: placed for free, then it needs 10 planks and 10 cut stone delivered before it works
        var storage = Save(NodeDef.Make("Storage", NodeBehaviour.Storage, 0, 0f, null), "Nodes");
        storage.baseCost = 0;
        storage.capacity = 100;
        storage.buildCost = new[] { new Ingredient(plank, 10), new Ingredient(cutStone, 10) };
        EditorUtility.SetDirty(storage);
        // Build Nodes for the heat: each costs 100 and needs 10 cut stone delivered after it is placed
        var oven = Save(NodeDef.Make("Oven", NodeBehaviour.Oven, 100, 0f, null), "Nodes");
        oven.baseCost = 100;
        oven.buildCost = new[] { new Ingredient(cutStone, 10) };
        oven.recipes = new[] { smelt };   // while it has heat it turns sand into glass
        EditorUtility.SetDirty(oven);
        var turbine = Save(NodeDef.Make("Steam Turbine", NodeBehaviour.Turbine, 100, 0f, null), "Nodes");
        turbine.baseCost = 100;
        turbine.buildCost = new[] { new Ingredient(cutStone, 10) };
        turbine.feedItem = water;
        EditorUtility.SetDirty(turbine);
        var quest = NodeDef.Make("Quest", NodeBehaviour.Quest, 0, 0f, null);
        quest.maxOwned = 1;
        Save(quest, "Nodes");

        // every level has its own map: level 1 has a forest in the starting area, level 2 adds a stone tile hidden under the "?" tiles
        var forestSpawn = new TileSpawn { tile = forest, count = 1 };
        var hiddenStone = new TileSpawn { tile = rock, count = 1, hidden = true };
        SaveLevel(LevelDef.Make("Level 01", new[] { new LevelGoal(plank, 100) }, forestSpawn));
        SaveLevel(LevelDef.Make("Level 02", new[] { new LevelGoal(cutStone, 100) }, forestSpawn, hiddenStone));
        // Level 03: 100 cut stone within one minute of placing the Quest node. Too fast to cut it live, so it forces a Storage with a chest edge.
        var level3 = SaveLevel(LevelDef.Make("Level 03", new[] { new LevelGoal(cutStone, 100) }, forestSpawn, hiddenStone));
        level3.timeLimit = 60;
        level3.unlocksEdges = new EdgeDef[0];   // the chest edge comes with the Storage; Tier and Filter edges are for a later level
        EditorUtility.SetDirty(level3);
        // Level 04: more room in the middle (3 rings open), the stone tile hidden in the first ring of "?" tiles, 300 planks and 300 cut stone within a minute
        var level4 = SaveLevel(LevelDef.Make("Level 04", new[] { new LevelGoal(plank, 300), new LevelGoal(cutStone, 300) }, forestSpawn,
            new TileSpawn { tile = rock, count = 1, hidden = true, lockedRing = 1 }));
        level4.timeLimit = 60;
        level4.openRings = 3.5f;
        EditorUtility.SetDirty(level4);
        // Level 05: 5000 wood, no time limit. Cut stone for an Oven and a Steam Turbine, water for the turbine: all three tiles are in the starting area
        var level5 = SaveLevel(LevelDef.Make("Level 05", new[] { new LevelGoal(wood, 5000) }, forestSpawn,
            new TileSpawn { tile = rock, count = 1 }, new TileSpawn { tile = pond, count = 1 }));
        level5.openRings = 3.5f;   // from level 4 on the map starts with more room: 3 open rings
        EditorUtility.SetDirty(level5);
        // Level 06: 100 sand (a Crusher on stone or cut stone). Level 07: 100 glass (an Oven with heat, fed with sand). Stone is in the starting area, no time limit.
        var stoneSpawn = new TileSpawn { tile = rock, count = 1 };
        var level6 = SaveLevel(LevelDef.Make("Level 06", new[] { new LevelGoal(sand, 100) }, forestSpawn, stoneSpawn));
        level6.openRings = 3.5f;
        EditorUtility.SetDirty(level6);
        var level7 = SaveLevel(LevelDef.Make("Level 07", new[] { new LevelGoal(glass, 100) }, forestSpawn, stoneSpawn));
        level7.openRings = 3.5f;
        EditorUtility.SetDirty(level7);
        // Level 08: 1000 cut stone in 6 minutes, and the clock runs from the moment the level loads. Two stone tiles and water (for Turbines) in the starting area.
        var level8 = SaveLevel(LevelDef.Make("Level 08", new[] { new LevelGoal(cutStone, 1000) }, forestSpawn,
            new TileSpawn { tile = rock, count = 2 }, new TileSpawn { tile = pond, count = 1 }));
        level8.goals = new[] { new LevelGoal(cutStone, 1000) };   // set here too, so an existing asset gets the new numbers
        level8.timeLimit = 360;
        level8.clockAtStart = true;
        level8.openRings = 3.5f;
        EditorUtility.SetDirty(level8);
        AssetDatabase.SaveAssets();
    }

    // like Save, but a level that exists from before the goals list (it only had one item and amount) gets its goals from here
    static LevelDef SaveLevel(LevelDef fresh)
    {
        var goals = fresh.goals;
        var level = Save(fresh, "Levels");
        if (level.goals == null || level.goals.Length == 0)
        {
            level.goals = goals;
            EditorUtility.SetDirty(level);
        }
        return level;
    }

    // an existing asset is kept (so your edits to it survive); otherwise the new one is saved
    public static T Save<T>(T obj, string folder) where T : ScriptableObject
    {
        string dir = Dir + "/" + folder;
        EnsureFolder(dir);
        string path = dir + "/" + obj.name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null)
        {
            Object.DestroyImmediate(obj);
            return existing;
        }
        AssetDatabase.CreateAsset(obj, path);
        return obj;
    }

    static void EnsureFolder(string path)
    {
        string parent = "Assets";
        foreach (var part in path.Substring("Assets/".Length).Split('/'))
        {
            string next = parent + "/" + part;
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(parent, part);
            parent = next;
        }
    }
}
