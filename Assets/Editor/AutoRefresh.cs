using UnityEditor;

// Switches Unity's "Auto Refresh" on, so changes to scripts and assets made outside the editor are picked up when the window gets focus.
[InitializeOnLoad]
public static class AutoRefresh
{
    static AutoRefresh()
    {
        if (EditorPrefs.GetInt("kAutoRefreshMode", 1) != 1) EditorPrefs.SetInt("kAutoRefreshMode", 1);
        if (!EditorPrefs.GetBool("kAutoRefresh", true)) EditorPrefs.SetBool("kAutoRefresh", true);
    }
}
