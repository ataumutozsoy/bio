# The .NET Bio testing framework.

.NET Bio uses [NUnit](https://nunit.org/), patches should come with tests and all tests should pass before a commit or branch is merged. The test projects target `net8.0` and `net10.0` and can be run from the Test Explorer in Visual Studio or Rider, or from the command line.

### Command line

Run all the unit tests on every target framework from the repository root:

```
dotnet test DotNetBio.sln -c Release
```

To run a single test project on one framework, or a subset of tests:

```
dotnet test Tests/Bio.Tests -f net10.0
dotnet test Tests/Bio.Tests --filter "FullyQualifiedName~GenBank"
```

## Test Data

All of the projects testing data is in a shared project located at  `Tests/TestData/TestUtils`.  New data files used in testing should be placed here. All of this data will be automatically copied by the build process into the output directory of `Bio.Tests.dll`.  