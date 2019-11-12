module SingleProjectPage

open Browser
open Elmish
open Fable.React
open Fable.React.Props
open Fulma
open Thoth.Elmish
open Thoth.Elmish.FormBuilder
open Thoth.Elmish.FormBuilder.BasicFields
open Thoth.Fetch

open JsonHelpers
open Shared

type Msg =
    | NewProjectPageNav of string
    | OnFormMsg of FormBuilder.Types.Msg
    | ListAllProjects
    | ListSingleProject of string
    | ProjectListRetrieved of JsonResult<Dto.ProjectList>
    | SingleProjectRetrieved of JsonResult<Dto.ProjectDetails>
    | ClearProjects
    | FormSubmitted
    | GotFormResult of JsonResult<int>
    | HandleFetchError of exn
    | GetConfig
    | GotConfig of Shared.Settings.MySqlSettings

type Model = { CurrentlyViewedProject : Dto.ProjectDetails option; ProjectList : Dto.ProjectList; FormState : FormBuilder.Types.State }

let (formState, formConfig) =
    Form<Msg>
        .Create(OnFormMsg)
        .AddField(
            BasicInput
                .Create("Name")
                .WithLabel("Project Name")
                .IsRequired()
                .WithDefaultView()
        )
        .AddField(
            BasicTextarea
                .Create("Description")
                .WithLabel("Description")
                .WithPlaceholder("Describe your project in a paragraph or two")
                .WithDefaultView()
        )
        .AddField(
            BasicInput
                .Create("Identifier")
                .WithLabel("Project Code")
                .IsRequired("You must specify a project code")
                .AddValidator(fun state ->
                    let lower = state.Value.ToLowerInvariant()
                    if state.Value <> lower then
                        Types.Invalid "Project codes must be in lowercase letters"
                    else
                        let chars = lower.ToCharArray() |> Array.distinct |> Array.filter (fun ch -> ch < 'a' || ch > 'z')
                        let hasInvalidChars = chars |> Array.filter (fun ch -> ch <> '-' && ch <> '_') |> Array.length > 0
                        if hasInvalidChars then
                            Types.Invalid "Project codes must contain only letters, hyphens, and underscores"
                        else
                            Types.Valid
                )
                .WithDefaultView()
        )
        .Build()

let init() =
    let formState, formCmds = Form.init formConfig formState
    { CurrentlyViewedProject = None; ProjectList = []; FormState = formState }, Cmd.map OnFormMsg formCmds

let update (msg : Msg) (currentModel : Model) : Model * Cmd<Msg> =
    match msg with
    | HandleFetchError e ->
        currentModel, Notifications.notifyError e.Message
    | NewProjectPageNav projectCode ->
        currentModel, Cmd.ofMsg (ListSingleProject projectCode)
    | OnFormMsg msg ->
        let (formState, formCmd) = Form.update formConfig msg currentModel.FormState
        let nextModel = { currentModel with FormState = formState }
        nextModel, Cmd.map OnFormMsg formCmd
    | ListAllProjects ->
        let url = "/api/project"
        currentModel, Cmd.OfPromise.either Fetch.get url ProjectListRetrieved HandleFetchError
    | ListSingleProject projectCode ->
        let url = sprintf "/api/project/%s" projectCode
        currentModel, Cmd.OfPromise.either Fetch.get url SingleProjectRetrieved HandleFetchError
    | SingleProjectRetrieved projectResult ->
        match toResult projectResult with
        | Ok project ->
            printfn "%A" project
            { currentModel with CurrentlyViewedProject = Some project }, Cmd.none
        | Error msg ->
            currentModel, Notifications.notifyError msg
    | ProjectListRetrieved projectsResult ->
        match toResult projectsResult with
        | Ok projects ->
            { currentModel with ProjectList = projects }, Cmd.none
        | Error msg ->
            currentModel, Notifications.notifyError msg
    | ClearProjects ->
        let nextModel = { currentModel with ProjectList = [] }
        nextModel, Cmd.none
    | FormSubmitted ->
        let newFormState, isValid = Form.validate formConfig currentModel.FormState
        let nextModel = { currentModel with FormState = newFormState }
        if isValid then
            let json = Form.toJson formConfig newFormState
            match Thoth.Json.Decode.Auto.fromString<Api.CreateProject> json with
            | Ok data ->
                let url = "/api/project"
                nextModel, Cmd.OfPromise.either (fun data -> Fetch.post(url, data)) data GotFormResult HandleFetchError
            | Error err ->
                printfn "Decoding error (fix the form validation?): %s" err
                nextModel, Cmd.none
        else
            nextModel, Cmd.none  // TODO: Do something to report "invalid form not submitted"?
    | GotFormResult jsonResult ->
        match toResult jsonResult with
        | Ok n ->
            printfn "Got ID %d from server" n
        | Error e ->
            printfn "Server responded with error message: %s" e
        currentModel, [fun _ -> history.go -1]
    | GetConfig ->
        let url = "/api/config"
        currentModel, Cmd.OfPromise.either Fetch.get url GotConfig HandleFetchError
    | GotConfig mySqlSettings ->
        printfn "Got config: %A" mySqlSettings
        printfn "Port: %d" mySqlSettings.Port
        currentModel, Cmd.none

let formActions (formState : FormBuilder.Types.State) dispatch =
    div [ ]
        [ Button.button
            [ Button.Props [ OnClick (fun _ -> dispatch FormSubmitted) ]
              Button.Color IsPrimary
            ]
            [ str "Submit" ] ]

let membershipView (membership : Dto.MemberList option) =
    match membership with
    | None -> str " (member list not provided)"
    | Some members ->
        if Seq.isEmpty members then
            str (sprintf " (no members in %A)" members)
        else
            let toStr title role (lst : Dto.MemberList) = title + ": " + if Seq.isEmpty lst then "(none)" else String.concat "," (lst |> Seq.filter (snd >> ((=) role)) |> Seq.map fst) + ";"
            // TODO: Investigate why List.isEmpty and List.length lst = 0 both seem to be returning *incorrect* results!
            console.log("Memberlist attempted: ", members)
            // str ((toStr "Managers" Manager members +
            //       toStr "Contributors" Contributor members +
            //       toStr "Observers" Observer members +
            //       toStr "Programmers" Programmer members).TrimEnd(';'))
            str (sprintf "%A" members)  // TODO: Figure out why the filtering above wasn't working right

let projectDetailsView (project : Dto.ProjectDetails option) =
    div [ ] [
        match project with
        | None -> str "(no project loaded)"
        | Some project ->
            h2 [ ] [ str project.name; str (sprintf " (%s)" project.code); membershipView project.membership ]
            p [ ] [ str project.description ]
    ]

let view (model : Model) (dispatch : Msg -> unit) =
    div [ ] [
        projectDetailsView model.CurrentlyViewedProject
        br [ ]
        Form.render {
            Config = formConfig
            State = model.FormState
            Dispatch = dispatch
            ActionsArea = (formActions model.FormState dispatch)
            Loader = Form.DefaultLoader }
        br [ ]
        str "Config should be valid MySqlSettings config; check it"
        br [ ]
        Button.button
            [ Button.Props [ OnClick (fun _ -> dispatch GetConfig) ]
              Button.Color IsPrimary
            ]
            [ str "Get Config" ]
        br [ ]
        (if model.ProjectList |> List.isEmpty then
            Button.button
                [ Button.Props [ OnClick (fun _ -> dispatch ListAllProjects) ]
                  Button.Color IsPrimary
                ]
                [ str "List Projects" ]
        else
            Button.button
                [ Button.Props [ OnClick (fun _ -> dispatch ClearProjects) ]
                  Button.Color IsPrimary
                ]
                [ str "Clear Project list" ])
        ul [ ]
           [ for project in model.ProjectList ->
                li [ ] [ a [ OnClick (fun _ -> dispatch (ListSingleProject project.code)) ] [ str (sprintf "%s:" project.name); membershipView project.membership ] ] ]
        ]
