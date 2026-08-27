

using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace ProjectToPromptScanner
{
    public class TreeDepthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is TreeViewItem item)) return 0.0;
            int depth = 0;
            ItemsControl parent = ItemsControl.ItemsControlFromItemContainer(item);
            while (parent != null && parent is TreeViewItem) {
                depth++;
                parent = ItemsControl.ItemsControlFromItemContainer(parent);
            }
            return (double)(depth * 18);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}