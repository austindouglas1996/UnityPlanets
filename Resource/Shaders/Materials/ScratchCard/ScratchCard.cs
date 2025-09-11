using System.Collections.Generic;
using TMPro;
using UnityEngine;

[System.Serializable]
public class ScratchItem
{
    public string Name = "";
    public int Multiplier;
    public bool InstantDeath = false;
    public bool Jackpot = false;

    public float SpawnChance = 100;
    public Sprite Texture;
}

public class ScratchCard : MonoBehaviour
{
    [SerializeField] private bool GambleAgain = false;
    [SerializeField] private float ScratchIntensity = 2f;

    [SerializeField] private Texture2D ScratchBackground;
    [SerializeField] private Texture2D ScratchForeground;

    [SerializeField] private TextMeshProUGUI messageText;

    [SerializeField] private List<ScratchItem> AvailableItems = new();

    [SerializeField] private List<GameObject> Slots;

    private List<ScratchItem> ItemsOnCard = new();

    private bool showingBack = false;

    void Start()
    {
        SetupCard();
    }

    void Update()
    {
        if (GambleAgain)
        {
            GambleAgain = false;
            SetupCard();
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            GambleAgain = true;
        }

        if (Input.GetKeyDown(KeyCode.J))
        {
            if (showingBack)
            {
                // Rotate to 463 on Z
                transform.rotation = Quaternion.Euler(
                    transform.rotation.eulerAngles.x,
                    transform.rotation.eulerAngles.y,
                    463f
                );
                showingBack = false;
            }
            else
            {
                // Rotate to 278 on Z
                transform.rotation = Quaternion.Euler(
                    transform.rotation.eulerAngles.x,
                    transform.rotation.eulerAngles.y,
                    278f
                );
                showingBack = true;
            }
        }
    }

    private void SetupCard()
    {
        FillCard();
        FillSlots();
    }

    private void FillCard()
    {
        ItemsOnCard.Clear();
        ClearSlots();

        for (int i = 0; i < 9; i++)
        {
            ItemsOnCard.Add(AvailableItems[Random.Range(0, AvailableItems.Count-1)]);
        }
    }

    private void FillSlots()
    {
        for (int i = 0; i < 9; i++)
        {
            var item = ItemsOnCard[i];
            var sprite = Slots[i].GetComponent<SpriteRenderer>();

            sprite.sprite = item.Texture;
        }
    }

    private void ClearSlots()
    {
        foreach (var go in Slots)
        {
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = null;
        }
    }
}
