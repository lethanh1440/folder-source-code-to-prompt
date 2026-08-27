using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ProjectToPromptScanner
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<FileTreeItem> RootItems { get; } = new ObservableCollection<FileTreeItem>();
        public ObservableCollection<FileTreeItem> VirtualRootItems { get; } = new ObservableCollection<FileTreeItem>();
        public ObservableCollection<FileTreeItem> CheckedFiles { get; } = new ObservableCollection<FileTreeItem>();
        public string[] LatestExportedFilePaths { get; } = new string[3];
        public bool IsCountingFiles { get; set; } = false;
        public Point DragStartPoint { get; set; }
        public bool IsClosingApp { get; set; } = false;

        private string _lastClipboardText = string.Empty;
        private NativeMessageListener _listener;
        private static readonly Regex FileContentRegex = new Regex(@"<file_content\s+path=""[^""]+"">", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public MainWindow()
        {
            InitializeComponent();
            tvFolder.ItemsSource = RootItems;
            tvVirtualFolder.ItemsSource = VirtualRootItems;
            icCheckedFiles.ItemsSource = CheckedFiles;
            Loaded += MainWindow_Loaded;
            Closed -= MainWindow_Closed;

            ConfigHelper.EnsureSaveFolder();
            MainWindowUtil.RefreshConfigList(this);
            MainWindowUtil.LoadLastSession(this);
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            //_listener = new NativeMessageListener();
            //_listener.OnFinishProcess += Listener_OnFinishProcess;
            //_listener.Start();
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            if (_listener != null) {
                _listener.Stop();
                _listener.OnFinishProcess -= Listener_OnFinishProcess;
            }
        }

        private void Listener_OnFinishProcess(string textFromGemini)
        {
            Dispatcher.Invoke(() =>
            {
               
            });
        }

        public void SendDataToChromeExtension(string filePath, string promptText)
        {
            if (!File.Exists(filePath)) return;

            var payload = new
            {
                action = "drop_file",
                filename = Path.GetFileName(filePath),
                content = File.ReadAllText(filePath),
                prompt = promptText
            };

            NativeMessagingHelper.SendMessageToExtension(payload);
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            IsClosingApp = true;
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            Application.Current.Shutdown();
            Environment.Exit(0);
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            try {
                if (IsClosingApp) return;
                if (!Clipboard.ContainsText()) return;
                string text = Clipboard.GetText();
                if (string.IsNullOrWhiteSpace(text) || text == _lastClipboardText) return;
                _lastClipboardText = text;

                string folderPath = txtFolderPath.Text;
                if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath)) return;

                if (FileContentRegex.IsMatch(text)) {
                    var importWin = new ImportWindow(folderPath, text) { Owner = this };
                    importWin.ShowDialog();
                }
            }
            catch { }
        }

        public void RefreshCheckedFiles() => MainWindowUtil.TriggerDebouncedCountUpdate(this);
        private void BtnCheckedFile_Click(object sender, RoutedEventArgs e) => MainWindowUtil.ScrollAndFocusFileItem(this, sender);
        private void BtnQuickSave_Click(object sender, RoutedEventArgs e) => MainWindowUtil.QuickSave(this);
        private void cboSavedConfigs_SelectionChanged(object sender, SelectionChangedEventArgs e) => MainWindowUtil.CboSavedConfigs_SelectionChanged(this, sender);
        private void BtnQuickLoad_Click(object sender, RoutedEventArgs e) => MainWindowUtil.QuickLoad(this);
        private void BtnNew_Click(object sender, RoutedEventArgs e) => MainWindowUtil.NewConfig(this);

        private void BtnBrowse_Click(object sender, RoutedEventArgs e) => MainWindowUtil.BrowseFolder(this);
        private void ChkToggleAll_Click(object sender, RoutedEventArgs e) => MainWindowUtil.ToggleAllCheckboxes(this, sender);
        private void FilterTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e) => MainWindowUtil.FilterTextBox_KeyDown(this, e);
        private void BtnExport1_Click(object sender, RoutedEventArgs e) => MainWindowUtil.RunExport(this, 0);
        private void BtnExport2_Click(object sender, RoutedEventArgs e) => MainWindowUtil.RunExport(this, 1);
        private void BtnExport3_Click(object sender, RoutedEventArgs e) => MainWindowUtil.RunExport(this, 2);
        private void BtnImport_Click(object sender, RoutedEventArgs e) => MainWindowUtil.ImportFromAI(this);
        private void BtnOpenFolder_Click(object sender, RoutedEventArgs e) => MainWindowUtil.OpenSavedFolder(this);
        private void CtxCopyName_Click(object sender, RoutedEventArgs e) => MainWindowUtil.CtxCopyName(sender);
        private void CtxShowExplorer_Click(object sender, RoutedEventArgs e) => MainWindowUtil.CtxShowExplorer(sender);
        private void CtxShowVSCode_Click(object sender, RoutedEventArgs e) => MainWindowUtil.CtxShowVSCode(sender);
        private void CtxExclude_Click(object sender, RoutedEventArgs e) => MainWindowUtil.CtxExclude(this, sender);
        private void CtxExcludeExtension_Click(object sender, RoutedEventArgs e) => MainWindowUtil.CtxExcludeExtension(this, sender);
        private void BtnLatestExport_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e) => DragStartPoint = e.GetPosition(null);
        private void BtnLatestExport_MouseMove(object sender, System.Windows.Input.MouseEventArgs e) => MainWindowUtil.LatestExport_MouseMove(this, sender, e);
        private void BtnLatestExport_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e) => MainWindowUtil.LatestExport_MouseLeftButtonUp(this, sender);
        private void Test_Click(object sender, RoutedEventArgs e) => MainWindowUtil.TestDrop(this);

        void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            void Uncheck(System.Collections.Generic.IEnumerable<FileTreeItem> items)
            {
                foreach (var item in items)
                {
                    item.IsChecked = false;
                    item.IsChecked2 = false;
                    item.IsChecked3 = false;
                    if (item.Children != null && item.Children.Count > 0)
                        Uncheck(item.Children);
                }
            }

            Uncheck(RootItems);
            Uncheck(VirtualRootItems);
            CheckedFiles.Clear();
            Array.Clear(LatestExportedFilePaths, 0, LatestExportedFilePaths.Length);
            btnLatestExport1.Visibility = Visibility.Collapsed;
            btnLatestExport2.Visibility = Visibility.Collapsed;
            btnLatestExport3.Visibility = Visibility.Collapsed;
            RefreshCheckedFiles();
        }
    }
}
