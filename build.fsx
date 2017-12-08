// include Fake lib
#r @"packages\build\FAKE\tools\FakeLib.dll"
open System
open System.IO
open System.Text
open Fake


let solutionFiles = [
    "SampleInput"</>"SampleInput.sln"; 
    "SampleUsage"</>"SampleUsage.sln"]
let (^) = (<|)

// in a distant future this could be a Fake module, or we could provide this bundled with the LensGenerator
// just keep in mind that we ship the content of the lib folder (FSharp.Core.dll/Reference Assemblies)
// to keep it self-contained (i.e. build server does not required have reference assemblies installed)
module LensGenerationHelper =
    type LensGenerationParams = {
        ToolPath : string
        ShimReferenceAssemblies : bool

        ProjectName : string
        ProjectDirectory : string
        ProjectFileName : string
        // either use outputfilename for single file output or outputdir for multifile output
        // for multifileoutput you will probably also update the outputprojectfile
        OutputFileName : string option
        OutputDir : string option
        OutputProjectFile : string option
        ErrorFileName : string option // if not set, errors go to console (unless silent)

        OutputNamespace : string option
        SystemLibraries : string list
        AbsolutePathLibraries : string list
        ProjectLibraries : string list
        LibraryDirectory : string option
        BinaryLibariesInLibraryDirectory : string list
    }
    let LensGenerationDefaults = {
        ToolPath = (findToolFolderInSubPath "FSCSLensGenerator.exe" (currentDirectory </> "packages" </> "build" </> "FSCS-LensGenerator" </> "tools")) </> "FSCSLensGenerator.exe"
        ShimReferenceAssemblies = true

        ProjectName = "Project"
        ProjectDirectory = currentDirectory
        ProjectFileName = currentDirectory </> "Project.fsproj"
        
        OutputFileName = Some(currentDirectory </> "Lenses.fs")
        OutputDir = None
        OutputProjectFile = None
        ErrorFileName = None
        OutputNamespace = None
        
        SystemLibraries = []
        AbsolutePathLibraries = []
        ProjectLibraries = []
        LibraryDirectory = Some(currentDirectory </> "lib")
        BinaryLibariesInLibraryDirectory = []
    }

    let buildLensGenerationArgs param = 
        let quote arg = sprintf "\"%s\"" arg
        let appendOnce name value s = appendWithoutQuotes (sprintf "--%s %s" name (quote value)) s
        let appendMandatory = appendOnce // only for verbosity
        let appendOptional name optvalue s = 
            match optvalue with
            | None -> s
            | Some value -> appendOnce name value s
        let rec appendMultiple name listvalue s =
            match listvalue with
            | [] -> s
            | value::rest -> appendMultiple name rest (appendOnce name value s)
        let appendLibdir s =
            match param.LibraryDirectory with
            | None -> s
            | Some dir ->
                s 
                |> appendOnce "libdir" dir
                |> appendMultiple "binlib" param.BinaryLibariesInLibraryDirectory
        let appendSwitch name flag s =
            match flag with
            | false -> s
            | true -> 
                s |> appendWithoutQuotes (sprintf "--%s" name)

        StringBuilder()
        |> appendMandatory "projectname" param.ProjectName
        |> appendMandatory "projectdir" param.ProjectDirectory
        |> appendMandatory "projectfilename" param.ProjectFileName
        |> appendOptional "outfilename" param.OutputFileName
        |> appendOptional "outputnamespace" param.OutputNamespace
        |> appendOptional "outputdir" param.OutputDir
        |> appendOptional "outputprojectfile" param.OutputProjectFile
        |> appendOptional "errorfilename" param.ErrorFileName
        |> appendMultiple "syslib" param.SystemLibraries
        |> appendMultiple "abslib" param.AbsolutePathLibraries
        |> appendMultiple "projlib" param.ProjectLibraries
        |> appendLibdir
        |> appendSwitch "shimreferenceassemblies" param.ShimReferenceAssemblies
        |> toText

    let GenerateLenses (setParams : LensGenerationParams -> LensGenerationParams) = 
        let taskName = "FSCSLensGenerator"
        let description = "Generating lenses for F# Project"
        use __ = traceStartTaskUsing taskName description // log start/end
        let parameters = LensGenerationDefaults |> setParams
        let timeOut = TimeSpan.FromMinutes 5.0
        let processArgs = buildLensGenerationArgs parameters

        tracefn "FSCSLensGenerator command\n%s %s" parameters.ToolPath processArgs

        let updateProcStartInfo (i:Diagnostics.ProcessStartInfo) = 
            i.FileName <- parameters.ToolPath
            i.Arguments <- processArgs
        
        let result = ExecProcessAndReturnMessages updateProcStartInfo timeOut

        if result.ExitCode <> 0 then failwithf "FSCSLensGenerator.exe failed with exit code %i and message %s" result.ExitCode (String.concat "" result.Messages)
        else
            if result.Errors.Count > 0 then
                failwithf "FSCSLensGenerator.exe failed with exit code %i and erros %s" result.ExitCode (String.concat "" result.Errors)
            //result.Messages |> String.concat "" |> fun j -> JsonConvert.DeserializeObject<LensGenerationProperties>(j)
            result.Messages |> Seq.iter ( fun m -> tracefn "FSCSLensGenerator: %s" m)
        ()


module Lenses = 
    let sampleInputConfig = 
        let sampleInputSetParams (p:LensGenerationHelper.LensGenerationParams) = 
            { p with
                ProjectName = "SampleInput"
                ProjectDirectory = currentDirectory </> "SampleInput" </> "SampleInput"
                ProjectFileName = currentDirectory </> "SampleInput" </> "SampleInput" </> "SampleInput.fsproj"
                OutputFileName = None //if single file output is desired: currentDirectory </> "SampleUsage" </>"SampleInput.Lenses" </> "Lenses.fs"
                OutputDir = Some(currentDirectory </> "SampleUsage" </> "SampleInput.Lenses")

                // if you use multi file output, you probably want your lens project file updated
                OutputProjectFile =  Some(currentDirectory </> "SampleUsage" </> "SampleInput.Lenses" </> "SampleInput.Lenses.fsproj")

                ErrorFileName = None // console, if you prefer a file: Some( currentDirectory </> "SampleUsage" </>"SampleInput.Lenses" </> "LensErrors.txt" )
                OutputNamespace = Some("SampleInput.Lenses")
                SystemLibraries = ["System.Numerics"] // System.ValueTuple or whatever already installed lib you need
                // LibraryDirectory and BinaryLibariesInLibraryDirectory are coupled:
                // if you have a redist/lib folder you can name that folder in LibraryDirectory and name every required 
                // lib in BinaryLibariesInLibraryDirectory.
                //  LibraryDirectory =  Some(currentDirectory </> "packages" </> "FParsec" </> "lib" </> "net40-client")
                //  BinaryLibariesInLibraryDirectory = ["FParsec.dll";"FParsecCS.dll"]
                LibraryDirectory = None 
                BinaryLibariesInLibraryDirectory = []
                // you can alternatively provide full path of every required lib
                // if you need to build a dll as dependcy for the lens input, you can put it here
                // AbsolutePathLibraries = [pathToDependencyBinary]
                AbsolutePathLibraries = []


                // todo: generalize for regression testing
                ToolPath = // default uses lens generator from packages (in group "Build")
                    // equivalent to default
                    //(findToolFolderInSubPath "FSCSLensGenerator.exe" (currentDirectory </> "packages" </> "build" </> "FSCS-LensGenerator" </> "tools")) </> "FSCSLensGenerator.exe"
                    // 0.4.0, latest tfs release
                    //(findToolFolderInSubPath "FSCSLensGenerator.exe" (currentDirectory </> "OldReleases" </> "FSCS-LensGenerator.0.4.0.tools")) </> "FSCSLensGenerator.exe"
                    (findToolFolderInSubPath "FSCSLensGenerator.exe" (currentDirectory </> "OldReleases" </> "FSCS-LensGenerator.0.4.2.tools")) </> "FSCSLensGenerator.exe"
            }
        
        let generate() = 
            let buildDependencies() = 
                // if the project you generate lenses for is depending on another project
                // which is not yet in binary form you should build it/them here
                //build (setParams "Build") pathToDependencyProjectFile
                ()
            let assureDependencyBinariesExist() =
                //match fileExists pathToDependencyBinary with 
                //| true -> ()
                //| false -> 
                //    tracefn "Building %s" pathToDependencyProjectFile
                //    buildDependencies()
                ()
            assureDependencyBinariesExist()
            LensGenerationHelper.GenerateLenses sampleInputSetParams
        
        let destroy() =
            let lensParams = sampleInputSetParams LensGenerationHelper.LensGenerationDefaults
            lensParams.OutputFileName |> Option.iter ^ fun name ->
                DeleteFile name
                File.WriteAllText(name,"// this file will be autogenerated during the build")
            lensParams.OutputDir |> Option.iter ^ fun dir ->
                DirectoryInfo(dir).EnumerateFiles("*.fs")
                |> Seq.filter (fun f -> f.Name <> "AssemblyInfo.fs")
                |> Seq.iter (fun f -> DeleteFile f.FullName)
                tracefn "delete any *.fs files except AssemblyInfo.fs in %s" dir
        destroy, generate

    let generate() =
        (snd sampleInputConfig)()

    let destroy() =
        (fst sampleInputConfig)()
    
    let regenerate() = 
        destroy()
        generate()

let setParams target defaults =

    let fsharpTargetsPath = 
        Path.GetFullPath("packages/build/FSharp.Compiler.Tools/tools/Microsoft.FSharp.Targets")

    let properties = [
        "Configuration", "Release"
        "FSharpTargetsPath", fsharpTargetsPath
    ]

    { defaults with
        Verbosity = Some Quiet
        // prevents msbuild.exe from locking files the ci server may want to delete for the
        // next build
        NodeReuse = false 
        Targets = [target]
        Properties = properties
    }

let bootstrap() = 
    // add git submodule init/update here if needed
    // paket restore will be handled by build.cmd/build.sh but
    // if you update submodules afterwards you need to restore
    // packages for those submodules again...

    Lenses.regenerate()

let cleanSolutions() = 
    solutionFiles
    |> Seq.iter ^ build (setParams "Clean")

let buildSolutions() = 
    solutionFiles
    |> Seq.iter ^ build (setParams "Build")

let doNothing() = ()

Target "Bootstrap" bootstrap
Target "Clean" cleanSolutions
Target "Build" buildSolutions
Target "All" doNothing

"Bootstrap"
    ==> "Clean"
    ==> "Build"
    ==> "All"

// start build
RunTargetOrDefault "All"