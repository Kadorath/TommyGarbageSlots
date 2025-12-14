using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class SlotReelController : MonoBehaviour
{
    public float spinSpeed = 1f;
    [SerializeField] private List<GameObject> icons;
    [SerializeField] bool spinning = false;

    public SymbolSO[] symbols;
    private Dictionary<string, SymbolSO> spriteNameToSymbol = new Dictionary<string, SymbolSO>();
    private bool stopSpin = false;

    private Animator anim;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        anim = GetComponent<Animator>();

        spriteNameToSymbol = new Dictionary<string, SymbolSO>();
        foreach (SymbolSO symbol in SlotMachineController.Instance.Symbols)
        {
            spriteNameToSymbol.Add(symbol.Icon.name, symbol);
        }

        icons = new List<GameObject>();
        foreach (Transform child in transform.Find("BG/Icons"))
        {
            icons.Add(child.gameObject);
            SetIconSprite(child.gameObject, SlotMachineController.Instance.SampleSymbol().Icon);
        }

        symbols = new SymbolSO[5];
    }

    void FixedUpdate()
    {
        if (spinning)
        {   
            float dY =  -1200f * spinSpeed * Time.fixedDeltaTime;

            bool stopping = false;
            if (stopSpin)
            {
                float firstIconStartY = icons[0].GetComponent<RectTransform>().anchoredPosition.y + 14;
                float firstIconEndY = firstIconStartY + dY;
                if ((firstIconStartY % 60) < (firstIconEndY % 60))
                {
                    stopping = true;
                    dY = -60f - (firstIconStartY%60);
                }
            }

            foreach (GameObject icon in icons)
            {
                MoveIconY(icon, dY);
                RectTransform t = icon.GetComponent<RectTransform>();
                if (t.anchoredPosition.y < -438)
                    LoopIcon(icon);
            }

            if (stopping)
            {
                anim.SetTrigger("stop");
                ToggleSpin();
                UpdateSymbols();
                stopSpin = false;
            }
        }
    }

    public void StartSpin()
    {
        anim.SetTrigger("start");
    }

    public void StopSpin()
    {
        stopSpin = true;
    }

    public void ToggleSpin()
    {
        spinning = !spinning;
    }

    private void MoveIconY(GameObject icon, float dY)
    {
        RectTransform t = icon.GetComponent<RectTransform>();
        t.anchoredPosition = new Vector2(t.anchoredPosition.x, t.anchoredPosition.y + dY);
    }

    private void LoopIcon(GameObject icon)
    {
        RectTransform t = icon.GetComponent<RectTransform>();
        t.anchoredPosition = new Vector3(t.anchoredPosition.x, -14f + t.anchoredPosition.y + 438);

        SetIconSprite(icon, SlotMachineController.Instance.SampleSymbol().Icon);
    }

    private void SetIconSprite(GameObject icon, Sprite spr)
    {
        Image img = icon.GetComponent<Image>();
        img.sprite = spr;
        icon.transform.localRotation = Quaternion.Euler(icon.transform.localRotation.x, icon.transform.localRotation.x, Random.Range(0f, 180f));
    }

    private void UpdateSymbols()
    {
        int firstIcon = 0;
        float firstY = icons[firstIcon].GetComponent<RectTransform>().anchoredPosition.y;
        for (int i = 1; i < icons.Count; i ++)
        {
            RectTransform t = icons[i].GetComponent<RectTransform>();
            if (t.anchoredPosition.y > firstY)
            {
                firstIcon = i;
                firstY = t.anchoredPosition.y;
            }
        }
        for (int i = 0; i < 5; i ++)
        {
            string spriteName = icons[(firstIcon + i) % icons.Count].GetComponent<Image>().sprite.name;
            SymbolSO symbol = spriteNameToSymbol[spriteName];
            symbols[i] = symbol;
        }
    }

    public GameObject GetSymbol(int ind)
    {
        int firstIcon = 0;
        float firstY = icons[firstIcon].GetComponent<RectTransform>().anchoredPosition.y;
        for (int i = 1; i < icons.Count; i ++)
        {
            RectTransform t = icons[i].GetComponent<RectTransform>();
            if (t.anchoredPosition.y > firstY)
            {
                firstIcon = i;
                firstY = t.anchoredPosition.y;
            }
        }

        GameObject targetIcon = icons[(firstIcon + ind) % icons.Count];
        return targetIcon;
    }

    public void HighlightSymbol(int ind)
    {
        GameObject symbol = GetSymbol(ind);
        symbol.GetComponent<SymbolIconController>().Highlight();
    }

    public void ResetHighlights()
    {
        foreach (GameObject icon in icons)
            icon.GetComponent<SymbolIconController>().Reset();
    }
}