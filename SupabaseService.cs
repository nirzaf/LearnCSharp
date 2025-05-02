using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Supabase;
using Supabase.Gotrue;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System.Linq;
using Microsoft.Extensions.Configuration; // For config

namespace LearnCSharp
{
    [Table("notes")]
    public class NoteModel : BaseModel
    {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }
        [Column("title")]
        public string Title { get; set; }
        [Column("content")]
        public string Content { get; set; }
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }

    public class SupabaseService
    {
        private readonly Supabase.Client _client;
        public SupabaseService(string url, string anonKey)
        {
            var options = new Supabase.SupabaseOptions
            {
                AutoConnectRealtime = false
            };
            _client = new Supabase.Client(url, anonKey, options);
        }

        public async Task InitializeAsync()
        {
            await _client.InitializeAsync();
        }

        public async Task<List<NoteModel>> GetNotesAsync()
        {
            var notes = await _client.From<NoteModel>().Get();
            return notes.Models.OrderByDescending(n => n.CreatedAt).ToList();
        }

        public async Task AddNoteAsync(string title, string content)
        {
            var now = DateTime.UtcNow;
            var note = new NoteModel
            {
                Title = title,
                Content = content,
                CreatedAt = now,
                UpdatedAt = now
            };
            await _client.From<NoteModel>().Insert(note);
        }

        public async Task UpdateNoteAsync(Guid id, string title, string content)
        {
            var now = DateTime.UtcNow;
            var update = new NoteModel
            {
                Id = id,
                Title = title,
                Content = content,
                UpdatedAt = now
            };
            await _client.From<NoteModel>().Where(x => x.Id == id).Update(update);
        }

        public async Task DeleteNoteAsync(Guid id)
        {
            await _client.From<NoteModel>().Where(x => x.Id == id).Delete();
        }
    }
}
