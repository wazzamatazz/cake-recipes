using System.Text.Json;

using Spectre.Console;

#tool dotnet:?package=CycloneDX&version=5.0.1

#load "build-state.cake"
#load "task-definitions.cake"
#load "profile-definitions.cake"
#load "profile-execution.cake"

public TaskDefinitions Targets { get; private set; }
public Dictionary<string, BuildProfile> CustomProfiles { get; private set; }


// Bootstraps the build using the specified default solution file and JSON version file.
public void Bootstrap(string solutionFilePath, string versionFilePath, IEnumerable<string> containerProjects = null, Dictionary<string, BuildProfile> customProfiles = null) {
    CustomProfiles = customProfiles;
    ConfigureBuildState(solutionFilePath, versionFilePath, GetBranchName(), containerProjects);
    ConfigureTaskEventHandlers();
    ConfigureTasks();
}


private string GetBranchName() {
    // For Git repositories, we always use the name of the current branch, regardless of what was
    // specified as the branch argument.
    var currentDir = DirectoryPath.FromString(".");
    return GetGitBranchName(currentDir) ?? Argument("branch", "main");
}


private string GetGitBranchName(DirectoryPath dir) {
    var settings = new ProcessSettings() {
        RedirectStandardOutput = true,
        Arguments = "rev-parse --abbrev-ref HEAD"
    };

    using (var process = StartAndReturnProcess("git", settings)) {
        process.WaitForExit();
        return process.GetExitCode() == 0
            ? process.GetStandardOutput().FirstOrDefault()
            : null;
    }
}


// Gets the profile to run (first positional argument, or "test" as default).
public string GetProfile() {
    // Check if profile is specified via --profile argument first
    var profileArg = Argument("profile", "");
    if (!string.IsNullOrWhiteSpace(profileArg)) {
        return profileArg;
    }
    
    // Try to detect profile from command line arguments
    var args = System.Environment.GetCommandLineArgs();
    for (int i = 1; i < args.Length; i++) {
        var arg = args[i];
        // Skip known non-profile arguments
        if (arg.StartsWith("--") || arg.StartsWith("-") || arg.EndsWith(".cake") || arg.EndsWith(".dll") || arg == "cake") {
            continue;
        }
        
        // Check if this might be a profile name by seeing if it's a known profile
        var availableProfiles = BuildProfiles.GetAvailableProfiles(CustomProfiles);
        if (availableProfiles.Any(p => string.Equals(p, arg, StringComparison.OrdinalIgnoreCase))) {
            return arg;
        }
    }
    
    // Fallback to target-based approach for backward compatibility
    var target = Argument("target", "");
    if (!string.IsNullOrWhiteSpace(target)) {
        return MapTargetToProfile(target);
    }
    
    return "test"; // Default profile
}

// Maps legacy target names to profile names for backward compatibility.
private string MapTargetToProfile(string target) {
    return target.ToLowerInvariant() switch {
        "clean" => "test",
        "restore" => "test", 
        "build" => HasArgument("no-tests") ? "dev" : "test",
        "test" => "test",
        "pack" => "pack",
        "publish" => "pack",
        "publishcontainer" => "containers",
        "billofmaterials" => "ci",
        _ => "test"
    };
}

// Gets the target to run (for backward compatibility).
public string GetTarget() {
    return Argument("target", HasArgument("no-tests") ? "Build" : "Test");
}

// Runs the specified profile.
public void Run(string profileName = null) {
    var profile = profileName ?? GetProfile();
    
    // Create and run a default task that executes the profile
    Task("ProfileRunner")
        .Does<BuildState>(state => {
            try {
                var buildProfile = BuildProfiles.GetProfile(profile, CustomProfiles);
                
                WriteLogMessage(BuildSystem, $"Available profiles: {string.Join(", ", BuildProfiles.GetAvailableProfiles(CustomProfiles))}");
                
                var executor = new ProfileExecutor(Context, Targets);
                executor.ExecuteProfile(buildProfile, state);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Unknown build profile")) {
                WriteErrorMessage(BuildSystem, ex.Message);
                throw;
            }
        });
        
    RunTarget("ProfileRunner");
}

// Runs the specified target (for backward compatibility).
public void RunLegacyTarget(string target) {
    Information($"Legacy target mode: {target}. Consider using profile-based execution instead.");
    RunTarget(target);
}


// Normalises metadata for use in a SemVer v2.0.0 version.
private string NormaliseMetadata(string s) {
    var metadataNormaliser = new System.Text.RegularExpressions.Regex("[^0-9A-Za-z-]");
    return metadataNormaliser.Replace(s.Trim(), ".");
}


private JsonElement ParseJsonFromFile(string filePath) {
    var json = System.IO.File.ReadAllText(filePath);
    return JsonSerializer.Deserialize<JsonElement>(json);
}


private bool UseReleaseConfigurationForTarget(string target) {
    return string.Equals(target, "Pack", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(target, "Publish", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(target, "PublishContainer", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(target, "BillOfMaterials", StringComparison.OrdinalIgnoreCase);
}


// Configures the build state using the specified default solution file and JSON version file.
private void ConfigureBuildState(string solutionFilePath, string versionFilePath, string branchName, IEnumerable<string> containerProjects) {
    // Constructs the build state object.
    Setup<BuildState>(context => {
        try {
            WriteTaskStartMessage(BuildSystem, "Setup");
            
            var profileName = GetProfile();
            var profile = BuildProfiles.GetProfile(profileName, CustomProfiles);
            
            var state = new BuildState() {
                SolutionName = Argument("project", solutionFilePath),
                Profile = profileName,
                Target = context.TargetTask.Name,
                PublishContainerProjects = containerProjects
            };
            
            // Apply profile defaults first, then override with command line arguments
            ApplyProfileDefaults(state, profile);
            ApplyCommandLineOverrides(state, profile);
            
            // Store profile-specific options
            ParseProfileOptions(state, profile);

            // Get raw version numbers from JSON.

            var versionJson = ParseJsonFromFile(versionFilePath);

            var majorVersion = versionJson.TryGetProperty("Major", out var major)
                ? major.GetInt32()
                : 0;
            var minorVersion = versionJson.TryGetProperty("Minor", out var minor)
                ? minor.GetInt32()
                : 0;
            var patchVersion = versionJson.TryGetProperty("Patch", out var patch)
                ? patch.GetInt32()
                : 0;
            var versionSuffix = versionJson.TryGetProperty("PreRelease", out var preRelease)
                ? preRelease.GetString()
                : null;

            state.MajorVersion = majorVersion;
            state.MinorVersion = minorVersion;
            state.PatchVersion = patchVersion;

            // Compute build and version numbers.

            var buildCounter = Argument("build-counter", -1);
            var buildMetadata = Argument("build-metadata", "");

            // Assembly version:
            //   MAJOR.MINOR.0.0
            state.AssemblyVersion = $"{majorVersion}.{minorVersion}.0.0";

            // File version:
            //   MAJOR.MINOR.PATCH.BUILD (build counter >= 0)
            //   MAJOR.MINOR.PATCH.0 (build counter < 0)
            state.AssemblyFileVersion = buildCounter >= 0
                ? $"{majorVersion}.{minorVersion}.{patchVersion}.{buildCounter}"
                : $"{majorVersion}.{minorVersion}.{patchVersion}.0";

            // Informational version:
            //   MAJOR.MINOR.PATCH[-SUFFIX].[BUILD]+BRANCH[.METADATA]
            var informationalVersionBuilder = new StringBuilder($"{majorVersion}.{minorVersion}.{patchVersion}");
            if (!string.IsNullOrWhiteSpace(versionSuffix)) {
                informationalVersionBuilder.Append($"-{versionSuffix}");
            }
            if (buildCounter >= 0) {
                informationalVersionBuilder.Append($".{buildCounter}");
            }
            informationalVersionBuilder.Append($"+{NormaliseMetadata(branchName)}");
            if (!string.IsNullOrWhiteSpace(buildMetadata)) {
                informationalVersionBuilder.Append($".{NormaliseMetadata(buildMetadata)}");
            }
            state.InformationalVersion = informationalVersionBuilder.ToString();

            // Package version:
            //   MAJOR.MINOR.PATCH[-SUFFIX].[BUILD] (build counter >= 0)
            //   MAJOR.MINOR.PATCH[-SUFFIX] (build counter < 0)
            state.PackageVersion = string.IsNullOrWhiteSpace(versionSuffix)
                ? $"{majorVersion}.{minorVersion}.{patchVersion}"
                : buildCounter >= 0
                    ? $"{majorVersion}.{minorVersion}.{patchVersion}-{versionSuffix}.{buildCounter}"
                    : $"{majorVersion}.{minorVersion}.{patchVersion}-{versionSuffix}";

            // Build number:
            //   MAJOR.MINOR.PATCH[-SUFFIX].[BUILD]+BRANCH (build counter >= 0)
            //   MAJOR.MINOR.PATCH[-SUFFIX]+BRANCH (build counter < 0)
            state.BuildNumber = string.IsNullOrWhiteSpace(versionSuffix)
                ? buildCounter >= 0
                    ? $"{majorVersion}.{minorVersion}.{patchVersion}.{buildCounter}+{branchName}"
                    : $"{majorVersion}.{minorVersion}.{patchVersion}+{branchName}"
                : buildCounter >= 0
                    ? $"{majorVersion}.{minorVersion}.{patchVersion}-{versionSuffix}.{buildCounter}+{branchName}"
                    : $"{majorVersion}.{minorVersion}.{patchVersion}-{versionSuffix}+{branchName}";

            var setBuildNumber = !string.Equals(state.Target, "Clean", StringComparison.OrdinalIgnoreCase) && !string.Equals(state.Target, "BillOfMaterials", StringComparison.OrdinalIgnoreCase);
            if (setBuildNumber) {
                SetBuildSystemBuildNumber(BuildSystem, state);
                WriteBuildStateToLog(BuildSystem, state);
            }

            return state;
        }
        finally {
            WriteTaskEndMessage(BuildSystem, "Setup");
        }
    });
}

// Applies profile default values to the build state.
private void ApplyProfileDefaults(BuildState state, BuildProfile profile) {
    foreach (var defaultOption in profile.DefaultOptions) {
        switch (defaultOption.Key.ToLowerInvariant()) {
            case "configuration":
                state.Configuration = defaultOption.Value?.ToString() ?? "Debug";
                break;
            case "clean":
                state.Clean = defaultOption.Value is bool cleanValue && cleanValue;
                break;
            case "no-tests":
                state.SkipTests = defaultOption.Value is bool skipTests && skipTests;
                break;
            case "ci":
                state.ContinuousIntegrationBuild = (defaultOption.Value is bool ciValue && ciValue) || !BuildSystem.IsLocalBuild;
                break;
            case "sign-output":
                state.SignOutput = defaultOption.Value is bool signValue && signValue;
                break;
        }
    }
    
    // Set MSBuild properties
    state.MSBuildProperties = new List<string>();
}

// Applies command line argument overrides to the build state.
private void ApplyCommandLineOverrides(BuildState state, BuildProfile profile) {
    // Apply command line overrides
    state.Configuration = Argument("configuration", state.Configuration);
    state.Clean = HasArgument("clean") || state.Clean;
    state.SkipTests = HasArgument("no-tests") || state.SkipTests;
    
    // CI mode can be enabled via command line arg or profile option
    var ciFromProfile = state.ProfileOptions.ContainsKey("ci") && state.ProfileOptions["ci"] is bool ciEnabled && ciEnabled;
    state.ContinuousIntegrationBuild = HasArgument("ci") || ciFromProfile || !BuildSystem.IsLocalBuild;
    
    // Sign output can be enabled via command line arg or profile option
    var signFromProfile = state.ProfileOptions.ContainsKey("sign-output") && state.ProfileOptions["sign-output"] is bool signEnabled && signEnabled;
    state.SignOutput = HasArgument("sign-output") || signFromProfile;
    
    if (HasArgument("property")) {
        state.MSBuildProperties = Arguments<string>("property");
    }
}

// Parses profile-specific options and stores them in ProfileOptions.
private void ParseProfileOptions(BuildState state, BuildProfile profile) {
    foreach (var supportedOption in profile.SupportedOptions) {
        switch (supportedOption.ToLowerInvariant()) {
            case "packages":
                state.ProfileOptions["packages"] = !HasArgument("packages") || Argument("packages", true);
                break;
            case "containers":
                state.ProfileOptions["containers"] = !HasArgument("containers") || Argument("containers", true);
                break;
            case "sbom":
                state.ProfileOptions["sbom"] = !HasArgument("sbom") || Argument("sbom", true);
                break;
            case "ci":
                // CI can be enabled via argument or profile default, but command line overrides
                state.ProfileOptions["ci"] = HasArgument("ci") || (profile.DefaultOptions.ContainsKey("ci") && profile.DefaultOptions["ci"] is bool ciDefault && ciDefault);
                break;
            case "sign-output":
                state.ProfileOptions["sign-output"] = HasArgument("sign-output");
                break;
            case "container-registry":
                var registry = Argument("container-registry", "");
                if (!string.IsNullOrWhiteSpace(registry)) {
                    state.ProfileOptions["container-registry"] = registry;
                }
                break;
            case "github-username":
                var githubUser = Argument("github-username", "");
                if (!string.IsNullOrWhiteSpace(githubUser)) {
                    state.ProfileOptions["github-username"] = githubUser;
                }
                break;
            case "github-token":
                var githubToken = Argument("github-token", "");
                if (!string.IsNullOrWhiteSpace(githubToken)) {
                    state.ProfileOptions["github-token"] = githubToken;
                }
                break;
            case "build-counter":
                state.ProfileOptions["build-counter"] = Argument("build-counter", -1);
                break;
            case "build-metadata":
                var buildMetadata = Argument("build-metadata", "");
                if (!string.IsNullOrWhiteSpace(buildMetadata)) {
                    state.ProfileOptions["build-metadata"] = buildMetadata;
                }
                break;
        }
    }
}


// Configures task setup and teardown handlers.
private void ConfigureTaskEventHandlers() {
    // Pre-task action.
    TaskSetup(context => {
        WriteTaskStartMessage(BuildSystem, context.Task.Name);
        WriteLogMessage(BuildSystem, $"Running {context.Task.Name} task");
    });


    // Post task action.
    TaskTeardown(context => {
        WriteLogMessage(BuildSystem, $"Completed {context.Task.Name} task");
        WriteTaskEndMessage(BuildSystem, context.Task.Name);
    });
}


// Configures the available tasks.
private void ConfigureTasks() {
    Targets = new TaskDefinitions {
        Clean = Task("Clean")
            .WithCriteria<BuildState>((c, state) => state.RunCleanTarget)
            .Does<BuildState>(state => {
                foreach (var pattern in new [] { $"./src/**/bin/{state.Configuration}/**", $"./src/**/obj/{state.Configuration}/**", "./artifacts/**", "./**/TestResults/**" }) {
                    WriteLogMessage(BuildSystem, $"Cleaning directories: {pattern}");
                    CleanDirectories(pattern);
                }
            }),

        Restore = Task("Restore")
            .Does<BuildState>(state => {
                DotNetRestore(state.SolutionName);
            }),

        Build = Task("Build")
            .Does<BuildState>(state => {
                var buildSettings = new DotNetBuildSettings {
                    Configuration = state.Configuration,
                    NoRestore = true,
                    MSBuildSettings = new DotNetMSBuildSettings()
                };

                buildSettings.MSBuildSettings.Targets.Add(state.Clean ? "Rebuild" : "Build");
                ApplyMSBuildProperties(buildSettings.MSBuildSettings, state);
                DotNetBuild(state.SolutionName, buildSettings);
            }),

        Test = Task("Test")
            .WithCriteria<BuildState>((c, state) => !state.SkipTests)
            .Does<BuildState>(state => {
                var testSettings = new DotNetTestSettings {
                    Configuration = state.Configuration,
                    NoBuild = true
                };

                var testResultsPrefix = state.ContinuousIntegrationBuild
                    ? Guid.NewGuid().ToString()
                    : null;

                if (testResultsPrefix != null) {
                    // We're using a build system; write the test results to a file so that they can be
                    // imported into the build system.
                    testSettings.Loggers = new List<string> {
                        $"trx;LogFilePrefix={testResultsPrefix}"
                    };
                }

                try {
                    DotNetTest(state.SolutionName, testSettings);
                }
                finally {
                    if (testResultsPrefix != null) {
                        foreach (var testResultsFile in GetFiles($"./**/TestResults/{testResultsPrefix}*.trx")) {
                            ImportTestResults(BuildSystem, "mstest", testResultsFile);
                        }
                    }
                }
            }),

        Pack = Task("Pack")
            .Does<BuildState>(state => {
                var buildSettings = new DotNetPackSettings {
                    Configuration = state.Configuration,
                    NoRestore = true,
                    NoBuild = true,
                    MSBuildSettings = new DotNetMSBuildSettings()
                };

                ApplyMSBuildProperties(buildSettings.MSBuildSettings, state);
                DotNetPack(state.SolutionName, buildSettings);
            }),

        Publish = Task("Publish")
            .Does<BuildState>(state => {
                foreach (var projectFile in GetFiles("./**/*.*proj")) {
                    var projectDir = projectFile.GetDirectory();

                    foreach (var publishProfileFile in GetFiles(projectDir.FullPath + "/**/*.pubxml")) {
                        WriteLogMessage(BuildSystem, $"Publishing project {projectFile.GetFilename()} using profile {publishProfileFile.GetFilename()}.");

                        var buildSettings = new DotNetPublishSettings {
                            Configuration = state.Configuration,
                            MSBuildSettings = new DotNetMSBuildSettings()
                        };

                        ApplyMSBuildProperties(buildSettings.MSBuildSettings, state);
                        buildSettings.MSBuildSettings.Properties["PublishProfile"] = new List<string> { publishProfileFile.FullPath };
                        DotNetPublish(projectFile.FullPath, buildSettings);
                    }
                }
            }),

        PublishContainer = Task("PublishContainer")
            .Does<BuildState>(state => {
                var containerImageProjects = state.PublishContainerProjects?.ToArray();

                if (containerImageProjects == null || containerImageProjects.Length == 0) {
                    throw new InvalidOperationException("No container projects were specified. Ensure that your Cake script specifies container projects when calling Bootstrap(). See https://github.com/wazzamatazz/cake-recipes#publishing-container-images for more information.");
                }

                var registry = Argument("container-registry", "");
                var rid = Argument("container-rid", "");

                foreach (var projectFile in GetFiles("./**/*.*proj")) {
                    var projectDir = projectFile.GetDirectory();

                    // Publish container images.
                    if (!containerImageProjects.Contains(projectFile.GetFilenameWithoutExtension().ToString(), StringComparer.OrdinalIgnoreCase)) {
                        continue;
                    }

                    WriteLogMessage(BuildSystem, $"Publishing container image for project {projectFile.GetFilename()} to {(string.IsNullOrWhiteSpace(registry) ? "default registry" : registry)}");

                    var buildSettings = new DotNetPublishSettings() { Configuration = state.Configuration };

                    buildSettings.MSBuildSettings = new DotNetMSBuildSettings();

                    if (!string.IsNullOrWhiteSpace(registry)) {
                        buildSettings.MSBuildSettings.WithProperty("ContainerRegistry", registry);
                    }

                    if (!string.IsNullOrWhiteSpace(rid)) {
                        buildSettings.MSBuildSettings.WithProperty("ContainerRuntimeIdentifier", rid);
                    }

                    ApplyMSBuildProperties(buildSettings.MSBuildSettings, state);

                    buildSettings.MSBuildSettings.WithTarget("PublishContainer");

                    DotNetPublish(projectFile.FullPath, buildSettings);
                }
            }),

        BillOfMaterials = Task("BillOfMaterials")
            .Does<BuildState>(state => {
                var cycloneDx = Context.Tools.Resolve(IsRunningOnWindows()
                    ? "dotnet-CycloneDX.exe"
                    : "dotnet-CycloneDX");

                var githubUser = Argument("github-username", "");
                var githubToken = Argument("github-token", "");

                if (!string.IsNullOrWhiteSpace(githubUser) && string.IsNullOrWhiteSpace(githubToken)) {
                    throw new InvalidOperationException("When specifying a GitHub username for Bill of Materials generation you must also specify a personal access token using the '--github-token' argument.");
                }

                if (!string.IsNullOrWhiteSpace(githubToken) && string.IsNullOrWhiteSpace(githubUser)) {
                    throw new InvalidOperationException("When specifying a GitHub personal access token for Bill of Materials generation you must also specify the username for the token using the '--github-username' argument.");
                }

                var cycloneDxArgs = new ProcessArgumentBuilder()
                    .Append(state.SolutionName)
                    .Append("-o")
                    .Append("./artifacts/bom");

                if (!string.IsNullOrWhiteSpace(githubUser)) {
                    cycloneDxArgs.Append("-egl"); // Enable GitHub licence resolution.
                    cycloneDxArgs.Append("-gu").Append(githubUser);
                }

                if (!string.IsNullOrWhiteSpace(githubToken)) {
                    cycloneDxArgs.Append("-gt").Append(githubToken);
                }

                StartProcess(cycloneDx, new ProcessSettings {
                    Arguments = cycloneDxArgs
                });
            })

    };

}


// Informs the build system of the build number and version numbers for this build.
private void SetBuildSystemBuildNumber(BuildSystem buildSystem, BuildState buildState) {
    // Tell TeamCity the build number if required.
    if (buildSystem.IsRunningOnTeamCity) {
        buildSystem.TeamCity.SetBuildNumber(buildState.BuildNumber);
        buildSystem.TeamCity.SetParameter("system.AssemblyVersion", buildState.AssemblyVersion);
        buildSystem.TeamCity.SetParameter("system.AssemblyFileVersion", buildState.AssemblyFileVersion);
        buildSystem.TeamCity.SetParameter("system.InformationalVersion", buildState.InformationalVersion);
        buildSystem.TeamCity.SetParameter("system.PackageVersion", buildState.PackageVersion);
        buildSystem.TeamCity.SetParameter("MajorVersion", buildState.MajorVersion.ToString());
        buildSystem.TeamCity.SetParameter("MinorVersion", buildState.MinorVersion.ToString());
        buildSystem.TeamCity.SetParameter("PatchVersion", buildState.PatchVersion.ToString());
    }
}


// Writes a log message.
public void WriteLogMessage(BuildSystem buildSystem, string message, bool newlineBeforeMessage = true) {
    if (buildSystem.IsRunningOnTeamCity) {
        buildSystem.TeamCity.WriteProgressMessage(message);
    }
    else {
        if (newlineBeforeMessage) {
            AnsiConsole.WriteLine();
        }
        AnsiConsole.WriteLine(message);
    }
}


public void WriteErrorMessage(BuildSystem buildSystem, string message, Exception error = null) {
    var msg = message ?? error?.Message ?? "Build error";
    if (buildSystem.IsRunningOnTeamCity) {
        buildSystem.TeamCity.WriteStatus(msg, "ERROR", error?.ToString());
    }
    else {
        AnsiConsole.WriteLine(msg);
        if (error != null) {
            AnsiConsole.WriteException(error);
        }
    }
}


// Writes a task started message.
private void WriteTaskStartMessage(BuildSystem buildSystem, string description) {
    if (buildSystem.IsRunningOnTeamCity) {
        buildSystem.TeamCity.WriteStartBuildBlock(description);
    }
}


// Writes a task completed message.
private void WriteTaskEndMessage(BuildSystem buildSystem, string description) {
    if (buildSystem.IsRunningOnTeamCity) {
        buildSystem.TeamCity.WriteEndBuildBlock(description);
    }
}


// Writes the specified build state to the log.
private void WriteBuildStateToLog(BuildSystem buildSystem, BuildState state) {
    WriteLogMessage(buildSystem, $"Solution Name: {state.SolutionName}", true);
    WriteLogMessage(buildSystem, $"Build Number: {state.BuildNumber}", false);
    WriteLogMessage(buildSystem, $"Assembly Version: {state.AssemblyVersion}", true);
    WriteLogMessage(buildSystem, $"File Version: {state.AssemblyFileVersion}", false);
    WriteLogMessage(buildSystem, $"Informational Version: {state.InformationalVersion}", false);
    WriteLogMessage(buildSystem, $"Package Version: {state.PackageVersion}", false);
    WriteLogMessage(buildSystem, $"Target: {state.Target}", true);
    WriteLogMessage(buildSystem, $"Configuration: {state.Configuration}", false);
    WriteLogMessage(buildSystem, $"Clean: {state.RunCleanTarget}", false);
    WriteLogMessage(buildSystem, $"Skip Tests: {state.SkipTests}", false);
    WriteLogMessage(buildSystem, $"Continuous Integration Build: {state.ContinuousIntegrationBuild}", false);
    WriteLogMessage(buildSystem, $"Sign Output: {state.CanSignOutput}", false);
}


// Adds MSBuild properties from the build state.
public static void ApplyMSBuildProperties(DotNetMSBuildSettings settings, BuildState state) {
    if (state.MSBuildProperties?.Count > 0) {
        // We expect each property to be in "NAME=VALUE" format.
        var regex = new System.Text.RegularExpressions.Regex(@"^(?<name>.+)=(?<value>.+)$");

        foreach (var prop in state.MSBuildProperties) {
            if (string.IsNullOrWhiteSpace(prop)) {
                continue;
            }

            var m = regex.Match(prop.Trim());
            if (!m.Success) {
                continue;
            }

            settings.Properties[m.Groups["name"].Value] = new List<string> { m.Groups["value"].Value };
        }
    }

    // Specify if this is a CI build.
    if (state.ContinuousIntegrationBuild) {
        settings.Properties["ContinuousIntegrationBuild"] = new List<string> { "True" };
    }

    // Specify if we are signing DLLs and NuGet packages.
    if (state.CanSignOutput) {
        settings.Properties["SignOutput"] = new List<string> { "True" };
    }

    // Set version numbers.
    settings.Properties["MajorVersion"] = new List<string> { state.MajorVersion.ToString() };
    settings.Properties["MinorVersion"] = new List<string> { state.MinorVersion.ToString() };
    settings.Properties["PatchVersion"] = new List<string> { state.PatchVersion.ToString() };
    settings.Properties["AssemblyVersion"] = new List<string> { state.AssemblyVersion };
    settings.Properties["FileVersion"] = new List<string> { state.AssemblyFileVersion };
    settings.Properties["Version"] = new List<string> { state.PackageVersion };
    settings.Properties["InformationalVersion"] = new List<string> { state.InformationalVersion };
}


// Imports test results into the build system.
private static void ImportTestResults(BuildSystem buildSystem, string testProvider, FilePath resultsFile) {
    if (resultsFile == null) {
        return;
    }

    if (buildSystem.IsRunningOnTeamCity) {
        buildSystem.TeamCity.ImportData(testProvider, resultsFile);
    }
}
