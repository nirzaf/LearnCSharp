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

    public MainWindow()
    {
        InitializeComponent();
        InitializeSupabaseAndLoadNotes();
        var searchBtn = this.FindControl<Button>("SearchButton");
        if (searchBtn != null) searchBtn.Click += OnSearchClicked;
        var newNoteBtn = this.FindControl<Button>("NewNoteButton");
        if (newNoteBtn != null) newNoteBtn.Click += OnNewNoteClicked;
        var saveNoteBtn = this.FindControl<Button>("SaveNoteButton");
        if (saveNoteBtn != null) saveNoteBtn.Click += OnSaveNoteClicked;
        var notesList = this.FindControl<ListBox>("NotesList");
        if (notesList != null) notesList.SelectionChanged += OnNoteSelected;
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
        catch
        {
            _supabaseReady = false;
            LoadNotes(); // fallback to local
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
        foreach (var file in Directory.GetFiles(NotesDirectory, "*.json"))
        {
            try
            {
                var note = JsonSerializer.Deserialize<Note>(File.ReadAllText(file));
                if (note != null)
                    Notes.Add(note);
            }
            catch { }
        }
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
                    CreatedAt = note.CreatedAt
                });
            }
            var notesList = this.FindControl<ListBox>("NotesList");
            if (notesList != null) notesList.ItemsSource = Notes.Select(n => n.Title).ToList();
        }
    }

    private void OnSearchClicked(object? sender, RoutedEventArgs e)
    {
        var query = this.FindControl<TextBox>("SearchBox").Text ?? string.Empty;
        var filtered = Notes.Where(n => n.Title.Contains(query, StringComparison.OrdinalIgnoreCase) || n.Content.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        var notesList = this.FindControl<ListBox>("NotesList");
        if (notesList != null) notesList.ItemsSource = filtered.Select(n => n.Title).ToList();
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
        var noteContentBox = this.FindControl<TextBox>("NoteContentBox");
        var content = noteContentBox != null ? noteContentBox.Text ?? string.Empty : string.Empty;
        var title = content.Split('\n').FirstOrDefault()?.Trim();
        if (string.IsNullOrWhiteSpace(title))
            title = "Untitled Note";
        var note = new Note { Title = title, Content = content, CreatedAt = DateTime.UtcNow };
        if (_supabaseService != null && _supabaseReady)
        {
            await _supabaseService.AddNoteAsync(title, content);
            await LoadNotesAsync();
        }
        else
        {
            var path = Path.Combine(NotesDirectory, title + ".json");
            File.WriteAllText(path, JsonSerializer.Serialize(note));
            LoadNotes();
        }
    }

    private void OnNoteSelected(object? sender, SelectionChangedEventArgs e)
    {
        var listBox = this.FindControl<ListBox>("NotesList");
        var selectedTitle = listBox?.SelectedItem as string;
        var note = Notes.FirstOrDefault(n => n.Title == selectedTitle);
        var noteContentBox = this.FindControl<TextBox>("NoteContentBox");
        if (noteContentBox != null) noteContentBox.Text = note?.Content ?? string.Empty;
    }
}