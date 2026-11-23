# TaskList

TaskList is a .NET 8 REST API for creating, renaming, deleting, and sharing task lists. MongoDB stores each list and its linked users in one document.

## Run locally

Use the .NET 8 SDK and an accessible MongoDB instance. The project is tested with MongoDB 8.0. Docker is optional for the API and required for the Testcontainers integration tests.

The API defaults to `mongodb://localhost:27017` and database `TaskList`. To override either value, set `MongoDB__ConnectionString` and `MongoDB__DatabaseName` in the API process environment. Keep credentials outside the repository. The database account needs read, write, and index-creation privileges because the API ensures its indexes at startup.

From the repository root, run:

```sh
dotnet restore TaskList.sln
dotnet run --project src/TaskList.Api/TaskList.Api.csproj --no-launch-profile -- --urls http://localhost:5080
```

## Try the API in Swagger UI

With MongoDB running, start the API in the Development environment:

```sh
dotnet run --project src/TaskList.Api/TaskList.Api.csproj --no-launch-profile -- --urls http://localhost:5080 --environment Development
```

Open [Swagger UI](http://localhost:5080/swagger). Expand an operation, select **Try it out**, and enter a non-empty GUID in `X-User-Id`. For example, run `POST /api/task-lists` with `{"name":"Shopping"}`, then use the returned `id` and the same caller ID in `GET /api/task-lists/{listId}`.

Swagger UI and the OpenAPI document at `/swagger/v1/swagger.json` are available in Development.

## HTTP API

Send a non-empty GUID in `X-User-Id` on every request. The header is a client-supplied caller ID for the exercise. The API does not verify that identity.

| Method and route | Access | Success |
|---|---|---|
| `POST /api/task-lists` | Any caller becomes the owner | `201` with list details and `Location` |
| `GET /api/task-lists` | Lists owned by or shared with the caller | `200` with a page |
| `GET /api/task-lists/{listId}` | Owner or linked user | `200` with list details |
| `PATCH /api/task-lists/{listId}` | Owner or linked user | `204` after renaming |
| `DELETE /api/task-lists/{listId}` | Owner | `204` after deletion |
| `PUT /api/task-lists/{listId}/linked-users/{userId}` | Owner or linked user | `201` for a new link, `204` if already linked |
| `GET /api/task-lists/{listId}/linked-users` | Owner or linked user | `200` with an array of linked user IDs |
| `DELETE /api/task-lists/{listId}/linked-users/{userId}` | Owner or linked user | `204` after removing an existing link |

POST and PATCH accept a JSON object with a `name` string. The API trims surrounding whitespace and requires 1–255 characters after trimming. Invalid input returns `400`. A missing list returns `404`. A caller without permission for an existing list receives `403`. Adding the owner as a linked user returns `409` when the caller has access.

For link deletion, `404` means either the list is missing or an authorized caller targeted a GUID absent from `linkedUserIds`. The API has no user registry. An authorized caller targeting the owner gets `409`. A linked user who removes their own link gets `204` on the first request and `403` on a retry. The owner gets `404` when retrying the removal of that link.

Listing accepts `page` (default 1, minimum 1) and `pageSize` (default 20, range 1–100). Results are sorted by creation time descending, then ID descending. `totalCount` counts all accessible lists. A page beyond the end has an empty `items` array.

### Examples

These PowerShell requests create a list, share it, and fetch the grantee's first page.

```powershell
$baseUrl = 'http://localhost:5080/api/task-lists'
$owner = @{ 'X-User-Id' = '11111111-1111-4111-8111-111111111111' }
$linkedUser = '22222222-2222-4222-8222-222222222222'

$list = Invoke-RestMethod -Method Post -Uri $baseUrl -Headers $owner `
    -ContentType 'application/json' -Body '{"name":"Shopping"}'
Invoke-RestMethod -Method Put -Uri "$baseUrl/$($list.id)/linked-users/$linkedUser" -Headers $owner
Invoke-RestMethod -Uri "${baseUrl}?page=1&pageSize=20" -Headers @{ 'X-User-Id' = $linkedUser }
```

The POST response has a `Location` header pointing to the new list. The path is `/api/task-lists/{listId}` when the app runs at the site root. When hosting sets a base path for the application, the generated link includes that prefix. Both POST and GET by ID return this shape.

```json
{
  "id": "33333333-3333-4333-8333-333333333333",
  "name": "Shopping",
  "ownerId": "11111111-1111-4111-8111-111111111111",
  "createdAtUtc": "2026-11-22T12:00:00Z"
}
```

The collection endpoint returns this shape.

```json
{
  "items": [{"id": "33333333-3333-4333-8333-333333333333", "name": "Shopping"}],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1
}
```

## Solution and persistence

- `src/TaskList.Api` owns HTTP endpoints and dependency injection.
- `src/TaskList.Application` owns use cases, validation, and the repository interface.
- `src/TaskList.Infrastructure` implements persistence with the MongoDB driver.
- `tests/TaskList.UnitTests` tests application rules. `tests/TaskList.IntegrationTests` exercises the API and MongoDB together.

API and Infrastructure depend on Application. The API composition root wires the Infrastructure implementation. MongoDB stores GUIDs as BSON UUID subtype 4. Write filters include the caller's access predicate, so permission checks and updates happen in one document operation. Link updates use `$addToSet` and `$pull`.

MongoDB's `_id_` index serves lookups by list ID. Two compound indexes, `(ownerId ASC, createdAtUtc DESC, _id DESC)` and `(linkedUserIds ASC, createdAtUtc DESC, _id DESC)`, support the two access checks and the page sort. The second is multikey because it indexes an array.

## Build and test

```sh
dotnet build TaskList.sln -c Release
dotnet test tests/TaskList.UnitTests/TaskList.UnitTests.csproj -c Release
dotnet test tests/TaskList.IntegrationTests/TaskList.IntegrationTests.csproj -c Release
```

Start a Docker daemon that can run Linux containers before running integration tests. Testcontainers starts MongoDB 8.0 and each test host uses an isolated database. Run `dotnet test TaskList.sln -c Release` for both suites.
