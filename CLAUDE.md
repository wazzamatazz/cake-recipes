# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a Cake build system recipe package called `Jaahas.Cake.Extensions`. It provides common build utilities and tasks for .NET projects using the Cake build automation tool.

## Architecture

- **Main Package**: `src/Jaahas.Cake.Extensions/` - Contains the NuGet package with Cake build utilities
- **Sample Project**: `samples/` - Contains an example implementation showing how to use the build utilities
- **Content Files**: `src/Jaahas.Cake.Extensions/content/` - Contains the core Cake scripts:
  - `build-utilities.cake` - Main bootstrapping and utility functions
  - `build-state.cake` - Build state management with profile support
  - `task-definitions.cake` - Task definitions for various build targets
  - `profile-definitions.cake` - Build profile definitions and registry
  - `profile-execution.cake` - Profile execution engine for task orchestration

## Build Commands

The build system uses profile-based execution with Cake build scripts:

### Running Builds with Profiles
```bash
# Linux/macOS
./build.sh <profile> [options]

# Windows  
.\build.ps1 <profile> [options]

# Examples
./build.sh test                        # Development build with tests
./build.sh dev                         # Fast build without tests  
./build.sh release                     # Complete release build (all components)
./build.sh release --packages=false    # Release without NuGet packages
./build.sh release --containers=false  # Release without containers
./build.sh release --ci --sign-output  # CI release with signing
```

### Available Profiles
- **`test`** - Standard development build: Restore → Build → Test
- **`dev`** - Fast development build: Restore → Build (no tests)
- **`pack`** - Package build: Restore → Build → Test → Pack
- **`containers`** - Container build: Restore → Build → Test → PublishContainer
- **`release`** - Configurable release build: Clean → Restore → Build → Test → Pack → PublishContainer → BillOfMaterials

### Release Profile Component Control
The release profile supports component toggles for flexible builds:
- `--packages=false` - Skip NuGet package creation
- `--containers=false` - Skip container image publishing
- `--sbom=false` - Skip Software Bill of Materials generation
- `--ci` - Enable continuous integration mode
- `--sign-output` - Enable output signing

### Common Build Options
- `--configuration=<CONFIG>` - Build configuration (Debug/Release, auto-selected per profile)
- `--clean` - Force clean rebuild
- `--no-tests` - Skip unit tests
- `--container-registry=<URL>` - Container registry for publishing
- `--build-counter=<NUMBER>` - Build counter for versioning
- `--github-username=<USER> --github-token=<TOKEN>` - GitHub credentials for SBOM (both required together)

### Legacy Target Support
The system maintains backward compatibility with `--target=<TARGET>` syntax, automatically mapping to appropriate profiles.

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