// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
(**

# AIT.VisualStudioTextTransform buildConfig.fsx configuration
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

// Used by the TestEnv.cs
setEnvironVar "PROJECT_DIRECTORY" (Path.GetFullPath ".")

(**
## Main project configuration

Then we need to set some general properties of the project.
*)
let buildConfig =
 // Read release notes document
 let release = ReleaseNotesHelper.parseReleaseNotes (File.ReadLines "doc/ReleaseNotes.md")
 { BuildConfiguration.Defaults with
    ProjectName = "AIT.Tools.VisualStudioTextTransform"
    CopyrightNotice = "Copyright © 2015, AIT GmbH & Co. KG"
    ProjectSummary = "AIT.Tools.VisualStudioTextTransform can automatically transform T4 templates from a given solution."
    ProjectDescription =
      "AIT.Tools.VisualStudioTextTransform can automatically transform T4 templates from a given solution."
    ProjectAuthors = ["AIT GmbH"]
    NugetTags = "building C# F# dotnet .net"
    PageAuthor = "AIT GmbH"
    GithubUser = "AITGmbH"
    // Defaults to ProjectName if unset
    GithubProject = "VisualStudioTextTransform"
    Version = release.NugetVersion
(**
Setup which nuget packages are created.
*)
    NugetPackages =
      [ "AIT.Tools.VisualStudioTextTransform.nuspec", (fun config p ->
          { p with
              Version = config.Version
              NoDefaultExcludes = true
              ReleaseNotes = toLines release.Notes
              Dependencies =
                [ ]
                  |> List.map (fun name -> name, (GetPackageVersion "packages" name |> RequireExactly)) } ) ]
(**
With `UseNuget` you can specify if AIT.Build should restore nuget packages
before running the build (if you only use paket, you either leave it out or use the default setting = false).
*)
    // We must restore to get a Razor3 and Razor2 (paket can only handle one)
    UseNuget = true
    SetupMSTest = fun p ->
      {p with
          TestSettingsPath = @"..\..\..\src\settings.testsettings" }
(**
## The `GeneratedFileList` property

The `GeneratedFileList` list is used to specify which files are copied over to the release directory.
This list is also used for documentation generation.
Defaults to [ x.ProjectName + ".dll"; x.ProjectName + ".xml" ] which is only enough for very simple projects.
*)
    GeneratedFileList =
     [ "AIT.Tools.VisualStudioTextTransform.exe"; "AIT.Tools.VisualStudioTextTransform.exe.xml" ]

(**
You can change which AssemblyInfo files are generated for you.
On default "./src/SharedAssemblyInfo.fs" and "./src/SharedAssemblyInfo.cs" are created.
*)
    SetAssemblyFileVersions = (fun config ->
      let info =
        [ Attribute.Company "AIT GmbH & Co. KG"
          Attribute.Product config.ProjectName
          Attribute.Copyright config.CopyrightNotice
          Attribute.Version config.Version
          Attribute.FileVersion config.Version
          Attribute.InformationalVersion config.Version]
      CreateCSharpAssemblyInfo "./src/GlobalAssemblyInfo.cs" info)
(**
## Yaaf.AdvancedBuilding features
Setup the builds
*)
    BuildTargets =
     [ { BuildParams.WithSolution with
          // The net404 client build
          PlatformName = "Net45"
          SimpleBuildName = "net45" }]

    EnableDebugSymbolConversion = false
    RestrictReleaseToWindows = true
  }

(**
## FAKE settings

You can setup FAKE variables as well.
*)

if isMono then
    monoArguments <- "--runtime=v4.0 --debug"

