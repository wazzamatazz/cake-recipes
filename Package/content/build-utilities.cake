#addin nuget:?package=Cake.Git&version=2.0.0
#addin nuget:?package=Cake.Json&version=7.0.1

#tool dotnet:?package=CycloneDX&version=2.3.0

#load "build-state.cake"

// Bootstraps the build using the specified default solution file and JSON version file.
public void Bootstrap(string solutionFilePath, string versionFilePath) {
    ConfigureBuildState(solutionFilePath, versionFilePath, GetBranchName());
    ConfigureTaskEventHandlers();
    ConfigureTasks();
}


private string GetBranchName() {
    return Argument("branch", "");
}


// Gets the target to run.
public string GetTarget() {
    return Argument("target", HasArgument("no-tests") ? "Build" : "Test");
}


// Normalises metadata for use in a SemVer v2.0.0 version.
private string NormaliseMetadata(string s) {
    var metadataNormaliser = new System.Text.RegularExpressions.Regex("[^0-9A-Za-z-]");
    return metadataNormaliser.Replace(s.Trim(), ".");
}


// Configures the build state using the specified default solution file and JSON version file.
private void ConfigureBuildState(string solutionFilePath, string versionFilePath, string branchName) {
    // Constructs the build state object.
    Setup<BuildState>(context => {
        try {
            WriteTaskStartMessage(BuildSystem, "Setup");
            var state = new BuildState() {
                SolutionName = Argument("project", solutionFilePath),
                Target = target,
                Configuration = Argument("configuration", "Debug"),
                ContinuousIntegrationBuild = HasArgument("ci") || !BuildSystem.IsLocalBuild,
                Clean = HasArgument("clean"),
                SkipTests = HasArgument("no-tests"),
                SignOutput = HasArgument("sign-output"),
                MSBuildProperties = HasArgument("property") ? Arguments<string>("property") : new List<string>()
            };

            // Get raw version numbers from JSON.

            var versionJson = ParseJsonFromFile(versionFilePath);

            var majorVersion = versionJson.Value<int>("Major");
            var minorVersion = versionJson.Value<int>("Minor");
            var patchVersion = versionJson.Value<int>("Patch");
            var versionSuffix = versionJson.Value<string>("PreRelease");
            
            state.MajorVersion = majorVersion;
            state.MinorVersion = minorVersion;
            state.PatchVersion = patchVersion;

            // Compute build and version numbers.

            var buildCounter = Argument("build-counter", 0);
            var buildMetadata = Argument("build-metadata", "");

            // Set branch name. For Git repositories, we always use the friendly name of the 
            // current branch, regardless of what was specified in the branchName parameter.
            string branch;

            var currentDir = DirectoryPath.FromString(".");
            if (GitIsValidRepository(currentDir)) {
                branch = GitBranchCurrent(currentDir).FriendlyName;
            }
            else {
                branch = string.IsNullOrEmpty(branchName)
                    ? "default"
                    : branchName;
            }
            
            state.AssemblyVersion = $"{majorVersion}.{minorVersion}.0.0";

            state.AssemblyFileVersion = $"{majorVersion}.{minorVersion}.{patchVersion}.{buildCounter}";

            state.InformationalVersion = string.IsNullOrWhiteSpace(versionSuffix)
                ? $"{majorVersion}.{minorVersion}.{patchVersion}.{buildCounter}+{NormaliseMetadata(branch)}"
                : $"{majorVersion}.{minorVersion}.{patchVersion}-{versionSuffix}.{buildCounter}+{NormaliseMetadata(branch)}";

            if (!string.IsNullOrWhiteSpace(buildMetadata)) {
                state.InformationalVersion = string.Concat(state.InformationalVersion, ".", NormaliseMetadata(buildMetadata));
            }

            state.PackageVersion = string.IsNullOrWhiteSpace(versionSuffix)
                ? $"{majorVersion}.{minorVersion}.{patchVersion}"
                : $"{majorVersion}.{minorVersion}.{patchVersion}-{versionSuffix}.{buildCounter}";

            state.BuildNumber = string.IsNullOrWhiteSpace(versionSuffix)
                ? $"{majorVersion}.{minorVersion}.{patchVersion}.{buildCounter}+{branch}"
                : $"{majorVersion}.{minorVersion}.{patchVersion}-{versionSuffix}.{buildCounter}+{branch}";

            if (!string.Equals(state.Target, "Clean", StringComparison.OrdinalIgnoreCase)) {
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
    // Cleans up artifact and bin folders.
    Task("Clean")
        .WithCriteria<BuildState>((c, state) => state.RunCleanTarget)
        .Does<BuildState>(state => {
            foreach (var pattern in new [] { $"./src/**/bin/{state.Configuration}", "./artifacts/**", "./**/TestResults/**" }) {
                WriteLogMessage(BuildSystem, $"Cleaning directories: {pattern}");
                CleanDirectories(pattern);
            }
        });


    // Restores NuGet packages.
    Task("Restore")
        .Does<BuildState>(state => {
            DotNetRestore(state.SolutionName);
        });


    // Builds the solution.
    Task("Build")
        .IsDependentOn("Clean")
        .IsDependentOn("Restore")
        .Does<BuildState>(state => {
            var buildSettings = new DotNetBuildSettings {
                Configuration = state.Configuration,
                NoRestore = true,
                MSBuildSettings = new DotNetMSBuildSettings()
            };

            buildSettings.MSBuildSettings.Targets.Add(state.Clean ? "Rebuild" : "Build");
            ApplyMSBuildProperties(buildSettings.MSBuildSettings, state);
            DotNetBuild(state.SolutionName, buildSettings);
        });


    // Runs unit tests.
    Task("Test")
        .IsDependentOn("Build")
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

            DotNetTest(state.SolutionName, testSettings);

            if (testResultsPrefix != null) {
                foreach (var testResultsFile in GetFiles($"./**/TestResults/{testResultsPrefix}*.trx")) {
                    ImportTestResults(BuildSystem, "mstest", testResultsFile);
                }
            }
        });


    // Builds NuGet packages.
    Task("Pack")
        .IsDependentOn("Test")
        .Does<BuildState>(state => {
            var buildSettings = new DotNetPackSettings {
                Configuration = state.Configuration,
                NoRestore = true,
                NoBuild = true,
                MSBuildSettings = new DotNetMSBuildSettings()
            };

            ApplyMSBuildProperties(buildSettings.MSBuildSettings, state);
            DotNetPack(state.SolutionName, buildSettings);
        });

    // Generates a CycloneDX Software Bill of Materials
    Task("BillOfMaterials")
        .IsDependentOn("Clean")
        .Does<BuildState>(state => {
            var cycloneDx = Context.Tools.Resolve("dotnet-CycloneDX.exe");
            StartProcess(cycloneDx, new ProcessSettings {
                Arguments = new ProcessArgumentBuilder()
                    .Append(state.SolutionName)
                    .Append("-o")
                    .Append("./artifacts/bom")
            });
        });
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
            Console.WriteLine();
        }
        Console.WriteLine(message);
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
    WriteLogMessage(buildSystem, $"Target: {state.Target}", false);
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
