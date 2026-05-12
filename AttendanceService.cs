using System;
using System.ServiceProcess;

namespace SamanDeviceAdapter
{
    /// <summary>
    /// Windows Service implementation.
    /// Orchestrates the SyncWorker and WebDashboard.
    /// </summary>
    public partial class AttendanceService : ServiceBase
    {
        private SyncWorker _syncWorker;
        private WebDashboard _webDashboard;
        private SettingsManager _settingsManager;

        public AttendanceService()
        {
            ServiceName = Constants.ServiceName;
            DisplayName = Constants.ServiceDisplayName;
            CanStop = true;
            CanPauseAndContinue = false;
            CanHandleSessionChangeEvent = false;
            AutoLog = true;
        }

        /// <summary>
        /// Called when the service starts.
        /// </summary>
        protected override void OnStart(string[] args)
        {
            try
            {
                ServiceLogger.LogInfo("AttendanceService", "Service starting...");

                // Initialize settings manager
                _settingsManager = new SettingsManager();
                var settings = _settingsManager.GetSettings();

                // Initialize and start sync worker
                _syncWorker = new SyncWorker(_settingsManager);
                _syncWorker.Start();

                // Initialize and start web dashboard
                _webDashboard = new WebDashboard(settings.DashboardPort, _syncWorker, _settingsManager);
                _webDashboard.Start();

                ServiceLogger.LogInfo("AttendanceService", "Service started successfully");
            }
            catch (Exception ex)
            {
                ServiceLogger.LogException("AttendanceService", ex);
                throw;
            }
        }

        /// <summary>
        /// Called when the service stops.
        /// </summary>
        protected override void OnStop()
        {
            try
            {
                ServiceLogger.LogInfo("AttendanceService", "Service stopping...");

                // Stop web dashboard
                if (_webDashboard != null)
                {
                    _webDashboard.Stop();
                    _webDashboard.Dispose();
                    _webDashboard = null;
                }

                // Stop sync worker
                if (_syncWorker != null)
                {
                    _syncWorker.Stop();
                    _syncWorker.Dispose();
                    _syncWorker = null;
                }

                ServiceLogger.LogInfo("AttendanceService", "Service stopped successfully");
            }
            catch (Exception ex)
            {
                ServiceLogger.LogException("AttendanceService", ex);
            }
        }
    }
}
