using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Camera))]
public class CameraPositioning : MonoBehaviour
{
    [SerializeField] private Tilemap targetTilemap;

    [Min(0f)]
    [SerializeField] private float padding = 0.5f;

    private Camera cameraComponent;

    private void Awake()
    {
        cameraComponent = GetComponent<Camera>();
        CenterAndFit();
    }

    [ContextMenu("Center and Fit Camera")]
    public void CenterAndFit()
    {
        if (targetTilemap == null)
            return;

        if (cameraComponent == null)
            cameraComponent = GetComponent<Camera>();

        Bounds bounds = targetTilemap.localBounds;

        Vector3 boardCenter =
            targetTilemap.transform.TransformPoint(bounds.center);

        transform.position = new Vector3(
            boardCenter.x,
            boardCenter.y,
            transform.position.z
        );

        cameraComponent.orthographic = true;

        float verticalSize = bounds.size.y / 2f;
        float horizontalSize =
            bounds.size.x / (2f * cameraComponent.aspect);

        cameraComponent.orthographicSize =
            Mathf.Max(verticalSize, horizontalSize) + padding;
    }
}
