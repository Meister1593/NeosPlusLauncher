﻿using Avalonia.Controls;
using ReactiveUI;
using System;
using System.Diagnostics;
using System.IO;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Threading;
using ReactiveUI.Fody.Helpers;

namespace NeosPlusLauncher.ViewModels
{
    public class MainWindowViewModel : ReactiveObject
    {

        [Reactive]
        public string StatusText { get; set; } = string.Empty;

        [Reactive]
        public string LauncherArguments { get; set; } = string.Empty;

        [Reactive]
        public bool InstallEnabled { get; set; } = true;
        [Reactive]
        public bool SteamRunEnabled { get; set; } = false;

        public ReactiveCommand<Unit, Unit> InstallCommand { get; }
        public ReactiveCommand<Unit, Unit> LaunchCommand { get; }
        private MainWindow mainWindow;

        public MainWindowViewModel(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;

            InitializeControls();

            InstallCommand = ReactiveCommand.CreateFromTask(ExecuteInstall);
            LaunchCommand = ReactiveCommand.Create(LaunchNeosPlus);
        }

        private void InitializeControls()
        {

        }

        private async Task ExecuteInstall()
        {
            try
            {
                InstallEnabled = false;
                StatusText = "Checking for updates...";

                string[] neosPaths = NeosPathHelper.GetNeosPaths();
                string neosPath = null;

                if (neosPaths.Length > 0)
                {
                    neosPath = neosPaths[0];
                }
                else
                {   //openfolderdialog is obsolete replace it soon.
                    var dialog = new OpenFolderDialog { Title = "Select Directory", Directory = "." };
                    var result = await dialog.ShowAsync(mainWindow);

                    if (result == null)
                    {
                        StatusText = "No Neos directory selected.";
                        InstallEnabled = true;
                        return;
                    }

                    neosPath = result;

                    // Save the custom directory to the configuration
                    Config config = Config.LoadConfig();
                    config.CustomInstallDir = neosPath;
                    config.SaveConfig();
                }

                string neosPlusDirectory = Path.Combine(neosPath, "Libraries", "NeosPlus");

                bool downloadSuccess = await DownloadNeosPlus(neosPath, neosPlusDirectory);

                if (!downloadSuccess)
                {
                    InstallEnabled = true;
                    return;
                }

                StatusText = "Done";
                InstallEnabled = true;
            }
            catch (Exception ex)
            {
                StatusText = $"Failed to execute install: {ex.Message}";
            }
        }

        private async Task<bool> DownloadNeosPlus(string neosPath, string neosPlusDirectory)
        {
            try
            {
                StatusText = "Downloading NeosPlus...";
                InstallEnabled = false;

                DownloadResult res = await Download.DownloadAndInstallNeosPlus(neosPath, neosPlusDirectory);
                StatusText = res.Message;


                InstallEnabled = true;
                return res.Succes;
            }
            catch (Exception ex)
            {
                StatusText = $"Error during NeosPlus download: {ex.Message}";
                return false;
            }
        }

        private void LaunchNeosPlus()
        {
            string[] neosPaths = NeosPathHelper.GetNeosPaths();

            if (neosPaths.Length == 0)
            {
                StatusText = "No Neos directory found.";
                return;
            }

            var path = neosPaths[0];

            string neosPlusDirectory = Path.Combine(path, "Libraries", "NeosPlus");

            LaunchNeosPlus(path, neosPlusDirectory);

            StatusText = "Done";
        }
        private void LaunchNeosPlus(string neosPath, string neosPlusDirectory)
        {

            string neosExePath;
            string neosPlusDllPath = Path.Combine(neosPlusDirectory, "NeosPlus.dll");
            string arguments;
            if (SteamRunEnabled)
            {
                // Without that, xdg-open will interprete slashes incorrectly
                neosExePath = "sh";
                // Absolute path replaced with relative, absolute doesn't work with steam run
                neosPlusDllPath = neosPlusDllPath.Replace(neosPath + "/", "");
                // Only double backslashes worked with steam run, not forward slashes
                // And for it to work inside sh, it needs another double backslash
                neosPlusDllPath = neosPlusDllPath.Replace("/", "\\\\");
                // Single quote is essential for steam run as well
                arguments = $"-c \"xdg-open steam://run/740250//'-LoadAssembly {neosPlusDllPath}";
            }
            else
            {
                neosExePath = Path.Combine(neosPath, "neos.exe");
                arguments = $"-LoadAssembly \"{neosPlusDllPath}\"";
            }

            // Get the value of the LauncherArgumentsTextBox and add it as an argument
            string launcherArguments = LauncherArguments.Trim();
            if (!string.IsNullOrEmpty(launcherArguments))
            {
                if (SteamRunEnabled)
                {
                    launcherArguments = launcherArguments.Replace("/", "\\\\");
                }
                arguments += $" {launcherArguments}";
            }
            if (SteamRunEnabled)
            {
                // Here we close entire argument for steam run, as well as for sh
                arguments += "'\"";
            }

            ProcessStartInfo startInfo = new ProcessStartInfo(neosExePath, arguments);
            startInfo.WorkingDirectory = neosPath;

            try
            {
                Process.Start(startInfo);

                // Save the configuration after launching NeosVR
                Config config = Config.LoadConfig();
                config.LauncherArguments = launcherArguments;
                config.SaveConfig();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to launch NeosVR: {ex.Message}");
            }
        }
    }
}
