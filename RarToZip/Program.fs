open System.IO

open SharpCompress.Archives;
open SharpCompress.Archives.Rar;
open SharpCompress.Archives.Zip
open SharpCompress.Common;
open SharpCompress.Writers

type FormatConversionResult =
| ConversionSucceded
| ConversionFailed of string

type ModifierParser =
| InputFile of string
| OutputFile of string
| ParsingModifierFailed

type CmdArgsParserResult =
| ParsedArgs of (string * string)
| ParsingFailed of string

let parseModifiers modifier argument =
    match modifier with
    | "--input" -> InputFile(argument)
    | "-i" -> InputFile(argument)
    | "--output" -> OutputFile(argument)
    | "-o" -> OutputFile(argument)
    | _ -> ParsingModifierFailed 

let parseCommandLineArguments argv =
    match argv with 
    | [| mod1; arg1; mod2; arg2 |] ->
        let arg1 = parseModifiers mod1 arg1
        let arg2 = parseModifiers mod2 arg2

        match (arg1, arg2) with
        | (InputFile(input), OutputFile(output)) -> ParsedArgs(input, output)
        | (OutputFile(output), InputFile(input)) -> ParsedArgs(input, output)
        | _ -> ParsingFailed("Invalid arguments.")
    | _ -> ParsingFailed("Usage: [program name]\n" +
                         "\t-i (--input) [input file]\n" +
                         "\t-o (--output) [output file]\n")

let extractRarIntoTempFolder (fileName: string) dir =
    if File.Exists(fileName) = false then ConversionFailed("Input file not found.")
    else if RarArchive.IsRarFile(fileName) = false then ConversionFailed("Input file is not a RAR archive.")
    else 
        use archive = RarArchive.Open(fileName)
        archive.Entries 
        |> Seq.cast
        |> Seq.filter (fun (entry: RarArchiveEntry) -> not entry.IsDirectory)
        |> Seq.iter (fun entry -> entry.WriteToDirectory(dir, new ExtractionOptions()))

        ConversionSucceded

let zipTempFolder (outputFile: string) tempDir =
    use archive = ZipArchive.Create()
    archive.AddAllFromDirectory(tempDir)
    archive.SaveTo(outputFile, new WriterOptions(CompressionType.Deflate))

    ConversionSucceded

let convert tempDir parserOutput =
    let printFailure result =
        printfn "Conversion failed with error: %s" result

    let printSuccess =
        printfn "Operation completed"

    match parserOutput with
    | ParsedArgs((inputFile, outputFile)) ->
        let unrarResult = extractRarIntoTempFolder inputFile tempDir
        match unrarResult with
        | ConversionSucceded ->
            let unzipResult = zipTempFolder outputFile tempDir
            match unzipResult with
            | ConversionFailed(result) -> printFailure result
            | ConversionSucceded -> printSuccess
        | ConversionFailed(result) -> printFailure result
    | ParsingFailed(result) -> printFailure result

[<EntryPoint>]
let main argv =
    let tempDir = "Temp"

    Directory.CreateDirectory(tempDir) |> ignore
    parseCommandLineArguments argv |> convert tempDir
    Directory.Delete(tempDir, true)

    0 // return an integer exit code
