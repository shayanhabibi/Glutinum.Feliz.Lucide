namespace Feliz.Lucide.Generator

open FParsec
open System

[<AutoOpen>]
module Helpers =
    // Attributable to Shmew - taken from Feliz.Generator.MaterialUI/Common.fs
    let appendApostropheToReservedKeywords =
      let reserved =
        [
          "checked"; "static"; "fixed"; "inline"; "default"; "component";
          "inherit"; "open"; "type"; "true"; "false"; "in"; "end"; "global"
        ]
        |> Set.ofList
      fun s -> if reserved.Contains s then s + "'" else s

module LabParsers =
    /// Find next <c>value</c>.
    let find value skip = skipCharsTillString value skip Int32.MaxValue >>. spaces
    /// Finds next <c>declare const</c> in lucide index
    let findValidDeclaration : Parser<_,_> = manyTill (find "declare" true >>. spaces ) ( followedBy (pstring "const") >>. pstring "const" )
    /// Reads the identifier
    let readIdentifier : Parser<_,_> = manyCharsTill (letter <|> digit ) (anyOf ": ")
    // Lucide Lab Parsers
    let getNextIconDecl : Parser<_,_> = findValidDeclaration >>. spaces >>. readIdentifier
    let getDecls : Parser<_,_> = manyTill (getNextIconDecl) (notFollowedBy ( find "declare" true ))

module LabGenerator =
    let parseIdentifiers target =
        runParserOnFile LabParsers.getDecls () target (System.Text.UTF8Encoding())
        |> function
            | Success(result, _, _) -> result
            | Failure(errorMsg,_,_) -> printfn $"{errorMsg}" |> exit 1

    let renderIdentifierMember identifier =
        $"""
        static member inline {identifier} ( props : #ISvgAttribute list ) = import "{identifier}" "@lucide/lab" |> Interop.svgAttribute "iconNode" |> fun iconProp -> Interop.reactApi.createElement(import "Icon" "lucide-react", createObj !!(iconProp::!!props))"""

    let renderDocument parsedIdentifiers =
        [
            """
namespace Feliz.Lucide

// THIS FILE IS AUTO-GENERATED

open Feliz
open Fable.Core
open Fable.Core.JsInterop

[<Erase>]
type private icon =
    static member inline iconNode ( value : string ) = Interop.svgAttribute "iconNode" value

module [<Erase>] Lab =
    type Lucide with"""
            for ( identifier : string ) in parsedIdentifiers do
                renderIdentifierMember ( identifier |> appendApostropheToReservedKeywords )
        ] |> String.concat ""

module LucideParser =
    let find value = skipCharsTillString value true Int32.MaxValue >>. spaces
    let findDeclaration = manyTill ( find "declare" ) ( followedBy (pstring "const") ) >>. pstring "const" >>. spaces
    let getIdentifier = manyCharsTill ( letter <|> digit <|> anyOf "_" ) ( anyOf " :" )
    let getIdentifierOfType type' = manyTill findDeclaration ( followedBy (getIdentifier >>. skipAnyOf " :" >>. pstring type') ) >>. getIdentifier
    let parser = manyTill (getIdentifierOfType "react") (notFollowedBy findDeclaration)

module LucideGenerator =
    let parseIdentifiers target =
        runParserOnFile LucideParser.parser () target (System.Text.UTF8Encoding())
        |> function
            | Success(result, _, _) -> result
            | Failure(errorMsg,_,_) -> printfn $"{errorMsg}" |> exit 1

    let renderIdentifierMember identifier =
        $"""
    static member inline {identifier} ( props : #ISvgAttribute list ) = Interop.reactApi.createElement(import "{identifier}" "lucide-react", createObj !!props)"""
    let renderDocument parsedIdentifiers =
        [
            """namespace Feliz.Lucide

// THIS FILE IS AUTO GENERATED

open Feliz
open Fable.Core
open Fable.Core.JsInterop

[<Erase>]
type Lucide ="""
            for ( identifier : string ) in parsedIdentifiers do
                if identifier <> "Icon" then
                    renderIdentifierMember ( identifier |> appendApostropheToReservedKeywords )
        ] |> String.concat ""


open System.IO

module Program =
    [<EntryPoint>]
    let main argv =
        LabGenerator.parseIdentifiers "../node_modules/@lucide/lab/dist/lucide-lab.d.ts"
        |> LabGenerator.renderDocument
        |> fun render -> File.WriteAllText(@"..\src\LabExports.fs", render)
        LucideGenerator.parseIdentifiers "../node_modules/lucide-react/dist/lucide-react.d.ts"
        |> LucideGenerator.renderDocument
        |> fun render -> File.WriteAllText(@"..\src\Exports.fs", render)
        0
