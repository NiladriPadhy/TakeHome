<!--
  Sync Impact Report
  ==================
  Version change: 0.0.0 → 1.0.0
  Modified principles: N/A (initial creation)
  Added sections:
    - Core Principles (6 principles)
    - Technology Constraints
    - Development Workflow
    - Governance
  Removed sections: N/A
  Templates requiring updates:
    - .specify/templates/plan-template.md ✅ reviewed (no changes needed)
    - .specify/templates/spec-template.md ✅ reviewed (no changes needed)
    - .specify/templates/tasks-template.md ✅ reviewed (no changes needed)
    - .specify/templates/checklist-template.md ✅ reviewed (no changes needed)
  Follow-up TODOs: None
-->

# MedDevice Desktop Platform Constitution

## Core Principles

### I. Contract-First API Design

All inter-service and inter-process communication MUST be defined by
explicit, versioned API contracts before implementation begins.

- REST endpoints MUST use OpenAPI specifications as the source of truth.
- Shared data models MUST reside in a dedicated Domain class library
  consumed by all projects that need them.
- Breaking changes to contracts MUST increment the API major version and
  MUST NOT be deployed without a documented migration path.
- Request/response schemas MUST use strongly-typed C# records or classes
  with nullable annotations enabled.

**Rationale**: Medical device integrations demand deterministic data
exchange; implicit contracts lead to silent data corruption.

### II. Domain Isolation

Domain logic MUST live in framework-agnostic class libraries that carry
zero dependency on ASP.NET Core, WPF, or MAUI.

- The `Domain` library MUST contain all business rules, validation, and
  entity definitions.
- Application hosts (API, Desktop, MAUI) MUST depend on `Domain`; the
  reverse is forbidden.
- Domain libraries MUST be independently compilable and testable without
  launching a host process.

**Rationale**: Reusable domain logic enables consistent behavior across
desktop, cross-platform, and API surfaces without duplication.

### III. MVVM & Presentation Separation

All UI applications (WPF and MAUI) MUST follow the Model-View-ViewModel
pattern with no business logic in code-behind files.

- Views MUST bind to ViewModels exclusively via data bindings and
  commands (ICommand / RelayCommand).
- ViewModels MUST receive dependencies through constructor injection.
- HTTP communication MUST use `HttpClient` injected via DI and invoked
  asynchronously (async/await throughout the call chain).
- Code-behind files MUST contain only framework-required initialization
  (e.g., `InitializeComponent()`).

**Rationale**: MVVM enables deterministic unit testing of presentation
logic and facilitates the WPF → MAUI migration path.

### IV. Test-Driven Quality

Automated testing MUST cover unit and integration levels for every
deliverable feature.

- Unit tests MUST validate domain logic and ViewModel behavior in
  isolation using mocks/stubs for external dependencies.
- Integration tests MUST exercise API endpoints end-to-end using
  `WebApplicationFactory<T>` (or equivalent test host).
- Test projects MUST mirror the source structure
  (e.g., `tests/Domain.Tests/`, `tests/IngestionApi.IntegrationTests/`).
- New public APIs MUST ship with at least one positive and one negative
  test case.

**Rationale**: Medical software quality expectations demand provable
correctness; manual testing alone is insufficient.

### V. Cross-Platform Portability

The system MUST support Windows (primary) and macOS (via Mac Catalyst)
through .NET MAUI for new UI surfaces.

- New UI features MUST target .NET MAUI with `net8.0-windows` and
  `net8.0-maccatalyst` TFMs unless explicitly scoped to Windows-only.
- Platform-specific code MUST be isolated behind interfaces or partial
  classes with platform folders.
- The existing WPF app MUST remain functional on Windows; MAUI does not
  replace it immediately but provides the cross-platform path.
- Shared UI logic (ViewModels, services) MUST compile against
  `net8.0` (no platform suffix) to maximize reuse.

**Rationale**: Supporting macOS alongside Windows broadens clinical
deployment options while leveraging a single .NET codebase.

### VI. Dependency Injection & Simplicity

All application hosts MUST use the built-in
`Microsoft.Extensions.DependencyInjection` container.

- Services MUST be registered with appropriate lifetimes (Scoped for
  request-bound, Singleton for stateless/shared, Transient only when
  justified).
- Constructor injection is the sole sanctioned injection mechanism;
  service locator patterns are forbidden.
- Abstractions MUST be introduced only when there is a concrete second
  consumer or a testability requirement—no speculative interfaces.
- Prefer the simplest solution; add complexity only when a requirement
  demands it (YAGNI).

**Rationale**: Explicit dependency graphs improve testability and make
the system comprehensible to new team members.

## Technology Constraints

| Concern | Standard |
|---------|----------|
| Runtime | .NET 8 (LTS) |
| Desktop (legacy) | WPF (`net8.0-windows`) |
| Desktop (new) | .NET MAUI (`net8.0-windows`, `net8.0-maccatalyst`) |
| API framework | ASP.NET Core Minimal APIs |
| Serialization | System.Text.Json (Newtonsoft MUST be replaced) |
| HTTP client | `IHttpClientFactory` / typed `HttpClient` via DI |
| Testing | xUnit + FluentAssertions + NSubstitute (or Moq) |
| Integration tests | `Microsoft.AspNetCore.Mvc.Testing` |
| Logging | `Microsoft.Extensions.Logging` abstractions |
| Nullable refs | Enabled project-wide (`<Nullable>enable</Nullable>`) |
| Implicit usings | Enabled (`<ImplicitUsings>enable</ImplicitUsings>`) |

## Development Workflow

1. **Branch per feature**: Work MUST occur on a feature branch created
   from `main`.
2. **Build before commit**: `dotnet build` MUST pass with zero warnings
   treated as errors before any commit.
3. **Tests before merge**: All unit and integration tests MUST pass
   (`dotnet test`) before a branch is eligible for merge.
4. **Code review**: Every merge into `main` MUST be reviewed by at least
   one team member who did not author the change.
5. **Incremental delivery**: Features SHOULD be decomposed into
   independently mergeable slices aligned with user stories.

## Governance

This constitution is the authoritative reference for architectural and
engineering decisions in the MedDevice Desktop Platform. It supersedes
informal conventions and ad-hoc practices.

- **Amendment process**: Any team member MAY propose an amendment via a
  pull request modifying this file. Adoption requires review and approval
  by at least two maintainers.
- **Versioning**: This document follows Semantic Versioning. MAJOR for
  principle removals/redefinitions, MINOR for additions/expansions,
  PATCH for clarifications.
- **Compliance review**: Pull requests MUST reference which principles
  apply. Reviewers MUST verify compliance as part of the review checklist.
- **Exceptions**: Deviations MUST be documented inline with rationale and
  tracked in the Complexity Tracking section of the implementation plan.

**Version**: 1.0.0 | **Ratified**: 2026-05-20 | **Last Amended**: 2026-05-20
