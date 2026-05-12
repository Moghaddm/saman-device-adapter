using System;

namespace SamanDeviceAdapter
{
    /// <summary>
    /// Hardcoded constants for the application.
    /// The HRM API URL is defined here and cannot be changed by the operator.
    /// </summary>
    public static class Constants
    {
        /// <summary>
        /// Hardcoded HRM API base URL - set by developers before building.
        /// This is the endpoint where attendance data will be posted.
        /// </summary>
        public const string HrmApiBaseUrl = "http://our-hrm-server.com";

        /// <summary>
        /// The API endpoint for importing attendance records.
        /// </summary>
        public const string AttendanceImportEndpoint = "/api/attendance/importAttendance";

        /// <summary>
        /// Default port for the web dashboard.
        /// </summary>
        public const int DefaultDashboardPort = 8080;

        /// <summary>
        /// Default sync interval in milliseconds (60 seconds).
        /// </summary>
        public const int DefaultSyncIntervalMs = 60000;

        /// <summary>
        /// ZKTeco device default port.
        /// </summary>
        public const int ZktecoDefaultPort = 4370;

        /// <summary>
        /// Maximum number of sync logs to keep in memory.
        /// </summary>
        public const int MaxSyncLogsToKeep = 20;

        /// <summary>
        /// Settings file name - stored in the application directory.
        /// </summary>
        public const string SettingsFileName = "settings.json";

        /// <summary>
        /// Windows Service name for the task scheduler and service manager.
        /// </summary>
        public const string ServiceName = "SamanDeviceAdapterService";

        /// <summary>
        /// Windows Service display name shown in Services.msc.
        /// </summary>
        public const string ServiceDisplayName = "Saman Device Adapter - Attendance Sync";
    }
}
