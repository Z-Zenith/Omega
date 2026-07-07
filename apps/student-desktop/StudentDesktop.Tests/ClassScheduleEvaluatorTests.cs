using System;
using System.Collections.Generic;
using StudentDesktop.Models;
using StudentDesktop.Services;

namespace StudentDesktop.Tests;

public class ClassScheduleEvaluatorTests
{
    private static CalendarItemDto ClassSession(DateTime start, DateTime end) =>
        new("class_session", Guid.NewGuid(), "Maths", start, end, "teacher=A;room=101");

    [Fact]
    public void SDA01_ReturnsTrue_WhenNowIsInsideASessionWindow()
    {
        var now = new DateTime(2026, 7, 7, 10, 30, 0);
        var sessions = new List<CalendarItemDto>
        {
            ClassSession(new DateTime(2026, 7, 7, 10, 0, 0), new DateTime(2026, 7, 7, 11, 0, 0)),
        };

        Assert.True(ClassScheduleEvaluator.IsClassInSession(sessions, now));
    }

    [Fact]
    public void SDA01_ReturnsFalse_BeforeSessionStarts()
    {
        var now = new DateTime(2026, 7, 7, 9, 59, 59);
        var sessions = new List<CalendarItemDto>
        {
            ClassSession(new DateTime(2026, 7, 7, 10, 0, 0), new DateTime(2026, 7, 7, 11, 0, 0)),
        };

        Assert.False(ClassScheduleEvaluator.IsClassInSession(sessions, now));
    }

    [Fact]
    public void SDA01_ReturnsFalse_AtOrAfterSessionEnd()
    {
        var sessions = new List<CalendarItemDto>
        {
            ClassSession(new DateTime(2026, 7, 7, 10, 0, 0), new DateTime(2026, 7, 7, 11, 0, 0)),
        };

        Assert.False(ClassScheduleEvaluator.IsClassInSession(sessions, new DateTime(2026, 7, 7, 11, 0, 0)));
        Assert.False(ClassScheduleEvaluator.IsClassInSession(sessions, new DateTime(2026, 7, 7, 12, 0, 0)));
    }

    [Fact]
    public void SDA01_IgnoresNonClassSessionCalendarItems()
    {
        var now = new DateTime(2026, 7, 7, 10, 30, 0);
        var sessions = new List<CalendarItemDto>
        {
            new("college_event", Guid.NewGuid(), "Fest", new DateTime(2026, 7, 7, 10, 0, 0), new DateTime(2026, 7, 7, 11, 0, 0), null),
            new("todo", Guid.NewGuid(), "Homework", now, now, null),
        };

        Assert.False(ClassScheduleEvaluator.IsClassInSession(sessions, now));
    }

    [Fact]
    public void SDA01_ReturnsTrue_WhenAnyOfMultipleSessionsIsActive()
    {
        var now = new DateTime(2026, 7, 7, 14, 15, 0);
        var sessions = new List<CalendarItemDto>
        {
            ClassSession(new DateTime(2026, 7, 7, 10, 0, 0), new DateTime(2026, 7, 7, 11, 0, 0)),
            ClassSession(new DateTime(2026, 7, 7, 14, 0, 0), new DateTime(2026, 7, 7, 15, 0, 0)),
        };

        Assert.True(ClassScheduleEvaluator.IsClassInSession(sessions, now));
    }

    [Fact]
    public void SDA01_ReturnsFalse_OutsideAnyClassHours()
    {
        var now = new DateTime(2026, 7, 7, 22, 0, 0);
        var sessions = new List<CalendarItemDto>
        {
            ClassSession(new DateTime(2026, 7, 7, 10, 0, 0), new DateTime(2026, 7, 7, 11, 0, 0)),
        };

        Assert.False(ClassScheduleEvaluator.IsClassInSession(sessions, now));
    }
}
