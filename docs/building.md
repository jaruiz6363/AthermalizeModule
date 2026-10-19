# Building

How to build AthermalizeModule from a clone, run its tests, and get an `atherm` executable to use outside the repository.

## What you need

- **The .NET SDK, version 8 or later.** The projects target .NET 8. They have been built with the .NET 9.0.302 SDK and with Visual Studio 2022's MSBuild.
- **git**, to clone. AberrationCalculator and WavefrontErrorCalculator are carried in `external/` as git subtrees, so a plain clone has everything; there are no submodules to fetch.
- **Optional: an embedded Python with Optiland,** for the cross-check against Optiland (see [Testing](#testing)). Nothing else needs it.

The module is developed and tested on Windows. It has no Windows-only code apart from the Optiland setup script, and .NET runs on Linux and macOS, but it has not been tested there.

## Cloning

```
git clone https://github.com/jaruiz6363/AthermalizeModule.git
```

**On Windows, mind the path length.** The subtrees nest deep, so a clone into a folder with a long path can pass Windows' 260-character limit. The build then stops with an error like:

```
error MSB4184: ... exceeds the OS max path limit. The fully qualified file name must be less than 260 characters.
```

Clone to a short path, such as `C:\src`, or run `git config --global core.longpaths true` before cloning.

## Building

From the repository's root:

```
dotnet build
```

This builds the library (`src/Athermalize.Core`), the command-line tool (`src/Athermalize.Cli`, whose program is `atherm`), the tests, and the parts of AberrationCalculator and WavefrontErrorCalculator they use. The glass catalogs are copied next to `atherm`.

To run the tool from the repository without installing it:

```
dotnet run --project src/Athermalize.Cli -- materials N-BK7
```

Everything after `--` goes to `atherm`.

**Visual Studio:** open `AthermalizeModule.sln` in Visual Studio 2022 (17.8 or later, for .NET 8) and build. Every project the tests and the tool reference is in the solution, under `external`, so a fresh clone builds there as it does with `dotnet build`.

## Testing

```
dotnet test
```

runs every test, about 270, in under a minute. [verification.md](verification.md) says what each group checks.

**The Optiland cross-check.** Three tests compare the module's index over temperature with Optiland's ([verification.md](verification.md#10-optiland)). They need Python with Optiland, run in process. To install it, from the repository's root in PowerShell:

```
.\tools\setup-python.ps1
```

This downloads an embeddable Python 3.12, installs Optiland into `python-embed\` (which git ignores), and takes a few minutes. Without it, `dotnet test` reports the three tests as skipped, with the reason, and the rest pass. To use another Python that has Optiland installed, set `ABCALC_PYTHON_HOME` to its folder.

Some useful filters:

```
dotnet test --filter "FullyQualifiedName~OptilandTests"      # only the Optiland cross-check
dotnet test --filter "FullyQualifiedName!~OptilandTests"     # everything else
dotnet test --filter "FullyQualifiedName~WorkedExampleTests" # one test class
```

## An `atherm` to use anywhere

`dotnet publish` puts the tool and its glass catalogs in one folder, which can then go anywhere:

```
dotnet publish src/Athermalize.Cli -c Release -o C:\tools\atherm
```

That folder (about 5 MB) needs the .NET 8 runtime on the machine. For a folder that needs no .NET installed (about 76 MB), name the platform:

```
dotnet publish src/Athermalize.Cli -c Release -r win-x64 --self-contained -o C:\tools\atherm
```

Use `linux-x64` or `osx-arm64` and so on for other platforms. Add the folder to your `PATH` to run `atherm` from any directory.

**The glass catalogs** are in the folder's `catalogs\Glass`, and `atherm` reads them from beside itself. To use other catalogs, such as a maker's newer edition, give their folder with `--catalogs <dir>`; it reads every `.agf` file there.

## What the build makes

| Project | What it is |
|---|---|
| `src/Athermalize.Core` | The library: materials, the lens at a temperature, the analyses |
| `src/Athermalize.Cli` | The command-line tool, `atherm` |
| `tests/Athermalize.Tests` | The tests |
| `external/AberrationCalculator` | The lens model, lens-file and catalog readers, ray tracing, and the bridge to Optiland used by the tests |
| `external/WavefrontErrorCalculator` | The wavefront error and the MTF |

The two libraries in `external/` are their own repositories, carried as git subtrees. They are updated by pulling a newer version into the subtree, not by editing them here.
