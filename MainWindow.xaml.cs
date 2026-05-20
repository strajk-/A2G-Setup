using Localizer;
using System;
using System.IO;
using System.Windows;
using System.Globalization;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;

namespace A2G_Setup
{
    public partial class MainWindow : NotifyingWindow
    {
        private string _CDPath = @"D:\";
        public string CDPath
        {
            get => _CDPath;
            set => NotifyPropertyChanged(ref _CDPath, value);
        }

        private string _InstallPath = string.Empty;
        public string InstallPath
        {
            get => _InstallPath;
            set => NotifyPropertyChanged(ref _InstallPath, value);
        }

        private bool _IsInstalling = false;
        public bool IsInstalling
        {
            get => _IsInstalling;
            set => NotifyPropertyChanged(ref _IsInstalling, value);
        }

        private string _InstallStep = string.Empty;
        public string InstallStep
        {
            get => _InstallStep;
            set => NotifyPropertyChanged(ref _InstallStep, value);
        }

        public MainWindow()
        {
            this.DataContext = this;
            InitializeComponent();
            LocalizedText.Language = CultureInfo.CurrentCulture.Name.StartsWith("de") ? Localizer.Language.German : Localizer.Language.English;
            LocalizedText.InitLanguage();
        }

        private void btChangeLanguage(object sender, RoutedEventArgs e)
        {
            switch (LocalizedText.Language) {
                case Localizer.Language.German:
                    LocalizedText.Language = Localizer.Language.English;
                    break;
                case Localizer.Language.English:
                    LocalizedText.Language = Localizer.Language.German;
                    break;
            }
            if (IsInstalling && !string.IsNullOrEmpty(InstallStep)) {
                InstallStep = LocalizedText.GetText();
            }
        }

        private void btExit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void SetOriginalInstallPath()
        {
            Version? selectedVersion = (cbCDVersion.SelectedItem as ComboBoxItem).Tag as Version?;
            switch (selectedVersion) {
                case Version.A2G: {
                        InstallPath = @"C:\Program Files (x86)\ASCARON\ANSTOSS 2";
                    }
                    break;
                case Version.A2007: {
                        InstallPath = @"C:\Program Files (x86)\ASCARON Entertainment\ANSTOSS 2 Gold";
                    }
                    break;
            }
        }

        private CancellationTokenSource _cancellationTokenSource;
        private void btInstall_Click(object sender, RoutedEventArgs e)
        {
            // Check states, this has to be first since they switch to IsEnabled = false when IsInstalling is true
            bool useOTVDM = (bool)cbOTVDM.IsChecked && cbOTVDM.IsEnabled;
            bool useWined = (bool)cbWINED3D.IsChecked && cbWINED3D.IsEnabled;
            bool useKlite = (bool)cbKLITE.IsChecked && cbKLITE.IsEnabled;
            bool use16bitColor = (bool)cb16BIT.IsChecked && cb16BIT.IsEnabled;

            IsInstalling = true;

            string tempDir = Path.Combine(Path.GetTempPath(), "A2G_Setup_Temp");
            if (!Directory.Exists(tempDir)) {
                Directory.CreateDirectory(tempDir);
            }
            Version? selectedVersion = (cbCDVersion.SelectedItem as ComboBoxItem).Tag as Version?;

            string cdPath = string.Empty;
            string installParameters = string.Empty;
            switch (selectedVersion) {
                case Version.A2G: { // IS3 - 16 bit
                        cdPath = CDPath;
                        installParameters = "/s";
                        string issFile = Path.Combine(CDPath, "setup.iss");
                        if (File.Exists(issFile)) {
                            string tmpissFile = Path.Combine(tempDir, "setup.iss");
                            File.Copy(issFile, tmpissFile, true);
                            File.SetAttributes(tmpissFile, FileAttributes.Normal); // remove Read-Only flags

                            string issContent = File.ReadAllText(tmpissFile);

                            // Update szPath under [AskDestPath-0]
                            issContent = System.Text.RegularExpressions.Regex.Replace(
                                issContent,
                                @"(?<=szPath=).*",
                                InstallPath
                            );

                            // Update szDir under [SdSetupType-0]
                            issContent = System.Text.RegularExpressions.Regex.Replace(
                                issContent,
                                @"(?<=szDir=).*",
                                InstallPath
                            );

                            File.WriteAllText(tmpissFile, issContent);

                            installParameters += $" /f1\"{tmpissFile}\"";
                        }
                    }
                    break;
                case Version.A2007: { // Inno Setup
                        cdPath = Path.Combine(CDPath, "ANSTOSS_2_GOLD");
                        installParameters = $"/VERYSILENT /SUPPRESSMSGBOXES /DIR=\"{InstallPath}\"";
                    }
                    break;
            }
            

            // Check files
            if (!Directory.Exists(cdPath)) {
                ShowMessageFromTask(string.Format(LocalizedText.GetText(30000), cdPath), LocalizedText.GetText(20000), MessageBoxImage.Error);
                IsInstalling = false;
                return;
            }

            string cdSetupPath = Path.Combine(cdPath, "setup.exe");
            if (!File.Exists(cdSetupPath)) {
                ShowMessageFromTask(string.Format(LocalizedText.GetText(30001), "setup.exe"), LocalizedText.GetText(20000), MessageBoxImage.Error);
                IsInstalling = false;
                return;
            }

            // --- BACKGROUND INSTALLATION ---
            _cancellationTokenSource = new CancellationTokenSource();
            CancellationToken token = _cancellationTokenSource.Token;
            Task.Factory.StartNew(() => {
                bool success = false;

                if (selectedVersion == Version.A2G) {
                    token.ThrowIfCancellationRequested();
                    string otvdmZipPath = Path.Combine(tempDir, "otvdm.zip");
                    if (useOTVDM) {
                        InstallStep = LocalizedText.GetText(40000);
                        if (ExtractResource("A2G_Setup.SetupFiles.otvdm.otvdm.zip", otvdmZipPath)) {
                            // OTVDM paths
                            string stagingDir = Path.Combine(tempDir, "otvdm_staging");
                            string finalOtvdmFolder = @"C:\otvdm";

                            // Clean up staging dir if it exists from a previous failed run
                            if (Directory.Exists(stagingDir)) {
                                Directory.Delete(stagingDir, true);
                            }
                            Directory.CreateDirectory(stagingDir);

                            // Unzip to the temporary staging folder
                            if (ExtractZipFileStandard(otvdmZipPath, stagingDir)) {
                                try {
                                    // Dynamically find the root folder inside the extracted zip
                                    string[] subDirs = Directory.GetDirectories(stagingDir);
                                    if (subDirs.Length == 0) {
                                        ShowMessageFromTask(LocalizedText.GetText(30002), LocalizedText.GetText(20000), MessageBoxImage.Error);
                                        return false;
                                    }

                                    // Assume the zip contains exactly one root folder, regardless of version name
                                    string extractedRootFolder = subDirs[0];

                                    // Move and rename to C:\otvdm
                                    if (Directory.Exists(finalOtvdmFolder)) {
                                        Directory.Delete(finalOtvdmFolder, true);
                                    }

                                    Directory.Move(extractedRootFolder, finalOtvdmFolder);

                                    // Clean up the staging directory
                                    Directory.Delete(stagingDir, true);
                                }
                                catch (Exception ex) {
                                    ShowMessageFromTask(string.Format(LocalizedText.GetText(30003), $"{ex.Message}\n{ex.InnerException}"), LocalizedText.GetText(20000), MessageBoxImage.Error);
                                    return false;
                                }

                                // Install OTVDM by invoking the maintainer's shortcut
                                string shortcutPath = Path.Combine(finalOtvdmFolder, "install (no console).lnk");
                                if (File.Exists(shortcutPath)) {
                                    RunProcess(shortcutPath, "", finalOtvdmFolder);
                                    Thread.Sleep(1500); // Wait for OS to register the hook
                                } else {
                                    ShowMessageFromTask(string.Format(LocalizedText.GetText(30004), "install (no console).lnk"), LocalizedText.GetText(20000), MessageBoxImage.Error);
                                    return false;
                                }
                            } else {
                                ShowMessageFromTask(LocalizedText.GetText(30005), LocalizedText.GetText(20000), MessageBoxImage.Error);
                                return false;
                            }
                        } else {
                            ShowMessageFromTask(LocalizedText.GetText(30006), LocalizedText.GetText(20000), MessageBoxImage.Error);
                            return false;
                        }
                    }
                }

                // Run Anstoss 2 Gold Setup
                if (File.Exists(cdSetupPath)) {
                    token.ThrowIfCancellationRequested();
                    InstallStep = LocalizedText.GetText(40001);

                    RunProcess(cdSetupPath, installParameters, cdPath);

                    string a2exePath = Path.Combine(InstallPath, "anstoss2.exe");
                    string verlexePath = Path.Combine(InstallPath, "verl.exe");
                    string editorexePath = Path.Combine(InstallPath, "editor.exe");
                    success = File.Exists(a2exePath);
                    if (!success) {
                        ShowMessageFromTask(LocalizedText.GetText(30007), LocalizedText.GetText(20000), MessageBoxImage.Error);
                        return false;
                    } else {
                        InstallStep = LocalizedText.GetText(40002);

                        // Full access for everyone in the install, to prevent virtualization of tools running in it such as WineD3D or others to be added in the future
                        GrantFullAccessToFolder(InstallPath);

                        // Set compatibility flags required by the application
                        if (use16bitColor) {
                            InstallStep = LocalizedText.GetText(40003);
                            System.Version osVersion = Environment.OSVersion.Version;
                            if (osVersion.Major >= 10 || (osVersion.Major == 6 && osVersion.Minor >= 2)) {
                                // We are on Windows 8, 10, 11 or newer - apply 16-bit compatibility mode
                                SetCompatibilityFlags(a2exePath);
                                SetCompatibilityFlags(verlexePath);
                                SetCompatibilityFlags(editorexePath);
                            } else {
                                ShowMessageFromTask(LocalizedText.GetText(30008), LocalizedText.GetText(20001), MessageBoxImage.Warning);
                            }
                        }
                    }
                } else {
                    ShowMessageFromTask(string.Format(LocalizedText.GetText(30001), "setup.exe"), LocalizedText.GetText(20000), MessageBoxImage.Error);
                    return false;
                }

                if (success) {
                    token.ThrowIfCancellationRequested();
                    // Install K-Lite
                    string kliteExePath = Path.Combine(tempDir, "K-Lite_Codec_Pack.exe");
                    string kliteIniPath = Path.Combine(tempDir, "klcp_standard_unattended.ini");

                    if (useKlite) {
                        InstallStep = LocalizedText.GetText(40004);
                        if (ExtractResource("A2G_Setup.SetupFiles.K_Lite.K-Lite_Codec_Pack.exe", kliteExePath) &&
                            ExtractResource("A2G_Setup.SetupFiles.K_Lite.klcp_standard_unattended.ini", kliteIniPath)) {
                            RunProcess(kliteExePath, string.Format("/VERYSILENT /NORESTART /LOADINF=\"{0}\"", kliteIniPath));

                            // Force enable Indeo 3.2 via registry
                            EnableIndeo32(tempDir);
                        } else {
                            ShowMessageFromTask(LocalizedText.GetText(30009), LocalizedText.GetText(20000), MessageBoxImage.Error);
                        }
                    }

                    // Extract WineD3D
                    token.ThrowIfCancellationRequested();
                    if (useWined) {
                        InstallStep = LocalizedText.GetText(40005);
                        string wineZipPath = Path.Combine(tempDir, "WineD3D.zip");
                        if (ExtractResource("A2G_Setup.SetupFiles.WineD3D.WineD3D.zip", wineZipPath)) {
                            if (!ExtractZipFileStandard(wineZipPath, InstallPath)) {
                                ShowMessageFromTask(LocalizedText.GetText(30010), LocalizedText.GetText(20000), MessageBoxImage.Error);
                            }
                        }
                    }
                }

                return success;

            }).ContinueWith(t => {
                IsInstalling = false;
                InstallStep = string.Empty;

                if (t.IsCanceled || (t.Exception != null && t.Exception.InnerExceptions[0] is OperationCanceledException)) {
                    // The user cancelled the installation
                    ShowMessageFromTask(LocalizedText.GetText(30014), LocalizedText.GetText(20003), MessageBoxImage.Warning);
                } else if (t.IsFaulted) {
                    Exception ex = t.Exception.InnerException ?? t.Exception;
                    ShowMessageFromTask(string.Format(LocalizedText.GetText(30011), $"{ex.Message}\n{ex.InnerException}"), LocalizedText.GetText(20000), MessageBoxImage.Error);
                } else if (t.Result == true) {
                    ShowMessageFromTask(LocalizedText.GetText(30012), LocalizedText.GetText(20002), MessageBoxImage.Information);
                }

                CleanupTempDirectory(tempDir);
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        // Helper to safely show messages from the background Task
        private MessageBoxResult ShowMessageFromTask (string message, string title, MessageBoxImage image, MessageBoxButton button = MessageBoxButton.OK)
        {
            if (Application.Current == null || Dispatcher.CheckAccess()) {
                return MessageBox.Show(this, message, title, button, image);
            }

            return (MessageBoxResult)Dispatcher.Invoke(new Func<MessageBoxResult>(() => {
                return MessageBox.Show(this, message, title, button, image);
            }));
        }

        // Returns true if successful, false instead of crashing
        private bool ExtractResource (string resourceName, string destinationPath)
        {
            try {
                var assembly = Assembly.GetExecutingAssembly();
                using (Stream stream = assembly.GetManifestResourceStream(resourceName)) {
                    if (stream == null) return false;

                    using (FileStream fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write)) {
                        stream.CopyTo(fileStream);
                    }
                }
                return true;
            }
            catch (Exception ex) {
                ShowMessageFromTask($"{ex.Message}\n{ex.InnerException}", LocalizedText.GetText(20000), MessageBoxImage.Error);
                return false;
            }
        }

        private void CleanupTempDirectory (string dirPath)
        {
            try {
                if (Directory.Exists(dirPath)) {
                    Directory.Delete(dirPath, true);
                }
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Cleanup warning: Could not delete temp directory. {ex.Message}");
            }
        }

        private int RunProcess (string filePath, string arguments, string workingDirectory = "", CancellationToken token = default(CancellationToken))
        {
            using (var process = new Process()) {
                process.StartInfo.FileName = filePath;
                process.StartInfo.Arguments = arguments;
                process.StartInfo.WorkingDirectory = string.IsNullOrEmpty(workingDirectory) ? Path.GetDirectoryName(filePath) : workingDirectory;
                process.StartInfo.UseShellExecute = true;

                process.Start();

                while (!process.HasExited) {
                    if (token.IsCancellationRequested) {
                        try {
                            if (!process.HasExited) {
                                process.Kill(); // Force close the external installer
                            }
                        }
                        catch {
                            // Ignore exceptions if the process is already closing or access is denied
                        }
                        token.ThrowIfCancellationRequested();
                    }
                    Thread.Sleep(100);
                }

                return process.ExitCode;
            }
        }

        private void CopyDirectory(string sourceDir, string destDir)
        {
            foreach (string dirPath in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(dirPath.Replace(sourceDir, destDir));

            foreach (string newPath in Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories))
                File.Copy(newPath, newPath.Replace(sourceDir, destDir), true);
        }

        // Returns true if successful, false instead of crashing
        public bool ExtractZipFileStandard(string zipPath, string extractPath)
        {
            try {
                if (!Directory.Exists(extractPath)) {
                    Directory.CreateDirectory(extractPath);
                }

                Type shellAppType = Type.GetTypeFromProgID("Shell.Application");
                dynamic shell = Activator.CreateInstance(shellAppType);
                dynamic zipFolder = shell.NameSpace(zipPath);
                dynamic destFolder = shell.NameSpace(extractPath);

                if (zipFolder != null && destFolder != null) {
                    destFolder.CopyHere(zipFolder.Items(), 4 | 16 | 1024);
                    return true;
                }
                return false;
            }
            catch {
                return false;
            }
        }

        private void EnableIndeo32 (string tempDir)
        {
            try {
                string indeoZipPath = Path.Combine(tempDir, "Indeo.zip");
                if (ExtractResource("A2G_Setup.SetupFiles.Indeo.Indeo.zip", indeoZipPath)) {
                    if (ExtractZipFileStandard(indeoZipPath, tempDir)) {
                        string indeoFolder = Path.Combine(tempDir, "Indeo");

                        if (Directory.Exists(indeoFolder)) {
                            string sysWow64Path = Environment.GetFolderPath(Environment.SpecialFolder.SystemX86);

                            foreach (string sourceFilePath in Directory.GetFiles(indeoFolder)) {
                                string fileName = Path.GetFileName(sourceFilePath);
                                string destFilePath = Path.Combine(sysWow64Path, fileName);

                                // Copy if it doesn't already exist in SysWOW64
                                if (!File.Exists(destFilePath)) {
                                    File.Copy(sourceFilePath, destFilePath, false);
                                }
                            }
                        }
                    }
                }

                using (var baseKey = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry32)) {

                    using (var k = baseKey.CreateSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Drivers32")) {
                        if (k != null) k.SetValue("vidc.iv32", "ir32_32.dll");
                    }

                    using (var k = baseKey.CreateSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Drivers.Desc")) {
                        if (k != null) k.SetValue("ir32_32.dll", "Intel Indeo(R) Video R3.2");
                    }
                }
            }
            catch (Exception ex) {
                Debug.WriteLine("Registry Indeo Error: " + ex.Message);
            }
        }

        private void SetCompatibilityFlags (string exePath)
        {
            if (File.Exists(exePath)) {
                string keyPath = @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";

                string compatFlags = "~ WIN7RTM 16BITCOLOR";

                // Try HKCU first (no admin needed)
                using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(keyPath))
                    key?.SetValue(exePath, compatFlags);

                // Also try HKLM if running as admin
                try {
                    using (var key = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(keyPath))
                        key?.SetValue(exePath, compatFlags);
                }
                catch (UnauthorizedAccessException) {
                    // Not running as admin, HKCU only
                }
            }
        }

        private void GrantFullAccessToFolder (string folderPath)
        {
            try {
                if (!Directory.Exists(folderPath)) return;

                DirectoryInfo dirInfo = new DirectoryInfo(folderPath);
                DirectorySecurity dirSecurity = dirInfo.GetAccessControl();

                // WorldSid is the universal identifier for the "Everyone" group
                SecurityIdentifier everyoneSid = new SecurityIdentifier(WellKnownSidType.WorldSid, null);

                // Create a rule that grants Full Control and propagates to all child files/folders
                FileSystemAccessRule accessRule = new FileSystemAccessRule(
                    everyoneSid,
                    FileSystemRights.FullControl,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                    PropagationFlags.None,
                    AccessControlType.Allow
                );

                // Add the rule and apply it to the directory
                dirSecurity.AddAccessRule(accessRule);
                dirInfo.SetAccessControl(dirSecurity);
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine("Permission Error: " + ex.Message);
            }
        }

        private void btSelectCDPath_Click (object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog()) {
                dialog.Description = LocalizedText.GetText(10019);
                dialog.ShowNewFolderButton = false;

                // If CDPath has a value, start the dialog there
                if (!string.IsNullOrWhiteSpace(CDPath) && Directory.Exists(CDPath)) {
                    dialog.SelectedPath = CDPath;
                }

                var result = dialog.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK) {
                    CDPath = dialog.SelectedPath;
                }
            }
        }

        private void cbCDVersion_SelectionChanged (object sender, SelectionChangedEventArgs e)
        {
            // CD Version changed -> Display the original install path normally used by this release
            SetOriginalInstallPath();
        }

        private void btSelectInstallPath_Click (object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog()) {
                dialog.Description = LocalizedText.GetText(10021);
                dialog.ShowNewFolderButton = true;

                // If InstallPath has a value, start the dialog there
                if (!string.IsNullOrWhiteSpace(InstallPath) && Directory.Exists(InstallPath)) {
                    dialog.SelectedPath = InstallPath;
                }

                var result = dialog.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK) {
                    InstallPath = dialog.SelectedPath;
                }
            }
        }

        protected override void OnClosing (CancelEventArgs e)
        {
            if (IsInstalling) {
                var result = ShowMessageFromTask(LocalizedText.GetText(30013), LocalizedText.GetText(20001), MessageBoxImage.Warning, MessageBoxButton.YesNo);
                if (result == MessageBoxResult.No) {
                    e.Cancel = true;
                    return;
                }

                // Trigger the cancellation to kill any active processes
                _cancellationTokenSource?.Cancel();

                // Give the background task a tiny fraction of a second to catch the token and kill the sub-processes before the CLR shuts down
                Thread.Sleep(500);
            }

            base.OnClosing(e);
        }

        
    }

    public enum Version
    {
        A2G,
        A2007,
    }
}