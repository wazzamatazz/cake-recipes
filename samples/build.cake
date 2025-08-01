///////////////////////////////////////////////////////////////////////////////////////////////////
// Use build.ps1 or build.sh to run the build script with a profile. Examples:
//   ./build.sh test                      - Run development build with tests
//   ./build.sh dev                       - Fast development build without tests
//   ./build.sh release                   - Complete release build with all components
//   ./build.sh release --packages=false  - Release build without NuGet packages
//   ./build.sh release --containers=false - Release build without containers
//   ./build.sh release --ci --sign-output - CI release build with signing
///////////////////////////////////////////////////////////////////////////////////////////////////

const string DefaultSolutionFile = "./Example.sln";
const string VersionFile = "./version.json";

///////////////////////////////////////////////////////////////////////////////////////////////////
// AVAILABLE BUILD PROFILES:
//
// test
//   Description: Runs a standard development build with tests
//   Tasks: Restore → Build → Test
//   Options: --configuration, --clean, --no-tests
//
// dev
//   Description: Fast development build without tests
//   Tasks: Restore → Build
//   Options: --configuration, --clean
//
// pack
//   Description: Creates NuGet packages after building and testing
//   Tasks: Restore → Build → Test → Pack
//   Options: --configuration, --clean, --no-tests
//
// containers
//   Description: Builds and publishes container images
//   Tasks: Restore → Build → Test → PublishContainer
//   Options: --configuration, --clean, --no-tests, --container-registry
//
// release
//   Description: Complete release build with configurable components
//   Tasks: Clean → Restore → Build → Test → Pack → PublishContainer → BillOfMaterials
//   Options: --configuration, --clean, --no-tests, --packages, --containers, --sbom,
//            --ci, --sign-output, --container-registry, --build-counter, --build-metadata,
//            --github-username, --github-token (both required for SBOM)
//
///////////////////////////////////////////////////////////////////////////////////////////////////
// COMMON OPTIONS:
//
// --configuration=<CONFIGURATION>
//   The MSBuild configuration to use (Debug or Release)
//
// --clean
//   Perform a clean rebuild
//
// --no-tests
//   Skip unit tests
//
// RELEASE PROFILE COMPONENT TOGGLES:
//
// --packages <true|false>
//   Enable/disable NuGet package creation (release profile only)
//   Default: true
//
// --containers <true|false>
//   Enable/disable container image publishing (release profile only)  
//   Default: true
//
// --sbom <true|false>
//   Enable/disable Software Bill of Materials generation (release profile only)
//   Default: true
//   Requires: --github-username and --github-token (both required together)
//
// --ci
//   Enable continuous integration mode (release profile only)
//   Default: false
//
// --sign-output  
//   Enable output signing (release profile only)
//   Default: false
//
// OTHER OPTIONS:
//
// --container-registry=<REGISTRY>
//   Container registry for publishing images
//
// --build-counter=<COUNTER>
//   Build counter for versioning
//
// --build-metadata=<METADATA>
//   Additional build metadata
//
// --github-username=<USERNAME> --github-token=<TOKEN>
//   GitHub credentials for SBOM generation (both required together)
//
///////////////////////////////////////////////////////////////////////////////////////////////////

#load ../src/Jaahas.Cake.Extensions/content/build-utilities.cake

// Bootstrap build context and tasks.
Bootstrap(DefaultSolutionFile, VersionFile, containerProjects: new [] {
    "ExampleApp"
});

Task("ErrorTest")
    .Does<BuildState>(state => {
        throw new NotImplementedException();
    });

// Run the provided target.
Run();
