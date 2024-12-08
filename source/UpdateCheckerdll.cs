using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using BepInEx;
using BepInEx.Configuration;

namespace PalmblomUpdateChecker
{
    [BepInPlugin("com.elwilo3.updatechecker", "GTW Practice Mod Update Checker", "1.0.0")]
    public class UpdateCheckerPlugin : BaseUnityPlugin
    {
        private const string GITHUB_API_URL = "https://api.github.com/repos/Elwilo3/GTW-practice-mod/releases/latest";
        private const string GITHUB_REPO_URL = "https://github.com/Elwilo3/GTW-practice-mod"; // Added direct repo URL
        private const string CURRENT_VERSION = "1.5.0";
        private static bool updateAvailable = false;
        private static string latestVersion = "";
        private static DateTime lastCheck = DateTime.MinValue;
        private static readonly TimeSpan CHECK_COOLDOWN = TimeSpan.FromHours(1);

        // Config entry for dismissed state
        private ConfigEntry<bool> updateDismissed;

        // Notification properties
        private bool isNotificationVisible = false;
        private float notificationTimer = 0f;
        private const float NOTIFICATION_DURATION = 15f;
        private Rect notificationRect = new Rect(10, 10, 240, 100);
        private bool isHovered = false;

        private void Awake()
        {
            // Initialize config
            updateDismissed = Config.Bind("General", "UpdateDismissed", false, "Whether the update notification has been dismissed");

            Logger.LogInfo($"GTW Practice Mod Update Checker is loaded!");
            _ = CheckForUpdates();
        }

        private void Update()
        {
            if (updateAvailable && !updateDismissed.Value)
            {
                if (notificationTimer < NOTIFICATION_DURATION)
                {
                    notificationTimer += Time.deltaTime;
                    isNotificationVisible = true;
                }
                else if (!isHovered)
                {
                    isNotificationVisible = false;
                }
            }
        }

        private void OnGUI()
        {
            if (updateAvailable && isNotificationVisible && !updateDismissed.Value)
            {
                // Draw notification box
                GUI.Box(notificationRect, "");

                // Check if mouse is over notification
                isHovered = notificationRect.Contains(Event.current.mousePosition);

                // Reset timer while hovered
                if (isHovered)
                {
                    notificationTimer = 0f;
                }

                // Close button
                if (GUI.Button(new Rect(notificationRect.x + notificationRect.width - 25, notificationRect.y + 5, 20, 20), "×"))
                {
                    isNotificationVisible = false;
                    updateDismissed.Value = true;
                    return;
                }

                GUILayout.BeginArea(notificationRect);
                GUILayout.BeginVertical();

                GUILayout.Space(5);
                GUILayout.Label("<color=yellow>GTW Practice Mod Update Available!</color>", new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    richText = true,
                    fontSize = 12,
                    fontStyle = FontStyle.Bold
                });

                GUILayout.Label($"Current: v{CURRENT_VERSION}\nLatest: v{latestVersion}", new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 11
                });

                GUILayout.Space(5);
                if (GUILayout.Button("Download Update", GUILayout.Height(25)))
                {
                    Application.OpenURL(GITHUB_REPO_URL); // Changed to use main repo URL
                    updateDismissed.Value = true;
                }

                GUILayout.EndVertical();
                GUILayout.EndArea();
            }
        }

        private async Task CheckForUpdates()
        {
            try
            {
                lastCheck = DateTime.Now;
                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "GTW-Practice-Mod-UpdateChecker");

                    string response = await client.GetStringAsync(GITHUB_API_URL);
                    JObject json = JObject.Parse(response);

                    latestVersion = json["tag_name"].ToString().Replace("v", "");

                    // Compare versions
                    Version current = new Version(CURRENT_VERSION);
                    Version latest = new Version(latestVersion);

                    updateAvailable = latest > current;

                    if (updateAvailable)
                    {
                        notificationTimer = 0f;
                        isNotificationVisible = true;
                        updateDismissed.Value = false; // Reset dismissed state for new updates
                        Logger.LogInfo($"New update available! Current: v{CURRENT_VERSION}, Latest: v{latestVersion}");
                    }
                    else
                    {
                        Logger.LogInfo($"No updates available. Current: v{CURRENT_VERSION}, Latest: v{latestVersion}");
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed to check for updates: {e.Message}");
            }
        }
    }
}