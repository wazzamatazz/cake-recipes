// Class for sharing build state between Cake tasks.
public class BuildState {

    // The solution to build.
    public string SolutionName { get; set; }

    // The build number.
    public string BuildNumber { get; set; }

    // The Cake target.
    public string Target { get; set; }

    // The MSBuild configuration.
    public string Configuration { get; set; }

    // Specifies if a clean should be performed prior to running the specified target.
    public bool Clean { get; set; }

    // Specifies if the Clean target should be run.
    public bool RunCleanTarget => Clean || string.Equals(Target, "Clean", StringComparison.OrdinalIgnoreCase);

    // Specifies if tests should be skipped.
    public bool SkipTests { get; set; }

    // Specifies if this is a continuous integration build.
    public bool ContinuousIntegrationBuild { get; set; }

    // Specifies if DLLs and NuGet packages should be signed.
    public bool SignOutput { get; set; }

    // Specifies if output signing is allowed.
    public bool CanSignOutput => SignOutput && ContinuousIntegrationBuild;

    // Major version number.
    public int MajorVersion { get; set; }
    
    // Minor version number.
    public int MinorVersion { get; set; }
    
    // Patch version number.
    public int PatchVersion { get; set; }
    
    // MSBuild AssemblyVersion property value.
    public string AssemblyVersion { get; set; }

    // MSBuild AssemblyFileVersion property value.
    public string AssemblyFileVersion { get; set; }

    // MSBuild InformationalVersion property value.
    public string InformationalVersion { get; set; }

    // MSBuild Version property value.
    public string PackageVersion { get; set; }

    // Projects to build container images for when the PublishContainer target is run.
    public IEnumerable<string> PublishContainerProjects { get; set; }

    // Additional MSBuild properties.
    public ICollection<string> MSBuildProperties { get; set; }

}
