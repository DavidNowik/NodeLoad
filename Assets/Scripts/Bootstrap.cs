using UnityEngine;
using UnityEngine.SceneManagement;

// Builds the whole game at runtime, so the game scene needs no setup. The main menu scene has no game in it.
// The game is built every time a scene loads (not only at start-up), so it also works when it is entered from the main menu.
public static class Bootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Run()
    {
        SceneManager.sceneLoaded += (scene, mode) => Build(scene);
        Build(SceneManager.GetActiveScene());
    }

    static void Build(Scene scene)
    {
        if (scene.name == Hud.MainMenuScene) return;
        if (Object.FindObjectOfType<World>() == null) new GameObject("World").AddComponent<World>();
    }
}
