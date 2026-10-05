---
title: "Organisation membership checks"
status: active
workstream: security
milestone: "Security & Auth"
issues: [175, 197, 198, 199, 200, 201]
superseded_by: null
last_reviewed: 2026-10-05
---

# Organisation membership checks

## Context
Endpoints took `organisationId` from the query or body and never checked that the caller belonged to that organisation (#175). Creating an organisation never recorded a member either, so `OrganisationUsers` was always empty. Some list endpoints (`/applications`, `/environments`, `/resource-templates`, `/organisations`) returned rows from every organisation.

Branch `feature/organisation_membership_check`, PR #195. The first version (`8686597`) used one reflection-based filter on every private group plus a membership check copied into each handler. It was reworked into explicit filters that each endpoint opts into.

## Design
- **Membership is recorded on create.** `CreateOrganisationEndpoint` calls `Organisation.Create(name).AddUser(user.GetUserId())`. The user id is the JWT `sub`, which maps to `ClaimTypes.NameIdentifier` (`Extensions/ClaimsPrincipalExtensions.GetUserId`).
- **Per-request membership.** `Shared/Authorization/IOrganisationAccess` (scoped) loads the caller's organisation ids once per request through `IOrganisationRepository.GetIdsForMemberAsync` and answers `IsMemberAsync`.
- **Endpoint filters, declared in each endpoint's `Map`** (`Shared/Authorization/OrganisationAuthorizationExtensions`):
  - `.RequireOrganisationMember()` checks a bound `Guid organisationId` parameter (query or route).
  - `.RequireOrganisationMember<TRequest>(r => r.OrganisationId)` checks an id on a bound request. The selector may return `Guid`, `string` or `OrganisationId`, so request contracts stay unchanged.
  - Both return 400 `INVALID_ORGANISATION_ID` for an unparseable id and 403 when the caller isn't a member.
  - `.RequireOrganisationAccess<TEntity, TId>(id => new TId(id))` loads the entity named by the `id` route parameter through `IRepository<TEntity, TId>` and returns 404 when it is missing or belongs to another organisation. This hides whether the entity exists. `TEntity` must implement `IEntity`, or an overload takes a resolver for the organisation id. Organisations use the resolver (`Core/Organisation/OrganisationAuthorization`), and so do deployments, via their application (`Engine/Deployment/DeploymentAuthorization`).
  - `.HandlesOrganisationScope()` only adds metadata. It marks endpoints that scope organisations themselves: `POST /organisations`, `POST /deployments`, and the list endpoints without `organisationId` (`/organisations`, `/applications`, `/environments`, `/resource-templates`), which query only the caller's organisations in SQL. The runner group (`/internal/runs/{runId}`) declares it too: it authenticates with a run token bound to the route's run, not a user.
  - Filter factories find the bound parameter by type (and name) and check that `IRepository<TEntity, TId>` is registered at startup, so a wrong declaration fails when the app maps its endpoints.
- **`POST /deployments`** treats an application or environment in a foreign organisation as missing (400, same message as a missing id). The environment must also belong to the application's organisation, so a member of two organisations can't deploy across them.
- **Managing members** (`Endpoints/Core/Organisation`, #199). All three use `.RequireOrganisationAccess()`, so a non-member gets 404:
  - `GET /organisations/{id}/members` lists the members: the `OrganisationUser` id, the identity user id, and the user's name and email.
  - `POST /organisations/{id}/members` with `{ "email": "..." }` adds a registered user through `Organisation.AddUser`. 400 `USER_NOT_FOUND` for an unknown email, 409 `ALREADY_A_MEMBER`.
  - `DELETE /organisations/{id}/members/{memberId}` removes a member by its `OrganisationUser` id through `Organisation.RemoveUser`. 404 for a member of another organisation, 409 `LAST_MEMBER` for the last one. A member can remove themselves.
  - Adding and removing run in a transaction that first locks the organisation's row (`IOrganisationRepository.LockAsync`), so two concurrent removals can't leave it empty. `UpdateUsersAsync` inserts and deletes `OrganisationUsers` rows to match the organisation.
  - `Organisation.RemoveUser` throws when asked to remove the last member.
- **Fail-closed guard.** `OrganisationScopeCoverageTests` fails when an authorised endpoint has neither `OrganisationScopedMetadata` nor `HandlesOrganisationScopeMetadata`.

## Testing
- `Orchitect.Api.Integration.Tests/OrganisationMembershipIntegrationTests.cs` uses `AuthTokenHelper.AddAuthorisationHeaderForNewUser()` to get an outsider client. `AddAuthorisationHeader()` still returns the shared, cached user that owns everything the other tests create.
- `OrganisationMemberIntegrationTests.cs` covers listing, adding and removing members, the last member, and a non-member getting 404.
- `OrganisationScopeCoverageTests` enumerates `EndpointDataSource`.
- Full suite: `DOTNET_USE_POLLING_FILE_WATCHER=true dotnet test src/Orchitect.Api.Integration.Tests`. Without the variable, `DeploymentIntegrationTests` can fail with "configured user limit (128) on the number of inotify instances". That is environmental: each `WithWebHostBuilder` host opens file watchers.

## Decisions
- 403 for an explicit `organisationId` the caller can't use, 404 for an entity id in another organisation. This follows the issue's "403/404" criterion and keeps entity existence private.
- Endpoint filters rather than authorization policies, because policies run before model binding and can't see ids in a request body.
- Each endpoint opts in explicitly, and the coverage test enforces it. There is no catch-all filter that guesses where the id is.
- The entity filter and the handler both load the entity, so a request does two primary-key lookups. This keeps the handlers free of authorization code.
- A deployment's organisation comes from its application. If the application is deleted, its deployments return 404 to everyone.
- No data migration. Organisations created before this change have no members, because the creator was never stored, and a migration can't know who should own them. `scripts/backfill-organisation-members.sh` fixes them once (see below).
- Every member can add and remove members. There are no roles yet.
- Members are added by email rather than user id, because users can't look up each other's ids.

## Backfilling organisations without members
Run once per environment, from `platform/`, after the user has registered:

```bash
scripts/backfill-organisation-members.sh "<connection-string>" admin@example.com --dry-run
scripts/backfill-organisation-members.sh "<connection-string>" admin@example.com
```

It adds the user to every organisation that has no members and prints those organisations. It is safe to run again: an organisation with a member is skipped. That user can then add the right members through the API. Set `PSQL="docker exec -i <postgres-container> psql"` when `psql` isn't installed locally (the Aspire database listens on port 41031).

## Outstanding
- `PipelineRepository.BulkUpsertAsync` inserts an existing owner again (`PK_Owners`). When fixed, add the pipeline case back to `InventoryApi_WhenNotAMember_ShouldReturn404NotFound` (#197)
- Stray `OrganisationId1` column on `OrganisationUsers` from the relationship being configured twice (#198)
- ~~There's no API to add or remove organisation members, and no backfill for organisations created before this change (#199)~~
