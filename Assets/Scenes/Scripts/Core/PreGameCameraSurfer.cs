using UnityEngine;

[RequireComponent(typeof(Camera))]
public class PreGameCameraSurfer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WorldGenerator worldGenerator;

    [Header("Smooth Surfing Settings")]
    [SerializeField] private float moveSpeed = 1.6f;
    [SerializeField] private float turnSmoothness = 0.8f;
    [SerializeField] private float wanderRadius = 40f;
    [SerializeField] private float waypointReachDistance = 4f;
    [SerializeField] private int previewChunkPadding = 1;

    private Vector3 originPoint;
    private Vector3 targetPoint;
    private Vector2 currentDirection;
    private float zPos;

    private void Awake()
    {
        if (worldGenerator == null)
            worldGenerator = FindFirstObjectByType<WorldGenerator>();

        zPos = transform.position.z;
        originPoint = transform.position;
        currentDirection = Random.insideUnitCircle.normalized;
        PickNewWaypoint();

        if (worldGenerator != null)
        {
            worldGenerator.targetCamera = GetComponent<Camera>();
            worldGenerator.ConfigureMenuPreview(
                Random.Range(-999999, 999999),
                previewChunkPadding
            );
        }
    }

    private void Update()
    {
        Vector2 toTarget = ((Vector2)targetPoint - (Vector2)transform.position).normalized;
        currentDirection = Vector2.Lerp(currentDirection, toTarget, Time.deltaTime * turnSmoothness).normalized;

        Vector3 nextPos = transform.position + (Vector3)(currentDirection * moveSpeed * Time.deltaTime);
        nextPos.z = zPos;
        transform.position = nextPos;

        if (Vector2.Distance(transform.position, targetPoint) <= waypointReachDistance)
        {
            PickNewWaypoint();
        }
    }

    private void PickNewWaypoint()
    {
        Vector2 randomDir = Random.insideUnitCircle.normalized * Random.Range(wanderRadius * 0.5f, wanderRadius);
        targetPoint = new Vector3(originPoint.x + randomDir.x, originPoint.y + randomDir.y, zPos);
    }
}