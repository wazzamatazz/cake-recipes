# About

Jaahas.Cake.Extensions is a recipe package for [Cake](https://cakebuild.net) that provides a profile-based build system for .NET projects. It offers predefined build workflows and intelligent task orchestration for common development scenarios.

# How to Use

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

#load nuget:?package=Jaahas.Cake.Extensions&version=5.0.0

// Bootstrap build context and tasks.
Bootstrap(DefaultSolutionFile, VersionFile);

// Run with profile-based execution.
Run();
```

# Build Profiles

The recipe provides predefined build profiles for common scenarios:

## Available Profiles

| Profile | Description | Tasks | Default Config |
| ------- | ----------- | ----- | -------------- |
| `test` | Standard development build with tests | Restore → Build → Test | Debug |
| `dev` | Fast development build without tests | Restore → Build | Debug |
| `pack` | Build and create NuGet packages | Restore → Build → Test → Pack | Release |
| `containers` | Build and publish container images | Restore → Build → Test → PublishContainer | Release |
| `release` | Complete release with all components | Clean → Restore → Build → Test → Pack → PublishContainer → BillOfMaterials | Release |

## Running Builds

Use intuitive profile-based commands:

```bash
# Development builds
./build.sh test                        # Standard build with tests
./build.sh dev                         # Fast build without tests

# Release builds  
./build.sh release                     # Complete release (all components)
./build.sh release --packages=false    # Release without NuGet packages
./build.sh release --containers=false  # Release without containers
./build.sh release --ci --sign-output  # CI release with signing

# Component-specific builds
./build.sh pack                        # Build and create packages
./build.sh containers                  # Build and publish containers
```

## Release Profile Component Control

The `release` profile supports component toggles for flexible builds:

| Argument | Description | Default |
| -------- | ----------- | ------- |
| `--packages=<true\|false>` | Enable/disable NuGet package creation | `true` |
| `--containers=<true\|false>` | Enable/disable container image publishing | `true` |
| `--sbom=<true\|false>` | Enable/disable Software Bill of Materials generation | `true` |
| `--ci` | Enable continuous integration mode | `false` |
| `--sign-output` | Enable output signing | `false` |

# Common Arguments

The following command line arguments are supported:

| Argument | Description | Default Value |
| -------- | ----------- | ------------- |
| `--project=<PROJECT OR SOLUTION>` | The MSBuild project or solution to build | `DefaultSolutionFile` constant in `build.cake` file |
| `--configuration=<CONFIGURATION>` | The MSBuild configuration to use | Profile-specific (Debug/Release) |
| `--clean` | Perform a clean rebuild | Profile-specific |
| `--no-tests` | Skip unit tests | `false` |
| `--build-counter=<COUNTER>` | Build counter for versioning | -1 |
| `--build-metadata=<METADATA>` | Additional build metadata | |
| `--container-registry=<REGISTRY>` | Container registry for publishing images | Local Docker/Podman registry |
| `--property=<PROPERTY>` | Additional MSBuild property (`NAME=VALUE` format) | |
| `--github-username=<USERNAME>` | GitHub username for SBOM generation (required with `--github-token`) | |
| `--github-token=<TOKEN>` | GitHub token for SBOM generation (required with `--github-username`) | |

## Legacy Support

The system maintains backward compatibility with the legacy target-based approach:

| Legacy Argument | Modern Equivalent | Notes |
| --------------- | ----------------- | ----- |
| `--target=Test` | `test` profile | Automatically mapped |
| `--target=Pack` | `pack` profile | Automatically mapped |
| `--target=PublishContainer` | `containers` profile | Automatically mapped |


# Individual Tasks

The recipe defines the following individual tasks that are orchestrated by profiles:

| Task | Description | Default Configuration |
| ---- | ----------- | -------------------- |
| `Clean` | Cleans the `obj`, `bin`, `artifacts` and `TestResults` folders for the repository | Debug |
| `Restore` | Restores NuGet packages for the solution | Debug |
| `Build` | Builds the solution | Debug |
| `Test` | Runs unit tests for the solution | Debug |
| `Pack` | Creates NuGet packages for the solution | Release |
| `Publish` | Publishes any solution projects that define `.pubxml` publish profiles | Release |
| `PublishContainer` | Publishes container images for registered container projects | Release |
| `BillOfMaterials` | Generates Software Bill of Materials (SBOM) using [CycloneDX](https://github.com/CycloneDX/cyclonedx-dotnet) | Release |

**Note**: Tasks are executed automatically by profiles. Direct task execution via `--target` is supported for backward compatibility but profile-based execution is recommended.


# Version Numbers

The version numbers generated by the build utilities are determined by the `version.json` file, the build counter, the build metadata, and the branch names. The version numbers use the following format:

| Version | MSBuild Property Name | Format | Conditions | Example |
| ------- | --------------------- | ------ | ---------- | ------- |
| Major Version | `MajorVersion` | `MAJOR` | | `1` |
| Minor Version | `MinorVersion` | `MINOR` | | `3` |
| Patch Version | `PatchVersion` | `PATCH` | | `2` |
| Assembly Version | `AssemblyVersion` | `MAJOR.MINOR.0.0` | | `1.3.0.0` |
| File Version | `FileVersion` | `MAJOR.MINOR.PATCH.BUILD` | Build counter >= 0 | `1.3.2.78` |
| | | `MAJOR.MINOR.PATCH.0` | Build counter < 0 | `1.3.2.0` |
| Informational Version | `InformationalVersion` | `MAJOR.MINOR.PATCH[-SUFFIX].[BUILD]+BRANCH[.METADATA]` | | `1.3.2-alpha.78+main.unofficial` |
| Package Version | `Version` | `MAJOR.MINOR.PATCH[-SUFFIX].[BUILD]` | Build counter >= 0 | `1.3.2-alpha.78` |
| | | `MAJOR.MINOR.PATCH[-SUFFIX]` | Build counter < 0 | `1.3.2-alpha` |
| Build Number | N/A | `MAJOR.MINOR.PATCH[-SUFFIX].[BUILD]+BRANCH` | Build counter >= 0 | `1.3.2-alpha.78+main` |
| | | `MAJOR.MINOR.PATCH[-SUFFIX]+BRANCH` | Build counter < 0 | `1.3.2-alpha+main` |


# Publishing Container Images

If your solution contains one or more projects that you want to publish container images for when the `PublishContainer` target is specified, you must include the names of the projects (without the project file extension) when calling the `Bootstrap()` method in your `build.cake` file. For example:

```cake
Bootstrap(
    DefaultSolutionFile,
    VersionFile,
    containerProjects: new [] {
        "MyFirstContainerProject",
        "MySecondContainerProject"
    });
```

Note that projects that publish container images can also define a `.pubxml` publish profile for publishing the image via the `Publish` target instead.


## Customising Container Images

Container images are published using the [.NET SDK container building tools](https://github.com/dotnet/sdk-container-builds). As such, it is possible to customise the container images using MSBuild properties. For example, additional tags that use the major and minor versions of the application (provided to MSBuild by the recipe script) could be configured as follows:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <!-- Other project settings removed for brevity. -->

  <PropertyGroup>
    <MajorVersion Condition=" '$(MajorVersion)' == '' ">1</MajorVersion>
    <MinorVersion Condition=" '$(MinorVersion)' == '' ">0</MinorVersion>
  </PropertyGroup>

  <PropertyGroup>
    <ContainerImageTags>latest;$(MajorVersion);$(MajorVersion).$(MinorVersion)</ContainerImageTags>
  </PropertyGroup>

</Project>
```


# Additional Documentation

Additional documentation is available on [GitHub](https://github.com/wazzamatazz/cake-recipes).
