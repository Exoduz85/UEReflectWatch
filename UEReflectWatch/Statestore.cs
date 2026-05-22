using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UEReflectWatch
{
    public sealed class FileMacroState
    {
        [JsonPropertyName("filePath")]
        public string FilePath { get; set; } = string.Empty;

        [JsonPropertyName("lastScanned")]
        public string LastScanned { get; set; } = string.Empty;

        [JsonPropertyName("macros")]
        public List<SerializableMacro> Macros { get; set; } = new();
    }

    public sealed class SerializableMacro
    {
        [JsonPropertyName("kind")]
        public string Kind { get; set; } = string.Empty;

        [JsonPropertyName("line")]
        public int Line { get; set; }

        [JsonPropertyName("raw")]
        public string Raw { get; set; } = string.Empty;
    }

    public sealed class StateStore
    {
        private readonly string _storePath;
        private Dictionary<string, FileMacroState> _state = new(StringComparer.OrdinalIgnoreCase);

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        public StateStore() : this(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "UEReflectWatch"))
        {
        }

        public StateStore(string globalStoragePath)
        {
            if (!Directory.Exists(globalStoragePath))
                Directory.CreateDirectory(globalStoragePath);

            _storePath = Path.Combine(globalStoragePath, "macro-state.json");
            Load();
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(_storePath)) return;
                var raw = File.ReadAllText(_storePath);
                _state = JsonSerializer.Deserialize<Dictionary<string, FileMacroState>>(raw, JsonOptions)
                         ?? new Dictionary<string, FileMacroState>(StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                _state = new Dictionary<string, FileMacroState>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(_state, JsonOptions);
                File.WriteAllText(_storePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UEReflectWatch] Failed to save state: {ex.Message}");
            }
        }

        public List<MacroEntry> GetMacros(string filePath)
        {
            if (!_state.TryGetValue(filePath, out var entry)) return new List<MacroEntry>();

            var result = new List<MacroEntry>();
            foreach (var m in entry.Macros)
            {
                if (Enum.TryParse<MacroKind>(m.Kind, out var kind))
                    result.Add(new MacroEntry(kind, m.Line, m.Raw));
            }
            return result;
        }

        public void SetMacros(string filePath, List<MacroEntry> macros)
        {
            _state[filePath] = new FileMacroState
            {
                FilePath = filePath,
                LastScanned = DateTime.UtcNow.ToString("o"),
                Macros = macros.ConvertAll(m => new SerializableMacro
                {
                    Kind = m.Kind.ToString(),
                    Line = m.Line,
                    Raw = m.Raw
                })
            };
            Save();
        }

        public void RemoveFile(string filePath)
        {
            _state.Remove(filePath);
            Save();
        }

        public void ClearAll()
        {
            _state.Clear();
            Save();
        }

        public Dictionary<string, FileMacroState> GetFullState() =>
            new(_state, StringComparer.OrdinalIgnoreCase);

        public List<string> GetAllFiles() =>
            new List<string>(_state.Keys);
    }
}