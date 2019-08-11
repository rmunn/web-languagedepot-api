module UserPage

open Browser
open Elmish
open Fable.Core.JsInterop
open Fable.React
open Fulma
// open Thoth.Elmish.Toast
open Thoth.Fetch
open Thoth.Json

open TextInput
open Shared

type Msg =
    | RootModelUpdated of RootPage.Model
    | NewUserPageNav of string
    | AddProject of string
    | DelProject of string
    | LogResult of Result<string,string>
    | GetProjectsForUser
    | GetProjectsByRole of int
    | ProjectsListRetrieved of (string * string) list
    | LogException of System.Exception

type Model = { RootModel : RootPage.Model; ProjectList : (string * string) list; CurrentlyViewedUser : SharedUser option; }

let init rootModel = { RootModel = rootModel; ProjectList = []; CurrentlyViewedUser = None }, Cmd.none

let update (msg : Msg) (currentModel : Model) : Model * Cmd<Msg> =
    match msg with
    | RootModelUpdated newRootModel ->
        let nextModel = { currentModel with RootModel = newRootModel }
        nextModel, Cmd.none
    | NewUserPageNav username ->
        let nextModel = { currentModel with CurrentlyViewedUser = Some { Name = username } }
        nextModel, Cmd.none
    | GetProjectsForUser ->
        match currentModel.CurrentlyViewedUser with
        | None ->
            currentModel, Cmd.none
        | Some user ->
            let url = sprintf "/api/users/%s/projects" user.Name
            let data = { username = user.Name; password = "s3kr3t" }
            currentModel, Cmd.OfPromise.either (fun data -> Fetch.post(url, data)) data ProjectsListRetrieved LogException
    | GetProjectsByRole roleId ->
        match currentModel.CurrentlyViewedUser with
        | None ->
            currentModel, Cmd.none
        | Some user ->
            let url = sprintf "/api/users/%s/projects/withRole/%d" user.Name roleId
            let data = { username = user.Name; password = "s3kr3t" }
            currentModel, Cmd.OfPromise.either (fun data -> Fetch.post(url, data)) data ProjectsListRetrieved LogException
    | ProjectsListRetrieved projects ->
        let nextModel = { currentModel with ProjectList = projects }
        nextModel, Cmd.none
    | AddProject projCode ->
        match currentModel.CurrentlyViewedUser with
        | None ->
            currentModel, Cmd.none
        | Some user ->
            let data = Add { Add = user }
            let url = sprintf "/api/projects/%s" projCode
            let promise = Fetch.patch(url, data) |> Promise.map LogResult
            currentModel, Cmd.OfPromise.result promise
    | DelProject projCode ->
        match currentModel.CurrentlyViewedUser with
        | None ->
            currentModel, Cmd.none
        | Some user ->
            let data = Remove { Remove = user }
            let url = sprintf "/api/projects/%s" projCode
            let promise = Fetch.patch(url, data) |> Promise.map LogResult
            currentModel, Cmd.OfPromise.result promise
    | LogResult result ->
        let cmd = result |> Notifications.notifyStrResult
        currentModel, cmd
    | LogException exn ->
        let cmd = Notifications.notifyError exn.Message
        currentModel, cmd

let view (model : Model) (dispatch : Msg -> unit) =
    let name = match model.CurrentlyViewedUser with None -> "" | Some user -> user.Name
    div [ ] [ str ("This is the user page" + if System.String.IsNullOrEmpty name then "" else " for " + name)
              br [ ]
              textInputComponent "Project code" "" (str "+") (dispatch << AddProject)
              br [ ]
              textInputComponent "Project code" "" (str "-") (dispatch << DelProject)
              Button.a
                [ Button.Size IsSmall
                  Button.Color IsPrimary
                  Button.OnClick (fun _ -> dispatch GetProjectsForUser) ]
                [ str "Projects" ]
              ul [ ]
                 [ for (project, role) in model.ProjectList -> li [ ] [ str (project + ": " + role) ] ]
              textInputComponent "Role ID (manager = 3, contributor = 4, LDP = 6)" "" (str "By Role") (dispatch << GetProjectsByRole << System.Int32.Parse) ]
