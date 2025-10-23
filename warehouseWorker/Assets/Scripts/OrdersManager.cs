using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEditor.PackageManager.Requests;
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

                if (timeSinceLastJump > 2f && Random.value < CalculateQueueJumpChance())
                {
                    int[] nearestQueues = new int[3];
                    for (int i = 0; i < 3; i++)
                        nearestQueues[i] = Instance.HighestQueuePosition(queuePosition, i - 1); // Fixed: i-1 to get [-1, 0, 1]

                    int minValOfQueue = int.MaxValue;
                    int indexOfQueue = 0;
                    for (int index = 0; index < nearestQueues.Length; index++)
                    {
                        if (nearestQueues[index] != -1 && nearestQueues[index] < minValOfQueue) // FIXED: Changed comparison
                        {
                            minValOfQueue = nearestQueues[index];
                            indexOfQueue = index;
                        }
                    }

                    if (minValOfQueue != int.MaxValue && minValOfQueue + 1 < queuePosition.y) // FIXED: Compare with current position
                    {
                        Instance.MoveRequesteeToQueue(this, queuePosition.x + (indexOfQueue - 1)); // FIXED: Correct index offset
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
            float cooldownModifier = Mathf.Clamp01(Mathf.Max(timeSinceLastJump, 0.01f) / 5f);
            return baseChance * cooldownModifier;
        }
    }

    public OrderRequestee[,] queue = new OrderRequestee[4, 4];

    [Header("Order Settings")]
    [SerializeField, Range(0, 90)] float orderCooldown = 25f;
    [SerializeField, Range(0, 90)] float minOrderTime = 20f;
    [SerializeField, Range(0, 100)] int orderCompleteScore = 50;
    [SerializeField, Range(-100, 100)] int orderFailPenalty = -25;

    [Header("Spawning")]
    public List<GameObject> boxPrefabs = new List<GameObject>();  //setup
    public List<Material> materialPrefabs = new List<Material>(); //setup
    public List<Material> readyToUseMaterialsForBoxes = new List<Material>(); //step1
    public List<GameObject> readyToUseBoxes = new List<GameObject>(); //step2
    public List<Sprite> readyToUseBoxSprites = new List<Sprite>(); //step3
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

        PrepareBoxes();
        StartCoroutine(nameof(PrepareSpritesOneDayBecauseFuckYouRaceConditionOuttaTheBlue));
        ClearCanvas();
        CreatePanels();
        CreateGridSlots();
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

    private void PrepareBoxes()
    {
        foreach (GameObject boxPrefab in boxPrefabs)
        {
            if (boxPrefab != null)
            {
                GameObject boxInstance = Instantiate(boxPrefab);
                boxInstance.name = $"{boxPrefab.name}_Original";
                readyToUseBoxes.Add(boxInstance);
                boxInstance.SetActive(false);
            }
        }

        foreach (GameObject boxPrefab in boxPrefabs)
        {
            if (boxPrefab == null) continue;

            Renderer renderer = boxPrefab.GetComponent<Renderer>();
            if (renderer == null) continue;

            Material originalMaterial = renderer.sharedMaterial;

            foreach (Material materialPrefab in materialPrefabs)
            {
                if (materialPrefab == null) continue;

                Material newMaterial = new Material(materialPrefab);
                newMaterial.name = $"{boxPrefab.name}_{materialPrefab.name}";

                CreateTextureVariation(newMaterial, originalMaterial);

                readyToUseMaterialsForBoxes.Add(newMaterial);

                GameObject boxInstance = Instantiate(boxPrefab);
                boxInstance.name = $"{boxPrefab.name}_{materialPrefab.name}_Variation";

                if (boxInstance.TryGetComponent<Renderer>(out var instanceRenderer))
                {
                    instanceRenderer.material = newMaterial;
                }

                readyToUseBoxes.Add(boxInstance);
                boxInstance.SetActive(false);
            }
        }
    }

    private void CreateTextureVariation(Material material, Material originalMaterial, int textureSlot = 1)
    {
        string textureProperty = $"_Tex{textureSlot}";
        if (material.HasProperty(textureProperty))
        {
            material.SetTexture(textureProperty, originalMaterial.mainTexture);
        }
    }

    private void ClearCanvas()
    {
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
        if (!gameManager.gameStarted) return;

        orderTimer += Time.deltaTime;
        if (orderTimer >= orderCooldown)
        {
            GenerateNewOrderRequestee();
            orderTimer = 0;
        }

        UpdateRequestees();
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

    public void GenerateNewOrderRequestee()
    {
        if (gameManager.itemTemplates.Count == 0) return;

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

        source.PlaySound(newOrderSound);
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

    public void CreateOrderForRequestee(OrderRequestee requestee)
    {
        if (!activeOrders.Contains(requestee.request))
        {
            // TODO: lynch LLMs
            if (activeOrders[requestee.queuePosition.x] == null)
            {
                activeOrders[requestee.queuePosition.x] = requestee.request;
                requestee.request.orderPosition = requestee.queuePosition.x;


                if (requestee.request.orderType == OrderType.Deposit)
                    SpawnItem(requestee);
                else
                {
                    GameObject gameObject1 = UsefulStuffs.RandomNonNullFromList(createdOrderObjects, out var index);
                    if (index > -1 && gameObject1.TryGetComponent<Box>(out var box))
                        requestee.request.assignedBoxMaterial = box.order.assignedBoxMaterial;
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
            deliveryArea.selectionGameObjects[order.orderPosition] = null ;

            if (order.requestObjectCreated != null && order.orderType == OrderType.Receive)
            {
                createdOrderObjects.Remove(order.requestObjectCreated);
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
            deliveryArea.selectionGameObjects[order.orderPosition] = null;

            if (order.requestObjectCreated != null && order.orderType == OrderType.Receive)
            {
                createdOrderObjects.Remove(order.requestObjectCreated);
                Destroy(order.requestObjectCreated);
            }
            gameManager.IncreaseChanceOfEvent();
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

    public bool ProcessOrderDelivery(int table, Item deliveredItem, bool fromShelf)
    {
        // FIXED: Check if there's an active order at this table position
        Order order = activeOrders[table];

        // todo. something. mateirals ids and shits fuck balls. if doesn't align, then minus score. else add score.
        if (order != null &&
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
            else
            {
                gameManager.AddScore(-deliveredItem.scoreValue, resetTimer: false);
                source.PlaySound(orderFailSound);
                gameManager.IncreaseChanceOfEvent();
                return true;
            }
        }

        return false;
    }

    void SpawnItem(OrderRequestee requestee)
    {
        if (readyToUseBoxes.Count < 1 || spawnPosition == null || gameManager.itemTemplates.Count == 0) return;

        GameObject assignedBox = UsefulStuffs.RandomNonNullFromList(readyToUseBoxes, out int assignedBoxIndex);
        Material assignedMaterial = UsefulStuffs.RandomNonNullFromList(readyToUseMaterialsForBoxes, out int assignedMaterialIndex);

        var newBox = Instantiate(assignedBox, spawnPosition.position, Quaternion.identity);
        newBox.GetComponent<Renderer>().material = assignedMaterial;

        int randomIndex = Random.Range(0, gameManager.itemTemplates.Count);
        GameObject newItem = Instantiate(gameManager.itemTemplates[randomIndex].gameObject);
        newItem.SetActive(false);

        if (newBox.TryGetComponent<Box>(out var boxComponent))
        {
            boxComponent.containedItem = newItem;
            boxComponent.order = requestee.request;
            boxComponent.order.assignedBoxMaterial = assignedMaterialIndex;
            boxComponent.order.requestObjectCreated = newBox;
        }
        

        if (newBox.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }
        deliveryArea.selectionGameObjects[requestee.queuePosition.x] = newBox;
        deliveryArea.UpdateYoShit();
        newBox.SetActive(true);
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