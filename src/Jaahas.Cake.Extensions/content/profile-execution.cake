// Profile execution engine for running build profiles.

#load "profile-definitions.cake"

// Executes a build profile by running its tasks in sequence.
public class ProfileExecutor
{
    private readonly ICakeContext _context;
    private readonly TaskDefinitions _targets;
    
    public ProfileExecutor(ICakeContext context, TaskDefinitions targets)
    {
        _context = context;
        _targets = targets;
    }
    
    public void ExecuteProfile(BuildProfile profile, BuildState state)
    {
        if (profile == null)
            throw new ArgumentNullException(nameof(profile));
        if (state == null)
            throw new ArgumentNullException(nameof(state));
            
        WriteLogMessage(_context.BuildSystem(), $"Executing build profile: {profile.Name}");
        WriteLogMessage(_context.BuildSystem(), $"Profile description: {profile.Description}", false);
        WriteLogMessage(_context.BuildSystem(), $"Tasks to execute: {string.Join(" → ", profile.Tasks)}", false);
        
        foreach (var taskName in profile.Tasks)
        {
            if (ShouldSkipTask(taskName, profile, state))
            {
                WriteLogMessage(_context.BuildSystem(), $"Skipping task: {taskName}");
                continue;
            }
            
            WriteLogMessage(_context.BuildSystem(), $"Executing task: {taskName}");
            ExecuteTask(taskName, state);
        }
        
        WriteLogMessage(_context.BuildSystem(), $"Successfully completed build profile: {profile.Name}");
    }
    
    private bool ShouldSkipTask(string taskName, BuildProfile profile, BuildState state)
    {
        // Skip tests if no-tests is enabled
        if (taskName == "Test" && state.SkipTests)
        {
            return true;
        }
        
        // Skip BillOfMaterials if sbom is disabled
        if (taskName == "BillOfMaterials" && state.ProfileOptions.ContainsKey("sbom") && 
            state.ProfileOptions["sbom"] is bool sbomEnabled && !sbomEnabled)
        {
            return true;
        }
        
        // Skip PublishContainer if no container projects are specified
        if (taskName == "PublishContainer" && 
            (state.PublishContainerProjects == null || !state.PublishContainerProjects.Any()))
        {
            WriteLogMessage(_context.BuildSystem(), "No container projects specified. Use Bootstrap() containerProjects parameter to enable container publishing.");
            return true;
        }
        
        // Skip Clean task if it's not needed (handled by individual task criteria)
        if (taskName == "Clean" && !state.RunCleanTarget)
        {
            return true;
        }
        
        return false;
    }
    
    private void ExecuteTask(string taskName, BuildState state)
    {
        var task = GetTaskByName(taskName);
        if (task == null)
        {
            throw new InvalidOperationException($"Task '{taskName}' is not defined. Available tasks: {GetAvailableTaskNames()}");
        }
        
        try
        {
            // Execute the task action directly
            ExecuteTaskAction(taskName, state);
        }
        catch (Exception ex)
        {
            WriteErrorMessage(_context.BuildSystem(), $"Task '{taskName}' failed", ex);
            throw;
        }
    }
    
    private void ExecuteTaskAction(string taskName, BuildState state)
    {
        switch (taskName)
        {
            case "Clean":
                ExecuteCleanTask(state);
                break;
            case "Restore":
                ExecuteRestoreTask(state);
                break;
            case "Build":
                ExecuteBuildTask(state);
                break;
            case "Test":
                ExecuteTestTask(state);
                break;
            case "Pack":
                ExecutePackTask(state);
                break;
            case "Publish":
                ExecutePublishTask(state);
                break;
            case "PublishContainer":
                ExecutePublishContainerTask(state);
                break;
            case "BillOfMaterials":
                ExecuteBillOfMaterialsTask(state);
                break;
            default:
                throw new InvalidOperationException($"Unknown task: {taskName}");
        }
    }
    
    private void ExecuteCleanTask(BuildState state)
    {
        if (!state.RunCleanTarget) return;
        
        WriteTaskStartMessage(_context.BuildSystem(), "Clean");
        try
        {
            foreach (var pattern in new [] { $"./src/**/bin/{state.Configuration}/**", $"./src/**/obj/{state.Configuration}/**", "./artifacts/**", "./**/TestResults/**" })
            {
                WriteLogMessage(_context.BuildSystem(), $"Cleaning directories: {pattern}", false);
                _context.CleanDirectories(pattern);
            }
        }
        finally
        {
            WriteTaskEndMessage(_context.BuildSystem(), "Clean");
        }
    }
    
    private void ExecuteRestoreTask(BuildState state)
    {
        WriteTaskStartMessage(_context.BuildSystem(), "Restore");
        try
        {
            _context.DotNetRestore(state.SolutionName);
        }
        finally
        {
            WriteTaskEndMessage(_context.BuildSystem(), "Restore");
        }
    }
    
    private void ExecuteBuildTask(BuildState state)
    {
        WriteTaskStartMessage(_context.BuildSystem(), "Build");
        try
        {
            var buildSettings = new DotNetBuildSettings {
                Configuration = state.Configuration,
                NoRestore = true,
                MSBuildSettings = new DotNetMSBuildSettings()
            };

            buildSettings.MSBuildSettings.Targets.Add(state.Clean ? "Rebuild" : "Build");
            ApplyMSBuildProperties(buildSettings.MSBuildSettings, state);
            _context.DotNetBuild(state.SolutionName, buildSettings);
        }
        finally
        {
            WriteTaskEndMessage(_context.BuildSystem(), "Build");
        }
    }
    
    private void ExecuteTestTask(BuildState state)
    {
        if (state.SkipTests) return;
        
        WriteTaskStartMessage(_context.BuildSystem(), "Test");
        try
        {
            var testSettings = new DotNetTestSettings {
                Configuration = state.Configuration,
                NoBuild = true
            };

            var testResultsPrefix = state.ContinuousIntegrationBuild
                ? Guid.NewGuid().ToString()
                : null;

            if (testResultsPrefix != null)
            {
                testSettings.Loggers = new List<string> {
                    $"trx;LogFilePrefix={testResultsPrefix}"
                };
            }

            try
            {
                _context.DotNetTest(state.SolutionName, testSettings);
            }
            finally
            {
                if (testResultsPrefix != null)
                {
                    foreach (var testResultsFile in _context.GetFiles($"./**/TestResults/{testResultsPrefix}*.trx"))
                    {
                        ImportTestResults(_context.BuildSystem(), "mstest", testResultsFile);
                    }
                }
            }
        }
        finally
        {
            WriteTaskEndMessage(_context.BuildSystem(), "Test");
        }
    }
    
    private void ExecutePackTask(BuildState state)
    {
        WriteTaskStartMessage(_context.BuildSystem(), "Pack");
        try
        {
            var buildSettings = new DotNetPackSettings {
                Configuration = state.Configuration,
                NoRestore = true,
                NoBuild = true,
                MSBuildSettings = new DotNetMSBuildSettings()
            };

            ApplyMSBuildProperties(buildSettings.MSBuildSettings, state);
            _context.DotNetPack(state.SolutionName, buildSettings);
        }
        finally
        {
            WriteTaskEndMessage(_context.BuildSystem(), "Pack");
        }
    }
    
    private void ExecutePublishTask(BuildState state)
    {
        WriteTaskStartMessage(_context.BuildSystem(), "Publish");
        try
        {
            foreach (var projectFile in _context.GetFiles("./**/*.*proj"))
            {
                var projectDir = projectFile.GetDirectory();

                foreach (var publishProfileFile in _context.GetFiles(projectDir.FullPath + "/**/*.pubxml"))
                {
                    WriteLogMessage(_context.BuildSystem(), $"Publishing project {projectFile.GetFilename()} using profile {publishProfileFile.GetFilename()}.", false);

                    var buildSettings = new DotNetPublishSettings {
                        Configuration = state.Configuration,
                        MSBuildSettings = new DotNetMSBuildSettings()
                    };

                    ApplyMSBuildProperties(buildSettings.MSBuildSettings, state);
                    buildSettings.MSBuildSettings.Properties["PublishProfile"] = new List<string> { publishProfileFile.FullPath };
                    _context.DotNetPublish(projectFile.FullPath, buildSettings);
                }
            }
        }
        finally
        {
            WriteTaskEndMessage(_context.BuildSystem(), "Publish");
        }
    }
    
    private void ExecutePublishContainerTask(BuildState state)
    {
        WriteTaskStartMessage(_context.BuildSystem(), "PublishContainer");
        try
        {
            var containerImageProjects = state.PublishContainerProjects?.ToArray();

            if (containerImageProjects == null || containerImageProjects.Length == 0)
            {
                throw new InvalidOperationException("No container projects were specified. Ensure that your Cake script specifies container projects when calling Bootstrap(). See https://github.com/wazzamatazz/cake-recipes#publishing-container-images for more information.");
            }

            var registry = GetProfileOption(state, "container-registry", "");
            var os = GetProfileOption(state, "container-os", "");
            var arch = GetProfileOption(state, "container-arch", "");

            foreach (var projectFile in _context.GetFiles("./**/*.*proj"))
            {
                if (!containerImageProjects.Contains(projectFile.GetFilenameWithoutExtension().ToString(), StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                WriteLogMessage(_context.BuildSystem(), $"Publishing container image for project {projectFile.GetFilename()} to {(string.IsNullOrWhiteSpace(registry) ? "default registry" : registry)}", false);

                var buildSettings = new DotNetPublishSettings() { Configuration = state.Configuration };
                buildSettings.MSBuildSettings = new DotNetMSBuildSettings();

                if (!string.IsNullOrWhiteSpace(registry))
                {
                    buildSettings.MSBuildSettings.WithProperty("ContainerRegistry", registry);
                }

                if (!string.IsNullOrWhiteSpace(os))
                {
                    buildSettings.MSBuildSettings.WithProperty("ContainerRuntimeIdentifier", $"{os}-{arch}");
                }
                else if (!string.IsNullOrWhiteSpace(arch))
                {
                    buildSettings.MSBuildSettings.WithProperty("ContainerRuntimeIdentifier", $"linux-{arch}");
                }

                ApplyMSBuildProperties(buildSettings.MSBuildSettings, state);
                buildSettings.MSBuildSettings.WithTarget("PublishContainer");

                _context.DotNetPublish(projectFile.FullPath, buildSettings);
            }
        }
        finally
        {
            WriteTaskEndMessage(_context.BuildSystem(), "PublishContainer");
        }
    }
    
    private void ExecuteBillOfMaterialsTask(BuildState state)
    {
        WriteTaskStartMessage(_context.BuildSystem(), "BillOfMaterials");
        try
        {
            var cycloneDx = _context.Tools.Resolve(_context.IsRunningOnWindows()
                ? "dotnet-CycloneDX.exe"
                : "dotnet-CycloneDX");

            var githubUser = GetProfileOption(state, "github-username", "");
            var githubToken = GetProfileOption(state, "github-token", "");

            if (!string.IsNullOrWhiteSpace(githubUser) && string.IsNullOrWhiteSpace(githubToken))
            {
                throw new InvalidOperationException("When specifying a GitHub username for Bill of Materials generation you must also specify a personal access token using the '--github-token' argument.");
            }

            if (!string.IsNullOrWhiteSpace(githubToken) && string.IsNullOrWhiteSpace(githubUser))
            {
                throw new InvalidOperationException("When specifying a GitHub personal access token for Bill of Materials generation you must also specify the username for the token using the '--github-username' argument.");
            }

            var cycloneDxArgs = new ProcessArgumentBuilder()
                .Append(state.SolutionName)
                .Append("-o")
                .Append("./artifacts/bom");

            if (!string.IsNullOrWhiteSpace(githubUser))
            {
                cycloneDxArgs.Append("-egl");
                cycloneDxArgs.Append("-gu").Append(githubUser);
            }

            if (!string.IsNullOrWhiteSpace(githubToken))
            {
                cycloneDxArgs.Append("-gt").Append(githubToken);
            }

            _context.StartProcess(cycloneDx, new ProcessSettings {
                Arguments = cycloneDxArgs
            });
        }
        finally
        {
            WriteTaskEndMessage(_context.BuildSystem(), "BillOfMaterials");
        }
    }
    
    private string GetProfileOption(BuildState state, string optionName, string defaultValue)
    {
        if (state.ProfileOptions.TryGetValue(optionName, out var value) && value is string stringValue)
        {
            return stringValue;
        }
        return defaultValue;
    }
    
    private CakeTaskBuilder GetTaskByName(string taskName)
    {
        return taskName switch
        {
            "Clean" => _targets.Clean,
            "Restore" => _targets.Restore,
            "Build" => _targets.Build,
            "Test" => _targets.Test,
            "Pack" => _targets.Pack,
            "Publish" => _targets.Publish,
            "PublishContainer" => _targets.PublishContainer,
            "BillOfMaterials" => _targets.BillOfMaterials,
            _ => null
        };
    }
    
    private string GetAvailableTaskNames()
    {
        return "Clean, Restore, Build, Test, Pack, Publish, PublishContainer, BillOfMaterials";
    }
}