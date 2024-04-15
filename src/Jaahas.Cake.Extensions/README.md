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

#load nuget:?package=Jaahas.Cake.Extensions&version=2.0.1

// Bootstrap build context and tasks.
Bootstrap(DefaultSolutionFile, VersionFile);

// Run the specified target.
Run();
```

Additional documentation is available on [GitHub](https://github.com/wazzamatazz/cake-recipes).
