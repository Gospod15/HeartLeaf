using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Rigidbody2D))]
public class NpcMovement : MonoBehaviour
{
    private enum NpcState
    {
        WanderingIndoors,
        GoingToDoorApproach,
        WalkingThroughDoor,
        WanderingOutdoors,
        ReturningToDoorApproach
    }

    [Header("Отладка и управление ИИ")]
    [Tooltip("Отключает весь интеллект и передвижение NPC")]
    public bool noAI = false;

    [Header("Физика и отталкивание игроком")]
    [Tooltip("Насколько легко игрок может сдвинуть NPC в сторону")]
    [SerializeField] private float pushAwaySpeed = 3.2f;
    [SerializeField] private float pushDecay = 12f;

    [Header("Видимость и Sorting Order")]
    [Tooltip("Order in Layer внутри дома и в дверях (выше черного фона 1 и стен 3, ниже крыши 11)")]
    [SerializeField] private int indoorSortingOrder = 4;
    [Tooltip("Order in Layer на улице (ниже черного фона интерьера 1, чтобы скрываться когда игрок в доме)")]
    [SerializeField] private int outdoorSortingOrder = 0;

    [Header("Зона ВНУТРИ дома (Зеленый Gizmos)")]
    public Transform house;
    public float indoorWidth = 8f;
    public float indoorHeight = 6f;
    public float indoorOffsetX = 0f;
    public float indoorOffsetY = 0f;

    [Header("Зона НА УЛИЦЕ (Желтый Gizmos)")]
    public Transform outdoorAnchor;
    public float outdoorWidth = 10f;
    public float outdoorHeight = 8f;
    public float outdoorOffsetX = 0f;
    public float outdoorOffsetY = -6f;

    [Header("Отступ от стенок")]
    [SerializeField] private float wallClearanceOffset = 0.25f;

    [Header("Проход через дверь (пешком)")]
    [Tooltip("Дистанция до insidePoint/outsidePoint, на которой NPC открывает дверь, отключает коллизию и заходит")]
    [SerializeField] private float doorInteractDistance = 0.75f;
    [Tooltip("Дистанция завершения прохода через дверь на противоположной точке")]
    [SerializeField] private float doorExitReachDistance = 0.15f;

    [Header("Намеренный выход на улицу и прогулка снаружи")]
    [Range(0f, 1f)]
    [SerializeField] private float goOutsideChance = 0.35f;
    [SerializeField] private int minWalksBeforeExit = 2;
    [SerializeField] private int maxWalksBeforeExit = 4;
    [SerializeField] private float maxTimeIndoors = 20f;
    [SerializeField] private float outdoorDuration = 8f;
    [SerializeField] private int minWalksOutdoors = 2;
    [SerializeField] private int maxWalksOutdoors = 4;

    [Header("Настройки движения и анти-застревания")]
    [SerializeField] private float moveSpeed = 2.2f;
    [SerializeField] private float minWaitTime = 1.5f;
    [SerializeField] private float maxWaitTime = 4.0f;
    [SerializeField] private float waypointReachDistance = 0.08f;
    [SerializeField] private float stuckTimeThreshold = 0.35f;

    [Header("Препятствия и тайлы")]
    [SerializeField] private LayerMask obstacleLayer;
    [SerializeField] private float cellCheckSize = 0.45f;
    [SerializeField] private int maxPathSearchNodes = 500;

    private Rigidbody2D rb;
    private Animator animator;
    private Tilemap groundTilemap;
    private SpriteRenderer[] spriteRenderers;
    private Collider2D[] npcColliders;
    private Collider2D solidCollider;

    private List<DoorController> houseDoors = new List<DoorController>();
    private DoorController activeDoor;
    private DoorController lastUsedDoor;

    private NpcState currentState = NpcState.WanderingIndoors;
    private List<Vector3> currentPath = new List<Vector3>();
    private int currentWaypointIndex = 0;
    private float waitTimer = 0f;
    private bool isWaiting = false;
    private Vector2 currentMoveDir;
    private Vector2 pushVelocity;
    private Vector2 lookDirOverride;
    private bool isTalking = false;

    private bool crossingToOutside = true;
    private Vector3 doorTargetPos;

    private float outdoorTimer = 0f;
    private bool lastNoAIState = false;
    private bool isDoorTestActive = false;

    private int walksDoneIndoors = 0;
    private int targetWalksBeforeExit = 3;
    private float timeSpentIndoors = 0f;
    private bool wantsToGoOutside = false;

    private int walksDoneOutdoors = 0;
    private int targetWalksOutdoors = 3;

    private Vector2 lastCheckPosition;
    private float stuckTimer = 0f;

    private Vector3 IndoorCenter => house != null
        ? new Vector3(house.position.x + indoorOffsetX, house.position.y + indoorOffsetY, house.position.z)
        : transform.position;

    private Vector3 OutdoorCenter
    {
        get
        {
            Transform anchor = outdoorAnchor != null ? outdoorAnchor : house;
            if (anchor != null)
                return new Vector3(anchor.position.x + outdoorOffsetX, anchor.position.y + outdoorOffsetY, anchor.position.z);

            return transform.position;
        }
    }

    private Vector2 ColliderOffset => solidCollider != null
        ? Vector2.Scale(solidCollider.offset, (Vector2)transform.lossyScale)
        : Vector2.zero;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        npcColliders = GetComponentsInChildren<Collider2D>(true);

        for (int i = 0; i < npcColliders.Length; i++)
        {
            if (npcColliders[i] != null && !npcColliders[i].isTrigger)
            {
                solidCollider = npcColliders[i];
                break;
            }
        }

        // Делаем тело легким Dynamic, чтобы игрок не упирался в NPC как в бетонную стену
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.mass = 0.35f;
        rb.linearDamping = 14f;
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
    }

    private void Start()
    {
        if (WorldGenerator.instance != null)
            groundTilemap = WorldGenerator.instance.groundTilemap;

        FindAllDoorsInHouse();
        ResetIndoorExitCounters();
        UpdateSortingOrder();
        lastCheckPosition = rb.position;
        lastNoAIState = noAI;
        StartWaiting(Random.Range(0.5f, minWaitTime));
    }

    public void SetNoAI(bool disabled)
    {
        noAI = disabled;
        ApplyNoAIStateChange();
    }

    public void SetTalkingState(bool talking, Transform listener = null)
    {
        isTalking = talking;
        if (talking)
        {
            currentMoveDir = Vector2.zero;
            if (listener != null)
            {
                Vector2 dir = ((Vector2)listener.position - (Vector2)transform.position).normalized;
                if (dir.sqrMagnitude > 0.001f)
                    lookDirOverride = dir;
            }
        }
        else
        {
            lookDirOverride = Vector2.zero;
        }
    }

    [ContextMenu("Тест дверей: выйти, пройтись и зайти")]
    public void TestDoorWalkCycle()
    {
        if (!Application.isPlaying || house == null) return;

        SetSolidCollisionEnabled(true);
        noAI = false;
        lastNoAIState = false;
        isWaiting = false;
        isDoorTestActive = true;
        currentState = NpcState.WanderingIndoors;
        UpdateSortingOrder();

        FindAllDoorsInHouse();
        if (houseDoors.Count == 0)
        {
            isDoorTestActive = false;
            Debug.LogWarning($"У {house.name} не найдено ни одной двери с назначенными insidePoint и outsidePoint!");
            return;
        }

        if (!TryStartExitThroughDoor())
        {
            isDoorTestActive = false;
            Debug.LogWarning($"Не удалось построить путь ни к одной двери ({houseDoors.Count} шт.). Проверь insidePoint / outsidePoint и Obstacle Layer.");
        }
    }

    private void ApplyNoAIStateChange()
    {
        if (noAI)
        {
            SetSolidCollisionEnabled(true);
            currentMoveDir = Vector2.zero;
            currentPath.Clear();
            currentWaypointIndex = 0;
            isWaiting = false;
            isDoorTestActive = false;
            UpdateAnimation();
        }
        else
        {
            StartWaiting(0.2f);
        }
        lastNoAIState = noAI;
    }

    private void SetSolidCollisionEnabled(bool enabled)
    {
        if (solidCollider != null)
        {
            solidCollider.enabled = enabled;
        }
    }

    private void UpdateSortingOrder()
    {
        if (spriteRenderers == null) return;

        bool isOutside = currentState == NpcState.WanderingOutdoors;
        int targetOrder = isOutside ? outdoorSortingOrder : indoorSortingOrder;

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null && spriteRenderers[i].sortingOrder != targetOrder)
            {
                spriteRenderers[i].sortingOrder = targetOrder;
            }
        }
    }

    public void FindAllDoorsInHouse()
    {
        houseDoors.Clear();
        if (house == null) return;

        DoorController[] foundDoors = house.GetComponentsInChildren<DoorController>(true);
        for (int i = 0; i < foundDoors.Length; i++)
        {
            if (foundDoors[i] != null && foundDoors[i].outsidePoint != null && foundDoors[i].insidePoint != null)
            {
                houseDoors.Add(foundDoors[i]);
            }
        }
    }

    private void ResetIndoorExitCounters()
    {
        walksDoneIndoors = 0;
        timeSpentIndoors = 0f;
        wantsToGoOutside = false;
        targetWalksBeforeExit = Random.Range(minWalksBeforeExit, maxWalksBeforeExit + 1);
    }

    private void ResetOutdoorCounters()
    {
        walksDoneOutdoors = 0;
        outdoorTimer = outdoorDuration;
        targetWalksOutdoors = isDoorTestActive ? 1 : Random.Range(minWalksOutdoors, maxWalksOutdoors + 1);
    }

    private void Update()
    {
        if (noAI != lastNoAIState)
            ApplyNoAIStateChange();

        UpdateSortingOrder();

        // Автоматически ставим NPC на паузу, если открыт диалог
        if (DialogueManager.Instance != null)
        {
            if (DialogueManager.Instance.IsDialogueActive && !isTalking)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null && Vector2.Distance(transform.position, player.transform.position) < 3.5f)
                {
                    SetTalkingState(true, player.transform);
                }
            }
            else if (!DialogueManager.Instance.IsDialogueActive && isTalking)
            {
                SetTalkingState(false);
            }
        }

        if (noAI || isTalking)
        {
            currentMoveDir = Vector2.zero;
            UpdateAnimation();
            return;
        }

        UpdateAnimation();

        if (currentState == NpcState.GoingToDoorApproach || currentState == NpcState.ReturningToDoorApproach)
        {
            CheckApproachArrival();
        }

        if (currentState == NpcState.WanderingOutdoors)
        {
            outdoorTimer -= Time.deltaTime;
            if (outdoorTimer <= 0f || walksDoneOutdoors >= targetWalksOutdoors)
            {
                isWaiting = false;
                currentPath.Clear();
                BeginReturnHome();
                return;
            }
        }

        if (currentState == NpcState.WanderingIndoors && houseDoors.Count > 0)
        {
            timeSpentIndoors += Time.deltaTime;
            if (timeSpentIndoors >= maxTimeIndoors || walksDoneIndoors >= targetWalksBeforeExit)
            {
                wantsToGoOutside = true;
            }
        }

        if (isWaiting)
        {
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f)
            {
                isWaiting = false;
                DecideNextAction();
            }
        }
    }

    private void FixedUpdate()
    {
        // Гасим импульс отталкивания от игрока
        if (pushVelocity.sqrMagnitude > 0.0001f)
        {
            pushVelocity = Vector2.MoveTowards(pushVelocity, Vector2.zero, pushDecay * Time.fixedDeltaTime);
        }
        else
        {
            pushVelocity = Vector2.zero;
        }

        if (noAI || isWaiting || isTalking)
        {
            currentMoveDir = Vector2.zero;
            stuckTimer = 0f;
            lastCheckPosition = rb.position;

            if (pushVelocity.sqrMagnitude > 0.0001f)
            {
                rb.MovePosition(rb.position + pushVelocity * Time.fixedDeltaTime);
            }
            return;
        }

        if (currentState == NpcState.WalkingThroughDoor)
        {
            Vector2 currentPos = rb.position;
            Vector2 targetPos = (Vector2)doorTargetPos - ColliderOffset;
            Vector2 toTarget = targetPos - currentPos;

            if (toTarget.magnitude <= doorExitReachDistance)
            {
                FinishWalkingThroughDoor();
                return;
            }

            currentMoveDir = toTarget.normalized;
            rb.MovePosition(currentPos + currentMoveDir * moveSpeed * Time.fixedDeltaTime);
            return;
        }

        if (currentPath == null || currentWaypointIndex >= currentPath.Count)
        {
            currentMoveDir = Vector2.zero;
            stuckTimer = 0f;
            lastCheckPosition = rb.position;

            if (pushVelocity.sqrMagnitude > 0.0001f)
            {
                rb.MovePosition(rb.position + pushVelocity * Time.fixedDeltaTime);
            }
            return;
        }

        Vector2 wpTarget = currentPath[currentWaypointIndex];
        Vector2 pos = rb.position;
        Vector2 diff = wpTarget - pos;

        if (diff.magnitude <= waypointReachDistance)
        {
            currentWaypointIndex++;
            stuckTimer = 0f;
            lastCheckPosition = pos;

            if (currentWaypointIndex >= currentPath.Count)
            {
                currentMoveDir = Vector2.zero;
                OnDestinationReached();
            }
            return;
        }

        Vector2 dir = diff.normalized;

        // Если NPC сейчас толкает игрок, не сбрасываем его маршрут как "застрял"
        if (pushVelocity.sqrMagnitude < 0.05f && (IsBlockedAhead(dir) || CheckIfStuck(pos)))
        {
            HandleObstacleOrStuck(dir);
            return;
        }

        currentMoveDir = dir;
        Vector2 totalDisplacement = (currentMoveDir * moveSpeed + pushVelocity) * Time.fixedDeltaTime;
        rb.MovePosition(pos + totalDisplacement);
    }

    private void CheckApproachArrival()
    {
        if (activeDoor == null) return;

        Vector2 npcPos = rb.position + ColliderOffset;
        bool goingOut = currentState == NpcState.GoingToDoorApproach;
        Vector3 approachPos = goingOut ? activeDoor.insidePoint.position : activeDoor.outsidePoint.position;

        if (Vector2.Distance(npcPos, approachPos) <= doorInteractDistance)
        {
            BeginWalkingThroughDoor(goingOut);
        }
    }

    private void BeginWalkingThroughDoor(bool goingOutside)
    {
        if (activeDoor == null) return;

        currentPath.Clear();
        currentWaypointIndex = 0;
        isWaiting = false;
        crossingToOutside = goingOutside;
        currentState = NpcState.WalkingThroughDoor;
        UpdateSortingOrder();

        if (!activeDoor.IsOpen)
        {
            activeDoor.SetState(true, playSound: true);
        }

        SetSolidCollisionEnabled(false);

        doorTargetPos = goingOutside
            ? activeDoor.outsidePoint.position
            : activeDoor.insidePoint.position;
    }

    private void FinishWalkingThroughDoor()
    {
        currentMoveDir = Vector2.zero;
        SetSolidCollisionEnabled(true);

        if (activeDoor != null && activeDoor.IsOpen)
        {
            activeDoor.SetState(false, playSound: true);
        }

        if (crossingToOutside)
        {
            currentState = NpcState.WanderingOutdoors;
            UpdateSortingOrder();
            ResetOutdoorCounters();
            PickOutdoorDestination(Vector2.zero);
        }
        else
        {
            activeDoor = null;
            isDoorTestActive = false;
            currentState = NpcState.WanderingIndoors;
            UpdateSortingOrder();
            ResetIndoorExitCounters();
            StartWaiting(Random.Range(minWaitTime, maxWaitTime));
        }
    }

    private bool CheckIfStuck(Vector2 currentPos)
    {
        float movedDistance = Vector2.Distance(currentPos, lastCheckPosition);
        float minExpectedMove = moveSpeed * Time.fixedDeltaTime * 0.25f;

        if (movedDistance < minExpectedMove)
        {
            stuckTimer += Time.fixedDeltaTime;
            if (stuckTimer >= stuckTimeThreshold)
            {
                stuckTimer = 0f;
                lastCheckPosition = currentPos;
                return true;
            }
        }
        else
        {
            stuckTimer = 0f;
            lastCheckPosition = currentPos;
        }

        return false;
    }

    private void HandleObstacleOrStuck(Vector2 blockedDir)
    {
        stuckTimer = 0f;
        lastCheckPosition = rb.position;

        if ((currentState == NpcState.GoingToDoorApproach || currentState == NpcState.ReturningToDoorApproach) && activeDoor != null)
        {
            Vector3 goalPos = currentState == NpcState.GoingToDoorApproach
                ? activeDoor.insidePoint.position
                : activeDoor.outsidePoint.position;

            if (Vector2.Distance(rb.position + ColliderOffset, goalPos) <= doorInteractDistance * 1.35f)
            {
                BeginWalkingThroughDoor(currentState == NpcState.GoingToDoorApproach);
                return;
            }

            List<Vector3> newPath = FindPathAStar(WorldToCell(rb.position + ColliderOffset), WorldToCell(goalPos), ignoreDoor: activeDoor, exactFinalTarget: goalPos);
            if (newPath != null && newPath.Count > 0)
            {
                currentPath = newPath;
                currentWaypointIndex = 0;
                return;
            }
        }

        currentPath.Clear();
        currentMoveDir = Vector2.zero;
        isWaiting = false;

        if (currentState == NpcState.WanderingOutdoors)
            PickOutdoorDestination(avoidDirection: blockedDir);
        else
            PickIndoorDestination(avoidDirection: blockedDir);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (currentState == NpcState.WalkingThroughDoor)
            return;

        // Если NPC толкает игрок — плавно уступаем дорогу и не считаем это застреванием в стене
        if (collision.collider != null && collision.collider.CompareTag("Player"))
        {
            Vector2 awayFromPlayer = ((Vector2)transform.position - (Vector2)collision.transform.position).normalized;
            if (awayFromPlayer.sqrMagnitude < 0.01f)
                awayFromPlayer = Vector2.right;

            pushVelocity = awayFromPlayer * pushAwaySpeed;
            stuckTimer = 0f;
            return;
        }

        if (noAI || isWaiting || isTalking || currentMoveDir.sqrMagnitude < 0.01f)
            return;

        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector2 normal = collision.GetContact(i).normal;
            if (Vector2.Dot(currentMoveDir, -normal) > 0.5f)
            {
                stuckTimer += Time.fixedDeltaTime * 2f;
                if (stuckTimer >= stuckTimeThreshold)
                {
                    HandleObstacleOrStuck(currentMoveDir);
                }
                break;
            }
        }
    }

    private void DecideNextAction()
    {
        if (house == null) return;

        if (currentState == NpcState.WanderingOutdoors)
        {
            if (outdoorTimer <= 0f || walksDoneOutdoors >= targetWalksOutdoors)
                BeginReturnHome();
            else
                PickOutdoorDestination(Vector2.zero);
            return;
        }

        bool shouldTryExit = currentState == NpcState.WanderingIndoors &&
                             houseDoors.Count > 0 &&
                             (wantsToGoOutside || Random.value < goOutsideChance);

        if (shouldTryExit && TryStartExitThroughDoor())
            return;

        activeDoor = null;
        currentState = NpcState.WanderingIndoors;
        PickIndoorDestination(Vector2.zero);
    }

    private bool TryStartExitThroughDoor()
    {
        if (houseDoors.Count == 0) FindAllDoorsInHouse();
        if (houseDoors.Count == 0) return false;

        Vector2 currentPos = rb.position + ColliderOffset;
        Vector3Int startCell = WorldToCell(currentPos);

        List<DoorController> sortedDoors = new List<DoorController>(houseDoors);
        sortedDoors.Sort((a, b) =>
        {
            float distA = Vector2.SqrMagnitude((Vector2)a.transform.position - currentPos);
            float distB = Vector2.SqrMagnitude((Vector2)b.transform.position - currentPos);
            return distA.CompareTo(distB);
        });

        for (int i = 0; i < sortedDoors.Count; i++)
        {
            DoorController candidate = sortedDoors[i];
            if (candidate == null || candidate.insidePoint == null || candidate.outsidePoint == null) continue;

            Vector3 targetPoint = candidate.insidePoint.position;
            Vector3Int approachCell = WorldToCell(targetPoint);

            if (approachCell == startCell)
            {
                activeDoor = candidate;
                lastUsedDoor = candidate;
                wantsToGoOutside = false;
                BeginWalkingThroughDoor(goingOutside: true);
                return true;
            }

            List<Vector3> exitPath = FindPathAStar(startCell, approachCell, ignoreDoor: candidate, exactFinalTarget: targetPoint);

            if (exitPath != null && exitPath.Count > 0)
            {
                activeDoor = candidate;
                lastUsedDoor = candidate;
                wantsToGoOutside = false;
                currentState = NpcState.GoingToDoorApproach;
                currentPath = exitPath;
                currentWaypointIndex = 0;
                return true;
            }
        }

        activeDoor = null;
        return false;
    }

    private void OnDestinationReached()
    {
        if (currentState == NpcState.GoingToDoorApproach)
        {
            BeginWalkingThroughDoor(goingOutside: true);
            return;
        }

        if (currentState == NpcState.ReturningToDoorApproach)
        {
            BeginWalkingThroughDoor(goingOutside: false);
            return;
        }

        if (currentState == NpcState.WanderingOutdoors)
        {
            walksDoneOutdoors++;
            if (walksDoneOutdoors >= targetWalksOutdoors || outdoorTimer <= 0f)
                BeginReturnHome();
            else
                StartWaiting(Random.Range(minWaitTime, maxWaitTime));
            return;
        }

        walksDoneIndoors++;
        StartWaiting(Random.Range(minWaitTime, maxWaitTime));
    }

    private void BeginReturnHome()
    {
        currentState = NpcState.ReturningToDoorApproach;
        UpdateSortingOrder();

        if (activeDoor == null && lastUsedDoor != null)
            activeDoor = lastUsedDoor;

        if (activeDoor == null && houseDoors.Count > 0)
            activeDoor = houseDoors[0];

        if (activeDoor == null || activeDoor.outsidePoint == null)
        {
            currentState = NpcState.WanderingIndoors;
            StartWaiting(minWaitTime);
            return;
        }

        Vector3 targetPoint = activeDoor.outsidePoint.position;
        Vector3Int startCell = WorldToCell(rb.position + ColliderOffset);
        Vector3Int goalCell = WorldToCell(targetPoint);

        List<Vector3> returnPath = FindPathAStar(startCell, goalCell, ignoreDoor: activeDoor, exactFinalTarget: targetPoint);
        if (returnPath != null && returnPath.Count > 0)
        {
            currentPath = returnPath;
            currentWaypointIndex = 0;
        }
        else
        {
            BeginWalkingThroughDoor(goingOutside: false);
        }
    }

    private void StartWaiting(float duration)
    {
        isWaiting = true;
        waitTimer = duration;
        currentMoveDir = Vector2.zero;
    }

    private void PickIndoorDestination(Vector2 avoidDirection)
    {
        PickDestinationInZone(IndoorCenter, indoorWidth, indoorHeight, avoidDirection, isIndoors: true);
    }

    private void PickOutdoorDestination(Vector2 avoidDirection)
    {
        PickDestinationInZone(OutdoorCenter, outdoorWidth, outdoorHeight, avoidDirection, isIndoors: false);
    }

    private void PickDestinationInZone(Vector3 zoneCenter, float zoneWidth, float zoneHeight, Vector2 avoidDirection, bool isIndoors)
    {
        Vector3Int startCell = WorldToCell(rb.position + ColliderOffset);
        float halfW = Mathf.Max(0.2f, zoneWidth * 0.5f);
        float halfH = Mathf.Max(0.2f, zoneHeight * 0.5f);

        for (int attempt = 0; attempt < 25; attempt++)
        {
            Vector3 randomPos = new Vector3(
                zoneCenter.x + Random.Range(-halfW, halfW),
                zoneCenter.y + Random.Range(-halfH, halfH),
                0f
            );

            if (avoidDirection.sqrMagnitude > 0.01f && attempt < 15)
            {
                Vector2 dirToCandidate = ((Vector2)randomPos - rb.position).normalized;
                if (Vector2.Dot(dirToCandidate, avoidDirection) > 0.3f)
                    continue;
            }

            Vector3Int targetCell = WorldToCell(randomPos);
            if (targetCell == startCell) continue;
            if (!IsCellWalkable(targetCell, null, allowHouseInterior: isIndoors)) continue;

            List<Vector3> path = FindPathAStar(startCell, targetCell, null, null, allowHouseInterior: isIndoors);
            if (path != null && path.Count > 0)
            {
                currentPath = path;
                currentWaypointIndex = 0;
                return;
            }
        }

        StartWaiting(1f);
    }

    private bool IsCellWalkable(Vector3Int cell, DoorController ignoreDoor, bool allowHouseInterior = true)
    {
        Vector2Int cell2D = new Vector2Int(cell.x, cell.y);
        Vector3 worldCenter = CellToWorldCenter(cell);

        if (WorldGenerator.instance != null && !allowHouseInterior)
        {
            if (WorldGenerator.instance.IsCellOccupied(cell2D))
                return false;

            if (WorldGenerator.instance.riverTilemap != null &&
                WorldGenerator.instance.riverTilemap.HasTile(cell))
                return false;

            if (WorldGenerator.instance.holeCollisionTilemap != null &&
                WorldGenerator.instance.holeCollisionTilemap.HasTile(cell))
                return false;

            if (groundTilemap != null)
            {
                TileBase tile = groundTilemap.GetTile(cell);
                if (tile == null || tile == WorldGenerator.instance.waterTile)
                    return false;
            }
        }

        Collider2D[] hits = Physics2D.OverlapBoxAll(worldCenter, Vector2.one * cellCheckSize, 0f, obstacleLayer);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit.gameObject == gameObject || hit.isTrigger)
                continue;

            if (ignoreDoor != null &&
                (hit.gameObject == ignoreDoor.gameObject || hit.transform.IsChildOf(ignoreDoor.transform)))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private bool IsBlockedAhead(Vector2 direction)
    {
        Vector2 checkOrigin = rb.position + ColliderOffset;
        Vector2 boxSize = solidCollider != null
            ? (Vector2)solidCollider.bounds.size * 0.85f
            : Vector2.one * (cellCheckSize * 0.7f);

        RaycastHit2D[] hits = Physics2D.BoxCastAll(
            checkOrigin,
            boxSize,
            0f,
            direction,
            0.22f,
            obstacleLayer
        );

        bool approachingDoor = currentState == NpcState.GoingToDoorApproach || currentState == NpcState.ReturningToDoorApproach;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D col = hits[i].collider;
            if (col == null || col.gameObject == gameObject || col.isTrigger)
                continue;

            if (approachingDoor && activeDoor != null &&
                (col.gameObject == activeDoor.gameObject || col.transform.IsChildOf(activeDoor.transform)))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private List<Vector3> FindPathAStar(Vector3Int start, Vector3Int goal, DoorController ignoreDoor, Vector3? exactFinalTarget = null, bool allowHouseInterior = true)
    {
        List<PathNode> openList = new List<PathNode>();
        HashSet<Vector3Int> closedSet = new HashSet<Vector3Int>();
        Dictionary<Vector3Int, PathNode> allNodes = new Dictionary<Vector3Int, PathNode>();

        PathNode startNode = new PathNode(start, null, 0, GetHeuristic(start, goal));
        openList.Add(startNode);
        allNodes[start] = startNode;

        int iterations = 0;

        while (openList.Count > 0 && iterations < maxPathSearchNodes)
        {
            iterations++;

            int bestIndex = 0;
            for (int i = 1; i < openList.Count; i++)
            {
                if (openList[i].F < openList[bestIndex].F)
                    bestIndex = i;
            }

            PathNode current = openList[bestIndex];
            openList.RemoveAt(bestIndex);

            if (current.pos == goal)
                return ReconstructPath(current, ignoreDoor, exactFinalTarget, allowHouseInterior);

            closedSet.Add(current.pos);

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;

                    Vector3Int neighborPos = new Vector3Int(current.pos.x + dx, current.pos.y + dy, 0);
                    if (closedSet.Contains(neighborPos)) continue;

                    bool isGoalCell = neighborPos == goal && exactFinalTarget.HasValue;
                    if (!isGoalCell && !IsCellWalkable(neighborPos, ignoreDoor, allowHouseInterior)) continue;

                    if (dx != 0 && dy != 0)
                    {
                        Vector3Int side1 = new Vector3Int(current.pos.x + dx, current.pos.y, 0);
                        Vector3Int side2 = new Vector3Int(current.pos.x, current.pos.y + dy, 0);
                        if (!IsCellWalkable(side1, ignoreDoor, allowHouseInterior) || !IsCellWalkable(side2, ignoreDoor, allowHouseInterior))
                            continue;
                    }

                    int stepCost = (dx != 0 && dy != 0) ? 14 : 10;
                    int newG = current.g + stepCost;

                    if (!allNodes.TryGetValue(neighborPos, out PathNode neighborNode))
                    {
                        neighborNode = new PathNode(neighborPos, current, newG, GetHeuristic(neighborPos, goal));
                        allNodes[neighborPos] = neighborNode;
                        openList.Add(neighborNode);
                    }
                    else if (newG < neighborNode.g)
                    {
                        neighborNode.g = newG;
                        neighborNode.parent = current;
                        if (!openList.Contains(neighborNode))
                            openList.Add(neighborNode);
                    }
                }
            }
        }

        return null;
    }

    private List<Vector3> ReconstructPath(PathNode endNode, DoorController ignoreDoor, Vector3? exactFinalTarget, bool allowHouseInterior)
    {
        List<Vector3> path = new List<Vector3>();
        PathNode current = endNode;
        Vector3 colOffset = ColliderOffset;
        bool isFirst = true;

        while (current != null && current.parent != null)
        {
            Vector3 waypoint;
            if (isFirst && exactFinalTarget.HasValue)
            {
                waypoint = exactFinalTarget.Value - colOffset;
            }
            else
            {
                waypoint = CellToWorldCenter(current.pos) - colOffset;
                waypoint = ApplyWallClearance(current.pos, waypoint, ignoreDoor, allowHouseInterior);
            }

            path.Add(waypoint);
            current = current.parent;
            isFirst = false;
        }

        path.Reverse();
        return path;
    }

    private Vector3 ApplyWallClearance(Vector3Int cell, Vector3 worldPos, DoorController ignoreDoor, bool allowHouseInterior)
    {
        if (wallClearanceOffset <= 0.001f) return worldPos;

        bool wallAbove = !IsCellWalkable(new Vector3Int(cell.x, cell.y + 1, 0), ignoreDoor, allowHouseInterior);
        bool wallBelow = !IsCellWalkable(new Vector3Int(cell.x, cell.y - 1, 0), ignoreDoor, allowHouseInterior);
        bool wallLeft = !IsCellWalkable(new Vector3Int(cell.x - 1, cell.y, 0), ignoreDoor, allowHouseInterior);
        bool wallRight = !IsCellWalkable(new Vector3Int(cell.x + 1, cell.y, 0), ignoreDoor, allowHouseInterior);

        if (wallAbove && !wallBelow)
            worldPos.y -= wallClearanceOffset;
        else if (wallBelow && !wallAbove)
            worldPos.y += wallClearanceOffset;

        if (wallLeft && !wallRight)
            worldPos.x += wallClearanceOffset;
        else if (wallRight && !wallLeft)
            worldPos.x -= wallClearanceOffset;

        return worldPos;
    }

    private int GetHeuristic(Vector3Int a, Vector3Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        return 10 * (dx + dy) + (14 - 2 * 10) * Mathf.Min(dx, dy);
    }

    private Vector3Int WorldToCell(Vector3 worldPos)
    {
        if (groundTilemap != null)
            return groundTilemap.WorldToCell(worldPos);

        return new Vector3Int(Mathf.FloorToInt(worldPos.x), Mathf.FloorToInt(worldPos.y), 0);
    }

    private Vector3 CellToWorldCenter(Vector3Int cell)
    {
        if (groundTilemap != null)
            return groundTilemap.GetCellCenterWorld(cell);

        return new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);
    }

    private void UpdateAnimation()
    {
        if (animator == null) return;

        if (isTalking && lookDirOverride.sqrMagnitude > 0.001f)
        {
            animator.SetFloat("X", lookDirOverride.x);
            animator.SetFloat("Y", lookDirOverride.y);
            animator.SetFloat("Speed", 0f);
            return;
        }

        animator.SetFloat("X", currentMoveDir.x);
        animator.SetFloat("Y", currentMoveDir.y);
        animator.SetFloat("Speed", currentMoveDir.sqrMagnitude);
    }

    private void OnDrawGizmos()
    {
        if (house != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(IndoorCenter, new Vector3(indoorWidth, indoorHeight, 0f));

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(OutdoorCenter, new Vector3(outdoorWidth, outdoorHeight, 0f));
        }

        if (Application.isPlaying && currentPath != null && currentPath.Count > 0)
        {
            Gizmos.color = Color.cyan;
            for (int i = currentWaypointIndex; i < currentPath.Count - 1; i++)
            {
                Gizmos.DrawLine(currentPath[i], currentPath[i + 1]);
            }
        }
    }

    private class PathNode
    {
        public Vector3Int pos;
        public PathNode parent;
        public int g;
        public int h;
        public int F => g + h;

        public PathNode(Vector3Int pos, PathNode parent, int g, int h)
        {
            this.pos = pos;
            this.parent = parent;
            this.g = g;
            this.h = h;
        }
    }
}

#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(NpcMovement))]
public class NpcMovementEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        NpcMovement npc = (NpcMovement)target;
        UnityEditor.EditorGUILayout.Space(8);

        GUI.enabled = Application.isPlaying;
        if (GUILayout.Button("Проверить двери: выйти, пройтись и зайти", GUILayout.Height(30)))
        {
            npc.TestDoorWalkCycle();
        }
        GUI.enabled = true;
    }
}
#endif