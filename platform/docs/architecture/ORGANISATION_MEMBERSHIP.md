---
title: "Organisation membership checks"
status: active
workstream: security
milestone: "Security & Auth"
issues: [175, 197, 198]
superseded_by: null
last_reviewed: 2026-10-02
---

# Organisation membership checks

## Context
Endpoints took `organisationId` from the query or body and never checked that the caller belonged to that organisation (#175). Creating an organisation never recorded a member either, so `OrganisationUsers` was always empty. Some list endpoints (`/applications`, `/environments`, `/resource-templates`, `/organisations`) returned rows from every organisation.

Branch `feature/organisation_membership_check`, PR #195 (open, awaiting review). Commits `8686597` (the change) and `1a9279f` (CLAUDE.md note).

## Design
- **Membership is recorded on create.** `CreateOrganisationEndpoint` calls `Organisation.Create(name).AddUser(user.GetUserId())`. The user id is the JWT `sub`, which maps to `ClaimTypes.NameIdentifier` (`Shared/ClaimsPrincipalExtensions.GetUserId`).
- **Repository.** `IOrganisationRepository.IsMemberAsync(id, identityUserId)` and `GetIdsForMemberAsync(identityUserId)` query `OrganisationUsers.OrganisationId`. `Shared/OrganisationMembershipExtensions` wraps both for a `ClaimsPrincipal`.
- **Central filter.** `Shared/OrganisationMembershipFilter` is added to every private group in `CoreEndpoints`, `EngineEndpoints` and `InventoryEndpoints`. It collects organisation ids from:
  - the `organisationId` query value
  - the `organisationId` route value
  - any bound argument with a public `OrganisationId` property of type `Guid`, `string` or `OrganisationId` (found by reflection, cached per type)

  The filter returns 400 `INVALID_ORGANISATION_ID` for an unparseable id and 403 (`TypedResults.Forbid()`) if the caller is not a member of every id. When no id is present, it passes through.
- **Entity-by-id endpoints** load the entity and return 404 when the caller isn't a member of its organisation. This hides whether the entity exists. Covered: credentials, organisations, applications, environments and resource templates (get/update/delete); deployments (get/destroy, via the deployment's application); and inventory get-by-id (cloud resources, cloud secrets, issues, pipelines, repositories, pull requests).
- **`POST /deployments`** treats an application or environment in a foreign organisation as missing (400, same message as a missing id).
- **List endpoints without `organisationId`** filter `GetAll()` in memory to `GetMemberOrganisationIdsAsync(user)`.

## Testing
- `Orchitect.Api.Integration.Tests/OrganisationMembershipIntegrationTests.cs` (20 cases) uses `AuthTokenHelper.AddAuthorisationHeaderForNewUser()` to get an outsider client. `AddAuthorisationHeader()` still returns the shared, cached user that owns everything the other tests create.
- Full suite: `DOTNET_USE_POLLING_FILE_WATCHER=true dotnet test src/Orchitect.Api.Integration.Tests` (150/150 at `1a9279f`). Without the variable, `DeploymentIntegrationTests` can fail on this machine with "configured user limit (128) on the number of inotify instances". That is environmental: each `WithWebHostBuilder` host opens file watchers.

## Decisions
- 403 for an explicit `organisationId` the caller can't use, 404 for an entity id in another organisation. This follows the issue's "403/404" criterion and keeps entity existence private.
- Reflection instead of a marker interface, so the Domain request records (`CreateApplicationRequest`, `CreateEnvironmentRequest`, `CreateResourceTemplateRequest`) and their JSON shapes stay unchanged. Swap to an interface if the reviewer prefers it.
- No data migration. Organisations created before this change have no members and are unreachable until members are added, because the creator was never stored.

## Outstanding
- `PipelineRepository.BulkUpsertAsync` inserts an existing owner again (`PK_Owners`). When fixed, add the pipeline case back to `InventoryApi_WhenNotAMember_ShouldReturn404NotFound` (#197)
- Stray `OrganisationId1` column on `OrganisationUsers` from the relationship being configured twice (#198)
- There's no API to add or remove organisation members, and no backfill for organisations created before this change (not yet raised as an issue)
- `GetAll` filtering for applications, environments and resource templates loads every row before filtering; move it into repository queries (not yet raised as an issue)
- `Shared/ClaimsPrincipalExtensions.GetOrganisationIdValue` and `Extensions/ClaimsPrincipalExtensions.GetOrganisationId` are unused, and they fall back to the user id as an organisation id. Remove them (not yet raised as an issue)

## Working notes for the next agent
- Repo rules in memory: only concise `/// <summary>` docs on interface members, no other code comments; `git add` explicit paths only; `.editorconfig` has `insert_final_newline = false`.
- The worktree is `../Orchitect-org-membership` (from the main checkout), based on `origin/master` at `a1fb809`. Local `master` in the main checkout was 15 commits behind.
- New endpoints must follow the "Organisation membership" paragraph in `platform/CLAUDE.md`.