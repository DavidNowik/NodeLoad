using UnityEngine;

// A thick mist hangs over the island, so at first the edge of the map cannot be seen. Unlocking a tile on the outermost ring pushes it back until the edge shows.
[ExecuteAlways]
public class Mist : MonoBehaviour
{
    public Vector2 clear = new Vector2(11f, 9.6f);        // half size of the area free of mist
    public Vector2 revealAt = new Vector2(17.2f, 13.2f);  // once the clear area reaches this, the mist is gone
    public float soft = 3f;
    public Color color = new Color(.02f, .025f, .04f);
    public bool showInEditor;

    Vector2 target, startClear;
    GameObject vis;
    Mesh mesh;
    Vector2 built = -Vector2.one;

    void OnEnable() { target = startClear = clear; Rebuild(); }

    // a new map: the mist closes in again to where it started
    public void ResetClear()
    {
        clear = target = startClear;
        built = -Vector2.one;
    }

    void OnDisable()
    {
        if (vis == null) return;
        if (Application.isPlaying) Destroy(vis); else DestroyImmediate(vis);
        vis = null;
        built = -Vector2.one;
    }

    public void Expand(Vector2 d) { target += d; }

    void Update()
    {
        if (Application.isPlaying) clear = Vector2.MoveTowards(clear, target, Time.deltaTime * 1.2f);
        Rebuild();
    }

    void Rebuild()
    {
        bool show = (Application.isPlaying || showInEditor) && !(clear.x >= revealAt.x && clear.y >= revealAt.y);
        if (!show)
        {
            if (vis != null) vis.SetActive(false);
            return;
        }
        if (vis == null)
        {
            vis = new GameObject("MistVisuals");
            mesh = new Mesh();
            vis.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = vis.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Gfx.LineMat;
            mr.sortingOrder = Layers.Mist;
            vis.hideFlags = HideFlags.HideAndDontSave;
            built = -Vector2.one;
        }
        vis.SetActive(true);
        if (clear == built) return;
        built = clear;

        float r = Mathf.Min(3f, Mathf.Min(clear.x, clear.y) - .1f);
        var inner = Gfx.RoundedRect(clear, r);
        var mid = Gfx.RoundedRect(clear + Vector2.one * soft, r + soft);
        var outer = Gfx.RoundedRect(clear + Vector2.one * 60f, r + 20f);
        int n = inner.Length;
        var verts = new Vector3[n * 3];
        var cols = new Color[n * 3];
        var clearC = Gfx.Vc(new Color(color.r, color.g, color.b, 0f));
        var thick = Gfx.Vc(new Color(color.r, color.g, color.b, .97f));
        for (int i = 0; i < n; i++)
        {
            verts[i] = inner[i]; cols[i] = clearC;
            verts[n + i] = mid[i]; cols[n + i] = thick;
            verts[2 * n + i] = outer[i]; cols[2 * n + i] = thick;
        }
        var tris = new int[n * 12];
        int k = 0;
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            for (int ring = 0; ring < 2; ring++)
            {
                int a = ring * n + i, b = ring * n + j, c = (ring + 1) * n + j, d = (ring + 1) * n + i;
                tris[k++] = a; tris[k++] = b; tris[k++] = c;
                tris[k++] = a; tris[k++] = c; tris[k++] = d;
            }
        }
        mesh.Clear();
        mesh.vertices = verts;
        mesh.colors = cols;
        mesh.triangles = tris;
    }
}
