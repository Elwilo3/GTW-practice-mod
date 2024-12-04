using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System;

namespace SpeedrunPracticeMod
{
    [BepInPlugin("com.Palmblom.SpeedrunPracticeMod", "SpeedrunPracticeMod", "1.0.0")]
    public class PracticeMod : BaseUnityPlugin
    {
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

        // Control configurations
        private ConfigEntry<KeyCode> menuKey;
        private ConfigEntry<KeyCode> noclipKey;
        private ConfigEntry<KeyCode> teleportKey;
        private ConfigEntry<KeyCode> slowMotionKey;
        private bool showingControls = false;
        private KeyCode? waitingForKey = null;
        private string waitingForKeyBinding = "";
        private bool isSlowMotionActive = false;  // Flag to track slow-motion status
        private float normalTimeScale = 1f;  // Save the normal time scale


        // Category selection
        private enum MenuCategory { None, Noclip, Checkpoints, Controls }
        private MenuCategory currentCategory = MenuCategory.None;

        // Noclip variables
        private Rigidbody playerRigidbody;
        private Collider playerCollider;
        private float moveSpeed = 10f;
        private float fastMultiplier = 3f;
        private bool isNoclipActive = false;
        private bool keepNoclipAfterTeleport = false;
        private bool waitingForMovement = false;

        // Checkpoints
        private Dictionary<string, Vector3> customCheckpoints = new Dictionary<string, Vector3>();
        private Dictionary<string, Vector3> presetCheckpoints = new Dictionary<string, Vector3>()
        {
            { "Spawn", new Vector3(-390.3687f, 144.25f, 112.2642f) },
            { "Unpaid Intern", new Vector3(-170.3318f, 234.25f, 122.8188f) },
            { "Warehouse Worker", new Vector3(-208.2579f, 97.25f, 139.1872f) },
            { "Warehouse Trainee", new Vector3(-242.1583f, 83.25997f, 133.4567f) },
            { "Interview", new Vector3(-289.6071f, 195.25f, 82.85135f) },
            { "Junior Financial Analyst", new Vector3(-131.5249f, 304.6553f, 78.66881f) },
            { "Wine Mixer", new Vector3(-86.7677f, 405.25f, -24.72382f) },
            { "Middle Manger Interview", new Vector3(-99.91953f, 503.8748f, -14.55534f) },
            { "Middle Management", new Vector3(-92.28833f, 437.7774f, 172.3422f) },
            { "Leave Company", new Vector3(-190.7224f, 487.25f, 243.2324f) },
            { "Department Head", new Vector3(-156.9074f, 539.25f, 298.8533f) },
            { "Vice President", new Vector3(-95.37256f, 624.7501f, 315.2366f) },
            { "CEO", new Vector3(-79.30243f, 791.2501f, 313.3521f) }
        };

        private void Awake()
        {
            // Config setup
            menuKey = Config.Bind("Controls",
                                "MenuKey",
                                KeyCode.Tab,
                                "Key to toggle the practice menu");

            noclipKey = Config.Bind("Controls",
                                   "NoclipKey",
                                   KeyCode.N,
                                   "Key to toggle noclip mode");

            teleportKey = Config.Bind("Controls",
                                     "TeleportKey",
                                     KeyCode.O,
                                     "Key to teleport to selected checkpoint");
            slowMotionKey = Config.Bind("Controls",
                            "SlowMotionKey",
                            KeyCode.P,
                            "Key to toggle slow motion");


            saveFilePath = Path.Combine(Paths.ConfigPath, "customcheckpoints.txt");
            LoadCheckpoints();
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
                        if (parts.Length == 4)
                        {
                            string name = parts[0];
                            float x = float.Parse(parts[1]);
                            float y = float.Parse(parts[2]);
                            float z = float.Parse(parts[3]);
                            customCheckpoints[name] = new Vector3(x, y, z);
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to load checkpoints: {e.Message}");
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
                        writer.WriteLine($"{checkpoint.Key},{checkpoint.Value.x},{checkpoint.Value.y},{checkpoint.Value.z}");
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to save checkpoints: {e.Message}");
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

            if (waitingForMovement && isNoclipActive)
            {
                if (Input.GetAxisRaw("Horizontal") != 0 ||
                    Input.GetAxisRaw("Vertical") != 0 ||
                    Input.GetKey(KeyCode.Space) ||
                    Input.GetKey(KeyCode.LeftControl))
                {
                    DisableNoclip();
                }
            }

            if (Input.GetKeyDown(menuKey.Value))
            {
                showMenu = !showMenu;
                if (showMenu)
                {
                    Time.timeScale = 0f;
                    Cursor.visible = true;
                    Cursor.lockState = CursorLockMode.Confined;
                }
                else
                {
                    Time.timeScale = 1f;
                    Cursor.visible = false;
                    Cursor.lockState = CursorLockMode.Locked;
                }
            }

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

            if (Input.GetKeyDown(slowMotionKey.Value))
            {
                ToggleSlowMotion();
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

        private void EnableNoclip()
        {
            isNoclipActive = true;
            playerRigidbody.isKinematic = true;
            playerCollider.enabled = false;
        }

        private void DisableNoclip()
        {
            isNoclipActive = false;
            playerRigidbody.isKinematic = false;
            playerCollider.enabled = true;
            waitingForMovement = false;
        }

        private void UpdateNoclip()
        {
            float currentSpeed = Input.GetKey(KeyCode.LeftShift) ? moveSpeed * fastMultiplier : moveSpeed;

            Vector3 movement = Vector3.zero;
            movement += Camera.main.transform.forward * Input.GetAxis("Vertical");
            movement += Camera.main.transform.right * Input.GetAxis("Horizontal");

            if (Input.GetKey(KeyCode.Space)) movement.y += 1f;
            if (Input.GetKey(KeyCode.LeftControl)) movement.y -= 1f;

            playerTransform.position += movement * currentSpeed * Time.deltaTime;
        }

        private void ToggleSlowMotion()
        {
            isSlowMotionActive = !isSlowMotionActive;

            // Toggle time scale for slow motion
            Time.timeScale = isSlowMotionActive ? 0.5f : normalTimeScale;  // 0.5f is a slower speed
            Time.fixedDeltaTime = 0.02f * Time.timeScale;  // Ensure physics update frequency scales accordingly

            ShowNotification(isSlowMotionActive ? "Slow Motion Enabled" : "Slow Motion Disabled");
        }


        private void ToggleNoclip()
        {
            isNoclipActive = !isNoclipActive;
            playerRigidbody.isKinematic = isNoclipActive;
            playerCollider.enabled = !isNoclipActive;
            ShowNotification(isNoclipActive ? "Noclip Enabled" : "Noclip Disabled");
        }

        private void TeleportToPresetCheckpoint(string checkpointName)
        {
            if (presetCheckpoints.TryGetValue(checkpointName, out Vector3 position))
            {
                playerTransform.position = position;
                ShowNotification($"Teleported to {checkpointName}");

                if (keepNoclipAfterTeleport)
                {
                    EnableNoclip();
                    waitingForMovement = true;
                }
            }
        }

        private void TeleportToCustomCheckpoint(string checkpointName)
        {
            if (customCheckpoints.TryGetValue(checkpointName, out Vector3 position))
            {
                playerTransform.position = position;
                ShowNotification($"Teleported to {checkpointName}");

                if (keepNoclipAfterTeleport)
                {
                    EnableNoclip();
                    waitingForMovement = true;
                }
            }
        }

        private void ShowNotification(string text)
        {
            notificationText = text;
            notificationTime = Time.unscaledTime + 2f;
        }

        void OnDisable()
        {
            if (isNoclipActive)
            {
                isNoclipActive = false;
                if (playerRigidbody != null) playerRigidbody.isKinematic = false;
                if (playerCollider != null) playerCollider.enabled = true;
            }
            Time.timeScale = 1f;
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
        private void OnGUI()
        {
            if (Time.unscaledTime < notificationTime)
            {
                GUI.Label(new Rect(Screen.width / 2 - 100, 20, 200, 30), notificationText);
            }

            GUI.Label(new Rect(Screen.width - 120, Screen.height - 25, 110, 20), "Practice Mod");

            if (!showMenu) return;

            float menuX = Screen.width - 310;
            GUI.Box(new Rect(menuX, 10, 300, 450), "Practice Menu");

            // Category Selection
            if (GUI.Button(new Rect(menuX + 20, 40, 85, 30), "Noclip"))
            {
                currentCategory = currentCategory == MenuCategory.Noclip ? MenuCategory.None : MenuCategory.Noclip;
            }
            if (GUI.Button(new Rect(menuX + 110, 40, 85, 30), "Checkpoints"))
            {
                currentCategory = currentCategory == MenuCategory.Checkpoints ? MenuCategory.None : MenuCategory.Checkpoints;
            }
            if (GUI.Button(new Rect(menuX + 200, 40, 85, 30), "Controls"))
            {
                currentCategory = currentCategory == MenuCategory.Controls ? MenuCategory.None : MenuCategory.Controls;
            }

            // Category specific menus
            if (currentCategory == MenuCategory.Controls)
            {
                GUI.Box(new Rect(menuX + 20, 80, 260, 160), "Controls");

                if (waitingForKey.HasValue)
                {
                    GUI.Label(new Rect(menuX + 30, 100, 240, 30), $"Press any key for: {waitingForKeyBinding}");
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
                        }
                        waitingForKey = null;
                        waitingForKeyBinding = "";
                    }
                }
                else
                {
                    if (GUI.Button(new Rect(menuX + 30, 100, 240, 25), $"Menu Key: {menuKey.Value}"))
                    {
                        waitingForKey = menuKey.Value;
                        waitingForKeyBinding = "Menu";
                    }
                    if (GUI.Button(new Rect(menuX + 30, 190, 240, 30), isSlowMotionActive ? "Disable Slow Motion" : "Enable Slow Motion"))
                    {
                        ToggleSlowMotion();
                    }
                    if (GUI.Button(new Rect(menuX + 30, 130, 240, 25), $"Noclip Key: {noclipKey.Value}"))
                    {
                        waitingForKey = noclipKey.Value;
                        waitingForKeyBinding = "Noclip";
                    }
                    if (GUI.Button(new Rect(menuX + 30, 160, 240, 25), $"Teleport Key: {teleportKey.Value}"))
                    {
                        waitingForKey = teleportKey.Value;
                        waitingForKeyBinding = "Teleport";
                    }
                }
            }
            else if (currentCategory == MenuCategory.Noclip)
            {
                GUI.Box(new Rect(menuX + 20, 80, 260, 120), "Noclip Settings");

                GUI.Label(new Rect(menuX + 30, 100, 100, 20), "Speed: " + moveSpeed.ToString("F1"));
                moveSpeed = GUI.HorizontalSlider(new Rect(menuX + 30, 120, 240, 20), moveSpeed, 1f, 50f);

                GUI.Label(new Rect(menuX + 30, 140, 100, 20), "Fast Multiplier: " + fastMultiplier.ToString("F1"));
                fastMultiplier = GUI.HorizontalSlider(new Rect(menuX + 30, 160, 240, 20), fastMultiplier, 1f, 10f);

                if (GUI.Button(new Rect(menuX + 30, 180, 240, 30), isNoclipActive ? "Disable Noclip" : "Enable Noclip"))
                {
                    ToggleNoclip();
                }
            }
            else if (currentCategory == MenuCategory.Checkpoints)
            {
                // Checkpoint Settings Section
                GUI.Box(new Rect(menuX + 20, 80, 260, 60), "Checkpoint Settings");
                keepNoclipAfterTeleport = GUI.Toggle(
                    new Rect(menuX + 30, 100, 260, 20),
                    keepNoclipAfterTeleport,
                    "Keep Noclip After Teleport (until movement)"
                );

                // Toggle between Preset and Custom Checkpoints
                if (GUI.Button(new Rect(menuX + 20, 150, 260, 30),
                    showingPresets ? "Show Custom Checkpoints" : "Show Preset Locations"))
                {
                    showingPresets = !showingPresets;
                    selectedCheckpoint = "";
                    scrollPosition = Vector2.zero;
                }

                if (showingPresets)
                {
                    DrawPresetCheckpoints(menuX);
                }
                else
                {
                    DrawCustomCheckpoints(menuX);
                }

                // Selected Checkpoint Display
                if (!string.IsNullOrEmpty(selectedCheckpoint))
                {
                    DrawSelectedCheckpoint(menuX);
                }

                // Help Text
                GUI.Box(new Rect(menuX + 20, 540, 260, 40), "");
                GUI.Label(new Rect(menuX + 30, 550, 240, 30),
                    "Ctrl + Click to quick teleport\n" +
                    $"Press {teleportKey.Value} to teleport to selected checkpoint"
                );
            }
        }

        private void DrawPresetCheckpoints(float menuX)
        {
            GUI.Box(new Rect(menuX + 20, 190, 260, 280), "Preset Locations");
            scrollPosition = GUI.BeginScrollView(
                new Rect(menuX + 30, 210, 240, 250),
                scrollPosition,
                new Rect(0, 0, 220, presetCheckpoints.Count * 25)
            );

            int index = 0;
            foreach (var checkpoint in presetCheckpoints)
            {
                if (GUI.Button(new Rect(0, index * 25, 220, 23), checkpoint.Key))
                {
                    selectedCheckpoint = checkpoint.Key;
                    if (Input.GetKey(KeyCode.LeftControl))
                    {
                        TeleportToPresetCheckpoint(selectedCheckpoint);
                    }
                }
                index++;
            }
            GUI.EndScrollView();
        }

        private void DrawCustomCheckpoints(float menuX)
        {
            GUI.Box(new Rect(menuX + 20, 190, 260, 280), "Custom Checkpoints");

            GUI.Label(new Rect(menuX + 30, 210, 100, 20), "New Checkpoint:");
            newCheckpointName = GUI.TextField(new Rect(menuX + 30, 230, 200, 20), newCheckpointName);

            if (GUI.Button(new Rect(menuX + 30, 255, 200, 25), "Create Checkpoint Here"))
            {
                if (!string.IsNullOrEmpty(newCheckpointName))
                {
                    if (playerTransform != null)
                    {
                        customCheckpoints[newCheckpointName] = playerTransform.position;
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

            GUI.Label(new Rect(menuX + 30, 290, 200, 20), "Saved Checkpoints:");
            scrollPosition = GUI.BeginScrollView(
                new Rect(menuX + 30, 310, 240, 150),
                scrollPosition,
                new Rect(0, 0, 220, customCheckpoints.Count * 25)
            );

            int index = 0;
            List<string> checkpointsToRemove = new List<string>();

            foreach (var checkpoint in customCheckpoints)
            {
                if (GUI.Button(new Rect(0, index * 25, 190, 23), checkpoint.Key))
                {
                    selectedCheckpoint = checkpoint.Key;
                    if (Input.GetKey(KeyCode.LeftControl))
                    {
                        TeleportToCustomCheckpoint(selectedCheckpoint);
                    }
                }

                if (GUI.Button(new Rect(195, index * 25, 25, 23), "X"))
                {
                    checkpointsToRemove.Add(checkpoint.Key);
                }
                index++;
            }
            GUI.EndScrollView();

            foreach (string checkpointName in checkpointsToRemove)
            {
                customCheckpoints.Remove(checkpointName);
                if (selectedCheckpoint == checkpointName)
                    selectedCheckpoint = "";
                SaveCheckpoints();
                ShowNotification($"Deleted checkpoint: {checkpointName}");
            }
        }

        private void DrawSelectedCheckpoint(float menuX)
        {
            GUI.Box(new Rect(menuX + 20, 480, 260, 50), "Selected Checkpoint");
            GUI.Label(new Rect(menuX + 30, 495, 240, 20),
                $"Selected: {selectedCheckpoint}");

            if (GUI.Button(new Rect(menuX + 30, 515, 240, 25), "Teleport"))
            {
                if (showingPresets)
                    TeleportToPresetCheckpoint(selectedCheckpoint);
                else
                    TeleportToCustomCheckpoint(selectedCheckpoint);
            }
        }
    }
}