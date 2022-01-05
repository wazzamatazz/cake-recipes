# Jaahas.Cake.Extensions

`Jaahas.Cake.Extensions` is a recipe package for [Cake](https://cakebuild.net), used to provide a common way of versioning and running builds using Cake.


# Dependencies

`Jaahas.Cake.Extensions` assumes that you are using Cake v2.0.0 or later.


# Getting Started

In the folder where your .NET solution file resides, create a `version.json` file that is structured as follows:

```json
{
  "Major": 1,
  "Minor": 0,
  "Patch": 0,
  "PreRelease": ""
}
```

Update your `build.cake` file as follows:

```cake
const string DefaultSolutionFile = "./MySolution.sln";
const string VersionFile = "./version.json";

#load nuget:?package=Jaahas.Cake.Extensions&version=1.0.0

// Bootstrap build context and tasks.
Bootstrap(DefaultSolutionFile, VersionFile);

// Get the target that was specified.
var target = GetTarget();

// Run the target.
RunTarget(target);
```


# Command Line Arguments

The following command line arguments are supported by the recipe:

| Argument | Description | Default Value | Allowed Values |
| -------- | ----------- | ------------- | ---------------|
| `--branch=<FRIENDLY BRANCH NAME>` | The friendly name of the source control branch that is being built. Ignored for Git repositories. | | |
| `--project=<PROJECT OR SOLUTION>` | The MSBuild project or solution to build. | `DefaultSolutionFile` constant in `build.cake` file | |
| `--target=<TARGET>` | The Cake target to run. | `Test` | `Clean`, `Restore`, `Build`, `Test`, `Pack` |
| `--configuration=<CONFIGURATION>` | The MSBuild configuration to use. | `Debug` | Any configuration defined in the MSBuild solution |
| `--clean` | Specifies that this is a rebuild rather than an incremental build. All artifact, bin, and test output folders will be cleaned prior to running the specified target. | | |
| `--no-tests` | Specifies that unit tests should be skipped, even if a target that depends on the `Test` target is specified. | | |
| `--ci` | Forces continuous integration build mode. Not required if the build is being run by a supported continuous integration build system. | | |
| `--sign-output` | Tells MSBuild that signing is required by setting the 'SignOutput' build property to 'True'. The signing implementation must be supplied by MSBuild. | | |
| `--build-counter=<COUNTER>` | The build counter. This is used when generating version numbers for the build. | 0 | |
| `--build-metadata=<METADATA>` | Additional build metadata that will be included in the information version number generated for compiled assemblies. | | |
| `--property=<PROPERTY>` | Specifies an additional property to pass to MSBuild during `Build` and `Pack` targets. The value must be specified using a `<NAME>=<VALUE>` format e.g. `--property="NoWarn=CS1591"`. This argument can be specified multiple times. | | |
