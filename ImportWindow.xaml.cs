using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;

namespace ProjectToPromptScanner
{
    public partial class ImportWindow : Window
    {
        private readonly string _rootPath;
        private readonly ObservableCollection<ImportItem> _items;
        private readonly string _textInput;
        private readonly Dictionary<string, bool> _fileBomMap = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        private static readonly Regex FileContentRegex = new Regex(
            @"<file_content\s+path=""([^""]+)"">\s*(.*?)\s*</file_content>",
            RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

        public ImportWindow(string rootPath, string initialText = "")
        {
            InitializeComponent();
            _rootPath = rootPath;
            _items = new ObservableCollection<ImportItem>();
            tvFiles.ItemsSource = _items;

            _textInput = initialText;
            btnApply.IsEnabled = !string.IsNullOrWhiteSpace(_textInput);

            ScanText();
        }

        private void ScanText()
        {
            _items.Clear();
            _fileBomMap.Clear();

            if (string.IsNullOrWhiteSpace(_textInput)) return;

            try {
                foreach (Match match in FileContentRegex.Matches(_textInput)) {
                    string relativePath = match.Groups[1].Value.Trim();
                    string aiResponse = match.Groups[2].Value;
                    string fullPath = Path.Combine(_rootPath, relativePath);
                    bool isNew = !File.Exists(fullPath);
                    bool isCSharp = relativePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);

                    string originalCode = string.Empty;
                    bool hasBom = false;

                    if (!isNew) {
                        byte[] fileBytes = File.ReadAllBytes(fullPath);
                        if (fileBytes.Length >= 3 && fileBytes[0] == 0xEF && fileBytes[1] == 0xBB && fileBytes[2] == 0xBF) {
                            hasBom = true;
                            originalCode = Encoding.UTF8.GetString(fileBytes, 3, fileBytes.Length - 3);
                        }
                        else {
                            hasBom = false;
                            originalCode = Encoding.UTF8.GetString(fileBytes);
                        }
                    }

                    _fileBomMap[fullPath] = hasBom;

                    var analysis = CodeUpdater.ApplyAiChanges(originalCode, aiResponse, relativePath);

                    _items.Add(new ImportItem
                    {
                        Path = relativePath,
                        Code = analysis.UpdatedCode,
                        IsNew = isNew,
                        TotalChanges = analysis.TotalBlocks,
                        SuccessChanges = analysis.SuccessCount,
                        FailedChanges = analysis.FailCount,
                        ErrorDetails = "- " + string.Join("\n- ", analysis.ErrorDetails)
                    });
                }
            }
            catch (Exception ex) {
                MessageBox.Show($"Error Parsing Content: {ex.Message}", "Parse Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            try {
                var modifiedFiles = new List<string>();

                foreach (var item in _items.Where(i => i.IsChecked)) {
                    string fullPath = Path.Combine(_rootPath, item.Path);
                    string directory = Path.GetDirectoryName(fullPath);

                    if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory)) {
                        Directory.CreateDirectory(directory);
                    }

                    bool isCSharp = item.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);

                    if (isCSharp) {
                        bool emitBom = _fileBomMap.TryGetValue(fullPath, out bool hasBom) && hasBom;
                        Encoding encoding = new UTF8Encoding(emitBom);
                        File.WriteAllText(fullPath, item.Code, encoding);
                    }
                    else {
                        File.WriteAllText(fullPath, item.Code, new UTF8Encoding(false));
                    }

                    modifiedFiles.Add($"\"{fullPath}\"");
                }

                if (chkPrettier.IsChecked == true && modifiedFiles.Any()) {
                    var extensionsToFormat = new[] { ".js", ".jsx", ".ts", ".tsx", ".css", ".scss", ".html" };
                    var filesToFormat = modifiedFiles.Where(f => extensionsToFormat.Contains(Path.GetExtension(f.Trim('"')).ToLower())).ToList();
                    if (filesToFormat.Any()) {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments = $"/c prettier --write --ignore-path \"nul\" {string.Join(" ", filesToFormat)}",
                            WorkingDirectory = _rootPath,
                            UseShellExecute = true
                        });
                    }
                }

                Close();
            }
            catch (Exception ex) {
                MessageBox.Show($"Error applying changes: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}