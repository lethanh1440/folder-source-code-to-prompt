using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ProjectToPromptScanner
{
    public class ImportItem : INotifyPropertyChanged
    {
        private bool _isChecked = true;

        public string Path { get; set; }
        public string Code { get; set; }
        public bool IsNew { get; set; }

        public string StatusText => IsNew ? "ADD" : "EDIT";
        public string StatusColor => IsNew ? "#28A745" : "#007BFF";

        public int TotalChanges { get; set; }
        public int SuccessChanges { get; set; }
        public int FailedChanges { get; set; }
        public string ErrorDetails { get; set; }

        public string ChangeSummary
        {
            get {
                if (IsNew) return "Tạo file mới";
                if (TotalChanges == 0) return "Không có khối thay đổi nào được nhận diện";
                return $"Áp dụng: {SuccessChanges}/{TotalChanges} khối thành công";
            }
        }

        public string ErrorSummary => FailedChanges > 0 ? $"Lỗi: {FailedChanges} khối thất bại. Chi tiết:\n {ErrorDetails}" : "";
        public string SummaryColor => FailedChanges > 0 ? "#DC3545" : (TotalChanges == 0 && !IsNew ? "#856404" : "#28A745");

        public bool IsChecked
        {
            get => _isChecked;
            set {
                if (_isChecked != value) {
                    _isChecked = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}