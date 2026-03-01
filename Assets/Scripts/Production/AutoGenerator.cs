using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class AutoGenerator : MonoBehaviour
{
    /*
     * AutoGenerator is responsible for:
     * : producing items on a timer and handing them off via OnItemProduced (Store subscribes).
     * : accepting up to maxSlots boost cubes via XRSocketInteractors to speed up production.
     * : optionally requiring at least one slot filled before production begins.
     * : reacting to game state changes [Initializing, Running, Paused, GameOver].
     */

    [Header("Production")]
    [SerializeField] private GameObject itemPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private float baseProductionInterval = 60f;

    [Header("Item Config")]
    [SerializeField] private string itemName = "Item";
    [SerializeField] private int itemValue = 1;

    [Header("Default Item (should never be used)")]
    [SerializeField] private Vector3 defaultItemScale = new Vector3(0.1f, 0.1f, 0.1f);
    [SerializeField] private Color itemColor = Color.white;

    [Header("Slot Boost")]
    [SerializeField] private XRSocketInteractor[] slots = new XRSocketInteractor[3];
    [SerializeField] private bool requireSlotToStart;
    [SerializeField] private float speedBoostPerSlot = 0.5f;

    private float timer = 0f;
    private bool isProducing;
    private int filledSlots = 0;
    private bool _subscribedToGameManager;

    public int TotalProduced { get; private set; }
    public int FilledSlots => filledSlots;
    public string ItemName => itemName;

    public float EffectiveInterval =>
        !requireSlotToStart ? baseProductionInterval : (filledSlots > 0 ? baseProductionInterval * (float)Math.Pow(1f - speedBoostPerSlot, filledSlots - 1) : float.PositiveInfinity);

    public event Action<GameObject> OnItemProduced;

    private bool CanProduce => isProducing && (!requireSlotToStart || filledSlots > 0);

    // --- MonoBehaviour lifecycle ---

    private void OnEnable()
    {
        if (GameManager.Instance != null && !_subscribedToGameManager)
        {
            GameManager.Instance.OnStateChanged += HandleStateChanged;
            _subscribedToGameManager = true;
        }

        SubscribeToSlots();
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnStateChanged -= HandleStateChanged;
        _subscribedToGameManager = false;

        UnsubscribeFromSlots();
    }

    private void Start()
    {
        if (spawnPoint == null)
            spawnPoint = transform;

        filledSlots = CountFilledSlots();

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
        if (!CanProduce)
            return;

        timer += Time.deltaTime;
        if (timer >= EffectiveInterval)
        {
            timer -= EffectiveInterval;
            ProduceItem();
        }
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnStateChanged -= HandleStateChanged;
    }

    // --- State handling ---

    private void HandleStateChanged(GameManager.GameState state)
    {
        switch (state)
        {
            case GameManager.GameState.Running:
                isProducing = true;
                break;

            case GameManager.GameState.Paused:
            case GameManager.GameState.GameOver:
                isProducing = false;
                break;

            case GameManager.GameState.Initializing:
                isProducing = false;
                timer = 0f;
                TotalProduced = 0;
                break;
        }
    }

    // --- Item production ---

    private void ProduceItem()
    {
        GameObject obj = itemPrefab != null
            ? Instantiate(itemPrefab, spawnPoint.position, spawnPoint.rotation)
            : CreateDefaultItem();

        var item = obj.GetComponent<Item>();
        if (item == null)
            item = obj.AddComponent<Item>();
        item.Initialize(itemName, itemValue);

        TotalProduced++;
        Debug.Log($"[AutoGenerator:{itemName}] Produced #{TotalProduced} " +
                  $"at t={GameManager.Instance?.SimulationTime:F1}s " +
                  $"(interval={EffectiveInterval:F1}s, slots={filledSlots}/{slots.Length})");

        OnItemProduced?.Invoke(obj);
    }

    private GameObject CreateDefaultItem() // Should never be used
    {
        var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
        item.name = itemName;
        item.transform.position = spawnPoint.position;
        item.transform.rotation = spawnPoint.rotation;
        item.transform.localScale = defaultItemScale;

        var renderer = item.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            renderer.material.color = itemColor;
        }

        return item;
    }

    // --- Slot tracking ---

    private void SubscribeToSlots()
    {
        foreach (var socket in slots)
        {
            if (socket == null) continue;
            socket.selectEntered.AddListener(OnSlotFilled);
            socket.selectExited.AddListener(OnSlotEmptied);
        }
    }

    private void UnsubscribeFromSlots()
    {
        foreach (var socket in slots)
        {
            if (socket == null) continue;
            socket.selectEntered.RemoveListener(OnSlotFilled);
            socket.selectExited.RemoveListener(OnSlotEmptied);
        }
    }

    private void OnSlotFilled(SelectEnterEventArgs args)
    {
        filledSlots = CountFilledSlots();
        Debug.Log($"[AutoGenerator:{itemName}] Slot filled ({filledSlots}/{slots.Length})");
    }

    private void OnSlotEmptied(SelectExitEventArgs args)
    {
        filledSlots = CountFilledSlots();
        Debug.Log($"[AutoGenerator:{itemName}] Slot emptied ({filledSlots}/{slots.Length})");
    }

    private int CountFilledSlots()
    {
        int count = 0;
        foreach (var socket in slots)
        {
            if (socket != null && socket.hasSelection)
                count++;
        }
        return count;
    }
}
