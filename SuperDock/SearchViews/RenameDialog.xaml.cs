using System.IO;
using System.Windows;
using System.Windows.Input;

namespace SuperDock.SearchViews;

public partial class RenameDialog : Window
{
    public RenameDialog(string originalName, string fullPath)
    {
        InitializeComponent();
        DialogTheme.Attach(this);

        OriginalName = originalName;
        FullPath = fullPath;
        NameBox.Text = originalName;
        HintText.Text = Path.GetDirectoryName(fullPath) ?? string.Empty;

        Loaded += (_, _) =>
        {
            NameBox.Focus();
            var extension = Path.GetExtension(originalName);
            var selectLength = extension.Length > 0 && extension.Length < originalName.Length
                ? originalName.Length - extension.Length
                : originalName.Length;
            NameBox.Select(0, selectLength);
        };

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                Confirm();
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                DialogResult = false;
                Close();
            }
        };
    }

    public string OriginalName { get; }

    public string FullPath { get; }

    public string NewName => NameBox.Text.Trim();

    private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Confirm_Click(object sender, RoutedEventArgs e) => Confirm();

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Confirm()
    {
        var name = NewName;
        if (name.Length == 0)
        {
            HintText.Text = "名称不能为空";
            return;
        }

        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            HintText.Text = "名称包含非法字符";
            return;
        }

        DialogResult = true;
        Close();
    }
}
