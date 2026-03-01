using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Store : MonoBehaviour
{
    /*
     * Store is responsible for:
     * : managing multiple item types, each defined by an ItemSlot.
     * : auto-subscribing to each slot's generator and tagging produced items.
     * : placing produced items immediately at an open deliveryPoint (socket) for player pickup.
     * : capacity = number of delivery points; one item per socket.
     * : detecting when items are picked up (reparented) and freeing that socket.
     * : reacting to game state changes [Initializing, Running, Paused, GameOver].
     */

    [Serializable]
    public class ItemSlot
    {
        [Header("Config")]
        public string itemTag;
        public AutoGenerator generator;

        [Header("Delivery Points (one socket per item, capacity = array length)")]
        public Transform[] deliveryPoints;

        [NonSerialized] public int totalReceived;
        [NonSerialized] internal GameObject[] items;

        public int Capacity => deliveryPoints != null ? deliveryPoints.Length : 0;

        public int Count
        {
            get
            {
                if (items == null) return 0;
                int count = 0;
                for (int i = 0; i < items.Length; i++)
                {
                    if (items[i] != null) count++;
                }
                return count;
            }
        }

        public bool IsFull => Count >= Capacity;

        internal void EnsureInitialized()
        {
            if (items == null || items.Length != Capacity)
                items = new GameObject[Capacity];
        }

        internal int FindOpenSocket()
        {
            if (items == null) return -1;
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] == null) return i;
            }
            return -1;
        }

        internal void Clear()
        {
            if (items != null)
            {
                for (int i = 0; i < items.Length; i++)
                {
                    if (items[i] != null)
                    {
                        Destroy(items[i]);
                        items[i] = null;
                    }
                }
            }
            else
            {
                items = new GameObject[Capacity];
            }

            totalReceived = 0;
        }
    }

    [Header("Item Slots")]
    [SerializeField] private List<ItemSlot> itemSlots = new();

    private readonly Dictionary<AutoGenerator, Action<GameObject>> generatorCallbacks = new();
    private bool isOperating;
    private bool _subscribedToGameManager;

    public int TotalItemCount => itemSlots.Sum(s => s.Count);
    public int TotalReceived => itemSlots.Sum(s => s.totalReceived);

    public event Action<GameObject> OnItemReceived;
    public event Action<GameObject> OnItemPickedUp;

    // --- Public API ---

    public int SlotCount => itemSlots.Count;
    public ItemSlot GetSlotByIndex(int index) => (index >= 0 && index < itemSlots.Count) ? itemSlots[index] : null;
    public ItemSlot GetSlot(string itemTag) => itemSlots.Find(s => s.itemTag == itemTag);

    public void ReceiveItem(GameObject item)
    {
        if (item == null) return;

        var slot = GetSlot(item.tag);
        if (slot == null)
        {
            Debug.LogWarning($"[Store] No slot configured for tag \"{item.tag}\", rejecting item.");
            Destroy(item);
            return;
        }

        ReceiveItemIntoSlot(slot, item);
    }

    // --- Generator subscription ---

    private void SubscribeToGenerators()
    {
        foreach (var slot in itemSlots)
        {
            if (slot.generator == null) continue;

            var captured = slot;
            Action<GameObject> callback = (item) => OnGeneratorProduced(captured, item);
            slot.generator.OnItemProduced += callback;
            generatorCallbacks[slot.generator] = callback;
        }
    }

    private void UnsubscribeFromGenerators()
    {
        foreach (var kvp in generatorCallbacks)
        {
            if (kvp.Key != null)
                kvp.Key.OnItemProduced -= kvp.Value;
        }
        generatorCallbacks.Clear();
    }

    private void OnGeneratorProduced(ItemSlot slot, GameObject item)
    {
        if (item == null) return;
        item.tag = slot.itemTag;
        ReceiveItemIntoSlot(slot, item);
    }

    private void ReceiveItemIntoSlot(ItemSlot slot, GameObject item)
    {
        slot.EnsureInitialized();

        int socketIndex = slot.FindOpenSocket();
        if (socketIndex < 0)
        {
            Debug.LogWarning($"[Store] \"{slot.itemTag}\" all sockets full ({slot.Capacity}), rejecting item.");
            Destroy(item);
            return;
        }

        Transform socket = slot.deliveryPoints[socketIndex];
        item.transform.SetParent(socket);
        item.transform.localPosition = Vector3.zero;
        item.transform.localRotation = Quaternion.identity;

        slot.items[socketIndex] = item;
        slot.totalReceived++;

        Debug.Log($"[Store] \"{slot.itemTag}\" placed at {socket.name} ({slot.Count}/{slot.Capacity})");
        OnItemReceived?.Invoke(item);
    }

    // --- MonoBehaviour lifecycle ---

    private void OnEnable()
    {
        if (GameManager.Instance != null && !_subscribedToGameManager)
        {
            GameManager.Instance.OnStateChanged += HandleStateChanged;
            _subscribedToGameManager = true;
        }

        SubscribeToGenerators();
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnStateChanged -= HandleStateChanged;
        _subscribedToGameManager = false;

        UnsubscribeFromGenerators();
    }

    private void Start()
    {
        foreach (var slot in itemSlots)
            slot.EnsureInitialized();

        if (GameManager.Instance != null && !_subscribedToGameManager)
        {
            GameManager.Instance.OnStateChanged += HandleStateChanged;
            _subscribedToGameManager = true;
        }

        if (GameManager.Instance != null)
            HandleStateChanged(GameManager.Instance.CurrentState);
    }

    private void Update()
    {
        if (!isOperating)
            return;

        CleanupPickedItems();
    }

    // --- State handling ---

    private void HandleStateChanged(GameManager.GameState state)
    {
        switch (state)
        {
            case GameManager.GameState.Running:
                isOperating = true;
                break;

            case GameManager.GameState.Paused:
            case GameManager.GameState.GameOver:
                isOperating = false;
                break;

            case GameManager.GameState.Initializing:
                isOperating = false;
                ClearAll();
                break;
        }
    }

    // --- Pickup detection ---

    private void CleanupPickedItems()
    {
        foreach (var slot in itemSlots)
        {
            if (slot.items == null) continue;

            for (int i = 0; i < slot.items.Length; i++)
            {
                GameObject item = slot.items[i];
                if (item == null) continue;

                if (item.transform.parent != slot.deliveryPoints[i])
                {
                    slot.items[i] = null;
                    OnItemPickedUp?.Invoke(item);
                }
            }
        }
    }

    // --- Cleanup ---

    private void ClearAll()
    {
        foreach (var slot in itemSlots)
            slot.Clear();
    }

    private void OnDestroy()
    {
        UnsubscribeFromGenerators();

        if (GameManager.Instance != null)
            GameManager.Instance.OnStateChanged -= HandleStateChanged;
    }
}
