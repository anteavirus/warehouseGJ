using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

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
        public OrderRequestee(Order request, float timeStart, float impatienceModif)
        {
            this.request = request;
            this.request.requestee = this;
            this.timeStart = this.timeRemaining = timeStart;
            this.impatienceModifier = Mathf.Clamp(impatienceModif, 0.05f, float.MaxValue);
        }

        public void Update()
        {
            timeRemaining -= Time.deltaTime * impatienceModifier;

            if (timeRemaining < 0)
            {
                Instance.AnnihilateRequestee(queuePosition);
                // TODO: OrderRequestee becomes null, and instantly fails Order if it's requestNotTaken is false
                return;
            }

            if (requestNotTaken) {
                if (queuePosition.y == 0)
                {
                    // create order via OrdersManager.createOrder() whatever
                    requestNotTaken = false;
                    timeRemaining = timeStart;
                    return;
                }

                // Throws dice to change queue (if is IN this said queue)
                if (Random.value < (timeRemaining / timeStart) * impatienceModifier)
                {
                    // TODO: throw in a value to this calculation to have the chance lower as more recent the last successful die throw was
                    // (i.e. can't jump queues every second)
                    int[] nearestQueues = new int[3];
                    for (int i = -1; i < 1; i++)
                        nearestQueues[i] = Instance.HighestQueuePosition(queuePosition, i);
                    int minValOfQueue = int.MaxValue, indexOfQueue = 0;
                    for (int index = 0; index < nearestQueues.Length; index++)
                    {
                        if (minValOfQueue < nearestQueues[index])
                        {
                            minValOfQueue = nearestQueues[index];
                            indexOfQueue = index;
                        }
                    }

                    // if it's still gonna be smallest when this moves in the new queue compared to our own queue, then we ask the ordermanager to move us there
                    if (minValOfQueue + 1 < nearestQueues[1])
                        Instance.MoveRequesteeToQueue(this, queuePosition.x + indexOfQueue);
                }

            } 
            else
            {
                if (request.orderFulfilled)
                {
                    Instance.AnnihilateRequestee(queuePosition);
                    // TODO: destroy thyself and give players points for being such a good boy~
                }
            }
        }
    }

    public OrderRequestee[,] queue = new OrderRequestee[4,4]; //length, height
    
    [Header("Order Settings")]
    [SerializeField] float orderCooldown = 25f;
    [SerializeField] float minOrderTime = 20f;

    [Header("Spawning")]
    public GameObject box;
    public Transform spawnPosition;
    [Range(0, 10), SerializeField] float randomSpawnIntervalMax = 1;
    public TextMeshProUGUI orderListUI;

    [Header("Audio")]
    [SerializeField] AudioClip[] newOrderSound;
    [SerializeField] AudioClip[] orderCompleteSound;
    [SerializeField] AudioClip[] orderFailSound;

    private GameManager gameManager;
    private readonly List<Order> activeOrders = new();
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
                MoveTheQueues();

                // if height == 0, activate order and await it's finish OR leave if timer ran out.
                // tick timer for everyone, check if there is empty X,0 and whoever isn't X,0 OR X,Y [if X,Y and no value above it, becomes X,0] throws dice to move to the emptiest array as last requestee of that queue (unless it'll be as long as the queue he is in now). dice chance increases to success with lowest value of nearest of queues
            }
        }
    }

    void GenerateNewOrderRequestee()
    {
        if (gameManager.itemTemplates.Count == 0) return;  // TODO: don't count in itemTemplates, items are now secondary priority, boxes of ranges of colors & forms is the main thing now.
        // idea, opening it can't seal it, but some things contain duct tape that will . uh seal the box for once more and picks the nearest item that isn't itself
        // so items are sorta important. hm. but they shouldn't be priority anyway now so
        Order newOrder = new()
        {
            requestedItemID = gameManager.itemTemplates[Random.Range(0, gameManager.itemTemplates.Count)].ID
        };

        activeOrders.Add(newOrder); // TODO: only when requestee finally reached X,0
        source.PlaySound(newOrderSound);
    }

    void UpdateActiveOrders()
    {
        foreach (Order order in activeOrders.ToList())
        {
            order.requestee.timeRemaining -= Time.deltaTime;
            if (order.requestee.timeRemaining <= 0)
            {
                gameManager.AddScore(-25, resetTimer: false);
                activeOrders.Remove(order);
                source.PlaySound(orderFailSound);
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
        if (reqPosition.x + offset < 0 || reqPosition.x + offset > reqPosition.x) return -1;  // Out of bounds? Out of mind.
        for (int height = 0; height < queue.GetLength(1); height++)
        {
            if (queue[reqPosition.x + offset, height] == null)
                return height;
        }
        return -1; // This queue is full.
    }

    public void MoveRequesteeToQueue(OrderRequestee requestee, int queueIndex)
    {
        for (int height = 0; height < queue.GetLength(1); height++)
        {
            if (queue[queueIndex, height] == null)
            {
                queue[queueIndex, height] = requestee;
                requestee.queuePosition = new Vector2Int(queueIndex, height);
                queue[requestee.queuePosition.x, requestee.queuePosition.y] = null;  // clearing that spot.
                return;
            }
        }
        Debug.Log($"Failed to move {requestee} to queue {queueIndex}, assumedly it's because the queue index requested to move to was full");
    }

    public void AnnihilateRequestee(Vector2Int requesteePos) => queue[requesteePos.x, requesteePos.y] = null;

    public void MoveTheQueues()
    {
        for (int width = 0; width < queue.GetLength(0); width++)
        {
            for (int height = 0; height < queue.GetLength(1); height++)
            {
                if (queue[width, height] == null)
                {
                    queue[width, height] = queue[width, height + 1];
                    queue[width, height + 1].queuePosition.y -= 1;  // Yowch! Dirty! anyway
                    queue[width, height + 1] = null;
                }
            }
        }
    }

    public void ProcessOrderDelivery(Item deliveredItem, bool fromShelf)
    {
        bool orderFound = false;

        foreach (Order order in activeOrders.ToList())
        {
            if (order.requestedItemID == deliveredItem.ID)
            {
                int scoreChange = fromShelf ? deliveredItem.scoreValue * 2 : -deliveredItem.scoreValue;
                gameManager.AddScore(scoreChange, resetTimer: !fromShelf, immediateReset: true);

                activeOrders.Remove(order);
                orderFound = true;
                source.PlaySound(fromShelf ? orderCompleteSound : orderFailSound);
                break;
            }
        }

        if (!orderFound)
        {
            gameManager.AddScore(deliveredItem.scoreValue * (fromShelf ? 0 : -3), resetTimer: !fromShelf);
            source.PlaySound(orderFailSound);
        }
    }

    public void SpawnInitialItem()
    {
        SpawnItem();
    }

    public void SpawnItemAfterDelay()
    {
        StartCoroutine(SpawnWithDelay());
    }

    IEnumerator SpawnWithDelay()
    {
        yield return new WaitForSeconds(Random.Range(0, randomSpawnIntervalMax));
        SpawnItem();
    }

    void SpawnItem()
    {
        if (box == null || spawnPosition == null || gameManager.itemTemplates.Count == 0) return;

        int randomIndex = Random.Range(0, gameManager.itemTemplates.Count);
        var newBox = Instantiate(box, spawnPosition.position, Quaternion.identity);
        GameObject newItem = Instantiate(gameManager.itemTemplates[randomIndex].gameObject);
        newItem.SetActive(false);

        if (newBox.TryGetComponent<Box>(out var boxComponent))
        {
            boxComponent.containedItem = newItem;
        }

        if (newBox.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }
    }

    public void UpdateOrderUI()
    {
        // TODO: completely different!  check image. if not seen uh.
        // |   O     | delivery chute is also different. it's a risen ground with hole in ceiling and four buttons. 
        // | O O O O | | /  __  OPEN  __  \ |    opening chute with delivery item will suck it up
        // |_________| | \     CLOSE      / |    opening chute with receiving item will drop item
        // | X Z Z Z | X - deposit of something, Z - get item.

        if (activeOrders.Count < 1)
        {
            bool foundNoOrders = LocalizationManager.TryGetVal("no_orders", out var transl);
            if (foundNoOrders) orderListUI.text = transl;
            return;
        }

        orderListUI.text = "";
        foreach (Order order in activeOrders)
        {
            Item item = gameManager.ReturnItemById(order.requestedItemID);
            float timePercent = order.requestee.timeRemaining / order.requestee.timeStart;
            string progressBar = GetProgressBar(timePercent);
            orderListUI.text += $"- {item.name} {progressBar} ({Mathf.FloorToInt(order.requestee.timeRemaining)}s)\n";
        }
    }

    string GetProgressBar(float percent)
    {
        int bars = 10;
        int filled = Mathf.RoundToInt(bars * percent);
        filled = Mathf.Clamp(filled, 0, bars);
        return "<color=#FF0000>" + new string('█', filled) + "</color>" +
               "<color=#444444>" + new string('█', bars - filled) + "</color>";
    }
}
