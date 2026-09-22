using Reggi.Core;
using Terminal.Gui;

namespace Reggi.UI;

/// <summary>
/// Terminal UI controller.
/// </summary>
public sealed class RegistryApplication(IRegistryService registryService)
{
    private const int _typeAheadTimeoutMs = 800;

    private readonly IRegistryService _registry = registryService;
    private readonly KeyTree _tree = new(registryService);
    private readonly ValueListSource _valueSource = new();

    private KeyTreeSource _treeSource = null!;
    private PathBarView _pathBar = null!;
    private FrameView _keysFrame = null!;
    private FrameView _valuesFrame = null!;
    private ListView _keysList = null!;
    private ListView _valuesList = null!;

    private RegistryPath _current;
    private SearchQuery? _lastQuery;
    private RegistrySearch? _activeSearch;
    private string _typeAheadBuffer = string.Empty;
    private DateTime _typeAheadLast = DateTime.MinValue;

    /// <summary>Set while rebuilding a list, so selection events do not re-enter.</summary>
    private bool _suppressSelectionEvents;

    public void Run(bool useNetDriver = false, Action<string>? startupTrace = null)
    {
        startupTrace?.Invoke("Before Application.Init");
        if (useNetDriver)
            Application.UseSystemConsole = true;
        Application.Init();

        startupTrace?.Invoke($"After Application.Init; driver={Application.Driver.GetType().Name}");
        try
        {
            Theme.Apply();
            startupTrace?.Invoke("After Theme.Apply");
            BuildLayout();
            startupTrace?.Invoke("After BuildLayout");

            _tree.LoadHives();
            startupTrace?.Invoke("After LoadHives");
            _treeSource = new KeyTreeSource(_tree);
            SyncTree(0);
            startupTrace?.Invoke("After SyncTree");

            _keysList.SetFocus();
            startupTrace?.Invoke("Before Application.Run");
            Application.Run();
            startupTrace?.Invoke("After Application.Run");
        }
        finally
        {
            Application.Shutdown();
            startupTrace?.Invoke("After Application.Shutdown");
        }
    }

    private void BuildLayout()
    {
        var top = Application.Top;
        top.ColorScheme = Theme.Pane;

        _pathBar = new PathBarView { X = 0, Y = 0, Width = Dim.Fill(), ColorScheme = Theme.PathBar };

        _keysFrame = new FrameView("Keys")
        {
            X = 0,
            Y = 1,
            Width = Dim.Percent(38),
            Height = Dim.Fill(1),
            ColorScheme = Theme.Frame
        };

        _valuesFrame = new FrameView("Values")
        {
            X = Pos.Right(_keysFrame),
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
            ColorScheme = Theme.Frame
        };

        _keysList = new ListView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            AllowsMarking = false,
            ColorScheme = Theme.Pane
        };
        _keysFrame.Add(_keysList);

        var header = new ValueHeaderView { X = 0, Y = 0, Width = Dim.Fill(), ColorScheme = Theme.Pane };
        _valuesList = new ListView
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            AllowsMarking = false,
            ColorScheme = Theme.Pane
        };
        _valuesFrame.Add(header, _valuesList);

        ScrollBars.AttachVertical(_keysList);
        ScrollBars.AttachVertical(_valuesList);

        WireKeysPane();
        WireValuesPane();
        WireGlobalShortcuts(top);

        top.Add(_pathBar, _keysFrame, _valuesFrame, BuildStatusBar());
    }

    private StatusBar BuildStatusBar() => new(
    [
        new StatusItem(Key.F1, "~F1~ Help", Dialogs.Help),
        new StatusItem(Key.CtrlMask | Key.F, "~^F~ Find", StartSearch),
        new StatusItem(Key.F3, "~F3~ Next", FindNextMatch),
        new StatusItem(Key.F2, "~F2~ Rename", RenameSelected),
        new StatusItem(Key.F5, "~F5~ Reload", ReloadCurrent),
        new StatusItem(Key.F6, "~F6~ New Value", CreateValue),
        new StatusItem(Key.F7, "~F7~ New Key", CreateKey),
        new StatusItem(Key.DeleteChar, "~Del~ Delete", DeleteSelected),
        new StatusItem(Key.CtrlMask | Key.Q, "~^Q~ Quit", () => Application.RequestStop())
    ])
    { ColorScheme = Theme.Status };

    private void WireKeysPane()
    {
        _keysList.SelectedItemChanged += _ =>
        {
            if (_suppressSelectionEvents) return;
            UpdateCurrentFromTree();
        };

        _keysList.OpenSelectedItem += _ => StepInto();
        _keysList.Enter += _ => RefreshFrameTitles();

        _keysList.KeyPress += e =>
        {
            switch (e.KeyEvent.Key)
            {
                case Key.CursorRight:
                    StepInto();
                    break;
                case Key.Enter:
                    StepInto();
                    break;
                case Key.CursorLeft:
                    StepOut();
                    break;
                case Key.Space:
                    ToggleSelected();
                    break;
                case Key.Tab:
                    _valuesList.SetFocus();
                    break;
                case Key.CursorUp:
                case Key.CursorDown:
                case Key.PageUp:
                case Key.PageDown:
                case Key.Home:
                case Key.End:
                    return;
                default:
                    if (!HandleTypeAhead(e.KeyEvent.Key)) return;
                    break;
            }
            e.Handled = true;
        };

        _keysList.MouseClick += _ => _keysList.SetFocus();
    }

    private void WireValuesPane()
    {
        _valuesList.OpenSelectedItem += _ => EditSelectedValue();
        _valuesList.Enter += _ => RefreshFrameTitles();

        _valuesList.KeyPress += e =>
        {
            switch (e.KeyEvent.Key)
            {
                case Key.Tab:
                    _keysList.SetFocus();
                    break;
                case Key.Backspace:
                    _keysList.SetFocus();
                    StepOut();
                    break;
                case Key.Enter:
                case Key.CursorRight:
                    EditSelectedValue();
                    break;
                default:
                    return;
            }
            e.Handled = true;
        };

        _valuesList.MouseClick += _ => _valuesList.SetFocus();
    }

    private void WireGlobalShortcuts(Toplevel top)
    {
        top.KeyPress += e =>
        {
            var key = e.KeyEvent.Key;

            if (key == Key.F1) Dialogs.Help();
            else if (IsCtrl(key, 'f')) StartSearch();
            else if (key == Key.F3) FindNextMatch();
            else if (key == Key.F2) RenameSelected();
            else if (key == Key.F5) ReloadCurrent();
            else if (key == Key.F6) CreateValue();
            else if (key == Key.F7) CreateKey();
            // F8 supports terminals that do not report Delete reliably.
            else if (key == Key.DeleteChar || key == Key.Delete || key == Key.F8) DeleteSelected();
            else if (key == Key.Backspace) StepOut();
            else if (IsCtrl(key, 'q')) Application.RequestStop();
            else return;

            e.Handled = true;
        };
    }

    /// <summary>
    /// Matches Ctrl+letter across terminal driver key encodings.
    /// </summary>
    private static bool IsCtrl(Key key, char letter)
    {
        if ((key & Key.CtrlMask) == 0) return false;

        var code = (int)(key & ~(Key.CtrlMask | Key.ShiftMask | Key.AltMask));
        var upper = char.ToUpperInvariant(letter);

        return code == upper || code == char.ToLowerInvariant(letter) || code == upper - 'A' + 1;
    }

    private void StepInto()
    {
        var index = _keysList.SelectedItem;
        var node = _tree[index];
        if (node is null) return;

        if (!node.HasChildren) return;

        if (!node.Expanded)
        {
            _tree.Expand(index);
            SyncTree(index);
            return;
        }

        var child = _tree.FirstChildIndex(index);
        if (child >= 0) SyncTree(child);
    }

    private void StepOut()
    {
        var index = _keysList.SelectedItem;
        var node = _tree[index];
        if (node is null) return;

        if (node.Expanded)
        {
            _tree.Collapse(index);
            SyncTree(index);
            return;
        }

        var parent = _tree.ParentIndex(index);
        if (parent.HasValue) SyncTree(parent.Value);
    }

    private void ToggleSelected()
    {
        var index = _keysList.SelectedItem;
        if (_tree.Toggle(index)) SyncTree(index);
    }

    private void ReloadCurrent()
    {
        var index = _keysList.SelectedItem;
        _tree.Reload(index);
        SyncTree(index);
    }

    /// <summary>Rebinds the tree list after a structural change and restores the selection.</summary>
    private void SyncTree(int selectIndex)
    {
        var target = _tree.Count == 0 ? 0 : Math.Clamp(selectIndex, 0, _tree.Count - 1);

        _suppressSelectionEvents = true;
        _keysList.Source = _treeSource;           // resets selection and scroll offset
        if (_tree.Count > 0) _keysList.SelectedItem = target;
        _suppressSelectionEvents = false;

        ScrollTreeIntoView(target);
        UpdateCurrentFromTree();
    }

    private void ScrollTreeIntoView(int index)
    {
        var height = _keysList.Bounds.Height;
        if (height <= 0 || _tree.Count == 0) return;

        var half = Math.Max(0, height / 2 - 1);
        var topItem = Math.Clamp(index - half, 0, Math.Max(0, _tree.Count - height));
        _keysList.TopItem = topItem;
        _keysList.SetNeedsDisplay();
    }

    private void UpdateCurrentFromTree()
    {
        var node = _tree[_keysList.SelectedItem];
        if (node is null) return;

        _current = node.Path;
        _pathBar.Path = _current;
        LoadValues(0);
    }

    private void LoadValues(int? preferredSelection)
    {
        var desired = preferredSelection ?? _valuesList.SelectedItem;
        var listing = _registry.GetValues(_current);

        if (listing.Denied)
            _valueSource.SetNotice(listing.AccessError!, isError: true);
        else if (listing.Values.Count == 0)
            _valueSource.SetNotice("(this key has no values)", isError: false);
        else
            _valueSource.SetValues(listing.Values);

        _suppressSelectionEvents = true;
        _valuesList.Source = _valueSource;
        if (_valueSource.Count > 0)
            _valuesList.SelectedItem = Math.Clamp(desired, 0, _valueSource.Count - 1);
        _suppressSelectionEvents = false;

        _valuesList.SetNeedsDisplay();
        RefreshFrameTitles();
    }

    /// <summary>Shows counts in the frame titles and marks which pane has focus.</summary>
    private void RefreshFrameTitles()
    {
        _keysFrame.Title = _keysList.HasFocus ? "Keys ●" : "Keys";
        _valuesFrame.Title = _valueSource.ShowingNotice
            ? (_valuesList.HasFocus ? "Values ●" : "Values")
            : $"Values ({_valueSource.Count})" + (_valuesList.HasFocus ? " ●" : string.Empty);
    }

    private RegistryValueItem? SelectedValue() =>
        _valueSource.ShowingNotice ? null : _valueSource.At(_valuesList.SelectedItem);

    private void EditSelectedValue()
    {
        if (!_valuesList.HasFocus && _valueSource.ShowingNotice)
        {
            Dialogs.Error("Nothing to edit", "The selected key has no values to edit.");
            return;
        }

        if (!_valuesList.HasFocus)
        {
            _valuesList.SetFocus();
            if (_valuesList.SelectedItem < 0) _valuesList.SelectedItem = 0;
        }

        var value = SelectedValue();
        if (value is null) return;

        if (value.Kind == RegistryValueType.Unknown)
        {
            Dialogs.Error("Cannot edit value",
                "This registry data type is not supported. It is shown read-only to avoid changing its type or data.");
            return;
        }

        var parsed = Dialogs.EditValue(value);
        if (parsed is null) return;

        var selection = _valuesList.SelectedItem;
        if (TryWrite(() => _registry.SetValue(_current, value.Name, parsed, value.Kind), "Cannot save value"))
            LoadValues(selection);
    }

    private void CreateValue()
    {
        var request = Dialogs.NewValue(_current);
        if (request is null) return;

        if (TryWrite(() => _registry.CreateValue(_current, request.Name, request.Value, request.Kind),
                     "Cannot create value"))
            LoadValues(null);
    }

    private void CreateKey()
    {
        var name = Dialogs.NewKeyName(_current);
        if (name is null) return;

        if (!TryWrite(() => _registry.CreateSubKey(_current, name), "Cannot create key")) return;

        var index = _tree.IndexOf(_current);
        if (index < 0) return;

        _tree.Reload(index);
        var created = _tree.IndexOf(_current.Child(name));
        SyncTree(created >= 0 ? created : index);
    }

    private void DeleteSelected()
    {
        if (_keysList.HasFocus) DeleteSelectedKey();
        else DeleteSelectedValue();
    }

    private void DeleteSelectedKey()
    {
        var index = _keysList.SelectedItem;
        var node = _tree[index];
        if (node is null) return;

        if (node.IsHive)
        {
            Dialogs.Error("Cannot delete", "Root hives cannot be deleted.");
            return;
        }

        var parentIndex = _tree.ParentIndex(index);
        if (parentIndex is null) return;
        var parent = _tree[parentIndex.Value];
        if (parent is null) return;

        if (!Dialogs.Confirm("Delete Key",
                $"Delete '{node.Name}' and everything under it?", "Delete"))
            return;

        if (!TryWrite(() => _registry.DeleteSubKeyTree(parent.Path, node.Name), "Cannot delete key")) return;

        _tree.Reload(parentIndex.Value);
        SyncTree(parentIndex.Value);
    }

    private void DeleteSelectedValue()
    {
        var value = SelectedValue();
        if (value is null) return;

        if (value.IsUnset)
        {
            Dialogs.Error("Cannot delete", "The (Default) value is not set.");
            return;
        }

        if (!Dialogs.Confirm("Delete Value", $"Delete value '{value.DisplayName}'?", "Delete")) return;

        var selection = _valuesList.SelectedItem;
        if (TryWrite(() => _registry.DeleteValue(_current, value.Name), "Cannot delete value"))
            LoadValues(selection);
    }

    /// <summary>F2. Renames whichever pane has focus.</summary>
    private void RenameSelected()
    {
        if (_keysList.HasFocus) RenameSelectedKey();
        else RenameSelectedValue();
    }

    private void RenameSelectedKey()
    {
        var index = _keysList.SelectedItem;
        var node = _tree[index];
        if (node is null) return;

        if (node.IsHive)
        {
            Dialogs.Error("Cannot rename", "Hives cannot be renamed.");
            return;
        }

        var parentIndex = _tree.ParentIndex(index);
        var parentPath = node.Path.Parent();
        if (parentIndex is null || parentPath is null) return;

        var newName = Dialogs.Rename("key", node.Name, parentPath.Value);
        if (newName is null) return;

        if (!TryWrite(() => _registry.RenameSubKey(node.Path, newName), "Cannot rename key")) return;

        _tree.Reload(parentIndex.Value);
        var renamed = _tree.IndexOf(parentPath.Value.Child(newName));
        SyncTree(renamed >= 0 ? renamed : parentIndex.Value);
        _keysList.SetFocus();
    }

    private void RenameSelectedValue()
    {
        var value = SelectedValue();
        if (value is null) return;

        if (value.IsDefault)
        {
            Dialogs.Error("Cannot rename", "The (Default) value cannot be renamed.");
            return;
        }

        var newName = Dialogs.Rename("value", value.Name, _current);
        if (newName is null) return;

        if (!TryWrite(() => _registry.RenameValue(_current, value.Name, newName), "Cannot rename value")) return;

        LoadValues(null);
        SelectValueNamed(newName);
    }

    private void StartSearch()
    {
        var query = SearchDialogs.AskForQuery(_lastQuery);
        if (query is null) return;

        _lastQuery = query;
        _activeSearch = new RegistrySearch(_registry, query, _current);
        RunSearch(firstAttempt: true);
    }

    /// <summary>F3. Continues the live search, or opens the form if there is not one.</summary>
    private void FindNextMatch()
    {
        if (_activeSearch is null)
        {
            StartSearch();
            return;
        }
        RunSearch(firstAttempt: false);
    }

    private void RunSearch(bool firstAttempt)
    {
        var result = SearchDialogs.Run(_activeSearch!);

        switch (result.Outcome)
        {
            case SearchOutcome.Found:
                RevealHit(result.Hit!);
                break;

            case SearchOutcome.NotFound:
                _activeSearch = null;
                Dialogs.Error("Find",
                    firstAttempt
                        ? $"'{_lastQuery!.Text}' was not found."
                        : $"No more matches for '{_lastQuery!.Text}'.");
                break;

            case SearchOutcome.Failed:
                _activeSearch = null;
                Dialogs.Error("Find", result.Error ?? "The search could not be completed.");
                break;

            case SearchOutcome.Cancelled:
                break;
        }
    }

    private void RevealHit(SearchHit hit)
    {
        var index = _tree.Reveal(hit.Path);
        if (index < 0)
        {
            Dialogs.Error("Find", $"Matched in {hit.Path}, but that key is no longer in the tree.");
            return;
        }

        SyncTree(index);                       // also reloads the values pane for the hit

        if (hit.ValueName is null)
        {
            _keysList.SetFocus();
            return;
        }

        if (!SelectValueNamed(hit.ValueName)) _keysList.SetFocus();
    }

    private bool SelectValueNamed(string name)
    {
        for (var i = 0; i < _valueSource.Count; i++)
        {
            if (!string.Equals(_valueSource.At(i)?.Name, name, StringComparison.OrdinalIgnoreCase)) continue;

            _valuesList.SelectedItem = i;
            _valuesList.SetFocus();
            RefreshFrameTitles();
            return true;
        }
        return false;
    }

    /// <summary>Runs a registry write, reporting any failure as a dialog. True if it succeeded.</summary>
    private static bool TryWrite(Action write, string title)
    {
        try
        {
            write();
            return true;
        }
        catch (Exception ex)
        {
            Dialogs.Error(title, RegistryErrors.Describe(ex));
            return false;
        }
    }

    private bool HandleTypeAhead(Key key)
    {
        var code = (int)key;
        if ((key & (Key.CtrlMask | Key.AltMask)) != 0) return false;
        if (code is < 32 or > 126) return false;

        var now = DateTime.UtcNow;
        if ((now - _typeAheadLast).TotalMilliseconds > _typeAheadTimeoutMs)
            _typeAheadBuffer = string.Empty;
        _typeAheadLast = now;
        _typeAheadBuffer += (char)code;

        var startAfter = _typeAheadBuffer.Length > 1 ? _keysList.SelectedItem - 1 : _keysList.SelectedItem;
        var found = _tree.FindByPrefix(_typeAheadBuffer, startAfter);
        if (found >= 0) SyncTree(found);

        return true;
    }
}
