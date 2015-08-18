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

// start build
RunTargetOrDefault "All"