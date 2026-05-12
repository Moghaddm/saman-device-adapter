using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Timers;
using Newtonsoft.Json;

namespace SamanDeviceAdapter
{
    /// <summary>
    /// Background worker that syncs attendance data from ZKTeco device to HRM API.
    /// Runs on a timer, connecting to the device, fetching records, and pushing to the HRM server.
    /// </summary>
    public class SyncWorker : IDisposable
    {
        private Timer _syncTimer;
        private readonly SettingsManager _settingsManager;
        private readonly HttpClient _httpClient;
        private ZktecoClient _zktecoClient;

        // State variables that are read by the dashboard
        public bool IsServiceRunning { get; private set; }
        public bool IsDeviceConnected { get; private set; }
        public DateTime LastSyncTime { get; private set; }

        // Sync history (last 20 actions)
        private readonly List<SyncLog> _syncLogs = new List<SyncLog>();
        private readonly object _syncLogLock = new object();

        public SyncWorker(SettingsManager settingsManager)
        {
            _settingsManager = settingsManager;
            _httpClient = new HttpClient();
            IsServiceRunning = false;
            IsDeviceConnected = false;
            LastSyncTime = DateTime.MinValue;
        }

        /// <summary>
        /// Starts the sync worker timer.
        /// </summary>
        public void Start()
        {
            try
            {
                if (_syncTimer != null)
                {
                    return;
                }

                _syncTimer = new Timer(Constants.DefaultSyncIntervalMs);
                _syncTimer.Elapsed += OnSyncTimerElapsed;
                _syncTimer.AutoReset = true;
                _syncTimer.Enabled = true;

                IsServiceRunning = true;
                ServiceLogger.LogInfo("SyncWorker", "Sync worker started");
                AddSyncLog(true, "Sync worker started successfully");
            }
            catch (Exception ex)
            {
                ServiceLogger.LogException("SyncWorker", ex);
                AddSyncLog(false, "Failed to start sync worker: " + ex.Message);
            }
        }

        /// <summary>
        /// Stops the sync worker timer.
        /// </summary>
        public void Stop()
        {
            try
            {
                if (_syncTimer != null)
                {
                    _syncTimer.Enabled = false;
                    _syncTimer.Dispose();
                    _syncTimer = null;
                }

                if (_zktecoClient != null)
                {
                    _zktecoClient.Disconnect();
                    _zktecoClient.Dispose();
                    _zktecoClient = null;
                }

                IsServiceRunning = false;
                IsDeviceConnected = false;
                ServiceLogger.LogInfo("SyncWorker", "Sync worker stopped");
            }
            catch (Exception ex)
            {
                ServiceLogger.LogException("SyncWorker", ex);
            }
        }

        /// <summary>
        /// Gets a copy of the current sync logs.
        /// </summary>
        public List<SyncLog> GetSyncLogs()
        {
            lock (_syncLogLock)
            {
                return _syncLogs.OrderByDescending(x => x.Timestamp).ToList();
            }
        }

        /// <summary>
        /// Timer callback - runs every 60 seconds to sync data.
        /// </summary>
        private void OnSyncTimerElapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                _syncTimer.Enabled = false; // Prevent overlapping executions

                PerformSync();
            }
            catch (Exception ex)
            {
                ServiceLogger.LogException("SyncWorker", ex);
                AddSyncLog(false, "Sync error: " + ex.Message);
            }
            finally
            {
                _syncTimer.Enabled = true;
            }
        }

        /// <summary>
        /// Performs the actual sync operation.
        /// </summary>
        private void PerformSync()
        {
            var settings = _settingsManager.GetSettings();

            // Step 1: Connect to ZKTeco device
            if (_zktecoClient == null)
            {
                _zktecoClient = new ZktecoClient(settings.DeviceIp, settings.DevicePort);
            }

            bool deviceConnected = _zktecoClient.Connect();
            IsDeviceConnected = deviceConnected;

            if (!deviceConnected)
            {
                AddSyncLog(false, $"Failed to connect to device at {settings.DeviceIp}:{settings.DevicePort}");
                ServiceLogger.LogWarning("SyncWorker", "Device connection failed");
                return;
            }

            AddSyncLog(true, $"Connected to device at {settings.DeviceIp}:{settings.DevicePort}");

            try
            {
                // Step 2: Fetch attendance records
                var records = _zktecoClient.FetchAttendanceRecords();

                if (records.Count == 0)
                {
                    ServiceLogger.LogInfo("SyncWorker", "No new attendance records");
                    LastSyncTime = DateTime.Now;
                    return;
                }

                AddSyncLog(true, $"Fetched {records.Count} attendance records from device");
                ServiceLogger.LogInfo("SyncWorker", $"Fetched {records.Count} records");

                // Step 3: Push to HRM API
                bool pushSuccess = PushToHrmApi(records, settings);

                if (pushSuccess)
                {
                    AddSyncLog(true, $"Successfully sent {records.Count} records to HRM API");
                    ServiceLogger.LogInfo("SyncWorker", "Data pushed to HRM API successfully");

                    // Step 4: Clear device records if setting is enabled
                    if (settings.DeleteAfterSend)
                    {
                        bool cleared = _zktecoClient.ClearAttendanceRecords();
                        if (cleared)
                        {
                            AddSyncLog(true, "Cleared attendance records from device");
                            ServiceLogger.LogInfo("SyncWorker", "Device records cleared");
                        }
                        else
                        {
                            AddSyncLog(false, "Failed to clear records from device");
                            ServiceLogger.LogWarning("SyncWorker", "Failed to clear device records");
                        }
                    }
                }
                else
                {
                    AddSyncLog(false, "Failed to push records to HRM API");
                    ServiceLogger.LogWarning("SyncWorker", "HRM API push failed");
                }

                LastSyncTime = DateTime.Now;
            }
            catch (Exception ex)
            {
                ServiceLogger.LogException("SyncWorker", ex);
                AddSyncLog(false, "Sync error: " + ex.Message);
            }
            finally
            {
                // Always try to disconnect cleanly
                if (_zktecoClient != null)
                {
                    _zktecoClient.Disconnect();
                }
            }
        }

        /// <summary>
        /// Pushes attendance records to the HRM API.
        /// </summary>
        private bool PushToHrmApi(List<AttendanceRecord> records, Settings settings)
        {
            try
            {
                var payload = new
                {
                    companyId = settings.CompanyId,
                    records = records.Select(r => new
                    {
                        employeeId = r.EmployeeId,
                        timestamp = r.Timestamp,
                        status = r.Status.ToString(),
                        verifyMode = r.VerifyMode
                    }).ToList()
                };

                string json = JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                string url = Constants.HrmApiBaseUrl + Constants.AttendanceImportEndpoint;
                var response = _httpClient.PostAsync(url, content).Result;

                if (response.IsSuccessStatusCode)
                {
                    return true;
                }

                ServiceLogger.LogWarning("SyncWorker", $"HRM API returned status {response.StatusCode}");
                return false;
            }
            catch (Exception ex)
            {
                ServiceLogger.LogException("SyncWorker", ex);
                return false;
            }
        }

        /// <summary>
        /// Adds a log entry to the sync history.
        /// Keeps only the last 20 entries.
        /// </summary>
        private void AddSyncLog(bool success, string message)
        {
            lock (_syncLogLock)
            {
                _syncLogs.Add(new SyncLog
                {
                    Timestamp = DateTime.Now,
                    Success = success,
                    Message = message
                });

                // Keep only the last 20 entries
                if (_syncLogs.Count > Constants.MaxSyncLogsToKeep)
                {
                    _syncLogs.RemoveAt(0);
                }
            }
        }

        public void Dispose()
        {
            Stop();
            _httpClient?.Dispose();
        }
    }

    /// <summary>
    /// Represents a single sync operation log entry.
    /// </summary>
    public class SyncLog
    {
        public DateTime Timestamp { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
    }
}
