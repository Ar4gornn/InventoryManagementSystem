# Contributing

This is a personal project, so the honest expectation first: I am the only maintainer, and I may
decline a change that is fine on its own merits but pulls the project somewhere I do not want it to
go. Open an issue before writing anything large — it costs you nothing and it saves you the work of
a pull request I cannot merge.

Small things — a typo, a broken link, a wrong statement in the README, a failing edge case with a
test that proves it — need no discussion. Send them.

## Getting it running

You need the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) and, for the UI,
[Node 22](https://nodejs.org/). No database to install; SQLite is created on first run.

```bash
git clone https://github.com/Ar4gornn/InventoryManagementSystem.git
cd InventoryManagementSystem
```

The API will not start without a key. Set one:

```bash
export Security__ApiKey="pick-any-secret-for-local-use"     # bash
$env:Security__ApiKey = "pick-any-secret-for-local-use"     # PowerShell
```

Then:

```bash
dotnet run --project InventoryManagementSystem
```

Swagger is at <http://localhost:5180/swagger>. For the UI:

```bash
cd web
npm ci
npm run dev
```

Or run both in containers — see *With Docker instead* in the [README](README.md).

## Before you open a pull request

Both of these must pass. CI runs the same two jobs and will tell you anyway, but locally is faster:

```bash
dotnet test
```

```bash
cd web && npx tsc -b --noEmit && npm run build
```

## The one design rule that is not negotiable

**Stock on hand is derived, never stored.** There is no quantity column; a product's stock is the
sum of the `QuantityDelta` on its movements. The movement log is append-only and a mistake is
corrected by recording a compensating movement, not by editing or deleting the past.

A change that adds a cached quantity column, or that writes stock directly, will be declined even
if it is faster. Everything else in the design follows from this rule, and the tests exist to
defend it. If you think the rule is wrong, that is an issue to argue in, not a pull request.

## Tests

New behaviour needs a test. The suite is xUnit and has three layers, so put yours in the one that
actually exercises what you changed:

| Layer | Where | Use it for |
|---|---|---|
| Service rules, EF Core InMemory | `InventoryManagementSystem.Tests` | Domain rules and validation. |
| Real SQLite, `DataSource=:memory:` | same project, the SQLite test classes | **Anything touching a query.** |
| HTTP, `WebApplicationFactory` | same project, the endpoint test classes | Routing, status codes, middleware, the API key. |

A warning worth more than it looks: **EF Core InMemory is not a relational provider.** It evaluates
LINQ in process, so it will happily run a query that throws
`InvalidOperationException: ... could not be translated` against SQLite in production, and it
ignores check constraints and unique indexes. Any change to a query needs a test against the real
SQLite provider, not just InMemory.

Make a new test fail before you trust it. Break the thing it claims to protect; if it stays green,
it is decoration.

## Style

`.editorconfig` covers formatting — four spaces for C#, two for TypeScript, LF endings, UTF-8. Your
editor should pick it up without configuration. Do not reformat files your change does not touch;
the diff noise buries the actual change.

Match the surrounding code rather than importing a style from elsewhere. Comments explain *why*
something is the way it is; the code already says what it does.

## Commits

[Conventional Commits](https://www.conventionalcommits.org/), imperative subject, 50 characters or
fewer:

```
feat: add a CSV export endpoint
fix: reject a movement that would zero a deleted product
test: cover the duplicate SKU path
docs: correct the movement payload field name
```

Work on a branch — `feat/…`, `fix/…`, `chore/…`, `docs/…` — never straight on `main`. Keep commits
small enough that the subject line is honest about what is in them.

## Security

Do not open a public issue for a vulnerability. [SECURITY.md](SECURITY.md) has the private route
and, just as usefully, a list of the limits this project already knows about.
