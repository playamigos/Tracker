using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.ComponentModel;
using System.IO;
using System.Web.Script.Serialization;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;
using System.Runtime.ConstrainedExecution;
using System.Security;
using System.Threading.Tasks;
using System.Reflection;

namespace Tracker
{
    public sealed class SafeTokenHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private SafeTokenHandle()
            : base(true)
        {
        }

        [DllImport("kernel32.dll")]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        [SuppressUnmanagedCodeSecurity]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);

        protected override bool ReleaseHandle()
        {
            return CloseHandle(handle);
        }
    }
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 
   
    public partial class MainWindow : Window
    {
        private DispatcherTimer _dispatcherTimer, _syncTimer;
        private ClientIdleHandler _clientIdleHandler;
        public string currentPath,syncedPath,appPath,hostPath,backUpHostPath;
        private TrackData currentData;
        private int taskStartTime = 0;
        public string serverRootPath, serverPcPath, serverBlockerPath, serverLogPath;
        public string userName, password;
        public bool immediateUpdateNeeded = false;
        TrackDataView.MainWindow logWindow;
        
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool LogonUser(String lpszUsername, String lpszDomain, String lpszPassword, int dwLogonType, int dwLogonProvider, out SafeTokenHandle phToken);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public extern static bool CloseHandle(IntPtr handle);




        public MainWindow()
        {
            if (!IsRunAsAdministrator())
            {
                var processInfo = new ProcessStartInfo(Assembly.GetExecutingAssembly().CodeBase);

                // The following properties run the new process as administrator
                processInfo.UseShellExecute = true;
                processInfo.Verb = "runas";

                // Start the new process
                try
                {
                    Process.Start(processInfo);
                }
                catch (Exception)
                {
                    // The user did not allow the application to run as administrator
                    MessageBox.Show("Sorry, this application must be run as Administrator.");
                }

                // Shut down the current process
                Application.Current.Shutdown();
            }
            else
            {
                InitializeComponent();
                MouseDown += Window_MouseDown;
                appPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                hostPath = Path.Combine(appPath, @"Tracker\Blocker");
                backUpHostPath = Path.Combine(appPath, @"Tracker\hostBackup");
                currentPath = Path.Combine(appPath, @"Tracker\current\");
                syncedPath = Path.Combine(appPath, @"Tracker\synced\");

                var lines = File.ReadAllLines("Credentials.txt");
                serverRootPath = lines[0];
                userName = lines[1];
                password = lines[2];
                serverPcPath = Path.Combine(serverRootPath, Environment.MachineName);
                serverBlockerPath = Path.Combine(serverPcPath, "blocker.txt");
                InitializeData();
            }
        }

        private bool IsRunAsAdministrator()
        {
            var wi = WindowsIdentity.GetCurrent();
            var wp = new WindowsPrincipal(wi);

            return wp.IsInRole(WindowsBuiltInRole.Administrator);
        }

        //[PermissionSetAttribute(SecurityAction.Demand, Name = "FullTrust")]
        public void OpenConnectionAndSync(object sender, EventArgs e)
        {
            SafeTokenHandle safeTokenHandle;
            try
            {
                string domainName;
                domainName = ".";

                const int LOGON32_PROVIDER_WinNT50 = 3;
                //This parameter causes LogonUser to create a primary token.
                const int LOGON32_LOGON_NewCredentials = 9;

                // Call LogonUser to obtain a handle to an access token.
                bool returnValue = LogonUser(userName, domainName, password,
                    LOGON32_LOGON_NewCredentials, LOGON32_PROVIDER_WinNT50,
                    out safeTokenHandle);

                if (!returnValue)
                {
                    int ret = Marshal.GetLastWin32Error();
                    Console.WriteLine("LogonUser failed with error code : {0}", ret);
                    throw new System.ComponentModel.Win32Exception(ret);
                }
                using (safeTokenHandle)
                {
                    // Use the token handle returned by LogonUser.
                    using (WindowsIdentity newId = new WindowsIdentity(safeTokenHandle.DangerousGetHandle()))
                    {
                        using (WindowsImpersonationContext impersonatedUser = newId.Impersonate())
                        {
                            var task = new Task<bool>(() =>
                            {
                                return sync();
                            });
                            task.Start();
                            //return task.Wait(1) && task.Result;
                            //sync();
                            impersonatedUser.Undo();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception occurred. " + ex.Message);
            }
        }

        public bool sync()
        {
            try
            {
                if (Directory.Exists(serverRootPath))
                {
                    Directory.CreateDirectory(serverPcPath);

                    if (File.Exists(serverBlockerPath))
                    {
                        File.WriteAllText(hostPath, File.ReadAllText(serverBlockerPath));
                    }
                    else
                    {
                        //Debug.Print(serverBlockerPath);
                    }
                    UpdateHostsFile();

                    serverLogPath = Path.Combine(serverPcPath, getMonth());
                    Directory.CreateDirectory(serverLogPath);

                    String[] fs = Directory.GetFiles(currentPath);

                    for (int i = 0; i < fs.Length; i++)
                    {
                        string n = Path.GetFileName(fs[i]);
                        if (n == getDate())
                        {
                            File.WriteAllText(Path.Combine(serverLogPath, n), File.ReadAllText(fs[i]));
                        }
                        else
                        {
                            String newPath = n.Substring(2);
                            newPath = Path.Combine(serverPcPath, newPath);
                            Directory.CreateDirectory(newPath);
                            File.WriteAllText(Path.Combine(newPath, n), File.ReadAllText(fs[i]));
                            File.Move(fs[i], Path.Combine(syncedPath, n));
                        }
                    }
                    return true;
                }
                Debug.Print("syncFailed");
                return false;
            }
            catch(Exception ex)
            {
                Debug.Print("syncFailedwith error");
                return false;
            }
        }

        public void OpenOnWindowsStart()
        {
            try
            {
                Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                Assembly curAssembly = Assembly.GetExecutingAssembly();
                key.SetValue(curAssembly.GetName().Name, curAssembly.Location);
                Debug.Print(curAssembly.GetName().Name);
                Debug.Print(curAssembly.Location);
            }
            catch { }
        }
        public void InitializeData()
        {
            //OpenOnWindowsStart();
            Directory.CreateDirectory(syncedPath);
            Directory.CreateDirectory(currentPath);
            backUpHostsFile();

            if (File.Exists( Path.Combine(currentPath, getDate()) ))
            {
                string json = File.ReadAllText( Path.Combine(currentPath, getDate()) );
                currentData = new JavaScriptSerializer().Deserialize<TrackData>(json);
            }
            else
            {
                currentData = new TrackData();
            }
            int c = currentData.ttTitle.Count;
            if (c>0)
            {
                TaskName.Text = currentData.ttTitle[c - 1];
                taskStartTime = currentData.ttStartTime[c - 1];
                SetTaskDuration();
            }
        }

        public void UpdateFiles()
        {
            if (!immediateUpdateNeeded)
            {
                if (!File.Exists(Path.Combine(currentPath, getDate())))
                {
                    currentData = new TrackData();
                    TaskName.Text = "Assign Task";
                    taskStartTime = 0;
                }
            }
            immediateUpdateNeeded = false;
            string json = new JavaScriptSerializer().Serialize(currentData);
            File.WriteAllText(Path.Combine(currentPath, getDate()), json);
        }

        private void OnClosing(object sender, CancelEventArgs e)
        {
            //MessageBoxResult result = MessageBox.Show("Really close?", "Warning", MessageBoxButton.YesNo);
            //if (result != MessageBoxResult.Yes)
            //{
            //    e.Cancel = true;
            //}
            e.Cancel = true;
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.OemTilde)
            {
                logWindow = new TrackDataView.MainWindow();
                logWindow.Show();
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //start client idle hook
            _clientIdleHandler = new ClientIdleHandler();
            _clientIdleHandler.Start();

            //start timer 
            _dispatcherTimer = new DispatcherTimer();
            _dispatcherTimer.Tick += ActivityTick;
            _dispatcherTimer.Interval = new TimeSpan(0, 0, 1, 0);
            _dispatcherTimer.Start();


            _syncTimer = new DispatcherTimer();
            _syncTimer.Tick += OpenConnectionAndSync;
            _syncTimer.Interval = new TimeSpan(0, 0, 2, 30);
            _syncTimer.Start();
        }

        private void ActivityTick(object sender, EventArgs e)
        {
            SetTaskDuration();
            RecordActivity();
            UpdateFiles();
        }

        private int getTimeInMin()
        {
            var dt = DateTime.Now;
            return dt.Hour * 60 + dt.Minute;
        }

        private String getDate()
        {
            var dt = DateTime.Now;
            return dt.ToString("ddMMMyyyy");
        }

        private String getMonth()
        {
            var dt = DateTime.Now;
            return dt.ToString("MMMyyyy");
        }

        private void AllowTaskEdit(object sender, MouseButtonEventArgs e)
        {
            TaskNameEditor.Text = TaskName.Text;
            SetTaskBtn.Visibility = Visibility.Visible;
            TaskNameEditor.Visibility = Visibility.Visible;
            cancelTaskBtn.Visibility = Visibility.Visible;
            TaskName.Visibility = Visibility.Hidden;
        }

        private void cancelTaskBtn_Click(object sender, RoutedEventArgs e)
        {
            SetTaskBtn.Visibility = Visibility.Hidden;
            TaskNameEditor.Visibility = Visibility.Hidden;
            cancelTaskBtn.Visibility = Visibility.Hidden;
            TaskName.Visibility = Visibility.Visible;
        }

        private void SetTaskBtn_Click(object sender, RoutedEventArgs e)
        {

            SetTaskBtn.Visibility = Visibility.Hidden;
            TaskNameEditor.Visibility = Visibility.Hidden;
            cancelTaskBtn.Visibility = Visibility.Hidden;
            TaskName.Visibility = Visibility.Visible;
            string t = CleanString(TaskNameEditor.Text);
            if(t.Replace(" ", string.Empty) == "")
            {
                return;
            }
            int c = currentData.ttTitle.Count;
            if (c>0)
            {
                if (currentData.ttTitle[c - 1] == t)
                {
                    return;
                }
                else
                {
                    currentData.ttTitle.Add(t);
                    currentData.ttStartTime.Add(getTimeInMin());
                    taskStartTime = getTimeInMin();
                }
            }
            else
            {
                currentData.ttTitle.Add(t);
                currentData.ttStartTime.Add(getTimeInMin());
                taskStartTime = getTimeInMin();
            }
            TaskName.Text = t;
            SetTaskDuration();
            immediateUpdateNeeded = true;
            UpdateFiles();
        }

        private void SetTaskDuration()
        {
            if (currentData.ttTitle.Count > 0)
            {
                if (getTimeInMin() >= taskStartTime)
                {
                    CounterTxt.Text = (getTimeInMin() - taskStartTime).ToString() + " min";
                }
                else
                {
                    CounterTxt.Text = "0 min";
                    TaskName.Text = "Assign Task";
                }

            }
        }

        private void RecordActivity()
        {
            //mouse and keyboard activity
            int c = 0;
            if (_clientIdleHandler.IsActive)
            {
                c = currentData.atStartTime.Count;
                if (c > 0)
                {
                    if (currentData.atStartTime[c - 1] + currentData.atDuration[c - 1] + 1 >= getTimeInMin())
                        currentData.atDuration[c - 1]++;
                    else
                    {
                        currentData.atStartTime.Add(getTimeInMin() - 1);
                        currentData.atDuration.Add(1);
                    }
                }
                else
                {
                    currentData.atStartTime.Add(getTimeInMin() - 1);
                    currentData.atDuration.Add(1);
                }
                //reset IsActive flag
                _clientIdleHandler.IsActive = false;
            }

            //App activity
            c = currentData.aaApp.Count;
            string pn = GetActiveProcessName();
            string pt = GetActiveProcessTitle();
            if (c > 0)
            {
                if (currentData.aaTitle[c - 1] == pt && currentData.aaStartTime[c - 1] + currentData.aaDuration[c - 1] + 1 >= getTimeInMin())
                    currentData.aaDuration[c - 1]++;
                else
                {
                    currentData.aaStartTime.Add(getTimeInMin() - 1);
                    currentData.aaDuration.Add(1);
                    currentData.aaApp.Add(pn);
                    currentData.aaTitle.Add(pt);
                }
            }
            else
            {
                currentData.aaStartTime.Add(getTimeInMin() - 1);
                currentData.aaDuration.Add(1);
                currentData.aaApp.Add(pn);
                currentData.aaTitle.Add(pt);
            }
        }

        private string CleanString(string s)
        {
            return System.Text.RegularExpressions.Regex.Replace(s, @"[^a-zA-Z0-9\-\s]", "");
        }

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindowThreadProcessId(IntPtr hWnd, out uint ProcessId);

        private string GetActiveProcessTitle()
        {
            const int nChars = 256;
            IntPtr handle;
            StringBuilder Buff = new StringBuilder(nChars);
            handle = GetForegroundWindow();
            if (GetWindowText(handle, Buff, nChars) > 0)
            {
               return Buff.ToString();
            }
            else
            {
                return "";
            }
        }

        private string GetActiveProcessName()
        {
            IntPtr hwnd = GetForegroundWindow();
            uint pid;
            GetWindowThreadProcessId(hwnd, out pid);
            return Process.GetProcessById((int)pid).ProcessName;
        }

        public void UpdateHostsFile()
        {
            try
            {
                if (File.Exists(hostPath))
                {
                    string hostData = File.ReadAllText(hostPath);
                    File.WriteAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts"), hostData);
                }
                else if(File.Exists(backUpHostPath))
                {
                    string hostData = File.ReadAllText(backUpHostPath);
                    File.WriteAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts"), hostData);
                }
                else
                {
                    File.WriteAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts"), "");
                }
            }
            catch (Exception ex)
            {
            }
        }

        public void backUpHostsFile()
        {
            if (!File.Exists(backUpHostPath))
            {
                string hostData = File.ReadAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts"));
                File.WriteAllText(backUpHostPath, hostData);
            }
        }

    }

    public class TrackData
    {
        public List<int> atStartTime = new List<int>();
        public List<int> atDuration = new List<int>();

        public List<int> aaStartTime = new List<int>();
        public List<int> aaDuration = new List<int>();
        public List<string> aaApp = new List<string>();
        public List<string> aaTitle = new List<string>();

        public List<int> ttStartTime = new List<int>();
        public List<string> ttTitle = new List<string>();
    }

    public class ClientIdleHandler : IDisposable
    {
        public bool IsActive { get; set; }

        int _hHookKbd;
        int _hHookMouse;

        public delegate int HookProc(int nCode, IntPtr wParam, IntPtr lParam);
        public event HookProc MouseHookProcedure;
        public event HookProc KbdHookProcedure;

        //Use this function to install thread-specific hook.
        [DllImport("user32.dll", CharSet = CharSet.Auto,
             CallingConvention = CallingConvention.StdCall)]
        public static extern int SetWindowsHookEx(int idHook, HookProc lpfn,
            IntPtr hInstance, int threadId);

        //Call this function to uninstall the hook.
        [DllImport("user32.dll", CharSet = CharSet.Auto,
             CallingConvention = CallingConvention.StdCall)]
        public static extern bool UnhookWindowsHookEx(int idHook);

        //Use this function to pass the hook information to next hook procedure in chain.
        [DllImport("user32.dll", CharSet = CharSet.Auto,
             CallingConvention = CallingConvention.StdCall)]
        public static extern int CallNextHookEx(int idHook, int nCode,
            IntPtr wParam, IntPtr lParam);

        //Use this hook to get the module handle, needed for WPF environment
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        public enum HookType : int
        {
            GlobalKeyboard = 13,
            GlobalMouse = 14
        }

        public int MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            //user is active, at least with the mouse
            IsActive = true;

            //just return the next hook
            return CallNextHookEx(_hHookMouse, nCode, wParam, lParam);
        }

        public int KbdHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            //user is active, at least with the keyboard
            IsActive = true;

            //just return the next hook
            return CallNextHookEx(_hHookKbd, nCode, wParam, lParam);
        }

        public void Start()
        {
            using (var currentProcess = Process.GetCurrentProcess())
            using (var mainModule = currentProcess.MainModule)
            {

                if (_hHookMouse == 0)
                {
                    // Create an instance of HookProc.
                    MouseHookProcedure = new HookProc(MouseHookProc);
                    // Create an instance of HookProc.
                    KbdHookProcedure = new HookProc(KbdHookProc);

                    //register a global hook
                    _hHookMouse = SetWindowsHookEx((int)HookType.GlobalMouse,
                                                  MouseHookProcedure,
                                                  GetModuleHandle(mainModule.ModuleName),
                                                  0);
                    if (_hHookMouse == 0)
                    {
                        Close();
                        throw new ApplicationException("SetWindowsHookEx() failed for the mouse");
                    }
                }

                if (_hHookKbd == 0)
                {
                    //register a global hook
                    _hHookKbd = SetWindowsHookEx((int)HookType.GlobalKeyboard,
                                                KbdHookProcedure,
                                                GetModuleHandle(mainModule.ModuleName),
                                                0);
                    if (_hHookKbd == 0)
                    {
                        Close();
                        throw new ApplicationException("SetWindowsHookEx() failed for the keyboard");
                    }
                }
            }
        }

        public void Close()
        {
            if (_hHookMouse != 0)
            {
                bool ret = UnhookWindowsHookEx(_hHookMouse);
                if (ret == false)
                {
                    throw new ApplicationException("UnhookWindowsHookEx() failed for the mouse");
                }
                _hHookMouse = 0;
            }

            if (_hHookKbd != 0)
            {
                bool ret = UnhookWindowsHookEx(_hHookKbd);
                if (ret == false)
                {
                    throw new ApplicationException("UnhookWindowsHookEx() failed for the keyboard");
                }
                _hHookKbd = 0;
            }
        }

        #region IDisposable Members

        public void Dispose()
        {
            if (_hHookMouse != 0 || _hHookKbd != 0)
                Close();
        }

        #endregion
    }

}
