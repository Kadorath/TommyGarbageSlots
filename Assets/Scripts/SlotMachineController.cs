using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using System;

using SBI = PlayerInputManager.SpaceBridgeInput;
using Random = UnityEngine.Random;
using TMPro;

public class SlotMachineController : MonoBehaviour
{
    bool spinLock = false;
    bool scoring = false;
    bool skip = false;

    public int score = 0;
    public int chipValue = 100;
    private int curWageredValue = 100;
    private TMP_Text payoutText;
    private TMP_Text scoreText;
    private TMP_Text chipValueText;

    private List<SlotReelController> reels;
    private List<List<int>> lines = new List<List<int>>()
    {
        // Horizontal Lines
        new List<int>() {0, 0, 0, 0, 0},
        new List<int>() {1, 1, 1, 1, 1},
        new List<int>() {2, 2, 2, 2, 2},
        new List<int>() {3, 3, 3, 3, 3},
        new List<int>() {4, 4, 4, 4, 4},

        // Diagonal Lines
        new List<int>() {0, 1, 2, 3, 4},
        new List<int>() {4, 3, 2, 1, 0},
        new List<int>() {0, 1, 2, 1, 0},
        new List<int>() {4, 3, 2, 3, 4},
        new List<int>() {0, 2, 4, 2, 0},
        new List<int>() {4, 2, 0, 2, 4},

        // Zig Zags
        new List<int>() {0, 4, 0, 4, 0},
        new List<int>() {4, 0, 4, 0, 4},
        new List<int>() {3, 0, 3, 0, 3},
        new List<int>() {0, 3, 0, 3, 0},
        new List<int>() {2, 3, 2, 3, 2},
        new List<int>() {2, 1, 2, 1, 2},

        // Curves
        new List<int>() {1, 0, 0, 0, 1},
        new List<int>() {3, 4, 4, 4, 3},
        new List<int>() {0, 0, 1, 0, 0},
        new List<int>() {3, 3, 4, 3, 3},
        new List<int>() {1, 1, 0, 1, 1},
        new List<int>() {4, 4, 3, 4, 4}
    };

    public List<SymbolSO> Symbols;

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
        foreach (Transform reel in transform.Find("SlotReels"))
            reels.Add(reel.GetComponent<SlotReelController>());

        scoreText = transform.Find("ScoreText").GetComponent<TMP_Text>();
        payoutText = transform.Find("PayoutText").GetComponent<TMP_Text>();
        chipValueText = transform.Find("ChipValueText").GetComponent<TMP_Text>();
        paylineParent = transform.Find("Paylines");
        UpdateScore(2000);

        audiosource = GetComponent<AudioSource>();
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
            yield return new WaitForSeconds(0.4f);
        }
    }

    IEnumerator ScoreLines()
    {
        int gainedScore = 0;
        foreach(List<int> line in lines)
        {
            int consecutive = 1;
            SymbolSO symbol = reels[0].symbols[line[0]];
            for (int i = 1; i < line.Count; i ++)
            {
                SymbolSO curSymbol = reels[i].symbols[line[i]];
                if (curSymbol == symbol)
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
                        reels[i].transform.Find("BG/Borders").GetChild(line[i]).position.y -0.75f, 
                        paylineParent.position.z - 0.1f));
                Gradient gradient = new Gradient();
                gradient.SetColorKeys(
                    new GradientColorKey[] { new GradientColorKey(Color.red, 0f), new GradientColorKey(Color.red, 1f)}
                );
                if (consecutive < 5)
                    gradient.SetAlphaKeys(
                        new GradientAlphaKey[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.9f, (consecutive)/5f), new GradientAlphaKey(0.05f, (consecutive)/5f + 0.1f), new GradientAlphaKey(0.05f, 0.9f) }
                    );
                newPayline.colorGradient = gradient;
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

    private void ClearPayoutInfo()
    {
        payoutText.text = "0";
        for (int i = paylineParent.childCount - 1; i >= 0; i --)
        {
            Destroy(paylineParent.GetChild(i).gameObject);
        }   
    }

    private void UpdateScore(int gain)
    {
        score += gain;
        scoreText.text = score.ToString();
    }
    public SymbolSO SampleSymbol()
    {
        return Symbols[Random.Range(0, Symbols.Count)];
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