# Security policy

## Reporting a vulnerability

Report privately, not in a public issue.

Use **[Report a vulnerability](https://github.com/Ar4gornn/InventoryManagementSystem/security/advisories/new)**
on the Security tab. That opens a private advisory only you and I can read.

If private reporting is unavailable to you, open a normal issue that says a security report is
waiting and gives **no** reproduction details, and I will open a private channel from there.

Please include: what an attacker gets, the smallest set of steps that shows it, and the commit or
version you tested. A working request or `curl` line is worth more than a description of one.

I will acknowledge within seven days. This is a personal project with one maintainer — there is no
bounty, no paid support, and no response-time guarantee beyond that.

## Supported versions

`main` only. There are no release branches and no backports; a fix lands on `main` and that is the
supported version.

## What this project is, so you can judge the risk yourself

This is a portfolio project. It is not a hardened product and nothing here should hold real
inventory data without work on top of it. Being specific about the limits is more useful than a
badge, so:

- **Authentication is a single shared API key**, sent as `X-Api-Key` and compared against
  `Security:ApiKey`. There are no user accounts, no roles, and no per-caller identity. Anyone
  holding the key can do anything a write can do.
- **Reads are unauthenticated by design.** `GET` on products, categories, and movement history is
  open. Do not put data in it that you would not publish.
- **There is no default key and no fallback.** The application refuses to start without
  `Security:ApiKey` set. This is deliberate: a default in source becomes the real key of every
  deployment that forgot to change it.
- **No TLS.** The app serves plain HTTP and `compose.yaml` publishes plain HTTP. The API key
  crosses the wire in a header in cleartext. Anything exposed beyond localhost needs a reverse
  proxy terminating TLS in front of it.
- **No rate limiting and no lockout.** Nothing slows down a caller brute-forcing the key.
- **The key is stored in the browser's `localStorage`** by the web UI, so it is readable by any
  script running on that origin.
- **SQLite, single file, no encryption at rest.** The compose volume holds it unencrypted.
- **CORS is an explicit allow-list** read from `Cors:AllowedOrigins`, never a wildcard, and
  `UseCors` runs before the key check so that a preflight — which carries no key — is answered
  rather than rejected.

Those are known and accepted properties of a demo, not vulnerabilities. Reports that restate them
will be closed with a pointer to this list.

## What is worth reporting

- Any way to change data without the API key, or to make the key check pass without the key.
- Any way to read something the read endpoints are not meant to expose — another row, the
  filesystem, the key itself, or environment variables.
- SQL injection, or a query that escapes the parameterisation EF Core does.
- Any path that drives stock negative, or that lets a stored quantity disagree with the sum of the
  movement log. Stock is derived and is never written directly; a way to break that is a real bug.
- A dependency with a known CVE that this project actually reaches.
- A secret committed to the repository or baked into an image.
