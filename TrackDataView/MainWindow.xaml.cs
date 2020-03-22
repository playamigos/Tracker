using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Path = System.IO.Path;

namespace TrackDataView
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
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private TrackData currentData;
        public string serverRootPath, serverLogPath, currentPath;
        public string userName, password;

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool LogonUser(String lpszUsername, String lpszDomain, String lpszPassword, int dwLogonType, int dwLogonProvider, out SafeTokenHandle phToken);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public extern static bool CloseHandle(IntPtr handle);
        MainWindow logWindow;
        Button currentDateBtn,currentTab; 
        BrushConverter brsh = new BrushConverter();
        bool isFileOpened = false;


        public MainWindow()
        {
            InitializeComponent();

            var lines = File.ReadAllLines("Credentials.txt");
            serverRootPath = lines[0];
            userName = lines[1];
            password = lines[2];
            FolderContainerScroller.Visibility = Visibility.Visible;
            LeftBar.Visibility = Visibility.Collapsed;
            RightBar.Visibility = Visibility.Collapsed;
            OpenConnectionAndSync(serverRootPath);
        }
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (serverRootPath != currentPath)
                {
                    string p = currentPath.TrimEnd(System.IO.Path.DirectorySeparatorChar);
                    if (isFileOpened)
                    {
                        p = System.IO.Path.GetDirectoryName(p);
                        isFileOpened = false;
                    }
                    p = System.IO.Path.GetDirectoryName(p);
                    OpenConnectionAndSync(p);
                }
            }
            else if (e.Key == Key.OemTilde)
            {
                logWindow = new TrackDataView.MainWindow();
                logWindow.Show();
            }
        }

        public async void OpenConnectionAndSync(string fileName)
        {
            await Task.Delay(1);
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
                            Task<bool> task = Task.Run(() => {
                                return IsServerAvailable();
                            });
                            if(task.Wait(400))
                            {
                                if (task.Result)
                                {
                                    loadScreen(fileName);
                                }
                                else
                                {
                                    FolderContainer.Children.Clear();
                                    Button btn = new Button();
                                    btn.Content = "Retry";
                                    btn.Name = "retryBtn";
                                    btn.Tag = serverRootPath;
                                    btn.Click += FolderClicked;
                                    FolderContainer.Children.Add(btn);
                                }
                            }
                            else
                            {
                                FolderContainer.Children.Clear();
                                Button btn = new Button();
                                btn.Content = "Retry";
                                btn.Name = "retryBtn";
                                btn.Tag = serverRootPath;
                                btn.Click += FolderClicked;
                                FolderContainer.Children.Add(btn);
                            }

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

        public async void FileDataLoad()
        {
            await Task.Delay(1);
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
                            Task<bool> task = Task.Run(() => {
                                return IsServerAvailable();
                            });
                            if (task.Wait(400))
                            {
                                if (task.Result)
                                {
                                    string json = File.ReadAllText(currentPath);
                                    currentData = new JavaScriptSerializer().Deserialize<TrackData>(json);

                                    calculateAppUsage();
                                    updateTimeline();
                                    LoadTasks();
                                }
                                else
                                {
                                    Debug.Print("file load failed");
                                }
                            }
                            else
                            {
                                Debug.Print("file load failed");
                            }

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

        private void updateTimeline()
        {
            Timeline.Children.Clear();
            int min = 0;
            float factor = 1100f / 1440f;
            for (int i = 0; i < currentData.atStartTime.Count; i++)
            {
                min += currentData.atDuration[i];
                Rectangle rc = new Rectangle();
                if (i == 0)
                {
                    rc.Margin = new Thickness(currentData.atStartTime[i]*factor, 0, 0, 0);
                }
                else
                {
                    rc.Margin = new Thickness((currentData.atStartTime[i] - (currentData.atStartTime[i-1] + currentData.atDuration[i-1]))*factor, 0, 0, 0);
                }
                rc.Width = factor*currentData.atDuration[i];
                Timeline.Children.Add(rc);
            }
            WorkTimeHolder.Content = ConvertMinToTime(min).Substring(0,7);
        }

        public bool IsServerAvailable()
        {
            try
            {
                if (Directory.Exists(serverRootPath))
                {
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public void loadScreen(string fileName)
        {
            try
            {
                string[] dirs = Directory.GetDirectories(fileName);
                if (dirs.Length != 0)
                {
                    FolderContainerScroller.Visibility = Visibility.Visible;
                    LeftBar.Visibility = Visibility.Collapsed;
                    RightBar.Visibility = Visibility.Collapsed;

                    currentPath = fileName + "\\";
                    this.Title = currentPath;
                    FolderContainer.Children.Clear();
                    for (int i = 0; i < dirs.Length; i++)
                    {
                        Button btn = new Button();
                        btn.Content = Path.GetFileName(dirs[i]);
                        btn.Name = "btn" + i.ToString();
                        btn.Tag = dirs[i];
                        btn.Click += FolderClicked;
                        FolderContainer.Children.Add(btn);
                    }
                }
                else
                {
                    FolderContainerScroller.Visibility = Visibility.Collapsed;
                    LeftBar.Visibility = Visibility.Visible;
                    RightBar.Visibility = Visibility.Visible;
                    FileBtnsHolder.Children.Clear();
                    dirs = Directory.GetFiles(fileName);
                    for (int i = 0; i < dirs.Length; i++)
                    {
                        Button btn = new Button();
                        btn.Content = Path.GetFileName(dirs[i]).Substring(0,2);
                        btn.Name = "dateBtn" + i.ToString();
                        btn.Tag = dirs[i];
                        btn.Click += FileClicked;
                        Grid.SetColumn(btn, i%7);
                        Grid.SetRow(btn, (int)Math.Floor(i/7f));
                        FileBtnsHolder.Children.Add(btn);
                        if (i == 0)
                        {
                            MonthHolder.Content = Path.GetFileName(dirs[0]).Substring(2, 3) + " " + Path.GetFileName(dirs[0]).Substring(5);
                            currentDateBtn = btn;
                            currentDateBtn.Background = new SolidColorBrush(Colors.White);
                        }
                    }
                    currentPath = dirs[0];
                    calculateAverageTime(fileName);

                    string json = File.ReadAllText(dirs[0]);
                    currentData = new JavaScriptSerializer().Deserialize<TrackData>(json);


                    calculateAppUsage();
                    updateTimeline();
                    LoadTasks();
                }

            }
            catch (Exception ex)
            {
            }
        }

        private void calculateAverageTime(String fileName)
        {
            isFileOpened = true;
            int totalMin = 0;
            String[] dirs = Directory.GetFiles(fileName);
            for (int i = 0; i < dirs.Length; i++)
            {
                string json = File.ReadAllText(dirs[i]);
                currentData = new JavaScriptSerializer().Deserialize<TrackData>(json);
                totalMin += currentData.atDuration.Sum();
            }
            int avgMin = totalMin / dirs.Length;
            AverageTimeHolder.Content = ((int)Math.Floor(avgMin / 60f)).ToString("00") + " : " + (avgMin % 60).ToString("00") ;
        }

        private void calculateAppUsage()
        {
            AppNameHolder.Children.Clear();
            AppPercentageHolder.Children.Clear();
            AppGraphicHolder.Children.Clear();

            List<int> duration = new List<int>();
            List<String> appName = new List<String>();

            for (int i = 0; i < currentData.aaApp.Count; i++)
            {
                if(appName.Contains(currentData.aaApp[i]))
                {
                    int idx = appName.IndexOf(currentData.aaApp[i]);
                    duration[idx] += currentData.aaDuration[i];
                }
                else
                {
                    appName.Add(currentData.aaApp[i]);
                    duration.Add(currentData.aaDuration[i]);
                }
            }

            int totalDur = duration.Sum();
            for (int i = 0; i < duration.Count; i++)
            {
                duration[i] = (int)Math.Round( (double)((duration[i]*100f) / (double)totalDur) );
            }
            float perPercentSize = 180f/duration.Max();

            //sort
            for (int i = 0; i < duration.Count; i++)
            {
                for (int j = i; j < duration.Count; j++)
                {
                    if(duration[j] > duration[i])
                    {
                        int d;
                        string n;
                        d = duration[i]; duration[i] = duration[j]; duration[j] = d;
                        n = appName[i]; appName[i] = appName[j]; appName[j] = n;
                    }
                }
            }

            for (int i = 0; i < appName.Count; i++)
            {
                Label lb = new Label();
                lb.Content = appName[i].Substring(0, Math.Min(appName[i].Length, 25));
                AppNameHolder.Children.Add(lb);

                lb = new Label();
                lb.Content = duration[i] + "%";
                AppPercentageHolder.Children.Add(lb);

                Rectangle rc = new Rectangle();
                rc.Width = (perPercentSize * duration[i]);
                AppGraphicHolder.Children.Add(rc);
            }
        }

        private void TasksClicked(object sender, RoutedEventArgs e)
        {
            LoadTasks();
        }
        private void aaClicked(object sender, RoutedEventArgs e)
        {
            LoadAppActivity();
        }
        private void raaClicked(object sender, RoutedEventArgs e)
        {
            LoadRawAppActivity();
        }

        private void LoadTasks()
        {
            if(currentTab != null)
            {
                currentTab.Background = new SolidColorBrush(Colors.White);
            }
            currentTab = taskBtn;
            currentTab.Background = new SolidColorBrush(Colors.GreenYellow);


            Grid gd = new Grid();
            gd.Margin = new Thickness(30, 30, 30, 30);

            // Define the Columns
            ColumnDefinition colDef1 = new ColumnDefinition();
            ColumnDefinition colDef2 = new ColumnDefinition();
            colDef1.Width = new GridLength(200, GridUnitType.Star);
            colDef2.Width = new GridLength(1100, GridUnitType.Star);
            gd.ColumnDefinitions.Add(colDef1);
            gd.ColumnDefinitions.Add(colDef2);

            // Define the Rows
            for (int i = 0; i < currentData.ttStartTime.Count; i++)
            {
                RowDefinition rowDef = new RowDefinition();
                rowDef.Height = new GridLength(60);
                gd.RowDefinitions.Add(rowDef);
            }

            // Add the first text cell to the Grid
            for (int i = 0; i < currentData.ttStartTime.Count; i++)
            {

                Border myBorder1 = new Border();
                gd.Children.Add(myBorder1);
                Grid.SetColumn(myBorder1, 0);
                Grid.SetRow(myBorder1, i);

                myBorder1 = new Border();
                gd.Children.Add(myBorder1);
                Grid.SetColumn(myBorder1, 1);
                Grid.SetRow(myBorder1, i);

                Label txt1 = new Label();
                txt1.Content = ConvertMinToTime(currentData.ttStartTime[i]);
                txt1.HorizontalContentAlignment = HorizontalAlignment.Center;
                gd.Children.Add(txt1);
                Grid.SetColumn(txt1, 0);
                Grid.SetRow(txt1, i);

                Label txt2 = new Label();
                txt2.Content = currentData.ttTitle[i];
                txt2.Margin = new Thickness(10, 0, 0, 0);
                gd.Children.Add(txt2);
                Grid.SetColumn(txt2, 1);
                Grid.SetRow(txt2, i);
            }

            TableHolder.Content = gd;
        }

        private void LoadAppActivity()
        {
            if (currentTab != null)
            {
                currentTab.Background = new SolidColorBrush(Colors.White);
            }
            currentTab = aaBtn;
            currentTab.Background = new SolidColorBrush(Colors.GreenYellow);

            List<int> duration = new List<int>();
            List<String> appName = new List<String>();
            List<String> title = new List<String>();

            for (int i = 0; i < currentData.aaApp.Count; i++)
            {
                if (title.Contains(currentData.aaTitle[i]))
                {
                    int idx = title.IndexOf(currentData.aaTitle[i]);
                    duration[idx] += currentData.aaDuration[i];
                }
                else
                {
                    appName.Add(currentData.aaApp[i]);
                    duration.Add(currentData.aaDuration[i]);
                    title.Add(currentData.aaTitle[i]);
                }
            }
            //sort
            for (int i = 0; i < duration.Count; i++)
            {
                for (int j = i; j < duration.Count; j++)
                {
                    if (duration[j] > duration[i])
                    {
                        int d;
                        string n;
                        string m;
                        d = duration[i]; duration[i] = duration[j]; duration[j] = d;
                        n = appName[i]; appName[i] = appName[j]; appName[j] = n;
                        m = title[i]; title[i] = title[j]; title[j] = m;
                    }
                }
            }



            Grid gd = new Grid();
            gd.Margin = new Thickness(30, 30, 30, 30);

            // Define the Columns
            ColumnDefinition colDef1 = new ColumnDefinition();
            ColumnDefinition colDef2 = new ColumnDefinition();
            ColumnDefinition colDef3 = new ColumnDefinition();
            colDef1.Width = new GridLength(200, GridUnitType.Star);
            colDef2.Width = new GridLength(200, GridUnitType.Star);
            colDef3.Width = new GridLength(900, GridUnitType.Star);
            gd.ColumnDefinitions.Add(colDef1);
            gd.ColumnDefinitions.Add(colDef2);
            gd.ColumnDefinitions.Add(colDef3);

            // Define the Rows
            for (int i = 0; i < duration.Count; i++)
            {
                RowDefinition rowDef = new RowDefinition();
                rowDef.Height = new GridLength(60);
                gd.RowDefinitions.Add(rowDef);
            }

            // Add the first text cell to the Grid
            for (int i = 0; i < duration.Count; i++)
            {

                Border myBorder1 = new Border();
                gd.Children.Add(myBorder1);
                Grid.SetColumn(myBorder1, 0);
                Grid.SetRow(myBorder1, i);

                myBorder1 = new Border();
                gd.Children.Add(myBorder1);
                Grid.SetColumn(myBorder1, 1);
                Grid.SetRow(myBorder1, i);

                myBorder1 = new Border();
                gd.Children.Add(myBorder1);
                Grid.SetColumn(myBorder1, 2);
                Grid.SetRow(myBorder1, i);
                
                Label txt1 = new Label();
                txt1.Content = duration[i] + " min"; ;
                txt1.HorizontalContentAlignment = HorizontalAlignment.Center;
                gd.Children.Add(txt1);
                Grid.SetColumn(txt1, 0);
                Grid.SetRow(txt1, i);

                txt1 = new Label();
                txt1.Content = appName[i];
                txt1.HorizontalContentAlignment = HorizontalAlignment.Center;
                gd.Children.Add(txt1);
                Grid.SetColumn(txt1, 1);
                Grid.SetRow(txt1, i);

                Label txt2 = new Label();
                txt2.Content = title[i];
                txt2.Margin = new Thickness(10, 0, 0, 0);
                gd.Children.Add(txt2);
                Grid.SetColumn(txt2, 3);
                Grid.SetRow(txt2, i);
            }

            TableHolder.Content = gd;
        }

        private void LoadRawAppActivity()
        {
            if (currentTab != null)
            {
                currentTab.Background = new SolidColorBrush(Colors.White);
            }
            currentTab = raaBtn;
            currentTab.Background = new SolidColorBrush(Colors.GreenYellow);


            Grid gd = new Grid();
            gd.Margin = new Thickness(30, 30, 30, 30);

            // Define the Columns
            ColumnDefinition colDef1 = new ColumnDefinition();
            ColumnDefinition colDef2 = new ColumnDefinition();
            ColumnDefinition colDef3 = new ColumnDefinition();
            ColumnDefinition colDef4 = new ColumnDefinition();
            colDef1.Width = new GridLength(200, GridUnitType.Star);
            colDef2.Width = new GridLength(200, GridUnitType.Star);
            colDef3.Width = new GridLength(200, GridUnitType.Star);
            colDef4.Width = new GridLength(700, GridUnitType.Star);
            gd.ColumnDefinitions.Add(colDef1);
            gd.ColumnDefinitions.Add(colDef2);
            gd.ColumnDefinitions.Add(colDef3);
            gd.ColumnDefinitions.Add(colDef4);

            // Define the Rows
            for (int i = 0; i < currentData.aaStartTime.Count; i++)
            {
                RowDefinition rowDef = new RowDefinition();
                rowDef.Height = new GridLength(60);
                gd.RowDefinitions.Add(rowDef);
            }

            // Add the first text cell to the Grid
            for (int i = 0; i < currentData.aaStartTime.Count; i++)
            {

                Border myBorder1 = new Border();
                gd.Children.Add(myBorder1);
                Grid.SetColumn(myBorder1, 0);
                Grid.SetRow(myBorder1, i);

                myBorder1 = new Border();
                gd.Children.Add(myBorder1);
                Grid.SetColumn(myBorder1, 1);
                Grid.SetRow(myBorder1, i);

                myBorder1 = new Border();
                gd.Children.Add(myBorder1);
                Grid.SetColumn(myBorder1, 2);
                Grid.SetRow(myBorder1, i);

                myBorder1 = new Border();
                gd.Children.Add(myBorder1);
                Grid.SetColumn(myBorder1, 3);
                Grid.SetRow(myBorder1, i);

                Label txt1 = new Label();
                txt1.Content = ConvertMinToTime(currentData.aaStartTime[i]);
                txt1.HorizontalContentAlignment = HorizontalAlignment.Center;
                gd.Children.Add(txt1);
                Grid.SetColumn(txt1, 0);
                Grid.SetRow(txt1, i);

                txt1 = new Label();
                txt1.Content = currentData.aaDuration[i] + " min";
                txt1.HorizontalContentAlignment = HorizontalAlignment.Center;
                gd.Children.Add(txt1);
                Grid.SetColumn(txt1, 1);
                Grid.SetRow(txt1, i);

                txt1 = new Label();
                txt1.Content = currentData.aaApp[i];
                txt1.HorizontalContentAlignment = HorizontalAlignment.Center;
                gd.Children.Add(txt1);
                Grid.SetColumn(txt1, 2);
                Grid.SetRow(txt1, i);

                Label txt2 = new Label();
                txt2.Content = currentData.aaTitle[i];
                txt2.Margin = new Thickness(10, 0, 0, 0);
                gd.Children.Add(txt2);
                Grid.SetColumn(txt2, 3);
                Grid.SetRow(txt2, i);
            }

            TableHolder.Content = gd;
        }

        private string ConvertMinToTime(int min)
        {
            int h = (int) Math.Floor( (float)min/60f);
            int m = min % 60;
            if (h > 12)
            {
                h = h - 12;
                return h.ToString("00") + " : " + m.ToString("00") + " pm";
            }
            else
            {
                return h.ToString("00") + " : " + m.ToString("00") + " am";
            }
        }

        private void FileClicked(object sender, RoutedEventArgs e)
        {
            currentDateBtn.Background = (Brush)brsh.ConvertFrom("#ff7");
            currentDateBtn = (Button)sender;
            currentDateBtn.Background = new SolidColorBrush(Colors.White);
            
            currentPath = currentDateBtn.Tag.ToString();
            FileDataLoad();
        }

        private void FolderClicked(object sender, RoutedEventArgs e)
        {
            Button b = (Button)sender;
            OpenConnectionAndSync(b.Tag.ToString());
        }
    }
}
