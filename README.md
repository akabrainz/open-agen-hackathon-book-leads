# open-agen-hackathon-book-leads
For the GENAI Hackathon 2026
Book Inventory Leads — Open Agent Hackathon 2026, Reasoning Architecture
An agent for independent book resellers that answers one decision:
what to stock, at what price, for what margin?
It discovers books via public APIs, enriches them, reasons over competitor
prices, demand and price-history trend, applies an operational cost model,
and returns a ranked, explainable list — not just a top-10 list.
Track: Reasoning Architecture — turn data, knowledge, memory and reasoning
into reliable decisions and actions. Deadline: October 27, 2026, 7:45 PM EDT
(11:45 PM UTC). Team: The Agentic Duo (Ankit Srivastava, Taroona Sharma).
What it does — the screenshot flow, implemented
Search for books (Google Books API + Open Library API, public APIs only)
Get an enriched list: author, reviews/rating, description, price where
available, popularity/demand signals, genre
Calculate the most competitive price from the market (competitor median)
Recommend the best sales price after competitor price and operational costs
No scraping of sites that forbid it. No sold-copies data is claimed — sold
copies are rarely public, so popularity is approximated transparently from
rating + ratings count + price trend, and sample data is always labelled.
Quick start (clean clone, Cursor / VS Code / dotnet CLI)
No IDE is required. The user works in Cursor; the CLI below works there too.
```bash
# .NET 8 SDK required (https://dotnet.microsoft.com/download)
cp .env.example .env   # edit only if you have real sponsor keys; SAMPLE_MODE=true by default in .env.example
dotnet restore
dotnet test
dotnet run --project src/BookInventoryLeads.Api
# Open http://localhost:5000  (or the URL printed by dotnet run)
```
If you do not have `dotnet` on PATH after a local SDK install, use the
full path, e.g. `~/workspace/.dotnet/dotnet run --project src/BookInventoryLeads.Api`.
On the judge's machine, a standard .NET 8 SDK install is enough.
API
`GET /health` — liveness
`GET /api/status` — data backend, reasoning provider, which integrations are live
`POST /api/leads` — JSON `LeadRequest`: `query`, `topN`, `days`,
`sampleMode`, `cost: { purchaseCost?, shippingCost, feePercent, targetMarginPercent }`
`GET /api/leads?query=...&topN=10&days=7&sample=true`
`GET /api/samples` — the 3 sample inputs
Example (sample mode, from the API folder):
```bash
curl -X POST http://localhost:5000/api/leads \
  -H 'Content-Type: application/json' \
  -d '{"query":"bestselling fiction","topN":10,"days":7,"sampleMode":true,"cost":{"shippingCost":3.99,"feePercent":0.12,"targetMarginPercent":0.20}}'
```
Reasoning engine (not a fixed list)
In `src/BookInventoryLeads.Core/Reasoning.cs`:
Normalise/dedupe editions (title + first author), union competitor prices.
Competitor median; demand score = rating (40 pts) + log ratings count
(40 pts) + trend bonus (−10 to +10 pts), clamped 0–100.
Cost model (configurable): purchase cost, shipping, fee % of sale price,
target margin % of sale price. If purchase cost is omitted it is estimated
as 55% of the competitor median and flagged `purchaseCostWasEstimated`.
Floor price = (purchase + shipping) / (1 − fee − margin).
Competitive price = median × 0.98. Break-even purchase cost is computed.
Decision: `Recommend` if competitive ≥ floor; `RecommendWithCaution`
if floor ≤ median × 1.10 and demand ≥ 60; otherwise `DoNotStock`
(flagged/rejected at a competitive price); `InsufficientData` if no price.
Confidence from competitor-price count, ratings count and history points.
Every lead carries a trace: sources, competitor prices, cost breakdown,
demand components, trend, decision, and "what would change it".
Memory engine (Meterless H-MEM-like)
`src/BookInventoryLeads.Core/Memory.cs` — `PriceHistoryStore` (SQLite)
records one competitor-median observation per book per day and derives
trend % from first-to-last in the requested window. Recommendations use
that trend, not just today's snapshot. Conformance tests:
`tests/BookInventoryLeads.Tests/MemoryTests.cs` (trend + single-point cases)
and `ReasoningTests.cs`. Run with `dotnet test`.
Sponsor technology — what is real vs fallback (read this before judging)
Integration	Real adapter	Fallback when keys are missing
Zetaris (data layer)	`Zetaris.cs`: `ZetarisAdapter` POSTs to `ZETARIS_API_URL` with Bearer `ZETARIS_API_KEY`	`SqliteDataLayer` (SQLite + JSON snapshot). This is NOT Zetaris and is labelled "SQLite fallback (not Zetaris)" in every response and in the UI.
Meterless Nightshift (primary AI provider)	`Nightshift.cs`: `NightshiftProvider` calls an OpenAI-compatible `/chat/completions` endpoint at `METERLESS_NIGHTSHIFT_API_URL` with model `METERLESS_NIGHTSHIFT_MODEL` (e.g. an NVIDIA Nemotron model)	`LocalReasoningFallback`: deterministic template explanation. This is NOT Nightshift / NVIDIA and is labelled as such.
NVIDIA	Via Nightshift (Nemotron model above) when configured	Not claimed when the fallback runs. For the separate NVIDIA award, set a Nemotron model and show the model name in the trace/status.
To go fully live, set in `.env` (never commit it; `.env` is gitignored,
`.env.example` has placeholders only): `ZETARIS_API_URL`, `ZETARIS_API_KEY`,
`METERLESS_NIGHTSHIFT_API_URL`, `METERLESS_NIGHTSHIFT_API_KEY`,
`METERLESS_NIGHTSHIFT_MODEL`. `GOOGLE_BOOKS_API_KEY` is optional in principle, but strongly recommended:
tested 2026-10-25, Google Books without a key returned HTTP 429 (quota) from
this shared environment, so live mode then relies on Open Library only and
many books have no price (hence `InsufficientData`). With a key, Google
supplies price/rating as well.
Sample mode & samples
`SAMPLE_MODE=true` (env) or `sampleMode: true` (request) uses
`SampleData.cs` — 6 books with illustrative competitor prices, clearly
labelled sample in the UI, API warnings and the `IsSample` field. If live
APIs return nothing, the pipeline falls back to labelled sample data and
warns, rather than failing or inventing real prices. See `samples/` for
at least 3 saved sample inputs/outputs (generated from sample mode).
Project layout
```
src/BookInventoryLeads.Core/   Models, Reasoning, Memory, Discovery, Zetaris, Nightshift, SampleData
src/BookInventoryLeads.Api/    ASP.NET Core API + wwwroot static UI
tests/BookInventoryLeads.Tests/ xUnit tests (reasoning + memory conformance)
samples/                       sample inputs/outputs
demo_script.md                 1–4 minute demo video flow
```
What still needs real sponsor keys to be fully live
Zetaris URL + key (otherwise SQLite fallback, labelled)
Meterless Nightshift URL + key + Nemotron model name (otherwise local fallback, labelled)
Nothing else is required to run, test or demo the app.
Security / data honesty
No API keys, passwords or tokens are committed. Use env vars; `.env` is gitignored.
No private, proprietary or unauthorised personal data. Only public APIs
and clearly labelled sample/synthetic data.
Sample/estimated values are never presented as real market or sold-copies data.
