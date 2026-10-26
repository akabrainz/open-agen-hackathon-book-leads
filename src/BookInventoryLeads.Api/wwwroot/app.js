// Book Inventory Leads UI — The Agentic Duo.
// All results come from the API. Nothing is hardcoded except the form defaults
// and the quick-sample presets fetched from /api/samples.
(function () {
  'use strict';
  var form = document.getElementById('lead-form');
  var runBtn = document.getElementById('run-btn');
  var resetBtn = document.getElementById('reset-btn');
  var leadsEl = document.getElementById('leads');
  var summaryEl = document.getElementById('results-summary');
  var warningsEl = document.getElementById('warnings');
  var errorEl = document.getElementById('error');
  var emptyEl = document.getElementById('empty-state');
  var loadingEl = document.getElementById('loading');
  var noResultsEl = document.getElementById('no-results');

  function esc(value) {
    return String(value == null ? '' : value)
      .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
  }
  function money(value) {
    if (value == null || value === '') return '—';
    return '$' + Number(value).toFixed(2);
  }
  function pct(value, digits) {
    if (value == null) return '—';
    return (Number(value) * 100).toFixed(digits == null ? 0 : digits) + '%';
  }
  function ratingText(lead) {
    if (lead.rating == null) return 'No rating yet';
    var count = lead.ratingsCount ? ' (' + lead.ratingsCount.toLocaleString() + ' ratings)' : '';
    return Number(lead.rating).toFixed(2) + ' ★' + count;
  }
  function badgeClass(status) {
    if (status === 'Recommend') return 'badge-recommend';
    if (status === 'RecommendWithCaution') return 'badge-caution';
    if (status === 'DoNotStock') return 'badge-donotstock';
    return 'badge-insufficient';
  }
  function badgeLabel(status) {
    if (status === 'RecommendWithCaution') return 'Recommend with caution';
    if (status === 'DoNotStock') return 'Do not stock';
    if (status === 'InsufficientData') return 'Insufficient data';
    return status || '—';
  }
  function setHidden(el, hidden) { el.hidden = hidden; }
  function showError(message) {
    errorEl.textContent = message;
    setHidden(errorEl, false);
  }
  function clearResults() {
    leadsEl.innerHTML = '';
    summaryEl.innerHTML = '';
    warningsEl.innerHTML = '';
    setHidden(summaryEl, true);
    setHidden(warningsEl, true);
    setHidden(errorEl, true);
    setHidden(noResultsEl, true);
  }

  function cardHtml(lead, index) {
    var genres = (lead.genres || []).map(function (g) { return '<span class="chip">' + esc(g) + '</span>'; }).join('');
    var prices = (lead.competitorPrices || []).map(function (p) {
      return '<li>' + esc(p.source || 'Source') + ': ' + money(p.amount) + ' ' + esc(p.currency || '') + (p.isSample ? ' <span class="sample-tag">Sample</span>' : '') + '</li>';
    }).join('');
    var trace = (lead.trace || []).map(function (t) { return '<li>' + esc(t) + '</li>'; }).join('');
    var demand = Math.max(0, Math.min(100, Number(lead.demandScore || 0)));
    var trend = Number(lead.trendPercent || 0);
    var trendText = (trend >= 0 ? '+' : '') + trend.toFixed(1) + '% over ' + (lead.trendDataPoints || 0) + ' point(s)';
    return '<article class="lead-card">' +
      '<div class="lead-top"><div><span class="rank">#' + (index + 1) + '</span>' +
      (lead.isSample ? ' <span class="sample-tag">Sample</span>' : '') +
      '<h3>' + esc(lead.title) + '</h3>' +
      '<p class="authors">' + esc((lead.authors || []).join(', ') || 'Author unknown') + '</p></div>' +
      '<span class="badge ' + badgeClass(lead.status) + '">' + esc(badgeLabel(lead.status)) + '</span></div>' +
      '<div class="chips">' + genres + '</div>' +
      (lead.description ? '<p class="description">' + esc(lead.description) + '</p>' : '') +
      '<div class="meta-grid">' +
        '<div><span class="label">Rating</span><strong>' + esc(ratingText(lead)) + '</strong></div>' +
        '<div><span class="label">Demand score</span><strong>' + demand.toFixed(0) + ' / 100</strong><div class="meter" aria-hidden="true"><span style="width:' + demand + '%"></span></div></div>' +
        '<div><span class="label">Price trend</span><strong>' + esc(trendText) + '</strong></div>' +
      '</div>' +
      '<div class="price-hero"><div><span class="label">Recommended sales price</span><br><span class="big">' + money(lead.recommendedPrice) + '</span></div>' +
      '<div><span class="label">Competitor median</span><br><strong>' + money(lead.competitorMedian) + '</strong><br><span class="hint">Competitive: ' + money(lead.competitivePrice) + '</span></div></div>' +
      '<table class="cost-table"><caption class="hint" style="text-align:left">Cost breakdown — floor price is what you need to hit your target margin after fees.</caption>' +
      '<tr><th>Purchase cost used</th><td>' + money(lead.purchaseCostUsed) + (lead.purchaseCostWasEstimated ? ' <em>(estimated)</em>' : '') + '</td></tr>' +
      '<tr><th>Shipping</th><td>' + money(lead.shippingCost) + '</td></tr>' +
      '<tr><th>Fee</th><td>' + pct(lead.feePercent) + '</td></tr>' +
      '<tr><th>Target margin</th><td>' + pct(lead.targetMarginPercent) + '</td></tr>' +
      '<tr><th>Floor price</th><td>' + money(lead.floorPrice) + '</td></tr>' +
      '<tr><th>Break-even purchase cost</th><td>' + money(lead.breakEvenPurchaseCost) + '</td></tr>' +
      '<tr><th>Confidence</th><td>' + (lead.confidence == null ? '—' : lead.confidence + '%') + '</td></tr></table>' +
      '<details><summary>Trace &amp; explanation</summary>' +
      (lead.explanation ? '<p>' + esc(lead.explanation) + '</p>' : '') +
      (lead.whatWouldChangeIt ? '<p><em>' + esc(lead.whatWouldChangeIt) + '</em></p>' : '') +
      (prices ? '<p><strong>Competitor prices</strong></p><ul class="trace-list">' + prices + '</ul>' : '') +
      (trace ? '<p><strong>Trace</strong></p><ul class="trace-list">' + trace + '</ul>' : '') +
      '</details></article>';
  }

  function render(data) {
    clearResults();
    var leads = data.leads || [];
    summaryEl.innerHTML = '<strong>' + leads.length + ' lead(s)</strong> for “' + esc(data.query || '') + '” · ' +
      'Top ' + esc(data.topN) + ' · ' + esc(data.days) + ' day(s) · ' +
      'Data: ' + esc(data.dataBackend || '') + ' · Provider: ' + esc(data.reasoningProvider || '') +
      (data.sampleMode ? ' · <span class="sample-tag">Sample mode</span>' : '') +
      (data.sampleFallbackUsed ? ' · Sample fallback used' : '');
    setHidden(summaryEl, false);
    if (data.warnings && data.warnings.length) {
      warningsEl.innerHTML = data.warnings.map(function (w) { return '<p>' + esc(w) + '</p>'; }).join('');
      setHidden(warningsEl, false);
    }
    if (!leads.length) { setHidden(noResultsEl, false); return; }
    leadsEl.innerHTML = leads.map(cardHtml).join('');
  }

  async function loadStatus() {
    try {
      var res = await fetch('/api/status');
      if (!res.ok) throw new Error('Status unavailable');
      var s = await res.json();
      document.getElementById('status-data').textContent = s.dataBackend || '—';
      document.getElementById('status-reasoning').textContent = s.reasoningProvider || '—';
      document.getElementById('status-sample-env').textContent = s.sampleModeEnv ? 'On' : 'Off';
    } catch (e) {
      document.getElementById('status-data').textContent = 'Unavailable';
      document.getElementById('status-reasoning').textContent = 'Unavailable';
    }
  }

  async function loadPresets() {
    var wrap = document.getElementById('preset-buttons');
    try {
      var res = await fetch('/api/samples');
      if (!res.ok) return;
      var data = await res.json();
      (data.inputs || []).forEach(function (input) {
        var btn = document.createElement('button');
        btn.type = 'button';
        btn.textContent = input.name;
        btn.addEventListener('click', function () {
          document.getElementById('query').value = input.query || '';
          document.getElementById('topN').value = input.topN || 10;
          document.getElementById('days').value = input.days || 7;
          document.getElementById('sampleMode').checked = !!input.sampleMode;
          document.getElementById('purchaseCost').value = input.purchaseCost == null ? '' : input.purchaseCost;
          document.getElementById('shippingCost').value = input.shippingCost == null ? 3.99 : input.shippingCost;
          document.getElementById('feePercent').value = input.feePercent == null ? 12 : Math.round(input.feePercent * 100);
          document.getElementById('margin').value = input.targetMarginPercent == null ? 20 : Math.round(input.targetMarginPercent * 100);
          form.requestSubmit();
        });
        wrap.appendChild(btn);
      });
    } catch (e) { /* presets are optional; the form still works */ }
  }

  form.addEventListener('submit', async function (e) {
    e.preventDefault();
    clearResults();
    setHidden(emptyEl, true);
    setHidden(loadingEl, false);
    runBtn.disabled = true;
    runBtn.textContent = 'Generating…';
    var purchase = document.getElementById('purchaseCost').value;
    var body = {
      query: document.getElementById('query').value.trim(),
      topN: parseInt(document.getElementById('topN').value, 10) || 10,
      days: parseInt(document.getElementById('days').value, 10) || 7,
      sampleMode: document.getElementById('sampleMode').checked,
      cost: {
        shippingCost: parseFloat(document.getElementById('shippingCost').value) || 0,
        feePercent: (parseFloat(document.getElementById('feePercent').value) || 0) / 100,
        targetMarginPercent: (parseFloat(document.getElementById('margin').value) || 0) / 100
      }
    };
    if (purchase !== '') body.cost.purchaseCost = parseFloat(purchase);
    try {
      var res = await fetch('/api/leads', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body)
      });
      if (!res.ok) throw new Error('The API returned ' + res.status + '. Please try again.');
      var data = await res.json();
      render(data);
    } catch (err) {
      showError('Could not generate leads: ' + err.message + ' Check that the API is running, then try again.');
    } finally {
      setHidden(loadingEl, true);
      runBtn.disabled = false;
      runBtn.textContent = 'Generate leads';
      document.getElementById('results').scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
  });

  resetBtn.addEventListener('click', function () {
    form.reset();
    clearResults();
    setHidden(emptyEl, false);
    document.getElementById('query').focus();
  });

  loadStatus();
  loadPresets();
})();
