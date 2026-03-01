using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public class GeneratorInfoBoard : MonoBehaviour
{
    [Header("Input")]
    public InputActionReference toggleAction;

    [Header("Generators")]
    [SerializeField] private List<AutoGenerator> generators = new();

    [Header("Build Stations")]
    [SerializeField] private List<BuildStation> buildStations = new();

    [Header("Storage")]
    [SerializeField] private Store storage;

    [Header("Board Settings")]
    [SerializeField] private float distanceFromPlayer = 1.5f;
    [SerializeField] private Vector2 boardSize = new Vector2(0.6f, 0.7f);
    [SerializeField] private float fontSize = 0.04f;

    private GameObject boardObject;
    private TMP_Text infoText;
    private bool isVisible;
    private Transform playerCamera;

    public void RegisterGenerator(AutoGenerator gen)
    {
        if (gen != null && !generators.Contains(gen))
            generators.Add(gen);
    }

    private void Start()
    {
        playerCamera = Camera.main != null ? Camera.main.transform : null;
        if (playerCamera == null)
        {
            var cam = FindAnyObjectByType<Camera>();
            if (cam != null)
                playerCamera = cam.transform;
        }

        CreateBoard();
        boardObject.SetActive(false);
    }

    private void OnEnable()
    {
        toggleAction.action.Enable();
        toggleAction.action.performed += OnToggleTriggered;
    }

    private void OnDisable()
    {
        toggleAction.action.performed -= OnToggleTriggered;
        toggleAction.action.Disable();
    }

    private void OnToggleTriggered(InputAction.CallbackContext context)
    {
        isVisible = !isVisible;
        boardObject.SetActive(isVisible);

        if (isVisible)
        {
            PositionBoard();
            UpdateInfo();
        }
    }

    private void Update()
    {
        if (!isVisible) return;

        PositionBoard();
        UpdateInfo();
    }

    private void PositionBoard()
    {
        if (playerCamera == null) return;

        Vector3 forward = playerCamera.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;
        forward.Normalize();

        boardObject.transform.position = playerCamera.position + forward * distanceFromPlayer;
        boardObject.transform.rotation = Quaternion.LookRotation(forward);
    }

    private void UpdateInfo()
    {
        if (infoText == null || generators == null) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<b>Generation Speed</b>");

        foreach (var gen in generators)
        {
            if (gen == null) continue;
            sb.AppendLine($"{gen.ItemName}: {gen.EffectiveInterval:F1}s per item");
        }

        if (storage != null)
        {
            sb.AppendLine();
            sb.AppendLine("<b>Storage Inventory</b>");

            for (int i = 0; i < storage.SlotCount; i++)
            {
                var slot = storage.GetSlotByIndex(i);
                if (slot == null) continue;
                sb.AppendLine($"{slot.itemTag}: {slot.Count} / {slot.Capacity}");
            }
        }

        if (buildStations != null && buildStations.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("<b>Build Stations</b>");

            for (int i = 0; i < buildStations.Count; i++)
            {
                var bs = buildStations[i];
                if (bs == null) continue;
                string status = bs.Deployed ? "BUILT" : $"{bs.FilledSlots} / {bs.RequiredSlots}";
                sb.AppendLine($"Station {i + 1}: {status}");
            }
        }

        infoText.text = sb.ToString();
    }

    private void CreateBoard()
    {
        boardObject = new GameObject("GeneratorInfoBoard_Canvas");
        boardObject.transform.SetParent(transform);

        var canvas = boardObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var rectTransform = boardObject.GetComponent<RectTransform>();
        rectTransform.sizeDelta = boardSize * 1000f;
        rectTransform.localScale = Vector3.one * 0.001f;

        boardObject.AddComponent<CanvasScaler>();
        boardObject.AddComponent<GraphicRaycaster>();

        var bgObj = new GameObject("Background");
        bgObj.transform.SetParent(boardObject.transform, false);
        var bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        var bgImage = bgObj.AddComponent<Image>();
        bgImage.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);

        var textObj = new GameObject("InfoText");
        textObj.transform.SetParent(boardObject.transform, false);
        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        float padding = 20f;
        textRect.offsetMin = new Vector2(padding, padding);
        textRect.offsetMax = new Vector2(-padding, -padding);

        infoText = textObj.AddComponent<TextMeshProUGUI>();
        infoText.fontSize = fontSize * 1000f;
        infoText.color = Color.white;
        infoText.alignment = TextAlignmentOptions.TopLeft;
        infoText.enableWordWrapping = true;
        infoText.overflowMode = TextOverflowModes.Truncate;
    }
}
