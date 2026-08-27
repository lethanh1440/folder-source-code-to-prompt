using System.Collections.Generic;

namespace ProjectToPromptScanner
{
    public class ProjectState
    {
        public string ScanFolderPath { get; set; }
        public bool IsWhitelistMode { get; set; }
        public string IgnoreExtensions { get; set; }
        public string OnlyExtensions { get; set; }
        public string FolderIgnores { get; set; }
        public string FileIgnores { get; set; }
        public List<string> CheckedItemPaths { get; set; } = new List<string>();
        public List<string> CheckedItemPaths2 { get; set; } = new List<string>();
        public List<string> CheckedItemPaths3 { get; set; } = new List<string>();
        public List<string> ExpandedItemPaths { get; set; } = new List<string>();
    }
}