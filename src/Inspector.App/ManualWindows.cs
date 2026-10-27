using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Inspector.App;

public sealed class KeyboardWindow : Window
{
    public KeyboardWindow()
    {
        Style = (Style)Application.Current.FindResource(typeof(Window));
        Title = "Keyboard checker / Atiq Laptop Inspector"; Width = 940; Height = 540; MinWidth = 800; MinHeight = 440; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new StackPanel { Margin = new(28) };
        root.Children.Add(new TextBlock { Text = "Check every key", FontSize = 25, FontWeight = FontWeights.SemiBold });
        root.Children.Add(new TextBlock { Text = "Press keys and watch them light up. Test modifier combinations and repeat input. Fn and some media keys are handled by firmware and may not appear. Close this window, then enter your result in Manual checks.", Margin = new(0, 12, 0, 18) });
        var keys = new WrapPanel();
        var keyLabels = new Dictionary<Key, Border>();
        var all = new[] { Key.Escape, Key.F1, Key.F2, Key.F3, Key.F4, Key.F5, Key.F6, Key.F7, Key.F8, Key.F9, Key.F10, Key.F11, Key.F12, Key.Oem3 }.Concat(Enumerable.Range((int)Key.D0, 10).Select(i => (Key)i)).Concat(Enumerable.Range((int)Key.A, 26).Select(i => (Key)i)).Concat(new[] { Key.OemMinus, Key.OemPlus, Key.Back, Key.Tab, Key.OemOpenBrackets, Key.OemCloseBrackets, Key.OemPipe, Key.OemSemicolon, Key.OemQuotes, Key.OemComma, Key.OemPeriod, Key.OemQuestion, Key.CapsLock, Key.Enter, Key.LeftShift, Key.RightShift, Key.LeftCtrl, Key.RightCtrl, Key.LeftAlt, Key.RightAlt, Key.Space, Key.Left, Key.Up, Key.Down, Key.Right, Key.Delete, Key.Insert, Key.Home, Key.End, Key.PageUp, Key.PageDown });
        foreach (var key in all.Distinct())
        {
            var border = new Border { Child = new TextBlock { Text = key.ToString(), FontSize = 11, Foreground = Brushes.LightGray, HorizontalAlignment = HorizontalAlignment.Center }, Background = new SolidColorBrush(Color.FromRgb(32, 49, 73)), CornerRadius = new(5), Padding = new(9, 10, 9, 10), Margin = new(3), MinWidth = 42 };
            keyLabels[key] = border; keys.Children.Add(border);
        }
        root.Children.Add(keys);
        var status = new TextBlock { Text = "No keys observed. This is not a keylogger; only key identifiers are displayed temporarily.", Margin = new(0, 18, 0, 10) }; root.Children.Add(status);
        var close = new Button { Content = "Close checker", HorizontalAlignment = HorizontalAlignment.Left }; close.Click += (_, _) => Close(); root.Children.Add(close); Content = new ScrollViewer { Content = root };
        var seen = new HashSet<Key>();
        PreviewKeyDown += (_, args) =>
        {
            var key = args.Key == Key.System ? args.SystemKey : args.Key;
            seen.Add(key);
            if (keyLabels.TryGetValue(key, out var border)) { border.Background = new SolidColorBrush(Color.FromRgb(8, 127, 121)); ((TextBlock)border.Child).Foreground = Brushes.White; }
            status.Text = $"Observed key identifiers: {seen.Count}  /  Last key: {key}  /  Repeat signal: {args.IsRepeat}";
            args.Handled = true;
        };
    }
}

public sealed class DisplayWindow : Window
{
    private int index;
    private readonly (Brush Brush, string Name)[] colors = [(Brushes.White, "White"), (Brushes.Black, "Black"), (Brushes.Red, "Red"), (Brushes.Lime, "Green"), (Brushes.Blue, "Blue"), (new SolidColorBrush(Color.FromRgb(128, 128, 128)), "Gray")];
    private readonly TextBlock instruction;
    public DisplayWindow()
    {
        Style = (Style)Application.Current.FindResource(typeof(Window));
        Title = "Display test / Atiq Laptop Inspector"; WindowStyle = WindowStyle.None; WindowState = WindowState.Maximized;
        var grid = new Grid(); var controls = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Background = new SolidColorBrush(Color.FromArgb(225, 16, 27, 44)), Margin = new(24), Orientation = Orientation.Horizontal };
        instruction = new TextBlock { Foreground = Brushes.White, Margin = new(18), VerticalAlignment = VerticalAlignment.Center, FontSize = 12 }; controls.Children.Add(instruction);
        var next = new Button { Content = "Next color", Margin = new(8), Background = Brushes.DimGray, Foreground = Brushes.White }; next.Click += (_, _) => Next(); controls.Children.Add(next);
        var close = new Button { Content = "Close / Esc", Margin = new(8), Background = Brushes.DimGray, Foreground = Brushes.White }; close.Click += (_, _) => Close(); controls.Children.Add(close);
        grid.Children.Add(controls); Content = grid; Apply();
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); else if (e.Key is Key.Space or Key.Right) Next(); };
    }
    private void Next() { index = (index + 1) % colors.Length; Apply(); }
    private void Apply() { Background = colors[index].Brush; instruction.Text = $"{colors[index].Name} ({index + 1}/6) / inspect pixels, lines and uniformity. Space: next. Esc: close."; }
}
