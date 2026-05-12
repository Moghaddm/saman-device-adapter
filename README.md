# Saman Attendance Sync - Windows Service

A production-ready Windows Service application for syncing ZKTeco biometric attendance data to an HRM API with an embedded web dashboard for management.

## Features

- **Windows Service**: Runs as a background service with automatic synchronization every 60 seconds
- **Embedded Web Dashboard**: Access via http://localhost:8080 for monitoring and configuration
- **ZKTeco Integration**: TCP/IP connection to ZKTeco biometric devices on port 4370
- **Real-time Status Indicators**: Clear visual indicators for service status and device connection
- **IE11/Windows 7 Compatible**: Uses vanilla HTML/CSS/JS with XMLHttpRequest for legacy compatibility
- **Hardcoded HRM API**: Prevents operator misconfiguration of the API endpoint
- **Easy Installation**: Simple batch file for installation and uninstallation

## Requirements

- Windows 7 or later
- .NET Framework 4.8 (or 4.5 minimum)
- Administrator privileges for installation
- Network connectivity to ZKTeco device and HRM API

## Installation

1. Ensure the folder contains:
   - `SamanDeviceAdapter.exe` (compiled application)
   - `install.bat` (installation script)
   - `uninstall.bat` (uninstallation script)
   - `settings.json` (will be created automatically)

2. Run `install.bat` as Administrator
   - The script will install the Windows Service
   - Automatically start the service
   - Open the web dashboard in your default browser

3. Configure the device settings in the dashboard:
   - Device IP Address
   - Device Port (default: 4370)
   - Company ID (if required)
   - Enable/disable "Delete records after send" option

## Usage

### Web Dashboard

Access the dashboard at: **http://localhost:8080/**

#### Service Status Section
- **Service Status**: Shows if the sync service is running
- **Device Connection**: Shows if the device is reachable
- **Last Sync Time**: Timestamp of the last successful sync

#### Sync History
Displays the last 20 sync operations with:
- Timestamp
- Status (SUCCESS/FAILED)
- Message describing the operation

#### Device Settings
Configure:
- Device IP Address (e.g., 192.168.1.201)
- Device Port (default: 4370)
- Company ID
- Auto-delete records after successful send (optional)

**Test Connection** button validates connectivity to the configured device.

### Dashboard Auto-Refresh

The dashboard automatically refreshes every 5 seconds to show:
- Current service and device status
- Latest sync history
- Settings from the device

### Logs

Application logs are stored in:
```
[Application Directory]\ServiceLog_YYYY-MM-DD.txt
```

Each log entry includes:
- Timestamp (yyyy-MM-dd HH:mm:ss.fff)
- Log level (INFO, WARN, ERROR)
- Source (component name)
- Message

## Settings File

Settings are automatically saved to `settings.json` in the application directory:

```json
{
  "DeviceIp": "192.168.1.201",
  "DevicePort": 4370,
  "CompanyId": "",
  "DeleteAfterSend": false,
  "DashboardPort": 8080
}
```

**Note:** The HRM API URL is hardcoded in the Constants.cs file and cannot be changed by the operator.

## Uninstallation

1. Run `uninstall.bat` as Administrator
2. The script will:
   - Stop the Windows Service
   - Remove the service from Windows

## Architecture

### Core Components

- **Program.cs**: Entry point with --install/--uninstall command handling
- **AttendanceService.cs**: Windows Service implementation (ServiceBase)
- **SyncWorker.cs**: Background timer (every 60 seconds) for data synchronization
- **WebDashboard.cs**: HttpListener-based web server for the management dashboard
- **ZktecoClient.cs**: TCP/IP client for ZKTeco device communication
- **SettingsManager.cs**: Settings persistence via JSON file
- **Constants.cs**: Application constants including hardcoded HRM API URL

### Data Flow

```
Sync Timer (every 60s)
    ↓
Connect to ZKTeco Device (TCP/4370)
    ↓
Fetch Attendance Records
    ↓
Push to HRM API (POST)
    ↓
Clear Device Records (optional)
    ↓
Log Results & Update Dashboard
```

## Configuration

### Hardcoded Settings (in Constants.cs)

These cannot be changed by the operator:

```csharp
public const string HrmApiBaseUrl = "http://our-hrm-server.com";
public const string AttendanceImportEndpoint = "/api/attendance/importAttendance";
```

**To change these values:**
1. Modify `Constants.cs`
2. Rebuild the application
3. Reinstall the service

### Operator-Configurable Settings (Dashboard)

- Device IP Address
- Device Port
- Company ID
- Delete After Send option

## Troubleshooting

### Service Won't Start
1. Check Windows Event Viewer for service errors
2. Review the application log file (`ServiceLog_YYYY-MM-DD.txt`)
3. Ensure the account has necessary privileges

### Device Connection Failed
1. Verify device IP address in settings
2. Verify device port (default: 4370)
3. Check network connectivity to the device
4. Use dashboard "Test Connection" button for diagnostics

### HRM API Connection Failed
1. Check the hardcoded HRM API URL in Constants.cs
2. Verify network connectivity to the HRM server
3. Check application logs for detailed error messages
4. Verify the API endpoint is correct and accessible

### Dashboard Not Accessible
1. Verify the service is running (check Service Manager)
2. Verify the port in settings is not in use by another application
3. Check Windows Firewall allows localhost access
4. Review application logs for startup errors

## Security Considerations

- Service runs under Local System account (highest privileges)
- HRM API URL is hardcoded to prevent operator misconfiguration
- Settings file contains only device configuration (no sensitive data)
- Log files contain only operational information (no passwords)

## Performance

- Sync interval: 60 seconds
- Dashboard refresh: 5 seconds
- Max sync logs kept in memory: 20 entries
- Typical memory usage: 50-100 MB
- Low CPU impact during normal operation

## Support & Maintenance

For support, review the application logs:
```
ServiceLog_YYYY-MM-DD.txt
```

Common issues and solutions are documented above in the "Troubleshooting" section.

## Technical Details

### Framework
- Target Framework: .NET Framework 4.8
- Compatible with: Windows 7 SP1 and later

### Dependencies
- Newtonsoft.Json (JSON serialization)
- System.Net.HttpListener (built-in, for web dashboard)
- System.Net.Sockets (built-in, for TCP communication)

### API Endpoint

The application sends attendance data to:
```
POST {HrmApiBaseUrl}/api/attendance/importAttendance
Content-Type: application/json

{
  "companyId": "string",
  "records": [
    {
      "employeeId": "string",
      "timestamp": "2024-01-01T12:00:00",
      "status": "In|Out",
      "verifyMode": "string"
    }
  ]
}
```

---

**Version:** 1.0.0  
**Last Updated:** 2024  
**License:** Proprietary
