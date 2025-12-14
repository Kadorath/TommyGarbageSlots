using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using System;

using SBI = PlayerInputManager.SpaceBridgeInput;
using Random = UnityEngine.Random;
using TMPro;
using System.Linq;
using Unity.VisualScripting;

public class SlotMachineController : MonoBehaviour
{
    bool spinLock = false;
    bool scoring = false;
    bool skip = false;

    [SerializeField] bool bonusGameActive = false;
    [SerializeField] GameObject grabbyPaw;

    public int score = 0;
    public int chipValue = 100;
    private int curWageredValue = 100;
    private TMP_Text payoutText;
    private TMP_Text scoreText;
    private TMP_Text chipValueText;

    private List<SlotReelController> reels;
    private List<Payline> lines = new List<Payline>()
    {
        // Horizontal Lines
        new Payline(new List<int>() {0, 0, 0, 0, 0}),
        new Payline(new List<int>() {1, 1, 1, 1, 1}),
        new Payline(new List<int>() {2, 2, 2, 2, 2}),
        new Payline(new List<int>() {3, 3, 3, 3, 3}),
        new Payline(new List<int>() {4, 4, 4, 4, 4}),

        // Diagonal Lines
        new Payline(new List<int>() {0, 1, 2, 3, 4}, Color.green),
        new Payline(new List<int>() {4, 3, 2, 1, 0}, Color.green),
        new Payline(new List<int>() {0, 1, 2, 1, 0}, Color.green),
        new Payline(new List<int>() {4, 3, 2, 3, 4}, Color.green),
        new Payline(new List<int>() {0, 2, 4, 2, 0}, Color.green),
        new Payline(new List<int>() {4, 2, 0, 2, 4}, Color.green),

        // Zig Zags
        new Payline(new List<int>() {0, 4, 0, 4, 0}, Color.green),
        new Payline(new List<int>() {4, 0, 4, 0, 4}, Color.green),
        new Payline(new List<int>() {3, 0, 3, 0, 3}, Color.green),
        new Payline(new List<int>() {0, 3, 0, 3, 0}, Color.green),
        new Payline(new List<int>() {2, 3, 2, 3, 2}, Color.green),
        new Payline(new List<int>() {2, 1, 2, 1, 2}, Color.green),
        new Payline(new List<int>() {0, 1, 0, 1, 0}, Color.green),
        new Payline(new List<int>() {1, 0, 1, 0, 1}, Color.green),
        new Payline(new List<int>() {1, 2, 1, 2, 1}, Color.green),
        new Payline(new List<int>() {2, 1, 2, 1, 2}, Color.green),
        new Payline(new List<int>() {2, 3, 2, 3, 2}, Color.green),
        new Payline(new List<int>() {3, 2, 3, 2, 3}, Color.green),
        new Payline(new List<int>() {4, 3, 4, 3, 4}, Color.green),
        new Payline(new List<int>() {3, 4, 3, 4, 3}, Color.green),

        // Curves
        new Payline(new List<int>() {1, 0, 0, 0, 1}, Color.blue),
        new Payline(new List<int>() {3, 4, 4, 4, 3}, Color.blue),
        new Payline(new List<int>() {0, 0, 1, 0, 0}, Color.blue),
        new Payline(new List<int>() {3, 3, 4, 3, 3}, Color.blue),
        new Payline(new List<int>() {1, 1, 0, 1, 1}, Color.blue),
        new Payline(new List<int>() {4, 4, 3, 4, 4}, Color.blue),
        new Payline(new List<int>() {2, 2, 1, 2, 2}, Color.blue),
        new Payline(new List<int>() {2, 2, 3, 2, 2}, Color.blue),
        new Payline(new List<int>() {2, 1, 1, 1, 2}, Color.blue),
        new Payline(new List<int>() {2, 3, 3, 3, 2}, Color.blue),

        new Payline(new List<int>() {0, 1, 1, 1, 1}, Color.blue),
        new Payline(new List<int>() {4, 3, 3, 3, 3}, Color.blue),
        new Payline(new List<int>() {0, 0, 0, 0, 1}, Color.blue),
        new Payline(new List<int>() {4, 4, 4, 4, 3}, Color.blue),
        new Payline(new List<int>() {2, 2, 4, 2, 2}, Color.blue),
        new Payline(new List<int>() {2, 2, 0, 2, 2}, Color.blue)
    };

    public SymbolSO ScatterSymbol;
    public List<SymbolSO> Symbols;
    private Queue<SymbolSO> symbolBank;

    [SerializeField] GameObject payline;
    private Transform paylineParent;

    // Audio
    [Header("Audio")]
    private AudioSource audiosource;
    [SerializeField] AudioClip ambienceClip;
    [SerializeField] AudioClip hypeClip;

    public static SlotMachineController Instance;
    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void OnEnable()
    {
        PlayerInputManager.PhidgetInput += onPhidgetInput;

        reels = new List<SlotReelController>();
        foreach (Transform reel in transform.Find("MainWindow/SlotReels"))
            reels.Add(reel.GetComponent<SlotReelController>());

        scoreText = transform.Find("ScoreText").GetComponent<TMP_Text>();
        payoutText = transform.Find("PayoutText").GetComponent<TMP_Text>();
        chipValueText = transform.Find("ChipValueText").GetComponent<TMP_Text>();
        paylineParent = transform.Find("Paylines");
        UpdateScore(2000);

        audiosource = GetComponent<AudioSource>();

        Debug.Log($"{lines.Count} paylines active");
    }

    void OnDisable()
    {
        PlayerInputManager.PhidgetInput -= onPhidgetInput;
    }

    public void StartSpin()
    {
        if (spinLock) 
        {
            if (!scoring)
                StopSpin();
            else
                skip = true;
            return; 
        }
        spinLock = true;

        ClearPayoutInfo();

        curWageredValue = chipValue;
        UpdateScore(-curWageredValue);

        audiosource.resource = hypeClip;
        audiosource.Play();

        foreach(SlotReelController reel in reels)
            reel.StartSpin();
    }

    public void StopSpin()
    {
        StartCoroutine(StopSpin_aux());
    }

    IEnumerator StopSpin_aux()
    {
        scoring = true;
        yield return StopSpin_Sequential();
        Debug.Log("SCORING TIME!!!");
        yield return ScoreLines();

        // Alert bonus game!
        if (bonusGameActive)
            yield return AlertBonusGame();

        spinLock = false;
        skip = false;
        scoring = false;

        audiosource.resource = ambienceClip;
        audiosource.Play();
    }

    IEnumerator StopSpin_Sequential()
    {
        foreach (SlotReelController reel in reels)
        {
            reel.StopSpin();
            yield return new WaitForSeconds(0.3f);
        }
    }

    IEnumerator ScoreLines()
    {
        int gainedScore = 0;
        foreach(Payline pl in lines)
        {
            List<int> line = pl.Line;
            int consecutive = 1;
            SymbolSO symbol = reels[0].symbols[line[0]];
            for (int i = 1; i < line.Count; i ++)
            {
                SymbolSO curSymbol = reels[i].symbols[line[i]];

                // If I haven't seen a non-Wild symbol yet, try to use the curSymbol as my
                // reference symbol
                if (symbol.Symbol == SymbolType.WILD)
                    symbol = curSymbol;

                if (curSymbol == symbol || curSymbol.Symbol == SymbolType.WILD)
                    consecutive ++;
                else
                    break; 
            }
            if (consecutive >= 3) 
            {
                Debug.Log($"Score {symbol.Scores[consecutive - 3]} on {String.Join(", ", line)}");
                gainedScore += symbol.Scores[consecutive - 3] * (curWageredValue / 100);

                LineRenderer newPayline = Instantiate(payline, paylineParent).GetComponent<LineRenderer>();
                for (int i = 0; i < reels.Count; i ++)
                    newPayline.SetPosition(i, new Vector3(reels[i].transform.position.x, 
                        reels[i].transform.Find("BG/Borders").GetChild(line[i]).position.y - 0.45f, 
                        paylineParent.position.z - 0.1f));
                Gradient gradient = new Gradient();
                gradient.SetColorKeys(
                    new GradientColorKey[] { new GradientColorKey(pl.Color, 0f), new GradientColorKey(pl.Color, 1f)}
                );
                if (consecutive < 5)
                    gradient.SetAlphaKeys(
                        new GradientAlphaKey[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.9f, (consecutive)/5f), new GradientAlphaKey(0.05f, (consecutive)/5f + 0.1f), new GradientAlphaKey(0.05f, 0.9f) }
                    );
                newPayline.colorGradient = gradient;

                for (int i = 0; i < consecutive; i ++)
                {
                    reels[i].HighlightSymbol(line[i]);
                }

                if (symbol.Symbol == SymbolType.TOMMY)
                    bonusGameActive = true;
            }
        }

        for (int i = 1; i <= gainedScore; i ++)
        {
            payoutText.text = i.ToString();
            if (!skip)
                yield return new WaitForSeconds(Mathf.Min(0.05f, 3f / gainedScore));
        }

        UpdateScore(gainedScore);
    }

    IEnumerator AlertBonusGame()
    {
        ClearPayoutInfo();
        Transform mainWindow = transform.Find("MainWindow/PawHolder");
        for (int i = 0; i < reels.Count; i ++)
        {
            for (int j = 0; j < reels[i].symbols.Length; j ++)
            {
                GrabbyPawBehaviour newPaw = Instantiate(grabbyPaw, mainWindow).GetComponent<GrabbyPawBehaviour>();
                RectTransform targetIcon = reels[i].GetSymbol(j).GetComponent<RectTransform>();

                switch (Random.Range(0, 2)) 
                {
                    // From Right/Left
                    case 0:
                        // From Right
                        if (i > 2 || (Random.value < 0.5f && i == 2))
                        {
                            newPaw.Init(new Vector2(reels[i].GetComponent<RectTransform>().anchoredPosition.x + 32, targetIcon.anchoredPosition.y), 
                                        new Vector2(800, targetIcon.anchoredPosition.y),
                                        0f,
                                        reels[i].GetSymbol(j)
                            );
                        }
                        // From Left
                        else
                        {
                            newPaw.Init(new Vector2(reels[i].GetComponent<RectTransform>().anchoredPosition.x + 98, targetIcon.anchoredPosition.y),
                                        new Vector2(0, targetIcon.anchoredPosition.y),
                                        180f,
                                        reels[i].GetSymbol(j)
                            );
                        }
                        break;
                    // From Top/Bottom
                    case 1:
                        // From Bottom
                        if (j > 2 || (Random.value < 0.5f && j == 2))
                        {
                            newPaw.Init(new Vector2(reels[i].GetComponent<RectTransform>().anchoredPosition.x + 72, targetIcon.anchoredPosition.y + 32),
                                        new Vector2(reels[i].GetComponent<RectTransform>().anchoredPosition.x + 72, -400f),
                                        270f,
                                        reels[i].GetSymbol(j)
                            );
                        }
                        // From Top
                        else
                        {
                            newPaw.Init(new Vector2(reels[i].GetComponent<RectTransform>().anchoredPosition.x + 72, targetIcon.anchoredPosition.y - 32),
                                        new Vector2(reels[i].GetComponent<RectTransform>().anchoredPosition.x + 72, 0f),
                                        90,
                                        reels[i].GetSymbol(j)
                            );
                        }
                        break;
                }
            }
        }
        yield return new WaitForSeconds(10f);
    }

    private void ClearPayoutInfo()
    {
        payoutText.text = "0";
        for (int i = paylineParent.childCount - 1; i >= 0; i --)
        {
            Destroy(paylineParent.GetChild(i).gameObject);
        }

        foreach (SlotReelController reel in reels)
            reel.ResetHighlights();
    }

    private void UpdateScore(int gain)
    {
        score += gain;
        scoreText.text = score.ToString();
    }

    public SymbolSO SampleSymbol()
    {
        if (symbolBank == null || symbolBank.Count <= 0)
            InitSymbolBank();
        
        return symbolBank.Dequeue();
    }

    private void InitSymbolBank()
    {
        int baseCount = 20;
        
        List<SymbolSO> symbolList = new List<SymbolSO>();
        foreach (SymbolSO symbol in Symbols)
        {
            for (int i = 0; i < symbol.Count*baseCount; i ++)
                symbolList.Add(symbol);
        }

        for (int i = 0; i < ScatterSymbol.Count*baseCount; i ++)
            symbolList.Add(ScatterSymbol);

        for (int i = symbolList.Count-1; i > 0; i --)
        {
            int j = Random.Range(0, i+1);
            var temp = symbolList[j];
            symbolList[j] = symbolList[i];
            symbolList[i] = temp;
        }

        symbolBank = new Queue<SymbolSO>(symbolList);
    }

    private void ChangeChipValue(int steps)
    {
        if (steps > 0)
            for (int i = 0; i < steps; i ++)
                chipValue = Math.Min(chipValue * 2, 1600);
        else
            for (int i = 0; i < Mathf.Abs(steps); i ++)
                chipValue = Math.Max(chipValue / 2, 100);

        chipValueText.text = chipValue.ToString();
    }

    public void onPhidgetInput(object sender, PhidgetInputChangeEventArgs e)
    {
        if (e.type != SBI.BUTTON_INNER || e.value < 0.5f) return;

        switch (e.channel)
        {
            case 0:
                ChangeChipValue(1);
                break;
            case 1:
                ChangeChipValue(-1);
                break;
            case 2:
                StartSpin();
                break;
        }
    }
}

public class Payline
{
    public List<int> Line;
    public Color Color;
    public bool BonusActive = false;

    public Payline(List<int> line, Color? c = null)
    {
        Color = c ?? Color.red;
        Line = new List<int>(line);
    }
}