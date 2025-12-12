using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Phidget22;
using Phidget22.Events;
using Linearstar.Windows.RawInput;
using System.Runtime.InteropServices;
using System;
using Linearstar.Windows.RawInput.Native;
using System.IO;

public delegate void TrackballChangeEventHandler(object sender, TrackballChangeEventArgs e);
public class TrackballChangeEventArgs {
    public readonly PlayerInputManager.SpaceBridgeInput side;
    public readonly Vector2 delta;
    public readonly int id;
    internal TrackballChangeEventArgs(PlayerInputManager.SpaceBridgeInput side, Vector2 d, int id)
    {
        this.side = side;
        delta = d;
        this.id = id;
    }
}

public delegate void PhidgetInputChangeEventHandler(object sender, PhidgetInputChangeEventArgs e);
public class PhidgetInputChangeEventArgs {
    public readonly float value;
    public readonly PlayerInputManager.SpaceBridgeInput type;
    public readonly int channel;
    public readonly int rawChannel;
    internal PhidgetInputChangeEventArgs(PlayerInputManager.SpaceBridgeInput t, int c, float v, int rawChannel)
    {
        type = t;
        channel = c;
        value = v;
        this.rawChannel = rawChannel;
    }
}

public delegate void BridgeRFIDTagEventHandler(object sender, BridgeRFIDTagEventArgs e);
public class BridgeRFIDTagEventArgs {
    public readonly string tag;
    public readonly PlayerInputManager.SpaceBridgeInput type;
    internal BridgeRFIDTagEventArgs(PlayerInputManager.SpaceBridgeInput t, string tag)
    {
        type = t;
        this.tag = tag;
    }
}

public class PlayerInputManager : MonoBehaviour
{
    [SerializeField] bool swapDisplaySides = false;
    public enum SpaceBridgeSide
    {
        DEFAULT,
        LEFT,
        RIGHT
    }
    public SpaceBridgeSide Side;

    // To get the hWnd for trackball input
    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int nIndex, IntPtr dwNewLong);
    [DllImport("user32.dll")]
    private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    private static WndProcDelegate _newWndProc;
    private static IntPtr _originalWndProc = IntPtr.Zero;
    private const int GWLP_WNDPROC = -4;
    private IntPtr unityWindowHandle;

    // Input Config
    string config;
    public enum SpaceBridgeInput
    {
        JOYSTICK,
        TRACKBALL_OUTER,
        TRACKBALL_INNER,
        BUTTON_INNER,
        BUTTON_CENTER,
        BUTTON_OUTER
    }
    private Dictionary<int, SpaceBridgeInput> SerialNoToType;
    private List<DigitalInput> Buttons_Joystick;
    private List<DigitalInput> Buttons_Inner;
    private List<DigitalInput> Buttons_Center;
    private List<DigitalInput> Buttons_Outer;

    // Trackballs
    public static Dictionary<string, int> trackballs;
    public static Tuple<int, string> innerTrackball;
    public static Tuple<int, string> outerTrackball;
    public static event TrackballChangeEventHandler TrackballChange;

    // Phidgets
    private Stack<PhidgetInputChangeEventArgs> phidgetEvents;
    public static event PhidgetInputChangeEventHandler PhidgetInput;
    private Stack<BridgeRFIDTagEventArgs> RFIDEvents;
    public static event BridgeRFIDTagEventHandler RFIDTagged;
    private RFID RFID_Center;
    private RFID RFID_Outer;
    private RFID RFID_Inner;

    // Singleton
    public static PlayerInputManager Instance;
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            LoadConfigFromResources();
            OpenPhidgetChannels();
            RegisterTrackballs();
            SetMultipleDisplay();
            unityWindowHandle = GetForegroundWindow();
            _newWndProc = CustomWndProc;
            _originalWndProc = SetWindowLongPtr(unityWindowHandle, GWLP_WNDPROC, Marshal.GetFunctionPointerForDelegate(_newWndProc));
        }
        else
            Destroy(this);
    }

    private void LoadConfigFromResources()
    {
        string fileName = "PlayerInputMgrConfig.txt";
        if (Side == SpaceBridgeSide.LEFT)
            fileName = "PlayerInputMgrConfig_Left.txt";
        else if (Side == SpaceBridgeSide.RIGHT)
            fileName = "PlayerInputMgrConfig_Right.txt";

        try
        {
            Debug.Log($"Attempting to load in configuration from {Application.streamingAssetsPath}/{fileName}");
            config = File.ReadAllText($"{Application.streamingAssetsPath}/{fileName}");
        }
        catch (FileNotFoundException)
        {
            Debug.LogWarning($"PlayerInputManager could not find {fileName} in Resources. Will load default configuration");
            config = "TRACKBALL_INNER,0\nTRACKBALL_OUTER,1";
        }
    }

    public void SetMultipleDisplay()
    {
        for (int i = 1; i < Display.displays.Length; i++)
        {
            Debug.Log(Display.displays[i]);
            Display.displays[i].Activate();
        }
    }

    bool OpenPhidgetChannels()
    {
        SerialNoToType = new Dictionary<int, SpaceBridgeInput>();
        Buttons_Joystick = new List<DigitalInput>();
        Buttons_Outer = new List<DigitalInput>();
        Buttons_Center = new List<DigitalInput>();
        Buttons_Inner = new List<DigitalInput>();
        foreach (string line in config.Split("\n"))
        {
            string[] controlInfo = line.Split(",");
            Debug.Log($"Setting up {controlInfo[0]} with serial no. {controlInfo[1]}");
            switch (controlInfo[0])
            {
                case "JOYSTICK":
                    SerialNoToType.Add(int.Parse(controlInfo[1]), SpaceBridgeInput.JOYSTICK);
                    for (int i = 2; i < controlInfo.Length; i++)
                    {
                        DigitalInput newDI = new DigitalInput();
                        newDI.DeviceSerialNumber = int.Parse(controlInfo[1]);
                        newDI.Channel = int.Parse(controlInfo[i]);
                        newDI.Open();
                        Buttons_Joystick.Add(newDI);
                    }
                    break;
                case "BTNOUTER":
                    SerialNoToType.Add(int.Parse(controlInfo[1]), SpaceBridgeInput.BUTTON_OUTER);
                    for (int i = 2; i < controlInfo.Length; i++)
                    {
                        DigitalInput newDI = new DigitalInput();
                        newDI.DeviceSerialNumber = int.Parse(controlInfo[1]);
                        newDI.Channel = int.Parse(controlInfo[i]);
                        newDI.Open();
                        Buttons_Outer.Add(newDI);
                    }
                    break;
                case "BTNCENTER":
                    SerialNoToType.Add(int.Parse(controlInfo[1]), SpaceBridgeInput.BUTTON_CENTER);
                    for (int i = 2; i < controlInfo.Length; i++)
                    {
                        DigitalInput newDI = new DigitalInput();
                        newDI.DeviceSerialNumber = int.Parse(controlInfo[1]);
                        newDI.Channel = int.Parse(controlInfo[i]);
                        newDI.Open();
                        Buttons_Center.Add(newDI);
                    }
                    break;
                case "BTNINNER":
                    SerialNoToType.Add(int.Parse(controlInfo[1]), SpaceBridgeInput.BUTTON_INNER);
                    for (int i = 2; i < controlInfo.Length; i++)
                    {
                        DigitalInput newDI = new DigitalInput();
                        newDI.DeviceSerialNumber = int.Parse(controlInfo[1]);
                        newDI.Channel = int.Parse(controlInfo[i]);
                        newDI.Open();
                        Buttons_Inner.Add(newDI);
                    }
                    break;
                case "TRACKBALL_OUTER":
                    outerTrackball = new Tuple<int, string>(-1, controlInfo[1].Trim());
                    break;
                case "TRACKBALL_INNER":
                    innerTrackball = new Tuple<int, string>(-1, controlInfo[1].Trim());
                    break;
                case "RFID_CENTER":
                    RFID_Center = new RFID();
                    RFID_Center.DeviceSerialNumber = int.Parse(controlInfo[1]);
                    RFID_Center.Tag += RFID_Tagged_Center;
                    RFID_Center.Open();
                    break;
                case "RFID_OUTER":
                    RFID_Outer = new RFID();
                    RFID_Outer.DeviceSerialNumber = int.Parse(controlInfo[1]);
                    RFID_Outer.Tag += RFID_Tagged_Outer;
                    RFID_Outer.Open();
                    break;
                case "RFID_INNER":
                    RFID_Inner = new RFID();
                    RFID_Inner.DeviceSerialNumber = int.Parse(controlInfo[1]);
                    RFID_Inner.Tag += RFID_Tagged_Inner;
                    RFID_Inner.Open();
                    break;
                default:
                    Debug.LogWarning($"Unknown input type {controlInfo[0]} read in PlayerInputMgrConfig. Skipping...");
                    break;
            }
        }
        return true;
    }

    bool RegisterTrackballs()
    {
        RawInputDevice[] devices = RawInputDevice.GetDevices();
        List<RawInputMouse> mice = devices.OfType<RawInputMouse>().ToList();
        trackballs = new Dictionary<string, int>();
        for (int i = 0; i < mice.Count; i++)
        {
            RawInputMouse m = mice[i];
            if (!trackballs.ContainsKey(m.DevicePath))
            {
                trackballs.Add(m.DevicePath, i);
                
                if (outerTrackball.Item2.Equals(m.DevicePath))
                    outerTrackball = new Tuple<int, string>(i, m.DevicePath);
                if (innerTrackball.Item2.Equals(m.DevicePath))
                    innerTrackball = new Tuple<int, string>(i, m.DevicePath);
            }
            Debug.Log($"{m.ProductName} : PID {m.ProductId} : VID {m.VendorId} : HID {m.DevicePath}");
        }
        return true;
    }

    private static IntPtr CustomWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        const int WM_INPUT = 0x00FF;
        if (msg == WM_INPUT)
        {
            RawInputData data = RawInputData.FromHandle(lParam);
            if (data.Device is RawInputMouse)
            {
                string dPath = ((RawInputMouse)data.Device).DevicePath;
                int trackballID = trackballs[dPath];
                if (trackballs.ContainsKey(dPath)
                    && (innerTrackball.Item2 == dPath || outerTrackball.Item2 == dPath))
                {
                    SpaceBridgeInput side = innerTrackball.Item2 == dPath ? SpaceBridgeInput.TRACKBALL_INNER : SpaceBridgeInput.TRACKBALL_OUTER;
                    RawMouse mouse = ((RawInputMouseData)data).Mouse;
                    TrackballChange?.Invoke(Instance, new TrackballChangeEventArgs(side, new Vector2(mouse.LastX, mouse.LastY), trackballID));
                }
                else
                {
                    RawMouse mouse = ((RawInputMouseData)data).Mouse;
                    TrackballChange?.Invoke(Instance, new TrackballChangeEventArgs(SpaceBridgeInput.JOYSTICK, new Vector2(mouse.LastX, mouse.LastY), trackballID));
                }
                return IntPtr.Zero;
            }
        }

        return CallWindowProc(_originalWndProc, hWnd, msg, wParam, lParam);
    }

    // Start is called before the first frame update
    void Start()
    {
        // Set up event stack and event listeners for all DigitalInput StateChange events
        phidgetEvents = new Stack<PhidgetInputChangeEventArgs>();
        foreach (DigitalInput input in Buttons_Joystick)
            input.StateChange += onStateChange;
        foreach (DigitalInput input in Buttons_Outer)
            input.StateChange += onStateChange;
        foreach (DigitalInput input in Buttons_Center)
            input.StateChange += onStateChange;
        foreach (DigitalInput input in Buttons_Inner)
            input.StateChange += onStateChange;

        // Set up RFID event stack
        RFIDEvents = new Stack<BridgeRFIDTagEventArgs>();

        // Set Inner camera and Outer camera target display based on swapDisplaySides
        Camera outerCam = null;
        Camera innerCam = null;
        try {
            outerCam = GameObject.Find("Outer Camera").GetComponent<Camera>();
            if (outerCam == null)
                throw new NullReferenceException();
        } catch (NullReferenceException)
        {
            Debug.LogWarning("Could not find Camera component on object 'Outer Camera'. Will not update target displays.");
        }
        try
        {
            innerCam = GameObject.Find("Inner Camera").GetComponent<Camera>();
            if (innerCam == null)
                throw new NullReferenceException();
        }  catch (NullReferenceException)
        {
            Debug.LogWarning("Could not find Camera component on object 'Inner Camera'. Will not update target displays.");
        }

        if (outerCam != null && innerCam != null)
        {
            int outerOriginal = outerCam.targetDisplay;
            int innerOriginal = innerCam.targetDisplay;
            outerCam.targetDisplay = swapDisplaySides ? innerOriginal : outerOriginal;
            innerCam.targetDisplay = swapDisplaySides ? outerOriginal: innerOriginal;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
#if UNITY_STANDALONE
            Application.Quit();
#endif
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        while (phidgetEvents.Count > 0)
            PhidgetInput?.Invoke(this, phidgetEvents.Pop());
        
        while (RFIDEvents.Count > 0)
            RFIDTagged?.Invoke(this, RFIDEvents.Pop());

        UpdateKeyboardInput();
    }

    private void UpdateKeyboardInput()
    {
        // Keyboard mapping to simulate Phidget inputs
        // Joystick: Outer Console
        if (Input.GetKeyDown(KeyCode.W))
            PhidgetInput?.Invoke(this, new PhidgetInputChangeEventArgs(SpaceBridgeInput.JOYSTICK, 0, 1f, -1));
        else if (Input.GetKeyDown(KeyCode.D))
            PhidgetInput?.Invoke(this, new PhidgetInputChangeEventArgs(SpaceBridgeInput.JOYSTICK, 1, 1f, -1));
        if (Input.GetKeyDown(KeyCode.A))
            PhidgetInput?.Invoke(this, new PhidgetInputChangeEventArgs(SpaceBridgeInput.JOYSTICK, 3, 1f, -1));
        else if (Input.GetKeyDown(KeyCode.S))
            PhidgetInput?.Invoke(this, new PhidgetInputChangeEventArgs(SpaceBridgeInput.JOYSTICK, 2, 1f, -1));

        if (Input.GetKeyUp(KeyCode.W))
            PhidgetInput?.Invoke(this, new PhidgetInputChangeEventArgs(SpaceBridgeInput.JOYSTICK, 0, 0f, -1));
        else if (Input.GetKeyUp(KeyCode.D))
            PhidgetInput?.Invoke(this, new PhidgetInputChangeEventArgs(SpaceBridgeInput.JOYSTICK, 1, 0f, -1));
        if (Input.GetKeyUp(KeyCode.A))
            PhidgetInput?.Invoke(this, new PhidgetInputChangeEventArgs(SpaceBridgeInput.JOYSTICK, 3, 0f, -1));
        else if (Input.GetKeyUp(KeyCode.S))
            PhidgetInput?.Invoke(this, new PhidgetInputChangeEventArgs(SpaceBridgeInput.JOYSTICK, 2, 0f, -1));

        // Buttons: Inner Console
        if (Input.GetKeyDown(KeyCode.Y))
            PhidgetInput?.Invoke(this, new PhidgetInputChangeEventArgs(SpaceBridgeInput.BUTTON_INNER, 0, 1f, -1));
        if (Input.GetKeyDown(KeyCode.U))
            PhidgetInput?.Invoke(this, new PhidgetInputChangeEventArgs(SpaceBridgeInput.BUTTON_INNER, 1, 1f, -1));
        if (Input.GetKeyDown(KeyCode.I))
            PhidgetInput?.Invoke(this, new PhidgetInputChangeEventArgs(SpaceBridgeInput.BUTTON_INNER, 2, 1f, -1));
        if (Input.GetKeyDown(KeyCode.O))
            PhidgetInput?.Invoke(this, new PhidgetInputChangeEventArgs(SpaceBridgeInput.BUTTON_INNER, 3, 1f, -1));
        if (Input.GetKeyDown(KeyCode.P))
            PhidgetInput?.Invoke(this, new PhidgetInputChangeEventArgs(SpaceBridgeInput.BUTTON_INNER, 4, 1f, -1));
        if (Input.GetKeyDown(KeyCode.LeftBracket))
            PhidgetInput?.Invoke(this, new PhidgetInputChangeEventArgs(SpaceBridgeInput.BUTTON_INNER, 5, 1f, -1));

        if (Input.GetKeyUp(KeyCode.Y))
            PhidgetInput?.Invoke(this, new PhidgetInputChangeEventArgs(SpaceBridgeInput.BUTTON_INNER, 0, 0f, -1));
        if (Input.GetKeyUp(KeyCode.U))
            PhidgetInput?.Invoke(this, new PhidgetInputChangeEventArgs(SpaceBridgeInput.BUTTON_INNER, 1, 0f, -1));
        if (Input.GetKeyUp(KeyCode.I))
            PhidgetInput?.Invoke(this, new PhidgetInputChangeEventArgs(SpaceBridgeInput.BUTTON_INNER, 2, 0f, -1));
        if (Input.GetKeyUp(KeyCode.O))
            PhidgetInput?.Invoke(this, new PhidgetInputChangeEventArgs(SpaceBridgeInput.BUTTON_INNER, 3, 0f, -1));
        if (Input.GetKeyUp(KeyCode.P))
            PhidgetInput?.Invoke(this, new PhidgetInputChangeEventArgs(SpaceBridgeInput.BUTTON_INNER, 4, 0f, -1));
        if (Input.GetKeyUp(KeyCode.LeftBracket))
            PhidgetInput?.Invoke(this, new PhidgetInputChangeEventArgs(SpaceBridgeInput.BUTTON_INNER, 5, 0f, -1));

        // Buttons: Center Console (10-15)
        if (Input.GetKeyDown(KeyCode.Z))
            PhidgetInput?.Invoke(this, new PhidgetInputChangeEventArgs(SpaceBridgeInput.BUTTON_CENTER, 0, 1f, -1));
        if (Input.GetKeyDown(KeyCode.X))
            PhidgetInput?.Invoke(this, new PhidgetInputChangeEventArgs(SpaceBridgeInput.BUTTON_CENTER, 1, 1f, -1));
        if (Input.GetKeyDown(KeyCode.C))
            PhidgetInput?.Invoke(this, new PhidgetInputChangeEventArgs(SpaceBridgeInput.BUTTON_CENTER, 2, 1f, -1));
        if (Input.GetKeyDown(KeyCode.B))
            PhidgetInput?.Invoke(this, new PhidgetInputChangeEventArgs(SpaceBridgeInput.BUTTON_CENTER, 3, 1f, -1));
        if (Input.GetKeyDown(KeyCode.N))
            PhidgetInput?.Invoke(this, new PhidgetInputChangeEventArgs(SpaceBridgeInput.BUTTON_CENTER, 4, 1f, -1));
        if (Input.GetKeyDown(KeyCode.M))
            PhidgetInput?.Invoke(this, new PhidgetInputChangeEventArgs(SpaceBridgeInput.BUTTON_CENTER, 5, 1f, -1));

        if (Input.GetKeyUp(KeyCode.Z))
            PhidgetInput?.Invoke(this, new PhidgetInputChangeEventArgs(SpaceBridgeInput.BUTTON_CENTER, 0, 0f, -1));
        if (Input.GetKeyUp(KeyCode.X))
            PhidgetInput?.Invoke(this, new PhidgetInputChangeEventArgs(SpaceBridgeInput.BUTTON_CENTER, 1, 0f, -1));
        if (Input.GetKeyUp(KeyCode.C))
            PhidgetInput?.Invoke(this, new PhidgetInputChangeEventArgs(SpaceBridgeInput.BUTTON_CENTER, 2, 0f, -1));
        if (Input.GetKeyUp(KeyCode.B))
            PhidgetInput?.Invoke(this, new PhidgetInputChangeEventArgs(SpaceBridgeInput.BUTTON_CENTER, 3, 0f, -1));
        if (Input.GetKeyUp(KeyCode.N))
            PhidgetInput?.Invoke(this, new PhidgetInputChangeEventArgs(SpaceBridgeInput.BUTTON_CENTER, 4, 0f, -1));
        if (Input.GetKeyUp(KeyCode.M))
            PhidgetInput?.Invoke(this, new PhidgetInputChangeEventArgs(SpaceBridgeInput.BUTTON_CENTER, 5, 0f, -1));

        // Buttons: Outer Console
        if (Input.GetKeyDown(KeyCode.H))
            PhidgetInput?.Invoke(this, new PhidgetInputChangeEventArgs(SpaceBridgeInput.BUTTON_OUTER, 0, 1f, -1));
        if (Input.GetKeyDown(KeyCode.J))
            PhidgetInput?.Invoke(this, new PhidgetInputChangeEventArgs(SpaceBridgeInput.BUTTON_OUTER, 1, 1f, -1));
        if (Input.GetKeyDown(KeyCode.K))
            PhidgetInput?.Invoke(this, new PhidgetInputChangeEventArgs(SpaceBridgeInput.BUTTON_OUTER, 2, 1f, -1));
        if (Input.GetKeyDown(KeyCode.L))
            PhidgetInput?.Invoke(this, new PhidgetInputChangeEventArgs(SpaceBridgeInput.BUTTON_OUTER, 3, 1f, -1));
        if (Input.GetKeyDown(KeyCode.Semicolon))
            PhidgetInput?.Invoke(this, new PhidgetInputChangeEventArgs(SpaceBridgeInput.BUTTON_OUTER, 4, 1f, -1));
        if (Input.GetKeyDown(KeyCode.Quote))
            PhidgetInput?.Invoke(this, new PhidgetInputChangeEventArgs(SpaceBridgeInput.BUTTON_OUTER, 5, 1f, -1));

        if (Input.GetKeyUp(KeyCode.H))
            PhidgetInput?.Invoke(this, new PhidgetInputChangeEventArgs(SpaceBridgeInput.BUTTON_OUTER, 0, 0f, -1));
        if (Input.GetKeyUp(KeyCode.J))
            PhidgetInput?.Invoke(this, new PhidgetInputChangeEventArgs(SpaceBridgeInput.BUTTON_OUTER, 1, 0f, -1));
        if (Input.GetKeyUp(KeyCode.K))
            PhidgetInput?.Invoke(this, new PhidgetInputChangeEventArgs(SpaceBridgeInput.BUTTON_OUTER, 2, 0f, -1));
        if (Input.GetKeyUp(KeyCode.L))
            PhidgetInput?.Invoke(this, new PhidgetInputChangeEventArgs(SpaceBridgeInput.BUTTON_OUTER, 3, 0f, -1));
        if (Input.GetKeyUp(KeyCode.Semicolon))
            PhidgetInput?.Invoke(this, new PhidgetInputChangeEventArgs(SpaceBridgeInput.BUTTON_OUTER, 4, 0f, -1));
        if (Input.GetKeyUp(KeyCode.Quote))
            PhidgetInput?.Invoke(this, new PhidgetInputChangeEventArgs(SpaceBridgeInput.BUTTON_OUTER, 5, 0f, -1));


        // Trackball Outer
        float dX = 0f;
        float dY = 0f;
        if (Input.GetKey(KeyCode.LeftArrow)) dX = -3f;
        else if (Input.GetKey(KeyCode.RightArrow)) dX = 3f;
        if (Input.GetKey(KeyCode.UpArrow)) dY = -3f;
        else if (Input.GetKey(KeyCode.DownArrow)) dY = 3f;

        if (dX != 0f || dY != 0f)
        {
            SpaceBridgeInput side = SpaceBridgeInput.TRACKBALL_OUTER;
            if (Input.GetKey(KeyCode.RightShift))
                side = SpaceBridgeInput.TRACKBALL_INNER;

            TrackballChange?.Invoke(Instance, new TrackballChangeEventArgs(side, new Vector2(dX, dY), -1));
        }
    }

    void onStateChange(object sender, DigitalInputStateChangeEventArgs e)
    {
        DigitalInput input = sender as DigitalInput;
        Debug.Log($"Channel: {input.Channel}, Value: {e.State}");
        SpaceBridgeInput inType = SerialNoToType[input.DeviceSerialNumber];

        int virtualChannel = -1;
        List<DigitalInput> buttonList = null;
        switch (inType)
        {
            case SpaceBridgeInput.BUTTON_INNER:
                buttonList = Buttons_Inner;
                break;
            case SpaceBridgeInput.BUTTON_CENTER:
                buttonList = Buttons_Center;
                break;
            case SpaceBridgeInput.BUTTON_OUTER:
                buttonList = Buttons_Outer;
                break;
            case SpaceBridgeInput.JOYSTICK:
                buttonList = Buttons_Joystick;
                break;
        }

        if (buttonList != null)
        {
            for (int i = 0; i < buttonList.Count; i ++)
            {
                if (buttonList[i] == input)
                {
                    virtualChannel = i;
                    break;
                }
            }
        }

        phidgetEvents.Push(new PhidgetInputChangeEventArgs(inType, virtualChannel, e.State ? 1f : 0f, input.Channel));
    }

    // Phidget22's RFIDTagEventArgs does not include information about the RFID reader that registered
    // the tag. Therefore, these three delegates are what the PlayerInputManager uses to distinguish the
    // device sides.
    void RFID_Tagged_Center(object sender, RFIDTagEventArgs e)
    {
        Debug.Log($"RFID TAG on CENTER: {e.Tag}");
        RFIDEvents.Push(new BridgeRFIDTagEventArgs(SpaceBridgeInput.BUTTON_CENTER, e.Tag));
    }
    void RFID_Tagged_Outer(object sender, RFIDTagEventArgs e)
    {
        Debug.Log($"RFID TAG on OUTER: {e.Tag}");
        RFIDEvents.Push(new BridgeRFIDTagEventArgs(SpaceBridgeInput.BUTTON_OUTER, e.Tag));
    }
    void RFID_Tagged_Inner(object sender, RFIDTagEventArgs e)
    {
        Debug.Log($"RFID TAG on INNER: {e.Tag}");
        RFIDEvents.Push(new BridgeRFIDTagEventArgs(SpaceBridgeInput.BUTTON_INNER, e.Tag));
    }

    void OnDestroy()
    {
        // Restore the window procedure to its original procedure
        SetWindowLongPtr(unityWindowHandle, GWLP_WNDPROC, _originalWndProc);

        // Close each Digital Input
        foreach (DigitalInput input in Buttons_Joystick)
            input.Close();
        foreach (DigitalInput input in Buttons_Outer)
            input.Close();
        foreach (DigitalInput input in Buttons_Center)
            input.Close();
        foreach (DigitalInput input in Buttons_Inner)
            input.Close();

        // Close each RFID reader, if they are present
        if (RFID_Center != null)
            RFID_Center.Close();
        if (RFID_Outer != null)
            RFID_Outer.Close();
        if (RFID_Inner != null)
            RFID_Inner.Close();
    }

    void OnApplicationQuit()
    {
        Destroy(gameObject);
    }
}