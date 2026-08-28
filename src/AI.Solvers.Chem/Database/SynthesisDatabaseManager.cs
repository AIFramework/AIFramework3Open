using AI.Solvers.Chem.Models;
using System.Text.Json;

namespace AI.Solvers.Chem.Database;

/// <summary>
/// Управление базой данных ретросинтетических маршрутов
/// </summary>
public class SynthesisDatabaseManager
{
    private SynthesisDatabase _database;
    private readonly string _databasePath;

    public SynthesisDatabaseManager(string? customPath = null)
    {
        _databasePath = customPath ?? Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, 
            "Data", 
            "synthesis_database.json");
        
        _database = new SynthesisDatabase();
        LoadDatabase();
    }

    /// <summary>
    /// Загрузка базы данных из JSON файла
    /// </summary>
    public void LoadDatabase()
    {
        try
        {
            if (File.Exists(_databasePath))
            {
                var json = File.ReadAllText(_databasePath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    WriteIndented = true
                };
                _database = JsonSerializer.Deserialize<SynthesisDatabase>(json, options) 
                    ?? new SynthesisDatabase();
            }
            else
            {
                // Создаём пустую базу данных
                _database = new SynthesisDatabase();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not load synthesis database: {ex.Message}");
            _database = new SynthesisDatabase();
        }
    }

    /// <summary>
    /// Сохранение базы данных в JSON файл
    /// </summary>
    public void SaveDatabase()
    {
        try
        {
            var directory = Path.GetDirectoryName(_databasePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            };
            
            var json = JsonSerializer.Serialize(_database, options);
            File.WriteAllText(_databasePath, json);
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to save synthesis database: {ex.Message}");
        }
    }

    /// <summary>
    /// Поиск соединения по имени или алиасу
    /// </summary>
    public TargetCompound? FindCompound(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var searchTerm = name.Trim().ToLower();

        return _database.Compounds.FirstOrDefault(c =>
            c.Name.Equals(searchTerm, StringComparison.OrdinalIgnoreCase) ||
            c.Aliases.Any(a => a.Equals(searchTerm, StringComparison.OrdinalIgnoreCase)) ||
            c.Formula.Equals(searchTerm, StringComparison.OrdinalIgnoreCase) ||
            c.IUPAC.Equals(searchTerm, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Поиск маршрута синтеза от конкретного исходного вещества
    /// </summary>
    public SynthesisRoute? FindRoute(string target, string? startingMaterial = null)
    {
        var compound = FindCompound(target);
        if (compound == null)
            return null;

        if (string.IsNullOrWhiteSpace(startingMaterial))
            return compound.Routes.FirstOrDefault();

        var searchTerm = startingMaterial.Trim().ToLower();
        return compound.Routes.FirstOrDefault(r =>
            r.StartingMaterial.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Получить все маршруты для соединения
    /// </summary>
    public List<SynthesisRoute> GetAllRoutes(string target)
    {
        var compound = FindCompound(target);
        return compound?.Routes ?? new List<SynthesisRoute>();
    }

    /// <summary>
    /// Добавить новое соединение в базу данных
    /// </summary>
    public void AddCompound(TargetCompound compound)
    {
        // Проверяем, не существует ли уже
        var existing = FindCompound(compound.Name);
        if (existing != null)
        {
            throw new Exception($"Compound '{compound.Name}' already exists in database");
        }

        _database.Compounds.Add(compound);
        _database.LastUpdated = DateTime.Now.ToString("yyyy-MM-dd");
    }

    /// <summary>
    /// Обновить существующее соединение
    /// </summary>
    public void UpdateCompound(TargetCompound compound)
    {
        var existing = FindCompound(compound.Name);
        if (existing == null)
        {
            throw new Exception($"Compound '{compound.Name}' not found in database");
        }

        _database.Compounds.Remove(existing);
        _database.Compounds.Add(compound);
        _database.LastUpdated = DateTime.Now.ToString("yyyy-MM-dd");
    }

    /// <summary>
    /// Получить статистику базы данных
    /// </summary>
    public string GetStatistics()
    {
        var totalCompounds = _database.Compounds.Count;
        var totalRoutes = _database.Compounds.Sum(c => c.Routes.Count);
        var avgSteps = _database.Compounds
            .SelectMany(c => c.Routes)
            .Average(r => r.StepCount);

        return $"Database Statistics:\n" +
               $"  Total Compounds: {totalCompounds}\n" +
               $"  Total Routes: {totalRoutes}\n" +
               $"  Average Steps per Route: {avgSteps:F1}\n" +
               $"  Last Updated: {_database.LastUpdated}\n" +
               $"  Version: {_database.Version}";
    }

    /// <summary>
    /// Получить список всех доступных соединений
    /// </summary>
    public List<string> GetAvailableCompounds()
    {
        return _database.Compounds
            .Select(c => c.Name)
            .OrderBy(n => n)
            .ToList();
    }
}

