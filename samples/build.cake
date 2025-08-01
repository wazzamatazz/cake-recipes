///////////////////////////////////////////////////////////////////////////////////////////////////
// Use build.ps1 or build.sh to run the build script with a profile. Examples:
//   ./build.sh test        - Run development build with tests
//   ./build.sh dev         - Fast development build without tests
//   ./build.sh release     - Complete release build with all artifacts
//   ./build.sh containers  - Build and publish container images
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
//   Options: --configuration, --clean, --no-tests, --container-registry, --container-os, --container-arch
//
// release
//   Description: Complete release build with all artifacts
//   Tasks: Clean → Restore → Build → Test → Pack → PublishContainer → BillOfMaterials
//   Options: --configuration, --no-tests, --sbom, --container-registry, --container-os, --container-arch,
//            --github-username, --github-token, --build-counter, --build-metadata
//
// ci
//   Description: Continuous integration build profile
//   Tasks: Clean → Restore → Build → Test → Pack → BillOfMaterials
//   Options: --configuration, --no-tests, --sbom, --github-username, --github-token,
//            --build-counter, --build-metadata, --sign-output
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
// --sbom <true|false>
//   Enable/disable Software Bill of Materials generation (release/ci profiles only)
//   Default: true for release/ci profiles
//
// --container-registry=<REGISTRY>
//   Container registry for publishing images
//
// --container-os=<OS> --container-arch=<ARCH>
//   Container target platform (e.g., linux, amd64)
//
// --build-counter=<COUNTER>
//   Build counter for versioning
//
// --build-metadata=<METADATA>
//   Additional build metadata
//
// --github-username=<USERNAME> --github-token=<TOKEN>
//   GitHub credentials for enhanced SBOM generation
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
