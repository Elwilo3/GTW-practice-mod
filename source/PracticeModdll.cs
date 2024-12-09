using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System;
using System.Collections;
using HarmonyLib;
using Isto.GTW.Player;
using System.Reflection;
using Isto.Core.StateMachine;
using Isto.GTW.Player.States;

namespace SpeedrunPracticeMod
{
    [BepInPlugin("com.Palmblom.SpeedrunPracticeMod", "SpeedrunPracticeMod", "1.0.0")]
    public class PracticeMod : BaseUnityPlugin
    {
        // GUI Layout Constants
        private const float MARGIN = 20f;
        private const float PADDING = 10f;
        private const float ELEMENT_HEIGHT = 20f;
        private const float BUTTON_HEIGHT = 25f;
        private const float BOX_PADDING = 20f;
        private const float MENU_WIDTH = 300f;

        private Dictionary<string, CheckpointData> customCheckpoints = new Dictionary<string, CheckpointData>();
        private Dictionary<string, CheckpointData> presetCheckpoints = new Dictionary<string, CheckpointData>()
    {
        { "Spawn", new CheckpointData(new Vector3(-390.3687f, 144.25f, 112.2642f), Vector3.zero, Quaternion.identity, Quaternion.identity) },
        { "Interview", new CheckpointData(new Vector3(-289.6071f, 195.25f, 82.85135f), Vector3.zero, Quaternion.identity, Quaternion.identity) },
        { "Warehouse Trainee", new CheckpointData(new Vector3(-242.1583f, 83.25997f, 133.4567f), Vector3.zero, Quaternion.identity, Quaternion.identity) },
        { "Warehouse Worker", new CheckpointData(new Vector3(-208.2579f, 97.25f, 139.1872f), Vector3.zero, Quaternion.identity, Quaternion.identity) },
        { "Unpaid Intern", new CheckpointData(new Vector3(-170.3318f, 234.25f, 122.8188f), Vector3.zero, Quaternion.identity, Quaternion.identity) },
        { "Junior Financial Analyst", new CheckpointData(new Vector3(-131.5249f, 304.6553f, 78.66881f), Vector3.zero, Quaternion.identity, Quaternion.identity) },
        { "Wine Mixer", new CheckpointData(new Vector3(-86.7677f, 405.25f, -24.72382f), Vector3.zero, Quaternion.identity, Quaternion.identity) },
        { "Middle Manger Interview", new CheckpointData(new Vector3(-99.91953f, 503.8748f, -14.55534f), Vector3.zero, Quaternion.identity, Quaternion.identity) },
        { "Middle Management", new CheckpointData(new Vector3(-92.28833f, 437.7774f, 172.3422f), Vector3.zero, Quaternion.identity, Quaternion.identity) },
        { "Leave Company", new CheckpointData(new Vector3(-190.7224f, 487.25f, 243.2324f), Vector3.zero, Quaternion.identity, Quaternion.identity) },
        { "Department Head", new CheckpointData(new Vector3(-156.9074f, 539.25f, 298.8533f), Vector3.zero, Quaternion.identity, Quaternion.identity) },
        { "Vice President", new CheckpointData(new Vector3(-95.37256f, 624.7501f, 315.2366f), Vector3.zero, Quaternion.identity, Quaternion.identity) },
        { "CEO", new CheckpointData(new Vector3(-79.30243f, 791.2501f, 313.3521f), Vector3.zero, Quaternion.identity, Quaternion.identity) }
    };
        private class MenuLayout
        {
            public float MenuX { get; private set; }
            public float CurrentY { get; private set; }
            public float Width { get; private set; }

            public MenuLayout(float menuX, float startY, float width)
            {
                MenuX = menuX;
                CurrentY = startY;
                Width = width;
            }

            public void AddSpace(float space)
            {
                CurrentY += space;
            }

            public Rect GetRect(float height)
            {
                Rect rect = new Rect(MenuX + MARGIN, CurrentY, Width - (MARGIN * 2), height);
                CurrentY += height + PADDING;
                return rect;
            }

            public Rect GetElementRect(float height, float extraPadding = 0)
            {
                Rect rect = new Rect(MenuX + MARGIN + PADDING, CurrentY, Width - (MARGIN * 2) - (PADDING * 2), height);
                CurrentY += height + PADDING + extraPadding;
                return rect;
            }
        }

        private class CheckpointData
        {
            public Vector3 Position { get; set; }
            public Vector3 Velocity { get; set; }
            public Quaternion PlayerRotation { get; set; }
            public Quaternion CameraRotation { get; set; }

            public CheckpointData(Vector3 position, Vector3 velocity, Quaternion playerRotation, Quaternion cameraRotation)
            {
                Position = position;
                Velocity = velocity;
                PlayerRotation = playerRotation;
                CameraRotation = cameraRotation;
            }
        }

        // Basic variables
        private bool showMenu = false;
        private float notificationTime = 0f;
        private string notificationText = "";
        private Vector2 scrollPosition = Vector2.zero;
        private string selectedCheckpoint = "";
        private string newCheckpointName = "";
        private Transform playerTransform;
        private string saveFilePath;
        private bool showingPresets = false;
        private Camera mainCamera;

        // Control configurations
        private ConfigEntry<KeyCode> menuKey;
        private ConfigEntry<KeyCode> noclipKey;
        private ConfigEntry<KeyCode> teleportKey;
        private ConfigEntry<KeyCode> timeScaleKey;
        private ConfigEntry<KeyCode> tempCheckpointKey;
        private ConfigEntry<KeyCode> physicsModKey;
        private ConfigEntry<KeyCode> ragdollToggleKey;
        private bool showingControls = false;
        private KeyCode? waitingForKey = null;
        private string waitingForKeyBinding = "";

        // Time Scale variables
        private bool isTimeScaleActive = false;
        private float timeScaleValue = 0.5f;
        private float normalTimeScale = 1f;

        // Category selection
        private enum MenuCategory { None, Movement, Checkpoints, Controls }
        private MenuCategory currentCategory = MenuCategory.None;

        // CheckPoint 
        private CheckpointData temporaryCheckpoint = null;
        private const string TEMP_CHECKPOINT_NAME = "Temporary Checkpoint";

        // Noclip variables
        private Rigidbody playerRigidbody;
        private Collider playerCollider;
        private float moveSpeed = 10f;
        private float fastMultiplier = 3f;
        private bool isNoclipActive = false;
        private bool keepNoclipAfterTeleport = true;
        private bool waitingForMovement = false;

        // Ball Physics variables
        private PlayerController playerController;
        private TorquePhysics torquePhysics;
        private bool valuesStored = false;

        // Original values storage
        private float originalFriction;
        private float originalAcceleration;
        private float originalDrag;

        // Custom physics values
        private float customFriction = 1.0f;
        private float customAcceleration = 200f;
        private float customDrag = 0.01f;
        private float minSpeed = 10f;

        // Individual toggle flags
        private bool frictionModEnabled = false;
        private bool accelerationModEnabled = false;
        private bool dragModEnabled = false;
        private bool constantSpeedModEnabled = false;
        private bool ballVisualizerEnabled = false;

        // Mod master switches
        private bool physicsModEnabled = false;
        private bool ragdollEnabled = true;

        public static PracticeMod Instance;

        private Harmony harmony;

        private void Awake()
        {
            Instance = this;

            // Config setup
            menuKey = Config.Bind("Controls", "MenuKey", KeyCode.Tab, "Key to toggle the practice menu");
            noclipKey = Config.Bind("Controls", "NoclipKey", KeyCode.N, "Key to toggle noclip mode");
            teleportKey = Config.Bind("Controls", "TeleportKey", KeyCode.O, "Key to teleport to selected checkpoint");
            timeScaleKey = Config.Bind("Controls", "TimeScaleKey", KeyCode.P, "Key to toggle time scale");
            tempCheckpointKey = Config.Bind("Controls", "TempCheckpointKey", KeyCode.C, "Key to set temporary checkpoint");
            physicsModKey = Config.Bind("Controls", "PhysicsModKey", KeyCode.K, "Key to toggle physics mod");
            ragdollToggleKey = Config.Bind("Controls", "RagdollToggleKey", KeyCode.R, "Key to toggle ragdoll effect");

            saveFilePath = Path.Combine(Paths.ConfigPath, "customcheckpoints.txt");
            LoadCheckpoints();

            // Harmony setup
            harmony = new Harmony("com.Palmblom.SpeedrunPracticeMod");

            try
            {
                // Patch TorquePhysics.UpdateTorque
                var torqueMethod = typeof(TorquePhysics).GetMethod("UpdateTorque", BindingFlags.NonPublic | BindingFlags.Instance);
                var prefix = typeof(PracticeMod).GetMethod(nameof(UpdateTorquePrefix), BindingFlags.NonPublic | BindingFlags.Static);
                harmony.Patch(torqueMethod, new HarmonyMethod(prefix));

                // Patch PlayerController.UpdateDirectionalVectorsAndSpeed
                var speedMethod = typeof(PlayerController).GetMethod("UpdateDirectionalVectorsAndSpeed", BindingFlags.Public | BindingFlags.Instance);
                var postfix = typeof(PracticeMod).GetMethod(nameof(UpdateDirectionalVectorsAndSpeedPostfix), BindingFlags.NonPublic | BindingFlags.Static);
                harmony.Patch(speedMethod, null, new HarmonyMethod(postfix));

                // Patch PlayerRagdoll.CollisionHit
                var collisionHitMethod = typeof(PlayerRagdoll).GetMethod("CollisionHit");
                var collisionHitPrefix = typeof(PracticeMod).GetMethod(nameof(CollisionHitPrefix), BindingFlags.NonPublic | BindingFlags.Static);
                harmony.Patch(collisionHitMethod, new HarmonyMethod(collisionHitPrefix));

                // Patch GTWPlayerRagdollState.Enter
                var ragdollEnterMethod = typeof(GTWPlayerRagdollState).GetMethod("Enter");
                var ragdollEnterPrefix = typeof(PracticeMod).GetMethod(nameof(RagdollStateEnterPrefix), BindingFlags.NonPublic | BindingFlags.Static);
                harmony.Patch(ragdollEnterMethod, new HarmonyMethod(ragdollEnterPrefix));

            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to apply Harmony patches: {e.Message}");
            }
        }

        private float DrawLabeledSlider(MenuLayout layout, string label, float value, float min, float max, string format = "F1")
        {
            GUI.Label(layout.GetElementRect(ELEMENT_HEIGHT), $"{label}: {value.ToString(format)}");
            return GUI.HorizontalSlider(layout.GetElementRect(ELEMENT_HEIGHT), value, min, max);
        }

        private bool DrawToggleButton(MenuLayout layout, string enableText, string disableText, bool currentState)
        {
            if (GUI.Button(layout.GetElementRect(BUTTON_HEIGHT), currentState ? disableText : enableText))
            {
                return !currentState;
            }
            return currentState;
        }

        private void DrawMovementMenu(MenuLayout layout)
        {
            // Noclip Settings
            GUI.Box(layout.GetRect(30), "NoClip settings");

            moveSpeed = DrawLabeledSlider(layout, "Speed", moveSpeed, 1f, 50f);
            fastMultiplier = DrawLabeledSlider(layout, "Fast Multiplier", fastMultiplier, 1f, 10f);
            isNoclipActive = DrawToggleButton(layout, "Enable Noclip", "Disable Noclip", isNoclipActive);

            layout.AddSpace(PADDING);

            // Time Control Settings
            GUI.Box(layout.GetRect(30), "Time Control Settings");

            timeScaleValue = DrawLabeledSlider(layout, "Time Scale", timeScaleValue, 0.01f, 20f, "F2");
            isTimeScaleActive = DrawToggleButton(layout, "Apply Time Scale", "Reset Time Scale", isTimeScaleActive);

            layout.AddSpace(PADDING);

            GUI.Box(layout.GetRect(30), "Physics Settings");

            // Physics Mod Master Switch
            physicsModEnabled = GUI.Toggle(layout.GetElementRect(ELEMENT_HEIGHT), physicsModEnabled, "Enable Physics Mods");

            // Physics Mod Keybind Info
            GUI.Label(layout.GetElementRect(ELEMENT_HEIGHT), $"Enable Physics Mods Key: {physicsModKey.Value}");
            

            layout.AddSpace(PADDING);

            // Individual toggles and settings

            // Friction Mod
            frictionModEnabled = GUI.Toggle(layout.GetElementRect(ELEMENT_HEIGHT), frictionModEnabled, "Modify Friction");
            if (frictionModEnabled)
            {
                customFriction = DrawLabeledSlider(layout, "Friction", customFriction, 0f, 250f, "F2");
                if (physicsModEnabled)
                    ApplyCustomFriction();
            }

            layout.AddSpace(PADDING);

            // Acceleration Mod
            accelerationModEnabled = GUI.Toggle(layout.GetElementRect(ELEMENT_HEIGHT), accelerationModEnabled, "Modify Acceleration");
            if (accelerationModEnabled)
            {
                customAcceleration = DrawLabeledSlider(layout, "Acceleration", customAcceleration, 0f, 2000f, "F1");
            }

            layout.AddSpace(PADDING);

            // Drag Mod
            dragModEnabled = GUI.Toggle(layout.GetElementRect(ELEMENT_HEIGHT), dragModEnabled, "Modify Drag");
            if (dragModEnabled)
            {
                customDrag = DrawLabeledSlider(layout, "Drag", customDrag, 0f, 100f, "F2");
            }

            layout.AddSpace(PADDING);

            // Constant Speed Mod
            constantSpeedModEnabled = GUI.Toggle(layout.GetElementRect(ELEMENT_HEIGHT), constantSpeedModEnabled, "Maintain Minimum Speed");
            if (constantSpeedModEnabled)
            {
                minSpeed = DrawLabeledSlider(layout, "Min Speed", minSpeed, 0f, 500f, "F1");
            }

            layout.AddSpace(PADDING);

            // Ball Visualizer
            bool previousBallVisualizerEnabled = ballVisualizerEnabled;
            ballVisualizerEnabled = GUI.Toggle(layout.GetElementRect(ELEMENT_HEIGHT), ballVisualizerEnabled, "Show Ball");
            if (ballVisualizerEnabled != previousBallVisualizerEnabled)
            {
                ShowNotification($"Ball Visualizer: {(ballVisualizerEnabled ? "Enabled" : "Disabled")}");
            }
        }

        private void DrawControlsMenu(MenuLayout layout)
        {
            GUI.Box(layout.GetRect(30), "Keybinds");

            if (waitingForKey.HasValue)
            {
                GUI.Label(layout.GetElementRect(30), $"Press any key for: {waitingForKeyBinding}");
                Event e = Event.current;
                if (e.isKey && e.type == EventType.KeyDown)
                {
                    switch (waitingForKeyBinding)
                    {
                        case "Menu":
                            menuKey.Value = e.keyCode;
                            break;
                        case "Noclip":
                            noclipKey.Value = e.keyCode;
                            break;
                        case "Teleport":
                            teleportKey.Value = e.keyCode;
                            break;
                        case "TimeScale":
                            timeScaleKey.Value = e.keyCode;
                            break;
                        case "TempCheckpoint":
                            tempCheckpointKey.Value = e.keyCode;
                            break;
                        case "PhysicsMod":
                            physicsModKey.Value = e.keyCode;
                            break;
                        case "RagdollToggle":
                            ragdollToggleKey.Value = e.keyCode;
                            break;
                    }
                    waitingForKey = null;
                    waitingForKeyBinding = "";
                }
            }
            else
            {
                if (GUI.Button(layout.GetElementRect(BUTTON_HEIGHT), $"Menu Key: {menuKey.Value}"))
                {
                    waitingForKey = menuKey.Value;
                    waitingForKeyBinding = "Menu";
                }
                if (GUI.Button(layout.GetElementRect(BUTTON_HEIGHT), $"Noclip Key: {noclipKey.Value}"))
                {
                    waitingForKey = noclipKey.Value;
                    waitingForKeyBinding = "Noclip";
                }
                if (GUI.Button(layout.GetElementRect(BUTTON_HEIGHT), $"Teleport Key: {teleportKey.Value}"))
                {
                    waitingForKey = teleportKey.Value;
                    waitingForKeyBinding = "Teleport";
                }
                if (GUI.Button(layout.GetElementRect(BUTTON_HEIGHT), $"TimeScale Key: {timeScaleKey.Value}"))
                {
                    waitingForKey = timeScaleKey.Value;
                    waitingForKeyBinding = "TimeScale";
                }
                if (GUI.Button(layout.GetElementRect(BUTTON_HEIGHT), $"Temp Checkpoint Key: {tempCheckpointKey.Value}"))
                {
                    waitingForKey = tempCheckpointKey.Value;
                    waitingForKeyBinding = "TempCheckpoint";
                }
                if (GUI.Button(layout.GetElementRect(BUTTON_HEIGHT), $"Physics Mod Key: {physicsModKey.Value}"))
                {
                    waitingForKey = physicsModKey.Value;
                    waitingForKeyBinding = "PhysicsMod";
                }
                if (GUI.Button(layout.GetElementRect(BUTTON_HEIGHT), $"Ragdoll Toggle Key: {ragdollToggleKey.Value}"))
                {
                    waitingForKey = ragdollToggleKey.Value;
                    waitingForKeyBinding = "RagdollToggle";
                }
            }
        }

        void Update()
        {
            if (playerTransform == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    playerTransform = player.transform;
                    playerRigidbody = player.GetComponent<Rigidbody>();
                    playerCollider = player.GetComponent<Collider>();
                }
                return;
            }

            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }

            if (playerController == null)
            {
                playerController = FindObjectOfType<PlayerController>();
                if (playerController != null && !valuesStored)
                {
                    StoreOriginalValues();
                }
            }

            // Process physics mod adjustments
            if (physicsModEnabled)
            {
                // Apply modifications only if the specific mod is enabled
                if (frictionModEnabled)
                {
                    ApplyCustomFriction();
                }
                if (accelerationModEnabled)
                {
                    // The acceleration is applied in the Harmony patch
                }
                if (dragModEnabled)
                {
                    // The drag is applied in the Harmony patch
                }
            }
            else
            {
                RestoreOriginalValues();
            }

            // Process ball visualizer
            if (ballVisualizerEnabled)
            {
                if (playerController?.DebugBallTransform != null)
                {
                    // Show debug ball
                    playerController.DebugBallTransform.gameObject.SetActive(true);
                    // Hide character model
                    var modelController = playerController.GetComponent<PlayerModelController>();
                    if (modelController?.ModelRoot != null)
                    {
                        modelController.ModelRoot.gameObject.SetActive(false);
                    }
                }
            }
            else
            {
                if (playerController?.DebugBallTransform != null)
                {
                    // Hide debug ball
                    playerController.DebugBallTransform.gameObject.SetActive(false);
                    // Show character model
                    var modelController = playerController.GetComponent<PlayerModelController>();
                    if (modelController?.ModelRoot != null)
                    {
                        modelController.ModelRoot.gameObject.SetActive(true);
                    }
                }
            }

            // Keybinds for toggling physics mod
            if (Input.GetKeyDown(physicsModKey.Value))
            {
                physicsModEnabled = !physicsModEnabled;
                if (!physicsModEnabled)
                {
                    RestoreOriginalValues();
                }
                ShowNotification($"Physics Mod: {(physicsModEnabled ? "Enabled" : "Disabled")}");
            }

            // Keybind for toggling ragdoll
            if (Input.GetKeyDown(ragdollToggleKey.Value))
            {
                ragdollEnabled = !ragdollEnabled;
                ShowNotification($"Ragdoll: {(ragdollEnabled ? "Enabled" : "Disabled")}");
            }

            // Add this back for teleport key functionality
            if (Input.GetKeyDown(teleportKey.Value) && !string.IsNullOrEmpty(selectedCheckpoint))
            {
                if (showingPresets)
                {
                    TeleportToPresetCheckpoint(selectedCheckpoint);
                }
                else
                {
                    TeleportToCustomCheckpoint(selectedCheckpoint);
                }
            }

            if (Input.GetKeyDown(menuKey.Value))
            {
                showMenu = !showMenu;
                if (showMenu)
                {
                    Time.timeScale = isTimeScaleActive ? timeScaleValue : normalTimeScale;
                    Cursor.visible = true;
                    Cursor.lockState = CursorLockMode.Confined;
                }
                else
                {
                    Cursor.visible = false;
                    Cursor.lockState = CursorLockMode.Locked;
                }
            }
            if (Input.GetKeyDown(tempCheckpointKey.Value))
            {
                SetTemporaryCheckpoint();
            }
            if (Input.GetKeyDown(timeScaleKey.Value))
            {
                ToggleTimeScale();
            }

            if (Input.GetKeyDown(noclipKey.Value))
            {
                ToggleNoclip();
            }

            if (isNoclipActive)
            {
                UpdateNoclip();
            }
        }

        private void SetTemporaryCheckpoint()
        {
            if (playerTransform != null && mainCamera != null)
            {
                Vector3 currentVelocity = playerRigidbody != null ? playerRigidbody.velocity : Vector3.zero;
                temporaryCheckpoint = new CheckpointData(
                    playerTransform.position,
                    currentVelocity,
                    playerTransform.rotation,
                    mainCamera.transform.rotation
                );
                selectedCheckpoint = TEMP_CHECKPOINT_NAME;
                showingPresets = false;
                ShowNotification("Temporary checkpoint set!");
            }
        }

        private void ToggleTimeScale()
        {
            isTimeScaleActive = !isTimeScaleActive;
            Time.timeScale = isTimeScaleActive ? timeScaleValue : normalTimeScale;
            Time.fixedDeltaTime = 0.02f * Time.timeScale;
            ShowNotification(isTimeScaleActive ? $"Time Scale: {timeScaleValue}x" : "Normal Time Scale");
        }
        private void DrawCheckpointsMenu(MenuLayout layout)
        {
            // Checkpoint Settings Box
            GUI.Box(layout.GetRect(0), "");
            keepNoclipAfterTeleport = GUI.Toggle(
                layout.GetElementRect(20),
                keepNoclipAfterTeleport,
                "Freeze After Teleporting"
            );

            // Toggle between preset and custom checkpoints
            if (GUI.Button(layout.GetRect(30),
                showingPresets ? "Show Custom Checkpoints" : "Show Preset Locations"))
            {
                showingPresets = !showingPresets;
                selectedCheckpoint = "";
                scrollPosition = Vector2.zero;
            }

            layout.AddSpace(PADDING);

            if (showingPresets)
            {
                // Preset Checkpoints
                GUI.Box(layout.GetRect(280), "Preset Locations");
                Rect viewRect = layout.GetElementRect(250);
                Rect contentRect = new Rect(0, 0, viewRect.width - 20, presetCheckpoints.Count * 25);

                scrollPosition = GUI.BeginScrollView(viewRect, scrollPosition, contentRect);

                float currentY = 0;
                foreach (var checkpoint in presetCheckpoints)
                {
                    if (GUI.Button(new Rect(0, currentY, contentRect.width, 23), checkpoint.Key))
                    {
                        selectedCheckpoint = checkpoint.Key;
                        if (Input.GetKey(KeyCode.LeftControl))
                        {
                            TeleportToPresetCheckpoint(selectedCheckpoint);
                        }
                    }
                    currentY += 25;
                }

                GUI.EndScrollView();
            }
            else
            {
                // Custom Checkpoints
                GUI.Box(layout.GetRect(30), "Custom Checkpoints");

                // New checkpoint creation
                GUI.Label(layout.GetElementRect(20), "New Checkpoint:");
                newCheckpointName = GUI.TextField(layout.GetElementRect(20), newCheckpointName);

                if (GUI.Button(layout.GetElementRect(25), "Create Checkpoint Here"))
                {
                    CreateNewCheckpoint();
                }

                layout.AddSpace(PADDING);

                // Saved checkpoints list
                GUI.Label(layout.GetElementRect(20), "Saved Checkpoints:");

                Rect viewRect = layout.GetElementRect(150);
                Rect contentRect = new Rect(0, 0, viewRect.width - 20, customCheckpoints.Count * 25);

                scrollPosition = GUI.BeginScrollView(viewRect, scrollPosition, contentRect);

                float currentY = 0;
                List<string> checkpointsToRemove = new List<string>();

                foreach (var checkpoint in customCheckpoints)
                {
                    Rect buttonRect = new Rect(0, currentY, contentRect.width - 30, 23);
                    if (GUI.Button(buttonRect, checkpoint.Key))
                    {
                        selectedCheckpoint = checkpoint.Key;
                        if (selectedCheckpoint != TEMP_CHECKPOINT_NAME)
                        {
                            temporaryCheckpoint = null;
                        }
                        if (Input.GetKey(KeyCode.LeftControl))
                        {
                            TeleportToCustomCheckpoint(selectedCheckpoint);
                        }
                    }

                    Rect deleteRect = new Rect(contentRect.width - 25, currentY, 25, 23);
                    if (GUI.Button(deleteRect, "X"))
                    {
                        checkpointsToRemove.Add(checkpoint.Key);
                    }
                    currentY += 25;
                }

                GUI.EndScrollView();

                // Process checkpoint deletions
                foreach (string checkpointName in checkpointsToRemove)
                {
                    DeleteCheckpoint(checkpointName);
                }
            }

            if (!string.IsNullOrEmpty(selectedCheckpoint))
            {
                DrawSelectedCheckpoint(layout);
            }

            // Help text
            GUI.Box(layout.GetRect(0), "");
            GUI.Label(layout.GetElementRect(40),
                $"Ctrl + Click to quick teleport\nPress {teleportKey.Value} to teleport to selected checkpoint");
        }
        private void OnGUI()
        {
            GUI.depth = 0;

            if (Time.unscaledTime < notificationTime)
            {
                GUI.Label(new Rect(Screen.width / 2 - 100, 20, 200, 30), notificationText);
            }

            GUI.Label(new Rect(Screen.width - 120, Screen.height - 25, 100, 20), "Practice Mod");

            if (!showMenu) return;

            float menuX = Screen.width - MENU_WIDTH - 10;

            // Main background box
            GUI.Box(new Rect(menuX, 10, MENU_WIDTH, 1200), "Practice Menu");

            // Category buttons - positioned inside the main box
            float buttonWidth = (MENU_WIDTH - 40) / 3;
            float buttonY = 40;

            if (GUI.Button(new Rect(menuX + 20, buttonY, buttonWidth, 30), "Movement"))
            {
                currentCategory = currentCategory == MenuCategory.Movement ? MenuCategory.None : MenuCategory.Movement;
            }
            if (GUI.Button(new Rect(menuX + buttonWidth + 30, buttonY, buttonWidth, 30), "Checkpoints"))
            {
                currentCategory = currentCategory == MenuCategory.Checkpoints ? MenuCategory.None : MenuCategory.Checkpoints;
            }
            if (GUI.Button(new Rect(menuX + (buttonWidth * 2) + 40, buttonY, buttonWidth, 30), "Controls"))
            {
                currentCategory = currentCategory == MenuCategory.Controls ? MenuCategory.None : MenuCategory.Controls;
            }

            MenuLayout layout = new MenuLayout(menuX, buttonY + 40, MENU_WIDTH);

            // Draw category content
            switch (currentCategory)
            {
                case MenuCategory.Movement:
                    DrawMovementMenu(layout);
                    break;
                case MenuCategory.Controls:
                    DrawControlsMenu(layout);
                    break;
                case MenuCategory.Checkpoints:
                    DrawCheckpointsMenu(layout);
                    break;
            }

            // Process key bindings if waiting for a key
            if (waitingForKey.HasValue)
            {
                Event e = Event.current;
                if (e.isKey && e.type == EventType.KeyDown)
                {
                    switch (waitingForKeyBinding)
                    {
                        case "Menu":
                            menuKey.Value = e.keyCode;
                            break;
                        case "Noclip":
                            noclipKey.Value = e.keyCode;
                            break;
                        case "Teleport":
                            teleportKey.Value = e.keyCode;
                            break;
                        case "TimeScale":
                            timeScaleKey.Value = e.keyCode;
                            break;
                        case "TempCheckpoint":
                            tempCheckpointKey.Value = e.keyCode;
                            break;
                        case "PhysicsMod":
                            physicsModKey.Value = e.keyCode;
                            break;
                        case "RagdollToggle":
                            ragdollToggleKey.Value = e.keyCode;
                            break;
                    }
                    waitingForKey = null;
                    waitingForKeyBinding = "";
                    ShowNotification($"Key bound successfully!");
                }
            }
        }

        private void ShowNotification(string text)
        {
            notificationText = text;
            notificationTime = Time.unscaledTime + 2f;
        }

        private void CreateNewCheckpoint()
        {
            if (!string.IsNullOrEmpty(newCheckpointName))
            {
                if (playerTransform != null && mainCamera != null)
                {
                    Vector3 currentVelocity = playerRigidbody != null ? playerRigidbody.velocity : Vector3.zero;
                    customCheckpoints[newCheckpointName] = new CheckpointData(
                        playerTransform.position,
                        currentVelocity,
                        playerTransform.rotation,
                        mainCamera.transform.rotation
                    );
                    selectedCheckpoint = newCheckpointName;
                    newCheckpointName = "";
                    SaveCheckpoints();
                    ShowNotification("Checkpoint created!");
                }
            }
            else
            {
                ShowNotification("Please enter a checkpoint name!");
            }
        }

        private void DeleteCheckpoint(string checkpointName)
        {
            customCheckpoints.Remove(checkpointName);
            if (selectedCheckpoint == checkpointName)
                selectedCheckpoint = "";
            SaveCheckpoints();
            ShowNotification($"Deleted checkpoint: {checkpointName}");
        }

        private void DrawSelectedCheckpoint(MenuLayout layout)
        {
            GUI.Box(layout.GetRect(50), "Selected Checkpoint");
            GUI.Label(layout.GetElementRect(20), $"Selected: {selectedCheckpoint}");

            if (GUI.Button(layout.GetElementRect(25), "Teleport"))
            {
                if (showingPresets)
                    TeleportToPresetCheckpoint(selectedCheckpoint);
                else
                    TeleportToCustomCheckpoint(selectedCheckpoint);
            }
        }

        private void TeleportToPresetCheckpoint(string checkpointName)
        {
            if (presetCheckpoints.TryGetValue(checkpointName, out CheckpointData data))
            {
                TeleportToCheckpoint(data);
            }
        }

        private void TeleportToCustomCheckpoint(string checkpointName)
        {
            if (checkpointName == TEMP_CHECKPOINT_NAME && temporaryCheckpoint != null)
            {
                TeleportToCheckpoint(temporaryCheckpoint);
                return;
            }

            if (customCheckpoints.TryGetValue(checkpointName, out CheckpointData data))
            {
                TeleportToCheckpoint(data);
            }
        }

        private void TeleportToCheckpoint(CheckpointData data)
        {
            playerTransform.position = data.Position;
            playerTransform.rotation = data.PlayerRotation;
            if (mainCamera != null)
            {
                mainCamera.transform.rotation = data.CameraRotation;
            }
            if (playerRigidbody != null)
            {
                playerRigidbody.velocity = data.Velocity;
            }
            ShowNotification($"Teleported to checkpoint");

            if (keepNoclipAfterTeleport)
            {
                EnableNoclip();
                waitingForMovement = true;
            }

            // Disable ragdoll after teleporting
            if (!ragdollEnabled)
            {
                var playerRagdoll = playerTransform.GetComponent<PlayerRagdoll>();
                if (playerRagdoll != null)
                {
                    playerRagdoll.SetRagdollActive(false, false);
                }
                var playerController = playerTransform.GetComponent<PlayerController>();
                if (playerController != null)
                {
                    playerController.ChangeToMoveState();
                }
            }
        }

        private void DisableNoclip()
        {
            isNoclipActive = false;
            if (playerRigidbody != null)
            {
                // Set isKinematic back to false
                playerRigidbody.isKinematic = false;
                // Do not change useGravity or reset velocities
            }
            if (playerCollider != null)
            {
                playerCollider.enabled = true;
            }
            waitingForMovement = false;
            ShowNotification("Noclip Disabled");
        }

        private void EnableNoclip()
        {
            isNoclipActive = true;
            if (playerRigidbody != null)
            {
                // Only set isKinematic to true
                playerRigidbody.isKinematic = true;
                // Do not change useGravity or reset velocities
            }
            if (playerCollider != null)
            {
                playerCollider.enabled = false;
            }
            ShowNotification("Noclip Enabled");
        }

        private void ToggleNoclip()
        {
            // Get the PlayerController and PlayerRagdoll components
            var playerController = playerTransform.GetComponent<Isto.GTW.Player.PlayerController>();
            var playerRagdoll = playerTransform.GetComponent<Isto.GTW.Player.PlayerRagdoll>();

            if (isNoclipActive)
            {
                DisableNoclip();
                // Wait one frame to ensure physics are properly reset
                StartCoroutine(ResetPhysicsNextFrame());
            }
            else
            {
                // Check if player is in ragdoll state
                if (playerController != null && playerController.IsRagdollState)
                {
                    // Disable ragdoll before enabling noclip
                    if (playerRagdoll != null)
                    {
                        playerRagdoll.SetRagdollActive(false, false);
                    }
                    // Change to move state
                    playerController.ChangeToMoveState();
                    // Wait a frame to ensure ragdoll is fully disabled
                    StartCoroutine(EnableNoclipNextFrame());
                }
                else
                {
                    EnableNoclip();
                }
            }
        }
        private IEnumerator EnableNoclipNextFrame()
        {
            yield return new WaitForFixedUpdate();
            EnableNoclip();
        }
        private IEnumerator ResetPhysicsNextFrame()
        {
            yield return new WaitForFixedUpdate();
            var playerController = playerTransform.GetComponent<Isto.GTW.Player.PlayerController>();
            if (playerController != null)
            {
                // Only reset velocity and change state if not in ragdoll
                if (!playerController.IsRagdollState)
                {
                    playerController.ResetVelocityToZero();
                    playerController.ChangeToMoveState();
                }
            }
        }

        private void UpdateNoclip()
        {
            if (playerTransform == null || mainCamera == null) return;

            float currentSpeed = Input.GetKey(KeyCode.LeftShift) ? moveSpeed * fastMultiplier : moveSpeed;

            Vector3 movement = Vector3.zero;
            movement += mainCamera.transform.forward * Input.GetAxis("Vertical");
            movement += mainCamera.transform.right * Input.GetAxis("Horizontal");

            if (Input.GetKey(KeyCode.Space)) movement.y += 1f;
            if (Input.GetKey(KeyCode.LeftControl)) movement.y -= 1f;

            playerTransform.position += movement * currentSpeed * Time.unscaledDeltaTime;

            if (waitingForMovement && (movement.magnitude > 0.1f))
            {
                DisableNoclip();
            }
        }

        private void SaveCheckpoints()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(saveFilePath))
                {
                    foreach (var checkpoint in customCheckpoints)
                    {
                        writer.WriteLine($"{checkpoint.Key}," +
                                       $"{checkpoint.Value.Position.x},{checkpoint.Value.Position.y},{checkpoint.Value.Position.z}," +
                                       $"{checkpoint.Value.Velocity.x},{checkpoint.Value.Velocity.y},{checkpoint.Value.Velocity.z}," +
                                       $"{checkpoint.Value.PlayerRotation.x},{checkpoint.Value.PlayerRotation.y},{checkpoint.Value.PlayerRotation.z},{checkpoint.Value.PlayerRotation.w}," +
                                       $"{checkpoint.Value.CameraRotation.x},{checkpoint.Value.CameraRotation.y},{checkpoint.Value.CameraRotation.z},{checkpoint.Value.CameraRotation.w}");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to save checkpoints: {e.Message}");
                ShowNotification("Failed to save checkpoints!");
            }
        }

        private void LoadCheckpoints()
        {
            try
            {
                if (File.Exists(saveFilePath))
                {
                    customCheckpoints.Clear();
                    string[] lines = File.ReadAllLines(saveFilePath);
                    foreach (string line in lines)
                    {
                        string[] parts = line.Split(',');
                        if (parts.Length == 15)
                        {
                            string name = parts[0];
                            Vector3 position = new Vector3(
                                float.Parse(parts[1]),
                                float.Parse(parts[2]),
                                float.Parse(parts[3])
                            );
                            Vector3 velocity = new Vector3(
                                float.Parse(parts[4]),
                                float.Parse(parts[5]),
                                float.Parse(parts[6])
                            );
                            Quaternion playerRotation = new Quaternion(
                                float.Parse(parts[7]),
                                float.Parse(parts[8]),
                                float.Parse(parts[9]),
                                float.Parse(parts[10])
                            );
                            Quaternion cameraRotation = new Quaternion(
                                float.Parse(parts[11]),
                                float.Parse(parts[12]),
                                float.Parse(parts[13]),
                                float.Parse(parts[14])
                            );
                            customCheckpoints[name] = new CheckpointData(position, velocity, playerRotation, cameraRotation);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load checkpoints: {e.Message}");
                ShowNotification("Failed to load checkpoints!");
            }
        }

        void OnDisable()
        {
            // Cleanup when mod is disabled
            if (isNoclipActive)
            {
                DisableNoclip();
            }

            if (isTimeScaleActive)
            {
                Time.timeScale = normalTimeScale;
                Time.fixedDeltaTime = 0.02f * normalTimeScale;
            }

            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;

            // Save checkpoints before disabling
            SaveCheckpoints();

            // Restore original physics values
            RestoreOriginalValues();

            // Reset ball visualizer
            if (playerController?.DebugBallTransform != null)
            {
                playerController.DebugBallTransform.gameObject.SetActive(false);
                var modelController = playerController.GetComponent<PlayerModelController>();
                if (modelController?.ModelRoot != null)
                {
                    modelController.ModelRoot.gameObject.SetActive(true);
                }
            }

            // Unpatch Harmony patches
            harmony.UnpatchSelf();
        }

        // Ball Physics Methods

        private void StoreOriginalValues()
        {
            if (playerController != null && !valuesStored)
            {
                // Store original friction values
                if (playerController.MainBallCollider?.material != null)
                {
                    originalFriction = playerController.MainBallCollider.material.dynamicFriction;
                    customFriction = originalFriction;
                }
                // Store original torque physics values
                torquePhysics = playerController.TorquePhysics;
                if (torquePhysics != null)
                {
                    var torqueField = typeof(TorquePhysics).GetField("torque", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (torqueField != null)
                    {
                        originalAcceleration = (float)torqueField.GetValue(torquePhysics);
                        customAcceleration = originalAcceleration;
                    }
                    var dragField = typeof(TorquePhysics).GetField("drag", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (dragField != null)
                    {
                        originalDrag = (float)dragField.GetValue(torquePhysics);
                        customDrag = originalDrag;
                    }
                }
                valuesStored = true;
                Debug.Log("Original values stored!");
                Debug.Log($"Original Friction: {originalFriction}");
                Debug.Log($"Original Acceleration: {originalAcceleration}");
                Debug.Log($"Original Drag: {originalDrag}");
            }
        }

        private void RestoreOriginalValues()
        {
            if (playerController?.MainBallCollider?.material != null)
            {
                playerController.MainBallCollider.material.dynamicFriction = originalFriction;
                playerController.MainBallCollider.material.staticFriction = originalFriction;
            }
            if (torquePhysics != null)
            {
                var torqueField = typeof(TorquePhysics).GetField("torque", BindingFlags.NonPublic | BindingFlags.Instance);
                if (torqueField != null)
                {
                    torqueField.SetValue(torquePhysics, originalAcceleration);
                }
                var dragField = typeof(TorquePhysics).GetField("drag", BindingFlags.NonPublic | BindingFlags.Instance);
                if (dragField != null)
                {
                    dragField.SetValue(torquePhysics, originalDrag);
                }
            }
        }

        private void ApplyCustomFriction()
        {
            if (playerController?.MainBallCollider?.material != null)
            {
                playerController.MainBallCollider.material.dynamicFriction = customFriction;
                playerController.MainBallCollider.material.staticFriction = customFriction;
            }
        }

        private static bool UpdateTorquePrefix(TorquePhysics __instance)
        {
            if (!Instance.physicsModEnabled) return true;
            if (!Instance.accelerationModEnabled && !Instance.dragModEnabled) return true;
            var rigidbody = __instance.Rigidbody;
            var playerController = __instance.PlayerController;
            Vector3 movementInput = playerController.MovementInput;
            if (Instance.accelerationModEnabled)
            {
                Vector3 inputTorque = Vector3.Cross(movementInput, Vector3.down) * Instance.customAcceleration;
                rigidbody.AddTorque(inputTorque, ForceMode.Acceleration);
            }
            if (Instance.dragModEnabled)
            {
                rigidbody.drag = Instance.customDrag;
            }
            return false; // Skip original method
        }

        private static void UpdateDirectionalVectorsAndSpeedPostfix(PlayerController __instance)
        {
            if (!Instance.physicsModEnabled || !Instance.constantSpeedModEnabled) return;
            Vector3 currentVelocity = __instance.Rigidbody.velocity;
            Vector3 horizontalVelocity = new Vector3(currentVelocity.x, 0, currentVelocity.z);
            float verticalVelocity = currentVelocity.y;
            if (horizontalVelocity.magnitude > 0.01f)
            {
                horizontalVelocity = horizontalVelocity.normalized * Instance.minSpeed;
            }
            __instance.Rigidbody.velocity = new Vector3(horizontalVelocity.x, verticalVelocity, horizontalVelocity.z);
        }

        // Ragdoll Methods

        private static bool CollisionHitPrefix(PlayerRagdoll __instance, Vector3 impulse)
        {
            return Instance.ragdollEnabled; // Return true to allow ragdoll, false to prevent it
        }

        private static bool RagdollStateEnterPrefix(GTWPlayerRagdollState __instance, ScriptableStateMachine controller)
        {
            return Instance.ragdollEnabled; // Return true to allow ragdoll state, false to prevent it
        }
    }
}
