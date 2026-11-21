using System.Collections.Generic;
using UnityEngine;

// Pixel art from text files: palette lines like "o=#2e1a12", then rows of characters (one character per pixel, '.' is transparent).
// Icons are found by name: Assets/Resources/Data/Icons/<name>.txt (an item's icon is named after the item), so a new icon is one new text file.
public static class PixelArt
{
    static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    // worldSize: how tall the icon is in world units. Pick a whole number of screen pixels per art pixel (16 px at .5 = 3x on a 1080p screen).
    public static Sprite ForItem(ItemDef item, float worldSize) { return item == null ? null : Named(item.name, worldSize); }

    // any picture in the Icons folder by its file name, e.g. "lock"
    public static Sprite Named(string name, float worldSize)
    {
        string key = name + "@" + worldSize;
        if (cache.TryGetValue(key, out var s) && s != null) return s;
        var art = Resources.Load<TextAsset>("Data/Icons/" + name);
        s = art != null ? Parse(art.text, worldSize) : null;
        cache[key] = s;
        return s;
    }

    static Sprite Parse(string text, float worldSize)
    {
        var palette = new Dictionary<char, Color>();
        var rows = new List<string>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length == 0 || line[0] == '#') continue;
            if (line.Length >= 9 && line[1] == '=' && line[2] == '#' && ColorUtility.TryParseHtmlString(line.Substring(2, 7), out var c)) palette[line[0]] = c;
            else rows.Add(line);
        }
        if (rows.Count == 0) return null;

        int h = rows.Count, w = 0;
        foreach (var r in rows) w = Mathf.Max(w, r.Length);
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            string row = rows[h - 1 - y];   // the first text row is the top
            for (int x = 0; x < w; x++)
                px[y * w + x] = x < row.Length && palette.TryGetValue(row[x], out var col) ? col : Color.clear;
        }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(.5f, .5f), h / worldSize);
    }
}
