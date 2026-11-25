using System.Windows;
using System.Windows.Controls;
namespace SuperDock;
// Decorative layers must never determine the Dock's desired size.
internal sealed class OverlayBorder : Border
{
    protected override Size MeasureOverride(Size constraint)
    {
        base.MeasureOverride(constraint);
        return new Size(0,0);
    }
}

