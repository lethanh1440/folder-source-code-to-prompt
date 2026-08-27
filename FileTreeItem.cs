using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace ProjectToPromptScanner
{
    public class FileTreeItem : INotifyPropertyChanged
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public bool IsFile { get; set; }
        public bool IsIgnored { get; set; }
        public FileTreeItem Parent { get; set; }
        public ObservableCollection<FileTreeItem> Children { get; } = new ObservableCollection<FileTreeItem>();

        private bool _isExpanded;
        public bool IsExpanded { get => _isExpanded; set { _isExpanded = value; OnPropertyChanged(); } }

        private bool? _isChecked = false;
        public bool? IsChecked { get => _isChecked; set => SetChecked(0, value, true, true); }

        private bool? _isChecked2 = false;
        public bool? IsChecked2 { get => _isChecked2; set => SetChecked(1, value, true, true); }

        private bool? _isChecked3 = false;
        public bool? IsChecked3 { get => _isChecked3; set => SetChecked(2, value, true, true); }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string prop = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        public void SetChecked(int index, bool? value, bool updateChildren, bool updateParent)
        {
            if (IsIgnored) return;

            if (index == 0) { if (_isChecked == value) return; _isChecked = value; OnPropertyChanged(nameof(IsChecked)); }
            else if (index == 1) { if (_isChecked2 == value) return; _isChecked2 = value; OnPropertyChanged(nameof(IsChecked2)); }
            else if (index == 2) { if (_isChecked3 == value) return; _isChecked3 = value; OnPropertyChanged(nameof(IsChecked3)); }

            if (updateChildren && value.HasValue) {
                foreach (var child in Children) child.SetChecked(index, value, true, false);
            }

            if (updateParent) Parent?.RecalculateParentStates();
        }

        public void RecalculateParentStates()
        {
            if (Children.Count > 0 && !IsIgnored) {
                var validChildren = Children.Where(c => !c.IsIgnored).ToList();
                if (validChildren.Count > 0) {
                    var state0 = validChildren.Select(c => c.IsChecked).Distinct().ToList();
                    SetChecked(0, state0.Count == 1 ? state0[0] : null, false, false);

                    var state1 = validChildren.Select(c => c.IsChecked2).Distinct().ToList();
                    SetChecked(1, state1.Count == 1 ? state1[0] : null, false, false);

                    var state2 = validChildren.Select(c => c.IsChecked3).Distinct().ToList();
                    SetChecked(2, state2.Count == 1 ? state2[0] : null, false, false);
                }
            }
            Parent?.RecalculateParentStates();
        }
    }
}