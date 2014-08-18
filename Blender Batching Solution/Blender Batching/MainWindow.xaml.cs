using System;
using System.IO;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Diagnostics;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Runtime.InteropServices;
using Xceed.Wpf.Toolkit;

namespace Blender_Batching
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>

    public enum BlenderFormats
    {
        TGA, IRIS, JPEG, MOVIE, IRIZ, RAWTGA, AVIRAW, AVIJPEG, PNG, BMP, FRAMESERVER
    };
    public partial class MainWindow : Window
    {
        [DllImport("User32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImportAttribute("User32.DLL")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        public List<String> FormatsList { get; set; }
        ObservableCollection<BlendData> blendList;
        FolderBrowserDialog svdlg = new FolderBrowserDialog();
        OpenFileDialog openFileDlg = new OpenFileDialog();
        SaveFileDialog saveFileDlg = new SaveFileDialog();
        String blenderPath = "";
        String BlenderPath
        {
            get
            {
                return blenderPath;
            }
            set
            {
                blenderPath = value;
                statusSBITB.Text = value;
                saveAppData();
            }
        }
        CancellationTokenSource tokenSource = new CancellationTokenSource();
        CancellationToken token;
        ConcurrentBag<Task> taskBag = new ConcurrentBag<System.Threading.Tasks.Task>();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            FormatsList = new List<string>() { "FileSetting", "PNG", "TGA", "BMP", "JPEG", "RAWTGA", "IRIS", "MPEG", "IRIZ", "AVIRAW", "AVIJPEG", "FRAMESERVER" };
            svdlg.ShowNewFolderButton = false;
            //svdlg.RootFolder = Environment.SpecialFolder.MyComputer;
            openFileDlg.AddExtension = true;
            openFileDlg.DefaultExt = "blend";
            openFileDlg.Filter = "Blender files (*.blend) | *.blend";
            openFileDlg.Multiselect = true;
            openFileDlg.RestoreDirectory = true;

            saveFileDlg.AddExtension = true;
            saveFileDlg.DefaultExt = "bat";
            saveFileDlg.Filter = "Batch (*.bat) | *.bat";
            saveFileDlg.RestoreDirectory = true;
            saveFileDlg.OverwritePrompt = true;
            BlendData.FormatsList = FormatsList;
            blendList = new ObservableCollection<BlendData>();
            blendList.CollectionChanged += blendList_CollectionChanged;

            //formatcolumn.item .ItemsSource = formatList;
            blendGrid.ItemsSource = blendList;
            formatCB.DataContext = FormatsList;
            loadAppData();
        }

        #region Events

        void blendList_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            blendList.OrderBy(b => b.Folder).ThenBy(b => b.Name);
            blendGrid.ItemsSource = blendList;
        }

        private void useCB_Checked(object sender, RoutedEventArgs e)
        {
            changeSelected("Use", true);
        }

        private void useCB_Unchecked(object sender, RoutedEventArgs e)
        {
            changeSelected("Use", false);
        }

        private void threadsIUD_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            changeSelected("Threads", (int)((IntegerUpDown)sender).Value);
        }

        private void endIUD_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            changeSelected("End", (int)((IntegerUpDown)sender).Value);
        }

        private void startIUD_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            changeSelected("Start", (int)((IntegerUpDown)sender).Value);
        }

        private void formatCB_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            changeSelected("FormatID", ((System.Windows.Controls.ComboBox)sender).SelectedIndex);
        }

        private void maskTB_TextChanged(object sender, TextChangedEventArgs e)
        {
            changeSelected("FileMask", ((System.Windows.Controls.TextBox)sender).Text);
        }

        private void folderBtn_Click(object sender, RoutedEventArgs e)
        {

            if (svdlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                changeSelected("Output", svdlg.SelectedPath);
            }
        }

        private void outputTB_TextChanged(object sender, TextChangedEventArgs e)
        {
            changeSelected("Output", ((System.Windows.Controls.TextBox)sender).Text);

        }

        private void changeSelectedInt(object sender, TextChangedEventArgs e, String propertyName)
        {

            int val = 0;
            if (Int32.TryParse(((System.Windows.Controls.TextBox)sender).Text, out val))
            {
                changeSelected(propertyName, val);
            }
            else
            {
                e.Handled = true;
            }
        }

        private void addBlendBtn_Click(object sender, RoutedEventArgs e)
        {
            checkEditing();
            DialogResult result = openFileDlg.ShowDialog();
            List<FileInfo> fl = new List<FileInfo>();
            if (result == System.Windows.Forms.DialogResult.OK) // Test result.
            {
                foreach (String fn in openFileDlg.FileNames)
                {
                    fl.Add(new FileInfo(fn));
                }
                if (fl.Count > 0)
                {
                    getBlendsFromFolder(fl);
                    svdlg.SelectedPath = fl[0].DirectoryName;
                }
            }
        }

        private void addFoldersBtn_Click(object sender, RoutedEventArgs e)
        {
            checkEditing();
            if (svdlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                DirectoryInfo di = new DirectoryInfo(svdlg.SelectedPath);
                getBlendsFromFolder(di.EnumerateFiles("*.blend", (bool)recursiveChkBox.IsChecked ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly).OrderBy(x => x.Name));
            }
        }

        private void clearBtn_Click(object sender, RoutedEventArgs e)
        {
            checkEditing();
            blendList.Clear();
        }

        private void saveBtn_Click(object sender, RoutedEventArgs e)
        {
            checkEditing();
            if (blendList.Count > 0)
            {
                generateBatchFile();
            }
        }

        private void loadBtn_Click(object sender, RoutedEventArgs e)
        {
            checkEditing();
            openFileDlg.Multiselect = false;
            openFileDlg.DefaultExt = "bat";
            openFileDlg.Filter = "Batch file (*.bat) | *.bat";
            if (openFileDlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                try
                {
                    List<BlendData> data = new List<BlendData>();
                    StringBuilder sb = new StringBuilder();
                    StreamReader sr = new StreamReader(openFileDlg.OpenFile());

                    while (!sr.EndOfStream)
                    {
                        data.Add(new BlendData(sr.ReadLine()));
                    }
                    sr.Close();
                    foreach (BlendData bd in data)
                    {
                        blendList.Add(bd);
                    }
                }
                catch (IOException ex)
                {

                }
            }
            openFileDlg.Multiselect = true;
            openFileDlg.DefaultExt = "blend";
            openFileDlg.Filter = "Blender files (*.blend) | *.blend";
        }

        private void startBtn_Click(object sender, RoutedEventArgs e)
        {
            checkEditing();
            if (taskBag.Count > 0)
            {
                tokenSource.Cancel();
            }
            else
            {
                startBlender();

            }
        }

        private void blenderBtn_Click(object sender, RoutedEventArgs e)
        {
            if (svdlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                BlenderPath = svdlg.SelectedPath;
            }
        }

        private void blendGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            //if (e.Column.Header.Equals("Output"))
            //{
            //    e.Cancel = true;
            //    if (svdlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            //    {
            //        ((BlendData)e.Row.Item).Output = svdlg.SelectedPath;
            //        e.EditingEventArgs.Handled = true;
            //        blendGrid.Items.Refresh();
            //    }
            //}
        }

        private void outputFolderSelectBtn_Click(object sender, RoutedEventArgs e)
        {
            if (svdlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ((BlendData)((ContentPresenter)((StackPanel)((System.Windows.Controls.Button)sender).Parent).TemplatedParent).Content).Output = svdlg.SelectedPath;
                e.Handled = true;
                checkEditing();
                blendGrid.Items.Refresh();
            }
        }

        private void blendGrid_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (blendGrid.SelectedItem == null)
            {
                e.Handled = true;
            }
        }

        private void blendGrid_ContextMenuClosing(object sender, ContextMenuEventArgs e)
        {
            blendGrid.Items.Refresh();
        }

        private void ppDonateBtn_Click(object sender, RoutedEventArgs e)
        {
            string url = "";

            string hosted_button_id = "9DRP7D7GZBJBJ";

            url += "https://www.paypal.com/cgi-bin/webscr" +
                "?cmd=" + "_s-xclick" +
                "&hosted_button_id=" + hosted_button_id;

            System.Diagnostics.Process.Start(url);
        }

        private void infoBtn_Click(object sender, RoutedEventArgs e)
        {
            InfoDialog id = new InfoDialog();
            id.ShowDialog();
        }

        #endregion Events

        private void getBlendsFromFolder(IEnumerable<FileInfo> p)
        {
            List<BlendData> data = new List<BlendData>();
            foreach (FileInfo file in p)
            {
                data.Add(new BlendData(false, file.Name.Remove(file.Name.Length - 6), file.DirectoryName, 0, 0, 0, file.Name.Remove(file.Name.Length - 6) + "_###", file.DirectoryName));
            }

            foreach (BlendData bd in data)
            {
                blendList.Add(bd);
            }
        }


        private void generateBatchFile()
        {
            if (saveFileDlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                Stream outfile;
                if ((outfile = saveFileDlg.OpenFile()) != null)
                {
                    StringBuilder sb = new StringBuilder();
                    //sb.Append();
                    foreach (BlendData item in blendList)
                    {
                        if (item.Use)
                            sb.Append("\"" + @BlenderPath + "\\blender.exe\"" + @item.generateBatchCommand() + Environment.NewLine);
                    }
                    outfile.Write(Encoding.UTF8.GetBytes(sb.ToString()), 0, Encoding.UTF8.GetByteCount(sb.ToString()));
                    outfile.Flush();
                    outfile.Close();
                }
            }

        }

        private async void startBlender()
        {
            startBtnImage.Source = new BitmapImage(new Uri(@"Ressources/116.png", UriKind.Relative));
            startBtn.ToolTip = "Cancel render queue";
            progressBar.Value = 0;
            progressBar.Maximum = blendList.Count;
            progressBar.Visibility = System.Windows.Visibility.Visible;
            token = tokenSource.Token;
            int counter = 0;
            Task t;
            List<BlendData> workList = new List<BlendData>();

            foreach (BlendData item in blendList)
            {
                if (!item.Use) continue;
                counter += item.GetFrameCount();
                workList.Add(new BlendData(item));
            }
            progressBar.Maximum = counter;
            foreach (BlendData item in workList)
            {
                //if (!item.Use) continue;

                t = Task.Factory.StartNew(() =>
               {
                   item.StartRendering(token, "\"" + @BlenderPath + "\\blender.exe\"", progressBar, Process.GetCurrentProcess().MainWindowHandle);
               }, token);
                taskBag.Add(t);
                await checkTasks();
                if (!t.IsCanceled && !t.IsFaulted)
                {
                    foreach (BlendData finished in blendList)
                    {
                        if (finished.Equals(item))
                        {
                            finished.Use = false;
                            blendGrid.Items.Refresh();
                            break;
                        }
                    }
                }
                taskBag = new ConcurrentBag<Task>();
            }

            tokenSource.Dispose();
            tokenSource = new CancellationTokenSource();
            progressBar.Visibility = System.Windows.Visibility.Hidden;
            startBtnImage.Source = new BitmapImage(new Uri(@"Ressources/33.png", UriKind.Relative));
            startBtn.ToolTip = "Start Blender with selected files";
        }

        private async Task<int> checkTasks()
        {
            int counter = 0;
            Task t;
            while (counter < taskBag.Count)
            {
                counter = 0;
                foreach (Task item in taskBag)
                {
                    if (item.Status != TaskStatus.Running)
                    {
                        counter++;
                    }
                }
                await Task.Delay(100);
            }
            return 1;
        }



        private void checkEditing()
        {
            if (blendList.Count > 0 && blendGrid.IsKeyboardFocusWithin)
            {
                blendGrid.CommitEdit(DataGridEditingUnit.Row, true);
            }
        }

        private void loadAppData()
        {
            try
            {
                StreamReader streamReader = new StreamReader(System.Windows.Forms.Application.UserAppDataPath + "\\appdata.data");
                string text = streamReader.ReadToEnd();
                streamReader.Close();
                if (String.IsNullOrWhiteSpace(text))
                {
                    BlenderPath = @"C:\Programme\Blender Foundation\Blender";
                }
                else
                {
                    BlenderPath = text;
                }
            }
            catch (IOException e)
            {
                BlenderPath = @"C:\Programme\Blender Foundation\Blender";
            }
        }

        private void saveAppData()
        {
            try
            {
                StreamWriter sw = new StreamWriter(System.Windows.Forms.Application.UserAppDataPath + "\\appdata.data", false);
                sw.Write(BlenderPath);
                sw.Flush();
                sw.Close();
            }
            catch (IOException e)
            {
                System.Windows.MessageBox.Show("Unable to write App Data", "Write Error");
            }
        }

        private void changeSelected(String propName, object value)
        {

            Type type1 = typeof(BlendData);
            PropertyInfo itemPropertyInfo = type1.GetProperty(propName);
            foreach (Object item in blendGrid.SelectedItems)
            {
                itemPropertyInfo.SetValue((BlendData)item, value, null);
            }
            blendGrid.Items.Refresh();
        }

    }

    public class BlendData
    {
        public static List<String> FormatsList;
        public bool Use { get; set; }
        public string Name { get; set; }
        public string Folder { get; set; }
        private int formatID;
        public int FormatID
        {
            get { return formatID; }
            set
            {
                formatID = value;
                Format = FormatsList[value];
            }
        }
        public string Format { get; set; }
        public int Start { get; set; }
        public int End { get; set; }
        public int Threads { get; set; }
        public string FileMask { get; set; }
        private string output;
        public string Output
        {
            get { return output; }
            set
            {
                if (value.EndsWith("\\") || value.EndsWith("/"))
                {
                    this.output = value.Substring(0, value.Length - 1);
                }
                else
                {
                    this.output = value;
                }
            }
        }

        System.Windows.Controls.ProgressBar pBar;
        string blenderPath;
        private CancellationToken token;

        public BlendData(bool use, string name, string folder, int start, int end, int threads, string fileMask, string output)
        {
            this.Use = use;
            this.Name = name;
            this.Folder = folder;
            this.Start = start;
            this.End = end;
            this.Threads = threads;
            this.FileMask = fileMask;
            this.Output = output;
            this.FormatID = 0;
        }
        public BlendData(BlendData original)
        {
            this.Use = original.Use;
            this.Name = original.Name;
            this.Folder = original.Folder;
            this.Start = original.Start;
            this.End = original.End;
            this.Threads = original.Threads;
            this.FileMask = original.FileMask;
            this.Output = original.Output;
            this.FormatID = original.FormatID;
        }

        public BlendData(string batchCommand)
        {
            this.Use = false;
            parseBlendDataFromBlenderCommand(batchCommand);
        }

        public String generateBatchCommand()
        {
            String result = " ";

            result += "-b " + "\"" + @Folder + "\\" + Name + ".blend\"";
            if (!Output.Equals("-1") && !FileMask.Equals("-1"))
            {
                result += " -o " + "\"" + @Output + "\\" + FileMask + "\"";
            }
            if (Threads > -1)
            {
                result += " -t " + Threads;
            }
            if (this.FormatID != FormatsList.IndexOf("FileSetting"))
            {
                result += " -F " + Format;
            }
            if (Start > -1)
            {
                if (Start >= End)
                {
                    result += " -f " + Start;
                }
                else
                {
                    result += " -s " + Start + " -e " + End + " -a";
                }
            }
            else
            {
                result += " -a";
            }
            return result;
        }

        private void parseBlendDataFromBlenderCommand(String command)
        {
            string name = "";
            string folder = "";
            int start = -1;
            int end = -1;
            int threads = -1;
            string fileMask = "-1";
            string output = "-1";
            string[] parts = command.Split('-');
            int pos = 0;
            int formatID = 0;
            foreach (string item in parts)
            {
                pos = item.StartsWith("-") ? 1 : 0;
                switch (item.Substring(pos, item.IndexOf(" ") - pos + 1))
                {
                    case "b ":
                    case "background ":
                        pos = item.LastIndexOf("\\");
                        if (pos < 0)
                            pos = item.LastIndexOf("/");
                        folder = item.Substring(item.IndexOf(" "), pos - 1).Replace("\"", "").Trim();
                        name = item.Substring(pos + 1).Replace("\"", "").Trim();
                        name = name.Remove(name.Length - 6);
                        break;
                    case "o ":
                    case "render-output":
                        pos = item.LastIndexOf("\\");
                        if (pos < 0)
                            pos = item.LastIndexOf("/");
                        output = item.Substring(item.IndexOf(" "), pos - 1).Replace("\"", "").Trim();
                        fileMask = item.Substring(pos + 1).Replace("\"", "").Trim();
                        break;
                    case "f ":
                    case "render-frame ":
                        if (int.TryParse(item.Substring(item.Trim().IndexOf(" ")).Trim(), out pos))
                        {
                            start = pos;
                            end = 0;
                        }
                        break;
                    case "t ":
                    case "threads ":
                        if (int.TryParse(item.Substring(item.Trim().IndexOf(" ")).Trim(), out pos))
                        {
                            threads = pos;
                        }
                        break;
                    case "s ":
                    case "frame-start ":
                        if (int.TryParse(item.Substring(item.Trim().IndexOf(" ")).Trim(), out pos))
                        {
                            start = pos;
                        }
                        break;
                    case "e ":
                    case "frame-end ":
                        if (int.TryParse(item.Substring(item.Trim().IndexOf(" ")).Trim(), out pos))
                        {
                            end = pos;
                        }
                        break;
                    case "F ":
                    case "render-format ":
                        formatID = FormatsList.IndexOf(item.Substring(item.Trim().IndexOf(" ")).Trim());
                        if (formatID < 0)
                            formatID = 0;
                        break;
                    default:
                        break;
                }
            }

            this.Name = name;
            this.Folder = folder;
            this.Start = start;
            this.End = end;
            this.Threads = threads;
            this.FileMask = fileMask;
            this.Output = output;
            this.FormatID = formatID;
        }

        public void StartRendering(CancellationToken token, string blenderPath, System.Windows.Controls.ProgressBar pBar, IntPtr handle)
        {
            this.token = token;
            this.blenderPath = blenderPath;
            string cmd = "";
            this.pBar = pBar;
            if (token.IsCancellationRequested)
            {
                token.ThrowIfCancellationRequested();
                return;
            }
            if (Start > -1)
            {
                if (FormatID == 0 || FormatID > 6)
                {
                    cmd = " -b " + "\"" + @Folder + "\\" + Name + ".blend\"";
                    if (!Output.Equals("-1") && !FileMask.Equals("-1"))
                    {
                        cmd += " -o " + "\"" + @Output + "\\" + FileMask + "\"";
                    }
                    if (Threads > -1)
                    {
                        cmd += " -t " + Threads;
                    }
                    if (FormatID > 6)
                    {
                        cmd += " -F " + Format;
                    }
                    cmd += " -s " + Start + " -e " + End + " -a";
                    runBlenderProcess(cmd);
                }
                else
                {
                    int loopEnd = End < Start ? Start : End;

                    for (int i = Start; i <= loopEnd; i++)
                    {
                        if (token.IsCancellationRequested)
                        {
                            return;
                        }
                        cmd = " -b " + "\"" + @Folder + "\\" + Name + ".blend\"";
                        if (!Output.Equals("-1") && !FileMask.Equals("-1"))
                        {
                            cmd += " -o " + "\"" + @Output + "\\" + FileMask + "\"";
                        }

                        if (Threads > -1)
                        {
                            cmd += " -t " + Threads;
                        }
                        cmd += " -F " + Format;
                        cmd += " -f " + i;
                        runBlenderProcess(cmd);

                        if (Start == loopEnd)
                            i++;
                    }
                }
            }
            else
            {
                cmd = "";
                cmd += " -b " + "\"" + @Folder + "\\" + Name + ".blend\"";
                if (!Output.Equals("-1") && !FileMask.Equals("-1"))
                {
                    cmd += " -o " + "\"" + @Output + "\\" + FileMask + "\"";
                }
                if (Threads > -1)
                {
                    cmd += " -t " + Threads;
                }
                if (FormatID > 0)
                {
                    cmd += " -F " + Format;
                }
                cmd += " -a";
                runBlenderProcess(cmd);
                //pBar.Dispatcher.BeginInvoke(new Action(() => pBar.Value += pBar.SmallChange));
            }
        }

        public int GetFrameCount()
        {
            int counter = 1;

            if (Start > -1 && End > Start && FormatID != 0 && FormatID <= 6)
                counter = End - Start + 1;

            return counter;

        }

        private void runBlenderProcess(string cmd)
        {
            Process p = new Process();
            p.StartInfo.FileName = blenderPath;
            p.StartInfo.Arguments = cmd;
            p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.CreateNoWindow = true;
            p.Start();
            while (!p.HasExited)
            {
                if (token.IsCancellationRequested)
                {
                    p.Kill();
                    return;
                }
                p.WaitForExit(100);
            }
            pBar.Dispatcher.BeginInvoke(new Action(() => pBar.Value += pBar.SmallChange));
        }


        // override object.Equals
        public override bool Equals(object obj)
        {
            //       
            // See the full list of guidelines at
            //   http://go.microsoft.com/fwlink/?LinkID=85237  
            // and also the guidance for operator== at
            //   http://go.microsoft.com/fwlink/?LinkId=85238
            //

            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            if (!this.Use.Equals(((BlendData)obj).Use))
                return false;
            if (!this.End.Equals(((BlendData)obj).End))
                return false;
            if (!this.formatID.Equals(((BlendData)obj).formatID))
                return false;
            if (!this.Start.Equals(((BlendData)obj).Start))
                return false;
            if (!this.Threads.Equals(((BlendData)obj).Threads))
                return false;
            if (!this.Name.Equals(((BlendData)obj).Name))
                return false;
            if (!this.FileMask.Equals(((BlendData)obj).FileMask))
                return false;
            if (!this.Folder.Equals(((BlendData)obj).Folder))
                return false;
            if (!this.output.Equals(((BlendData)obj).output))
                return false;
            return true;
        }

        // override object.GetHashCode
        public override int GetHashCode()
        {
            // TODO: write your implementation of GetHashCode() here
            return base.GetHashCode();
        }
    }

}
