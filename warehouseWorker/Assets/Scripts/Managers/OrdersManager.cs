using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class OrdersManager : GenericManager<OrdersManager>
{
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
        public Order request; // this creates a loop of classes, preferrably needs reworking but lowest of priorities as it works... barely acceptably, and that's all i need
        public Vector2Int queuePosition;
        public bool requestNotTaken;
        public float timeStart = 30;
        public float timeRemaining;
        public float impatienceModifier;
        public float lastQueueJumpTime;
        public float timeSinceLastJump => Time.time - lastQueueJumpTime;
        public bool alive = true;  // TODO: fill the slots with impossible amounts of time to run out, impatienceModifier hardset to 0 (not during class creation), fill out queues not in use with not alive requestees. [not in use queues => queues.amount - players.ingame (if someone joins, clear out. leaves, fill up, get rid of other requestees in queue, leave the requestee at the table.)]

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
            if (!alive) return;
            timeRemaining -= Time.deltaTime * impatienceModifier;

            if (timeRemaining < 0)
            {
                if (!requestNotTaken && !request.orderFulfilled)
                {
                    Instance.FailOrder(request);
                }
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

                    int minValOfQueue = int.MaxValue;
                    int indexOfQueue = 0;
                    for (int index = 0; index < nearestQueues.Length; index++)
                    {
                        if (nearestQueues[index] != -1 && nearestQueues[index] < minValOfQueue)
                        {
                            minValOfQueue = nearestQueues[index];
                            indexOfQueue = index;
                        }
                    }

                    if (minValOfQueue != int.MaxValue && minValOfQueue + 1 < queuePosition.y)
                    {
                        Instance.MoveRequesteeToQueue(this, queuePosition.x + (indexOfQueue - 1));
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

            if (Instance != null && Instance.isServer)
            {
                Instance.RpcUpdateRequesteeSlot(queuePosition.x, queuePosition.y, timeRemaining, timeStart, true);
            }
        }

        private float CalculateQueueJumpChance()
        {
            float baseChance = (timeRemaining / timeStart) * impatienceModifier * 0.1f;
            float cooldownModifier = Mathf.Clamp01(Mathf.Max(timeSinceLastJump, 0.01f) / 5f);
            return baseChance * cooldownModifier;
        }
    }

    public OrderRequestee[,] queue = new OrderRequestee[4, 4];
    public DeliveryArea[] doors = new DeliveryArea[4];  // hardcoded 4 because there's only 4 holes in the walls for the packages. theoretically it should be 4 for the 4 players that can play max, but... i'd like to do multiplayer whenever i'll start rotting

    [Header("Order Settings")]
    [SerializeField, Range(0, 90)] float orderCooldown = 25f;
    [SerializeField, Range(0, 90)] float minOrderTime = 20f;
    [SerializeField, Range(0, 100)] int orderCompleteScore = 50;
    [SerializeField, Range(-100, 100)] int orderFailPenalty = -25;

    [Header("Spawning")]
    public List<GameObject> boxPrefabs = new List<GameObject>();  //setup
    public List<GameObject> readyToUseBoxes = new List<GameObject>(); //step2
    public List<Sprite> readyToUseBoxSprites = new List<Sprite>(); //step3
    public Transform spawnPosition;   // Uh, they should spawn at the door spawn areas instead... Spawn position is best for players AND orders made by players.  I think.
                                      // We really do need a Game Design Document...
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

    [Header("Audio")]
    [SerializeField] AudioClip[] newOrderSound;
    [SerializeField] AudioClip[] orderCompleteSound;
    [SerializeField] AudioClip[] orderFailSound;

    private GameManager gameManager;
    private Order[] activeOrders = new Order[4];
    List<GameObject> createdOrderObjects = new();
    private float orderTimer = 0;
    AudioSource source;
    internal DeliveryArea deliveryArea;

    public void Initialize(GameManager gm)
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Debug.LogWarning("Dupe orders manager; killed.");
            Destroy(gameObject);
            return;
        }

        gameManager = gm;
        if (gameManager == null)
            gameManager = GameManager.Instance;
        source = GetComponent<AudioSource>();
        if (canvas.IsTrulyNull())
        {
            canvas = GameObject.Find("OrdersCanvas").GetComponent<RectTransform>();
        }

        // Don't initialize UI on server (headless) unless it's a host
        if (!isServerOnly) // Host
        {
            CreatePanels(); 
            CreateGridSlots();
            UpdateOrderUI(); // Initial UI update
        }

        // Server-only initialization
        if (isServer)
        {
            for (int i = 0; i < queue.GetLength(0); i++)
            {
                for (int j = 0; j < queue.GetLength(1); j++)
                {
                    queue[i, j] = null;
                }
            }

        }
        doors = FindObjectsOfType<DeliveryArea>();
        PrepareBoxes();
        StartCoroutine(nameof(PrepareSpritesOneDayBecauseFuckYouRaceConditionOuttaTheBlue));
    }

    IEnumerator PrepareSpritesOneDayBecauseFuckYouRaceConditionOuttaTheBlue()
    {
        readyToUseBoxSprites.Clear();
        yield return new WaitUntil(() => IconManager.Instance != null);

        for (int i = 0; i < readyToUseBoxes.Count; i++)
        {
            GameObject item = readyToUseBoxes[i];
            if (item == null) continue;
            var texture = IconManager.Instance.RenderCopyToTexture(item, 128, 128);
            if (texture != null)
            {
                Rect rect = new Rect(0, 0, texture.width, texture.height);
                Sprite itemSprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f));
                itemSprite.name = $"{item.name}_Sprite";
                readyToUseBoxSprites.Add(itemSprite);
            }
        }
    }

    [ClientRpc]
    public void SyncFullOrdersStateToPlayers()
    {
        // Send all requestee slots
        for (int x = 0; x < gridWidth; x++)
            for (int y = 0; y < gridHeight; y++)
            {
                var req = queue[x, y];
                if (req != null)
                    TargetUpdateRequesteeSlot(x, y, req.timeRemaining, req.timeStart, true);
                else
                    TargetUpdateRequesteeSlot(x, y, 0, 1, false);
            }

        // Send all active orders
        for (int i = 0; i < activeOrders.Length; i++)
        {
            var order = activeOrders[i];
            if (order != null)
                TargetUpdateOrderSlot(i, (int)order.orderType, order.assignedBoxMaterial, true);
            else
                TargetUpdateOrderSlot(i, 0, 0, false);
        }
    }

    [TargetRpc]
    private void TargetUpdateRequesteeSlot(int x, int y, float timeRemaining, float timeStart, bool exists)
        => RpcUpdateRequesteeSlot(x, y, timeRemaining, timeStart, exists);

    [TargetRpc]
    private void TargetUpdateOrderSlot(int index, int orderType, int assignedBoxMaterial, bool exists)
        => RpcUpdateOrderSlot(index, orderType, assignedBoxMaterial, exists);


    private void PrepareBoxes()
    {
        foreach (GameObject boxPrefab in boxPrefabs)
        {
            if (boxPrefab != null)
            {
                GameObject boxInstance = Instantiate(boxPrefab);
                boxInstance.name = $"{boxPrefab.name}_Template";
                readyToUseBoxes.Add(boxInstance);
                boxInstance.transform.SetParent(transform);
                boxInstance.SetActive(false);

                if (boxInstance.TryGetComponent<Box>(out var box))
                {
                    Destroy(box);
                }

                // Remove NetworkIdentity if it exists on the template
                if (boxInstance.TryGetComponent<NetworkIdentity>(out var netIdentity))
                {
                    Destroy(netIdentity);
                }
            }
        }
    }

    private void ClearCanvas()
    {
        if (canvas == null) return;
        foreach (Transform child in canvas)
            Destroy(child.gameObject);
    }

    private void CreatePanels()
    {
        GameObject requesteePanelObj = new GameObject("RequesteePanel");
        requesteePanel = requesteePanelObj.AddComponent<RectTransform>();
        requesteePanel.SetParent(canvas, false);
        requesteePanel.anchorMin = new Vector2(0, 0.25f);
        requesteePanel.anchorMax = new Vector2(1, 1f);
        requesteePanel.offsetMin = Vector2.zero;
        requesteePanel.offsetMax = Vector2.zero;

        GameObject orderPanelObj = new GameObject("OrderPanel");
        orderPanel = orderPanelObj.AddComponent<RectTransform>();
        orderPanel.SetParent(canvas, false);
        orderPanel.anchorMin = new Vector2(0, 0);
        orderPanel.anchorMax = new Vector2(1, 0.25f);
        orderPanel.offsetMin = Vector2.zero;
        orderPanel.offsetMax = Vector2.zero;
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

                var element = slotObj.AddComponent<LayoutElement>();
                element.preferredWidth = element.preferredHeight = 40;

                requesteeSlots[col, row] = slotImg;
                slotImg.color = UsefulStuffs.semiTransparent;
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
            slotImg.type = Image.Type.Simple;
            slotImg.preserveAspect = true;

            var element = slotObj.AddComponent<LayoutElement>();
            element.preferredWidth = element.preferredHeight = 40;

            orderSlots[i] = slotImg;
            slotImg.color = UsefulStuffs.semiTransparent;
        }
    }

    public void UpdateOrders()
    {
        if (gameManager == null) gameManager = GameManager.Instance;
        if (!gameManager.gameStarted) return;

        if (canvas == null)
            canvas = GameObject.Find("OrdersCanvas")?.GetComponent<RectTransform>();

        // Server handles order generation and updates
        if (isServer)
        {
            orderTimer += Time.deltaTime;
            if (orderTimer >= orderCooldown)
            {
                GenerateNewOrderRequestee();
                orderTimer = 0;
            }

            // Server updates requestees
            UpdateRequestees();
        }

        // All clients update UI
        UpdateOrderUI();
        SyncFullOrdersStateToPlayers(); //well. 
    }

    void UpdateRequestees()
    {
        if (!isServer) return;
        
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

    [Server]
    public void GenerateNewOrderRequestee()
    {
        if (gameManager.items.Count == 0) return;

        Vector2Int? emptySpot = FindEmptyQueueSpot();
        if (!emptySpot.HasValue) return;

        Order newOrder = new()
        {
            orderType = (OrderType)  (createdOrderObjects.Count > 0 ? Random.Range(0, 2) : 1)
        };

        OrderRequestee newRequestee = new(newOrder, minOrderTime + Random.Range(0f, 10f), Random.Range(0.8f, 1.2f))
        {
            queuePosition = emptySpot.Value
        };

        queue[emptySpot.Value.x, emptySpot.Value.y] = newRequestee;
        RpcUpdateRequesteeSlot(emptySpot.Value.x, emptySpot.Value.y,
                               newRequestee.timeRemaining, newRequestee.timeStart, true);
        RpcPlayOrderSound(0);
    }
    
    [ClientRpc]
    private void RpcPlayOrderSound(int soundIndex)
    {
        if (source == null) source = GetComponent<AudioSource>();
        
        switch (soundIndex)
        {
            case 0: source.PlaySound(newOrderSound); break;
            case 1: source.PlaySound(orderCompleteSound); break;
            case 2: source.PlaySound(orderFailSound); break;
        }
    }

    private Vector2Int? FindEmptyQueueSpot()
    {
        for (int height = 0; height < queue.GetLength(1); height++)
        {
            for (int width = 0; width < queue.GetLength(0); width++)
            {
                if (queue[width, height] == null)
                    return new Vector2Int(width, height);
            }
        }
        return null;
    }

    [Server]
    public void CreateOrderForRequestee(OrderRequestee requestee)
    {
        if (!activeOrders.Contains(requestee.request))
        {
            if (activeOrders[requestee.queuePosition.x] == null)
            {
                activeOrders[requestee.queuePosition.x] = requestee.request;
                requestee.request.orderPosition = requestee.queuePosition.x;
                RpcUpdateOrderSlot(requestee.queuePosition.x, (int)requestee.request.orderType,
                   requestee.request.assignedBoxMaterial, true);

                if (requestee.request.orderType == OrderType.Deposit)
                {
                    // Make sure to spawn the item on the server only
                    SpawnItem(requestee);

                    // IMPORTANT: Sync the order UI to all clients
                    RpcUpdateOrderUI();
                }
                else
                {
                    GameObject gameObject1 = UsefulStuffs.RandomNonNullFromList(createdOrderObjects, out var index);
                    requestee.request.requestObjectCreated = gameObject1;
                    if (index > -1 && gameObject1 != null && gameObject1.TryGetComponent<Box>(out var box))
                    {
                        if (box.order != null)
                            requestee.request.assignedBoxMaterial = box.order.assignedBoxMaterial;
                        else
                            requestee.request.assignedBoxMaterial = Random.Range(0, readyToUseBoxes.Count);
                    }
                    else
                    {
                        // Handle case where no box is available
                        requestee.request.assignedBoxMaterial = Random.Range(0, readyToUseBoxes.Count);
                    }
                }
            }
        }
    }

    // FUCK, I'm getting confused. I made the Delivery Area destroy the gameObjects, but THIS destroys the object from the list of pullable objects??
    // Why am I so scatterbrained... This fucking sucks, needs rework, and probably have more thought put into
    // tl;dr Shit must stay until customers receive their fucking shit, and if they don't they whine and bitch to the eldrich gods to fuck over the players
    // EDITED: CompleteOrder now only runs on server
    [Server]
    public void CompleteOrder(Order order)
    {
        if (activeOrders.Contains(order))
        {
            activeOrders[order.orderPosition] = null;
            gameManager.AddScore(orderCompleteScore, resetTimer: true, immediateReset: true);
            RpcPlayOrderSound(1); // 1 = orderCompleteSound
            if (deliveryArea != null)
                deliveryArea.selectionGameObjects[order.orderPosition] = null;

            if (order.requestObjectCreated != null && order.orderType == OrderType.Receive)
            {
                createdOrderObjects.Remove(order.requestObjectCreated);
            }
            RpcUpdateOrderSlot(order.orderPosition, 0, 0, false);
        }
    }

    // EDITED: FailOrder now only runs on server
    [Server]
    public void FailOrder(Order order)
    {
        if (activeOrders.Contains(order))
        {
            activeOrders[order.orderPosition] = null;
            gameManager.AddScore(orderFailPenalty, resetTimer: false);
            RpcPlayOrderSound(2); // 2 = orderFailSound
            if (deliveryArea != null)
                deliveryArea.selectionGameObjects[order.orderPosition] = null;

            gameManager.IncreaseChanceOfEvent();
            RpcUpdateOrderSlot(order.orderPosition, 0, 0, false);
        }
    }

    public int HighestQueuePosition(Vector2Int reqPosition, int side)
    {
        int offset = Mathf.Clamp(side, -1, 1);
        int targetX = reqPosition.x + offset;

        if (targetX < 0 || targetX >= queue.GetLength(0)) return -1;

        for (int height = 0; height < queue.GetLength(1); height++)
        {
            if (queue[targetX, height] == null)
                return height;
        }
        return queue.GetLength(1);
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
                return;
            }
        }
    }

    public void AnnihilateRequestee(Vector2Int requesteePos)
    {
        if (requesteePos.x >= 0 && requesteePos.x < queue.GetLength(0) &&
            requesteePos.y >= 0 && requesteePos.y < queue.GetLength(1))
        {
            // FIXED: Clear from activeOrders if this was an active order
            var requestee = queue[requesteePos.x, requesteePos.y];
            if (requestee != null && !requestee.requestNotTaken)
            {
                activeOrders[requestee.request.orderPosition] = null;
            }

            queue[requesteePos.x, requesteePos.y] = null;
        }

        if (isServer)
        {
            RpcUpdateRequesteeSlot(requesteePos.x, requesteePos.y, 0, 1, false);
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
                            break;
                        }
                    }
                }
            }
        }
    }

    // EDITED: ProcessOrderDelivery now properly handles server/client calls
    public bool ProcessOrderDelivery(int table, Item deliveredItem, bool fromShelf)
    {
        if (!isServer)
        {
            // Client requests server to process delivery
            CmdProcessOrderDelivery(table, deliveredItem.netId, fromShelf);
            return false; // Client doesn't know result yet
        }
        
        // Server processes delivery
        return ProcessOrderDeliveryServer(table, deliveredItem, fromShelf);
    }
    
    [Command(requiresAuthority = false)]
    private void CmdProcessOrderDelivery(int table, uint itemNetId, bool fromShelf)
    {
        if (!NetworkServer.spawned.ContainsKey(itemNetId)) return;
        
        GameObject itemObj = NetworkServer.spawned[itemNetId].gameObject;
        Item deliveredItem = itemObj.GetComponent<Item>();
        if (deliveredItem == null) return;
        
        ProcessOrderDeliveryServer(table, deliveredItem, fromShelf);
    }
    
    [Server]
    private bool ProcessOrderDeliveryServer(int table, Item deliveredItem, bool fromShelf)
    {
        // FIXED: Check if there's an active order at this table position
        if (table < 0 || table >= activeOrders.Length) return false;
        
        Order order = activeOrders[table];

        // todo. something. mateirals ids and shits fuck balls. if doesn't align, then minus score. else add score.
        if (order != null &&
            deliveredItem.order != null &&
            deliveredItem.order.assignedBoxMaterial == order.assignedBoxMaterial &&
            !order.orderFulfilled &&
            order.orderType == OrderType.Receive)
        {
            order.orderFulfilled = true;

            if (fromShelf)
            {
                CompleteOrder(order);
                return true;
            }
        }

        return false;
    }

    [ClientRpc]
    private void RpcUpdateOrderUI()
    {
        UpdateOrderUI();
    }

    [ClientRpc]
    private void RpcUpdateRequesteeSlot(int x, int y, float timeRemaining, float timeStart, bool exists)
    {
        if (requesteeSlots == null) return; // UI not yet initialized

        if (x < 0 || x >= gridWidth || y < 0 || y >= gridHeight) return;

        var slot = requesteeSlots[x, y];
        if (!exists)
        {
            slot.color = UsefulStuffs.semiTransparent;
            slot.fillAmount = 1f;
        }
        else
        {
            float percent = timeRemaining / timeStart;
            slot.fillAmount = percent;
            slot.color = GetTimeColor(percent);
            slot.color = UsefulStuffs.WithAlpha(slot.color, 1f);
        }
    }

    [ClientRpc]
    private void RpcUpdateOrderSlot(int index, int orderType, int assignedBoxMaterial, bool exists)
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

    [Server]
    void SpawnItem(OrderRequestee requestee)
    {
        if (readyToUseBoxes.Count < 1 || doors.Length < 1 || gameManager.items.Count == 0) return;

        GameObject assignedBoxTemplate = UsefulStuffs.RandomNonNullFromList(readyToUseBoxes, out int assignedBoxIndex);
        if (assignedBoxTemplate == null) return;

        // Get the original prefab from boxPrefabs, not the template
        GameObject boxPrefab = boxPrefabs[assignedBoxIndex];

        // Instantiate the box from the registered network prefab
        var newBox = Instantiate(boxPrefab, UsefulStuffs.RandomFromArray(doors).transform.position, Quaternion.identity);

        // Make sure the box has NetworkIdentity and NetworkTransform components
        if (newBox.GetComponent<NetworkIdentity>() == null)
        {
            newBox.AddComponent<NetworkIdentity>();
        }

        // Add NetworkTransform if not present
        if (newBox.GetComponent<NetworkTransformReliable>() == null)
        {
            newBox.AddComponent<NetworkTransformReliable>();
        }

        newBox.SetActive(true);
        NetworkServer.Spawn(newBox);

        // Spawn item if gameManager.itemTemplates exists
        GameObject newItem = null;
        if (gameManager.items != null && gameManager.items.Count > 0)
        {
            int randomIndex = Random.Range(0, gameManager.items.Count);
            newItem = Instantiate(gameManager.items[randomIndex].gameObject);

            if (newItem.GetComponent<NetworkIdentity>() == null)
            {
                newItem.AddComponent<NetworkIdentity>();
            }

            if (newItem.GetComponent<NetworkTransformReliable>() == null)
            {
                newItem.AddComponent<NetworkTransformReliable>();
            }

            newItem.SetActive(true);
            NetworkServer.Spawn(newItem);
            newItem.SetActive(false); // Item is inside the box
        }

        if (newBox.TryGetComponent<Box>(out var boxComponent))
        {
            boxComponent.containedItem = newItem;
            boxComponent.order = requestee.request;
            boxComponent.order.requestObjectCreated = newBox;
            boxComponent.order.assignedBoxMaterial = assignedBoxIndex;
        }

        if (newBox.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        if (deliveryArea != null)
        {
            deliveryArea.selectionGameObjects[requestee.queuePosition.x] = newBox;
        }

        createdOrderObjects.Add(newBox);
    }

    private void UpdateOrderUI()
    {
        // Reset all slots first
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

        // Update requestee slots
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                OrderRequestee requestee = queue[x, y];
                if (requestee != null)
                {
                    requesteeSlots[x, y].color = Color.white;
                    float timePercent = requestee.timeRemaining / requestee.timeStart;
                    requesteeSlots[x, y].fillAmount = timePercent;
                    requesteeSlots[x, y].color = GetTimeColor(timePercent);
                }
            }
        }

        // Update order slots (active orders)
        for (int i = 0; i < activeOrders.Length; i++)
        {
            Order order = activeOrders[i];
            if (order != null)
            {
                orderSlots[i].color = Color.white;
                    
                if (order.orderType == OrderType.Receive)
                {
                    orderSlots[i].sprite = readyToUseBoxSprites[order.assignedBoxMaterial];
                }
                else if (order.orderType == OrderType.Deposit)
                {
                    orderSlots[i].sprite = depositImage;
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

        foreach (var obj in createdOrderObjects)
        {
            if (obj != null) Destroy(obj);
        }
        createdOrderObjects.Clear();
    }
}