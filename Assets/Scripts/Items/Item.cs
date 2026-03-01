using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(XRGrabInteractable))]
public class Item : MonoBehaviour
{
    public enum ItemState
    {
        Created,
        InStore,
        Held,
        Released
    }

    [Header("Item Data")]
    [SerializeField] private string itemName;
    [SerializeField] private int value = 1;

    private ItemState currentState = ItemState.Created;
    private XRGrabInteractable grabInteractable;
    private Rigidbody rb;

    public string ItemName => string.IsNullOrEmpty(itemName) ? gameObject.name : itemName;
    public int Value => value;
    public ItemState CurrentState => currentState;
    public float CreationTime { get; private set; }

    public event Action<Item, ItemState> OnStateChanged;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        grabInteractable = GetComponent<XRGrabInteractable>();

        rb.useGravity = false;
        rb.isKinematic = true;

        CreationTime = GameManager.Instance != null ? GameManager.Instance.SimulationTime : 0f;
    }

    private void OnEnable()
    {
        grabInteractable.selectEntered.AddListener(OnGrabbed);
        grabInteractable.selectExited.AddListener(OnReleased);
    }

    private void OnDisable()
    {
        grabInteractable.selectEntered.RemoveListener(OnGrabbed);
        grabInteractable.selectExited.RemoveListener(OnReleased);
    }

    public void SetState(ItemState newState)
    {
        if (currentState == newState) return;

        currentState = newState;
        OnStateChanged?.Invoke(this, newState);
    }

    public void PlaceInStore()
    {
        SetState(ItemState.InStore);
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        SetState(ItemState.Held);
        rb.isKinematic = false;
        rb.useGravity = false;
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        SetState(ItemState.Released);
        rb.isKinematic = false;
        rb.useGravity = true;
    }

    public void Initialize(string name, int itemValue = 1)
    {
        itemName = name;
        value = itemValue;
    }
}
