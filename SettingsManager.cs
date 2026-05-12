using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;

namespace SamanDeviceAdapter
{
    /// <summary>
    /// Manages application settings persistence via JSON file.
    /// Settings are stored in the application directory as settings.json.
    /// </summary>
    public class SettingsManager
    {
        private readonly string _settingsPath;
        private Settings _currentSettings;

        public SettingsManager()
        {
            // Get the application directory
            string appDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            _settingsPath = Path.Combine(appDirectory, Constants.SettingsFileName);
            
            // Load or create default settings
            LoadSettings();
        }

        /// <summary>
        /// Gets the current settings object.
        /// </summary>
        public Settings GetSettings()
        {
            return _currentSettings;
        }

        /// <summary>
        /// Loads settings from the JSON file, or creates default if not found.
        /// </summary>
        private void LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    string json = File.ReadAllText(_settingsPath);
                    _currentSettings = JsonConvert.DeserializeObject<Settings>(json);
                    
                    // Ensure all properties have valid defaults
                    if (_currentSettings == null)
                    {
                        _currentSettings = new Settings();
                    }
                    
                    // Validate and set defaults for missing values
                    if (string.IsNullOrWhiteSpace(_currentSettings.DeviceIp))
                        _currentSettings.DeviceIp = "192.168.1.201";
                    if (_currentSettings.DevicePort <= 0)
                        _currentSettings.DevicePort = Constants.ZktecoDefaultPort;
                    if (_currentSettings.DashboardPort <= 0)
                        _currentSettings.DashboardPort = Constants.DefaultDashboardPort;
                }
                else
                {
                    // Create default settings
                    _currentSettings = new Settings
                    {
                        DeviceIp = "192.168.1.201",
                        DevicePort = Constants.ZktecoDefaultPort,
                        CompanyId = "",
                        DeleteAfterSend = false,
                        DashboardPort = Constants.DefaultDashboardPort
                    };
                    SaveSettings();
                }
            }
            catch (Exception ex)
            {
                // If loading fails, create default settings
                ServiceLogger.LogError("SettingsManager", "Failed to load settings: " + ex.Message);
                _currentSettings = new Settings
                {
                    DeviceIp = "192.168.1.201",
                    DevicePort = Constants.ZktecoDefaultPort,
                    CompanyId = "",
                    DeleteAfterSend = false,
                    DashboardPort = Constants.DefaultDashboardPort
                };
            }
        }

        /// <summary>
        /// Saves the current settings to the JSON file.
        /// </summary>
        public void SaveSettings()
        {
            try
            {
                string json = JsonConvert.SerializeObject(_currentSettings, Formatting.Indented);
                File.WriteAllText(_settingsPath, json);
            }
            catch (Exception ex)
            {
                ServiceLogger.LogError("SettingsManager", "Failed to save settings: " + ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Updates a specific setting and saves.
        /// </summary>
        public void UpdateSetting(string key, object value)
        {
            try
            {
                var property = typeof(Settings).GetProperty(key);
                if (property != null && property.CanWrite)
                {
                    property.SetValue(_currentSettings, Convert.ChangeType(value, property.PropertyType));
                    SaveSettings();
                }
            }
            catch (Exception ex)
            {
                ServiceLogger.LogError("SettingsManager", $"Failed to update setting {key}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Reload settings from disk (useful if externally modified).
        /// </summary>
        public void Reload()
        {
            LoadSettings();
        }
    }

    /// <summary>
    /// Represents the application settings model.
    /// Serialized to/from JSON.
    /// </summary>
    public class Settings
    {
        [JsonProperty("DeviceIp")]
        public string DeviceIp { get; set; } = "192.168.1.201";

        [JsonProperty("DevicePort")]
        public int DevicePort { get; set; } = 4370;

        [JsonProperty("CompanyId")]
        public string CompanyId { get; set; } = "";

        [JsonProperty("DeleteAfterSend")]
        public bool DeleteAfterSend { get; set; } = false;

        [JsonProperty("DashboardPort")]
        public int DashboardPort { get; set; } = 8080;
    }
}
