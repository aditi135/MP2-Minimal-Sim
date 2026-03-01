using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class BuildStation : MonoBehaviour, IXRSelectFilter
{
    /*
     * BuildStation is responsible for:
     * : accepting items into XRSocketInteractor slots via player placement.
     * : filtering items so only those matching allowedItemTag can be socketed.
     * : consuming placed items and activating a pre-placed (disabled) AutoGenerator
     *   once all slots are filled.
     * : reacting to game state changes [Initializing, Running, Paused, GameOver].
     */

    [Header("Slots")]
    [SerializeField] private XRSocketInteractor[] slots;

    [Header("Item Filter")]
    [SerializeField] private string allowedItemTag;

    [Header("Generator to Deploy")]
    [SerializeField] private GameObject targetGenerator;

    [Header("Info Board (optional)")]
    [SerializeField] private GeneratorInfoBoard infoBoard;

    private int filledSlots;
    private bool isActive;
    private bool deployed;

    public int FilledSlots => filledSlots;
    public int RequiredSlots => slots != null ? slots.Length : 0;
    public bool IsComplete => filledSlots >= RequiredSlots && RequiredSlots > 0;
    public bool Deployed => deployed;

    public event Action OnGeneratorDeployed;

    // --- IXRSelectFilter ---

    public bool canProcess => isActiveAndEnabled && !deployed;

    public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
    {
        if (deployed) return false;

        if (string.IsNullOrEmpty(allowedItemTag))
            return true;

        return interactable.transform.CompareTag(allowedItemTag);
    }

    // --- MonoBehaviour lifecycle ---

    private void OnEnable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnStateChanged += HandleStateChanged;

        SubscribeToSlots();
        RegisterFilters();
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnStateChanged -= HandleStateChanged;

        UnsubscribeFromSlots();
        UnregisterFilters();
    }

    private void Start()
    {
        filledSlots = CountFilledSlots();

        if (GameManager.Instance != null)
            HandleStateChanged(GameManager.Instance.CurrentState);
    }

    // --- State handling ---

    private void HandleStateChanged(GameManager.GameState state)
    {
        switch (state)
        {
            case GameManager.GameState.Running:
                isActive = true;
                break;

            case GameManager.GameState.Paused:
            case GameManager.GameState.GameOver:
                isActive = false;
                break;

            case GameManager.GameState.Initializing:
                isActive = false;
                filledSlots = 0;
                deployed = false;
                break;
        }
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

    private void RegisterFilters()
    {
        foreach (var socket in slots)
        {
            if (socket == null) continue;
            socket.selectFilters.Add(this);
        }
    }

    private void UnregisterFilters()
    {
        foreach (var socket in slots)
        {
            if (socket == null) continue;
            socket.selectFilters.Remove(this);
        }
    }

    private void OnSlotFilled(SelectEnterEventArgs args)
    {
        if (deployed) return;

        filledSlots = CountFilledSlots();
        Debug.Log($"[BuildStation] Slot filled ({filledSlots}/{RequiredSlots})");

        if (IsComplete)
            DeployGenerator();
    }

    private void OnSlotEmptied(SelectExitEventArgs args)
    {
        if (deployed) return;

        filledSlots = CountFilledSlots();
        Debug.Log($"[BuildStation] Slot emptied ({filledSlots}/{RequiredSlots})");
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

    // --- Deployment ---

    private void DeployGenerator()
    {
        deployed = true;

        ConsumeItems();

        if (targetGenerator != null)
        {
            targetGenerator.SetActive(true);
            Debug.Log($"[BuildStation] Generator activated: {targetGenerator.name}");
        }

        var gen = targetGenerator != null ? targetGenerator.GetComponent<AutoGenerator>() : null;
        if (gen != null && infoBoard != null)
            infoBoard.RegisterGenerator(gen);

        OnGeneratorDeployed?.Invoke();
        Debug.Log("[BuildStation] Generator deployed!");

        DisableSlots();
    }

    private void ConsumeItems()
    {
        foreach (var socket in slots)
        {
            if (socket == null) continue;

            var interactablesSelected = socket.interactablesSelected;
            for (int i = interactablesSelected.Count - 1; i >= 0; i--)
            {
                var interactable = interactablesSelected[i];
                if (interactable == null) continue;

                var go = interactable.transform.gameObject;
                socket.interactionManager.CancelInteractableSelection(interactable);
                Destroy(go);
            }
        }
    }

    private void DisableSlots()
    {
        foreach (var socket in slots)
        {
            if (socket != null)
                socket.enabled = false;
        }
    }

    private void OnDestroy()
    {
        UnregisterFilters();
        UnsubscribeFromSlots();

        if (GameManager.Instance != null)
            GameManager.Instance.OnStateChanged -= HandleStateChanged;
    }
}
