using System;
using System.Collections.Generic;
using System.Linq;
using StudentDesktop.Models;

namespace StudentDesktop.Services;

// SDA-01: pure "is a class session in progress right now" check, kept separate from
// ClassLockService's timer/networking concerns so it's trivially unit-testable.
public static class ClassScheduleEvaluator
{
    private const string ClassSessionKind = "class_session";

    public static bool IsClassInSession(IEnumerable<CalendarItemDto> calendarItems, DateTime now) =>
        calendarItems.Any(item =>
            item.Kind == ClassSessionKind && now >= item.Start && now < item.End);
}
