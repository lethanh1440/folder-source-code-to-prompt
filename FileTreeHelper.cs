using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ProjectToPromptScanner
{
    public static class FileTreeHelper
    {
        public static void CountCheckedFiles(FileTreeItem item, ref int c1, ref int c2, ref int c3)
        {
            if (item.IsIgnored) return;
            if (item.IsFile) {
                if (item.IsChecked == true) c1++;
                if (item.IsChecked2 == true) c2++;
                if (item.IsChecked3 == true) c3++;
            }
            else {
                foreach (var child in item.Children) CountCheckedFiles(child, ref c1, ref c2, ref c3);
            }
        }

        public static void CollectCheckedFiles(FileTreeItem item, List<FileTreeItem> list)
        {
            if (item.IsIgnored) return;
            if (item.IsFile) {
                if (item.IsChecked == true || item.IsChecked2 == true || item.IsChecked3 == true) list.Add(item);
            }
            else {
                foreach (var child in item.Children) CollectCheckedFiles(child, list);
            }
        }

        public static void ToggleAllChildren(FileTreeItem item, int index, bool? isChecked)
        {
            if (!item.IsIgnored) item.SetChecked(index, isChecked, false, false);
            foreach (var child in item.Children) ToggleAllChildren(child, index, isChecked);
        }

        public static void CollectState(FileTreeItem item, List<string> p1, List<string> p2, List<string> p3, List<string> exp)
        {
            if (item.IsChecked == true) p1.Add(item.FullPath);
            if (item.IsChecked2 == true) p2.Add(item.FullPath);
            if (item.IsChecked3 == true) p3.Add(item.FullPath);
            if (item.IsExpanded) exp.Add(item.FullPath);
            foreach (var child in item.Children) CollectState(child, p1, p2, p3, exp);
        }

        public static void ApplyState(FileTreeItem item, ProjectState state)
        {
            item.SetChecked(0, state.CheckedItemPaths?.Contains(item.FullPath) == true, false, false);
            item.SetChecked(1, state.CheckedItemPaths2?.Contains(item.FullPath) == true, false, false);
            item.SetChecked(2, state.CheckedItemPaths3?.Contains(item.FullPath) == true, false, false);
            item.IsExpanded = state.ExpandedItemPaths?.Contains(item.FullPath) == true;

            foreach (var child in item.Children) ApplyState(child, state);
            item.RecalculateParentStates();
        }

        public static void ProcessExport(FileTreeItem item, StringBuilder sb, string rootPath, int checkIndex)
        {
            if (item.IsIgnored) return;

            bool? isChecked = checkIndex == 0 ? item.IsChecked : (checkIndex == 1 ? item.IsChecked2 : item.IsChecked3);

            if (item.IsFile) {
                if (isChecked != true) return;

                try {
                    string content = File.ReadAllText(item.FullPath);
                    string virtualFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigHelper.VIRTUAL_FOLDER);
                    bool isVirtual = !string.IsNullOrEmpty(rootPath) && item.FullPath.StartsWith(virtualFolderPath, StringComparison.OrdinalIgnoreCase);
                    string baseDir = isVirtual ? virtualFolderPath : rootPath;
                    string relativePath = string.IsNullOrEmpty(baseDir) ? item.FullPath : Path.GetRelativePath(baseDir, item.FullPath);

                    string tagName = isVirtual ? "prompt_request" : "file_content";
                    if (isVirtual) {
                        sb.AppendLine($"<{tagName}>");
                    }
                    else {
                        sb.AppendLine($"<{tagName} path=\"{relativePath}\">");
                    }
                    sb.AppendLine(content);
                    sb.AppendLine($"</{tagName}>");
                    sb.AppendLine();
                }
                catch (Exception ex) {
                    sb.AppendLine($"<error path=\"{item.FullPath}\">{ex.Message}</error>");
                }
            }
            else {
                if (isChecked == false) return;

                foreach (var child in item.Children) ProcessExport(child, sb, rootPath, checkIndex);
            }
        }

        public static void CollectExpandedOnly(FileTreeItem item, List<string> exp)
        {
            if (item.IsExpanded) exp.Add(item.FullPath);
            foreach (var child in item.Children) CollectExpandedOnly(child, exp);
        }

        public static void RestoreExpandedOnly(FileTreeItem item, List<string> exp)
        {
            if (exp.Contains(item.FullPath)) item.IsExpanded = true;
            foreach (var child in item.Children) RestoreExpandedOnly(child, exp);
        }
    }
}