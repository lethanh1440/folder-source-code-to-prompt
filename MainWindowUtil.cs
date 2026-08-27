using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ProjectToPromptScanner
{
    public static class MainWindowUtil
    {
        public static void RefreshConfigList(MainWindow win)
        {
            try { win.cboSavedConfigs.ItemsSource = ConfigHelper.GetSavedConfigs(); } catch { }
        }

        public static void LoadLastSession(MainWindow win)
        {
            try {
                string lastConfig = ConfigHelper.GetLastSessionConfigName();
                if (!string.IsNullOrEmpty(lastConfig)) {
                    win.cboSavedConfigs.SelectedItem = lastConfig;
                    QuickLoad(win);
                }
            }
            catch { }
        }

        public static void QuickSave(MainWindow win)
        {
            string inputName = win.txtSaveName.Text.Trim();
            if (string.IsNullOrEmpty(inputName)) { MessageBox.Show("Vui lòng nhập tên cấu hình!"); return; }

            var state = new ProjectState
            {
                ScanFolderPath = win.txtFolderPath.Text,
                IsWhitelistMode = win.rbOnly.IsChecked == true,
                IgnoreExtensions = win.txtExtensionIgnore.Text,
                OnlyExtensions = win.txtExtensionOnly.Text,
                FolderIgnores = win.txtFolderIgnore.Text,
                FileIgnores = win.txtFileIgnore.Text,
                CheckedItemPaths = new List<string>(),
                CheckedItemPaths2 = new List<string>(),
                CheckedItemPaths3 = new List<string>(),
                ExpandedItemPaths = new List<string>()
            };

            foreach (var item in win.RootItems) FileTreeHelper.CollectState(item, state.CheckedItemPaths, state.CheckedItemPaths2, state.CheckedItemPaths3, state.ExpandedItemPaths);
            foreach (var item in win.VirtualRootItems) FileTreeHelper.CollectState(item, state.CheckedItemPaths, state.CheckedItemPaths2, state.CheckedItemPaths3, state.ExpandedItemPaths);

            try {
                ConfigHelper.SaveState(inputName, state);
                ConfigHelper.SaveLastSessionName(inputName);
                RefreshConfigList(win);
            }
            catch (Exception ex) { MessageBox.Show("Lỗi lưu cấu hình: " + ex.Message); }
        }

        public static void QuickLoad(MainWindow win)
        {
            string selectedName = win.cboSavedConfigs.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedName)) return;

            try {
                var state = ConfigHelper.LoadState(selectedName);
                if (state == null) return;

                win.txtFolderPath.Text = state.ScanFolderPath;
                win.txtExtensionIgnore.Text = state.IgnoreExtensions;
                win.txtExtensionOnly.Text = state.OnlyExtensions;
                win.txtFolderIgnore.Text = state.FolderIgnores;
                win.txtFileIgnore.Text = state.FileIgnores;
                win.txtSaveName.Text = selectedName;

                if (state.IsWhitelistMode) win.rbOnly.IsChecked = true; else win.rbIgnore.IsChecked = true;

                if (Directory.Exists(state.ScanFolderPath)) {
                    LoadDirectory(win, state.ScanFolderPath);
                    foreach (var root in win.RootItems) FileTreeHelper.ApplyState(root, state);
                    foreach (var root in win.VirtualRootItems) FileTreeHelper.ApplyState(root, state);
                    win.RefreshCheckedFiles();
                }
                ConfigHelper.SaveLastSessionName(selectedName);
            }
            catch { }
        }

        public static void NewConfig(MainWindow win)
        {
            win.txtSaveName.Text = string.Empty;
            win.cboSavedConfigs.SelectedItem = null;
            win.txtFolderPath.Text = "Chọn đường dẫn dự án...";
            win.txtExtensionIgnore.Text = ".exe;.dll;.pdb;.cache;.suo;.user;.png;.jpg;.jpeg;.ico;.obj";
            win.txtExtensionOnly.Text = ".cs;.xaml;.xml;.json;.js;.ts;.html;.css;.sql";
            win.txtFolderIgnore.Text = ".git;bin;obj;.vs;node_modules;debug;release";
            win.txtFileIgnore.Text = string.Empty;
            win.rbIgnore.IsChecked = true;
            win.RootItems.Clear();
            win.VirtualRootItems.Clear();
            win.CheckedFiles.Clear();
            win.txtCheckedCount.Text = "Số file đã chọn: C1 (0) | C2 (0) | C3 (0)";
        }

        public static void CboSavedConfigs_SelectionChanged(MainWindow win, object sender)
        {
            if (win.cboSavedConfigs.SelectedItem is string selectedName && !string.IsNullOrWhiteSpace(selectedName)) {
                win.Title = $"{selectedName}";
            }
            else {
                win.Title = "Code Project Scanner to Prompt";
            }
        }

        public static void ScrollAndFocusFileItem(MainWindow win, object sender)
        {
            if (!((sender as FrameworkElement)?.Tag is FileTreeItem item)) return;

            bool isVirtual = false;
            foreach (var vRoot in win.VirtualRootItems) {
                if (ContainsItem(vRoot, item)) { isVirtual = true; break; }
            }

            win.tabMain.SelectedIndex = isVirtual ? 1 : 0;
            var targetTree = isVirtual ? win.tvVirtualFolder : win.tvFolder;

            var parent = item.Parent;
            while (parent != null) {
                parent.IsExpanded = true;
                parent = parent.Parent;
            }

            targetTree.UpdateLayout();
            var tvi = GetTreeViewItem(targetTree, item);
            if (tvi != null) {
                tvi.BringIntoView();
                tvi.IsSelected = true;
                tvi.Focus();
            }
        }

        private static bool ContainsItem(FileTreeItem root, FileTreeItem target)
        {
            if (root == target) return true;
            foreach (var child in root.Children) {
                if (ContainsItem(child, target)) return true;
            }
            return false;
        }

        private static TreeViewItem GetTreeViewItem(ItemsControl container, FileTreeItem item)
        {
            if (container == null || item == null) return null;

            var stack = new Stack<FileTreeItem>();
            var current = item;
            while (current != null) {
                stack.Push(current);
                current = current.Parent;
            }

            ItemsControl parentContainer = container;
            TreeViewItem targetTvi = null;

            while (stack.Count > 0) {
                var node = stack.Pop();
                targetTvi = parentContainer.ItemContainerGenerator.ContainerFromItem(node) as TreeViewItem;
                if (targetTvi == null) {
                    parentContainer.UpdateLayout();
                    targetTvi = parentContainer.ItemContainerGenerator.ContainerFromItem(node) as TreeViewItem;
                    if (targetTvi == null) return null;
                }
                if (stack.Count > 0) {
                    targetTvi.IsExpanded = true;
                    targetTvi.UpdateLayout();
                    parentContainer = targetTvi;
                }
            }

            return targetTvi;
        }

        public static void BrowseFolder(MainWindow win)
        {
            var dialog = new OpenFolderDialog { Title = "Chọn thư mục dự án" };
            if (dialog.ShowDialog() == true) {
                win.txtFolderPath.Text = dialog.FolderName;
                LoadDirectory(win, dialog.FolderName);
            }
        }

        public static void LoadDirectory(MainWindow win, string path)
        {
            win.RootItems.Clear();
            win.VirtualRootItems.Clear();
            win.txtCheckedCount.Text = "Số file đã chọn: C1 (0) | C2 (0) | C3 (0)";

            bool isWhitelistMode = win.rbOnly.IsChecked == true;
            var extensions = (isWhitelistMode ? win.txtExtensionOnly.Text : win.txtExtensionIgnore.Text)
                .Split(';').Select(x => x.Trim().ToLower()).Where(x => !string.IsNullOrEmpty(x)).ToList();
            var folderIgnores = win.txtFolderIgnore.Text
                .Split(';').Select(x => x.Trim().ToLower()).Where(x => !string.IsNullOrEmpty(x)).ToList();
            var fileIgnores = win.txtFileIgnore.Text
                .Split(';').Select(x => x.Trim().ToLower()).Where(x => !string.IsNullOrEmpty(x)).ToList();

            var rootItem = CreateTreeItem(path, extensions, folderIgnores, fileIgnores, isWhitelistMode, () => TriggerDebouncedCountUpdate(win));
            if (rootItem != null) {
                rootItem.Parent = null;
                win.RootItems.Add(rootItem);
            }

            string virtualFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigHelper.VIRTUAL_FOLDER);
            if (Directory.Exists(virtualFolderPath)) {
                var virtualRootItem = CreateTreeItem(virtualFolderPath, extensions, folderIgnores, fileIgnores, isWhitelistMode, () => TriggerDebouncedCountUpdate(win));
                if (virtualRootItem != null) {
                    virtualRootItem.Name = ConfigHelper.VIRTUAL_FOLDER;
                    virtualRootItem.FullPath = "";
                    virtualRootItem.Parent = null;
                    win.VirtualRootItems.Add(virtualRootItem);
                }
            }

            TriggerDebouncedCountUpdate(win);
        }

        private static FileTreeItem CreateTreeItem(string path, List<string> extensions, List<string> folderIgnores, List<string> fileIgnores, bool isWhitelistMode, Action onUpdate)
        {
            var item = new FileTreeItem { Name = Path.GetFileName(path), FullPath = path, IsFile = false, IsIgnored = false };
            if (string.IsNullOrEmpty(item.Name)) item.Name = path;

            item.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(FileTreeItem.IsChecked) || e.PropertyName == nameof(FileTreeItem.IsChecked2) || e.PropertyName == nameof(FileTreeItem.IsChecked3)) onUpdate();
            };

            if (folderIgnores.Contains(item.Name.ToLower())) return null;

            try {
                foreach (var dir in Directory.GetDirectories(path)) {
                    var child = CreateTreeItem(dir, extensions, folderIgnores, fileIgnores, isWhitelistMode, onUpdate);
                    if (child != null) { child.Parent = item; item.Children.Add(child); }
                }
                foreach (var file in Directory.GetFiles(path)) {
                    var fileItem = new FileTreeItem { Name = Path.GetFileName(file), FullPath = file, IsFile = true, Parent = item };
                    fileItem.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == nameof(FileTreeItem.IsChecked) || e.PropertyName == nameof(FileTreeItem.IsChecked2) || e.PropertyName == nameof(FileTreeItem.IsChecked3)) onUpdate();
                    };
                    string fileExt = Path.GetExtension(file).ToLower();
                    bool shouldIgnoreFile = isWhitelistMode ? !extensions.Contains(fileExt) : extensions.Contains(fileExt);
                    if (fileIgnores.Contains(fileItem.Name.ToLower())) shouldIgnoreFile = true;
                    if (!shouldIgnoreFile) item.Children.Add(fileItem);
                }
            }
            catch { }
            return item;
        }

        public static async void TriggerDebouncedCountUpdate(MainWindow win)
        {
            if (win.IsCountingFiles) return;
            win.IsCountingFiles = true;
            await Task.Delay(50);
            int c1 = 0, c2 = 0, c3 = 0;
            var checkedList = new List<FileTreeItem>();
            foreach (var root in win.RootItems) {
                FileTreeHelper.CountCheckedFiles(root, ref c1, ref c2, ref c3);
                FileTreeHelper.CollectCheckedFiles(root, checkedList);
            }
            foreach (var root in win.VirtualRootItems) {
                FileTreeHelper.CountCheckedFiles(root, ref c1, ref c2, ref c3);
                FileTreeHelper.CollectCheckedFiles(root, checkedList);
            }
            win.txtCheckedCount.Text = $"Số file đã chọn: C1 ({c1}) | C2 ({c2}) | C3 ({c3})";
            win.CheckedFiles.Clear();
            foreach (var item in checkedList) win.CheckedFiles.Add(item);
            win.IsCountingFiles = false;
        }

        public static void ToggleAllCheckboxes(MainWindow win, object sender)
        {
            if (sender is CheckBox chk && int.TryParse(chk.Tag?.ToString(), out int index)) {
                bool? isChecked = chk.IsChecked;
                foreach (var root in win.RootItems) {
                    FileTreeHelper.ToggleAllChildren(root, index, isChecked);
                    root.RecalculateParentStates();
                }
                foreach (var root in win.VirtualRootItems) {
                    FileTreeHelper.ToggleAllChildren(root, index, isChecked);
                    root.RecalculateParentStates();
                }
                TriggerDebouncedCountUpdate(win);
            }
        }

        public static void FilterTextBox_KeyDown(MainWindow win, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter) {
                if (!string.IsNullOrEmpty(win.txtFolderPath.Text) && Directory.Exists(win.txtFolderPath.Text)) {
                    LoadDirectory(win, win.txtFolderPath.Text);
                }
            }
        }

        public static void ReloadPreservingExpandedState(MainWindow win)
        {
            if (string.IsNullOrEmpty(win.txtFolderPath.Text) || !Directory.Exists(win.txtFolderPath.Text)) return;
            var expandedPaths = new List<string>();
            foreach (var root in win.RootItems) FileTreeHelper.CollectExpandedOnly(root, expandedPaths);
            foreach (var root in win.VirtualRootItems) FileTreeHelper.CollectExpandedOnly(root, expandedPaths);
            LoadDirectory(win, win.txtFolderPath.Text);
            foreach (var root in win.RootItems) FileTreeHelper.RestoreExpandedOnly(root, expandedPaths);
            foreach (var root in win.VirtualRootItems) FileTreeHelper.RestoreExpandedOnly(root, expandedPaths);
        }

        public static void RunExport(MainWindow win, int checkIndex)
        {
            if (win.RootItems.Count == 0 && win.VirtualRootItems.Count == 0) return;
            if (string.IsNullOrEmpty(win.txtSaveName.Text)) { MessageBox.Show("Vui lòng nhập Save Name trước khi Export!"); return; }

            int countC1 = 0, countC2 = 0, countC3 = 0;
            foreach (var root in win.RootItems) FileTreeHelper.CountCheckedFiles(root, ref countC1, ref countC2, ref countC3);
            foreach (var root in win.VirtualRootItems) FileTreeHelper.CountCheckedFiles(root, ref countC1, ref countC2, ref countC3);

            bool hasSelection = (checkIndex == 0 && countC1 > 0) || (checkIndex == 1 && countC2 > 0) || (checkIndex == 2 && countC3 > 0);
            if (!hasSelection) { MessageBox.Show($"Không có file nào được chọn trong mục Check {checkIndex + 1} để export!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            var sb = new StringBuilder();
            string rootPath = win.txtFolderPath.Text;
            foreach (var root in win.RootItems) FileTreeHelper.ProcessExport(root, sb, rootPath, checkIndex);
            foreach (var root in win.VirtualRootItems) FileTreeHelper.ProcessExport(root, sb, rootPath, checkIndex);

            try {
                string fileName = $"{win.txtSaveName.Text.Trim()}_source.txt";
                string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigHelper.SAVE_FOLDER, fileName);
                File.WriteAllText(fullPath, sb.ToString());
                win.LatestExportedFilePaths[checkIndex] = fullPath;

                Border targetBtn = checkIndex == 0 ? win.btnLatestExport1 : (checkIndex == 1 ? win.btnLatestExport2 : win.btnLatestExport3);
                TextBlock targetTxt = checkIndex == 0 ? win.txtLatestExportName1 : (checkIndex == 1 ? win.txtLatestExportName2 : win.txtLatestExportName3);

                if (targetBtn != null && targetTxt != null) {
                    targetTxt.Text = $"{fileName}_check_{checkIndex + 1}";
                    targetBtn.Visibility = Visibility.Visible;
                    if (targetBtn.Resources["BlinkAnimation"] is System.Windows.Media.Animation.Storyboard sbAnim) sbAnim.Begin(targetBtn);
                }
            }
            catch (Exception ex) { MessageBox.Show($"Lỗi export: {ex.Message}"); }
        }

        public static void ImportFromAI(MainWindow win)
        {
            string folderPath = win.txtFolderPath.Text;
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath)) {
                MessageBox.Show("Vui lòng chọn thư mục dự án (Folder Path) trước!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            string initialText = "";
            if (Clipboard.ContainsText()) {
                string text = Clipboard.GetText();
                if (System.Text.RegularExpressions.Regex.IsMatch(text, @"<file_content\s+path=""[^""]+"">", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) initialText = text;
            }
            var importWin = new ImportWindow(folderPath, initialText) { Owner = win };
            importWin.ShowDialog();
        }

        public static async void OpenSavedFolder(MainWindow win)
        {
            try {
                string folderPath = AppDomain.CurrentDomain.BaseDirectory + "\\saved";
                if (Directory.Exists(folderPath)) {
                    Process.Start(new ProcessStartInfo { FileName = folderPath, UseShellExecute = true, Verb = "open" });
                    await Task.Delay(500);
                    if (!win.IsClosingApp) win.WindowState = WindowState.Minimized;
                }
            }
            catch (Exception ex) { MessageBox.Show($"Không thể mở thư mục: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        public static FileTreeItem GetFileTreeItemFromMenuItem(object sender)
        {
            if (sender is MenuItem menuItem) {
                if (menuItem.DataContext is FileTreeItem item) return item;
                if (menuItem.Parent is ContextMenu contextMenu && contextMenu.PlacementTarget is FrameworkElement target) return target.DataContext as FileTreeItem;
            }
            return null;
        }

        public static void CtxCopyName(object sender)
        {
            var item = GetFileTreeItemFromMenuItem(sender);
            if (item != null && !string.IsNullOrEmpty(item.Name)) Clipboard.SetText(item.Name);
        }

        public static void CtxShowExplorer(object sender)
        {
            var item = GetFileTreeItemFromMenuItem(sender);
            if (item != null) {
                if (File.Exists(item.FullPath)) Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = $"/select,\"{item.FullPath}\"", UseShellExecute = true });
                else if (Directory.Exists(item.FullPath)) Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = $"\"{item.FullPath}\"", UseShellExecute = true });
            }
        }

        public static void CtxShowVSCode(object sender)
        {
            var item = GetFileTreeItemFromMenuItem(sender);
            if (item != null) {
                try { Process.Start(new ProcessStartInfo { FileName = "code", Arguments = $"\"{item.FullPath}\"", UseShellExecute = true, CreateNoWindow = true }); }
                catch (Exception ex) { MessageBox.Show($"Không thể mở bằng VSCode. Cần đảm bảo bạn đã cài VSCode và đường dẫn 'code' đã được thêm vào môi trường (PATH).\nLỗi: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error); }
            }
        }

        public static void CtxExclude(MainWindow win, object sender)
        {
            var item = GetFileTreeItemFromMenuItem(sender);
            if (item != null) {
                if (item.IsFile) {
                    var ignores = win.txtFileIgnore.Text.Split(';').Select(x => x.Trim()).Where(x => !string.IsNullOrEmpty(x)).ToList();
                    if (!ignores.Contains(item.Name, StringComparer.OrdinalIgnoreCase)) { ignores.Add(item.Name); win.txtFileIgnore.Text = string.Join(";", ignores); }
                }
                else {
                    var ignores = win.txtFolderIgnore.Text.Split(';').Select(x => x.Trim()).Where(x => !string.IsNullOrEmpty(x)).ToList();
                    if (!ignores.Contains(item.Name, StringComparer.OrdinalIgnoreCase)) { ignores.Add(item.Name); win.txtFolderIgnore.Text = string.Join(";", ignores); }
                }
                ReloadPreservingExpandedState(win);
            }
        }

        public static void CtxExcludeExtension(MainWindow win, object sender)
        {
            var item = GetFileTreeItemFromMenuItem(sender);
            if (item != null && item.IsFile) {
                string ext = Path.GetExtension(item.Name).ToLower();
                if (!string.IsNullOrEmpty(ext)) {
                    var ignores = win.txtExtensionIgnore.Text.Split(';').Select(x => x.Trim()).Where(x => !string.IsNullOrEmpty(x)).ToList();
                    if (!ignores.Contains(ext, StringComparer.OrdinalIgnoreCase)) {
                        ignores.Add(ext); win.txtExtensionIgnore.Text = string.Join(";", ignores); win.rbIgnore.IsChecked = true;
                    }
                }
                ReloadPreservingExpandedState(win);
            }
        }

        public static void LatestExport_MouseMove(MainWindow win, object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed && sender is Border border && int.TryParse(border.Tag?.ToString(), out int idx)) {
                string path = win.LatestExportedFilePaths[idx];
                if (!string.IsNullOrEmpty(path)) {
                    Point mousePos = e.GetPosition(null);
                    Vector diff = win.DragStartPoint - mousePos;
                    if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance || Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance) {
                        if (File.Exists(path)) {
                            string[] files = new string[] { path };
                            DataObject data = new DataObject(DataFormats.FileDrop, files);
                            DragDrop.DoDragDrop(border, data, DragDropEffects.Copy);
                        }
                    }
                }
            }
        }

        public static void LatestExport_MouseLeftButtonUp(MainWindow win, object sender)
        {
            if (sender is Border border && int.TryParse(border.Tag?.ToString(), out int idx)) {
                string path = win.LatestExportedFilePaths[idx];
                if (!string.IsNullOrEmpty(path) && File.Exists(path)) {
                    try {
                        string fileContent = File.ReadAllText(path);
                        var message = new { action = "drop_file", filename = Path.GetFileName(path), content = fileContent, prompt = string.Empty };
                        NativeMessagingHelper.SendMessageToExtension(message);
                    }
                    catch (Exception ex) { MessageBox.Show($"Lỗi gửi file: {ex.Message}"); }
                }
            }
        }

        public static void TestDrop(MainWindow win)
        {
            var message = new { action = "drop_file", filename = "test_from_wpf.txt", content = "Dữ liệu này được giả lập drop từ ứng dụng WPF sang Chrome!", prompt = string.Empty };
            NativeMessagingHelper.SendMessageToExtension(message);
        }
    }
}
