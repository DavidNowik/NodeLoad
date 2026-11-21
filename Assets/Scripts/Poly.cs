using UnityEngine;

public static class Poly
{
    // true when the two segments cross each other (touching at an end does not count)
    public static bool SegmentsCross(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        float d1 = Cross(b - a, c - a), d2 = Cross(b - a, d - a), d3 = Cross(d - c, a - c), d4 = Cross(d - c, b - c);
        return d1 * d2 < 0 && d3 * d4 < 0;
    }

    static float Cross(Vector2 u, Vector2 v) { return u.x * v.y - u.y * v.x; }

    public static float SegDist(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
        return Vector2.Distance(p, a + ab * t);
    }
}
