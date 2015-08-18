// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
(**

# AIT.VisualStudioTextTransform buildConfig.fsx configuration

This file handles the configuration of the AIT.Build build script.

The first step is handled in `build.sh` and `build.cmd` by restoring either paket dependencies or bootstrapping a NuGet.exe and
executing NuGet to resolve all build dependencies (dependencies required for the build to work, for example FAKE).
For details of paket look into http://fsprojects.github.io/Paket/ .

To be able to update `build.sh` and `build.cmd` with Yaaf.AdvancedBuilding the `build.sh` and `build.cmd` files in the project root directory delegate to
`packages/AIT.Build/content/build.sh` and `packages/AIT.Build/content/build.cmd`, which do the actual work.

When using NuGet instead of paket (or when using both), nuget packages from a `packages.config` file are restored.
This only works if there is a NuGet.exe found, you can just drop the `downloadNuget.fsx` file in your project root and Yaaf.AdvancedBuilding will make sure to
bootstrap a NuGet.exe into `packages/Nuget.CommandLine`.


The second step is to invoke FAKE with `build.fsx` which loads the current `buildConfig.fsx` file and delegates the work to
`packages/AIT.Build/content/buildInclude.fsx`. For details about FAKE look into http://fsharp.github.com/FAKE .

In the remainder of this document we will explain the various configuration options for AIT.Build.

*)

#if FAKE
#else
// Support when file is opened in Visual Studio
#load "packages/AIT.Build/content/buildConfigDef.fsx"
#endif

(**
## Required config start

First We need to load some dependencies and open some namespaces.
*)
open BuildConfigDef
open System.Collections.Generic
open System.IO

open Fake
open Fake.Git
open Fake.FSharpFormatting
open AssemblyInfoFile

(**
## Main project configuration

Then we need to set some general properties of the project.
*)
let buildConfig =
 // Read release notes document
 let release = ReleaseNotesHelper.parseReleaseNotes (File.ReadLines "doc/ReleaseNotes.md")
 { BuildConfiguration.Defaults with
    ProjectName = "AIT.KraussMaffei.CreateMC6Package"
    CopyrightNotice = "AIT.KrausMaffei.CreateMC6Package Copyright © AITGmbH 2015"
    ProjectSummary = "AIT.KrausMaffei.CreateMC6Package provides several build scripts for you to use."
    ProjectDescription =
      "AIT.Build is a loose collection of build scripts. "
    ProjectAuthors = ["AIT GmbH"]
    // Defaults to "https://www.nuget.org/packages/%(ProjectName)/"
    //NugetUrl = "https://www.nuget.org/packages/AIT.Build/"
    NugetTags = "building C# F# dotnet .net"
    PageAuthor = "AIT GmbH"
    GithubUser = "matthid"
    BuildDocumentation = false
    // Defaults to ProjectName if unset
    // GithubProject = "AIT.Build"
    Version = release.NugetVersion
(**
Setup which nuget packages are created.
*)
    NugetPackages =
      [ "AIT.KrausMaffei.CreateMC6Package.nuspec", (fun config p ->
          { p with
              Version = config.Version
              NoDefaultExcludes = true
              ReleaseNotes = toLines release.Notes
              Dependencies =
                [ "FSharp.Formatting"
                  // "FSharp.Compiler.Service" included in FAKE
                  "FSharpVSPowerTools.Core"
                  // "Mono.Cecil" included in FAKE
                  "FAKE" ]
                  |> List.map (fun name -> name, (GetPackageVersion "packages" name |> RequireExactly)) } ) ]
(**
With `UseNuget` you can specify if Yaaf.AdvancedBuilding should restore nuget packages
before running the build (if you only use paket, you either leave it out or use the default setting = false).
*)
    // We must restore to get a Razor3 and Razor2 (paket can only handle one)
    UseNuget = true
(**
## The `GeneratedFileList` property

The `GeneratedFileList` list is used to specify which files are copied over to the release directory.
This list is also used for documentation generation.
Defaults to [ x.ProjectName + ".dll"; x.ProjectName + ".xml" ] which is only enough for very simple projects.
*)
    GeneratedFileList =
     [ "AIT.KraussMaffei.CreateMC6Package.exe"; "AIT.KraussMaffei.CreateMC6Package.xml";
       "AIT.KraussMaffei.InstallerEntryPoint.dll"; "AIT.KraussMaffei.InstallerEntryPoint.xml";
       "AIT.KraussMaffei.InstallerUI.exe"; "AIT.KraussMaffei.InstallerUI.xml" ]

(**
You can change which AssemblyInfo files are generated for you.
On default "./src/SharedAssemblyInfo.fs" and "./src/SharedAssemblyInfo.cs" are created.
*)
    SetAssemblyFileVersions = (fun config ->
      let info =
        [ Attribute.Company config.ProjectName
          Attribute.Product config.ProjectName
          Attribute.Copyright config.CopyrightNotice
          Attribute.Version config.Version
          Attribute.FileVersion config.Version
          Attribute.InformationalVersion config.Version]
      CreateFSharpAssemblyInfo "./src/SharedAssemblyInfo.fs" info)
(**
## Yaaf.AdvancedBuilding features
Setup the builds
*)
    BuildTargets =
     [ { BuildParams.WithSolution with
          // The net40 client build
          PlatformName = "Net40"
          SimpleBuildName = "net40" }]
(**
enable mdb2pdb and pdb2mdb conversation for paket/nuget packages and your package.
Note that mdb2pdb only works on windows so to get a cross platform debugging experience you:
 - either include the pdb file and can therefore only release on a windows machine
 - include a mdb and tell your users to use mdb2pdb on windows.
*)
    EnableDebugSymbolConversion = false
(**
We include a mdb so we can actually release on linux.
*)
    RestrictReleaseToWindows = false
  }

(**
## FAKE settings

You can setup FAKE variables as well.
*)

if isMono then
    monoArguments <- "--runtime=v4.0 --debug"
    //monoArguments <- "--runtime=v4.0"

(**
## Remove ME!
This is specific to the AIT.Build project, you can safely remove everything below.
*)
if buildConfig.ProjectName = "AIT.Build" then
  if File.Exists "./buildConfig.fsx" then
    // We copy the buildConfig to ./doc so that F# formatting generates a html page from this file
    File.Copy ("./buildConfig.fsx", "./doc/buildConfig.fsx", true)
  // Copy templates to their normal path.
