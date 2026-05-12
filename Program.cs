using System;
using System.Configuration.Install;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.ServiceProcess;

namespace SamanDeviceAdapter
{
    /// <summary>
    /// Entry point for the application.
    /// Handles --install, --uninstall arguments and runs the Windows Service.
    /// </summary>
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                // Check for command line arguments
                if (args.Length > 0)
                {
                    string command = args[0].ToLower();

                    if (command == "--install" || command == "-install" || command == "install")
                    {
                        InstallService();
                        return;
                    }
                    else if (command == "--uninstall" || command == "-uninstall" || command == "uninstall")
                    {
                        UninstallService();
                        return;
                    }
                }

                // Run as Windows Service
                ServiceBase[] servicesToRun = new ServiceBase[]
                {
                    new AttendanceService()
                };

                ServiceBase.Run(servicesToRun);
            }
            catch (Exception ex)
            {
                ServiceLogger.LogException("Program", ex);
                throw;
            }
        }

        /// <summary>
        /// Installs the Windows Service.
        /// </summary>
        private static void InstallService()
        {
            try
            {
                ServiceLogger.LogInfo("Program", "Installing service...");

                // Get the assembly path
                string assemblyPath = Assembly.GetExecutingAssembly().Location;
                string servicePath = string.Format("\"{0}\"", assemblyPath);

                // Use InstallUtil to install the service
                using (AssemblyInstaller installer = new AssemblyInstaller(assemblyPath, null))
                {
                    installer.UseNewContext = true;
                    try
                    {
                        installer.Install(new System.Collections.Hashtable());
                        installer.Commit(new System.Collections.Hashtable());
                        ServiceLogger.LogInfo("Program", "Service installed successfully");
                        Console.WriteLine("Service installed successfully.");

                        // Start the service
                        try
                        {
                            ServiceController sc = new ServiceController(Constants.ServiceName);
                            if (sc.Status != ServiceControllerStatus.Running)
                            {
                                sc.Start();
                                sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
                                ServiceLogger.LogInfo("Program", "Service started");
                                Console.WriteLine("Service started.");
                            }
                        }
                        catch (Exception ex)
                        {
                            ServiceLogger.LogWarning("Program", "Failed to start service: " + ex.Message);
                            Console.WriteLine("Service installed but failed to start automatically.");
                        }

                        // Open dashboard in browser
                        try
                        {
                            SettingsManager settingsManager = new SettingsManager();
                            int port = settingsManager.GetSettings().DashboardPort;
                            string url = $"http://localhost:{port}/";
                            Process.Start(url);
                        }
                        catch (Exception ex)
                        {
                            ServiceLogger.LogWarning("Program", "Failed to open dashboard: " + ex.Message);
                        }
                    }
                    catch (Exception ex)
                    {
                        ServiceLogger.LogError("Program", "Installation failed: " + ex.Message);
                        Console.WriteLine($"Installation failed: {ex.Message}");
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                ServiceLogger.LogException("Program", ex);
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Uninstalls the Windows Service.
        /// </summary>
        private static void UninstallService()
        {
            try
            {
                ServiceLogger.LogInfo("Program", "Uninstalling service...");

                // Stop the service if it's running
                try
                {
                    ServiceController sc = new ServiceController(Constants.ServiceName);
                    if (sc.Status == ServiceControllerStatus.Running)
                    {
                        sc.Stop();
                        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
                        ServiceLogger.LogInfo("Program", "Service stopped");
                    }
                }
                catch (Exception ex)
                {
                    ServiceLogger.LogWarning("Program", "Failed to stop service: " + ex.Message);
                }

                // Get the assembly path
                string assemblyPath = Assembly.GetExecutingAssembly().Location;

                // Use InstallUtil to uninstall the service
                using (AssemblyInstaller installer = new AssemblyInstaller(assemblyPath, null))
                {
                    installer.UseNewContext = true;
                    try
                    {
                        installer.Uninstall(new System.Collections.Hashtable());
                        ServiceLogger.LogInfo("Program", "Service uninstalled successfully");
                        Console.WriteLine("Service uninstalled successfully.");
                    }
                    catch (Exception ex)
                    {
                        ServiceLogger.LogError("Program", "Uninstallation failed: " + ex.Message);
                        Console.WriteLine($"Uninstallation failed: {ex.Message}");
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                ServiceLogger.LogException("Program", ex);
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Service installer for use with InstallUtil.
    /// Required by .NET Framework for service installation.
    /// </summary>
    [System.Configuration.Install.RunInstaller(true)]
    public class ServiceInstaller : Installer
    {
        private ServiceProcessInstaller _serviceProcessInstaller;
        private System.ServiceProcess.ServiceInstaller _serviceInstaller;

        public ServiceInstaller()
        {
            _serviceProcessInstaller = new ServiceProcessInstaller();
            _serviceInstaller = new System.ServiceProcess.ServiceInstaller();

            // Service will run under Local System account
            _serviceProcessInstaller.Account = ServiceAccount.LocalSystem;

            // Service configuration
            _serviceInstaller.ServiceName = Constants.ServiceName;
            _serviceInstaller.DisplayName = Constants.ServiceDisplayName;
            _serviceInstaller.StartType = ServiceStartMode.Automatic;

            Installers.Add(_serviceProcessInstaller);
            Installers.Add(_serviceInstaller);
        }
    }
}
