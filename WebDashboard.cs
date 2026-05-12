using System;
using System.IO;
using System.Net;
using System.Text;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace SamanDeviceAdapter
{
    /// <summary>
    /// HttpListener-based web server for the dashboard.
    /// Serves the embedded HTML dashboard and provides API endpoints for status, logs, and settings.
    /// </summary>
    public class WebDashboard : IDisposable
    {
        private HttpListener _httpListener;
        private readonly int _port;
        private readonly SyncWorker _syncWorker;
        private readonly SettingsManager _settingsManager;
        private bool _isRunning;

        public WebDashboard(int port, SyncWorker syncWorker, SettingsManager settingsManager)
        {
            _port = port;
            _syncWorker = syncWorker;
            _settingsManager = settingsManager;
            _isRunning = false;
        }

        /// <summary>
        /// Starts the web dashboard server.
        /// </summary>
        public void Start()
        {
            try
            {
                _httpListener = new HttpListener();
                _httpListener.Prefixes.Add($"http://localhost:{_port}/");
                _httpListener.Start();

                _isRunning = true;
                ServiceLogger.LogInfo("WebDashboard", $"Dashboard started on port {_port}");

                // Start accepting requests asynchronously
                _httpListener.BeginGetContext(OnHttpRequest, _httpListener);
            }
            catch (Exception ex)
            {
                ServiceLogger.LogException("WebDashboard", ex);
                throw;
            }
        }

        /// <summary>
        /// Stops the web dashboard server.
        /// </summary>
        public void Stop()
        {
            try
            {
                _isRunning = false;

                if (_httpListener != null)
                {
                    _httpListener.Stop();
                    _httpListener.Close();
                    _httpListener = null;
                }

                ServiceLogger.LogInfo("WebDashboard", "Dashboard stopped");
            }
            catch (Exception ex)
            {
                ServiceLogger.LogException("WebDashboard", ex);
            }
        }

        /// <summary>
        /// Handles incoming HTTP requests.
        /// </summary>
        private void OnHttpRequest(IAsyncResult result)
        {
            try
            {
                if (!_isRunning || _httpListener == null)
                {
                    return;
                }

                HttpListenerContext context = _httpListener.EndGetContext(result);
                _httpListener.BeginGetContext(OnHttpRequest, _httpListener);

                ProcessRequest(context);
            }
            catch (HttpListenerException)
            {
                // Listener was closed
            }
            catch (Exception ex)
            {
                ServiceLogger.LogException("WebDashboard", ex);
            }
        }

        /// <summary>
        /// Routes the HTTP request to the appropriate handler.
        /// </summary>
        private void ProcessRequest(HttpListenerContext context)
        {
            try
            {
                string path = context.Request.Url.AbsolutePath.ToLower();
                string method = context.Request.HttpMethod.ToUpper();

                switch (path)
                {
                    case "/":
                        HandleGetDashboard(context);
                        break;

                    case "/api/status":
                        if (method == "GET")
                            HandleGetStatus(context);
                        break;

                    case "/api/logs":
                        if (method == "GET")
                            HandleGetLogs(context);
                        break;

                    case "/api/settings":
                        if (method == "GET")
                            HandleGetSettings(context);
                        else if (method == "POST")
                            HandlePostSettings(context);
                        break;

                    case "/api/test-connection":
                        if (method == "POST")
                            HandleTestConnection(context);
                        break;

                    default:
                        SendResponse(context, 404, "text/plain", "Not Found");
                        break;
                }
            }
            catch (Exception ex)
            {
                ServiceLogger.LogException("WebDashboard", ex);
                SendResponse(context, 500, "text/plain", "Internal Server Error");
            }
        }

        /// <summary>
        /// GET / - Serves the embedded HTML dashboard.
        /// </summary>
        private void HandleGetDashboard(HttpListenerContext context)
        {
            string html = GetDashboardHtml();
            SendResponse(context, 200, "text/html", html);
        }

        /// <summary>
        /// GET /api/status - Returns service and device status.
        /// </summary>
        private void HandleGetStatus(HttpListenerContext context)
        {
            var status = new
            {
                serviceRunning = _syncWorker.IsServiceRunning,
                deviceConnected = _syncWorker.IsDeviceConnected,
                lastSyncTime = _syncWorker.LastSyncTime.ToString("yyyy-MM-dd HH:mm:ss")
            };

            string json = JsonConvert.SerializeObject(status);
            SendResponse(context, 200, "application/json", json);
        }

        /// <summary>
        /// GET /api/logs - Returns the last 20 sync logs.
        /// </summary>
        private void HandleGetLogs(HttpListenerContext context)
        {
            var logs = _syncWorker.GetSyncLogs();

            var logEntries = logs.ConvertAll(log => new
            {
                timestamp = log.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                success = log.Success,
                message = log.Message
            });

            string json = JsonConvert.SerializeObject(logEntries);
            SendResponse(context, 200, "application/json", json);
        }

        /// <summary>
        /// GET /api/settings - Returns current device settings (excluding HRM API URL).
        /// </summary>
        private void HandleGetSettings(HttpListenerContext context)
        {
            var settings = _settingsManager.GetSettings();

            var settingsDto = new
            {
                deviceIp = settings.DeviceIp,
                devicePort = settings.DevicePort,
                companyId = settings.CompanyId,
                deleteAfterSend = settings.DeleteAfterSend,
                dashboardPort = settings.DashboardPort
            };

            string json = JsonConvert.SerializeObject(settingsDto);
            SendResponse(context, 200, "application/json", json);
        }

        /// <summary>
        /// POST /api/settings - Updates device settings.
        /// </summary>
        private void HandlePostSettings(HttpListenerContext context)
        {
            try
            {
                string body = new StreamReader(context.Request.InputStream).ReadToEnd();
                dynamic newSettings = JsonConvert.DeserializeObject(body);

                var settings = _settingsManager.GetSettings();

                // Update only the device-configurable settings
                if (newSettings.deviceIp != null)
                    settings.DeviceIp = newSettings.deviceIp;

                if (newSettings.devicePort != null)
                    settings.DevicePort = (int)newSettings.devicePort;

                if (newSettings.companyId != null)
                    settings.CompanyId = newSettings.companyId;

                if (newSettings.deleteAfterSend != null)
                    settings.DeleteAfterSend = (bool)newSettings.deleteAfterSend;

                _settingsManager.SaveSettings();

                var response = new { success = true, message = "Settings updated successfully" };
                SendResponse(context, 200, "application/json", JsonConvert.SerializeObject(response));

                ServiceLogger.LogInfo("WebDashboard", "Settings updated");
            }
            catch (Exception ex)
            {
                ServiceLogger.LogException("WebDashboard", ex);
                var response = new { success = false, message = ex.Message };
                SendResponse(context, 400, "application/json", JsonConvert.SerializeObject(response));
            }
        }

        /// <summary>
        /// POST /api/test-connection - Tests connection to ZKTeco device.
        /// </summary>
        private void HandleTestConnection(HttpListenerContext context)
        {
            try
            {
                var settings = _settingsManager.GetSettings();
                var zkClient = new ZktecoClient(settings.DeviceIp, settings.DevicePort);

                bool connected = zkClient.Connect();
                zkClient.Disconnect();
                zkClient.Dispose();

                var response = new
                {
                    success = connected,
                    message = connected 
                        ? $"Successfully connected to {settings.DeviceIp}:{settings.DevicePort}"
                        : $"Failed to connect to {settings.DeviceIp}:{settings.DevicePort}"
                };

                SendResponse(context, 200, "application/json", JsonConvert.SerializeObject(response));
                ServiceLogger.LogInfo("WebDashboard", "Connection test: " + (connected ? "Success" : "Failed"));
            }
            catch (Exception ex)
            {
                ServiceLogger.LogException("WebDashboard", ex);
                var response = new { success = false, message = ex.Message };
                SendResponse(context, 400, "application/json", JsonConvert.SerializeObject(response));
            }
        }

        /// <summary>
        /// Helper method to send HTTP response.
        /// </summary>
        private void SendResponse(HttpListenerContext context, int statusCode, string contentType, string responseBody)
        {
            try
            {
                context.Response.StatusCode = statusCode;
                context.Response.ContentType = contentType;
                context.Response.ContentEncoding = Encoding.UTF8;

                byte[] buffer = Encoding.UTF8.GetBytes(responseBody);
                context.Response.ContentLength64 = buffer.Length;

                context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                context.Response.OutputStream.Close();
            }
            catch (Exception ex)
            {
                ServiceLogger.LogException("WebDashboard", ex);
            }
        }

        public void Dispose()
        {
            Stop();
        }

        /// <summary>
        /// Returns the embedded HTML dashboard as a string.
        /// Uses vanilla HTML/CSS/JS with XMLHttpRequest for IE11 compatibility.
        /// </summary>
        private static string GetDashboardHtml()
        {
            // Minified HTML to avoid string literal complexity
            string html = HtmlContent.BuildDashboard();
            return html;
        }
    }

    /// <summary>
    /// Helper class to build the HTML content programmatically to avoid string literal issues.
    /// </summary>
    internal static class HtmlContent
    {
        public static string BuildDashboard()
        {
            StringBuilder html = new StringBuilder();
            
            html.Append("<!DOCTYPE html>");
            html.Append("<html><head><meta charset='UTF-8'><meta name='viewport' content='width=device-width, initial-scale=1.0'>");
            html.Append("<meta http-equiv='X-UA-Compatible' content='IE=edge'>");
            html.Append("<title>Saman Attendance Sync</title>");
            html.Append("<style>");
            html.Append("*{margin:0;padding:0;box-sizing:border-box}");
            html.Append("body{font-family:'Segoe UI',Tahoma,Geneva,Verdana,sans-serif;background:linear-gradient(135deg,#667eea 0%,#764ba2 100%);min-height:100vh;padding:20px}");
            html.Append(".container{max-width:1200px;margin:0 auto;background:white;border-radius:10px;box-shadow:0 10px 40px rgba(0,0,0,0.3);overflow:hidden}");
            html.Append(".header{background:linear-gradient(135deg,#667eea 0%,#764ba2 100%);color:white;padding:30px;text-align:center}");
            html.Append(".header h1{font-size:28px;margin-bottom:5px}");
            html.Append(".header p{font-size:14px;opacity:0.9}");
            html.Append(".content{padding:30px}");
            html.Append(".status-section{display:grid;grid-template-columns:1fr 1fr;gap:20px;margin-bottom:40px}");
            html.Append(".status-card{background:#f8f9fa;border:2px solid #e9ecef;border-radius:8px;padding:20px;text-align:center}");
            html.Append(".status-indicator{width:80px;height:80px;border-radius:50%;margin:0 auto 15px;display:flex;align-items:center;justify-content:center;font-size:40px;font-weight:bold}");
            html.Append(".status-indicator.ok{background:#d4edda;color:#155724}");
            html.Append(".status-indicator.error{background:#f8d7da;color:#721c24}");
            html.Append(".status-indicator.pending{background:#fff3cd;color:#856404}");
            html.Append(".status-label{font-size:18px;font-weight:bold;margin-bottom:5px;color:#333}");
            html.Append(".status-value{font-size:14px;color:#666}");
            html.Append(".last-sync{font-size:12px;color:#999;margin-top:10px}");
            html.Append(".section-title{font-size:20px;font-weight:bold;color:#333;margin-bottom:15px;border-bottom:2px solid #667eea;padding-bottom:10px}");
            html.Append(".logs-section{margin-bottom:40px}");
            html.Append(".log-table{width:100%;border-collapse:collapse;background:white;font-size:13px}");
            html.Append(".log-table thead{background:#f8f9fa}");
            html.Append(".log-table th{padding:12px;text-align:left;font-weight:bold;border-bottom:2px solid #e9ecef;color:#333}");
            html.Append(".log-table td{padding:12px;border-bottom:1px solid #e9ecef;color:#666}");
            html.Append(".log-table tr:hover{background:#f8f9fa}");
            html.Append(".log-status{padding:4px 8px;border-radius:4px;font-weight:bold;text-align:center;width:60px}");
            html.Append(".log-status.success{background:#d4edda;color:#155724}");
            html.Append(".log-status.failed{background:#f8d7da;color:#721c24}");
            html.Append(".settings-section{background:#f8f9fa;border-radius:8px;padding:20px}");
            html.Append(".form-group{margin-bottom:15px}");
            html.Append(".form-group label{display:block;font-weight:bold;margin-bottom:5px;color:#333}");
            html.Append("input[type='text'],input[type='number']{width:100%;padding:10px;border:1px solid #ddd;border-radius:4px;font-size:14px;font-family:inherit}");
            html.Append("input:focus{outline:none;border-color:#667eea;box-shadow:0 0 5px rgba(102,126,234,0.3)}");
            html.Append(".form-group-row{display:grid;grid-template-columns:1fr 1fr;gap:20px}");
            html.Append(".checkbox-group{display:flex;align-items:center;margin-top:15px}");
            html.Append(".checkbox-group input{width:18px;height:18px;margin-right:10px;cursor:pointer}");
            html.Append(".checkbox-group label{margin:0;cursor:pointer;font-weight:normal}");
            html.Append(".button-group{display:grid;grid-template-columns:1fr 1fr;gap:10px;margin-top:20px}");
            html.Append("button{padding:12px 20px;border:none;border-radius:4px;font-size:14px;font-weight:bold;cursor:pointer;background:#667eea;color:white;transition:all 0.3s}");
            html.Append("button:hover{background:#5568d3;transform:translateY(-2px)}");
            html.Append(".alert{padding:12px;border-radius:4px;margin-bottom:15px;font-size:13px}");
            html.Append(".alert-success{background:#d4edda;color:#155724;border:1px solid #c3e6cb}");
            html.Append(".alert-error{background:#f8d7da;color:#721c24;border:1px solid #f5c6cb}");
            html.Append(".alert-info{background:#d1ecf1;color:#0c5460;border:1px solid #bee5eb}");
            html.Append("@media(max-width:768px){.status-section{grid-template-columns:1fr}.form-group-row{grid-template-columns:1fr}.button-group{grid-template-columns:1fr}}");
            html.Append("</style></head><body>");
            html.Append("<div class='container'><div class='header'><h1>Saman Attendance Sync</h1><p>Biometric Device Integration Dashboard</p></div>");
            html.Append("<div class='content'><div class='status-section'>");
            html.Append("<div class='status-card'><div class='status-label'>Service Status</div><div id='serviceStatusIndicator' class='status-indicator pending'></div><div class='status-value' id='serviceStatusText'>Checking...</div></div>");
            html.Append("<div class='status-card'><div class='status-label'>Device Connection</div><div id='deviceStatusIndicator' class='status-indicator pending'></div><div class='status-value' id='deviceStatusText'>Checking...</div><div class='last-sync' id='lastSyncTime'></div></div>");
            html.Append("</div><div class='logs-section'><div class='section-title'>Sync History</div>");
            html.Append("<table class='log-table'><thead><tr><th>Timestamp</th><th>Status</th><th>Message</th></tr></thead>");
            html.Append("<tbody id='logsTableBody'><tr><td colspan='3' style='text-align:center;color:#999;'>Loading...</td></tr></tbody></table></div>");
            html.Append("<div class='settings-section'><div class='section-title'>Device Settings</div><div id='alertBox'></div>");
            html.Append("<div class='form-group-row'>");
            html.Append("<div class='form-group'><label for='deviceIp'>Device IP Address</label><input type='text' id='deviceIp' placeholder='192.168.1.201'></div>");
            html.Append("<div class='form-group'><label for='devicePort'>Device Port</label><input type='number' id='devicePort' min='1' max='65535' placeholder='4370'></div>");
            html.Append("</div><div class='form-group'><label for='companyId'>Company ID</label><input type='text' id='companyId'></div>");
            html.Append("<div class='checkbox-group'><input type='checkbox' id='deleteAfterSend'><label for='deleteAfterSend'>Delete records after send</label></div>");
            html.Append("<div class='button-group'><button onclick='testConnection()'>Test Connection</button><button onclick='saveSettings()'>Save Settings</button></div>");
            html.Append("</div></div></div></body>");
            html.Append(GetJavaScript());
            html.Append("</html>");
            
            return html.ToString();
        }

        private static string GetJavaScript()
        {
            return "<script>" +
            "var REFRESH_INTERVAL=5000,refreshTimer=null;" +
            "document.addEventListener('DOMContentLoaded',function(){loadAllData();refreshTimer=setInterval(loadAllData,REFRESH_INTERVAL)});" +
            "function loadAllData(){loadStatus();loadLogs();loadSettings()}" +
            "function loadStatus(){xmlHttpRequest('GET','/api/status',null,function(data){try{var status=JSON.parse(data);updateServiceStatus(status.serviceRunning);updateDeviceStatus(status.deviceConnected,status.lastSyncTime)}catch(e){}},function(){})}" +
            "function updateServiceStatus(isRunning){var indicator=document.getElementById('serviceStatusIndicator'),statusText=document.getElementById('serviceStatusText');if(isRunning){indicator.className='status-indicator ok';indicator.textContent='●';statusText.textContent='Running';statusText.style.color='#155724'}else{indicator.className='status-indicator error';indicator.textContent='●';statusText.textContent='Stopped';statusText.style.color='#721c24'}}" +
            "function updateDeviceStatus(isConnected,lastSyncTime){var indicator=document.getElementById('deviceStatusIndicator'),statusText=document.getElementById('deviceStatusText'),syncTimeText=document.getElementById('lastSyncTime');if(isConnected){indicator.className='status-indicator ok';indicator.textContent='●';statusText.textContent='Connected';statusText.style.color='#155724'}else{indicator.className='status-indicator error';indicator.textContent='●';statusText.textContent='Disconnected';statusText.style.color='#721c24'}if(lastSyncTime&&lastSyncTime!=='0001-01-01 00:00:00'){syncTimeText.textContent='Last sync: '+lastSyncTime}else{syncTimeText.textContent='Never synced'}}" +
            "function loadLogs(){xmlHttpRequest('GET','/api/logs',null,function(data){try{var logs=JSON.parse(data);updateLogsTable(logs)}catch(e){}},function(){})}" +
            "function updateLogsTable(logs){var tbody=document.getElementById('logsTableBody');if(logs.length===0){tbody.innerHTML='<tr><td colspan=\"3\" style=\"text-align:center;color:#999;\">No sync history</td></tr>';return}var html='';for(var i=0;i<logs.length;i++){var log=logs[i],statusClass=log.success?'success':'failed',statusText=log.success?'SUCCESS':'FAILED';html+='<tr><td>'+log.timestamp+'</td><td><span class=\"log-status '+statusClass+'\">'+statusText+'</span></td><td>'+escapeHtml(log.message)+'</td></tr>'}tbody.innerHTML=html}" +
            "function loadSettings(){xmlHttpRequest('GET','/api/settings',null,function(data){try{var settings=JSON.parse(data);document.getElementById('deviceIp').value=settings.deviceIp||'';document.getElementById('devicePort').value=settings.devicePort||4370;document.getElementById('companyId').value=settings.companyId||'';document.getElementById('deleteAfterSend').checked=settings.deleteAfterSend||false}catch(e){}},function(){})}" +
            "function testConnection(){showAlert('info','Testing...');xmlHttpRequest('POST','/api/test-connection',null,function(data){try{var result=JSON.parse(data);showAlert(result.success?'success':'error',result.message)}catch(e){showAlert('error','Failed')}},function(){showAlert('error','Failed')})}" +
            "function saveSettings(){var settings={deviceIp:document.getElementById('deviceIp').value,devicePort:parseInt(document.getElementById('devicePort').value)||4370,companyId:document.getElementById('companyId').value,deleteAfterSend:document.getElementById('deleteAfterSend').checked};xmlHttpRequest('POST','/api/settings',JSON.stringify(settings),function(data){try{var result=JSON.parse(data);showAlert(result.success?'success':'error',result.message);if(result.success)loadSettings()}catch(e){showAlert('error','Failed')}},function(){showAlert('error','Failed')})}" +
            "function xmlHttpRequest(method,url,data,onSuccess,onError){var xhr=null;try{xhr=new XMLHttpRequest()}catch(e){try{xhr=new ActiveXObject('Msxml2.XMLHTTP')}catch(e2){try{xhr=new ActiveXObject('Microsoft.XMLHTTP')}catch(e3){onError('Not supported');return}}}xhr.onreadystatechange=function(){if(xhr.readyState===4){if(xhr.status===200||xhr.status===400){onSuccess(xhr.responseText)}else{onError('HTTP '+xhr.status)}}};xhr.onerror=function(){onError('Network error')};try{xhr.open(method,url,true);xhr.timeout=10000;if(method==='POST'&&data){xhr.setRequestHeader('Content-Type','application/json');xhr.send(data)}else{xhr.send(null)}}catch(e){onError(e.message)}}" +
            "function showAlert(type,message){var alertBox=document.getElementById('alertBox');alertBox.innerHTML='<div class=\"alert alert-'+type+'\">'+message+'</div>';if(type==='info'){setTimeout(function(){alertBox.innerHTML=''},5000)}}" +
            "function escapeHtml(text){if(!text)return'';var div=document.createElement('div');div.textContent=text;return div.innerHTML}" +
            "window.addEventListener('beforeunload',function(){if(refreshTimer)clearInterval(refreshTimer)});" +
            "</script>";
        }
    }
}
