using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChequeWriter.Core;

/// <summary>
/// Saves everything (banks, payees, register, settings) in one JSON file.
/// Writes go to a temp file first and the previous version is kept as .bak,
/// so a crash or power cut cannot leave a half-written file.
/// </summary>
public class DataStore
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public string FilePath { get; }
    public AppData Data { get; private set; }

    public DataStore(string filePath)
    {
        FilePath = filePath;
        Data = Load(filePath);
    }

    public const string DataFileName = "rakspecial-data.json";

    /// <summary>
    /// Data lives next to the program when a file named "portable.txt" is there (handy on a USB stick),
    /// otherwise in %APPDATA%\RAK Special.
    /// </summary>
    public static string DefaultPath(string exeFolder)
    {
        var folder = File.Exists(Path.Combine(exeFolder, "portable.txt"))
            ? Path.Combine(exeFolder, "Data")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RAK Special");
        return Path.Combine(folder, DataFileName);
    }

    /// <summary>Where version 1 of this program (then called "Cheque Writer") kept its data.</summary>
    public static string OldVersionPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ChequeWriter", "chequewriter-data.json");

    /// <summary>
    /// First start of RAK Special: copies the data of the earlier version (if any) so payees,
    /// register and layout carry over. The old file is left untouched. Returns true when copied.
    /// </summary>
    public static bool MigrateFrom(string oldPath, string newPath)
    {
        if (File.Exists(newPath) || !File.Exists(oldPath)) return false;
        Directory.CreateDirectory(Path.GetDirectoryName(newPath)!);
        File.Copy(oldPath, newPath);
        return true;
    }

    public static AppData Load(string path)
    {
        if (!File.Exists(path)) return DefaultLayouts.NewAppData();
        var json = File.ReadAllText(path, Encoding.UTF8);
        var data = JsonSerializer.Deserialize<AppData>(json, JsonOptions)
                   ?? throw new InvalidDataException("The data file is empty or damaged.");
        DefaultLayouts.EnsureBuiltIns(data);
        return data;
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(Data, JsonOptions), Encoding.UTF8);
        if (File.Exists(FilePath))
            File.Replace(tmp, FilePath, FilePath + ".bak");
        else
            File.Move(tmp, FilePath);
    }

    /// <summary>Replaces all data with the contents of a backup file (the current data is saved as .bak first).</summary>
    public void RestoreFrom(string backupFile)
    {
        var restored = Load(backupFile);
        Data = restored;
        Save();
    }

    public void BackupTo(string backupFile)
    {
        Save();
        File.Copy(FilePath, backupFile, overwrite: true);
    }

    /// <summary>Adds the payee to the saved list if it is new (case-insensitive).</summary>
    public void RememberPayee(string name)
    {
        name = name.Trim();
        if (name.Length == 0) return;
        if (!Data.Payees.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
            Data.Payees.Add(new Payee { Name = name });
    }

    /// <summary>"000123" -> "000124", keeping leading zeros. Non-numeric numbers are returned unchanged.</summary>
    public static string NextNumber(string current)
    {
        current = (current ?? "").Trim();
        if (current.Length == 0 || !current.All(char.IsDigit)) return current;
        var next = (decimal.Parse(current) + 1).ToString(new string('0', current.Length));
        return next;
    }
}
