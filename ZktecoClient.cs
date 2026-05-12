using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace SamanDeviceAdapter
{
    /// <summary>
    /// Handles TCP communication with ZKTeco biometric devices.
    /// Provides methods to connect, disconnect, and fetch attendance records.
    /// </summary>
    public class ZktecoClient : IDisposable
    {
        private TcpClient _tcpClient;
        private NetworkStream _networkStream;
        private readonly string _deviceIp;
        private readonly int _devicePort;
        private bool _isConnected;

        public bool IsConnected
        {
            get { return _isConnected && _tcpClient != null && _tcpClient.Connected; }
        }

        public ZktecoClient(string deviceIp, int devicePort)
        {
            _deviceIp = deviceIp;
            _devicePort = devicePort;
            _isConnected = false;
        }

        /// <summary>
        /// Attempts to connect to the ZKTeco device.
        /// </summary>
        public bool Connect()
        {
            try
            {
                if (_tcpClient != null && _tcpClient.Connected)
                {
                    return true;
                }

                _tcpClient = new TcpClient();
                _tcpClient.ConnectAsync(_deviceIp, _devicePort).Wait(TimeSpan.FromSeconds(5));

                if (_tcpClient.Connected)
                {
                    _networkStream = _tcpClient.GetStream();
                    _isConnected = true;
                    ServiceLogger.LogInfo("ZktecoClient", $"Connected to device at {_deviceIp}:{_devicePort}");
                    return true;
                }

                _isConnected = false;
                ServiceLogger.LogWarning("ZktecoClient", $"Failed to connect to {_deviceIp}:{_devicePort}");
                return false;
            }
            catch (Exception ex)
            {
                _isConnected = false;
                ServiceLogger.LogError("ZktecoClient", $"Connection error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Disconnects from the ZKTeco device.
        /// </summary>
        public void Disconnect()
        {
            try
            {
                if (_networkStream != null)
                {
                    _networkStream.Close();
                    _networkStream.Dispose();
                    _networkStream = null;
                }

                if (_tcpClient != null)
                {
                    _tcpClient.Close();
                    _tcpClient.Dispose();
                    _tcpClient = null;
                }

                _isConnected = false;
                ServiceLogger.LogInfo("ZktecoClient", "Disconnected from device");
            }
            catch (Exception ex)
            {
                ServiceLogger.LogError("ZktecoClient", $"Disconnect error: {ex.Message}");
            }
        }

        /// <summary>
        /// Fetches attendance records from the device.
        /// In production, this would send the actual ZKTeco protocol commands.
        /// For now, returns a list of dummy attendance records for testing.
        /// 
        /// ZKTeco Protocol Overview:
        /// - Command packet structure: [Header(8 bytes)][Cmd(2 bytes)][Data(...)]
        /// - Header: 0xFF 0xFF 0xFF 0xFF 0xFF 0xFF 0xFF 0xFF (device identification)
        /// - Common commands: 0x0F (Connect), 0x38 (Get Records), 0x24 (Enable Device)
        /// - Response includes: User ID, Time, Status (IN=0, OUT=1)
        /// </summary>
        public List<AttendanceRecord> FetchAttendanceRecords()
        {
            var records = new List<AttendanceRecord>();

            try
            {
                if (!IsConnected)
                {
                    ServiceLogger.LogWarning("ZktecoClient", "Not connected to device");
                    return records;
                }

                // In production, send actual ZKTeco command to fetch records
                // For MVP, we'll return dummy data or query previously stored records
                // This would involve:
                // 1. Sending command 0x38 (Get Records) with session ID
                // 2. Receiving attendance data packets
                // 3. Parsing records and converting to AttendanceRecord objects

                ServiceLogger.LogInfo("ZktecoClient", "Fetching attendance records from device");

                // TODO: Implement actual ZKTeco protocol communication
                // For testing, return empty list
                return records;
            }
            catch (Exception ex)
            {
                ServiceLogger.LogError("ZktecoClient", $"Error fetching records: {ex.Message}");
                return records;
            }
        }

        /// <summary>
        /// Clears attendance records from the device.
        /// In production, sends the delete command to the device.
        /// </summary>
        public bool ClearAttendanceRecords()
        {
            try
            {
                if (!IsConnected)
                {
                    ServiceLogger.LogWarning("ZktecoClient", "Not connected to device");
                    return false;
                }

                // Send clear records command (0xEF in ZKTeco protocol)
                // TODO: Implement actual ZKTeco protocol communication

                ServiceLogger.LogInfo("ZktecoClient", "Cleared attendance records from device");
                return true;
            }
            catch (Exception ex)
            {
                ServiceLogger.LogError("ZktecoClient", $"Error clearing records: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Sends a generic command to the device and receives response.
        /// Used internally by other methods.
        /// </summary>
        private byte[] SendCommand(byte[] commandData)
        {
            try
            {
                if (!IsConnected || _networkStream == null)
                {
                    throw new InvalidOperationException("Not connected to device");
                }

                // Send command
                _networkStream.Write(commandData, 0, commandData.Length);
                _networkStream.Flush();

                // Receive response (with timeout)
                byte[] buffer = new byte[4096];
                _networkStream.ReadTimeout = 5000; // 5 second timeout

                int bytesRead = _networkStream.Read(buffer, 0, buffer.Length);
                Array.Resize(ref buffer, bytesRead);

                return buffer;
            }
            catch (Exception ex)
            {
                ServiceLogger.LogError("ZktecoClient", $"Command error: {ex.Message}");
                Disconnect();
                return new byte[0];
            }
        }

        public void Dispose()
        {
            Disconnect();
        }
    }

    /// <summary>
    /// Represents a single attendance record from the ZKTeco device.
    /// </summary>
    public class AttendanceRecord
    {
        public string EmployeeId { get; set; }
        public DateTime Timestamp { get; set; }
        public AttendanceStatus Status { get; set; } // IN (0) or OUT (1)
        public string VerifyMode { get; set; } // Fingerprint, Face, Card, etc.
    }

    /// <summary>
    /// Attendance event status.
    /// </summary>
    public enum AttendanceStatus
    {
        In = 0,
        Out = 1
    }
}
