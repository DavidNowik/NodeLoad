using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Scene additions: camera pan/zoom, the ground "Background" object, and the "Mist" that hides the map edge.
public static class CreateWorldExtrasMenu
{
    public static void Ensure()
    {
        var cam = Camera.main;
        if (cam != null && cam.GetComponent<CameraController>() == null)
        {
            Undo.AddComponent<CameraController>(cam.gameObject);
            EditorSceneManager.MarkSceneDirty(cam.gameObject.scene);
        }

        if (Object.FindObjectOfType<Background>() == null)
        {
            var go = new GameObject("Background");
            Undo.RegisterCreatedObjectUndo(go, "Create Background");
            go.AddComponent<Background>();
            EditorSceneManager.MarkSceneDirty(go.scene);
        }

        if (Object.FindObjectOfType<Mist>() == null)
        {
            var go = new GameObject("Mist");
            Undo.RegisterCreatedObjectUndo(go, "Create Mist");
            go.AddComponent<Mist>();
            EditorSceneManager.MarkSceneDirty(go.scene);
        }
    }
}
