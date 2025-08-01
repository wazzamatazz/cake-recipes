# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a Cake build system recipe package called `Jaahas.Cake.Extensions`. It provides common build utilities and tasks for .NET projects using the Cake build automation tool.

## Architecture

- **Main Package**: `src/Jaahas.Cake.Extensions/` - Contains the NuGet package with Cake build utilities
- **Sample Project**: `samples/` - Contains an example implementation showing how to use the build utilities
- **Content Files**: `src/Jaahas.Cake.Extensions/content/` - Contains the core Cake scripts:
  - `build-utilities.cake` - Main bootstrapping and utility functions
  - `build-state.cake` - Build state management
  - `task-definitions.cake` - Task definitions for various build targets

## Build Commands

The build system uses Cake with cross-platform build scripts:

### Running Builds
```bash
# Linux/macOS
./build.sh --target=<TARGET> --configuration=<CONFIG>

# Windows
.\build.ps1 --target=<TARGET> --configuration=<CONFIG>
```

### Available Targets
- `Clean` - Cleans build outputs
- `Restore` - Restores NuGet packages  
- `Build` - Builds the solution
- `Test` - Runs unit tests (default target)
- `Pack` - Creates NuGet packages
- `Publish` - Publishes projects with publish profiles
- `PublishContainer` - Publishes container images
- `BillOfMaterials` - Generates SBOM using CycloneDX

### Common Build Arguments
- `--target=<TARGET>` - Specify build target (default: Test)
- `--configuration=<CONFIG>` - Build configuration (default: Debug for most targets, Release for Pack/Publish)
- `--clean` - Perform clean rebuild
- `--no-tests` - Skip unit tests
- `--ci` - Enable CI build mode
- `--build-counter=<NUMBER>` - Set build counter for versioning

## Version Management

Projects use a `version.json` file structure:
```json
{
  "Major": 1,
  "Minor": 0, 
  "Patch": 0,
  "PreRelease": ""
}
```

Version numbers are automatically calculated based on this file, build counter, and branch information.

## Container Projects

When working with container projects, specify them in the `Bootstrap()` call in `build.cake`:
```cake
Bootstrap(
    DefaultSolutionFile,
    VersionFile,
    containerProjects: new [] {
        "ProjectName"
    });
```

## Development Notes

- The package targets .NET Standard 2.0 for broad compatibility
- Build scripts automatically detect Git branch names for versioning
- The build system integrates with TeamCity for CI/CD
- All build scripts restore dotnet tools automatically before running Cake