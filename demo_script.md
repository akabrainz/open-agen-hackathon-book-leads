# Demo video script — 1–4 minutes (aim ~3:00)

0:00–0:20 — Problem & user
: "Independent book resellers guess what to stock and what price to set. This agent answers: what to stock, at what price, for what margin?"

0:20–0:50 — Search & enrich (live or sample)
: Open the UI, search "bestselling fiction", Top N = 10, Days = 7. Show the enriched list: author, genre, rating, ratings count, description, competitor prices, sources (Google Books API / Open Library API).

0:50–1:50 — Reasoning, not just a list
: Expand one book's trace. Show competitor median, demand score, trend from price history, cost breakdown (purchase + shipping + fees + target margin), floor price vs competitive price, and the Recommend / RecommendWithCaution / DoNotStock decision plus "what would change it" (break-even purchase cost).

1:50–2:30 — Sponsor technology
: Show the status banner: data backend and reasoning provider. Explain: Zetaris is the intended data layer (adapter in `Zetaris.cs`); without keys the app runs on the clearly labelled SQLite fallback, NOT Zetaris. Meterless Nightshift is the primary AI provider (adapter in `Nightshift.cs`); without keys the clearly labelled local fallback runs, NOT Nightshift/NVIDIA. Show the H-MEM-like price-history trend and the passing test for it.

2:30–3:00 — Configurable & reproducible
: Change Top N to 5 and Days to 30, re-run. Mention SAMPLE_MODE=true fallback, at least 3 sample inputs/outputs in `samples/`, and that the repo runs from a clean clone with `dotnet run`. Close with the decision list a reseller can act on today.
