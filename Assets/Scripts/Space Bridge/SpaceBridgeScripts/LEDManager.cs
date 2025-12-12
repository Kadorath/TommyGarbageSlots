using UnityEngine;
using Phidget22;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System;

using Random = UnityEngine.Random;
using SpaceBridgeInput = PlayerInputManager.SpaceBridgeInput;

public class LEDManager : MonoBehaviour
{
    string config;

    private List<DigitalOutput> LEDs_Outer;
    private List<DigitalOutput> LEDs_Center;
    private List<DigitalOutput> LEDs_Inner;

    // Associates panel's input channels with LED output channels;
    private Dictionary<int, DigitalOutput> OuterInputToLED;
    private Dictionary<int, DigitalOutput> CenterInputToLED;
    private Dictionary<int, DigitalOutput> InnerInputToLED;

    public enum Panel
    {
        OUTER,
        CENTER,
        INNER
    }

    private List<LCD> LCDs;

    Coroutine ActivePattern;

    // Singleton
    public static LEDManager Instance;
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            LoadConfigFromResources();
            OpenLEDChannels();
            OpenLCDChannels();
        }
        else
            Destroy(this);
    }

    private void LoadConfigFromResources()
    {
        try
        {
            config = File.ReadAllText($"{Application.streamingAssetsPath}/LEDMgrConfig.txt");
        }
        catch (FileNotFoundException)
        {
            Debug.LogWarning("LEDMgrConfig could not find LEDMgrConfig.txt in Resources. Will load default configuration");
            config = "";
        }
    }

    private void OpenLEDChannels()
    {
        LEDs_Outer = new List<DigitalOutput>();
        LEDs_Center = new List<DigitalOutput>();
        LEDs_Inner = new List<DigitalOutput>();
        OuterInputToLED = new Dictionary<int, DigitalOutput>();
        CenterInputToLED = new Dictionary<int, DigitalOutput>();
        InnerInputToLED = new Dictionary<int, DigitalOutput>();

        List<DigitalOutput>[] panels = new List<DigitalOutput>[] { LEDs_Outer, LEDs_Center, LEDs_Inner };
        if (config == "") return;
        string[] outputInfo = config.Split("\n");

        for (int i = 0; i < outputInfo.Length; i++)
        {
            string[] line = outputInfo[i].Split(",");
            for (int j = 2; j < line.Length; j++)
            {
                string[] channels = line[j].Split("/");
                int ledChannel = int.Parse(channels[0]);
                int diChannel = channels.Length > 1 ? int.Parse(channels[1]) : -1;
                DigitalOutput newLED = new DigitalOutput();
                newLED.DeviceSerialNumber = int.Parse(line[1]);
                newLED.Channel = ledChannel;
                newLED.Open();
                switch (line[0])
                {
                    case "OUTER":
                        panels[0].Add(newLED);
                        if (diChannel != -1)
                            OuterInputToLED[diChannel] = newLED;
                        break;
                    case "CENTER":
                        panels[1].Add(newLED);
                        if (diChannel != -1)
                            CenterInputToLED[diChannel] = newLED;
                        break;
                    case "INNER":
                        panels[2].Add(newLED);
                        if (diChannel != -1)
                            InnerInputToLED[diChannel] = newLED;
                        break;
                }
            }
        }
    }

    private void OpenLCDChannels()
    {
        if (config == "") return;
        string[] outputInfo = config.Split("\n");

        LCDs = new List<LCD>(new LCD[3]);
        for (int i = 0; i < outputInfo.Length; i++)
        {
            string[] line = outputInfo[i].Split(",");
            if (!line[0].Contains("LCD")) continue;
            LCD newLCD = new LCD();
            newLCD.DeviceSerialNumber = int.Parse(line[1]);
            newLCD.Open();
            if (line[0].Contains("OUTER"))
                LCDs[0] = newLCD;
            else if (line[0].Contains("CENTER"))
                LCDs[1] = newLCD;
            else if (line[0].Contains("INNER"))
                LCDs[2] = newLCD;
        }
    }

    public bool WriteToLCD(SpaceBridgeInput console, string text)
    {
        bool written = false;
        LCD targetLCD = null;
        switch (console)
        {
            case SpaceBridgeInput.BUTTON_OUTER:
                targetLCD = LCDs[0];
                break;
            case SpaceBridgeInput.BUTTON_CENTER:
                targetLCD = LCDs[1];
                break;
            case SpaceBridgeInput.BUTTON_INNER:
                targetLCD = LCDs[2];
                break;
        }

        if (targetLCD != null && targetLCD.Attached)
        {
            targetLCD.Backlight = 1;
            targetLCD.WriteText(LCDFont.Dimensions_5x8, 0, 0, text);
            targetLCD.Flush();
            written = true;
        }

        return written;
    }

    void OnDestroy()
    {
        List<DigitalOutput>[] panels = new List<DigitalOutput>[] { LEDs_Outer, LEDs_Center, LEDs_Inner };
        foreach (List<DigitalOutput> panel in panels)
        {
            foreach (DigitalOutput led in panel)
            {
                led.Close();
            }
        }

        if (LCDs != null)
        {
            foreach (LCD lcd in LCDs)
            {
                if (lcd != null)
                    lcd.Close();
            }
        }
    }

    void OnApplicationQuit()
    {
        if (ActivePattern != null)
            StopCoroutine(ActivePattern);
        Destroy(gameObject);
    }

    //  ***
    //    LED Patterns
    //  ***
    public void AllSetTo(float level)
    {
        List<DigitalOutput>[] panels = new List<DigitalOutput>[] { LEDs_Outer, LEDs_Center, LEDs_Inner };
        foreach (List<DigitalOutput> panel in panels)
        {
            foreach (DigitalOutput led in panel)
            {
                led.DutyCycle = level;
            }
        }
    }

    public void LightByButton(SpaceBridgeInput type, int channel)
    {
        Debug.Log($"Lighting {type} {channel}");
        try
        {
            try
            {
                switch (type)
                {
                    case SpaceBridgeInput.BUTTON_INNER:
                        InnerInputToLED[channel].DutyCycle = 1f;
                        break;
                    case SpaceBridgeInput.BUTTON_CENTER:
                        CenterInputToLED[channel].DutyCycle = 1f;
                        break;
                    case SpaceBridgeInput.BUTTON_OUTER:
                        OuterInputToLED[channel].DutyCycle = 1f;
                        break;
                }
            }
            catch (KeyNotFoundException) { }
        } catch (PhidgetException e)
        {
            Debug.LogWarning(e);
        }
    }

    public void FlashList(List<Tuple<SpaceBridgeInput, int>> flashing, float defaultLevel)
    {
        if (ActivePattern != null)
            StopCoroutine(ActivePattern);
        ActivePattern = StartCoroutine(FlashList_aux(flashing, defaultLevel));
    }
    IEnumerator FlashList_aux(List<Tuple<SpaceBridgeInput, int>> flashing, float defaultLevel)
    {
        List<Panel> panelIDs = new List<Panel>() { Panel.OUTER, Panel.CENTER, Panel.INNER };
        List<float> blinkers = new List<float>() { 0.25f, 0.5f, 0.75f };
        while (true)
        {
            for (int i = 0; i < blinkers.Count; i++)
            {
                blinkers[i] -= Time.fixedDeltaTime;
                if (blinkers[i] <= 0f)
                {
                    blinkers[i] = 1f;
                }
            }
            AllSetTo(defaultLevel);
            foreach (Tuple<SpaceBridgeInput, int> toFlash in flashing)
            {
                Debug.Log(toFlash);
                switch (toFlash.Item1)
                {
                    case SpaceBridgeInput.BUTTON_INNER:
                        InnerInputToLED[toFlash.Item2].DutyCycle = blinkers[0] < 0.5f ? 0f : 1f;
                        break;
                    case SpaceBridgeInput.BUTTON_CENTER:
                        CenterInputToLED[toFlash.Item2].DutyCycle = blinkers[1] < 0.5f ? 0f : 1f;
                        break;
                    case SpaceBridgeInput.BUTTON_OUTER:
                        OuterInputToLED[toFlash.Item2].DutyCycle = blinkers[2] < 0.5f ? 0f : 1f;
                        break;
                }
            }

            yield return new WaitForFixedUpdate();
        }
    }

    public void AllFade(float rate)
    {
        if (ActivePattern != null)
            StopCoroutine(ActivePattern);
        ActivePattern = StartCoroutine(AllFade_aux(rate));
    }
    IEnumerator AllFade_aux(float rate)
    {
        List<DigitalOutput>[] panels = { LEDs_Outer, LEDs_Center, LEDs_Inner };
        while (true)
        {
            foreach (List<DigitalOutput> panel in panels)
            {
                foreach (DigitalOutput led in panel)
                {
                    if (led.Channel > 3)
                        led.DutyCycle = Mathf.Max(0f, (float)led.DutyCycle - rate);
                    else
                        led.DutyCycle = 1f;
                }
            }
            yield return new WaitForFixedUpdate();
        }
    }

    public void AllOn() { AllSetTo(1f); }

    public void AllOff() { AllSetTo(0f); }

    public void RiseOn()
    {
        if (ActivePattern != null)
            StopCoroutine(ActivePattern);
        ActivePattern = StartCoroutine(RiseOn_aux());
    }
    IEnumerator RiseOn_aux()
    {
        // Trackball
        LEDs_Outer[0].DutyCycle = 0.25f;
        LEDs_Outer[1].DutyCycle = 0.25f;
        LEDs_Outer[2].DutyCycle = 0.25f;

        // Outer Triangle Lower Row
        LEDs_Outer[3].DutyCycle = 1;
        LEDs_Outer[4].DutyCycle = 1;
        yield return new WaitForSeconds(0.25f);
        // TrackBall
        LEDs_Outer[0].DutyCycle = 0.5f;
        LEDs_Outer[1].DutyCycle = 0.5f;
        LEDs_Outer[2].DutyCycle = 0.5f;

        // Outer Triangle Inner Lower
        LEDs_Outer[8].DutyCycle = 1;
        yield return new WaitForSeconds(0.25f);
        // TrackBall
        LEDs_Outer[0].DutyCycle = 0.75f;
        LEDs_Outer[1].DutyCycle = 0.75f;
        LEDs_Outer[2].DutyCycle = 0.75f;

        // Outer Triangle Upper Row
        LEDs_Outer[5].DutyCycle = 1;
        LEDs_Outer[6].DutyCycle = 1;
        yield return new WaitForSeconds(0.25f);
        // TrackBall
        LEDs_Outer[0].DutyCycle = 1f;
        LEDs_Outer[1].DutyCycle = 1f;
        LEDs_Outer[2].DutyCycle = 1f;

        // Outer Trianlge Inner Upper
        LEDs_Outer[7].DutyCycle = 1;
    }

    public void RiseOff()
    {
        if (ActivePattern != null)
            StopCoroutine(ActivePattern);
        ActivePattern = StartCoroutine(RiseOff_aux());
    }
    IEnumerator RiseOff_aux()
    {
        // Outer Triangle Lower Row
        LEDs_Outer[3].DutyCycle = 0;
        LEDs_Outer[4].DutyCycle = 0;
        yield return new WaitForSeconds(0.25f);
        // TrackBall
        LEDs_Outer[0].DutyCycle = 0.75f;
        LEDs_Outer[1].DutyCycle = 0.75f;
        LEDs_Outer[2].DutyCycle = 0.75f;

        // Outer Triangle Inner Lower
        LEDs_Outer[8].DutyCycle = 0;
        yield return new WaitForSeconds(0.25f);
        // TrackBall
        LEDs_Outer[0].DutyCycle = 0.5f;
        LEDs_Outer[1].DutyCycle = 0.5f;
        LEDs_Outer[2].DutyCycle = 0.5f;

        // Outer Triangle Upper Row
        LEDs_Outer[5].DutyCycle = 0;
        LEDs_Outer[6].DutyCycle = 0;
        yield return new WaitForSeconds(0.25f);
        // TrackBall
        LEDs_Outer[0].DutyCycle = 0f;
        LEDs_Outer[1].DutyCycle = 0f;
        LEDs_Outer[2].DutyCycle = 0f;

        // Outer Trianlge Inner Upper
        LEDs_Outer[7].DutyCycle = 0;
    }

    public void RandomBubble()
    {
        if (ActivePattern != null)
            StopCoroutine(ActivePattern);
        ActivePattern = StartCoroutine(RandomBubble_aux());
    }
    IEnumerator RandomBubble_aux()
    {
        List<DigitalOutput>[] panels = new List<DigitalOutput>[] { LEDs_Outer, LEDs_Center, LEDs_Inner };
        while (true)
        {
            foreach (List<DigitalOutput> panel in panels)
            {
                foreach (DigitalOutput led in panel)
                {
                    try {
                        led.DutyCycle = Random.value < 0.5f ? 1f : 0f;
                    }
                    catch (PhidgetException)
                    {
                        Debug.LogWarning($"Phidget LED not attached {led.DeviceSerialNumber}:{led.Channel}");
                    }
                }
            }

            yield return new WaitForSeconds(1f);
        }
    }
}
