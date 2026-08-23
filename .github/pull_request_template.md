<!--
Keep this short. The diff says what changed; this says why, and what you ran.
-->

## What and why

<!-- One or two sentences. Link the issue if there is one: Fixes #12 -->

## How it was verified

<!--
Not "should work". What you actually ran, and what it printed. Both must pass:

  dotnet test
  cd web && npx tsc -b --noEmit && npm run build
-->

- [ ] `dotnet test` passes
- [ ] `npx tsc -b --noEmit && npm run build` passes in `web/` — or the change does not touch the UI
- [ ] New behaviour has a test, and I saw that test fail before the fix

## Checks

- [ ] Any query change is tested against **real SQLite**, not only EF Core InMemory
- [ ] Stock is still derived from the movement log — nothing caches or writes a quantity directly
- [ ] No secret, key, or `.env` file in the diff
- [ ] No unrelated reformatting
- [ ] Conventional Commit subject, imperative, 50 characters or fewer
