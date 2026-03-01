using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using TMPro;

public class DisplayBoard : MonoBehaviour, IXRSelectFilter
{
    /*
     * DisplayBoard is responsible for:
     * : accepting items into XRSocketInteractor slots via player placement.
     * : filtering items so only those matching allowedItemTag can be socketed.
     * : displaying a pre-configured letter on a TMP_Text when all slots are filled.
     * : reacting to game state changes [Initializing, Running, Paused, GameOver].
     */

    [Header("Slots")]
    [SerializeField] private XRSocketInteractor[] slots;

    [Header("Item Filter")]
    [SerializeField] private string allowedItemTag;

    [Header("Display")]
    [SerializeField] private TMP_Text displayText;
    [SerializeField] private string letter;

    private int filledSlots;
    private bool isActive;

    public int FilledSlots => filledSlots;
    public int RequiredSlots => slots != null ? slots.Length : 0;
    public bool IsComplete => filledSlots >= RequiredSlots && RequiredSlots > 0;

    public event Action OnBoardCompleted;
    public event Action OnBoardIncomplete;

    // --- IXRSelectFilter ---

    public bool canProcess => isActiveAndEnabled; // isActiveAndEnabled is a property of MonoBehaviour that returns true if the object is active and enabled

    public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
    {
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
        UpdateDisplay();

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
                UpdateDisplay();
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
        filledSlots = CountFilledSlots();
        UpdateDisplay();
        Debug.Log($"[DisplayBoard] Slot filled ({filledSlots}/{RequiredSlots})");

        if (IsComplete)
        {
            OnBoardCompleted?.Invoke();
            Debug.Log($"[DisplayBoard] Complete! Displaying \"{letter}\"");
        }
    }

    private void OnSlotEmptied(SelectExitEventArgs args)
    {
        bool wasComplete = IsComplete;
        filledSlots = CountFilledSlots();
        UpdateDisplay();
        Debug.Log($"[DisplayBoard] Slot emptied ({filledSlots}/{RequiredSlots})");

        if (wasComplete && !IsComplete)
            OnBoardIncomplete?.Invoke();
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

    // --- Display ---

    private void UpdateDisplay()
    {
        if (displayText == null) return;

        if (IsComplete)
        {
            displayText.text = letter;
            displayText.enabled = true;
        }
        else
        {
            displayText.text = "";
            displayText.enabled = false;
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
