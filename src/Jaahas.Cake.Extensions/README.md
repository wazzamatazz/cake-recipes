# About

Jaahas.Cake.Extensions is a recipe package for [Cake](https://cakebuild.net), used to provide a common way of versioning and running builds using Cake.

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

#load nuget:?package=Jaahas.Cake.Extensions&version=2.1.0

// Bootstrap build context and tasks.
Bootstrap(DefaultSolutionFile, VersionFile);

// Run the specified target.
Run();
```

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

# Additional Documentation

Additional documentation is available on [GitHub](https://github.com/wazzamatazz/cake-recipes).
