using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudentDesktop.Services;

namespace StudentDesktop.ViewModels;

// SDA-14: college-wide events, registered events, personal to-dos, and custom entries in a
// single calendar. Class sessions (from the student's timetable) are overlaid on the same
// week grid as events, Google-Calendar-style, per the desktop app's design direction — the
// other three kinds are distinguished as separate labeled lists below the grid so all four
// stay visually distinguishable even though only two are time-slot-shaped.
public partial class CalendarViewModel : ViewModelBase
{
    private static readonly int FirstHour = 7;
    private static readonly int LastHour = 20;
    private static readonly string[] DayNames = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

    private readonly ApiClient _apiClient;

    public ObservableCollection<CalendarCellViewModel> GridCells { get; } = [];
    public ObservableCollection<CalendarListItemViewModel> Todos { get; } = [];
    public ObservableCollection<CalendarListItemViewModel> CustomEntries { get; } = [];
    public ObservableCollection<CalendarListItemViewModel> OtherEvents { get; } = [];

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    public CalendarViewModel(ApiClient apiClient)
    {
        _apiClient = apiClient;
        BuildGridSkeleton();
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var response = await _apiClient.GetMyCalendarAsync();
            PlaceItems(response.Items);
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void BuildGridSkeleton()
    {
        GridCells.Add(new CalendarCellViewModel(0, 0, "header"));

        var monday = ThisWeekMonday();
        for (var day = 0; day < 7; day++)
        {
            var date = monday.AddDays(day);
            GridCells.Add(new CalendarCellViewModel(0, day + 1, "header", DayNames[day], date.ToString("MMM d")));
        }

        for (var hour = FirstHour; hour <= LastHour; hour++)
        {
            var row = hour - FirstHour + 1;
            GridCells.Add(new CalendarCellViewModel(row, 0, "hour", $"{hour}:00"));
        }
    }

    private void PlaceItems(System.Collections.Generic.List<Models.CalendarItemDto> items)
    {
        GridCells.Where(c => c.Kind is "class_session" or "college_event-grid").ToList()
            .ForEach(c => GridCells.Remove(c));
        Todos.Clear();
        CustomEntries.Clear();
        OtherEvents.Clear();

        var monday = ThisWeekMonday();
        var weekEnd = monday.AddDays(7);

        foreach (var item in items)
        {
            switch (item.Kind)
            {
                case "todo":
                    Todos.Add(new CalendarListItemViewModel(item.Title, item.Start,
                        item.Extra == "completed=true" ? "Completed" : "Due"));
                    break;
                case "custom_entry":
                    CustomEntries.Add(new CalendarListItemViewModel(item.Title, item.Start));
                    break;
                case "college_event":
                    var registered = item.Extra == "registered=true";
                    if (item.Start >= monday && item.Start < weekEnd && item.Start.Hour >= FirstHour && item.Start.Hour <= LastHour)
                    {
                        var col = (item.Start.Date - monday.Date).Days + 1;
                        var row = item.Start.Hour - FirstHour + 1;
                        GridCells.Add(new CalendarCellViewModel(row, col, "college_event-grid", item.Title,
                            registered ? "Registered" : null));
                    }
                    else
                    {
                        OtherEvents.Add(new CalendarListItemViewModel(item.Title, item.Start, registered ? "Registered" : null));
                    }
                    break;
                case "class_session":
                    var classCol = (item.Start.Date - monday.Date).Days + 1;
                    var classRow = item.Start.Hour - FirstHour + 1;
                    if (classCol is >= 1 and <= 7 && classRow >= 1 && item.Start.Hour <= LastHour)
                    {
                        GridCells.Add(new CalendarCellViewModel(classRow, classCol, "class_session", item.Title, item.Extra));
                    }
                    break;
            }
        }
    }

    private static DateTime ThisWeekMonday()
    {
        var today = DateTime.Now.Date;
        var offset = today.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)today.DayOfWeek - 1;
        return today.AddDays(-offset);
    }
}
