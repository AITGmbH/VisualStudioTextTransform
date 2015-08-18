// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
#load "packages/AIT.Build/content/buildConfigDef.fsx"
#load @"buildConfig.fsx"
#load "packages/AIT.Build/content/buildInclude.fsx"

open Fake
let config = BuildInclude.config
// Define your FAKE targets here

let MyTarget = BuildInclude.MyTarget

MyTarget "CreateMC6Package" (fun _ ->
    let timeout = System.TimeSpan.FromSeconds(30.)
    let zipDir = ("release" @@ "zipPackage")
    CleanDir zipDir
    CreateDir zipDir
    
    // Copy other files & dependencies
    !! ("build/net40/*.exe")
    ++ ("build/net40/*.dll")
    ++ ("build/net40/*.config")
    -- ("build/net40/*Test*")
    |> Copy zipDir

    // Handle installer files.
    let installerDllName = "AIT.KraussMaffei.InstallerEntryPoint.dll"
    let entryDll = ("release" @@ "lib" @@ "net40" @@ installerDllName)

    //CopyFile zipDir entryDll
    // Copy not rename, because our installer needs to find the assembly at runtime.
    System.IO.File.Copy(zipDir @@ installerDllName, zipDir @@ "KM.SGM.Admin.dll")

    
    let installerUIName = "AIT.KraussMaffei.InstallerUI.exe"
    CreateDir zipDir
    let uiExe = ("release" @@ "lib" @@ "net40" @@ installerUIName)

    //CopyFile zipDir uiExe
    Rename (zipDir @@ "KM.MC6Installation.exe") (zipDir @@ installerUIName)
    Rename (zipDir @@ "KM.MC6Installation.exe.config") (zipDir @@ installerUIName + ".config")




    // Create the zip package
    let createPackageExecutable = "release" @@ "lib" @@ "net40" @@ "AIT.KraussMaffei.CreateMC6Package.exe"
    let zipPackage = "release" @@ "package.zip"
    let exitCode = 
        ExecProcess (fun startInfo ->
            startInfo.Arguments <- sprintf "zip \"%s\" \"%s\"" zipDir zipPackage
            startInfo.FileName <- createPackageExecutable)
            timeout
    if exitCode <> 0 then failwith "Creating the zip failed!"

    // Create the .dat package
    let exitCode = 
        ExecProcess (fun startInfo ->
            startInfo.Arguments <- sprintf "pack \"%s\" \"%s\"" zipPackage ("release" @@ "MC6Installation.dat")
            startInfo.FileName <- createPackageExecutable)
            timeout
    if exitCode <> 0 then failwith "Creating the .dat package failed!"

)

"CopyToRelease"
    ==> "CreateMC6Package"
    ==> "All"

// start build
RunTargetOrDefault "All"