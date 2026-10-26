# Samples

These are SAMPLE_MODE outputs: prices, history and demand are illustrative
sample data, not live market prices. Sample mode returns the fixed sample
catalogue in `SampleData.cs` regardless of the query string — that is a
known demo limitation; live mode (sampleMode=false) searches the actual
query via Google Books + Open Library APIs.

- sample_input_1.json -> sample_output_1.json (daily top 10, 1 day)
- sample_input_2.json -> sample_output_2.json (5 books, 7 days)
- sample_input_3.json -> sample_output_3.json (explicit purchase cost $9.00)

Note on live mode from testing on 2026-10-25: Google Books without an API
key returned HTTP 429 (quota) from this shared environment, so live mode
fell back to Open Library titles/authors only and many books were
`InsufficientData` (no price). Set `GOOGLE_BOOKS_API_KEY` in `.env` for
live prices/ratings, or use SAMPLE_MODE for a reliable demo.
