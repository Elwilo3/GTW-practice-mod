using BepInEx;
using UnityEngine;
using System.Collections.Generic;
using System.IO;

namespace TeleportMod
{
    [BepInPlugin("com.Palmblom.SpeedrunPracticeMod", "SpeedrunPracticeMod", "1.0.0")]
    public class TeleportPlugin : BaseUnityPlugin
    {
        private bool showMenu = false;
        private string teleportDistance = "10";
        private float notificationTime = 0f;
        private string notificationText = "";
        private bool isTeleporting = false;
        private float teleportStartTime;
        private Vector3 startPosition;
        private Vector3 targetPosition;
        private Transform playerTransform;
        private float teleportDuration = 1.5f;
        private bool showingPresets = false;
        private Vector2 scrollPosition = Vector2.zero;
        private string selectedCheckpoint = "";
        private string newCheckpointName = "";
        private Dictionary<string, Vector3> customCheckpoints = new Dictionary<string, Vector3>();
        private string saveFilePath;

        private Dictionary<string, Vector3> presetCheckpoints = new Dictionary<string, Vector3>()
        // Add more locations here if you want
        {
            { "Unpaid Intern", new Vector3(-170.3318f, 234.25f, 122.8188f) },
            { "Warehouse Worker", new Vector3(-208.2579f, 97.25f, 139.1872f) },
            { "Warehouse Trainee", new Vector3(-242.1583f, 83.25997f, 133.4567f) },
            { "Interview", new Vector3(-289.6071f, 195.25f, 82.85135f) },
            { "Spawn", new Vector3(-390.3687f, 144.25f, 112.2642f) },
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
            saveFilePath = Path.Combine(Paths.ConfigPath, "customcheckpoints.txt");
            LoadCheckpoints();
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

        void Update()
        {
            playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (playerTransform == null) return;
            //GetKeyDown is for keypresses edit if you want to swtich keybinds
            if (Input.GetKeyDown(KeyCode.Tab))
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
            //GetKeyDown is for keypresses edit if you want to swtich keybinds
            if (Input.GetKeyDown(KeyCode.T) && !showMenu && !isTeleporting)
            {
                StartTeleport(playerTransform);
            }
            //GetKeyDown is for keypresses edit if you want to swtich keybinds
            if (Input.GetKeyDown(KeyCode.O) && !string.IsNullOrEmpty(selectedCheckpoint))
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

            if (isTeleporting)
            {
                float timeSinceStart = Time.unscaledTime - teleportStartTime;
                float progress = timeSinceStart / teleportDuration;

                if (progress >= 1f)
                {
                    playerTransform.position = targetPosition;
                    isTeleporting = false;
                    ShowNotification("Teleport complete!");
                }
                else
                {
                    playerTransform.position = Vector3.Lerp(startPosition, targetPosition, progress);
                }
            }
        }

        private void StartTeleport(Transform playerTransform)
        {
            if (float.TryParse(teleportDistance, out float distance))
            {
                Camera mainCamera = Camera.main;
                if (mainCamera != null)
                {
                    Vector3 teleportDirection = mainCamera.transform.forward;
                    startPosition = playerTransform.position;
                    targetPosition = startPosition + (teleportDirection * distance);
                    teleportStartTime = Time.unscaledTime;
                    isTeleporting = true;
                    ShowNotification($"Teleporting {distance} units forward...");
                }
            }
            else
            {
                ShowNotification("Invalid distance value!");
            }
        }

        private void TeleportToPresetCheckpoint(string checkpointName)
        {
            if (presetCheckpoints.TryGetValue(checkpointName, out Vector3 position))
            {
                playerTransform.position = position;
                ShowNotification($"Teleported to {checkpointName}");
            }
        }

        private void TeleportToCustomCheckpoint(string checkpointName)
        {
            if (customCheckpoints.TryGetValue(checkpointName, out Vector3 position))
            {
                playerTransform.position = position;
                ShowNotification($"Teleported to {checkpointName}");
            }
        }

        private void OnGUI()
        {
            if (Time.unscaledTime < notificationTime)
            {
                GUI.Label(new Rect(Screen.width / 2 - 100, 20, 200, 30), notificationText);
            }

            // Watermark
            GUI.Label(new Rect(Screen.width - 120, Screen.height - 25, 110, 20), "Modded practice!");

            if (!showMenu) return;

            float menuX = Screen.width - 310;
            GUI.Box(new Rect(menuX, 10, 300, 450), "Teleport Menu");

            // Distance input at top
            GUI.Label(new Rect(menuX + 20, 30, 100, 20), "Distance:");
            teleportDistance = GUI.TextField(new Rect(menuX + 20, 50, 200, 20), teleportDistance);

            // Toggle button for presets
            if (GUI.Button(new Rect(menuX + 20, 80, 200, 30), showingPresets ? "Show Custom Checkpoints" : "Show Preset Locations"))
            {
                showingPresets = !showingPresets;
                selectedCheckpoint = "";
            }

            if (showingPresets)
            {
                // Preset checkpoints list
                GUI.Label(new Rect(menuX + 20, 120, 200, 20), "Preset Locations:");
                scrollPosition = GUI.BeginScrollView(
                    new Rect(menuX + 20, 140, 260, 200),
                    scrollPosition,
                    new Rect(0, 0, 240, presetCheckpoints.Count * 25)
                );

                int index = 0;
                foreach (var checkpoint in presetCheckpoints)
                {
                    if (GUI.Button(new Rect(0, index * 25, 240, 23), checkpoint.Key))
                    {
                        selectedCheckpoint = checkpoint.Key;
                    }
                    index++;
                }
                GUI.EndScrollView();
            }
            else
            {
                // Custom checkpoints interface
                GUI.Label(new Rect(menuX + 20, 120, 100, 20), "New Checkpoint:");
                newCheckpointName = GUI.TextField(new Rect(menuX + 20, 140, 200, 20), newCheckpointName);

                if (GUI.Button(new Rect(menuX + 20, 165, 200, 25), "Create Checkpoint Here"))
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
                }

                // Custom checkpoints list
                GUI.Label(new Rect(menuX + 20, 195, 200, 20), "Custom Checkpoints:");
                scrollPosition = GUI.BeginScrollView(
                    new Rect(menuX + 20, 215, 260, 125),
                    scrollPosition,
                    new Rect(0, 0, 240, customCheckpoints.Count * 25)
                );

                int index = 0;
                foreach (var checkpoint in customCheckpoints)
                {
                    if (GUI.Button(new Rect(0, index * 25, 200, 23), checkpoint.Key))
                    {
                        selectedCheckpoint = checkpoint.Key;
                    }

                    if (GUI.Button(new Rect(210, index * 25, 30, 23), "X"))
                    {
                        customCheckpoints.Remove(checkpoint.Key);
                        if (selectedCheckpoint == checkpoint.Key)
                            selectedCheckpoint = "";
                        SaveCheckpoints();
                    }
                    index++;
                }
                GUI.EndScrollView();
            }

            // Selected checkpoint display
            if (!string.IsNullOrEmpty(selectedCheckpoint))
            {
                GUI.Label(new Rect(menuX + 20, 350, 260, 20), $"Selected: {selectedCheckpoint}");
            }

            GUI.Box(new Rect(menuX + 10, 380, 280, 60), "");
            GUI.Label(new Rect(menuX + 20, 385, 260, 50),
                "Press O to teleport to selected checkpoint\n" +
                "Press T to teleport forward with set distance" 
            );
        }

        private void ShowNotification(string text)
        {
            notificationText = text;
            notificationTime = Time.unscaledTime + 2f;
        }

        void OnDisable()
        {
            Time.timeScale = 1f;
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
    }
}