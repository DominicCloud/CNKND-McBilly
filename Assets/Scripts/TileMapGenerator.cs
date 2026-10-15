using UnityEngine;
using UnityEngine.Tilemaps;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class TilemapGenerator : MonoBehaviour
{
    [Header("Tilemap")]
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private TileBase lightTile;
    [SerializeField] private TileBase darkTile;

    [Header("Dimensions")]
    [Min(1)]
    [SerializeField] private int width = 8;

    [Min(1)]
    [SerializeField] private int height = 8;

    [Header("Placement")]
    [SerializeField] private Vector2Int origin;

    [Header("Editor Generation")]
    [SerializeField] private bool regenerateAutomatically = true;

    private bool regenerationRequested;

    private void OnEnable()
    {
        regenerationRequested = true;
    }

    private void OnValidate()
    {
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);

        if (regenerateAutomatically)
        {
            regenerationRequested = true;
        }
    }

    private void Update()
    {
        if (Application.isPlaying)
            return;

        if (!regenerationRequested)
            return;

        regenerationRequested = false;
        Generate();
    }

    [ContextMenu("Generate Checkerboard")]
    public void Generate()
    {
        if (groundTilemap == null || lightTile == null || darkTile == null)
            return;

#if UNITY_EDITOR
        Undo.RecordObject(groundTilemap, "Generate Checkerboard");
#endif

        groundTilemap.ClearAllTiles();

        BoundsInt area = new BoundsInt(
            origin.x,
            origin.y,
            0,
            width,
            height,
            1
        );

        TileBase[] tiles = new TileBase[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = x + y * width;
                bool useLightTile = (x + y) % 2 == 0;

                tiles[index] = useLightTile ? lightTile : darkTile;
            }
        }

        groundTilemap.SetTilesBlock(area, tiles);

#if UNITY_EDITOR
        EditorUtility.SetDirty(groundTilemap);
#endif
    }

    [ContextMenu("Clear Checkerboard")]
    public void Clear()
    {
        if (groundTilemap == null)
            return;

#if UNITY_EDITOR
        Undo.RecordObject(groundTilemap, "Clear Checkerboard");
#endif

        groundTilemap.ClearAllTiles();

#if UNITY_EDITOR
        EditorUtility.SetDirty(groundTilemap);
#endif
    }
}
