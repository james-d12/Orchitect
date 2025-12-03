# Organisation Domain Model

## Overview

The Organisation domain model implements a proper aggregate root pattern where `Organisation` is the aggregate root that contains and manages `OrganisationUser` and `OrganisationTeam` entities.

## Aggregate Structure

### Organisation (Aggregate Root)

The `Organisation` is the aggregate root that:
- Contains a collection of `OrganisationUser` entities
- Contains a collection of `OrganisationTeam` entities
- Enforces all business invariants
- Manages the lifecycle of users and teams

**Properties:**
- `Id: OrganisationId`
- `Name: string`
- `Users: List<OrganisationUser>`
- `Teams: List<OrganisationTeam>`
- `CreatedAt: DateTime`
- `UpdatedAt: DateTime`

**Methods:**
- `Create(string name)` - Static factory method to create a new organisation
- `Update(string name)` - Updates the organisation name
- `AddUser(string identityUserId)` - Adds a user to the organisation (enforces uniqueness)
- `RemoveUser(OrganisationUserId userId)` - Removes a user and cleans up from all teams
- `AddTeam(string name)` - Adds a team to the organisation (enforces unique names, case-insensitive)
- `RemoveTeam(OrganisationTeamId teamId)` - Removes a team from the organisation
- `AddUserToTeam(OrganisationUserId userId, OrganisationTeamId teamId)` - Adds a user to a team
- `RemoveUserFromTeam(OrganisationUserId userId, OrganisationTeamId teamId)` - Removes a user from a team

### OrganisationUser (Child Entity)

Represents a user within an organisation.

**Properties:**
- `Id: OrganisationUserId`
- `IdentityUserId: string` - Reference to the identity system user
- `OrganisationId: OrganisationId`

**Methods:**
- `Create(string identityUserId, OrganisationId organisationId)` - Static factory method

### OrganisationTeam (Child Entity)

Represents a team within an organisation. Teams track which users are members.

**Properties:**
- `Id: OrganisationTeamId`
- `OrganisationId: OrganisationId`
- `Name: string`
- `UserIds: List<OrganisationUserId>` - List of user IDs that are members of this team
- `CreatedAt: DateTime`
- `UpdatedAt: DateTime`

**Methods:**
- `Create(string name, OrganisationId organisationId)` - Static factory method
- `AddUser(OrganisationUserId userId)` - Internal method to add a user (called by Organisation)
- `RemoveUser(OrganisationUserId userId)` - Internal method to remove a user (called by Organisation)

## Business Invariants

1. **User Uniqueness**: Each user (identified by `IdentityUserId`) must be unique within an organisation
2. **Team Name Uniqueness**: Each team name must be unique within an organisation (case-insensitive comparison)
3. **User-Team Relationship**: Users can belong to multiple teams within the same organisation
4. **Cascade Removal**: When a user is removed from an organisation, they are automatically removed from all teams
5. **Timestamp Updates**: All operations that modify the organisation must update the `UpdatedAt` timestamp

## Database Structure

### Required Tables

1. **Organisations**
   - Primary Key: `Id`
   - Unique Index: `Name`

2. **OrganisationUsers**
   - Primary Key: `Id`
   - Foreign Key: `OrganisationId` → `Organisations.Id`
   - Unique Index: (`IdentityUserId`, `OrganisationId`)

3. **OrganisationTeams**
   - Primary Key: `Id`
   - Foreign Key: `OrganisationId` → `Organisations.Id`
   - Unique Index: (`Name`, `OrganisationId`)

4. **OrganisationTeamUsers** (Join Table)
   - Composite Primary Key: (`OrganisationTeamId`, `OrganisationUserId`)
   - Foreign Key: `OrganisationTeamId` → `OrganisationTeams.Id`
   - Foreign Key: `OrganisationUserId` → `OrganisationUsers.Id`
   - Cascade delete on both foreign keys

## Design Decisions

### Why UserIds instead of OrganisationUser entities in Teams?

The `OrganisationTeam` uses `List<OrganisationUserId>` instead of `List<OrganisationUser>` to:
- Avoid circular references and data duplication when loading from the database
- Maintain clear ownership: `Organisation` owns the `Users` collection, teams just reference them
- Support efficient queries (e.g., "find all teams for a user")
- Preserve aggregate boundaries: teams reference users by ID, not by entity

### Aggregate Root Pattern

All modifications to users and teams must go through the `Organisation` aggregate root methods. This ensures:
- Business invariants are always enforced
- Consistency is maintained across the aggregate
- The aggregate boundary is respected

## Implementation Status

### Completed
- ✅ Domain model structure with collections
- ✅ Business logic methods on Organisation
- ✅ Invariant enforcement (uniqueness checks)
- ✅ Cascade removal logic

### Pending
- ⏳ EF Core configuration for join table (`OrganisationTeamUsers`)
- ⏳ Repository updates to handle the new structure
- ⏳ Migration to update database schema

## Notes

- The domain model uses immutable records with `with` expressions for updates
- All business logic is encapsulated in the domain entities
- The aggregate root pattern ensures transactional consistency
- Team membership is stored as a list of IDs, not full entities, to avoid duplication

