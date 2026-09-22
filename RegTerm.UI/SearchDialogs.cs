using RegTerm.Core;
using Terminal.Gui;

namespace RegTerm.UI;

public enum SearchOutcome { Found, NotFound, Cancelled, Failed }

public sealed record SearchResult(SearchOutcome Outcome, SearchHit? Hit, string? Error);

/// <summary>
/// Find form and cancellable search progress dialog.
/// </summary>
public static class SearchDialogs
{
    /// <summary>The Find form. Returns null if cancelled or nothing usable was entered.</summary>
    public static SearchQuery? AskForQuery(SearchQuery? previous)
    {
        var text = new TextField(previous?.Text ?? string.Empty)
        {
            X = 1,
            Y = 2,
            Width = Dim.Fill(2)
        };

        var keys = new CheckBox("Key names") { X = 1, Y = 5, Checked = previous?.MatchKeys ?? true };
        var valueNames = new CheckBox("Value names") { X = 1, Y = 6, Checked = previous?.MatchValueNames ?? true };
        var valueData = new CheckBox("Value data") { X = 1, Y = 7, Checked = previous?.MatchData ?? true };
        var whole = new CheckBox("Match whole string only")
        {
            X = 1,
            Y = 9,
            Checked = previous?.WholeStringOnly ?? false
        };

        SearchQuery? result = null;
        var find = new Button("Find", is_default: true);
        var cancel = new Button("Cancel");

        find.Clicked += () =>
        {
            var candidate = new SearchQuery(
                (text.Text.ToString() ?? string.Empty).Trim(),
                keys.Checked,
                valueNames.Checked,
                valueData.Checked,
                whole.Checked);

            if (candidate.Text.Length == 0)
            {
                Dialogs.Error("Nothing to find", "Enter some text to search for.");
                return;
            }
            if (!candidate.IsUsable)
            {
                Dialogs.Error("Nothing to search", "Select at least one of keys, value names or value data.");
                return;
            }

            result = candidate;
            Application.RequestStop();
        };
        cancel.Clicked += () => Application.RequestStop();

        var dialog = Dialogs.Build("Find", 62, 16, find, cancel);
        dialog.Add(
            Dialogs.Caption(1, 1, "Find what"),
            text,
            Dialogs.Caption(1, 4, "Look at"),
            keys, valueNames, valueData,
            whole,
            Dialogs.Hint(1, 11, "Runs from the selected key onward. F3 finds the next."));

        text.SetFocus();
        Application.Run(dialog);
        return result;
    }

    /// <summary>
    /// Runs one <see cref="RegistrySearch.FindNext"/> behind a cancellable progress dialog.
    /// On cancellation the search cursor is left intact, so F3 resumes from the same place.
    /// </summary>
    public static SearchResult Run(RegistrySearch search)
    {
        using var cancellation = new CancellationTokenSource();

        var outcome = SearchOutcome.NotFound;
        SearchHit? hit = null;
        string? error = null;

        var stop = new Button("Stop", is_default: true);
        stop.Clicked += () => cancellation.Cancel();

        var dialog = Dialogs.Build("Searching", 72, 10, stop);
        var counter = Dialogs.Caption(1, 1, "starting...");
        var location = Dialogs.Hint(1, 3, string.Empty);
        dialog.Add(counter, location, Dialogs.Hint(1, 5, "Stop leaves the search where it is; F3 resumes."));

        object? ticker = null;

        // Start after the modal loop is active.
        dialog.Ready += () =>
        {
            ticker = Application.MainLoop.AddTimeout(TimeSpan.FromMilliseconds(120), _ =>
            {
                counter.Text = $"{search.KeysScanned:N0} keys scanned";
                location.Text = Draw.EllipsizeStart(search.Scanning, 66);
                return true;
            });

            Task.Run(() =>
            {
                try
                {
                    hit = search.FindNext(cancellation.Token);
                    outcome = hit is null ? SearchOutcome.NotFound : SearchOutcome.Found;
                }
                catch (OperationCanceledException)
                {
                    outcome = SearchOutcome.Cancelled;
                }
                catch (Exception ex)
                {
                    outcome = SearchOutcome.Failed;
                    error = RegistryErrors.Describe(ex);
                }

                Application.MainLoop.Invoke(() => Application.RequestStop());
            });
        };

        Application.Run(dialog);

        if (ticker is not null) Application.MainLoop.RemoveTimeout(ticker);
        return new SearchResult(outcome, hit, error);
    }
}
