// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
#load "packages/AIT.Build/content/buildConfigDef.fsx"
#load @"buildConfig.fsx"
#load "packages/AIT.Build/content/generateDocsInclude.fsx"
open Fake
RunTargetOrDefault "LocalDoc"