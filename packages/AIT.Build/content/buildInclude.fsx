// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------

(*
    This file handles the complete build process.

    The first step is handled in build.sh and build.cmd by bootstrapping a NuGet.exe and 
    executing NuGet to resolve all build dependencies (dependencies required for the build to work, for example FAKE)

    The secound step is executing this file which resolves all dependencies, builds the solution and executes all unit tests
*)

#if FAKE
#else
// Support when file is opened in Visual Studio
#load "buildConfigDef.fsx"
#load "../../../buildConfig.fsx"
#endif

open BuildConfigDef
let config = BuildConfig.buildConfig.FillDefaults ()

// NOTE: We want to add that to buildConfigDef.fsx sometimes in the future
// #I @"../../FSharp.Compiler.Service/lib/net40/" // included in FAKE, but to be able to use the latest
// Bundled
//#I @"../../Yaaf.FSharp.Scripting/lib/net40/"
#I "../tools/"
#r "AIT.Build.dll"


open Yaaf.AdvancedBuilding
open System.IO
open System

open Fake
open Fake.MSTest
open Fake.Git
open Fake.FSharpFormatting
open AssemblyInfoFile

let readLine msg def =
  if isLocalBuild then
    printf "%s" msg
    System.Console.ReadLine()
  else
    printf "%s%s" msg def
    def

if config.UseNuget then
    // Ensure the ./src/.nuget/NuGet.exe file exists (required by xbuild)
    let nuget = findToolInSubPath "NuGet.exe" (config.GlobalPackagesDir @@ "NuGet.CommandLine/tools/NuGet.exe")
    if Directory.Exists "./src/.nuget" then
      File.Copy(nuget, "./src/.nuget/NuGet.exe", true)
    else
      failwith "you set UseNuget to true but there is no \"./src/.nuget/NuGet.targets\" or \"./src/.nuget/NuGet.Config\"! Please copy them from ./packages/Yaaf.AdvancedBuilding/scaffold/nuget"

let createMissingSymbolFiles assembly =
  try
    match File.Exists (Path.ChangeExtension(assembly, "pdb")), File.Exists (assembly + ".mdb") with
    | true, false when not isLinux ->
      // create mdb
      trace (sprintf "Creating mdb for %s" assembly)
      DebugSymbolHelper.writeMdbFromPdb assembly
    | true, false ->
      trace (sprintf "Cannot create mdb for %s because we are not on windows :(" assembly) 
    | false, true when not isLinux ->
      // create pdb
      trace (sprintf "Creating pdb for %s" assembly)
      DebugSymbolHelper.writePdbFromMdb assembly
    | false, true ->
      trace (sprintf "Cannot create pdb for %s because we are not on windows :(" assembly) 
    | _, _ -> 
      // either no debug symbols available or already both.
      ()
  with exn -> traceError (sprintf "Error creating symbols: %s" exn.Message)


let buildWithFiles msg dir projectFileFinder (buildParams:BuildParams) =
    let files = projectFileFinder buildParams |> Seq.toList
    let buildDir = 
        if not buildParams.UseProjectOutDir then
            let buildDir = dir @@ buildParams.SimpleBuildName
            CleanDirs [ buildDir ]
            buildDir
        else 
            files
                |> MSBuild null "clean"
                    [ "Configuration", buildParams.BuildMode
                      "Platform", buildParams.PlatformName ] 
                |> Log "Cleaning: "
            null
    // build app
    files
        |> MSBuild buildDir "Build"
            [ "Configuration", buildParams.BuildMode
              "Platform", buildParams.PlatformName ]
        |> Log msg
        
let buildSolution = buildWithFiles "BuildSolution-Output: " config.BuildDir (fun buildParams -> buildParams.FindSolutionFiles buildParams)
let buildApp = buildWithFiles "AppBuild-Output: " config.BuildDir (fun buildParams -> buildParams.FindProjectFiles buildParams)
let buildTests = buildWithFiles "TestBuild-Output: " config.TestDir (fun buildParams -> buildParams.FindTestFiles buildParams)

let runTests (buildParams:BuildParams) =
    let testDir = config.TestDir @@ buildParams.SimpleBuildName
    let logs = System.IO.Path.Combine(testDir, "logs")
    System.IO.Directory.CreateDirectory(logs) |> ignore
    let files = buildParams.FindUnitTestDlls (testDir, buildParams)
    if files |> Seq.isEmpty then
      traceError (sprintf "NO test found in %s" testDir)
    else
      files
        |> NUnit (fun p ->
            {p with
                //NUnitParams.WorkingDir = working
                //ExcludeCategory = if isMono then "VBNET" else ""
                ProcessModel =
                    // Because the default nunit-console.exe.config doesn't use .net 4...
                    if isMono then NUnitProcessModel.SingleProcessModel else NUnitProcessModel.DefaultProcessModel
                WorkingDir = testDir
                StopOnError = true
                TimeOut = System.TimeSpan.FromMinutes 30.0
                Framework = "4.0"
                DisableShadowCopy = true;
                OutputFile = "logs/TestResults.xml" } |> config.SetupNUnit)
      if not isLinux then
        files
          |> MSTest (fun p ->
              {p with
                  WorkingDir = testDir
                  ResultsDir = "logs" } |> config.SetupMSTest)

let buildAll (buildParams:BuildParams) =
    buildParams.BeforeBuild ()
    buildSolution buildParams
    buildApp buildParams
    buildTests buildParams
    buildParams.AfterBuild ()
    runTests buildParams
    buildParams.AfterTest ()

/// Run the given buildscript with fsi.exe
let executeFSIWithOutput workingDirectory script args =
    let exitCode =
        ExecProcessWithLambdas
            (fsiStartInfo script workingDirectory args)
            TimeSpan.MaxValue false ignore ignore
    System.Threading.Thread.Sleep 1000
    exitCode

// Documentation
let buildDocumentationTarget target =
    trace (sprintf "Building documentation (%s), this could take some time, please wait..." target)
    let exit = executeFSIWithOutput "." "generateDocs.fsx" ["target", target]
    if exit <> 0 then
        failwith "documentation failed"
    ()

let tryDelete dir =
    try
        CleanDirs [ dir ]
    with
    | :? System.IO.IOException as e ->
        traceImportant (sprintf "Cannot access: %s\nTry closing Visual Studio!" e.Message)
    | :? System.UnauthorizedAccessException as e ->
        traceImportant (sprintf "Cannot access: %s\nTry closing Visual Studio!" e.Message)
    
let MyTarget name body =
    Target name (fun _ -> body false)
    let single = (sprintf "%s_single" name)
    Target single (fun _ -> body true)

// Targets
MyTarget "Clean" (fun _ ->
    tryDelete config.BuildDir
    tryDelete config.TestDir
    
    CleanDirs [ config.OutLibDir; config.OutDocDir; config.OutNugetDir ]
)

MyTarget "CleanAll" (fun _ ->
    // Only done when we want to redownload all.
    Directory.EnumerateDirectories config.GlobalPackagesDir
    |> Seq.filter (fun buildDepDir ->
        let buildDepName = Path.GetFileName buildDepDir
        // We can't delete the FAKE directory (as it is used currently)
        buildDepName <> "FAKE")
    |> Seq.iter (fun dir ->
        try
            DeleteDir dir
        with exn ->
            traceError (sprintf "Unable to delete %s: %O" dir exn))
)

MyTarget "RestorePackages" (fun _ ->
    // will catch src/targetsDependencies
    !! "./src/**/packages.config"
    |> Seq.iter 
        (RestorePackage (fun param ->
            { param with    
                // ToolPath = ""
                OutputPath = config.NugetPackageDir }))
)

MyTarget "CreateDebugFiles" (fun _ ->
    // creates .mdb from .pdb files and the other way around
    !! (config.GlobalPackagesDir + "/**/*.exe")
    ++ (config.GlobalPackagesDir + "/**/*.dll")
    |> Seq.iter createMissingSymbolFiles  
)

MyTarget "SetVersions" (fun _ -> 
    config.SetAssemblyFileVersions config
)

config.BuildTargets
    |> Seq.iter (fun buildParam -> 
        MyTarget (sprintf "Build_%s" buildParam.SimpleBuildName) (fun _ -> buildAll buildParam))

MyTarget "CopyToRelease" (fun _ ->
    trace "Copying to release because test was OK."
    let outLibDir = config.OutLibDir
    CleanDirs [ outLibDir ]
    Directory.CreateDirectory(outLibDir) |> ignore

    // Copy files to release directory
    config.BuildTargets
        |> Seq.map (fun buildParam -> buildParam.SimpleBuildName)
        |> Seq.map (fun t -> config.BuildDir @@ t, t)
        |> Seq.filter (fun (p, _) -> Directory.Exists p)
        |> Seq.iter (fun (source, buildName) ->
            let outDir = outLibDir @@ buildName
            ensureDirectory outDir
            config.GeneratedFileList
            |> Seq.collect (fun file ->
              let extension = (Path.GetExtension file).TrimStart('.')
              match extension with
              | "dll" | "exe" -> 
                [ file
                  Path.ChangeExtension(file, "pdb")
                  Path.ChangeExtension(file, extension + ".mdb" ) ]              
              | _ -> [ file ]
            )
            |> Seq.filter (fun (file) -> File.Exists (source @@ file))
            |> Seq.iter (fun (file) ->
                let sourceFile = source @@ file
                let newfile = outDir @@ Path.GetFileName file
                trace (sprintf "Copying %s to %s" sourceFile newfile)
                File.Copy(sourceFile, newfile))
        )
)

MyTarget "CreateReleaseSymbolFiles" (fun _ ->
    // creates .mdb from .pdb files and the other way around
    !! (config.OutLibDir + "/**/*.exe")
    ++ (config.OutLibDir + "/**/*.dll")
    |> Seq.iter createMissingSymbolFiles  
)

/// push package (and try again if something fails), FAKE Version doesn't work on mono
/// From https://raw.githubusercontent.com/fsharp/FAKE/master/src/app/FakeLib/NuGet/NugetHelper.fs
let rec private publish parameters =
    let replaceAccessKey key (text : string) =
        if isNullOrEmpty key then text
        else text.Replace(key, "PRIVATEKEY")
    let nuspec = sprintf "%s.%s.nupkg" parameters.Project parameters.Version
    traceStartTask "MyNuGetPublish" nuspec
    let tracing = enableProcessTracing
    enableProcessTracing <- false
    let source =
        if isNullOrEmpty parameters.PublishUrl then ""
        else sprintf "-s %s" parameters.PublishUrl

    let args = sprintf "push \"%s\" %s %s" (parameters.OutputPath @@ nuspec) parameters.AccessKey source
    tracefn "%s %s in WorkingDir: %s Trials left: %d" parameters.ToolPath (replaceAccessKey parameters.AccessKey args)
        (FullName parameters.WorkingDir) parameters.PublishTrials
    try
        let result =
            ExecProcess (fun info ->
                info.FileName <- parameters.ToolPath
                info.WorkingDirectory <- FullName parameters.WorkingDir
                info.Arguments <- args) parameters.TimeOut
        enableProcessTracing <- tracing
        if result <> 0 then failwithf "Error during NuGet push. %s %s" parameters.ToolPath args
    with exn ->
        if parameters.PublishTrials > 0 then publish { parameters with PublishTrials = parameters.PublishTrials - 1 }
        else
          (if exn.InnerException <> null then exn.Message + "\r\n" + exn.InnerException.Message
           else exn.Message)
          |> replaceAccessKey parameters.AccessKey
          |> failwith
    traceEndTask "MyNuGetPublish" nuspec

let packSetup config p =
  { p with
      Authors = config.ProjectAuthors
      Project = config.ProjectName
      Summary = config.ProjectSummary
      Version = config.Version
      Description = config.ProjectDescription
      Tags = config.NugetTags
      WorkingDir = "."
      OutputPath = config.OutNugetDir
      AccessKey = getBuildParamOrDefault "nugetkey" ""
      Publish = false
      Dependencies = [ ] }

MyTarget "NuGetPack" (fun _ ->
    ensureDirectory config.OutNugetDir
    for (nuspecFile, settingsFunc) in config.NugetPackages do
      let packSetup = packSetup config
      NuGet (fun p -> { (packSetup >> settingsFunc config) p with Publish = false }) (sprintf "nuget/%s" nuspecFile)
)

MyTarget "NuGetPush" (fun _ ->
    for (_, settingsFunc) in config.NugetPackages do
      let packSetup = packSetup config
      let parameters = NuGetDefaults() |> (fun p -> { packSetup p with Publish = true }) |> settingsFunc config
      // This allows us to specify packages which we do not want to push...
      if hasBuildParam "nugetkey" && parameters.Publish then publish parameters
)

// Documentation 
MyTarget "GithubDoc" (fun _ -> buildDocumentationTarget "GithubDoc")

MyTarget "LocalDoc" (fun _ -> buildDocumentationTarget "LocalDoc")

MyTarget "WatchDocs" (fun _ -> buildDocumentationTarget "WatchDocs")

// its just faster to generate all at the same time...
MyTarget "AllDocs" (fun _ -> buildDocumentationTarget "AllDocs")

MyTarget "ReleaseGithubDoc" (fun isSingle ->
    let repro = (sprintf "git@github.com:%s/%s.git" config.GithubUser config.GithubProject)
    let doAction =
        if isSingle then true
        else
            let msg = sprintf "update github docs to %s? (y,n): " repro
            let line = readLine msg "y"
            line = "y"
    if doAction then
        CleanDir "gh-pages"
        cloneSingleBranch "" repro "gh-pages" "gh-pages"
        fullclean "gh-pages"
        CopyRecursive ("release"@@"documentation"@@(sprintf "%s.github.io" config.GithubUser)@@"html") "gh-pages" true |> printfn "%A"
        StageAll "gh-pages"
        Commit "gh-pages" (sprintf "Update generated documentation %s" config.Version)
        let msg = sprintf "gh-pages branch updated in the gh-pages directory, push that branch to %s now? (y,n): " repro
        let line = readLine msg "y"
        if line = "y" then
            Branches.pushBranch "gh-pages" "origin" "gh-pages"
)

Target "All" (fun _ ->
    trace "All finished!"
)

Target "CheckWindows" (fun _ ->
    if isLinux then failwith "can only do releases on windows." 
)

MyTarget "VersionBump" (fun _ ->
    let doBranchUpdates = not isLocalBuild && (getBuildParamOrDefault "yaaf_merge_master" "false") = "true"
    if doBranchUpdates then
      // Make sure we are on develop (commit will fail otherwise)
      Stash.push "" ""
      try Branches.deleteBranch "" true "develop"
      with e -> trace (sprintf "deletion of develop branch failed %O" e)
      Branches.checkout "" true "develop"
      try Stash.pop ""
      with e -> trace (sprintf "stash pop failed %O" e)

    // Commit updates the SharedAssemblyInfo.cs files.
    let changedFiles = Fake.Git.FileStatus.getChangedFilesInWorkingCopy "" "HEAD" |> Seq.toList
    if changedFiles |> Seq.isEmpty |> not then
        for (status, file) in changedFiles do
            printfn "File %s changed (%A)" file status

        let line = readLine "version bump commit? (y,n): " "y"
        if line = "y" then
            StageAll ""
            Commit "" (sprintf "Bump version to %s" config.Version)

    if doBranchUpdates then
      try Branches.deleteBranch "" true "master"
      with e -> trace (sprintf "deletion of master branch failed %O" e)
      Branches.checkout "" false "origin/master"
      Branches.checkout "" true "master"
      Merge.merge "" NoFastForwardFlag "develop"

      Branches.pushBranch "" "origin" "master"
      //try Branches.deleteTag "" config.Version
      //with e -> trace (sprintf "deletion of tag %s failed %O" config.Version e)
      Branches.tag "" config.Version
      Branches.pushTag "" "origin" config.Version
      try Branches.deleteBranch "" true "develop"
      with e -> trace (sprintf "deletion of develop branch failed %O" e)
      Branches.checkout "" false "origin/develop"
      Branches.checkout "" true "develop"
      Merge.merge "" NoFastForwardFlag "master"
      Branches.pushBranch "" "origin" "develop"
)

Target "Release" (fun _ ->
    trace "All released!"
)

Target "ReadyForBuild" ignore
Target "AfterBuild" ignore

// Clean all
"Clean" 
  ==> "CleanAll"
"Clean_single" 
  ==> "CleanAll_single"

"Clean"
  =?> ("RestorePackages", config.UseNuget)
  =?> ("CreateDebugFiles", config.EnableDebugSymbolConversion)
  ==> "SetVersions"
  ==> "ReadyForBuild"

config.BuildTargets
    |> Seq.iter (fun buildParam ->
        let buildName = sprintf "Build_%s" buildParam.SimpleBuildName
        "ReadyForBuild"
          ==> buildName
          |> ignore
        buildName
          ==> "AfterBuild"
          |> ignore
    )

// Dependencies
"AfterBuild" 
  ==> "CopyToRelease"
  =?> ("CreateReleaseSymbolFiles", config.EnableDebugSymbolConversion)
  ==> "NuGetPack"
  =?> ("AllDocs", config.BuildDocumentation)
  ==> "All"
 
"All"
  =?> ("CheckWindows", config.RestrictReleaseToWindows)
  ==> "VersionBump"
  =?> ("ReleaseGithubDoc", config.EnableGithub)
  ==> "NuGetPush"
  ==> "Release"

