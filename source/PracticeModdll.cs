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
    [BepInPlugin("com.Palmblom.SpeedrunPracticeMod", "SpeedrunPracticeMod", "1.5.0")]
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
            public float CurrentY { get; set; }
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
        private ConfigEntry<KeyCode> controllerMenuKey;
        private ConfigEntry<KeyCode> controllerNoclipKey;
        private ConfigEntry<KeyCode> controllerTeleportKey;
        private ConfigEntry<KeyCode> controllerTimeScaleKey;
        private ConfigEntry<KeyCode> controllerTempCheckpointKey;
        private ConfigEntry<KeyCode> controllerPhysicsModKey;
        private bool waitingForKeyPress = false;
        private string currentBindingAction = "";
        private Dictionary<string, List<ConfigEntry<KeyCode>>> actionBindings = new Dictionary<string, List<ConfigEntry<KeyCode>>>();

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

        // Add these variables with the other toggle flags
        private bool disableRagdollEnabled = false;
        private bool originalRagdollState = false;

        // Add these variables at the top of the class with other physics variables
        private float customGravity = 9.8f;
        private float customBounciness = 0.5f;
        private float customBallRadius = 0.25f;
        private bool gravityModEnabled = false;
        private bool bouncinessModEnabled = false;
        private bool ballRadiusModEnabled = false;

        // Add these variables at the top of the class with other physics variables
        private float customMaxGravityAngle = 25f;
        private float customMaxTorque = 60f;
        private float customGrabAbilityDrag = 5f;
        private float customGroundedBounciness = 0.3f;
        private float customAirBounciness = 0.5f;
        private bool maxGravityAngleModEnabled = false;
        private bool maxTorqueModEnabled = false;
        private bool grabAbilityDragModEnabled = false;
        private bool airBouncinessModEnabled = false;

        public static PracticeMod Instance;

        private Harmony harmony;

        // Add these field declarations
        private Texture2D backgroundTexture;
        private Texture2D buttonTexture;
        private Texture2D buttonHoverTexture;
        private Texture2D textFieldTexture;
        private Texture2D headerTexture;
        private Texture2D selectedTexture;
        private Texture2D alternateRowTexture;
        private Texture2D toggleOnTexture;
        private Texture2D toggleOffTexture;
        private Texture2D separatorTexture;
        private Texture2D textFieldBorderTexture;
        private Texture2D deleteButtonTexture;
        private Texture2D deleteButtonHoverTexture;
        private List<UIElement> uiElements = new List<UIElement>();
        private float navigationCooldown = 0f;
        private const float NAVIGATION_COOLDOWN_TIME = 0.2f;
        private Dictionary<string, bool> hoveredElements = new Dictionary<string, bool>();

        // Add UI element class
        public enum UIElementType
        {
            Button,
            Toggle,
            Slider,
            TextField,
            Label
        }

        public class UIElement
        {
            public UIElementType type;
            public Rect rect;
            public System.Action action;
            public System.Action<bool> toggleAction;
            public System.Action<float> sliderAction;
            public string text;
            public bool isToggled;
            public float sliderValue;
            public float sliderMin;
            public float sliderMax;

            public UIElement(UIElementType type, Rect rect, string text = "")
            {
                this.type = type;
                this.rect = rect;
                this.text = text;
            }
        }

        // Add this near the top with other private variables
        private bool uiOnRightSide = true; // Default to right side

        private bool selectionActive = false;

        // Add original values for all physics properties at class level
        private float originalGravity;
        private float originalMaxGravityAngle;
        private float originalMaxTorque;
        private float originalGrabAbilityDrag;
        private float originalBallRadius;
        private float originalGroundedBounciness;
        private float originalAirBounciness;

        private void Awake()
        {
            Instance = this;

            // Keyboard config setup
            menuKey = Config.Bind("Controls", "MenuKey", KeyCode.Tab, "Key to toggle the practice menu");
            noclipKey = Config.Bind("Controls", "NoclipKey", KeyCode.N, "Key to toggle noclip mode");
            teleportKey = Config.Bind("Controls", "TeleportKey", KeyCode.O, "Key to teleport to selected checkpoint");
            timeScaleKey = Config.Bind("Controls", "TimeScaleKey", KeyCode.P, "Key to toggle time scale");
            tempCheckpointKey = Config.Bind("Controls", "TempCheckpointKey", KeyCode.C, "Key to set temporary checkpoint");
            physicsModKey = Config.Bind("Controls", "PhysicsModKey", KeyCode.K, "Key to toggle physics mod");

            // Controller config setup - these will be empty by default
            controllerMenuKey = Config.Bind("Controls", "ControllerMenuKey", KeyCode.None, "Controller button to toggle the practice menu");
            controllerNoclipKey = Config.Bind("Controls", "ControllerNoclipKey", KeyCode.None, "Controller button to toggle noclip mode");
            controllerTeleportKey = Config.Bind("Controls", "ControllerTeleportKey", KeyCode.None, "Controller button to teleport to selected checkpoint");
            controllerTimeScaleKey = Config.Bind("Controls", "ControllerTimeScaleKey", KeyCode.None, "Controller button to toggle time scale");
            controllerTempCheckpointKey = Config.Bind("Controls", "ControllerTempCheckpointKey", KeyCode.None, "Controller button to set temporary checkpoint");
            controllerPhysicsModKey = Config.Bind("Controls", "ControllerPhysicsModKey", KeyCode.None, "Controller button to toggle physics mod");

            // Set up action bindings dictionary
            SetupActionBindings();

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

            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to apply Harmony patches: {e.Message}");
            }
        }

        private void SetupActionBindings()
        {
            actionBindings.Clear();
            actionBindings["Menu"] = new List<ConfigEntry<KeyCode>> { menuKey, controllerMenuKey };
            actionBindings["Noclip"] = new List<ConfigEntry<KeyCode>> { noclipKey, controllerNoclipKey };
            actionBindings["Teleport"] = new List<ConfigEntry<KeyCode>> { teleportKey, controllerTeleportKey };
            actionBindings["TimeScale"] = new List<ConfigEntry<KeyCode>> { timeScaleKey, controllerTimeScaleKey };
            actionBindings["TempCheckpoint"] = new List<ConfigEntry<KeyCode>> { tempCheckpointKey, controllerTempCheckpointKey };
            actionBindings["PhysicsMod"] = new List<ConfigEntry<KeyCode>> { physicsModKey, controllerPhysicsModKey };
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
            // Create styles with textures
            GUIStyle headerStyle = new GUIStyle(GUI.skin.box);
            headerStyle.normal.background = headerTexture;
            headerStyle.fontSize = 14;
            headerStyle.fontStyle = FontStyle.Bold;
            headerStyle.normal.textColor = Color.white;
            headerStyle.alignment = TextAnchor.MiddleLeft;
            headerStyle.padding = new RectOffset(10, 10, 5, 5);

            GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = 12;
            labelStyle.normal.textColor = Color.white;

            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.normal.background = buttonTexture;
            buttonStyle.hover.background = buttonHoverTexture;
            buttonStyle.normal.textColor = Color.white;
            buttonStyle.fontSize = 12;
            buttonStyle.alignment = TextAnchor.MiddleCenter;

            GUIStyle textFieldStyle = new GUIStyle(GUI.skin.textField);
            textFieldStyle.normal.background = textFieldTexture;
            textFieldStyle.normal.textColor = Color.white;
            textFieldStyle.alignment = TextAnchor.MiddleLeft;
            textFieldStyle.padding = new RectOffset(5, 5, 3, 3);

            GUIStyle toggleOnStyle = new GUIStyle(GUI.skin.button);
            toggleOnStyle.normal.background = toggleOnTexture;
            toggleOnStyle.hover.background = toggleOnTexture;
            toggleOnStyle.normal.textColor = Color.white;
            toggleOnStyle.fontSize = 12;
            toggleOnStyle.alignment = TextAnchor.MiddleCenter;

            GUIStyle toggleOffStyle = new GUIStyle(GUI.skin.button);
            toggleOffStyle.normal.background = toggleOffTexture;
            toggleOffStyle.hover.background = toggleOffTexture;
            toggleOffStyle.normal.textColor = Color.white;
            toggleOffStyle.fontSize = 12;
            toggleOffStyle.alignment = TextAnchor.MiddleCenter;

            // No Clip Settings
            GUI.Box(layout.GetRect(30), "NoClip Settings", headerStyle);
            GUI.Box(layout.GetElementRect(1), "", new GUIStyle { normal = { background = separatorTexture } });
            layout.AddSpace(5);

            // NoClip Toggle and Speed
            GUI.Label(new Rect(layout.MenuX + MARGIN + PADDING, layout.CurrentY, layout.Width - 120, ELEMENT_HEIGHT), "NoClip:", labelStyle);
            Rect noclipToggleRect = new Rect(layout.MenuX + layout.Width - 80, layout.CurrentY, 50, 24);
            if (GUI.Button(noclipToggleRect, "", isNoclipActive ? toggleOnStyle : toggleOffStyle))
            {
                ToggleNoclip();
            }
            uiElements.Add(new UIElement(UIElementType.Toggle, noclipToggleRect, "NoClip")
            {
                action = () => ToggleNoclip(),
                isToggled = isNoclipActive
            });
            layout.CurrentY += 30;

            // Keep NoClip after teleport
            GUI.Label(new Rect(layout.MenuX + MARGIN + PADDING, layout.CurrentY, layout.Width - 120, ELEMENT_HEIGHT), "NoClip after teleport:", labelStyle);
            Rect keepNoclipToggleRect = new Rect(layout.MenuX + layout.Width - 80, layout.CurrentY, 50, 24);
            if (GUI.Button(keepNoclipToggleRect, "", keepNoclipAfterTeleport ? toggleOnStyle : toggleOffStyle))
            {
                keepNoclipAfterTeleport = !keepNoclipAfterTeleport;
            }
            uiElements.Add(new UIElement(UIElementType.Toggle, keepNoclipToggleRect, "Keep NoClip")
            {
                action = () => keepNoclipAfterTeleport = !keepNoclipAfterTeleport,
                isToggled = keepNoclipAfterTeleport
            });
            layout.CurrentY += 30;

            // NoClip Normal Speed
            GUI.Label(new Rect(layout.MenuX + MARGIN + PADDING, layout.CurrentY, layout.Width - 120, ELEMENT_HEIGHT), "Normal Speed:", labelStyle);
            layout.CurrentY += 25;
            Rect normalSpeedRect = layout.GetElementRect(24);
            moveSpeed = GUI.HorizontalSlider(normalSpeedRect, moveSpeed, 1f, 30f);
            GUI.Label(new Rect(normalSpeedRect.x + normalSpeedRect.width - 40, normalSpeedRect.y, 40, 24), moveSpeed.ToString("F1"), labelStyle);

            uiElements.Add(new UIElement(UIElementType.Slider, normalSpeedRect)
            {
                sliderValue = moveSpeed,
                sliderMin = 1f,
                sliderMax = 30f,
                sliderAction = (value) => moveSpeed = value
            });
            layout.AddSpace(5);

            // Fast Speed Multiplier
            GUI.Label(new Rect(layout.MenuX + MARGIN + PADDING, layout.CurrentY, layout.Width - 120, ELEMENT_HEIGHT), "Fast Speed Multiplier:", labelStyle);
            layout.CurrentY += 25;
            Rect fastMultiplierRect = layout.GetElementRect(24);
            fastMultiplier = GUI.HorizontalSlider(fastMultiplierRect, fastMultiplier, 1f, 5f);
            GUI.Label(new Rect(fastMultiplierRect.x + fastMultiplierRect.width - 40, fastMultiplierRect.y, 40, 24), fastMultiplier.ToString("F1"), labelStyle);

            uiElements.Add(new UIElement(UIElementType.Slider, fastMultiplierRect)
            {
                sliderValue = fastMultiplier,
                sliderMin = 1f,
                sliderMax = 5f,
                sliderAction = (value) => fastMultiplier = value
            });
            layout.AddSpace(10);

            // Time Scale
            GUI.Box(layout.GetRect(30), "Time Scale", headerStyle);
            GUI.Box(layout.GetElementRect(1), "", new GUIStyle { normal = { background = separatorTexture } });
            layout.AddSpace(5);

            // Time Scale Toggle
            GUI.Label(new Rect(layout.MenuX + MARGIN + PADDING, layout.CurrentY, layout.Width - 120, ELEMENT_HEIGHT), "Slow Motion:", labelStyle);
            Rect timeScaleToggleRect = new Rect(layout.MenuX + layout.Width - 80, layout.CurrentY, 50, 24);
            if (GUI.Button(timeScaleToggleRect, "", isTimeScaleActive ? toggleOnStyle : toggleOffStyle))
            {
                ToggleTimeScale();
            }
            uiElements.Add(new UIElement(UIElementType.Toggle, timeScaleToggleRect, "Slow Motion")
            {
                action = () => ToggleTimeScale(),
                isToggled = isTimeScaleActive
            });
            layout.CurrentY += 30;

            // Time Scale Value
            GUI.Label(new Rect(layout.MenuX + MARGIN + PADDING, layout.CurrentY, layout.Width - 120, ELEMENT_HEIGHT), "Time Scale Value:", labelStyle);
            layout.CurrentY += 25;
            Rect timeScaleRect = layout.GetElementRect(24);
            timeScaleValue = GUI.HorizontalSlider(timeScaleRect, timeScaleValue, 0.1f, 1f);
            GUI.Label(new Rect(timeScaleRect.x + timeScaleRect.width - 40, timeScaleRect.y, 40, 24), timeScaleValue.ToString("F2"), labelStyle);

            uiElements.Add(new UIElement(UIElementType.Slider, timeScaleRect)
            {
                sliderValue = timeScaleValue,
                sliderMin = 0.1f,
                sliderMax = 1f,
                sliderAction = (value) => timeScaleValue = value
            });
            layout.AddSpace(10);

            // Ragdoll Control
            GUI.Box(layout.GetRect(30), "Ragdoll Control", headerStyle);
            GUI.Box(layout.GetElementRect(1), "", new GUIStyle { normal = { background = separatorTexture } });
            layout.AddSpace(5);

            // Disable Ragdoll
            GUI.Label(new Rect(layout.MenuX + MARGIN + PADDING, layout.CurrentY, layout.Width - 120, ELEMENT_HEIGHT), "Disable Ragdoll:", labelStyle);
            Rect disableRagdollToggleRect = new Rect(layout.MenuX + layout.Width - 80, layout.CurrentY, 50, 24);
            if (GUI.Button(disableRagdollToggleRect, "", disableRagdollEnabled ? toggleOnStyle : toggleOffStyle))
            {
                disableRagdollEnabled = !disableRagdollEnabled;
                if (disableRagdollEnabled && playerController != null && playerController.IsRagdollState)
                {
                    ForceExitRagdoll();
                }
            }
            uiElements.Add(new UIElement(UIElementType.Toggle, disableRagdollToggleRect, "Disable Ragdoll")
            {
                action = () => {
                    disableRagdollEnabled = !disableRagdollEnabled;
                    if (disableRagdollEnabled && playerController != null && playerController.IsRagdollState)
                    {
                        ForceExitRagdoll();
                    }
                },
                isToggled = disableRagdollEnabled
            });
            layout.AddSpace(25); // Increased space here

            // Physics Mod
            GUI.Box(layout.GetRect(30), "Physics Modifications", headerStyle);
            GUI.Box(layout.GetElementRect(1), "", new GUIStyle { normal = { background = separatorTexture } });
            layout.AddSpace(5);

            // Global Physics Toggle
            GUI.Label(new Rect(layout.MenuX + MARGIN + PADDING, layout.CurrentY, layout.Width - 120, ELEMENT_HEIGHT), "Physics Modifications:", labelStyle);
            Rect physicsModToggleRect = new Rect(layout.MenuX + layout.Width - 80, layout.CurrentY, 50, 24);
            if (GUI.Button(physicsModToggleRect, "", physicsModEnabled ? toggleOnStyle : toggleOffStyle))
            {
                physicsModEnabled = !physicsModEnabled;
            }
            uiElements.Add(new UIElement(UIElementType.Toggle, physicsModToggleRect, "Physics Modifications")
            {
                action = () => physicsModEnabled = !physicsModEnabled,
                isToggled = physicsModEnabled
            });
            layout.CurrentY += 30;

            if (physicsModEnabled)
            {
                // ===== PHYSICS PROPERTIES =====

                // Gravity Modification
                GUI.Label(new Rect(layout.MenuX + MARGIN + PADDING, layout.CurrentY,
                                  layout.Width - 120, ELEMENT_HEIGHT), "Gravity:", labelStyle);

                Rect gravityToggleRect = new Rect(layout.MenuX + layout.Width - 80, layout.CurrentY, 50, 24);

                if (GUI.Button(gravityToggleRect, "", gravityModEnabled ? toggleOnStyle : toggleOffStyle))
                {
                    gravityModEnabled = !gravityModEnabled;
                }

                uiElements.Add(new UIElement(UIElementType.Toggle, gravityToggleRect, "Custom Gravity")
                {
                    action = () => gravityModEnabled = !gravityModEnabled,
                    isToggled = gravityModEnabled
                });
                layout.CurrentY += 30;

                if (gravityModEnabled)
                {
                    layout.CurrentY += 5;
                    Rect gravitySliderRect = layout.GetElementRect(24);

                    customGravity = GUI.HorizontalSlider(gravitySliderRect, customGravity, 0f, 20f);
                    GUI.Label(new Rect(gravitySliderRect.x + gravitySliderRect.width - 40, gravitySliderRect.y, 40, 24),
                             customGravity.ToString("F1"), labelStyle);

                    uiElements.Add(new UIElement(UIElementType.Slider, gravitySliderRect)
                    {
                        sliderValue = customGravity,
                        sliderMin = 0f,
                        sliderMax = 20f,
                        sliderAction = (value) => customGravity = value
                    });
                    layout.AddSpace(10);
                }

                // Max Gravity Angle
                GUI.Label(new Rect(layout.MenuX + MARGIN + PADDING, layout.CurrentY,
                                  layout.Width - 120, ELEMENT_HEIGHT), "Max Gravity Angle:", labelStyle);

                Rect maxGravityAngleToggleRect = new Rect(layout.MenuX + layout.Width - 80, layout.CurrentY, 50, 24);

                if (GUI.Button(maxGravityAngleToggleRect, "", maxGravityAngleModEnabled ? toggleOnStyle : toggleOffStyle))
                {
                    maxGravityAngleModEnabled = !maxGravityAngleModEnabled;
                }

                uiElements.Add(new UIElement(UIElementType.Toggle, maxGravityAngleToggleRect, "Custom Max Gravity Angle")
                {
                    action = () => maxGravityAngleModEnabled = !maxGravityAngleModEnabled,
                    isToggled = maxGravityAngleModEnabled
                });
                layout.CurrentY += 30;

                if (maxGravityAngleModEnabled)
                {
                    layout.CurrentY += 5;
                    Rect maxGravityAngleSliderRect = layout.GetElementRect(24);

                    customMaxGravityAngle = GUI.HorizontalSlider(maxGravityAngleSliderRect, customMaxGravityAngle, 0f, 90f);
                    GUI.Label(new Rect(maxGravityAngleSliderRect.x + maxGravityAngleSliderRect.width - 40, maxGravityAngleSliderRect.y, 40, 24),
                             customMaxGravityAngle.ToString("F1"), labelStyle);

                    uiElements.Add(new UIElement(UIElementType.Slider, maxGravityAngleSliderRect)
                    {
                        sliderValue = customMaxGravityAngle,
                        sliderMin = 0f,
                        sliderMax = 90f,
                        sliderAction = (value) => customMaxGravityAngle = value
                    });
                    layout.AddSpace(10);
                }

                // Max Torque Modification
                GUI.Label(new Rect(layout.MenuX + MARGIN + PADDING, layout.CurrentY,
                                  layout.Width - 120, ELEMENT_HEIGHT), "Max Torque:", labelStyle);

                Rect maxTorqueToggleRect = new Rect(layout.MenuX + layout.Width - 80, layout.CurrentY, 50, 24);

                if (GUI.Button(maxTorqueToggleRect, "", maxTorqueModEnabled ? toggleOnStyle : toggleOffStyle))
                {
                    maxTorqueModEnabled = !maxTorqueModEnabled;
                }

                uiElements.Add(new UIElement(UIElementType.Toggle, maxTorqueToggleRect, "Custom Max Torque")
                {
                    action = () => maxTorqueModEnabled = !maxTorqueModEnabled,
                    isToggled = maxTorqueModEnabled
                });
                layout.CurrentY += 30;

                if (maxTorqueModEnabled)
                {
                    layout.CurrentY += 5;
                    Rect maxTorqueSliderRect = layout.GetElementRect(24);

                    customMaxTorque = GUI.HorizontalSlider(maxTorqueSliderRect, customMaxTorque, 10f, 200f);
                    GUI.Label(new Rect(maxTorqueSliderRect.x + maxTorqueSliderRect.width - 40, maxTorqueSliderRect.y, 40, 24),
                             customMaxTorque.ToString("F1"), labelStyle);

                    uiElements.Add(new UIElement(UIElementType.Slider, maxTorqueSliderRect)
                    {
                        sliderValue = customMaxTorque,
                        sliderMin = 10f,
                        sliderMax = 200f,
                        sliderAction = (value) => customMaxTorque = value
                    });
                    layout.AddSpace(10);
                }

                // Friction Modification
                GUI.Label(new Rect(layout.MenuX + MARGIN + PADDING, layout.CurrentY,
                                  layout.Width - 120, ELEMENT_HEIGHT), "Friction:", labelStyle);

                Rect frictionToggleRect = new Rect(layout.MenuX + layout.Width - 80, layout.CurrentY, 50, 24);

                if (GUI.Button(frictionToggleRect, "", frictionModEnabled ? toggleOnStyle : toggleOffStyle))
                {
                    frictionModEnabled = !frictionModEnabled;
                }

                uiElements.Add(new UIElement(UIElementType.Toggle, frictionToggleRect, "Custom Friction")
                {
                    action = () => frictionModEnabled = !frictionModEnabled,
                    isToggled = frictionModEnabled
                });
                layout.CurrentY += 30;

                if (frictionModEnabled)
                {
                    layout.CurrentY += 5;
                    Rect frictionSliderRect = layout.GetElementRect(24);

                    customFriction = GUI.HorizontalSlider(frictionSliderRect, customFriction, 0f, 2f);
                    GUI.Label(new Rect(frictionSliderRect.x + frictionSliderRect.width - 40, frictionSliderRect.y, 40, 24),
                             customFriction.ToString("F2"), labelStyle);

                    uiElements.Add(new UIElement(UIElementType.Slider, frictionSliderRect)
                    {
                        sliderValue = customFriction,
                        sliderMin = 0f,
                        sliderMax = 2f,
                        sliderAction = (value) => customFriction = value
                    });
                    layout.AddSpace(10);
                }

                // Drag Modification
                GUI.Label(new Rect(layout.MenuX + MARGIN + PADDING, layout.CurrentY,
                                  layout.Width - 120, ELEMENT_HEIGHT), "Drag:", labelStyle);

                Rect dragToggleRect = new Rect(layout.MenuX + layout.Width - 80, layout.CurrentY, 50, 24);

                if (GUI.Button(dragToggleRect, "", dragModEnabled ? toggleOnStyle : toggleOffStyle))
                {
                    dragModEnabled = !dragModEnabled;
                }

                uiElements.Add(new UIElement(UIElementType.Toggle, dragToggleRect, "Custom Drag")
                {
                    action = () => dragModEnabled = !dragModEnabled,
                    isToggled = dragModEnabled
                });
                layout.CurrentY += 30;

                if (dragModEnabled)
                {
                    layout.CurrentY += 5;
                    Rect dragSliderRect = layout.GetElementRect(24);

                    customDrag = GUI.HorizontalSlider(dragSliderRect, customDrag, 0f, 0.1f);
                    GUI.Label(new Rect(dragSliderRect.x + dragSliderRect.width - 40, dragSliderRect.y, 40, 24),
                             customDrag.ToString("F3"), labelStyle);

                    uiElements.Add(new UIElement(UIElementType.Slider, dragSliderRect)
                    {
                        sliderValue = customDrag,
                        sliderMin = 0f,
                        sliderMax = 0.1f,
                        sliderAction = (value) => customDrag = value
                    });
                    layout.AddSpace(10);
                }

                // Grab Ability Drag
                GUI.Label(new Rect(layout.MenuX + MARGIN + PADDING, layout.CurrentY,
                                  layout.Width - 120, ELEMENT_HEIGHT), "Grab Ability Drag:", labelStyle);

                Rect grabDragToggleRect = new Rect(layout.MenuX + layout.Width - 80, layout.CurrentY, 50, 24);

                if (GUI.Button(grabDragToggleRect, "", grabAbilityDragModEnabled ? toggleOnStyle : toggleOffStyle))
                {
                    grabAbilityDragModEnabled = !grabAbilityDragModEnabled;
                }

                uiElements.Add(new UIElement(UIElementType.Toggle, grabDragToggleRect, "Custom Grab Ability Drag")
                {
                    action = () => grabAbilityDragModEnabled = !grabAbilityDragModEnabled,
                    isToggled = grabAbilityDragModEnabled
                });
                layout.CurrentY += 30;

                if (grabAbilityDragModEnabled)
                {
                    layout.CurrentY += 5;
                    Rect grabDragSliderRect = layout.GetElementRect(24);

                    customGrabAbilityDrag = GUI.HorizontalSlider(grabDragSliderRect, customGrabAbilityDrag, 0f, 20f);
                    GUI.Label(new Rect(grabDragSliderRect.x + grabDragSliderRect.width - 40, grabDragSliderRect.y, 40, 24),
                             customGrabAbilityDrag.ToString("F1"), labelStyle);

                    uiElements.Add(new UIElement(UIElementType.Slider, grabDragSliderRect)
                    {
                        sliderValue = customGrabAbilityDrag,
                        sliderMin = 0f,
                        sliderMax = 20f,
                        sliderAction = (value) => customGrabAbilityDrag = value
                    });
                    layout.AddSpace(10);
                }

                // Bounciness (combined ground and air)
                GUI.Label(new Rect(layout.MenuX + MARGIN + PADDING, layout.CurrentY,
                                  layout.Width - 120, ELEMENT_HEIGHT), "Bounciness:", labelStyle);

                Rect bouncinessToggleRect = new Rect(layout.MenuX + layout.Width - 80, layout.CurrentY, 50, 24);

                if (GUI.Button(bouncinessToggleRect, "", bouncinessModEnabled ? toggleOnStyle : toggleOffStyle))
                {
                    bouncinessModEnabled = !bouncinessModEnabled;
                }

                uiElements.Add(new UIElement(UIElementType.Toggle, bouncinessToggleRect, "Custom Bounciness")
                {
                    action = () => bouncinessModEnabled = !bouncinessModEnabled,
                    isToggled = bouncinessModEnabled
                });
                layout.CurrentY += 30;

                if (bouncinessModEnabled)
                {
                    layout.CurrentY += 5;
                    Rect bouncinessSliderRect = layout.GetElementRect(24);

                    customBounciness = GUI.HorizontalSlider(bouncinessSliderRect, customBounciness, 0f, 1f);
                    GUI.Label(new Rect(bouncinessSliderRect.x + bouncinessSliderRect.width - 40, bouncinessSliderRect.y, 40, 24),
                             customBounciness.ToString("F2"), labelStyle);

                    uiElements.Add(new UIElement(UIElementType.Slider, bouncinessSliderRect)
                    {
                        sliderValue = customBounciness,
                        sliderMin = 0f,
                        sliderMax = 1f,
                        sliderAction = (value) => customBounciness = value
                    });
                    layout.AddSpace(10);
                }

                // Air-specific Bounciness
                GUI.Label(new Rect(layout.MenuX + MARGIN + PADDING, layout.CurrentY,
                                  layout.Width - 120, ELEMENT_HEIGHT), "Air Bounciness:", labelStyle);

                Rect airBouncinessToggleRect = new Rect(layout.MenuX + layout.Width - 80, layout.CurrentY, 50, 24);

                if (GUI.Button(airBouncinessToggleRect, "", airBouncinessModEnabled ? toggleOnStyle : toggleOffStyle))
                {
                    airBouncinessModEnabled = !airBouncinessModEnabled;
                }

                uiElements.Add(new UIElement(UIElementType.Toggle, airBouncinessToggleRect, "Custom Air Bounciness")
                {
                    action = () => airBouncinessModEnabled = !airBouncinessModEnabled,
                    isToggled = airBouncinessModEnabled
                });
                layout.CurrentY += 30;

                if (airBouncinessModEnabled)
                {
                    layout.CurrentY += 5;
                    Rect airBouncinessSliderRect = layout.GetElementRect(24);

                    customAirBounciness = GUI.HorizontalSlider(airBouncinessSliderRect, customAirBounciness, 0f, 1f);
                    GUI.Label(new Rect(airBouncinessSliderRect.x + airBouncinessSliderRect.width - 40, airBouncinessSliderRect.y, 40, 24),
                             customAirBounciness.ToString("F2"), labelStyle);

                    uiElements.Add(new UIElement(UIElementType.Slider, airBouncinessSliderRect)
                    {
                        sliderValue = customAirBounciness,
                        sliderMin = 0f,
                        sliderMax = 1f,
                        sliderAction = (value) => customAirBounciness = value
                    });
                    layout.AddSpace(10);
                }

                // Ball Radius Modification
                GUI.Label(new Rect(layout.MenuX + MARGIN + PADDING, layout.CurrentY,
                                  layout.Width - 120, ELEMENT_HEIGHT), "Ball Radius:", labelStyle);

                Rect ballRadiusToggleRect = new Rect(layout.MenuX + layout.Width - 80, layout.CurrentY, 50, 24);

                if (GUI.Button(ballRadiusToggleRect, "", ballRadiusModEnabled ? toggleOnStyle : toggleOffStyle))
                {
                    ballRadiusModEnabled = !ballRadiusModEnabled;
                }

                uiElements.Add(new UIElement(UIElementType.Toggle, ballRadiusToggleRect, "Custom Ball Radius")
                {
                    action = () => ballRadiusModEnabled = !ballRadiusModEnabled,
                    isToggled = ballRadiusModEnabled
                });
                layout.CurrentY += 30;

                if (ballRadiusModEnabled)
                {
                    layout.CurrentY += 5;
                    Rect ballRadiusSliderRect = layout.GetElementRect(24);

                    customBallRadius = GUI.HorizontalSlider(ballRadiusSliderRect, customBallRadius, 0.1f, 1f);
                    GUI.Label(new Rect(ballRadiusSliderRect.x + ballRadiusSliderRect.width - 40, ballRadiusSliderRect.y, 40, 24),
                             customBallRadius.ToString("F2"), labelStyle);

                    uiElements.Add(new UIElement(UIElementType.Slider, ballRadiusSliderRect)
                    {
                        sliderValue = customBallRadius,
                        sliderMin = 0.1f,
                        sliderMax = 1f,
                        sliderAction = (value) => customBallRadius = value
                    });
                    layout.AddSpace(10);
                }

                // Ball Visualizer Toggle
                GUI.Label(new Rect(layout.MenuX + MARGIN + PADDING, layout.CurrentY, layout.Width - 120, ELEMENT_HEIGHT), "Ball Visualizer:", labelStyle);
                Rect ballVisualizerToggleRect = new Rect(layout.MenuX + layout.Width - 80, layout.CurrentY, 50, 24);
                if (GUI.Button(ballVisualizerToggleRect, "", ballVisualizerEnabled ? toggleOnStyle : toggleOffStyle))
                {
                    ballVisualizerEnabled = !ballVisualizerEnabled;
                }
                uiElements.Add(new UIElement(UIElementType.Toggle, ballVisualizerToggleRect, "Ball Visualizer")
                {
                    action = () => ballVisualizerEnabled = !ballVisualizerEnabled,
                    isToggled = ballVisualizerEnabled
                });
                layout.AddSpace(5);
            }

            // Apply the physics changes in Update method
        }

        private void DrawControlsMenu(MenuLayout layout)
        {
            // Setup styles similar to FPStogglemod.cs
            GUIStyle headerStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = headerTexture, textColor = Color.white },
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(10, 10, 5, 5)
            };

            GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = Color.white }
            };

            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
            {
                normal = {
                    background = buttonTexture,
                    textColor = Color.white
                },
                hover = { background = buttonHoverTexture },
                active = { background = buttonTexture },
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(10, 10, 5, 5)
            };

            // Add toggle styles
            GUIStyle toggleOnStyle = new GUIStyle(GUI.skin.button)
            {
                normal = { background = toggleOnTexture },
                hover = { background = toggleOnTexture },
                active = { background = toggleOnTexture },
                fixedWidth = 50,
                fixedHeight = 24,
                margin = new RectOffset(5, 5, 5, 5)
            };

            GUIStyle toggleOffStyle = new GUIStyle(GUI.skin.button)
            {
                normal = { background = toggleOffTexture },
                hover = { background = toggleOffTexture },
                active = { background = toggleOffTexture },
                fixedWidth = 50,
                fixedHeight = 24,
                margin = new RectOffset(5, 5, 5, 5)
            };

            // Clear UI elements
            uiElements.Clear();

            // Draw the header for the controls menu
            GUI.Box(layout.GetRect(30), "Keybinds", headerStyle);

            // Draw a separator and add spacing
            layout.AddSpace(5);
            GUI.Box(layout.GetElementRect(2), "", new GUIStyle() { normal = { background = separatorTexture } });
            layout.AddSpace(5);

            if (waitingForKeyPress)
            {
                // Draw semi-transparent overlay
                GUI.color = new Color(0, 0, 0, 0.8f);
                GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "");
                GUI.color = Color.white;

                // Draw popup box
                float popupWidth = 350;
                float popupHeight = 150;
                Rect popupRect = new Rect(Screen.width / 2 - popupWidth / 2, Screen.height / 2 - popupHeight / 2, popupWidth, popupHeight);
                GUI.Box(popupRect, "", new GUIStyle(GUI.skin.box) { normal = { background = backgroundTexture } });

                string actionName = currentBindingAction.Split('_')[0];
                string bindingType = currentBindingAction.Split('_')[1];

                GUIStyle messageStyle = new GUIStyle(GUI.skin.label)
                {
                    normal = { textColor = Color.white },
                    fontSize = 14,
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true
                };

                // Main instruction
                GUI.Label(new Rect(popupRect.x + 10, popupRect.y + 20, popupRect.width - 20, 30),
                         $"Press any key to bind for: {actionName}", messageStyle);

                // Additional instructions
                GUI.Label(new Rect(popupRect.x + 10, popupRect.y + 50, popupRect.width - 20, 40),
                         "Press ESC to clear the binding\nClick Cancel or press Space to keep previous binding", messageStyle);

                if (GUI.Button(new Rect(popupRect.x + popupWidth / 2 - 50, popupRect.y + popupHeight - 40, 100, 30), "Cancel", buttonStyle))
                {
                    waitingForKeyPress = false;
                    currentBindingAction = "";
                }

                // Handle key input
                if (Event.current.type == EventType.KeyDown)
                {
                    if (Event.current.keyCode == KeyCode.Escape)
                    {
                        ClearBinding(currentBindingAction);
                    }
                    else if (Event.current.keyCode == KeyCode.Space)
                    {
                        waitingForKeyPress = false;
                        currentBindingAction = "";
                    }
                    else
                    {
                        AssignKeyBinding(currentBindingAction, Event.current.keyCode);
                    }
                    Event.current.Use();
                }
            }
            else
            {
                // Rest of the controls menu code...
                // Draw header row for actions
                float actionWidth = 120;
                float bindingWidth = 45;
                float spacing = 10;
                Rect headerRect = layout.GetElementRect(ELEMENT_HEIGHT);
                GUI.Label(new Rect(headerRect.x, headerRect.y, actionWidth, ELEMENT_HEIGHT), "Action", labelStyle);
                GUI.Label(new Rect(headerRect.x + actionWidth + spacing, headerRect.y, bindingWidth, ELEMENT_HEIGHT), "Key 1", labelStyle);
                GUI.Label(new Rect(headerRect.x + actionWidth + bindingWidth + spacing * 2, headerRect.y, bindingWidth, ELEMENT_HEIGHT), "Key 2", labelStyle);
                layout.AddSpace(5);
                GUI.Box(new Rect(layout.MenuX + MARGIN, layout.CurrentY, layout.Width - (MARGIN * 2), 2), "", new GUIStyle() { normal = { background = separatorTexture } });
                layout.AddSpace(10);

                // Draw each action binding row from actionBindings
                foreach (var actionPair in actionBindings)
                {
                    Rect rowRect = layout.GetElementRect(ELEMENT_HEIGHT);
                    // Format the action name for display
                    string displayName = string.Join(" ", System.Text.RegularExpressions.Regex.Split(actionPair.Key, @"(?<!^)(?=[A-Z])"));
                    GUI.Label(new Rect(rowRect.x, rowRect.y, actionWidth, ELEMENT_HEIGHT), displayName, labelStyle);

                    // Draw primary binding button
                    string primaryText = actionPair.Value[0].Value == KeyCode.None ? "---" : actionPair.Value[0].Value.ToString();
                    Rect primaryRect = new Rect(rowRect.x + actionWidth + spacing, rowRect.y, bindingWidth, ELEMENT_HEIGHT);
                    if (GUI.Button(primaryRect, primaryText, buttonStyle))
                    {
                        waitingForKeyPress = true;
                        currentBindingAction = actionPair.Key + "_Primary";
                    }
                    uiElements.Add(new UIElement(UIElementType.Button, primaryRect, actionPair.Key + "_Primary")
                    {
                        action = () => { waitingForKeyPress = true; currentBindingAction = actionPair.Key + "_Primary"; }
                    });

                    // Draw secondary binding button
                    string secondaryText = (actionPair.Value.Count > 1 && actionPair.Value[1].Value != KeyCode.None) ? actionPair.Value[1].Value.ToString() : "---";
                    Rect secondaryRect = new Rect(rowRect.x + actionWidth + bindingWidth + spacing * 2, rowRect.y, bindingWidth, ELEMENT_HEIGHT);
                    if (GUI.Button(secondaryRect, secondaryText, buttonStyle))
                    {
                        waitingForKeyPress = true;
                        currentBindingAction = actionPair.Key + "_Secondary";
                    }
                    uiElements.Add(new UIElement(UIElementType.Button, secondaryRect, actionPair.Key + "_Secondary")
                    {
                        action = () => { waitingForKeyPress = true; currentBindingAction = actionPair.Key + "_Secondary"; }
                    });
                    layout.AddSpace(5);
                }

                layout.AddSpace(10);
                GUI.Box(layout.GetElementRect(2), "", new GUIStyle() { normal = { background = separatorTexture } });
                layout.AddSpace(5);
                GUI.Label(layout.GetElementRect(ELEMENT_HEIGHT), "Press Escape to cancel binding", labelStyle);
            }

            // Add UI Position toggle after the keybinds section
            GUI.Box(layout.GetRect(30), "UI Settings", headerStyle);
            GUI.Box(layout.GetElementRect(1), "", new GUIStyle() { normal = { background = separatorTexture } });
            layout.AddSpace(5);

            GUI.Label(new Rect(layout.MenuX + MARGIN + PADDING, layout.CurrentY,
                              layout.Width - 120, ELEMENT_HEIGHT), "UI Position:", labelStyle);

            Rect uiPositionRect = new Rect(layout.MenuX + layout.Width - 80, layout.CurrentY, 50, 24);

            if (GUI.Button(uiPositionRect, "", uiOnRightSide ? toggleOnStyle : toggleOffStyle))
            {
                uiOnRightSide = !uiOnRightSide;
            }

            uiElements.Add(new UIElement(UIElementType.Toggle, uiPositionRect, "UI Position")
            {
                action = () => uiOnRightSide = !uiOnRightSide,
                isToggled = uiOnRightSide
            });
            layout.CurrentY += 30;

            // Add help text
            GUIStyle helpStyle = new GUIStyle(labelStyle)
            {
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
            };
            GUI.Label(layout.GetElementRect(20), "Toggle between right and left side of screen", helpStyle);
        }

        private void AssignKeyBinding(string bindingAction, KeyCode keyCode)
        {
            string[] parts = bindingAction.Split('_');
            string action = parts[0];
            string slot = parts[1]; // Primary or Secondary

            int index = slot == "Primary" ? 0 : 1;

            if (actionBindings.ContainsKey(action) && index < actionBindings[action].Count)
            {
                actionBindings[action][index].Value = keyCode;
                ShowNotification($"{action} {slot} binding set to {keyCode}");
            }

            waitingForKeyPress = false;
            currentBindingAction = "";
        }

        private void ClearBinding(string bindingAction)
        {
            string[] parts = bindingAction.Split('_');
            string action = parts[0];
            string slot = parts[1]; // Primary or Secondary

            int index = slot == "Primary" ? 0 : 1;

            if (actionBindings.ContainsKey(action) && index < actionBindings[action].Count)
            {
                actionBindings[action][index].Value = KeyCode.None;
                ShowNotification($"{action} {slot} binding cleared");
            }

            waitingForKeyPress = false;
            currentBindingAction = "";
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

            // Add this after getting playerController but before the physics mod adjustments
            if (playerController != null)
            {
                // Check for and prevent ragdoll if disabled
                if (physicsModEnabled && disableRagdollEnabled)
                {
                    var playerRagdoll = playerTransform.GetComponent<PlayerRagdoll>();
                    if (playerController.IsRagdollState)
                    {
                        ForceExitRagdoll();
                    }
                }

                // Continue with existing physics mod adjustments
                if (physicsModEnabled)
                {
                    // ... existing physics mod code ...
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
                if (gravityModEnabled) ApplyCustomGravity();
                if (bouncinessModEnabled || airBouncinessModEnabled) ApplyCustomAirBounciness();
                if (ballRadiusModEnabled) ApplyCustomBallRadius();
                if (maxGravityAngleModEnabled) ApplyCustomMaxGravityAngle();
                if (maxTorqueModEnabled) ApplyCustomMaxTorque();
                if (grabAbilityDragModEnabled) ApplyCustomGrabAbilityDrag();
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

            // Check for any key press for all actions
            foreach (var actionPair in actionBindings)
            {
                foreach (var binding in actionPair.Value)
                {
                    if (binding.Value != KeyCode.None && Input.GetKeyDown(binding.Value))
                    {
                        // Handle the action
                        HandleAction(actionPair.Key);
                        break;
                    }
                }
            }

            if (isNoclipActive)
            {
                UpdateNoclip();
            }
        }

        private void HandleAction(string action)
        {
            switch (action)
            {
                case "Menu":
                    ToggleMenu();
                    break;
                case "Noclip":
                    ToggleNoclip();
                    break;
                case "Teleport":
                    if (!string.IsNullOrEmpty(selectedCheckpoint))
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
                    break;
                case "TimeScale":
                    ToggleTimeScale();
                    break;
                case "TempCheckpoint":
                    SetTemporaryCheckpoint();
                    break;
                case "PhysicsMod":
                    physicsModEnabled = !physicsModEnabled;
                    if (!physicsModEnabled)
                    {
                        RestoreOriginalValues();
                    }
                    ShowNotification($"Physics Mod: {(physicsModEnabled ? "Enabled" : "Disabled")}");
                    break;
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
            // Create styles for consistent appearance
            GUIStyle headerStyle = new GUIStyle(GUI.skin.box)
            {
                normal = {
                    background = headerTexture,
                    textColor = Color.white
                },
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(10, 10, 5, 5)
            };

            GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = Color.white }
            };

            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
            {
                normal = {
                    background = buttonTexture,
                    textColor = Color.white
                },
                hover = { background = buttonHoverTexture },
                active = { background = buttonTexture },
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(10, 10, 5, 5)
            };

            // Style for selected buttons
            GUIStyle selectedButtonStyle = new GUIStyle(buttonStyle)
            {
                normal = { background = selectedTexture }
            };

            GUIStyle textFieldStyle = new GUIStyle(GUI.skin.textField)
            {
                normal = {
                    background = textFieldTexture,
                    textColor = Color.white
                },
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(8, 8, 4, 4),
                border = new RectOffset(2, 2, 2, 2)
            };

            // Toggle styles
            GUIStyle toggleOnStyle = new GUIStyle(GUI.skin.button)
            {
                normal = { background = toggleOnTexture },
                hover = { background = toggleOnTexture },
                active = { background = toggleOnTexture },
                fixedWidth = 50,
                fixedHeight = 24,
                margin = new RectOffset(5, 5, 5, 5)
            };

            GUIStyle toggleOffStyle = new GUIStyle(GUI.skin.button)
            {
                normal = { background = toggleOffTexture },
                hover = { background = toggleOffTexture },
                active = { background = toggleOffTexture },
                fixedWidth = 50,
                fixedHeight = 24,
                margin = new RectOffset(5, 5, 5, 5)
            };

            // Alternating row styles
            GUIStyle alternateRowStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = alternateRowTexture },
                padding = new RectOffset(5, 5, 5, 5),
                margin = new RectOffset(0, 0, 0, 0)
            };

            // Delete button style
            GUIStyle deleteButtonStyle = new GUIStyle(GUI.skin.button)
            {
                normal = {
                    background = deleteButtonTexture,
                    textColor = Color.white
                },
                hover = { background = deleteButtonHoverTexture },
                active = { background = deleteButtonTexture },
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(5, 5, 3, 3)
            };

            // Improved scrollbar style
            GUIStyle verticalScrollbarStyle = new GUIStyle(GUI.skin.verticalScrollbar)
            {
                normal = { background = textFieldTexture }
            };

            GUIStyle verticalScrollbarThumbStyle = new GUIStyle(GUI.skin.verticalScrollbarThumb)
            {
                normal = { background = buttonTexture }
            };

            // Separator style
            GUIStyle separatorStyle = new GUIStyle
            {
                normal = { background = separatorTexture },
                margin = new RectOffset(0, 0, 5, 5),
                fixedHeight = 1
            };

            // Clear UI elements
            uiElements.Clear();

            // Checkpoint Settings Header
            GUI.Box(layout.GetRect(30), "Checkpoint Settings", headerStyle);

            // Section separator
            GUI.Box(layout.GetElementRect(1), "", separatorStyle);
            layout.AddSpace(5);

            // Convert checkbox to a toggle switch with label
            GUI.Label(new Rect(layout.MenuX + MARGIN + PADDING, layout.CurrentY,
                              layout.Width - 120, ELEMENT_HEIGHT), "Freeze After Teleporting:", labelStyle);

            Rect toggleRect = new Rect(layout.MenuX + layout.Width - 80, layout.CurrentY, 50, 24);

            // Draw toggle switch
            if (GUI.Button(toggleRect, "", keepNoclipAfterTeleport ? toggleOnStyle : toggleOffStyle))
            {
                keepNoclipAfterTeleport = !keepNoclipAfterTeleport;
            }

            layout.CurrentY += 30;  // Manual adjustment for the toggle row

            uiElements.Add(new UIElement(UIElementType.Button, toggleRect, "Freeze After Teleporting")
            {
                action = () => keepNoclipAfterTeleport = !keepNoclipAfterTeleport
            });

            // Section separator
            GUI.Box(layout.GetElementRect(1), "", separatorStyle);
            layout.AddSpace(5);

            // Toggle between preset and custom checkpoints with arrow indicator
            Rect toggleButtonRect = layout.GetRect(30);

            string toggleButtonText = showingPresets ? "⟵ Show Custom Checkpoints" : "Show Preset Locations ⟶";
            if (GUI.Button(toggleButtonRect, toggleButtonText, buttonStyle))
            {
                showingPresets = !showingPresets;
                selectedCheckpoint = "";
                scrollPosition = Vector2.zero;
            }
            uiElements.Add(new UIElement(UIElementType.Button, toggleButtonRect, toggleButtonText)
            {
                action = () => {
                    showingPresets = !showingPresets;
                    selectedCheckpoint = "";
                    scrollPosition = Vector2.zero;
                }
            });

            // Section separator
            GUI.Box(layout.GetElementRect(1), "", separatorStyle);
            layout.AddSpace(5);

            if (showingPresets)
            {
                // Preset Checkpoints section
                GUI.Box(layout.GetRect(30), "Preset Locations", headerStyle);
                Rect viewRect = layout.GetElementRect(250);
                Rect contentRect = new Rect(0, 0, viewRect.width - 20, presetCheckpoints.Count * 30);

                // Apply custom scrollbar styles
                GUI.skin.verticalScrollbar = verticalScrollbarStyle;
                GUI.skin.verticalScrollbarThumb = verticalScrollbarThumbStyle;

                scrollPosition = GUI.BeginScrollView(viewRect, scrollPosition, contentRect);

                float currentY = 0;
                int presetIndex = 0;
                foreach (var checkpoint in presetCheckpoints)
                {
                    // Alternate row background
                    if (presetIndex % 2 == 1)
                    {
                        GUI.Box(new Rect(0, currentY, contentRect.width, 28), "", alternateRowStyle);
                    }

                    Rect buttonRect = new Rect(0, currentY, contentRect.width, 28);

                    // Use button style for selected checkpoint
                    GUIStyle checkpointButtonStyle = checkpoint.Key == selectedCheckpoint ? selectedButtonStyle : buttonStyle;

                    // Check for hover
                    string buttonId = "preset_" + checkpoint.Key;
                    if (Event.current.type == EventType.Repaint && buttonRect.Contains(Event.current.mousePosition))
                    {
                        hoveredElements[buttonId] = true;
                    }
                    else if (Event.current.type == EventType.MouseMove && !buttonRect.Contains(Event.current.mousePosition))
                    {
                        hoveredElements[buttonId] = false;
                    }

                    if (GUI.Button(buttonRect, checkpoint.Key, checkpointButtonStyle))
                    {
                        selectedCheckpoint = checkpoint.Key;
                        if (Input.GetKey(KeyCode.LeftControl))
                        {
                            TeleportToPresetCheckpoint(selectedCheckpoint);
                        }
                    }

                    uiElements.Add(new UIElement(UIElementType.Button, buttonRect, checkpoint.Key)
                    {
                        action = () => {
                            selectedCheckpoint = checkpoint.Key;
                            if (Input.GetKey(KeyCode.LeftControl))
                            {
                                TeleportToPresetCheckpoint(selectedCheckpoint);
                            }
                        }
                    });
                    presetIndex++;
                    currentY += 30;
                }

                GUI.EndScrollView();
            }
            else
            {
                // Custom Checkpoints section
                GUI.Box(layout.GetRect(30), "Custom Checkpoints", headerStyle);

                // New checkpoint creation
                GUI.Label(layout.GetElementRect(24), "New Checkpoint:", labelStyle);

                // Text field with placeholder and border
                Rect textFieldRect = layout.GetElementRect(24);

                // Draw border for text field
                GUI.Box(new Rect(textFieldRect.x - 1, textFieldRect.y - 1, textFieldRect.width + 2, textFieldRect.height + 2), "",
                       new GUIStyle { normal = { background = textFieldBorderTexture } });

                // Show placeholder text if the field is empty
                if (string.IsNullOrEmpty(newCheckpointName))
                {
                    GUI.Label(textFieldRect, "   Enter checkpoint name...",
                             new GUIStyle(labelStyle) { normal = { textColor = new Color(0.7f, 0.7f, 0.7f) } });
                }

                newCheckpointName = GUI.TextField(textFieldRect, newCheckpointName, textFieldStyle);
                uiElements.Add(new UIElement(UIElementType.TextField, textFieldRect, "New Checkpoint Name")
                {
                    text = newCheckpointName
                });

                Rect createButtonRect = layout.GetElementRect(28);

                if (GUI.Button(createButtonRect, "Create Checkpoint Here", buttonStyle))
                {
                    CreateNewCheckpoint();
                }
                uiElements.Add(new UIElement(UIElementType.Button, createButtonRect, "Create Checkpoint Here")
                {
                    action = CreateNewCheckpoint
                });

                // Section separator
                GUI.Box(layout.GetElementRect(1), "", separatorStyle);
                layout.AddSpace(5);

                // Saved checkpoints list
                GUI.Label(layout.GetElementRect(24), "Saved Checkpoints:", labelStyle);

                Rect viewRect = layout.GetElementRect(150);
                Rect contentRect = new Rect(0, 0, viewRect.width - 20, customCheckpoints.Count * 30);

                // Apply custom scrollbar styles
                GUI.skin.verticalScrollbar = verticalScrollbarStyle;
                GUI.skin.verticalScrollbarThumb = verticalScrollbarThumbStyle;

                scrollPosition = GUI.BeginScrollView(viewRect, scrollPosition, contentRect);

                float currentY = 0;
                List<string> checkpointsToRemove = new List<string>();
                int customIndex = 0;

                foreach (var checkpoint in customCheckpoints)
                {
                    // Alternate row background
                    if (customIndex % 2 == 1)
                    {
                        GUI.Box(new Rect(0, currentY, contentRect.width, 28), "", alternateRowStyle);
                    }

                    // Use button style for selected checkpoint
                    GUIStyle checkpointButtonStyle = checkpoint.Key == selectedCheckpoint ? selectedButtonStyle : buttonStyle;

                    Rect buttonRect = new Rect(0, currentY, contentRect.width - 34, 28);

                    // Check for hover
                    string buttonId = "custom_" + checkpoint.Key;
                    if (Event.current.type == EventType.Repaint && buttonRect.Contains(Event.current.mousePosition))
                    {
                        hoveredElements[buttonId] = true;
                    }
                    else if (Event.current.type == EventType.MouseMove && !buttonRect.Contains(Event.current.mousePosition))
                    {
                        hoveredElements[buttonId] = false;
                    }

                    if (GUI.Button(buttonRect, checkpoint.Key, checkpointButtonStyle))
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

                    uiElements.Add(new UIElement(UIElementType.Button, buttonRect, checkpoint.Key)
                    {
                        action = () => {
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
                    });

                    // More distinct delete button
                    Rect deleteRect = new Rect(contentRect.width - 32, currentY, 28, 28);

                    // Check for hover on delete button
                    string deleteId = "delete_" + checkpoint.Key;
                    if (Event.current.type == EventType.Repaint && deleteRect.Contains(Event.current.mousePosition))
                    {
                        hoveredElements[deleteId] = true;
                    }
                    else if (Event.current.type == EventType.MouseMove && !deleteRect.Contains(Event.current.mousePosition))
                    {
                        hoveredElements[deleteId] = false;
                    }

                    if (GUI.Button(deleteRect, "✕", deleteButtonStyle))
                    {
                        checkpointsToRemove.Add(checkpoint.Key);
                    }

                    uiElements.Add(new UIElement(UIElementType.Button, deleteRect, "X")
                    {
                        action = () => checkpointsToRemove.Add(checkpoint.Key)
                    });
                    customIndex++;
                    currentY += 30;
                }

                GUI.EndScrollView();

                // Process checkpoint deletions
                foreach (string checkpointName in checkpointsToRemove)
                {
                    DeleteCheckpoint(checkpointName);
                }
            }

            // Selected checkpoint section
            if (!string.IsNullOrEmpty(selectedCheckpoint))
            {
                // Section separator
                GUI.Box(layout.GetElementRect(1), "", separatorStyle);
                layout.AddSpace(5);

                GUI.Box(layout.GetRect(30), "Selected Checkpoint", headerStyle);
                GUI.Label(layout.GetElementRect(24), $"Selected: {selectedCheckpoint}", labelStyle);

                Rect teleportButtonRect = layout.GetElementRect(28);

                if (GUI.Button(teleportButtonRect, "Teleport", buttonStyle))
                {
                    if (showingPresets)
                        TeleportToPresetCheckpoint(selectedCheckpoint);
                    else
                        TeleportToCustomCheckpoint(selectedCheckpoint);
                }

                uiElements.Add(new UIElement(UIElementType.Button, teleportButtonRect, "Teleport")
                {
                    action = () => {
                        if (showingPresets)
                            TeleportToPresetCheckpoint(selectedCheckpoint);
                        else
                            TeleportToCustomCheckpoint(selectedCheckpoint);
                    }
                });
            }

            // Section separator
            GUI.Box(layout.GetElementRect(1), "", separatorStyle);
            layout.AddSpace(5);

            // Help text at the bottom
            GUIStyle helpStyle = new GUIStyle(labelStyle)
            {
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
            };

            GUI.Label(layout.GetElementRect(40),
                $"Ctrl + Click to quick teleport\nPress {teleportKey.Value} to teleport to selected checkpoint",
                helpStyle);
        }
        private void OnGUI()
        {
            InitializeTextures();

            // Create styles
            GUIStyle windowStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = backgroundTexture },
                padding = new RectOffset(10, 10, 10, 10)
            };

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
                alignment = TextAnchor.UpperCenter
            };

            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
            {
                normal = {
                    background = buttonTexture,
                    textColor = Color.white
                },
                hover = { background = buttonHoverTexture },
                active = { background = buttonTexture },
                fontSize = 12,
                padding = new RectOffset(10, 10, 5, 5)
            };

            // Add toggle styles
            GUIStyle toggleOnStyle = new GUIStyle(GUI.skin.button)
            {
                normal = { background = toggleOnTexture },
                hover = { background = toggleOnTexture },
                active = { background = toggleOnTexture },
                fixedWidth = 50,
                fixedHeight = 24,
                margin = new RectOffset(5, 5, 5, 5)
            };

            GUIStyle toggleOffStyle = new GUIStyle(GUI.skin.button)
            {
                normal = { background = toggleOffTexture },
                hover = { background = toggleOffTexture },
                active = { background = toggleOffTexture },
                fixedWidth = 50,
                fixedHeight = 24,
                margin = new RectOffset(5, 5, 5, 5)
            };

            // Notification display
            if (Time.unscaledTime < notificationTime)
            {
                GUI.Label(new Rect(Screen.width / 2 - 100, 20, 200, 30), notificationText);
            }

            GUI.Label(new Rect(Screen.width - 120, Screen.height - 25, 100, 20), "Practice Mod");

            // Update main window position based on uiOnRightSide
            float windowX = uiOnRightSide ? Screen.width - MENU_WIDTH - 10 : 10;

            if (!showMenu) return;

            // Update the main window position
            GUI.Box(new Rect(windowX, 10, MENU_WIDTH, Screen.height - 20), "", windowStyle);
            GUI.Label(new Rect(windowX, 20, MENU_WIDTH, 25), "Practice Mod", titleStyle);

            // Update button positions
            float buttonY = 50;
            float buttonWidth = (MENU_WIDTH - 40) / 3;

            // Update all button X positions
            float baseButtonX = windowX + 10;

            // Movement button
            Rect movementButtonRect = new Rect(baseButtonX, buttonY, buttonWidth, 30);

            if (GUI.Button(movementButtonRect, "Movement",
                          currentCategory == MenuCategory.Movement ? buttonStyle : buttonStyle))
            {
                currentCategory = currentCategory == MenuCategory.Movement ? MenuCategory.None : MenuCategory.Movement;
            }
            uiElements.Add(new UIElement(UIElementType.Button, movementButtonRect, "Movement")
            {
                action = () => currentCategory = currentCategory == MenuCategory.Movement ? MenuCategory.None : MenuCategory.Movement
            });

            // Checkpoints button
            Rect checkpointsButtonRect = new Rect(baseButtonX + buttonWidth + 10, buttonY, buttonWidth, 30);

            if (GUI.Button(checkpointsButtonRect, "Checkpoints",
                          currentCategory == MenuCategory.Checkpoints ? buttonStyle : buttonStyle))
            {
                currentCategory = currentCategory == MenuCategory.Checkpoints ? MenuCategory.None : MenuCategory.Checkpoints;
            }
            uiElements.Add(new UIElement(UIElementType.Button, checkpointsButtonRect, "Checkpoints")
            {
                action = () => currentCategory = currentCategory == MenuCategory.Checkpoints ? MenuCategory.None : MenuCategory.Checkpoints
            });

            // Controls button
            Rect controlsButtonRect = new Rect(baseButtonX + (buttonWidth + 10) * 2, buttonY, buttonWidth, 30);

            if (GUI.Button(controlsButtonRect, "Controls",
                          currentCategory == MenuCategory.Controls ? buttonStyle : buttonStyle))
            {
                currentCategory = currentCategory == MenuCategory.Controls ? MenuCategory.None : MenuCategory.Controls;
            }
            uiElements.Add(new UIElement(UIElementType.Button, controlsButtonRect, "Controls")
            {
                action = () => currentCategory = currentCategory == MenuCategory.Controls ? MenuCategory.None : MenuCategory.Controls
            });

            // Content area
            if (currentCategory != MenuCategory.None)
            {
                // Update content area position
                Rect contentArea = new Rect(baseButtonX + 10, buttonY + 40, MENU_WIDTH - 40, Screen.height - buttonY - 60);
                GUI.BeginGroup(contentArea);

                // Set up menu layout
                MenuLayout layout = new MenuLayout(0, 0, contentArea.width);

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

                GUI.EndGroup();
            }

            // Handle navigation at the end
            HandleMenuNavigation();
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
                    if (physicsModEnabled && disableRagdollEnabled)
                    {
                        // We've disabled ragdoll, so exit it before enabling noclip
                        ForceExitRagdoll();
                        EnableNoclip();
                    }
                    else
                    {
                        // Standard ragdoll exit procedure
                        if (playerRagdoll != null)
                        {
                            playerRagdoll.SetRagdollActive(false, false);
                        }
                        // Change to move state
                        playerController.ChangeToMoveState();
                        // Wait a frame to ensure ragdoll is fully disabled
                        StartCoroutine(EnableNoclipNextFrame());
                    }
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
                        // Use InvariantCulture to always save with period as decimal separator
                        writer.WriteLine($"{checkpoint.Key}," +
                                       $"{checkpoint.Value.Position.x.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                                       $"{checkpoint.Value.Position.y.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                                       $"{checkpoint.Value.Position.z.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                                       $"{checkpoint.Value.Velocity.x.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                                       $"{checkpoint.Value.Velocity.y.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                                       $"{checkpoint.Value.Velocity.z.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                                       $"{checkpoint.Value.PlayerRotation.x.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                                       $"{checkpoint.Value.PlayerRotation.y.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                                       $"{checkpoint.Value.PlayerRotation.z.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                                       $"{checkpoint.Value.PlayerRotation.w.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                                       $"{checkpoint.Value.CameraRotation.x.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                                       $"{checkpoint.Value.CameraRotation.y.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                                       $"{checkpoint.Value.CameraRotation.z.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                                       $"{checkpoint.Value.CameraRotation.w.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
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
                        try
                        {
                            // Split by comma first to get the checkpoint name and values
                            string[] parts = line.Split(',');
                            if (parts.Length == 15)
                            {
                                string name = parts[0];
                                // Parse numbers using InvariantCulture to ensure consistent decimal handling
                                Vector3 position = new Vector3(
                                    float.Parse(parts[1].Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture),
                                    float.Parse(parts[2].Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture),
                                    float.Parse(parts[3].Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture)
                                );
                                Vector3 velocity = new Vector3(
                                    float.Parse(parts[4].Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture),
                                    float.Parse(parts[5].Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture),
                                    float.Parse(parts[6].Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture)
                                );
                                Quaternion playerRotation = new Quaternion(
                                    float.Parse(parts[7].Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture),
                                    float.Parse(parts[8].Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture),
                                    float.Parse(parts[9].Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture),
                                    float.Parse(parts[10].Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture)
                                );
                                Quaternion cameraRotation = new Quaternion(
                                    float.Parse(parts[11].Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture),
                                    float.Parse(parts[12].Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture),
                                    float.Parse(parts[13].Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture),
                                    float.Parse(parts[14].Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture)
                                );
                                customCheckpoints[name] = new CheckpointData(position, velocity, playerRotation, cameraRotation);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"Failed to parse checkpoint line: {line}. Error: {ex.Message}");
                            continue; // Skip this line and continue with the next one
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
            if (valuesStored)
                return;

            // Get physics components if needed
            if (torquePhysics == null)
            {
                playerController = FindObjectOfType<PlayerController>();
                if (playerController != null)
                {
                    torquePhysics = playerController.GetComponent<TorquePhysics>();
                }
            }

            if (torquePhysics != null)
            {
                // Store original values
                originalFriction = torquePhysics.SlopeFrictionCurve.keys[0].value;
                originalAcceleration = torquePhysics.Acceleration;
                originalDrag = torquePhysics.VelocityDrag;
                originalGravity = torquePhysics.GravityMagnitude;
                originalMaxGravityAngle = torquePhysics.MaxGravityAngle;
                originalMaxTorque = torquePhysics.MaxTorque;
                originalGrabAbilityDrag = torquePhysics.GrabAbilityDrag;
                originalBallRadius = torquePhysics.BallRadius;
                originalGroundedBounciness = torquePhysics.GroundBounciness;
                originalAirBounciness = torquePhysics.AirBounciness;

                valuesStored = true;
                Debug.Log("Original physics values stored");
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
            if (torquePhysics == null)
                return;

            // Use reflection to get and set values
            var physicsPresetField = torquePhysics.GetType().GetField("physicsPreset", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (physicsPresetField != null)
            {
                var physicsPreset = physicsPresetField.GetValue(torquePhysics) as TorquePlayerPhysicsPreset;

                if (physicsPreset != null)
                {
                    if (frictionModEnabled)
                    {
                        // Get the current keys from the curve
                        Keyframe[] keys = physicsPreset.slopeFrictionCurve.keys;

                        // Modify the friction value while keeping the same x coordinates
                        for (int i = 0; i < keys.Length; i++)
                        {
                            keys[i].value = customFriction;
                        }

                        // Create a new curve with the modified keys
                        AnimationCurve newCurve = new AnimationCurve(keys);
                        physicsPreset.slopeFrictionCurve = newCurve;
                    }
                    else
                    {
                        // Reset to original friction
                        Keyframe[] keys = physicsPreset.slopeFrictionCurve.keys;
                        for (int i = 0; i < keys.Length; i++)
                        {
                            keys[i].value = originalFriction;
                        }
                        AnimationCurve newCurve = new AnimationCurve(keys);
                        physicsPreset.slopeFrictionCurve = newCurve;
                    }
                }
            }
        }

        private void ApplyCustomGravity()
        {
            if (torquePhysics == null)
                return;

            // Use reflection to get and set values
            var physicsPresetField = torquePhysics.GetType().GetField("physicsPreset", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (physicsPresetField != null)
            {
                var physicsPreset = physicsPresetField.GetValue(torquePhysics) as TorquePlayerPhysicsPreset;

                if (physicsPreset != null)
                {
                    if (gravityModEnabled)
                    {
                        physicsPreset.gravity = customGravity;
                    }
                    else
                    {
                        physicsPreset.gravity = originalGravity;
                    }
                }
            }
        }

        private void ApplyCustomBounciness()
        {
            if (torquePhysics == null)
                return;

            // Use reflection to get and set values
            var physicsPresetField = torquePhysics.GetType().GetField("physicsPreset", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (physicsPresetField != null)
            {
                var physicsPreset = physicsPresetField.GetValue(torquePhysics) as TorquePlayerPhysicsPreset;

                if (physicsPreset != null)
                {
                    if (bouncinessModEnabled)
                    {
                        physicsPreset.groundedBounciness = customBounciness;
                        physicsPreset.airBounciness = customBounciness;
                    }
                    else
                    {
                        physicsPreset.groundedBounciness = originalGroundedBounciness;
                        physicsPreset.airBounciness = originalAirBounciness;
                    }
                }
            }
        }

        private void ApplyCustomBallRadius()
        {
            if (torquePhysics == null)
                return;

            // Use reflection to get and set values
            var physicsPresetField = torquePhysics.GetType().GetField("physicsPreset", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (physicsPresetField != null)
            {
                var physicsPreset = physicsPresetField.GetValue(torquePhysics) as TorquePlayerPhysicsPreset;

                if (physicsPreset != null)
                {
                    if (ballRadiusModEnabled)
                    {
                        physicsPreset.ballRadius = customBallRadius;

                        // Update the collider radius if we can access it
                        if (playerRigidbody != null)
                        {
                            SphereCollider sphereCollider = playerRigidbody.GetComponent<SphereCollider>();
                            if (sphereCollider != null)
                            {
                                sphereCollider.radius = customBallRadius;
                            }
                        }
                    }
                    else
                    {
                        physicsPreset.ballRadius = originalBallRadius;

                        // Reset collider radius
                        if (playerRigidbody != null)
                        {
                            SphereCollider sphereCollider = playerRigidbody.GetComponent<SphereCollider>();
                            if (sphereCollider != null)
                            {
                                sphereCollider.radius = originalBallRadius;
                            }
                        }
                    }
                }
            }
        }

        private void ApplyCustomMaxGravityAngle()
        {
            if (torquePhysics == null)
                return;

            // Use reflection to get and set values
            var physicsPresetField = torquePhysics.GetType().GetField("physicsPreset", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (physicsPresetField != null)
            {
                var physicsPreset = physicsPresetField.GetValue(torquePhysics) as TorquePlayerPhysicsPreset;

                if (physicsPreset != null)
                {
                    if (maxGravityAngleModEnabled)
                    {
                        physicsPreset.maxGravityAngle = customMaxGravityAngle;
                    }
                    else
                    {
                        physicsPreset.maxGravityAngle = originalMaxGravityAngle;
                    }
                }
            }
        }

        private void ApplyCustomMaxTorque()
        {
            if (torquePhysics == null)
                return;

            // Use reflection to get and set values
            var physicsPresetField = torquePhysics.GetType().GetField("physicsPreset", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (physicsPresetField != null)
            {
                var physicsPreset = physicsPresetField.GetValue(torquePhysics) as TorquePlayerPhysicsPreset;

                if (physicsPreset != null)
                {
                    if (maxTorqueModEnabled)
                    {
                        physicsPreset.maxTorque = customMaxTorque;
                    }
                    else
                    {
                        physicsPreset.maxTorque = originalMaxTorque;
                    }
                }
            }
        }

        private void ApplyCustomGrabAbilityDrag()
        {
            if (torquePhysics == null)
                return;

            // Use reflection to get and set values
            var physicsPresetField = torquePhysics.GetType().GetField("physicsPreset", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (physicsPresetField != null)
            {
                var physicsPreset = physicsPresetField.GetValue(torquePhysics) as TorquePlayerPhysicsPreset;

                if (physicsPreset != null)
                {
                    if (grabAbilityDragModEnabled)
                    {
                        physicsPreset.grabAbilityDrag = customGrabAbilityDrag;
                    }
                    else
                    {
                        physicsPreset.grabAbilityDrag = originalGrabAbilityDrag;
                    }
                }
            }
        }

        private void ApplyCustomAirBounciness()
        {
            if (torquePhysics == null)
                return;

            // Use reflection to get and set values
            var physicsPresetField = torquePhysics.GetType().GetField("physicsPreset", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (physicsPresetField != null)
            {
                var physicsPreset = physicsPresetField.GetValue(torquePhysics) as TorquePlayerPhysicsPreset;

                if (physicsPreset != null)
                {
                    if (airBouncinessModEnabled)
                    {
                        physicsPreset.airBounciness = customAirBounciness;
                    }
                    else
                    {
                        physicsPreset.airBounciness = originalAirBounciness;
                    }
                }
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

        // Add method to create textures
        private Texture2D CreateColorTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        // Add method to initialize textures
        private void InitializeTextures()
        {
            if (backgroundTexture == null)
            {
                backgroundTexture = CreateColorTexture(new Color(0.1f, 0.1f, 0.1f, 0.95f));
                buttonTexture = CreateColorTexture(new Color(0.3f, 0.5f, 0.8f, 1f)); // Blue button
                buttonHoverTexture = CreateColorTexture(new Color(0.4f, 0.6f, 0.9f, 1f)); // Lighter blue on hover
                textFieldTexture = CreateColorTexture(new Color(0.15f, 0.15f, 0.15f, 1f));
                headerTexture = CreateColorTexture(new Color(0.05f, 0.05f, 0.05f, 0.7f));
                selectedTexture = CreateColorTexture(new Color(0.4f, 0.6f, 0.9f, 0.8f));

                // New textures for enhanced UI
                alternateRowTexture = CreateColorTexture(new Color(0.12f, 0.12f, 0.12f, 0.5f));
                toggleOnTexture = CreateColorTexture(new Color(0.3f, 0.7f, 0.4f, 1f)); // Green for ON
                toggleOffTexture = CreateColorTexture(new Color(0.5f, 0.5f, 0.5f, 1f)); // Gray for OFF
                separatorTexture = CreateColorTexture(new Color(0.4f, 0.4f, 0.4f, 0.5f));
                textFieldBorderTexture = CreateColorTexture(new Color(0.4f, 0.4f, 0.4f, 1f));
                deleteButtonTexture = CreateColorTexture(new Color(0.8f, 0.2f, 0.2f, 1f)); // Red for delete
                deleteButtonHoverTexture = CreateColorTexture(new Color(0.9f, 0.3f, 0.3f, 1f)); // Brighter red on hover
            }
        }

        // Add navigation handling method
        private void HandleMenuNavigation()
        {
            if (navigationCooldown > 0)
            {
                navigationCooldown -= Time.deltaTime;
                return;
            }

            bool navigationInput = false;

            // Check for select key to activate selected element
            bool selectPressed = Input.GetKeyDown(KeyCode.Return) ||
                               Input.GetKeyDown(KeyCode.JoystickButton0);

            // Only respond to controller/keyboard input if a UI element is selected
            if (selectPressed && uiElements.Count > 0 &&
                (Input.GetAxis("Vertical") != 0 || Input.GetAxis("Horizontal") != 0))
            {
                // Find first interactable element to activate when using controller/keyboard
                foreach (var element in uiElements)
                {
                    if (element.action != null)
                    {
                        element.action();
                        navigationInput = true;
                        break;
                    }
                }
            }

            if (navigationInput)
            {
                navigationCooldown = NAVIGATION_COOLDOWN_TIME;
            }
        }

        private void ToggleMenu()
        {
            showMenu = !showMenu;

            if (showMenu)
            {
                // Remove: selectedElementIndex = 0;

                // Show cursor
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.Confined;
            }
            else
            {
                // Hide cursor when closing menu
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
            }
        }

        // Add this method after StoreOriginalValues()
        private void ForceExitRagdoll()
        {
            if (playerTransform == null || playerController == null) return;

            // Check if player is in ragdoll state and exit if needed
            if (playerController.IsRagdollState)
            {
                playerController.ChangeToMoveState();
            }
        }
    }
}