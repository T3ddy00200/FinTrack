using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FinTrack.Properties
{
    public static class UIHelpers
    {
        public static DataGridRow? GetDataGridRowUnderMouse<T>(DataGrid grid, MouseButtonEventArgs e)
        {
            var point = e.GetPosition(grid);
            var hit = VisualTreeHelper.HitTest(grid, point);

            DependencyObject current = hit?.VisualHit;
            while (current != null && !(current is DataGridRow))
            {
                current = VisualTreeHelper.GetParent(current);
            }

            return current as DataGridRow;
        }
    }
}
