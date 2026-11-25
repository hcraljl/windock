using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SuperDock.SearchViews;

public enum MessageDialogKind
{
    Info,
    Question,
    Warning,
    Danger
}

/// <summary>
/// 统一风格的消息/确认弹窗，替代系统 MessageBox（系统弹窗和整体设计不搭）。
/// </summary>
public partial class MessageDialog : Window
{
    private const string InfoGlyph =
        "M12 3.5a8.5 8.5 0 1 0 0 17 8.5 8.5 0 0 0 0-17z M12 11.2v5 M12 7.8v.1";

    private const string QuestionGlyph =
        "M9.4 9.6a2.6 2.6 0 1 1 3.5 2.4c-.7.3-1 .8-1 1.5v.8 M12 17.4v.1";

    private const string WarningGlyph =
        "M12 4.4 21 19.4H3z M12 10v4.1 M12 16.9v.1";

    private MessageDialog(
        string title,
        string message,
        string confirmText,
        string? cancelText,
        MessageDialogKind kind)
    {
        InitializeComponent();
        DialogTheme.Attach(this);

        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;
        ConfirmButton.Content = confirmText;

        if (string.IsNullOrEmpty(cancelText))
        {
            CancelButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            CancelButton.Content = cancelText;
        }

        ApplyKind(kind);

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                DialogResult = false;
                Close();
            }
            else if (e.Key == Key.Enter)
            {
                e.Handled = true;
                DialogResult = true;
                Close();
            }
        };

        Loaded += (_, _) =>
        {
            // 危险操作默认落在“取消”上，避免顺手回车误删
            var focusTarget = kind == MessageDialogKind.Danger && CancelButton.Visibility == Visibility.Visible
                ? CancelButton
                : ConfirmButton;
            focusTarget.Focus();
        };
    }

    public static bool Confirm(
        Window? owner,
        string title,
        string message,
        string confirmText = "确定",
        string cancelText = "取消",
        MessageDialogKind kind = MessageDialogKind.Question)
    {
        return Show(owner, title, message, confirmText, cancelText, kind) == true;
    }

    public static void Info(Window? owner, string title, string message, string confirmText = "知道了")
    {
        Show(owner, title, message, confirmText, null, MessageDialogKind.Info);
    }

    private static bool? Show(
        Window? owner,
        string title,
        string message,
        string confirmText,
        string? cancelText,
        MessageDialogKind kind)
    {
        var dialog = new MessageDialog(title, message, confirmText, cancelText, kind);
        if (owner is { IsVisible: true })
        {
            dialog.Owner = owner;
        }
        else
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        return dialog.ShowDialog();
    }

    private void ApplyKind(MessageDialogKind kind)
    {
        IconGlyph.Data = Geometry.Parse(kind switch
        {
            MessageDialogKind.Question => QuestionGlyph,
            MessageDialogKind.Info => InfoGlyph,
            _ => WarningGlyph
        });

        ConfirmButton.SetResourceReference(BackgroundProperty, "PrimaryBrush");
        ConfirmButton.SetResourceReference(BorderBrushProperty, "PrimaryBrush");
        ConfirmButton.SetResourceReference(ForegroundProperty, "PrimaryTextBrush");

        switch (kind)
        {
            case MessageDialogKind.Danger:
                IconBadge.SetResourceReference(BackgroundProperty, "DangerBrush");
                IconGlyph.SetResourceReference(Shape.StrokeProperty, "DangerTextBrush");
                ConfirmButton.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xD6, 0x45, 0x45));
                ConfirmButton.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xD6, 0x45, 0x45));
                ConfirmButton.Foreground = System.Windows.Media.Brushes.White;
                break;

            case MessageDialogKind.Warning:
                IconBadge.SetResourceReference(BackgroundProperty, "DangerBrush");
                IconGlyph.Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE8, 0xA3, 0x3D));
                break;

            default:
                IconBadge.SetResourceReference(BackgroundProperty, "AccentSoftBrush");
                IconGlyph.SetResourceReference(Shape.StrokeProperty, "AccentBrush");
                break;
        }
    }

    private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            try
            {
                DragMove();
            }
            catch
            {
                // 拖动过程中关闭会抛异常，忽略
            }
        }
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
