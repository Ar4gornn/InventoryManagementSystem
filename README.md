# InventoryManagementSystem

A .NET 8 REST API for tracking stock. Products, categories, and an append-only movement log.

The design decision everything else follows from: **there is no quantity column.** Stock on hand is
the sum of a product's movements, so the history is the only source of truth and the two can never
drift apart. A mistake is corrected by recording a compensating movement, not by editing the past.

## Stack

| | |
|---|---|
| Runtime | .NET 8 (`net8.0`) |
| Web | ASP.NET Core, attribute-routed controllers, Swagger |
| Data | EF Core 8.0.30 + SQLite, migrations applied on startup |
| Tests | xUnit — 63 tests, most against real SQLite |

## Quickstart

Needs the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0). No database to install.

```bash
git clone https://github.com/Ar4gornn/InventoryManagementSystem.git
cd InventoryManagementSystem
```

The API **will not start without an API key.** There is no default — a fallback in source becomes
the key of every deployment that forgot to set one.

```bash
export Security__ApiKey="pick-any-secret-for-local-use"     # bash
$env:Security__ApiKey = "pick-any-secret-for-local-use"     # PowerShell
```

Then:

```bash
dotnet run --project InventoryManagementSystem
```

Open <http://localhost:5180/swagger>. The database is created, migrated and seeded with three
categories and three products on first run.

Run the tests:

```bash
dotnet test
```

### With Docker instead

```bash
cp .env.example .env      # then set INVENTORY_API_KEY in it
docker compose up --build
```

The API is on <http://localhost:8080>. A named volume keeps the SQLite file when the container is
replaced.

## API

Reads are open. **Everything that changes data needs an `X-Api-Key` header** — Swagger UI has a
box for it under *Authorize*.

| Method | Route | Notes |
|---|---|---|
| `GET` | `/api/products` | Paged. `?page` `?pageSize` `?search` `?categoryId` |
| `GET` | `/api/products/{id}` | |
| `POST` | `/api/products` | 409 on a duplicate SKU |
| `PUT` | `/api/products/{id}` | SKU is immutable |
| `DELETE` | `/api/products/{id}` | 409 if the product has any stock history |
| `POST` | `/api/products/import` | CSV upload, per-row results |
| `GET` | `/api/products/{id}/movements` | History, oldest first, with a running total |
| `POST` | `/api/products/{id}/movements` | 400 if it would take stock below zero |
| `GET` | `/api/products/{id}/stock` | Current stock on hand |
| `GET` | `/api/categories` | Paged. `?page` `?pageSize` `?search` |
| `GET` | `/api/categories/{id}` | |
| `POST` | `/api/categories` | 409 on a duplicate name |
| `PUT` | `/api/categories/{id}` | |
| `DELETE` | `/api/categories/{id}` | 409 while any product still belongs to it |

### Recording stock

Quantity is written the way a person would say it. To remove five units you send `5` with type
`Out`, never `-5` — the sign is the type's job, and an `Out` with a negative quantity is rejected
rather than quietly reinterpreted.

```bash
curl -X POST http://localhost:5180/api/products/1/movements \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: $Security__ApiKey" \
  -d '{"type":"Out","quantity":5,"reason":"Sales order 1099"}'
```

`Adjustment` is the one signed case, because a stock-count correction has a direction of its own.

**Stock is never allowed below zero.** A movement that would overdraw is rejected with a 400 naming
the balance, and nothing is written. It is never clamped — silently recording a smaller movement
would make the history disagree with what the caller was told happened. Reaching exactly zero is
fine.

### CSV import

Header: `sku,name,description,category,quantity`. Category is matched by name.

Rows are independent. One bad line does not abort the file — valid rows import and the response
says which lines failed and why, numbered to match a text editor:

```json
{
  "totalRows": 5,
  "importedCount": 2,
  "failedCount": 3,
  "rows": [
    { "line": 2, "sku": "IM-001", "imported": true,  "error": null },
    { "line": 4, "sku": "",       "imported": false, "error": "Sku is required." },
    { "line": 5, "sku": "IM-004", "imported": false, "error": "Unknown category 'Nope'." }
  ]
}
```

An imported opening quantity becomes a stock movement, so it has the same provenance as any other
stock.

## Architecture

Four layers in one project. `Domain/Entities` holds the model; `Persistence` is the EF Core
boundary; `Services` holds the rules; `WebApi/Controllers` does HTTP and nothing else. `Contracts`
holds the DTOs, so entities never cross the wire and the SKU cannot be changed by an update simply
because the entity has a setter.

Rules that live in the schema, not just in code: unique SKU and category name, a check constraint
rejecting a zero movement, a composite index on `(ProductId, OccurredAt)` matching how the stock
aggregate queries, and a `Restrict` foreign key so deleting a category can never orphan products.

Errors are raised as a `DomainException` carrying the status code the API should answer with, and
translated centrally. Unexpected exceptions become a 500 whose body says nothing about the
internals.

## Testing notes

Most tests run against real SQLite rather than the EF InMemory provider, deliberately. InMemory is
not relational: it evaluates LINQ in process, so it cannot catch a query that fails to translate to
SQL, and it ignores check constraints and unique indexes. A real bug got through it during
development — a list endpoint that ordered by a projected DTO passed every InMemory test and
returned a 500 against SQLite.

`StockMovementConcurrencyTests` covers two simultaneous withdrawals. Read its remarks before
changing it: it passes, but it **also passes with the transaction isolation weakened**, because
SQLite's file locking already serialises a reader against an uncommitted writer. The serializable
transaction is there for a provider with row-level MVCC, and that test would not catch its removal.

## Known limitations

- One shared API key, so this is authorization without authentication — there is no notion of who
  is calling. Real users and roles would need an identity system.
- SQLite only. The code is provider-agnostic apart from the connection string, but nothing has been
  run against another database.
- No rate limiting.
- The movement log is never compacted, so stock is recomputed from the full history on every read.
  Fine at this size; a snapshot table would be the answer at millions of rows.

## Roadmap

- [ ] JWT authentication with users and roles
- [ ] Supplier and purchase-order flow
- [ ] Low-stock alerts driven by a reorder level per product
- [ ] PostgreSQL support and a compose profile for it

## License

MIT — see [LICENSE](LICENSE).
