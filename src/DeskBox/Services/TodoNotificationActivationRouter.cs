namespace DeskBox.Services;

internal sealed record TodoNotificationActivationRouteResult(
    string Disposition,
    bool Succeeded,
    string? WidgetId,
    string? ItemId,
    string? Action,
    string? SnoozeSelection,
    DateTimeOffset? SnoozedUntil,
    bool TargetRequested,
    bool TargetPresented,
    bool RefreshRequested,
    bool RefreshCompleted,
    bool ConfirmationRequested);

internal static class TodoNotificationActivationRouter
{
    internal const string SourceValue = "todoReminder";
    internal const string ActionComplete = "complete";
    internal const string ActionSnooze = "snooze";
    internal const string LegacyActionSnooze10 = "snooze10";
    internal const string SnoozeInputId = "todoSnooze";
    internal const string Snooze10Minutes = "10m";
    internal const string Snooze30Minutes = "30m";
    internal const string Snooze1Hour = "1h";
    internal const string SnoozeTomorrow = "tomorrow";

    internal const string DispositionNotTodoReminder = "NotTodoReminder";
    internal const string DispositionOpened = "Opened";
    internal const string DispositionTargetUnavailable = "TargetUnavailable";
    internal const string DispositionCompleted = "Completed";
    internal const string DispositionSnoozed = "Snoozed";
    internal const string DispositionRejectedMissingTarget = "RejectedMissingTarget";
    internal const string DispositionRejectedUnsupportedAction = "RejectedUnsupportedAction";
    internal const string DispositionRejectedUnsupportedSnooze = "RejectedUnsupportedSnooze";
    internal const string DispositionServiceUnavailable = "ServiceUnavailable";
    internal const string DispositionMutationFailed = "MutationFailed";

    internal static bool IsTodoReminder(
        IReadOnlyDictionary<string, string> arguments)
    {
        return TryGetValue(arguments, "source", out string? source) &&
               string.Equals(source, SourceValue, StringComparison.OrdinalIgnoreCase);
    }

    internal static async Task<TodoNotificationActivationRouteResult> RouteAsync(
        IReadOnlyDictionary<string, string> arguments,
        IReadOnlyDictionary<string, string> userInput,
        TodoReminderService? reminderService,
        Func<DateTimeOffset> clock,
        TimeZoneInfo localTimeZone,
        Func<string?, string?, bool, Task<bool>> showTargetAsync,
        Func<string?, Task<bool>> refreshAsync,
        Func<string, Task> showSnoozeConfirmationAsync)
    {
        string? widgetId = GetOptionalValue(arguments, "widgetId");
        string? itemId = GetOptionalValue(arguments, "itemId");
        string? action = GetOptionalValue(arguments, "action");

        if (!IsTodoReminder(arguments))
        {
            return CreateResult(
                DispositionNotTodoReminder,
                succeeded: false,
                widgetId,
                itemId,
                action);
        }

        if (string.IsNullOrWhiteSpace(action))
        {
            bool preferTodayFilter =
                TryGetValue(arguments, "view", out string? view) &&
                string.Equals(view, "today", StringComparison.OrdinalIgnoreCase);
            bool targetPresented = await showTargetAsync(
                widgetId,
                itemId,
                preferTodayFilter);
            return CreateResult(
                targetPresented ? DispositionOpened : DispositionTargetUnavailable,
                succeeded: targetPresented,
                widgetId,
                itemId,
                action: null,
                targetRequested: true,
                targetPresented: targetPresented);
        }

        if (string.Equals(action, ActionComplete, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(widgetId) || string.IsNullOrWhiteSpace(itemId))
            {
                return CreateResult(
                    DispositionRejectedMissingTarget,
                    succeeded: false,
                    widgetId,
                    itemId,
                    ActionComplete);
            }

            if (reminderService is null)
            {
                return CreateResult(
                    DispositionServiceUnavailable,
                    succeeded: false,
                    widgetId,
                    itemId,
                    ActionComplete);
            }

            bool completed = await reminderService.CompleteAsync(widgetId, itemId);
            if (!completed)
            {
                return CreateResult(
                    DispositionMutationFailed,
                    succeeded: false,
                    widgetId,
                    itemId,
                    ActionComplete);
            }

            bool refreshCompleted = await refreshAsync(widgetId);
            return CreateResult(
                DispositionCompleted,
                succeeded: true,
                widgetId,
                itemId,
                ActionComplete,
                refreshRequested: true,
                refreshCompleted: refreshCompleted);
        }

        if (string.Equals(action, ActionSnooze, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(action, LegacyActionSnooze10, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(widgetId) || string.IsNullOrWhiteSpace(itemId))
            {
                return CreateResult(
                    DispositionRejectedMissingTarget,
                    succeeded: false,
                    widgetId,
                    itemId,
                    action);
            }

            string? selection = ResolveSnoozeSelection(action, userInput);
            if (selection is null)
            {
                return CreateResult(
                    DispositionRejectedUnsupportedSnooze,
                    succeeded: false,
                    widgetId,
                    itemId,
                    action);
            }

            if (reminderService is null)
            {
                return CreateResult(
                    DispositionServiceUnavailable,
                    succeeded: false,
                    widgetId,
                    itemId,
                    action,
                    selection);
            }

            DateTimeOffset snoozedUntil = GetSnoozedUntil(
                selection,
                clock(),
                localTimeZone);
            bool snoozed = await reminderService.SnoozeUntilAsync(
                widgetId,
                itemId,
                snoozedUntil);
            if (!snoozed)
            {
                return CreateResult(
                    DispositionMutationFailed,
                    succeeded: false,
                    widgetId,
                    itemId,
                    action,
                    selection,
                    snoozedUntil);
            }

            bool refreshCompleted = await refreshAsync(widgetId);
            await showSnoozeConfirmationAsync(selection);
            return CreateResult(
                DispositionSnoozed,
                succeeded: true,
                widgetId,
                itemId,
                action,
                selection,
                snoozedUntil,
                refreshRequested: true,
                refreshCompleted: refreshCompleted,
                confirmationRequested: true);
        }

        return CreateResult(
            DispositionRejectedUnsupportedAction,
            succeeded: false,
            widgetId,
            itemId,
            action);
    }

    internal static DateTimeOffset GetSnoozedUntil(
        string selection,
        DateTimeOffset now,
        TimeZoneInfo localTimeZone)
    {
        return selection switch
        {
            Snooze10Minutes => now.AddMinutes(10),
            Snooze30Minutes => now.AddMinutes(30),
            Snooze1Hour => now.AddHours(1),
            SnoozeTomorrow => GetTomorrowAtNine(now, localTimeZone),
            _ => throw new ArgumentOutOfRangeException(
                nameof(selection),
                selection,
                "Unsupported Todo notification snooze selection.")
        };
    }

    private static string? ResolveSnoozeSelection(
        string action,
        IReadOnlyDictionary<string, string> userInput)
    {
        if (string.Equals(
                action,
                LegacyActionSnooze10,
                StringComparison.OrdinalIgnoreCase))
        {
            return Snooze10Minutes;
        }

        if (!TryGetValue(userInput, SnoozeInputId, out string? selected))
        {
            return null;
        }

        string normalized = selected.Trim().ToLowerInvariant();
        return normalized switch
        {
            Snooze10Minutes => Snooze10Minutes,
            Snooze30Minutes => Snooze30Minutes,
            Snooze1Hour => Snooze1Hour,
            SnoozeTomorrow => SnoozeTomorrow,
            _ => null
        };
    }

    private static DateTimeOffset GetTomorrowAtNine(
        DateTimeOffset now,
        TimeZoneInfo localTimeZone)
    {
        DateTime localNow = TimeZoneInfo.ConvertTime(now, localTimeZone).DateTime;
        DateTime localTomorrowAtNine = DateTime.SpecifyKind(
            localNow.Date.AddDays(1).AddHours(9),
            DateTimeKind.Unspecified);
        TimeSpan offset = localTimeZone.GetUtcOffset(localTomorrowAtNine);
        return new DateTimeOffset(localTomorrowAtNine, offset);
    }

    private static string? GetOptionalValue(
        IReadOnlyDictionary<string, string> values,
        string key)
    {
        return TryGetValue(values, key, out string? value) &&
               !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;
    }

    private static bool TryGetValue(
        IReadOnlyDictionary<string, string> values,
        string key,
        out string value)
    {
        if (values.TryGetValue(key, out string? exactValue))
        {
            value = exactValue ?? string.Empty;
            return true;
        }

        foreach ((string candidateKey, string candidateValue) in values)
        {
            if (string.Equals(candidateKey, key, StringComparison.OrdinalIgnoreCase))
            {
                value = candidateValue ?? string.Empty;
                return true;
            }
        }

        value = string.Empty;
        return false;
    }

    private static TodoNotificationActivationRouteResult CreateResult(
        string disposition,
        bool succeeded,
        string? widgetId,
        string? itemId,
        string? action,
        string? snoozeSelection = null,
        DateTimeOffset? snoozedUntil = null,
        bool targetRequested = false,
        bool targetPresented = false,
        bool refreshRequested = false,
        bool refreshCompleted = false,
        bool confirmationRequested = false)
    {
        return new TodoNotificationActivationRouteResult(
            disposition,
            succeeded,
            widgetId,
            itemId,
            action,
            snoozeSelection,
            snoozedUntil,
            targetRequested,
            targetPresented,
            refreshRequested,
            refreshCompleted,
            confirmationRequested);
    }
}
