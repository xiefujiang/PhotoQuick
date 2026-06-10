using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfButton = System.Windows.Controls.Button;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace PhotoQuick.Views;

public sealed class TextInputDialog : Window
{
    private readonly WpfTextBox _textBox;

    private TextInputDialog(string title, string message, string defaultValue)
    {
        Title = title;
        Width = 420;
        Height = 170;
        MinWidth = 360;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush(0x17, 0x1A, 0x20);
        Foreground = Brush(0xF3, 0xF6, 0xFA);

        var root = new Grid { Margin = new Thickness(16) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        root.Children.Add(new TextBlock
        {
            Text = message,
            Foreground = Brush(0xF3, 0xF6, 0xFA),
            Margin = new Thickness(0, 0, 0, 8)
        });

        _textBox = new WpfTextBox
        {
            Text = defaultValue,
            MinWidth = 340,
            Margin = new Thickness(0, 0, 0, 16),
            Padding = new Thickness(10, 6, 10, 6),
            Background = Brush(0x24, 0x29, 0x33),
            Foreground = Brush(0xF3, 0xF6, 0xFA),
            BorderBrush = Brush(0x4B, 0xA3, 0xFF),
            CaretBrush = Brush(0xF3, 0xF6, 0xFA)
        };
        Grid.SetRow(_textBox, 1);
        root.Children.Add(_textBox);

        var buttons = new StackPanel
        {
            Orientation = WpfOrientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right
        };

        var ok = new WpfButton
        {
            Content = "确定",
            MinWidth = 72,
            Margin = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(12, 6, 12, 6),
            IsDefault = true,
            Background = Brush(0x4B, 0xA3, 0xFF),
            Foreground = Brush(0x07, 0x11, 0x1D),
            BorderBrush = Brush(0x73, 0xBA, 0xFF)
        };
        ok.Click += (_, _) => { DialogResult = true; Close(); };

        var cancel = new WpfButton
        {
            Content = "取消",
            MinWidth = 72,
            Padding = new Thickness(12, 6, 12, 6),
            IsCancel = true,
            Background = Brush(0x2A, 0x30, 0x3B),
            Foreground = Brush(0xF3, 0xF6, 0xFA),
            BorderBrush = Brush(0x3A, 0x41, 0x4E)
        };
        cancel.Click += (_, _) => { DialogResult = false; Close(); };

        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        Grid.SetRow(buttons, 2);
        root.Children.Add(buttons);

        Content = root;
        Loaded += (_, _) =>
        {
            _textBox.Focus();
            _textBox.SelectAll();
        };
    }

    public string Response => _textBox.Text.Trim();

    public static string? Prompt(string title, string message, string defaultValue)
    {
        var dialog = new TextInputDialog(title, message, defaultValue)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };

        return dialog.ShowDialog() == true ? dialog.Response : null;
    }

    private static SolidColorBrush Brush(byte r, byte g, byte b) =>
        new(System.Windows.Media.Color.FromRgb(r, g, b));
}
