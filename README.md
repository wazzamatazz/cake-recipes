# Jaahas.Cake.Extensions

`Jaahas.Cake.Extensions` is a recipe package for [Cake](https://cakebuild.net), used to provide a common way of versioning and running builds using Cake.


# Dependencies

`Jaahas.Cake.Extensions` assumes that you are using Cake v4.0.0 or later.


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

#load nuget:?package=Jaahas.Cake.Extensions&version=3.0.0

// Bootstrap build context and tasks.
Bootstrap(DefaultSolutionFile, VersionFile);

// Run the specified target.
Run();
```


# Command Line Arguments

The following command line arguments are supported by the recipe:

| Argument | Description | Default Value | Allowed Values |
| -------- | ----------- | ------------- | ---------------|
| `--branch=<FRIENDLY BRANCH NAME>` | The friendly name of the source control branch that is being built. Ignored for Git repositories. | | |
| `--project=<PROJECT OR SOLUTION>` | The MSBuild project or solution to build. | `DefaultSolutionFile` constant in `build.cake` file | |
| `--target=<TARGET>` | The Cake target to run. | `Test` | `Clean`, `Restore`, `Build`, `Test`, `Pack`, `Publish`, `PublishContainer`, `BillOfMaterials` |
| `--configuration=<CONFIGURATION>` | The MSBuild configuration to use. | `Debug`; `Release` when the `Pack`, `Publish` or `PublishContainer` target is specified | Any configuration defined in the MSBuild solution |
| `--clean` | Specifies that this is a rebuild rather than an incremental build. All artifact, bin, and test output folders will be cleaned prior to running the specified target. | | |
| `--no-tests` | Specifies that unit tests should be skipped, even if a target that depends on the `Test` target is specified. | | |
| `--ci` | Forces continuous integration build mode. Not required if the build is being run by a supported continuous integration build system. | | |
| `--sign-output` | Tells MSBuild that signing is required by setting the 'SignOutput' build property to 'True'. The signing implementation must be supplied by MSBuild. | | |
| `--build-counter=<COUNTER>` | The build counter. This is used when generating version numbers for the build. | -1 | |
| `--build-metadata=<METADATA>` | Additional build metadata that will be included in the information version number generated for compiled assemblies. | | |
| `--container-registry=<REGISTRY>` | The container registry to publish images to when calling the `PublishContainer` target. | Local Docker or Podman registry. | Any valid registry address. Registry authentication is managed by Docker or Podman. See [here](https://github.com/dotnet/sdk-container-builds/blob/main/docs/RegistryAuthentication.md) for more information. |
| `--container-os=<OS>` | The container operating system to target. | `linux` | Any valid [.NET runtime identifier](https://learn.microsoft.com/en-us/dotnet/core/rid-catalog) operating system. |
| `--container-arch=<ARCHITECTURE>` | The container processor architecture to use. | The architecture for the operating system. | Any valid [.NET runtime identifier](https://learn.microsoft.com/en-us/dotnet/core/rid-catalog) processor architecture. |
| `--property=<PROPERTY>` | Specifies an additional property to pass to MSBuild. The value must be specified using a `<NAME>=<VALUE>` format e.g. `--property="NoWarn=CS1591"`. This argument can be specified multiple times. | | |
| `--github-username=<USERNAME>` | Specifies the GitHub username to use when making authenticated API calls to GitHub while running the `BillOfMaterials` task. You must specify the `--github-token` argument as well when specifying this argument. | |
| `--github-token=<PERSONAL ACCESS TOKEN>` | Specifies the GitHub personal access token to use when making authenticated API calls to GitHub while running the `BillOfMaterials` task. You must specify the `--github-username` argument as well when specifying this argument. | |


# Targets

The recipe supports the following targets (specified by the `--target` parameter passed to Cake):

| Target | Description | Default Build Configuration |
| ------ | ----------- | --------------------------- |
| `Clean` | Cleans the `obj`, `bin`, `artifacts` and `TestResults` folders for the repository. | Debug |
| `Restore` | Restores NuGet packages for the solution. | Debug |
| `Build` | Builds the solution. | Debug |
| `Test` | Runs unit tests for the solution. | Debug |
| `Pack` | Creates NuGet packages for the solution. | Release |
| `Publish` | Publishes any solution projects that define one or more `.pubxml` publish profiles under the project folder. | Release |
| `PublishContainer` | Publishes container images for any solution projects that are registered as container projects. [See below](#publishing-container-images) for more information. | Release |
| `BillOfMaterials` | Generates a Software Bill of Materials (SBOM) for the solution using [CycloneDX](https://github.com/CycloneDX/cyclonedx-dotnet). | Release |

The Default Build Configuration specifies the default MSBuild configuration used when specifying a target and the `--configuration` parameter is not specified.


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


# Build System Integration

The build utilities automatically set the build number and and version numbers in the following build systems:

* TeamCity
