public static class NumFmt
{
    public static string Short(double v)
    {
        if (v < 10000) return v == System.Math.Floor(v) ? ((long)v).ToString() : v.ToString("0.##");
        if (v < 1e6) return (v / 1e3).ToString("0.0") + "K";
        if (v < 1e9) return (v / 1e6).ToString("0.00") + "M";
        if (v < 1e12) return (v / 1e9).ToString("0.00") + "B";
        return (v / 1e12).ToString("0.00") + "T";
    }
}
