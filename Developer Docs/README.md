# OpenOrderSystem Developer Docs

The reference for how OpenOrderSystem-Core works **as the code stands today**. If you need to know
what a service does, how to call it, or what will bite you, it should be here.

## How this folder is organized

The folder tree mirrors the source tree. Docs for `Services/Catalog/CatalogService.cs` live under
`Developer Docs/Services/Catalog/`. Each folder has a `README.md` that gives an overview of
that area and links to its topic files.

| Area | Start here |
|---|---|
| Catalog (products, variants, options, media, groups, snapshots) | [Services/Catalog](Services/Catalog/README.md) |
| Ordering (carts, order placement, queue, stages) | [Services/Ordering](Services/Ordering/README.md) |
| Email (sending, SMTP security, templates, email settings and the keyring) | [Services/EmailService](Services/EmailService/README.md) |

## Developer Docs vs. Design Docs

- **`/Design Docs`** (repository root) records *intent*: designs, open questions, and the reasons
  behind decisions. It can describe things that are not built yet.
- **`Developer Docs`** (this folder) records *what the code does now*: method contracts, behavior,
  edge cases, and known issues. When the two disagree about current behavior, the code wins, and
  this folder should be updated to match it.

## Writing conventions

- **One topic per file.** Prefer several short files over one long one, and link between them.
- **Mirror the source tree.** New docs go in the folder matching the code they describe, with a
  `README.md` index per folder.
- **Document behavior, not just signatures.** Cover null handling, return statuses, side effects
  (snapshots, cache updates, cascades), and anything surprising.
- **Keep a "Known issues" section** wherever there are gaps or bugs, and remove items when they are fixed.
- **Update docs in the same change as the code.** A stale doc is worse than a missing one.
- **Status markers:** if something is designed but not implemented, say so explicitly. Do not
  document planned behavior as though it exists.
