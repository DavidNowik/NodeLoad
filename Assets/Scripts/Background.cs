using UnityEngine;

// The ground: a rounded island with a soft shadow, a cream edge and a faint hexagon grid. Water is the camera background.
[ExecuteAlways]
public class Background : MonoBehaviour
{
    public Vector2 size = new Vector2(30, 24);
    public float corner = 2.5f;
    public float hexSize = .7f;
    public Color water = new Color(.55f, .78f, .86f);
    public Color grass = new Color(.36f, .58f, .36f);
    public Color edge = new Color(.96f, .9f, .74f);

    GameObject vis;
    Texture2D tex;
    int sig;

    void OnEnable()
    {
        Rebuild();
        if (Application.isPlaying && Camera.main != null) Camera.main.backgroundColor = water;
    }

    void OnDisable()
    {
        if (vis != null)
        {
            if (Application.isPlaying) Destroy(vis); else DestroyImmediate(vis);
            vis = null;
        }
        sig = 0;
    }

    void Update() { if (!Application.isPlaying) Rebuild(); }

    static Mesh Fan(Vector2[] poly, Color c)
    {
        int n = poly.Length;
        var verts = new Vector3[n + 1];
        var cols = new Color[n + 1];
        var tris = new int[n * 3];
        c = Gfx.Vc(c);
        for (int i = 0; i < n; i++)
        {
            verts[i + 1] = poly[i];
            cols[i + 1] = c;
            tris[i * 3] = 0;
            tris[i * 3 + 1] = (i + 1) % n + 1;
            tris[i * 3 + 2] = i + 1;
        }
        cols[0] = c;
        return new Mesh { vertices = verts, colors = cols, triangles = tris };
    }

    GameObject Layer(string name, Mesh mesh, Material mat, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(vis.transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.sortingOrder = order;
        return go;
    }

    // one repeating tile that holds two rows of pointy-top hexagons (hex radius 1)
    static Texture2D HexTile()
    {
        const int W = 128, H = 222;
        var t = new Texture2D(W, H, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
        var px = new Color32[W * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float wx = (x + .5f) / W * 1.7320508f, wy = (y + .5f) / H * 3f;
                float d1 = 1e9f, d2 = 1e9f;
                for (int j = -1; j <= 3; j++)
                    for (int i = -1; i <= 2; i++)
                    {
                        float cx = i * 1.7320508f + ((j & 1) != 0 ? .8660254f : 0f), cy = j * 1.5f;
                        float d = Mathf.Sqrt((wx - cx) * (wx - cx) + (wy - cy) * (wy - cy));
                        if (d < d1) { d2 = d1; d1 = d; } else if (d < d2) d2 = d;
                    }
                float line = Mathf.Clamp01(1f - (d2 - d1) / .07f);
                px[y * W + x] = new Color32(255, 255, 255, (byte)(line * 255));
            }
        t.SetPixels32(px);
        t.Apply();
        return t;
    }

    void Rebuild()
    {
        int s = size.GetHashCode() * 31 + corner.GetHashCode() + hexSize.GetHashCode() * 7 + water.GetHashCode() + grass.GetHashCode() + edge.GetHashCode();
        if (vis != null && s == sig) return;
        sig = s;
        if (vis != null) { if (Application.isPlaying) Destroy(vis); else DestroyImmediate(vis); }

        vis = new GameObject("BackgroundVisuals");
        vis.transform.position = transform.position;
        var half = size / 2f;
        var poly = Gfx.RoundedRect(half, corner);

        var flat = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };
        Layer("Shadow", Fan(poly, new Color(0, 0, 0, .22f)), flat, -12).transform.localPosition = new Vector3(.12f, -.5f, 0);
        Layer("Ground", Fan(poly, grass), flat, -11);

        if (tex == null) tex = HexTile();
        var gridMat = new Material(Shader.Find("Sprites/Default")) { mainTexture = tex, color = new Color(1f, .97f, .85f, .22f), hideFlags = HideFlags.HideAndDontSave };
        float inset = corner * .3f;
        Vector2 q = half - Vector2.one * inset;
        float pw = 1.7320508f * hexSize, ph = 3f * hexSize;
        var quad = new Mesh
        {
            vertices = new[] { new Vector3(-q.x, -q.y), new Vector3(q.x, -q.y), new Vector3(q.x, q.y), new Vector3(-q.x, q.y) },
            uv = new[] { new Vector2(0, 0), new Vector2(q.x * 2 / pw, 0), new Vector2(q.x * 2 / pw, q.y * 2 / ph), new Vector2(0, q.y * 2 / ph) },
            triangles = new[] { 0, 2, 1, 0, 3, 2 }
        };
        Layer("Grid", quad, gridMat, -10);

        var lr = Gfx.Line(vis.transform, "Edge", edge, .18f, -9, false);
        lr.loop = true;
        lr.positionCount = poly.Length;
        for (int i = 0; i < poly.Length; i++) lr.SetPosition(i, poly[i]);

        foreach (var t in vis.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.HideAndDontSave;
    }
}
