using System.Windows;

namespace SimpleToDoMemo.Views;

public partial class LanguageWindow : Window
{
    public string Choice { get; private set; } = "ko";

    public LanguageWindow()
    {
        InitializeComponent();
        KoBtn.Click += (_, _) => { Choice = "ko"; DialogResult = true; };
        EnBtn.Click += (_, _) => { Choice = "en"; DialogResult = true; };
    }
}
