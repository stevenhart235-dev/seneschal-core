# Northwind Financial deterministic demo history

Northwind Financial is a fictional cloud-native payments company used for
local product demonstrations. Sprint 14's baseline-history profile populates the existing process-local
audit and activity stores with deterministic Northwind Financial evidence.
It is intended for local demonstrations and is disabled during normal startup.

## Enable the profile

Set the ASP.NET Core configuration value before starting the API:

```powershell
$env:Seneschal__Demo__NorthwindHistory__Enabled = 'true'
dotnet run --project Seneschal.Api
```

The optional seed-version setting defaults to `s14-c6-v1`:

```powershell
$env:Seneschal__Demo__NorthwindHistory__SeedVersion = 's14-c6-v1'
```

Unset the variables to return to the normal empty process-local startup:

```powershell
Remove-Item Env:Seneschal__Demo__NorthwindHistory__Enabled -ErrorAction SilentlyContinue
Remove-Item Env:Seneschal__Demo__NorthwindHistory__SeedVersion -ErrorAction SilentlyContinue
```

## Time and identity behavior

The loader captures one UTC anchor when the process starts. All 400
timestamps are offsets from that anchor and cover slightly more than 14 days.
Tests supply a fixed clock, so the same anchor and seed version produce
equivalent records.

Seeded identifiers use this shape:

```text
northwind-{seed-version}-{workload-key}-{sequence}
```

The version, workload, and deterministic ordinal make IDs unique without
random GUIDs. The loader is idempotent within a process and also skips a seed
record if its ID already exists in the target audit store.

## Reset behavior and scope

Audit and activity stores remain in memory. Restarting the API resets them and
creates a fresh history relative to the new startup anchor. Page refreshes do
not regenerate data.

This profile seeds:

- 400 baseline audit decisions and matching activity aggregates;
- 14 days of evidence relative to the process startup anchor;
- seeded identities from the configured catalog;
- healthy business-hour and overnight automation;
- sparser weekend activity;
- limited denial and approval-required evidence;
- observable activity across Azure, GitHub, Terraform, Kubernetes, OpenAI,
  PostgreSQL, Slack, Microsoft 365, and custom capabilities.

It does not seed approval records, approval lifecycle chains, incidents,
governance windows, hero-scenario timelines, or external integrations.
Historical approval-required decisions therefore describe the policy result
without claiming that an approval request was created.

## Investigate the seeded environment

After starting the API, open `http://localhost:5077/dashboard` and follow the
standard workflow:

```text
Dashboard
    ↓
Technology Explorer
    ↓
Capability Activity
    ↓
Decision Trace
    ↓
Audit Trail
```

The catalog and policies are loaded from `Seneschal.Api/Policies/`. The
history profile adds operational evidence; it does not replace or mutate those
configuration files.
