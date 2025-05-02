using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System;
using Microsoft.Extensions.Configuration; // For config


namespace LearnCSharp;


public partial class MainWindow : Window
{
    private ObservableCollection<Note> Notes = new();
    private string NotesDirectory = "notes";
    private SupabaseService _supabaseService;
    private bool _supabaseReady = false;

    private Note? _selectedNote = null;
    private string _lastSavedTitle = string.Empty;
    private string _lastSavedContent = string.Empty;
    private DateTime _lastSavedUpdatedAt = DateTime.MinValue;
    private System.Timers.Timer? _autosaveTimer;
    private bool _isSaving = false;


    public MainWindow()
    {
        InitializeComponent();
        // Set default theme
        Classes.Add("Light");
        
        // Load Supabase config
        var config = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
        var supabaseSection = config.GetSection("Supabase");
        var url = supabaseSection["Url"];
        var anonKey = supabaseSection["AnonKey"];
        // Realtime is now handled in SupabaseService

        // Defer notes loading until window is shown
        this.Opened += async (_, __) => { InitializeSupabaseAndLoadNotes(); };

        var searchBox = this.FindControl<TextBox>("SearchBox");
        if (searchBox != null)
            searchBox.GetObservable(TextBox.TextProperty).Subscribe(_ => FilterNotesBySearch());
        var newNoteBtn = this.FindControl<Button>("NewNoteButton");
        if (newNoteBtn != null) newNoteBtn.Click += OnNewNoteClicked;
        var saveNoteBtn = this.FindControl<Button>("SaveNoteButton");
        if (saveNoteBtn != null) saveNoteBtn.Click += OnSaveNoteClicked;
        var updateNoteBtn = this.FindControl<Button>("UpdateNoteButton");
        if (updateNoteBtn != null) updateNoteBtn.Click += OnUpdateNoteClicked;
        var themeToggleBtn = this.FindControl<Button>("ThemeToggleButton");
        if (themeToggleBtn != null) themeToggleBtn.Click += OnThemeToggleClicked;
        var deleteNoteBtn = this.FindControl<Button>("DeleteNoteButton");
        if (deleteNoteBtn != null) deleteNoteBtn.Click += OnDeleteNoteClicked;
        var notesList = this.FindControl<ListBox>("NotesList");
        if (notesList != null) notesList.SelectionChanged += OnNoteSelected;
        var noteTitleBox = this.FindControl<TextBox>("NoteTitleBox");
        var noteContentBox = this.FindControl<TextBox>("NoteContentBox");
        if (noteTitleBox != null) noteTitleBox.GetObservable(TextBox.TextProperty).Subscribe(_ => OnNoteEditorChanged());
        if (noteContentBox != null) noteContentBox.GetObservable(TextBox.TextProperty).Subscribe(_ => OnNoteEditorChanged());
        AutosaveStatus = this.FindControl<TextBlock>("AutosaveStatus");
    }



    private async void InitializeSupabaseAndLoadNotes()
    {
        try
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .Build();
            var url = config["Supabase:Url"];
            var anonKey = config["Supabase:AnonKey"];
            _supabaseService = new SupabaseService(url, anonKey);
            await _supabaseService.InitializeAsync();
            _supabaseReady = true;
            await LoadNotesAsync();
        }
        catch (Exception ex)
        {
            _supabaseReady = false;
            LoadNotes(); // fallback to local
            await ShowMessageBox($"Failed to load notes from Supabase: {ex.Message}");
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void LoadNotes()
    {
        Notes.Clear();
        if (!Directory.Exists(NotesDirectory))
            Directory.CreateDirectory(NotesDirectory);
        var loadedNotes = new List<Note>();
        foreach (var file in Directory.GetFiles(NotesDirectory, "*.json"))
        {
            try
            {
                var note = JsonSerializer.Deserialize<Note>(File.ReadAllText(file));
                if (note != null)
                    loadedNotes.Add(note);
            }
            catch { }
        }
        // Sort by UpdatedAt descending
        foreach (var note in loadedNotes.OrderByDescending(n => n.UpdatedAt))
            Notes.Add(note);
        var notesList = this.FindControl<ListBox>("NotesList");
        if (notesList != null) notesList.ItemsSource = Notes.Select(n => n.Title).ToList();
    }

    private async Task LoadNotesAsync()
    {
        Notes.Clear();
        if (_supabaseService != null && _supabaseReady)
        {
            var supabaseNotes = await _supabaseService.GetNotesAsync();
            foreach (var note in supabaseNotes)
            {
                Notes.Add(new Note
                {
                    Id = note.Id,
                    Title = note.Title,
                    Content = note.Content,
                    CreatedAt = note.CreatedAt,
                    UpdatedAt = note.UpdatedAt
                });
            }
            var notesList = this.FindControl<ListBox>("NotesList");
            if (notesList != null) notesList.ItemsSource = Notes;
        }
    }

    private void FilterNotesBySearch()
    {
        var searchBox = this.FindControl<TextBox>("SearchBox");
        var query = searchBox?.Text ?? string.Empty;
        var notesList = this.FindControl<ListBox>("NotesList");
        if (string.IsNullOrWhiteSpace(query))
        {
            if (notesList != null) notesList.ItemsSource = Notes;
            return;
        }
        var filtered = Notes.Where(n =>
            (!string.IsNullOrEmpty(n.Title) && n.Title.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrEmpty(n.Content) && n.Content.Contains(query, StringComparison.OrdinalIgnoreCase))
        ).ToList();
        if (notesList != null) notesList.ItemsSource = filtered;
    }

    private void OnNewNoteClicked(object? sender, RoutedEventArgs e)
    {
        var noteContentBox = this.FindControl<TextBox>("NoteContentBox");
        if (noteContentBox != null) noteContentBox.Text = string.Empty;
        var notesList = this.FindControl<ListBox>("NotesList");
        if (notesList != null) notesList.SelectedIndex = -1;
    }

    private async void OnSaveNoteClicked(object? sender, RoutedEventArgs e)
    {
        var noteTitleBox = this.FindControl<TextBox>("NoteTitleBox");
        var noteContentBox = this.FindControl<TextBox>("NoteContentBox");
        var title = noteTitleBox?.Text ?? string.Empty;
        var content = noteContentBox?.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(content))
        {
            await ShowMessageBox("Cannot save empty note.");
            return;
        }
        if (_supabaseService != null && _supabaseReady)
        {
            await _supabaseService.AddNoteAsync(title, content);
            await LoadNotesAsync();
        }
        else
        {
            var note = new Note { Title = title, Content = content, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            var path = Path.Combine(NotesDirectory, title + ".json");
            File.WriteAllText(path, JsonSerializer.Serialize(note));
            LoadNotes();
        }
        SetAutosaveStatus("Saved");
    }

    private void OnNoteSelected(object? sender, SelectionChangedEventArgs e)
    {
        var listBox = this.FindControl<ListBox>("NotesList");
        var note = listBox?.SelectedItem as Note;
        _selectedNote = note;
        var noteTitleBox = this.FindControl<TextBox>("NoteTitleBox");
        var noteContentBox = this.FindControl<TextBox>("NoteContentBox");
        if (noteTitleBox != null) noteTitleBox.Text = note?.Title ?? string.Empty;
        if (noteContentBox != null) noteContentBox.Text = note?.Content ?? string.Empty;
        _lastSavedTitle = note?.Title ?? string.Empty;
        _lastSavedContent = note?.Content ?? string.Empty;
        _lastSavedUpdatedAt = note?.UpdatedAt ?? DateTime.MinValue;
        SetAutosaveStatus("Loaded");
    }

    private async void OnDeleteNoteClicked(object? sender, RoutedEventArgs e)
    {
        var listBox = this.FindControl<ListBox>("NotesList");
        var note = listBox?.SelectedItem as Note;
        if (note == null)
        {
            await ShowMessageBox("Please select a note to delete.");
            return;
        }
        if (_supabaseService != null && _supabaseReady)
        {
            await _supabaseService.DeleteNoteAsync(note.Id);
            await LoadNotesAsync();
        }
        else
        {
            var path = Path.Combine(NotesDirectory, note.Title + ".json");
            if (File.Exists(path))
                File.Delete(path);
            LoadNotes();
        }
        var noteTitleBox = this.FindControl<TextBox>("NoteTitleBox");
        var noteContentBox = this.FindControl<TextBox>("NoteContentBox");
        if (noteTitleBox != null) noteTitleBox.Text = string.Empty;
        if (noteContentBox != null) noteContentBox.Text = string.Empty;
        if (listBox != null) listBox.SelectedIndex = -1;
        SetAutosaveStatus("");
    }

    private async Task ShowMessageBox(string message)
    {
        var dialog = new Window
        {
            Title = "Info",
            Width = 300,
            Height = 150,
            Content = new StackPanel
            {
                Margin = new Thickness(10),
                Children =
                {
                    new TextBlock { Text = message, Margin = new Thickness(0,20,0,20), TextAlignment = Avalonia.Media.TextAlignment.Center },
                    new Button { Content = "OK", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, Width = 60 }
                }
            }
        };
        var okButton = ((dialog.Content as StackPanel)?.Children[1]) as Button;
        if (okButton != null)
        {
            okButton.Click += (_, __) => dialog.Close();
        }
        await dialog.ShowDialog((Window)this);
    }

    // --- Yandex Notes-style update/autosave logic ---
    private void OnNoteEditorChanged()
    {
        if (_autosaveTimer != null)
        {
            _autosaveTimer.Stop();
            _autosaveTimer.Dispose();
        }
        _autosaveTimer = new System.Timers.Timer(2000); // 2 seconds debounce
        _autosaveTimer.Elapsed += async (s, e) => await AutosaveNoteAsync();
        _autosaveTimer.AutoReset = false;
        _autosaveTimer.Start();
        SetAutosaveStatus("Saving...");
    }

    private async Task AutosaveNoteAsync()
    {
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
        {
            if (_isSaving) return;
            _isSaving = true;
            var noteTitleBox = this.FindControl<TextBox>("NoteTitleBox");
            var noteContentBox = this.FindControl<TextBox>("NoteContentBox");
            var title = noteTitleBox?.Text ?? string.Empty;
            var content = noteContentBox?.Text ?? string.Empty;
            if (_selectedNote == null || (title == _lastSavedTitle && content == _lastSavedContent))
            {
                SetAutosaveStatus("Up to date");
                _isSaving = false;
                return;
            }
            if (string.IsNullOrWhiteSpace(content))
            {
                SetAutosaveStatus("Cannot autosave empty note");
                _isSaving = false;
                return;
            }
            try
            {
                if (_supabaseService != null && _supabaseReady)
                {
                    await _supabaseService.UpdateNoteAsync(_selectedNote.Id, title, content);
                    await LoadNotesAsync();
                }
                else
                {
                    var note = new Note { Id = _selectedNote.Id, Title = title, Content = content, CreatedAt = _selectedNote.CreatedAt, UpdatedAt = DateTime.UtcNow };
                    var path = Path.Combine(NotesDirectory, title + ".json");
                    File.WriteAllText(path, JsonSerializer.Serialize(note));
                    LoadNotes();
                }
                _lastSavedTitle = title;
                _lastSavedContent = content;
                SetAutosaveStatus("Saved");
            }
            catch
            {
                SetAutosaveStatus("Autosave failed");
            }
            _isSaving = false;
        });
    }

    private async void OnUpdateNoteClicked(object? sender, RoutedEventArgs e)
    {
        await AutosaveNoteAsync();
    }

    private void SetAutosaveStatus(string status)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() => {
            var autosaveStatus = this.FindControl<TextBlock>("AutosaveStatus");
            if (autosaveStatus != null)
            {
                autosaveStatus.Text = status;
            }
        });
    }

    private void OnThemeToggleClicked(object? sender, RoutedEventArgs e)
    {
        // Toggle between Light and Dark classes
        if (Classes.Contains("Dark"))
        {
            Classes.Remove("Dark");
            Classes.Add("Light");
        }
        else
        {
            Classes.Remove("Light");
            Classes.Add("Dark");
        }
    }
}