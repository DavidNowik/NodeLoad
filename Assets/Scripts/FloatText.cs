using UnityEngine;

public class FloatText : MonoBehaviour
{
    TextMesh tm;
    Color c;
    float age;

    public static void Spawn(Vector2 pos, string text, Color color)
    {
        var go = new GameObject("FloatText");
        go.transform.position = pos;
        var f = go.AddComponent<FloatText>();
        f.c = color;
        f.tm = Gfx.Text(go.transform, "Text", Vector2.zero, .3f, color, Layers.FloatText);
        f.tm.text = text;
    }

    void Update()
    {
        age += Time.deltaTime;
        transform.position += Vector3.up * Time.deltaTime * 1.1f;
        tm.color = new Color(c.r, c.g, c.b, 1f - age / .8f);
        if (age > .8f) Destroy(gameObject);
    }
}
