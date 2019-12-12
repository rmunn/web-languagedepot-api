open System.IO
open System.Threading.Tasks

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Hosting
open FSharp.Control.Tasks.V2
open Giraffe
open Giraffe.HttpStatusCodeHandlers
open Saturn
open Shared
open Shared.Settings
open Thoth.Json.Net

let tryGetEnv = System.Environment.GetEnvironmentVariable >> function null | "" -> None | x -> Some x

let publicPath = Path.GetFullPath "../Client/public"

let errorHandler : Giraffe.Core.ErrorHandler = fun ex logger ->
    logger.LogError(ex, "")
    // Unfortunately the Thoth library just throws generic System.Exceptions, so we have to
    // inspect the message to detect JSON parsing failures
    if ex.Message.StartsWith("Error at: `$`") then
        // JSON parsing failure
        setStatusCode 400 >=> json {| status = "error"; message = ex.Message |}
    else
        setStatusCode 500 >=> json "Internal Server Error"

let port =
    "SERVER_PORT"
    |> tryGetEnv |> Option.map uint16 |> Option.defaultValue 8085us

let webApp = router {
    get "/api/project/private" Controller.getAllPrivateProjects
    getf "/api/project/private/%s" Controller.getPrivateProject
    get "/api/project" Controller.getAllPublicProjects
    getf "/api/project/%s" Controller.getPublicProject
    // TODO: Not in real API spec. Why not? Probably need to add it
    get "/api/users" Controller.listUsers
    get "/api/privateUsers" Controller.listUsersPrivate  // TODO: Test-only. Remove before going to production.
    getf "/api/users/limit/%i" Controller.listUsersLimit
    getf "/api/users/offset/%i" Controller.listUsersOffset
    getf "/api/users/limit/%i/offset/%i" Controller.listUsersLimitOffset
    getf "/api/users/%s" Controller.getUser  // Note this needs to come below the limit & offset endpoints so that we don't end up trying to fetch a user called "limit" or "offset"
    postf "/api/searchUsers/%s" (fun searchText -> bindJson<Api.LoginCredentials> (Controller.searchUsers searchText))
    // TODO: Change limit and offset above to be query parameters, because forbidding usernames called "limit" or "offset" would be an artificial restriction
    getf "/api/project/exists/%s" Controller.projectExists
    getf "/api/users/exists/%s" Controller.userExists
    postf "/api/users/%s/projects" (fun username -> bindJson<Api.LoginCredentials> (Controller.projectsAndRolesByUser username))
    patchf "/api/project/%s" (fun projId -> bindJson<Api.EditProjectMembershipApiCall> (Controller.addOrRemoveUserFromProject projId))
    // Suggested by Chris Hirt: POST to add, DELETE to remove, no JSON body needed
    postf "/api/project/%s/user/%s/withRole/%s" Controller.addUserToProjectWithRole
    postf "/api/project/%s/user/%s" Controller.addUserToProject  // Default role is "Contributer", yes, spelled with "er"
    deletef "/api/project/%s/user/%s" Controller.removeUserFromProject
    postf "/api/users/%s/projects/withRole/%s" (fun (username,roleName) -> bindJson<Api.LoginCredentials> (Controller.projectsAndRolesByUserRole username roleName))
    get "/api/roles" Controller.getAllRoles
    post "/api/users" (bindJson<Api.CreateUser> Controller.createUser)
    putf "/api/users/%s" (fun login -> bindJson<Api.CreateUser> (Controller.upsertUser login))
    patchf "/api/users/%s" (fun login -> bindJson<Api.ChangePassword> (Controller.changePassword login))
    postf "/api/users/%s/verify-password" (fun login -> bindJson<Api.LoginCredentials> (Controller.verifyPassword login))
    post "/api/project" (bindJson<Api.CreateProject> Controller.createProject)
    get "/api/count/users" Controller.countUsers
    get "/api/count/projects" Controller.countProjects
    get "/api/count/non-test-projects" Controller.countRealProjects
    deletef "/api/project/%s" Controller.archiveProject
    deletef "/api/project/private/%s" Controller.archivePrivateProject
    // Rejected API: POST /api/project/{projId}/add-user/{username}
}

let setupAppConfig (context : WebHostBuilderContext) (configBuilder : IConfigurationBuilder) =
    configBuilder.AddIniFile("/etc/ldapi-server/ldapi-server.ini", optional=true, reloadOnChange=false) |> ignore
    // TODO: Find out how to catch "configuration reloaded" event and re-register MySQL services when that happens. Then set reloadOnChange=true instead

let registerMySqlServices (context : WebHostBuilderContext) (svc : IServiceCollection) =
    let x = getSettingsValue<MySqlSettings> context.Configuration
    Model.ModelRegistration.registerServices svc x.ConnString

let hostConfig (builder : IWebHostBuilder) =
    builder
        .ConfigureAppConfiguration(setupAppConfig)
        .ConfigureServices(registerMySqlServices)

let extraJsonCoders =
    Extra.empty
    |> Extra.withInt64

let app = application {
    url ("http://0.0.0.0:" + port.ToString() + "/")
    use_router webApp
    memory_cache
    disable_diagnostics  // Don't create site.map file
    error_handler errorHandler
    use_json_serializer(Thoth.Json.Giraffe.ThothSerializer(extra=extraJsonCoders))
    use_gzip
    host_config hostConfig
    use_config buildConfig // TODO: Get rid of this
}

run app
