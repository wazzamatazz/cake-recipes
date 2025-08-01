// Build profile definitions for the Cake recipe system.

// Represents a build profile with its associated tasks and configuration.
public class BuildProfile
{
    public string Name { get; init; }
    public string Description { get; init; }
    public List<string> Tasks { get; init; }
    public Dictionary<string, object> DefaultOptions { get; init; }
    public List<string> SupportedOptions { get; init; }
    
    public BuildProfile()
    {
        Tasks = new List<string>();
        DefaultOptions = new Dictionary<string, object>();
        SupportedOptions = new List<string>();
    }
}

// Registry of all available build profiles.
public static class BuildProfiles
{
    public static Dictionary<string, BuildProfile> GetDefaultProfiles()
    {
        return new Dictionary<string, BuildProfile>(StringComparer.OrdinalIgnoreCase)
        {
            ["test"] = new BuildProfile
            {
                Name = "test",
                Description = "Runs a standard development build with tests",
                Tasks = new List<string> { "Restore", "Build", "Test" },
                DefaultOptions = new Dictionary<string, object>
                {
                    ["configuration"] = "Debug",
                    ["clean"] = false
                },
                SupportedOptions = new List<string> { "configuration", "clean", "no-tests" }
            },
            
            ["dev"] = new BuildProfile
            {
                Name = "dev",
                Description = "Fast development build without tests",
                Tasks = new List<string> { "Restore", "Build" },
                DefaultOptions = new Dictionary<string, object>
                {
                    ["configuration"] = "Debug",
                    ["clean"] = false,
                    ["no-tests"] = true
                },
                SupportedOptions = new List<string> { "configuration", "clean" }
            },
            
            ["pack"] = new BuildProfile
            {
                Name = "pack",
                Description = "Creates NuGet packages after building and testing",
                Tasks = new List<string> { "Restore", "Build", "Test", "Pack" },
                DefaultOptions = new Dictionary<string, object>
                {
                    ["configuration"] = "Release",
                    ["clean"] = true
                },
                SupportedOptions = new List<string> { "configuration", "clean", "no-tests" }
            },
            
            ["containers"] = new BuildProfile
            {
                Name = "containers",
                Description = "Builds and publishes container images",
                Tasks = new List<string> { "Restore", "Build", "Test", "PublishContainer" },
                DefaultOptions = new Dictionary<string, object>
                {
                    ["configuration"] = "Release",
                    ["clean"] = true
                },
                SupportedOptions = new List<string> { "configuration", "clean", "no-tests", "container-registry" }
            },
            
            ["release"] = new BuildProfile
            {
                Name = "release",
                Description = "Complete release build with configurable components",
                Tasks = new List<string> { "Clean", "Restore", "Build", "Test", "Pack", "PublishContainer", "BillOfMaterials" },
                DefaultOptions = new Dictionary<string, object>
                {
                    ["configuration"] = "Release",
                    ["clean"] = true,
                    ["packages"] = true,
                    ["containers"] = true,
                    ["sbom"] = true,
                    ["ci"] = false,
                    ["sign-output"] = false
                },
                SupportedOptions = new List<string> 
                { 
                    "configuration", "clean", "no-tests", "packages", "containers", "sbom", 
                    "ci", "sign-output", "container-registry", "build-counter", "build-metadata",
                    "github-username", "github-token"
                }
            }
        };
    }
    
    public static BuildProfile GetProfile(string profileName, Dictionary<string, BuildProfile> customProfiles = null)
    {
        var allProfiles = GetDefaultProfiles();
        
        // Add custom profiles if provided
        if (customProfiles != null)
        {
            foreach (var customProfile in customProfiles)
            {
                allProfiles[customProfile.Key] = customProfile.Value;
            }
        }
        
        if (allProfiles.TryGetValue(profileName ?? "", out var profile))
        {
            return profile;
        }
        
        throw new InvalidOperationException($"Unknown build profile: {profileName}. Available profiles: {string.Join(", ", allProfiles.Keys)}");
    }
    
    public static List<string> GetAvailableProfiles(Dictionary<string, BuildProfile> customProfiles = null)
    {
        var allProfiles = GetDefaultProfiles();
        
        if (customProfiles != null)
        {
            foreach (var customProfile in customProfiles)
            {
                allProfiles[customProfile.Key] = customProfile.Value;
            }
        }
        
        return allProfiles.Keys.ToList();
    }
}