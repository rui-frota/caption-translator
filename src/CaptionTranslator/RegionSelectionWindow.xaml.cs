using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DrawingRectangle = System.Drawing.Rectangle;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfPoint = System.Windows.Point;

namespace CaptionTranslator;

public partial class RegionSelectionWindow : Window
{
    private WpfPoint? _startPoint;

    public RegionSelectionWindow()
    {
        InitializeComponent();
        KeyDown += (_, eventArgs) =>
        {
            if (eventArgs.Key == Key.Escape)
            {
                DialogResult = false;
            }
        };
    }

    public DrawingRectangle? SelectedRegion { get; private set; }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs eventArgs)
    {
        _startPoint = eventArgs.GetPosition(this);
        SelectionRectangle.Visibility = Visibility.Visible;
        CaptureMouse();
    }

    private void Window_MouseMove(object sender, WpfMouseEventArgs eventArgs)
    {
        if (_startPoint is not { } startPoint || eventArgs.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var currentPoint = eventArgs.GetPosition(this);
        var left = Math.Min(startPoint.X, currentPoint.X);
        var top = Math.Min(startPoint.Y, currentPoint.Y);
        SelectionRectangle.SetValue(Canvas.LeftProperty, left);
        SelectionRectangle.SetValue(Canvas.TopProperty, top);
        SelectionRectangle.Width = Math.Abs(currentPoint.X - startPoint.X);
        SelectionRectangle.Height = Math.Abs(currentPoint.Y - startPoint.Y);
    }

    private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs eventArgs)
    {
        if (_startPoint is not { } startPoint)
        {
            return;
        }

        ReleaseMouseCapture();
        var endPoint = eventArgs.GetPosition(this);
        var startScreenPoint = PointToScreen(startPoint);
        var endScreenPoint = PointToScreen(endPoint);
        var left = (int)Math.Round(Math.Min(startScreenPoint.X, endScreenPoint.X));
        var top = (int)Math.Round(Math.Min(startScreenPoint.Y, endScreenPoint.Y));
        var width = (int)Math.Round(Math.Abs(endScreenPoint.X - startScreenPoint.X));
        var height = (int)Math.Round(Math.Abs(endScreenPoint.Y - startScreenPoint.Y));
        if (width < 10 || height < 10)
        {
            _startPoint = null;
            SelectionRectangle.Visibility = Visibility.Collapsed;
            return;
        }

        SelectedRegion = new DrawingRectangle(left, top, width, height);
        DialogResult = true;
    }
}