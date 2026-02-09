using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace JustLauncher.Services;

public class ModManagerService
{
    public async Task<List<ModInfo>> GetModsAsync(string gameDirectory)
    {
        var modsDir = Path.Combine(gameDirectory, "mods");
        if (!Directory.Exists(modsDir)) Directory.CreateDirectory(modsDir);

        var mods = new List<ModInfo>();

        foreach (var file in Directory.GetFiles(modsDir))
        {
            var fileName = Path.GetFileName(file);
            var isEnabled = file.EndsWith(".jar", StringComparison.OrdinalIgnoreCase);
            var isDisabled = file.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase);

            if (!isEnabled && !isDisabled) continue;

            var mod = new ModInfo
            {
                FileName = fileName,
                Path = file,
                Name = fileName, // Default
                IsEnabled = isEnabled
            };

            try
            {
                if (isEnabled)
                {
                    using var archive = ZipFile.OpenRead(file);
                    var fabricJson = archive.GetEntry("fabric.mod.json");
                    if (fabricJson != null)
                    {
                        using var stream = fabricJson.Open();
                        var meta = await JsonSerializer.DeserializeAsync<FabricModMetadata>(stream);
                        if (meta != null)
                        {
                            mod.Name = meta.Name ?? fileName;
                            mod.Version = meta.Version ?? "";
                            mod.Description = meta.Description ?? "";
                            if (meta.Authors != null) mod.Authors = string.Join(", ", meta.Authors);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading mod metadata for {fileName}: {ex.Message}");
            }

            mods.Add(mod);
        }

        return mods;
    }

    public void ToggleMod(ModInfo mod)
    {
        if (mod.IsEnabled)
        {
            var newPath = mod.Path + ".disabled";
            if (File.Exists(newPath)) File.Delete(newPath);
            File.Move(mod.Path, newPath);
            mod.Path = newPath;
            mod.FileName = Path.GetFileName(newPath);
            mod.IsEnabled = false;
        }
        else
        {
            if (mod.Path.EndsWith(".disabled"))
            {
                var newPath = mod.Path.Substring(0, mod.Path.Length - ".disabled".Length);
                if (File.Exists(newPath)) File.Delete(newPath);
                File.Move(mod.Path, newPath);
                mod.Path = newPath;
                mod.FileName = Path.GetFileName(newPath);
                mod.IsEnabled = true;
            }
        }
    }
}

public class FabricModMetadata
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [JsonPropertyName("version")]
    public string? Version { get; set; }
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    [JsonPropertyName("authors")]
    public List<string>? Authors { get; set; }
}
