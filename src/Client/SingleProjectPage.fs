module SingleProjectPage

open Browser
open Elmish
open Fable.FontAwesome
open Fable.FontAwesome.Free
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
    | ShowConfirmationModal of Cmd<Msg>
    | SubmitConfirmationModal of bool
    | FormSubmitted
    | GotFormResult of JsonResult<int>
    | HandleFetchError of exn
    | EditMembership of string * Dto.ProjectDetails
    | RemoveMembership of string * Dto.ProjectDetails * string
    | ShowAddUserDialog of Dto.ProjectDetails * string
    | LogResultAndReload of string * JsonResult<string>

type Model = { CurrentlyViewedProject : Dto.ProjectDetails option; ProjectList : Dto.ProjectList; FormState : FormBuilder.Types.State; ConfirmationModalVisible : bool; ConfirmationModalCmd : Cmd<Msg> }

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
    { CurrentlyViewedProject = None; ProjectList = [||]; FormState = formState; ConfirmationModalVisible = false; ConfirmationModalCmd = Cmd.none }, Cmd.map OnFormMsg formCmds

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
        let nextModel = { currentModel with ProjectList = [||] }
        nextModel, Cmd.none
    | ShowConfirmationModal cmd ->
        let nextModel = { currentModel with ConfirmationModalVisible = true; ConfirmationModalCmd = cmd }
        nextModel, Cmd.none
    | SubmitConfirmationModal shouldRunCmd ->
        let cmd = if shouldRunCmd then currentModel.ConfirmationModalCmd else Cmd.none
        let nextModel = { currentModel with ConfirmationModalVisible = false; ConfirmationModalCmd = Cmd.none }
        nextModel, cmd
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
    | LogResultAndReload (projectCode,jsonResult) ->
        match toResult jsonResult with
        | Ok s ->
            printfn "Got success message %s from server" s
        | Error e ->
            printfn "Server responded with error message: %s" e
        currentModel, Cmd.ofMsg (ListSingleProject projectCode)
    | EditMembership (name,project) ->
        console.log("Not yet implemented. Would edit membership of",name,"in project",project)
        // Once we implement, this should pop up a modal with four checkboxes, so you can change what role(s) someone has.
        currentModel, Cmd.none
    | ShowAddUserDialog (project, role) ->
        console.log("Not yet implemented. Would add a user to project",project,"with role",role)
        // Once we implement, this should pop up a modal with four checkboxes, so you can change what role(s) someone has.
        currentModel, Cmd.none
    | RemoveMembership (name,project,role) ->
        let isLastManager =
            role = "Manager" &&
            project.membership |> Seq.filter (fun kv -> kv.Value = "Manager") |> Seq.length = 1
        let url = sprintf "/api/project/%s/user/%s/withRole/%s" project.code name (role.ToString())
        let cmd = Cmd.OfPromise.either (fun data -> Fetch.delete(url, data)) () (fun result -> LogResultAndReload(project.code,result)) HandleFetchError
        currentModel, if isLastManager then Cmd.ofMsg (ShowConfirmationModal cmd) else cmd

let formActions (formState : FormBuilder.Types.State) dispatch =
    div [ ]
        [ Button.button
            [ Button.Props [ OnClick (fun _ -> dispatch FormSubmitted) ]
              Button.Color IsPrimary
            ]
            [ str "Submit" ] ]

let confirmationModal visible submit =
    Modal.modal [ Modal.IsActive visible ]
        [ Modal.background [ Props [ OnClick (submit false) ] ] [ ]
          Modal.Card.card [ ]
            [ Modal.Card.body [ ]
                [ str "Are you sure you want to remove the last manager of this project?" ]
              Modal.Card.foot [ ]
                [ Button.button [ Button.OnClick (submit true) ]
                    [ str "Remove last manager" ]
                  Button.button [ Button.OnClick (submit false); Button.Color IsSuccess ]
                    [ str "Make no change" ] ] ] ]

let membershipViewInline (membership : Map<string,string>) =
    if Seq.isEmpty membership then
        str (sprintf " (no members in %A)" membership)
    else
        membership |> Seq.map (fun kv -> sprintf "%s: %s" kv.Key kv.Value) |> String.concat "," |> str

let membershipViewBlock (dispatch : Msg -> unit) (project : Dto.ProjectDetails) =
    // TODO: Implement with appropriate Edit/Remove buttons
    // For now, just replicate inline view
    membershipViewInline project.membership
    // match project.membership with
    // | None -> []
    // | Some members ->
    //     let section title (role : RoleType) (lst : Dto.MemberList) =
    //         [
    //             h2 [ ] [ str title ]
    //             for name, itemRole in lst do
    //                 // TODO: Investigate why the itemRole.ToNumericId() in all these pairs is *always* coming out as 3 (manager) no matter what the role actually is.
    //                 // For now, we'll use ToString instead of ToNumericId; it's not meaningfully slower since the strings are short, and it works.
    //                 if itemRole.ToString() = role.ToString() then yield! [
    //                     str name
    //                     str "\u00a0"
    //                     a [ Style [Color "inherit"]; OnClick (fun _ -> dispatch (EditMembership (name, project)))] [ Fa.span [ Fa.Solid.Edit ] [ ] ]
    //                     str "\u00a0"
    //                     a [ Style [Color "red"]; OnClick (fun _ -> dispatch (RemoveMembership (name, project, itemRole)))] [ Fa.span [ Fa.Solid.Times ] [ ] ]
    //                     br []
    //                 ]
    //                 // Can't just compare itemRole to role because in Javascript they end up being different types, for reasons I don't yet understand
    //             a [ Style [Color "inherit"]; OnClick (fun _ -> dispatch (ShowAddUserDialog (project, role))) ] [ Fa.span [ Fa.Solid.Plus; Fa.Props [ Style [Color "green"] ] ] [ ]; str (" Add " + role.ToString()) ]
    //         ]
    //     [
    //         yield! section "Managers" Manager members
    //         yield! section "Contributors" Contributor members
    //         yield! section "Observers" Observer members
    //         yield! section "Programmers" Programmer members
    //     ]

let projectDetailsView (dispatch : Msg -> unit) (project : Dto.ProjectDetails option) =
    div [ ] [
        match project with
        | None -> str "(no project loaded)"
        | Some project ->
            h2 [ ] [ str project.name; str (sprintf " (%s)" project.code); membershipViewInline project.membership ]
            p [ ] [ str project.description ]
            membershipViewBlock dispatch project
    ]

let view (model : Model) (dispatch : Msg -> unit) =
    div [ ] [
        confirmationModal model.ConfirmationModalVisible (fun b _ -> dispatch (SubmitConfirmationModal b))
        projectDetailsView dispatch model.CurrentlyViewedProject
        br [ ]
        Form.render {
            Config = formConfig
            State = model.FormState
            Dispatch = dispatch
            ActionsArea = (formActions model.FormState dispatch)
            Loader = Form.DefaultLoader }
        br [ ]
        (if model.ProjectList |> Array.isEmpty then
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
                li [ ] [ a [ OnClick (fun _ -> dispatch (ListSingleProject project.code)) ] [ str (sprintf "%s:" project.name); membershipViewInline project.membership ] ] ]
        ]
