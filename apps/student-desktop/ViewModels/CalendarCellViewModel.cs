namespace StudentDesktop.ViewModels;

// Row/Column drive Grid.Row/Grid.Column bindings in CalendarView.axaml (header row = 0,
// time-label column = 0). Kind selects the cell's background brush via a XAML style selector.
public class CalendarCellViewModel(int row, int column, string kind, string? text = null, string? subText = null)
{
    public int Row { get; } = row;
    public int Column { get; } = column;
    public string Kind { get; } = kind;
    public string? Text { get; } = text;
    public string? SubText { get; } = subText;
}
