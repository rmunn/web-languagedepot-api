module JsonHelpers

open Fable.Core
open Shared

[<Erase>]
type JsonResult<'a> =
    | Success of JsonSuccess<'a>
    | Failure of JsonError

let success data = Success { ok = true; data = data }
let failure msg = Failure { ok = false; message = msg }

let toResult (jsonResult : JsonResult<'a>) =
    match jsonResult with
    | Success { ok = true; data = data } -> Ok data
    | Success _ -> Error (sprintf "Invalid JsonResult: Success case should have ok = true. JsonResult was %A" jsonResult)
    | Failure { ok = false; message = msg } -> Error msg
    | Failure _ -> Error (sprintf "Invalid JsonResult: Success case should have ok = true. JsonResult was %A" jsonResult)

let unpackJsonResult currentModel jsonResult fn =
        match toResult jsonResult with
        | Ok newData ->
            fn newData
        | Error msg ->
            currentModel, Notifications.notifyError msg

// TODO: Consider whether the version below with separate ofSuccess and ofError is worth restoring, or whether we should just delete it
(*
let handleResult ofSuccess ofError (model,jsonResult) =
    match (toResult jsonResult) with
    | Ok data -> ofSuccess data
    | Error msg -> model, ofError msg

let handleSuccess ofSuccess jsonResult =
    jsonResult
    |> handleResult ofSuccess Notifications.notifyError
*)
