using System.Globalization;
using System.Windows.Data;
namespace SuperDock.SearchConverters;
// Keep the original XAML converter contract, reuse Windock's native icon cache.
public sealed class ExecutableIconConverter : IValueConverter
{
    public object? Convert(object value,Type targetType,object parameter,CultureInfo culture)=>value is string path?ShellIcons.Load(path):null;
    public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)=>Binding.DoNothing;
}
