using System;

namespace StudentDesktop.ViewModels;

public class CalendarListItemViewModel(string title, DateTime when, string? detail = null)
{
    public string Title { get; } = title;
    public DateTime When { get; } = when;
    public string? Detail { get; } = detail;
}
