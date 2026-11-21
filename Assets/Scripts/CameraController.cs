using UnityEngine;

// Right mouse drag pans, mouse wheel zooms toward the cursor. A right click without dragging is left for cancelling.
public class CameraController : MonoBehaviour
{
    public float minSize = 2.5f, maxSize = 9f;
    public float margin = 1.5f;   // how far past the outermost tiles the camera centre may go

    public static bool rightDragged;

    Camera cam;
    Vector3 lastMouse;
    Vector3 pressPos;
    Bounds bounds;
    bool bounded;

    void Awake() { cam = GetComponent<Camera>(); }

    void Start()
    {
        foreach (var t in FindObjectsOfType<Tile>())
        {
            var p = t.transform.position;
            if (!bounded) { bounds = new Bounds(p, Vector3.zero); bounded = true; }
            else bounds.Encapsulate(p);
        }
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(1))
        {
            pressPos = lastMouse = Input.mousePosition;
            rightDragged = false;
        }
        if (Input.GetMouseButton(1))
        {
            if (!rightDragged && (Input.mousePosition - pressPos).magnitude > 6f) rightDragged = true;
            if (rightDragged)
            {
                Vector3 d = cam.ScreenToWorldPoint(lastMouse) - cam.ScreenToWorldPoint(Input.mousePosition);
                transform.position += new Vector3(d.x, d.y, 0);
            }
            lastMouse = Input.mousePosition;
        }

        float wheel = Input.mouseScrollDelta.y;
        bool overUi = UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
        if (Mathf.Abs(wheel) > .01f && !overUi)
        {
            Vector3 before = cam.ScreenToWorldPoint(Input.mousePosition);
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize * Mathf.Pow(.88f, wheel), minSize, maxSize);
            Vector3 after = cam.ScreenToWorldPoint(Input.mousePosition);
            transform.position += before - after;
        }

        var p = transform.position;
        if (bounded)
        {
            p.x = Mathf.Clamp(p.x, bounds.min.x - margin, bounds.max.x + margin);
            p.y = Mathf.Clamp(p.y, bounds.min.y - margin, bounds.max.y + margin);
        }
        p.z = -10f;
        transform.position = p;
    }
}
