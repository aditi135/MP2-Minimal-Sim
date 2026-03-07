using UnityEngine;
using TMPro;

public class ClickerButton : MonoBehaviour
{
    public GameObject itemPrefab;

    public float forwardDistance = 1f;  // how far in front
    public float sideOffset = 1f;       // how far to the right (negative = left)
    public float cooldown = 2f;

    public TextMeshPro countdown;

    private float lastUseTime = -999f;

    private void Start()
    {
        if (countdown != null)
            countdown.text = "";
    }

    private void OnTriggerEnter(Collider other)
    {
        float elapsed = Time.time - lastUseTime;
        if (elapsed < cooldown)
            return;

        lastUseTime = Time.time;

        if (itemPrefab != null)
        {
            // Spawn in front + to the side, same height
            Vector3 spawnPos =
                transform.position
                - transform.forward * forwardDistance   // in front
                + transform.right * sideOffset;         // to the side

            Instantiate(itemPrefab, spawnPos, Quaternion.identity);
        }
    }

    private void Update()
    {
        float remaining = cooldown - (Time.time - lastUseTime);

        if (countdown != null)
        {
            if (remaining > 0)
                countdown.text = remaining.ToString("F1");
            else
                countdown.text = "";
        }
    }
}
