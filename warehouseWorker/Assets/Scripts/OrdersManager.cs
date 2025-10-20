using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class OrdersManager : MonoBehaviour
{
    public static OrdersManager Instance;

    [System.Serializable]
    public enum OrderType
    {
        None = -1,
        Receive,
        Deposit
    }

    [System.Serializable]
    public class Order
    {
        public OrderRequestee requestee;
        public GameObject requestObjectCreated;  // assigned value only when Requestee is no longer in queue (in front of table (!inQueue && requestee != null)).
        public OrderType orderType;
        public int orderPosition;
        public int requestedItemID;
        public bool specialRequirement; // TODO item status chnagable, like easily flammable 'n shit
        public bool orderFulfilled;
    }

    [System.Serializable]
    public class OrderRequestee
    {
        public Order request;
        public Vector2Int queuePosition;
        public bool requestNotTaken;
        public float timeStart = 30;
        public float timeRemaining;
        public float impatienceModifier;  // bigger -> faster timeRemaining goes down. min value = .05
        public float lastQueueJumpTime;
        public float timeSinceLastJump => Time.time - lastQueueJumpTime;

        public OrderRequestee(Order request, float timeStart, float impatienceModif)
        {
            this.request = request;
            this.request.requestee = this;
            this.timeStart = this.timeRemaining = timeStart;
            this.impatienceModifier = Mathf.Clamp(impatienceModif, 0.05f, float.MaxValue);
            this.lastQueueJumpTime = Time.time;
            this.requestNotTaken = true;
        }

        public void Update()
        {
            timeRemaining -= Time.deltaTime * impatienceModifier;

            if (timeRemaining < 0)
            {
                // Fail the order if timer runs out
                if (!requestNotTaken && !request.orderFulfilled)
                {
                    Instance.FailOrder(request);
                }
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

                // Throws dice to change queue (if is IN this said queue)
                if (timeSinceLastJump > 2f && Random.value < CalculateQueueJumpChance())
                {
                    int[] nearestQueues = new int[3];
                    for (int i = 0; i < 3; i++)
                        nearestQueues[i] = Instance.HighestQueuePosition(queuePosition, i);   // some day , optimize this to - whenever someone list is interacted with - another list gets ++ or -- of nonnull vals. this is a minor optimization but . yeah tbh this is a minor one
                    int minValOfQueue = int.MaxValue, indexOfQueue = 0;
                    for (int index = 0; index < nearestQueues.Length; index++)
                    {
                        if (minValOfQueue < nearestQueues[index])
                        {
                            minValOfQueue = nearestQueues[index];
                            indexOfQueue = index;
                        }
                    }

                    if (minValOfQueue != int.MaxValue && minValOfQueue + 1 < nearestQueues[1])
                    {
                        Instance.MoveRequesteeToQueue(this, queuePosition.x + indexOfQueue);
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
        }

        private float CalculateQueueJumpChance()
        {
            float baseChance = (timeRemaining / timeStart) * impatienceModifier * 0.1f;
            float cooldownModifier = Mathf.Clamp01( Mathf.Max(timeSinceLastJump, 0.01f)  / 5f); //math max just incase timesincelastjump is somehow 0. i forgot if it throws an error or not
            return baseChance * cooldownModifier;
        }
    }

    public OrderRequestee[,] queue = new OrderRequestee[4, 4]; // width, height

    [Header("Order Settings")]
    [SerializeField] float orderCooldown = 25f;
    [SerializeField] float minOrderTime = 20f;
    [SerializeField] int orderCompleteScore = 50;
    [SerializeField] int orderFailPenalty = -25;

    [Header("Spawning")]
    public GameObject box;
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
    public List<GameObject> boxes = new List<GameObject>();  // TODO: fuckin. replace the "box" . i dont remember wher but replace it with this. multiple boxes with multiple textures and so on 'n so forth. no dupes
    private RectTransform requesteePanel; // 75% top section
    private RectTransform orderPanel;     // 25% bottom section
    private Image[,] requesteeSlots;
    private Image[] orderSlots;

    [Header("Audio")]
    [SerializeField] AudioClip[] newOrderSound;
    [SerializeField] AudioClip[] orderCompleteSound;
    [SerializeField] AudioClip[] orderFailSound;

    private GameManager gameManager;
    private readonly Order[] activeOrders = new Order[4];   // NOTE! update to queue's width!'
    List<GameObject> createdOrderObjects = new(); 
    private float orderTimer = 0;
    AudioSource source;
    
    public void Initialize(GameManager gm)
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Debug.Log("Dupe orders manager; killed.");
            Destroy(gameObject);
            return;
        }

        gameManager = gm;
        source = GetComponent<AudioSource>();

        for (int i = 0; i < queue.GetLength(0); i++)
        {
            for (int j = 0; j < queue.GetLength(1); j++)
            {
                queue[i, j] = null;
            }
        }
        
        ClearCanvas();
        CreatePanels();
        CreateGridSlots();
    }
    private void ClearCanvas()
    {
        foreach (Transform child in canvas)
            Destroy(child.gameObject);
    }

    private void CreatePanels()
    {
        // Create top panel (75% for requestees)
        GameObject requesteePanelObj = new GameObject("RequesteePanel");
        requesteePanel = requesteePanelObj.AddComponent<RectTransform>();
        requesteePanel.SetParent(canvas, false);
        requesteePanel.localPosition = Vector3.zero;
        requesteePanel.rotation = canvas.transform.rotation;
        requesteePanel.localScale = Vector3.one;

        GameObject orderPanelObj = new GameObject("OrderPanel");
        orderPanel = orderPanelObj.AddComponent<RectTransform>();
        orderPanel.SetParent(canvas, false);
        orderPanel.localPosition = Vector3.zero;
        orderPanel.rotation = canvas.transform.rotation;
        orderPanel.localScale = Vector3.one;
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
        var requesteeVertical = requesteePanel.gameObject.AddComponent<VerticalLayoutGroup>();
        requesteeVertical.childControlHeight = true;
        requesteeVertical.childControlWidth = true;
        requesteeVertical.childForceExpandHeight = true;
        requesteeVertical.childForceExpandWidth = true;
        requesteeVertical.reverseArrangement = true;
        requesteeVertical.spacing = 5f;
        requesteeVertical.padding = new RectOffset(5, 5, 5, 5);

        for (int row = 0; row < gridHeight; row++)
        {
            GameObject rowObj = new GameObject($"Row_{row}");
            var rowRect = rowObj.AddComponent<RectTransform>();
            rowRect.SetParent(requesteePanel, false);

            var rowHorizontal = rowObj.AddComponent<HorizontalLayoutGroup>();
            rowHorizontal.childControlHeight = true;
            rowHorizontal.childControlWidth = true;
            rowHorizontal.childForceExpandHeight = true;
            rowHorizontal.childForceExpandWidth = true;
            rowHorizontal.spacing = 5f;

            for (int col = 0; col < gridWidth; col++)
            {
                GameObject slotObj = new GameObject($"RequesteeSlot_{row}_{col}");
                var slotRect = slotObj.AddComponent<RectTransform>();
                slotRect.SetParent(rowRect, false);

                Image slotImg = slotObj.AddComponent<Image>();
                slotImg.sprite = requesteeImage;
                slotImg.type = Image.Type.Filled;
                slotImg.fillMethod = Image.FillMethod.Radial360;
                slotImg.fillOrigin = (int)Image.Origin360.Top;

                requesteeSlots[col, row] = slotImg; // Note: [x,y] = [col,row]
                slotImg.gameObject.GetComponent<Image>().color = UsefulStuffs.semiTransparent;
            }
        }
    }

    private void CreateOrderRow()
    {
        var orderHorizontal = orderPanel.gameObject.AddComponent<HorizontalLayoutGroup>();
        orderHorizontal.childControlHeight = true;
        orderHorizontal.childControlWidth = true;
        orderHorizontal.childForceExpandHeight = true;
        orderHorizontal.childForceExpandWidth = true;
        orderHorizontal.spacing = 5f;
        orderHorizontal.padding = new RectOffset(5, 5, 5, 5);

        for (int i = 0; i < gridWidth; i++)
        {
            GameObject slotObj = new GameObject($"OrderSlot_{i}");
            var slotRect = slotObj.AddComponent<RectTransform>();
            slotRect.SetParent(orderPanel, false);

            Image slotImg = slotObj.AddComponent<Image>();
            slotImg.type = Image.Type.Filled;
            slotImg.fillMethod = Image.FillMethod.Radial360;
            slotImg.fillOrigin = (int)Image.Origin360.Top;

            orderSlots[i] = slotImg;
            slotImg.gameObject.GetComponent<Image>().color = UsefulStuffs.semiTransparent;
        }
    }

    public void UpdateOrders()
    {
        if (!gameManager.gameStarted) return;

        orderTimer += Time.deltaTime;
        if (orderTimer >= orderCooldown)
        {
            GenerateNewOrderRequestee();
            orderTimer = 0;
        }

        UpdateRequestees();
        UpdateActiveOrders();
        UpdateOrderUI();
    }

    void UpdateRequestees()
    {
        for (int width = 0; width < queue.GetLength(0); width++)
        {
            for (int height = 0; height < queue.GetLength(1); height++)
            {
                var requestee = queue[width, height];
                if (requestee == null) continue;

                requestee.Update();
            }
        }

        MoveTheQueues();
    }

    void GenerateNewOrderRequestee()
    {
        if (gameManager.itemTemplates.Count == 0) return;

        Vector2Int? emptySpot = FindEmptyQueueSpot();
        if (!emptySpot.HasValue) return; // Queue is full

        Order newOrder = new()
        {
            requestedItemID = gameManager.itemTemplates[Random.Range(0, gameManager.itemTemplates.Count)].ID,
            orderType = (OrderType)Random.Range(0, 1) // Randomly assign Receive or Deposit   // Fuck deposit all my omies ate deposit
        };

        OrderRequestee newRequestee = new(newOrder, minOrderTime + Random.Range(0f, 10f), Random.Range(0.8f, 1.2f))
        {
            queuePosition = emptySpot.Value
        };
        queue[emptySpot.Value.x, emptySpot.Value.y] = newRequestee;

        source.PlaySound(newOrderSound);
    }

    private Vector2Int? FindEmptyQueueSpot()
    {
        List<Vector2Int> allPositions = new();
        for (int width = 0; width < queue.GetLength(0); width++)
        {
            for (int height = 0; height < queue.GetLength(1); height++)
            {
                allPositions.Add(new Vector2Int(width, height));
            }
        }

        allPositions = UsefulStuffs.ShuffleList(allPositions);

        foreach (Vector2Int pos in allPositions)
        {
            if (queue[pos.x, pos.y] == null)
                return pos;
        }

        return null;
    }

    public void CreateOrderForRequestee(OrderRequestee requestee)
    {
        if (!activeOrders.Contains(requestee.request))
        {
            Debug.Log($"Setting orderPosition to: {requestee.request.orderPosition} for queue position: {requestee.queuePosition}");
            activeOrders[requestee.queuePosition.x] = requestee.request;
            requestee.request.orderPosition = requestee.queuePosition.x;
            Debug.Log($"request orderpos is now {requestee.queuePosition.x} ");
            // TODO: deposit can be whatever, but we actually save the deposit, rewrite it as receive and at some point throw it in as an actual mission
            // actually yeah we need to save the objects we create
            if (requestee.request.requestObjectCreated == null)
            {
                // TODO: create the box icon requested or deposit symbol on appropriate queue table
                SpawnItem(requestee);
            }
        }
    }

    void UpdateActiveOrders()
    {
        foreach (Order order in activeOrders.ToList())
        {
            if (order == null) continue;
            if (order.requestee != null)
            {
                if (order.requestee.timeRemaining <= 0 && !order.orderFulfilled)
                {
                    FailOrder(order);
                }
            }
            else
            {
                for (int tick = 0; tick < activeOrders.Length; tick++)
                {
                    if (activeOrders[tick] == order)
                    {
                        activeOrders[tick] = null;
                        break;
                    }
                }
            }
        }
    }

    public void CompleteOrder(Order order)
    {
        if (activeOrders.Contains(order))
        {
            activeOrders[order.orderPosition] = null;
            gameManager.AddScore(orderCompleteScore, resetTimer: true, immediateReset: true);
            source.PlaySound(orderCompleteSound);
            requesteeSlots[order.orderPosition, 0].color = UsefulStuffs.semiTransparent;
            requesteeSlots[order.orderPosition, 0].fillAmount = 1;

            if (order.requestObjectCreated != null)
            {
                Destroy(order.requestObjectCreated);
            }
        }
    }

    public void FailOrder(Order order)
    {
        if (activeOrders.Contains(order))
        {
            activeOrders[order.orderPosition] = null;
            gameManager.AddScore(orderFailPenalty, resetTimer: false);
            source.PlaySound(orderFailSound);
            requesteeSlots[order.orderPosition, 0].color = UsefulStuffs.semiTransparent;
            requesteeSlots[order.orderPosition, 0].fillAmount = 1;

            if (order.requestObjectCreated != null)
            {
                Destroy(order.requestObjectCreated);
            }
        }
    }

    /// <summary>
    /// Asks queue for an int that represents the furthest position from start.
    /// </summary>
    /// <param name="side"> int to be clamped to [-1, 1]</param>
    /// <returns>Null position from start based on requestee position. -1 if invalid (i.e. impossible to reach).</returns>
    public int HighestQueuePosition(Vector2Int reqPosition, int side)
    {
        int offset = Mathf.Clamp(side, -1, 1); // Mathf math includes non float returns. That's my level of spaghetticoding.
        int targetX = reqPosition.x + offset;

        if (targetX < 0 || targetX >= queue.GetLength(0)) return -1;  // Out of bounds? Out of mind.

        for (int height = 0; height < queue.GetLength(1); height++)
        {
            if (queue[targetX, height] == null)
                return height;
        }
        return queue.GetLength(1); // This queue is full.
    }

    public void MoveRequesteeToQueue(OrderRequestee requestee, int queueIndex)
    {
        if (queueIndex < 0 || queueIndex >= queue.GetLength(0)) return;

        for (int height = 0; height < queue.GetLength(1); height++)
        {
            if (queue[queueIndex, height] == null)
            {
                queue[requestee.queuePosition.x, requestee.queuePosition.y] = null;
                queue[queueIndex, height] = requestee;
                requestee.queuePosition = new Vector2Int(queueIndex, height);
                requestee.request.orderPosition = queueIndex;
                UpdateOrderUI();
                return;
            }
        }

        Debug.Log($"Failed to move {requestee} to queue {queueIndex}, assumedly it's because the queue index requested to move to was full");
    }

    public void AnnihilateRequestee(Vector2Int requesteePos)
    {
        if (requesteePos.x >= 0 && requesteePos.x < queue.GetLength(0) &&
            requesteePos.y >= 0 && requesteePos.y < queue.GetLength(1))
        {
            queue[requesteePos.x, requesteePos.y] = null;
        }
    }

    public void MoveTheQueues()
    {
        for (int width = 0; width < queue.GetLength(0); width++)
        {
            for (int height = 0; height < queue.GetLength(1); height++)
            {
                if (queue[width, height] == null)
                {
                    for (int aboveHeight = height + 1; aboveHeight < queue.GetLength(1); aboveHeight++)
                    {
                        if (queue[width, aboveHeight] != null)
                        {
                            queue[width, height] = queue[width, aboveHeight];
                            queue[width, aboveHeight] = null;
                            queue[width, height].queuePosition = new Vector2Int(width, height);
                            // TODO: move progress as well, if null = full
                            break;
                        }
                    }
                }
            }
        }

        UpdateOrderUI();
    }

    public bool ProcessOrderDelivery(int table, Item deliveredItem, bool fromShelf)
    {
        Order order = queue[table, 0]?.request;
        Debug.Log($"{table} {deliveredItem} {fromShelf} {order}");
        if (order != null && order.requestedItemID == deliveredItem.ID && !order.orderFulfilled && order.orderType == OrderType.Receive)
        {
            order.orderFulfilled = true;

            if (fromShelf)
            {
                CompleteOrder(order);
            }
            else
            {
                gameManager.AddScore(-deliveredItem.scoreValue, resetTimer: false);
                source.PlaySound(orderFailSound);
            }
            return true;
        }
    
        return false;
    }

    public void SpawnInitialItem()
    {
        GenerateNewOrderRequestee();
    }

    public void SpawnItemAfterDelay()
    {
        StartCoroutine(SpawnWithDelay());
    }

    IEnumerator SpawnWithDelay()
    {
        yield return new WaitForSeconds(Random.Range(0, randomSpawnIntervalMax));
        GenerateNewOrderRequestee();
    }

    void SpawnItem(OrderRequestee requestee)
    {
        if (box == null || spawnPosition == null || gameManager.itemTemplates.Count == 0) return;

        int randomIndex = Random.Range(0, gameManager.itemTemplates.Count);
        var newBox = Instantiate(box, spawnPosition.position, Quaternion.identity);
        GameObject newItem = Instantiate(gameManager.itemTemplates[randomIndex].gameObject);
        newItem.SetActive(false);

        if (newBox.TryGetComponent<Box>(out var boxComponent))
        {
            boxComponent.containedItem = newItem;
            boxComponent.containedItem.GetComponent<Item>().order = requestee.request;
        }

        if (newBox.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }
    }

    private void UpdateOrderUI()
    {
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                OrderRequestee requestee = queue[x, y];
                if (requestee == null)
                {
                    requesteeSlots[x, y].color = UsefulStuffs.semiTransparent;
                    if (y == 0) orderSlots[x].color = UsefulStuffs.semiTransparent;
                    if (y == 0) orderSlots[x].sprite = requesteeImage;
                    continue;
                }

                requesteeSlots[x, y].color = Color.white;

                Image radialImage = requesteeSlots[x, y].transform.GetComponent<Image>();
                if (radialImage != null && requestee != null)
                {
                    float timePercent = requestee.timeRemaining / requestee.timeStart;
                    radialImage.fillAmount = timePercent;

                    radialImage.color = GetTimeColor(timePercent);
                }
                    
                Order order = requestee.request;
                if (!requestee.requestNotTaken)
                {
                    orderSlots[x].color = Color.white;

                    Item item = gameManager.ReturnItemById(order.requestedItemID);
                    Sprite itemSprite = requestee.request.orderType switch
                    {
                        OrderType.Receive => IconManager.Instance?.previewSprites.Find(i => i.name.StartsWith(IconManager.IconNamePrefix(item.ID.ToString()))),
                        OrderType.Deposit => depositImage,
                        _ => null,
                    };
                    if (item != null && itemSprite != null)
                    {
                        orderSlots[x].sprite = itemSprite;
                    }
                }
            }
        }
    }

    private Color GetTimeColor(float timePercent)
    {
        float clampedPercent = Mathf.Clamp01(timePercent);

        if (clampedPercent > 0.6f)
            return Color.green;
        else if (clampedPercent > 0.3f)
            return UsefulStuffs.LerpColor(Color.yellow, Color.green, (clampedPercent - 0.3f) / 0.3f);
        else
            return UsefulStuffs.LerpColor(Color.red, Color.yellow, clampedPercent / 0.3f);
    }

    public void ClearAllOrders()
    {
        for (int i = 0; i < queue.GetLength(0); i++)
        {
            activeOrders[i] = null;
            for (int j = 0; j < queue.GetLength(1); j++)
            {
                if (queue[i, j] != null)
                {
                    if (queue[i, j].request.requestObjectCreated != null)
                    {
                        Destroy(queue[i, j].request.requestObjectCreated);
                    }
                    queue[i, j] = null;
                }
            }
        }
    }
}
