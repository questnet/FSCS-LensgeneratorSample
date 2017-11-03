namespace SampleInput

open System.IO

module FrontEnd = 
    type InputProject = {
        projectName : string
        projectDir : string
        projectFileName : string    
        referencedAssemblies : string list
    }

type LensLibrary = 
    | Aether

type OutputFileCreation =
    | CreateLensFile of (unit -> TextWriter)
    | CreateLensFileFor of (string -> TextWriter)

type OutputConfig = {
    FileConfig : OutputFileCreation
    Namespace : string
    LensLibrary : LensLibrary
    CreateErrorWriter : (unit -> TextWriter)
    UpdateProjectFile : (TextWriter -> string list -> unit)
}

type LensGenerationParameters = {
    input : FrontEnd.InputProject
    output : OutputConfig
}

type IFormatLenses =
    abstract member FormatLensFileHeader: unit -> string
    abstract member FormatLensHeader: string -> string -> string
    abstract member FormatLens: string -> string -> string -> string -> string -> string
    abstract member FormatLensFooter: unit -> string
    abstract member FormatLensFileFooter: unit -> string

type IWriteLenses =
    abstract member WriteLensFileHeader: unit -> unit
    abstract member WriteLensHeader: string -> string -> unit
    abstract member WriteLens: string -> string -> string -> string -> string -> unit
    abstract member WriteLensFooter: unit -> unit
    abstract member WriteLensFileFooter: unit -> unit
