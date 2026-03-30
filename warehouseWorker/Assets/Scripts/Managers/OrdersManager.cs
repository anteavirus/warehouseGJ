using Mirror;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Order system manager. Pure MonoBehaviour — all networking goes through
/// <see cref="GameNetworkBridge"/>.
/// </summary>
public class OrdersManager : GenericManager<OrdersManager>
{
    #region Audio clips (exposed so GameNetworkBridge can play them via RPCs)

    [HideInInspector] public AudioSource Source => source;

    [Header("Audio")]
    [SerializeField] public AudioClip[] NewOrderClips;
    [SerializeField] public AudioClip[] OrderCompleteClips;
    [SerializeField] public AudioClip[] OrderFailClips;

    #endregion

    #region Enums & data classes

    [System.Serializable]
    public enum OrderType { None = -1, Receive, Deposit }

    [System.Serializable]
    public class Order
    {
        public OrderRequestee requestee;
        public GameObject requestObjectCreated;
        public OrderType orderType;
        public int orderPosition;
        public bool specialRequirement;
        public bool orderFulfilled;
        public int assignedBoxMaterial;
    }

    [System.Serializable]
    public class OrderRequestee
    {
        public Order request;
        public Vector2Int queuePosition;
        public bool requestNotTaken;
        public float timeStart = 30;
        public float timeRemaining;
        public float impatienceModifier;
        public float lastQueueJumpTime;
        public float timeSinceLastJump => Time.time - lastQueueJumpTime;
        public bool alive = true;

        public OrderRequestee(Order order, float timeStart, float impatienceModif)
        {
            this.request = order;
            this.request.requestee = this;
            this.timeStart = this.timeRemaining = timeStart;
            this.impatienceModifier = Mathf.Clamp(impatienceModif, 0.05f, float.MaxValue);
            this.lastQueueJumpTime = Time.time;
            this.requestNotTaken = true;
        }

        public void Update()
        {
            if (!alive) return;
            timeRemaining -= Time.deltaTime * impatienceModifier;

            if (timeRemaining < 0)
            {
                if (!requestNotTaken && !request.orderFulfilled)
                    Instance.FailOrder(request);

                if (GameManager.Instance != null)
                    GameManager.Instance.IncreaseChanceOfEvent();

                Instance.AnnihilateRequestee(queuePosition);
                return;
            }

            if (requestNotTaken)
            {
                if (queuePosition.y == 0)
                {
                    Instance.CreateOrderForRequestee(this);
                    requestNotTaken = false;
                    timeRemaining = timeStart;
                    return;
                }

                if (timeSinceLastJump > 2f && Random.value < CalculateQueueJumpChance())
                {
                    int[] nearestQueues = new int[3];
                    for (int i = 0; i < 3; i++)
                        nearestQueues[i] = Instance.HighestQueuePosition(queuePosition, i - 1);

                    int minVal = int.MaxValue, idx = 0;
                    for (int i = 0; i < nearestQueues.Length; i++)
                    {
                        if (nearestQueues[i] != -1 && nearestQueues[i] < minVal)
                        {
                            minVal = nearestQueues[i];
                            idx = i;
                        }
                    }

                    if (minVal != int.MaxValue && minVal + 1 < queuePosition.y)
                    {
                        Instance.MoveRequesteeToQueue(this, queuePosition.x + (idx - 1));
                        lastQueueJumpTime = Time.time;
                    }
                }
            }
            else
            {
                if (request.orderFulfilled)
                {
                    Instance.CompleteOrder(request);
                    Instance.AnnihilateRequestee(queuePosition);
                }
            }

            // Sync slot to clients via bridge
            if (Instance != null && GameNetworkBridge.Instance != null && GameNetworkBridge.Instance.isServer)
            {
                GameNetworkBridge.Instance.RpcUpdateRequesteeSlot(
                    queuePosition.x, queuePosition.y, timeRemaining, timeStart, true);
            }
        }

        private float CalculateQueueJumpChance()
        {
            float baseChance = (timeRemaining / timeStart) * impatienceModifier * 0.1f;
            float cd = Mathf.Clamp01(Mathf.Max(timeSinceLastJump, 0.01f) / 5f);
            return baseChance * cd;
        }
    }

    #endregion

    #region Fields

    public OrderRequestee[,] queue = new OrderRequestee[4, 4];
    public DeliveryArea[] doors = new DeliveryArea[4];

    [Header("Order Settings")]
    [SerializeField, Range(0, 90)] float orderCooldown = 25f;
    [SerializeField, Range(0, 90)] float minOrderTime = 20f;
    [SerializeField, Range(0, 100)] int orderCompleteScore = 50;
    [SerializeField, Range(-100, 100)] int orderFailPenalty = -25;

    [Header("Spawning")]
    public List<GameObject> boxPrefabs = new List<GameObject>();
    public List<GameObject> readyToUseBoxes = new List<GameObject>();
    public List<Sprite> readyToUseBoxSprites = new List<Sprite>();
    public Transform spawnPosition;

    [Range(0, 10), SerializeField] float randomSpawnIntervalMax = 1;

    [Header("Layout")]
    public RectTransform canvas;
    public float margin = 10f;
    public int gridWidth => queue.GetLength(0);
    public int gridHeight => queue.GetLength(1);

    [Header("UI")]
    public Sprite depositImage;
    public Sprite requesteeImage;
    private RectTransform requesteePanel;
    private RectTransform orderPanel;
    private Image[,] requesteeSlots;
    private Image[] orderSlots;

    private GameManager gameManager;
    private Order[] activeOrders = new Order[4];
    List<GameObject> createdOrderObjects = new();
    private float orderTimer = 0;
    AudioSource source;
    internal DeliveryArea deliveryArea;

    #endregion

    #region Bridge shortcut

    private bool IsServer => GameNetworkBridge.Instance != null && GameNetworkBridge.Instance.isServer;
    private bool IsServerOnly => IsServer && !(GameNetworkBridge.Instance != null && GameNetworkBridge.Instance.isClient);

    #endregion

    #region Initialize

    public void Initialize(GameManager gm)
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Debug.LogWarning("Duplicate OrdersManager — destroyed.");
            Destroy(gameObject);
            return;
        }

        gameManager = gm;
        if (gameManager == null)
            gameManager = GameManager.Instance;

        source = GetComponent<AudioSource>();
        if (canvas.IsTrulyNull())
            canvas = GameObject.Find("OrdersCanvas")?.GetComponent<RectTransform>();

        // UI init on host / non-headless
        if (!IsServerOnly)
        {
            CreatePanels();
            CreateGridSlots();
            UpdateOrderUI();
        }

        if (IsServer)
        {
            for (int i = 0; i < queue.GetLength(0); i++)
                for (int j = 0; j < queue.GetLength(1); j++)
                    queue[i, j] = null;
        }

        doors = FindObjectsOfType<DeliveryArea>();
        PrepareBoxes();
        StartCoroutine(nameof(WaitForIcons));
    }

    IEnumerator WaitForIcons()
    {
        readyToUseBoxSprites.Clear();
        yield return new WaitUntil(() => IconManager.Instance != null);

        for (int i = 0; i < readyToUseBoxes.Count; i++)
        {
            GameObject item = readyToUseBoxes[i];
            if (item == null) continue;
            var tex = IconManager.Instance.RenderCopyToTexture(item, 128, 128);
            if (tex != null)
            {
                var spr = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                spr.name = $"{item.name}_Sprite";
                readyToUseBoxSprites.Add(spr);
            }
        }
    }

    #endregion

    #region UI — public methods called by GameNetworkBridge RPCs

    /// <summary>Updates a single requestee slot (called from bridge RPC).</summary>
    public void UpdateRequesteeSlotUI(int x, int y, float timeRemaining, float timeStart, bool exists)
    {
        if (requesteeSlots == null) return;
        if (x < 0 || x >= gridWidth || y < 0 || y >= gridHeight) return;

        var slot = requesteeSlots[x, y];
        if (!exists)
        {
            slot.color = UsefulStuffs.semiTransparent;
            slot.fillAmount = 1f;
        }
        else
        {
            float pct = timeRemaining / timeStart;
            slot.fillAmount = pct;
            slot.color = UsefulStuffs.WithAlpha(GetTimeColor(pct), 1f);
        }
    }

    /// <summary>Updates a single order slot (called from bridge RPC).</summary>
    public void UpdateOrderSlotUI(int index, int orderType, int assignedBoxMaterial, bool exists)
    {
        if (orderSlots == null) return;
        if (index < 0 || index >= orderSlots.Length) return;

        var slot = orderSlots[index];
        if (!exists)
        {
            slot.color = UsefulStuffs.semiTransparent;
            slot.sprite = null;
        }
        else
        {
            slot.color = Color.white;
            if (orderType == (int)OrderType.Receive)
            {
                if (readyToUseBoxSprites != null && assignedBoxMaterial < readyToUseBoxSprites.Count)
                    slot.sprite = readyToUseBoxSprites[assignedBoxMaterial];
            }
            else if (orderType == (int)OrderType.Deposit)
            {
                slot.sprite = depositImage;
            }
        }
    }

    /// <summary>Full UI refresh (called from bridge RPC).</summary>
    public void UpdateOrderUI()
    {
        if (requesteeSlots == null || orderSlots == null) return;

        for (int x = 0; x < gridWidth; x++)
        {
            orderSlots[x].color = UsefulStuffs.semiTransparent;
            orderSlots[x].sprite = null;
            for (int y = 0; y < gridHeight; y++)
            {
                requesteeSlots[x, y].color = UsefulStuffs.semiTransparent;
                requesteeSlots[x, y].fillAmount = 1f;
            }
        }

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                var req = queue[x, y];
                if (req != null)
                {
                    float pct = req.timeRemaining / req.timeStart;
                    requesteeSlots[x, y].fillAmount = pct;
                    requesteeSlots[x, y].color = GetTimeColor(pct);
                }
            }
        }

        for (int i = 0; i < activeOrders.Length; i++)
        {
            var order = activeOrders[i];
            if (order != null)
            {
                orderSlots[i].color = Color.white;
                if (order.orderType == OrderType.Receive)
                    orderSlots[i].sprite = readyToUseBoxSprites[order.assignedBoxMaterial];
                else if (order.orderType == OrderType.Deposit)
                    orderSlots[i].sprite = depositImage;
            }
        }
    }

    /// <summary>Syncs the entire orders snapshot (called from bridge RPC after client connects).</summary>
    public void SyncFullOrdersStateToPlayers()
    {
        for (int x = 0; x < gridWidth; x++)
            for (int y = 0; y < gridHeight; y++)
            {
                var req = queue[x, y];
                UpdateRequesteeSlotUI(x, y, req?.timeRemaining ?? 0, req?.timeStart ?? 1, req != null);
            }

        for (int i = 0; i < activeOrders.Length; i++)
        {
            var order = activeOrders[i];
            UpdateOrderSlotUI(i, order != null ? (int)order.orderType : 0,
                              order?.assignedBoxMaterial ?? 0, order != null);
        }
    }

    #endregion

    #region Boxes & sprites

    private void PrepareBoxes()
    {
        foreach (var boxPrefab in boxPrefabs)
        {
            if (boxPrefab == null) continue;
            var inst = Instantiate(boxPrefab);
            inst.name = $"{boxPrefab.name}_Template";
            readyToUseBoxes.Add(inst);
            inst.transform.SetParent(transform);
            inst.SetActive(false);

            if (inst.TryGetComponent<Box>(out var box))
                Destroy(box);
            if (inst.TryGetComponent<NetworkIdentity>(out var ni))
                Destroy(ni);
        }
    }

    #endregion

    #region UI construction (panels + slots)

    private void ClearCanvas()
    {
        if (canvas == null) return;
        foreach (Transform child in canvas)
            Destroy(child.gameObject);
    }

    private void CreatePanels()
    {
        GameObject reqObj = new GameObject("RequesteePanel");
        requesteePanel = reqObj.AddComponent<RectTransform>();
        requesteePanel.SetParent(canvas, false);
        requesteePanel.anchorMin = new Vector2(0, 0.25f);
        requesteePanel.anchorMax = new Vector2(1, 1f);

        GameObject ordObj = new GameObject("OrderPanel");
        orderPanel = ordObj.AddComponent<RectTransform>();
        orderPanel.SetParent(canvas, false);
        orderPanel.anchorMin = new Vector2(0, 0);
        orderPanel.anchorMax = new Vector2(1, 0.25f);
    }

    private void CreateGridSlots()
    {
        requesteeSlots = new Image[gridWidth, gridHeight];
        orderSlots = new Image[gridWidth];

        CreateRequesteeGrid();
        CreateOrderRow();
    }

    private void CreateRequesteeGrid()
    {
        var vert = requesteePanel.gameObject.AddComponent<VerticalLayoutGroup>();
        vert.childControlHeight = vert.childControlWidth = true;
        vert.childForceExpandHeight = vert.childForceExpandWidth = true;
        vert.reverseArrangement = true;
        vert.spacing = 5f;
        vert.padding = new RectOffset(5, 5, 5, 5);

        for (int row = 0; row < gridHeight; row++)
        {
            var rowObj = new GameObject($"Row_{row}");
            rowObj.AddComponent<RectTransform>().SetParent(requesteePanel, false);
            var horiz = rowObj.AddComponent<HorizontalLayoutGroup>();
            horiz.childControlHeight = horiz.childControlWidth = true;
            horiz.childForceExpandHeight = horiz.childForceExpandWidth = true;
            horiz.spacing = 5f;

            for (int col = 0; col < gridWidth; col++)
            {
                var slotObj = new GameObject($"RequesteeSlot_{row}_{col}");
                slotObj.AddComponent<RectTransform>().SetParent(rowObj.transform, false);

                var img = slotObj.AddComponent<Image>();
                img.sprite = requesteeImage;
                img.type = Image.Type.Filled;
                img.fillMethod = Image.FillMethod.Radial360;
                img.fillOrigin = (int)Image.Origin360.Top;

                var le = slotObj.AddComponent<LayoutElement>();
                le.preferredWidth = le.preferredHeight = 40;

                requesteeSlots[col, row] = img;
                img.color = UsefulStuffs.semiTransparent;
            }
        }
    }

    private void CreateOrderRow()
    {
        var horiz = orderPanel.gameObject.AddComponent<HorizontalLayoutGroup>();
        horiz.childControlHeight = horiz.childControlWidth = true;
        horiz.childForceExpandHeight = horiz.childForceExpandWidth = true;
        horiz.spacing = 5f;
        horiz.padding = new RectOffset(5, 5, 5, 5);

        for (int i = 0; i < gridWidth; i++)
        {
            var slotObj = new GameObject($"OrderSlot_{i}");
            slotObj.AddComponent<RectTransform>().SetParent(orderPanel, false);

            var img = slotObj.AddComponent<Image>();
            img.type = Image.Type.Simple;
            img.preserveAspect = true;

            var le = slotObj.AddComponent<LayoutElement>();
            le.preferredWidth = le.preferredHeight = 40;

            orderSlots[i] = img;
            img.color = UsefulStuffs.semiTransparent;
        }
    }

    #endregion

    #region Order update loop

    public void UpdateOrders()
    {
        if (gameManager == null) gameManager = GameManager.Instance;
        if (gameManager != null && !gameManager.GameStarted) return;

        if (canvas == null)
            canvas = GameObject.Find("OrdersCanvas")?.GetComponent<RectTransform>();

        if (IsServer)
        {
            orderTimer += Time.deltaTime;
            if (orderTimer >= orderCooldown)
            {
                GenerateNewOrderRequestee();
                orderTimer = 0;
            }
            UpdateRequestees();
        }

        UpdateOrderUI();
        // periodically push full state to clients
        if (IsServer)
            GameNetworkBridge.Instance?.RpcSyncFullOrdersState();
    }

    void UpdateRequestees()
    {
        if (!IsServer) return;
        for (int w = 0; w < queue.GetLength(0); w++)
            for (int h = 0; h < queue.GetLength(1); h++)
                queue[w, h]?.Update();
        MoveTheQueues();
    }

    #endregion

    #region Order generation

    /// <summary>Server: creates a new requestee and places them in the queue.</summary>
    public void GenerateNewOrderRequestee()
    {
        if (!IsServer) return;
        if (gameManager == null || gameManager.items.Count == 0) return;

        var spot = FindEmptyQueueSpot();
        if (!spot.HasValue) return;

        Order newOrder = new()
        {
            orderType = (OrderType)(createdOrderObjects.Count > 0 ? Random.Range(0, 2) : 1)
        };

        var req = new OrderRequestee(newOrder, minOrderTime + Random.Range(0f, 10f), Random.Range(0.8f, 1.2f))
        {
            queuePosition = spot.Value
        };

        queue[spot.Value.x, spot.Value.y] = req;
        GameNetworkBridge.Instance?.RpcUpdateRequesteeSlot(
            spot.Value.x, spot.Value.y, req.timeRemaining, req.timeStart, true);
        GameNetworkBridge.Instance?.RpcPlayOrderSound(0);
    }

    private Vector2Int? FindEmptyQueueSpot()
    {
        for (int h = 0; h < queue.GetLength(1); h++)
            for (int w = 0; w < queue.GetLength(0); w++)
                if (queue[w, h] == null)
                    return new Vector2Int(w, h);
        return null;
    }

    /// <summary>Server: converts a waiting requestee into an active order.</summary>
    public void CreateOrderForRequestee(OrderRequestee requestee)
    {
        if (!IsServer) return;
        if (activeOrders.Contains(requestee.request)) return;
        if (activeOrders[requestee.queuePosition.x] != null) return;

        activeOrders[requestee.queuePosition.x] = requestee.request;
        requestee.request.orderPosition = requestee.queuePosition.x;

        GameNetworkBridge.Instance?.RpcUpdateOrderSlot(
            requestee.queuePosition.x, (int)requestee.request.orderType,
            requestee.request.assignedBoxMaterial, true);

        if (requestee.request.orderType == OrderType.Deposit)
        {
            SpawnItem(requestee);
            GameNetworkBridge.Instance?.RpcUpdateOrderUI();
        }
        else
        {
            var go = UsefulStuffs.RandomNonNullFromList(createdOrderObjects, out var idx);
            requestee.request.requestObjectCreated = go;
            if (idx > -1 && go != null && go.TryGetComponent<Box>(out var box))
            {
                requestee.request.assignedBoxMaterial = box.order?.assignedBoxMaterial
                    ?? Random.Range(0, readyToUseBoxes.Count);
            }
            else
            {
                requestee.request.assignedBoxMaterial = Random.Range(0, readyToUseBoxes.Count);
            }
        }
    }

    #endregion

    #region Order completion / failure

    /// <summary>Server: marks order as completed.</summary>
    public void CompleteOrder(Order order)
    {
        if (!IsServer) return;
        if (!activeOrders.Contains(order)) return;

        activeOrders[order.orderPosition] = null;
        GameManager.Instance?.AddScore(orderCompleteScore, resetTimer: true, immediateReset: true);
        GameNetworkBridge.Instance?.RpcPlayOrderSound(1);

        ClearDeliverySlot(order);

        if (order.requestObjectCreated != null && order.orderType == OrderType.Receive)
            createdOrderObjects.Remove(order.requestObjectCreated);

        GameNetworkBridge.Instance?.RpcUpdateOrderSlot(order.orderPosition, 0, 0, false);

        GameManager.Instance?.timer.ResetTimer();
    }

    /// <summary>Server: marks order as failed.</summary>
    public void FailOrder(Order order)
    {
        if (!IsServer) return;
        if (!activeOrders.Contains(order)) return;

        activeOrders[order.orderPosition] = null;
        GameManager.Instance?.AddScore(orderFailPenalty, resetTimer: false);
        GameNetworkBridge.Instance?.RpcPlayOrderSound(2);

        ClearDeliverySlot(order);

        GameManager.Instance?.IncreaseChanceOfEvent();
        GameNetworkBridge.Instance?.RpcUpdateOrderSlot(order.orderPosition, 0, 0, false);
    }

    private void ClearDeliverySlot(Order order)
    {
        if (deliveryArea?.selectionGameObjects != null &&
            order.orderPosition >= 0 && order.orderPosition < deliveryArea.selectionGameObjects.Length)
        {
            deliveryArea.selectionGameObjects[order.orderPosition] = null;
        }
    }

    #endregion

    #region Order delivery — public entry point + server processing

    /// <summary>Call from anywhere. Routes through the bridge.</summary>
    public bool ProcessOrderDelivery(int table, Item deliveredItem, bool fromShelf)
    {
        if (!IsServer)
        {
            // Client: ask server via bridge.
            GameNetworkBridge.Instance?.RequestProcessOrderDelivery(table, deliveredItem.netId, fromShelf);
            return false;
        }
        return ProcessOrderDeliveryServer(table, deliveredItem.netId, fromShelf);
    }

    /// <summary>Server-side processing of a delivery.</summary>
    public bool ProcessOrderDeliveryServer(int table, uint itemNetId, bool fromShelf)
    {
        if (!IsServer) return false;
        if (table < 0 || table >= activeOrders.Length) return false;

        var order = activeOrders[table];
        if (order == null) return false;

        // Look up the actual delivered item (needed to verify material)
        Item deliveredItem = null;
        if (Mirror.NetworkServer.spawned.TryGetValue(itemNetId, out var netObj))
            deliveredItem = netObj.GetComponent<Item>();
        if (deliveredItem == null) return false;

        if (deliveredItem.order != null &&
            deliveredItem.order.assignedBoxMaterial == order.assignedBoxMaterial &&
            !order.orderFulfilled &&
            order.orderType == OrderType.Receive)
        {
            order.orderFulfilled = true;
            if (fromShelf)
                CompleteOrder(order);
        }
        return true;
    }

    #endregion

    #region Queue management

    public int HighestQueuePosition(Vector2Int pos, int side)
    {
        int targetX = pos.x + Mathf.Clamp(side, -1, 1);
        if (targetX < 0 || targetX >= queue.GetLength(0)) return -1;

        for (int h = 0; h < queue.GetLength(1); h++)
            if (queue[targetX, h] == null)
                return h;
        return queue.GetLength(1);
    }

    public void MoveRequesteeToQueue(OrderRequestee requestee, int queueIndex)
    {
        if (queueIndex < 0 || queueIndex >= queue.GetLength(0)) return;
        for (int h = 0; h < queue.GetLength(1); h++)
        {
            if (queue[queueIndex, h] == null)
            {
                queue[requestee.queuePosition.x, requestee.queuePosition.y] = null;
                queue[queueIndex, h] = requestee;
                requestee.queuePosition = new Vector2Int(queueIndex, h);
                return;
            }
        }
    }

    public void AnnihilateRequestee(Vector2Int pos)
    {
        if (pos.x < 0 || pos.x >= queue.GetLength(0) ||
            pos.y < 0 || pos.y >= queue.GetLength(1)) return;

        var req = queue[pos.x, pos.y];
        if (req != null && !req.requestNotTaken)
            activeOrders[req.request.orderPosition] = null;

        queue[pos.x, pos.y] = null;

        if (IsServer)
            GameNetworkBridge.Instance?.RpcUpdateRequesteeSlot(pos.x, pos.y, 0, 1, false);
    }

    public void MoveTheQueues()
    {
        for (int w = 0; w < queue.GetLength(0); w++)
            for (int h = 0; h < queue.GetLength(1); h++)
                if (queue[w, h] == null)
                    for (int above = h + 1; above < queue.GetLength(1); above++)
                        if (queue[w, above] != null)
                        {
                            queue[w, h] = queue[w, above];
                            queue[w, above] = null;
                            queue[w, h].queuePosition = new Vector2Int(w, h);
                            break;
                        }
    }

    #endregion

    #region Spawning

    /// <summary>Server: spawns a box + item for a deposit order.</summary>
    void SpawnItem(OrderRequestee requestee)
    {
        if (!IsServer) return;
        if (readyToUseBoxes.Count < 1 || doors.Length < 1 ||
            (gameManager != null && gameManager.items.Count == 0)) return;

        var boxPrefab = UsefulStuffs.RandomNonNullFromList(boxPrefabs, out int assignedIdx);
        if (boxPrefab == null || assignedIdx < 0 || assignedIdx >= boxPrefabs.Count) return;

        var newBox = Instantiate(boxPrefab,
            UsefulStuffs.RandomFromArray(doors).transform.position,
            Quaternion.identity);

        if (newBox.GetComponent<NetworkIdentity>() == null)
            newBox.AddComponent<NetworkIdentity>();
        if (newBox.GetComponent<NetworkTransformReliable>() == null)
            newBox.AddComponent<NetworkTransformReliable>();

        newBox.SetActive(true);
        GameNetworkBridge.Instance?.ServerSpawn(newBox);

        // Spawn contained item (hidden inside box)
        if (gameManager?.items != null && gameManager.items.Count > 0)
        {
            int ri = Random.Range(0, gameManager.items.Count);
            var newItem = Instantiate(gameManager.items[ri].gameObject);

            if (newItem.GetComponent<NetworkIdentity>() == null)
                newItem.AddComponent<NetworkIdentity>();
            if (newItem.GetComponent<NetworkTransformReliable>() == null)
                newItem.AddComponent<NetworkTransformReliable>();

            newItem.SetActive(false); // hidden inside box
        }

        if (newBox.TryGetComponent<Box>(out var box))
        {
            box.order = requestee.request;
            box.order.requestObjectCreated = newBox;
            box.order.assignedBoxMaterial = assignedIdx;
        }

        if (newBox.TryGetComponent<Rigidbody>(out var rb))
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        if (deliveryArea?.selectionGameObjects != null &&
            requestee.queuePosition.x >= 0 &&
            requestee.queuePosition.x < deliveryArea.selectionGameObjects.Length)
        {
            deliveryArea.selectionGameObjects[requestee.queuePosition.x] = newBox;
        }

        createdOrderObjects.Add(newBox);
    }

    #endregion

    #region Helpers

    private Color GetTimeColor(float pct)
    {
        float c = Mathf.Clamp01(pct);
        if (c > 0.6f) return Color.green;
        if (c > 0.3f) return UsefulStuffs.LerpColor(Color.yellow, Color.green, (c - 0.3f) / 0.3f);
        return UsefulStuffs.LerpColor(Color.red, Color.yellow, c / 0.3f);
    }

    public void ClearAllOrders()
    {
        for (int i = 0; i < queue.GetLength(0); i++)
        {
            activeOrders[i] = null;
            for (int j = 0; j < queue.GetLength(1); j++)
            {
                var req = queue[i, j];
                if (req?.request.requestObjectCreated != null)
                    Destroy(req.request.requestObjectCreated);
                queue[i, j] = null;
            }
        }
        foreach (var obj in createdOrderObjects)
            if (obj != null) Destroy(obj);
        createdOrderObjects.Clear();
    }

    #endregion
}
