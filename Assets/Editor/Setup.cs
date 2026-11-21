using UnityEditor;
using UnityEngine;

// The one NodeLoad button: creates whatever is missing (data assets, tiles, camera, background, mist, HUD) and removes missing scripts.
// Safe to press again at any time; save the scene afterwards.
public static class Setup
{
    [MenuItem("NodeLoad/Setup")]
    static void Run()
    {
        if (Application.isPlaying) { Debug.LogWarning("NodeLoad Setup: leave Play mode first."); return; }
        CreateMapMenu.CreateData();
        CreateMapMenu.EnsureTiles();
        CreateWorldExtrasMenu.Ensure();
        CreateHudMenu.Ensure();
        CreateMainMenuMenu.EnsureBuffs();
        RemoveMissingScriptsMenu.Run();
        Debug.Log("NodeLoad Setup done. Save the scene (Ctrl+S).");
    }
}
