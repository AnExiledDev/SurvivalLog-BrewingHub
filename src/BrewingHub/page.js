// Brewing Hub's web half. Runs in Root.html, the document every game page iframe sits in. Adds a fourth column, a
// quality line, nudges and buttons to the game's own brewing barrel screen, and shows a short toast for what each press
// did. Talks to the plugin through vuplex.postMessage("slmod|brewinghub|<verb>") and is driven by window.__slbrewinghub.
(function () {
  if (window.__slbrewinghub) return;

  var PREFIX = 'slmod|brewinghub|';
  var STYLE_ID = 'bh-style';
  var TOAST_STYLE_ID = 'bh-toast-style';
  // Marks everything drawn on the brewing screen, so it can all be taken off again.
  var OURS = 'bh-ours';
  var BUSY_TIMEOUT_MS = 5000;
  var TOAST_MS = 3500;
  var URGENT_HOURS = 24;
  var TIER_CLASS = ['poor', 'avg', 'strong'];
  var TABS = [['now', 'tabNow'], ['recipes', 'tabRecipes'], ['ings', 'tabIngs'], ['barrels', 'tabBarrels']];
  // Barrel states as the game numbers them.
  var IDLE = 0;
  var BREWING = 1;
  var DONE = 2;

  var cat = { ings: {}, ingList: [], drinks: [], items: {} };
  var st = { show: false };
  var ui = { tab: 'now', search: '', rawOnly: true, haveOnly: false, sort: 'quality' };
  var toast = null;
  var toastTimer = 0;
  var isBusy = false;
  var busyTimer = 0;
  var hasReportedPageAccess = false;

  // The plugin hands the words over before its first call, so a missing one only shows its key.
  function say(key) {
    var words = window.__slbrewinghubWords || {};
    var text = words[key] || key;

    for (var i = 1; i < arguments.length; i++) text = text.split('{' + (i - 1) + '}').join(String(arguments[i]));

    return text;
  }

  function send(message) {
    try {
      if (window.vuplex) window.vuplex.postMessage(PREFIX + message);
    } catch (e) {}
  }

  // One press at a time: a double click would otherwise move food twice. Every press is answered by a status line,
  // which frees the buttons; the timer frees them anyway if that answer never comes.
  function sendAction(message) {
    if (isBusy) return;

    isBusy = true;
    clearTimeout(busyTimer);
    busyTimer = setTimeout(clearBusy, BUSY_TIMEOUT_MS);
    send(message);
  }

  function clearBusy() {
    isBusy = false;
    clearTimeout(busyTimer);
  }

  // ---------- text helpers ----------

  function esc(text) {
    return String(text == null ? '' : text).replace(/[&<>"]/g, function (c) {
      return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c];
    });
  }

  function fmt(n) { return (Math.round(n * 100) / 100).toFixed(2); }

  function num(n) { return String(Math.round(n * 10) / 10); }

  // Hours as "2d 5h" or "17h".
  function hoursText(h) {
    h = Math.max(0, Math.ceil(h));

    var d = Math.floor(h / 24);
    var rest = h % 24;

    return d ? say('daysHours', d, rest) : say('hoursShort', rest);
  }

  function tierName(tier) { return say('tier' + tier); }

  function rules() { return st.rules || { low: 0, high: 0.5, per: 25, max: 9, min: 25, speed: 0 }; }

  function tierOf(quality) {
    var r = rules();

    if (quality < r.low) return 0;

    return quality < r.high ? 1 : 2;
  }

  function perBottle(sat) { return sat > 0 ? Math.max(1, Math.ceil(rules().per / sat - 1e-9)) : 0; }

  // Icon paths are relative to Root.html's folder (WebUI), and an image made by a game page would look for them below
  // that page's own folder instead.
  function fromRoot(path) {
    var href = String(location.href || '');

    if (/^[a-z]+:|^\//i.test(path) || href.indexOf('/') < 0) return path;

    return href.slice(0, href.lastIndexOf('/') + 1) + path;
  }

  function img(icon) { return icon ? '<img src="' + esc(fromRoot(icon)) + '">' : '<img>'; }

  // Anything named by id: a food first, then the other items the drinks name.
  function thing(id) { return cat.ings[id] || cat.items[id] || { name: '#' + id, icon: null }; }

  function drinkOf(tier) {
    for (var i = 0; i < cat.drinks.length; i++) if (cat.drinks[i].tier === tier) return cat.drinks[i];

    return { tier: tier, name: tierName(tier), icon: null, des: '', asks: 0, used: [] };
  }

  // A clickable part: the game only passes a click to the parts a page marks as interactive.
  function act(name, arg, extra) {
    return ' class="' + (extra || '') + ' interactive-btn" data-interactive data-act="' + name + '"' + (arg != null ? ' data-arg="' + esc(arg) + '"' : '');
  }

  function bandText(tier) {
    var r = rules();

    if (tier === 0) return say('below', fmt(r.low));
    if (tier === 1) return fmt(r.low) + ' - ' + fmt(r.high);

    return say('orMore', fmt(r.high));
  }

  // ---------- toast ----------

  var TOAST_CSS = '#bh-toast{position:fixed;left:50%;bottom:120px;transform:translateX(-50%);z-index:2147483001;pointer-events:none;display:none;max-width:640px;text-align:center;padding:10px 18px;border-radius:4px;border:1px solid rgba(255,200,58,.6);background:linear-gradient(180deg,rgba(64,52,40,.97),rgba(44,36,28,.97));color:#f0e8d8;font-family:"Microsoft YaHei","PingFang SC",sans-serif;font-size:14px;box-shadow:0 6px 20px rgba(0,0,0,.6)}';

  // The toast is made on first use, and again if the root page lost it; the style outlives a lost toast, so it is added once.
  function toastNode() {
    if (toast && document.body.contains(toast)) return toast;

    addPageStyle(document, TOAST_STYLE_ID, TOAST_CSS);
    toast = document.createElement('div');
    toast.id = 'bh-toast';
    document.body.appendChild(toast);

    return toast;
  }

  // ---------- API ----------

  // What a press did, as a toast over the game; an empty status only hides it.
  function status(message) {
    var node = toastNode();

    clearBusy();

    if (!message) {
      node.style.display = 'none';

      return;
    }

    node.textContent = message;
    node.style.display = 'block';
    clearTimeout(toastTimer);
    toastTimer = setTimeout(function () { node.style.display = 'none'; }, TOAST_MS);
  }

  // Names, icons and numbers of every brewable food and every drink; sent once per game and language.
  function catalog(data) {
    cat = { ings: {}, ingList: [], drinks: [], items: {} };

    ((data && data.ings) || []).forEach(function (i) {
      cat.ings[i.id] = i;
      cat.ingList.push(i);
    });
    cat.drinks = (data && data.drinks) || [];
    cat.items = (data && data.items) || {};
    patchPagesSafely();
  }

  // The open barrel and everything around it; drawn at once and kept up by the page check.
  function brew(data) {
    st = data || { show: false };
    patchPagesSafely();
  }

  // ---------- game pages ----------

  // The brewing screen is a same-origin iframe, so its document can be reached from here; polled because the game
  // creates and drops that iframe whenever it likes.
  function patchPages() {
    var frames = document.querySelectorAll('iframe');

    for (var i = 0; i < frames.length; i++) {
      var src = frames[i].src || '';

      if (!/BrewPanel\.html/.test(src)) continue;

      var doc = null;

      try {
        doc = frames[i].contentDocument;
      } catch (e) {}

      if (!doc) {
        if (!hasReportedPageAccess) send('diag|cannot reach a game page document (root ' + location.protocol + ', iframe ' + src + ')');
        hasReportedPageAccess = true;
        continue;
      }

      draw(doc, frames[i].contentWindow);
    }
  }

  function patchPagesSafely() {
    try {
      patchPages();
    } catch (e) {}
  }

  function addPageStyle(doc, id, css) {
    if (doc.getElementById(id)) return;

    var style = doc.createElement('style');
    style.id = id;
    style.textContent = css;
    (doc.head || doc.body).appendChild(style);
  }

  // Classes the game's own stylesheet already gives this page (BrewPanel.html): its colours, its columns.
  var PAGE_CSS = [
    '.bh-ours .poor,.bh-ours.poor{color:#c98a6a}.bh-ours .avg{color:var(--warm-gold)}.bh-ours .strong{color:#e07a8e}',
    '.bh-ours .dim{color:var(--text-dark);opacity:.8}.bh-ours .neg{color:#e08a8a}.bh-ours .have{color:#9fc88a}',
    '.bh-col{width:400px;min-height:0}',
    '.bh-tabs{display:flex;gap:6px;margin-bottom:8px;flex-shrink:0}',
    '.bh-tab{flex:1;text-align:center;padding:6px 4px;font-size:12px;border-radius:6px;cursor:pointer;color:var(--text-dark);background:rgba(25,20,18,.85);border:1px solid rgba(255,255,255,.1);white-space:nowrap}',
    '.bh-tab:hover{color:var(--text-main);border-color:rgba(251,176,52,.4)}',
    '.bh-tab.on{color:var(--warm-gold);border-color:rgba(251,176,52,.6);background:rgba(251,176,52,.12);font-weight:bold}',
    '.bh-view{overflow-y:auto;height:0;flex-grow:1;display:flex;flex-direction:column;gap:6px;padding-right:5px}',
    '.bh-view::-webkit-scrollbar{width:4px}.bh-view::-webkit-scrollbar-thumb{background:rgba(255,255,255,.2);border-radius:2px}',
    '.bh-sub{font-size:11px;color:var(--text-dark);opacity:.85;padding:1px 2px;line-height:1.45}',
    '.bh-sub.warn{color:#e0a070;opacity:1}',
    '.bh-lbl{font-size:11px;color:var(--warm-gold);margin:6px 2px 3px;letter-spacing:.5px}',
    '.bh-card{background:rgba(12,9,7,.55);border:1px solid rgba(255,255,255,.1);border-radius:6px;padding:8px;flex-shrink:0}',
    '.bh-card.dim{opacity:.55}',
    '.bh-head{display:flex;align-items:center;gap:8px}',
    '.bh-head img{width:34px;height:34px;object-fit:contain;background:rgba(0,0,0,.35);border-radius:5px;padding:2px}',
    '.bh-head>div{flex:1;min-width:0}.bh-head b{font-size:13px}',
    '.bh-desc{font-size:11px;color:var(--text-main);opacity:.75;margin:5px 0 2px}',
    '.bh-can{font-size:11px;padding:2px 7px;border-radius:4px;background:rgba(255,255,255,.06);color:var(--text-dark);white-space:nowrap}',
    '.bh-can.ok{background:rgba(127,174,98,.22);color:#b6dca0}',
    '.bh-load{font:inherit;font-size:12px;font-weight:bold;padding:5px 12px;border-radius:5px;cursor:pointer;color:#2a1c08;background:var(--warm-gold);border:0}',
    '.bh-load.go{background:var(--ok-green,#7fae62);color:#10200a}',
    '.bh-load:disabled{opacity:.35;cursor:default}.bh-card>.bh-load{margin-top:6px}',
    '.bh-chips{display:flex;flex-wrap:wrap;gap:4px;margin-top:6px}',
    '.bh-chip{display:inline-flex;align-items:center;gap:4px;font-size:11px;padding:2px 6px 2px 2px;border-radius:10px;background:rgba(255,255,255,.06);color:var(--text-main);max-width:100%}',
    '.bh-chip img{width:18px;height:18px;object-fit:contain}',
    '.bh-chip i{font-style:normal;color:var(--text-dark);font-size:10px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;max-width:110px}',
    '.bh-chip.urgent{background:rgba(224,85,85,.18)}.bh-chip.urgent i{color:#ffb4b4}',
    '.bh-uses{display:flex;flex-direction:column;gap:3px;font-size:11px}',
    '.bh-uses span{display:flex;align-items:center;gap:5px}.bh-uses img{width:18px;height:18px;object-fit:contain}',
    '.bh-uses i{font-style:normal;color:var(--text-dark)}.bh-uses .ask{color:#e8b04b}',
    '.bh-tools{display:none;gap:6px;align-items:center;flex-shrink:0;margin-bottom:6px}.bh-tools.on{display:flex}',
    '.bh-search{flex:1;min-width:0;padding:6px 9px;border-radius:5px;border:1px solid rgba(255,255,255,.15);background:rgba(0,0,0,.35);color:var(--text-main);font:inherit;font-size:12px}',
    '.bh-fchip{font-size:11px;padding:4px 8px;border-radius:10px;cursor:pointer;color:var(--text-dark);border:1px solid rgba(255,255,255,.14);white-space:nowrap}',
    '.bh-fchip.on{color:var(--warm-gold);border-color:rgba(251,176,52,.55);background:rgba(251,176,52,.1)}',
    '.bh-trow{display:grid;grid-template-columns:22px 1fr 34px 36px 36px 28px 32px 52px;gap:4px;align-items:center;font-size:11px;padding:3px 4px;border-radius:4px;flex-shrink:0;cursor:pointer}',
    '.bh-trow:not(.bh-th):hover{background:rgba(251,176,52,.1)}',
    '.bh-trow img{width:20px;height:20px;object-fit:contain}',
    '.bh-trow .nm{white-space:nowrap;overflow:hidden;text-overflow:ellipsis;color:var(--text-main)}',
    '.bh-trow>span:not(.nm){text-align:right}',
    '.bh-th{cursor:default;color:var(--warm-gold);font-size:10px;border-bottom:1px solid rgba(255,255,255,.1);border-radius:0}',
    '.bh-th [data-act]{cursor:pointer;text-decoration:underline dotted}.bh-th .on{text-decoration:underline}',
    '.bh-prog{height:7px;border-radius:4px;background:rgba(0,0,0,.5);overflow:hidden;margin:7px 0 3px}',
    '.bh-prog i{display:block;height:100%;background:linear-gradient(90deg,var(--amber,#c8873a),var(--warm-gold))}',
    '.bh-switch{display:flex;align-items:center;gap:7px;font-size:11px;color:var(--text-main);cursor:pointer;margin-top:7px;padding:3px;border-radius:4px}',
    '.bh-switch .k{width:26px;height:14px;border-radius:7px;background:rgba(255,255,255,.15);position:relative;flex-shrink:0}',
    '.bh-switch .k::after{content:"";position:absolute;left:2px;top:2px;width:10px;height:10px;border-radius:50%;background:#bbb;transition:.2s}',
    '.bh-switch:hover .k{background:rgba(251,176,52,.3)}',
    '.bh-switch.on .k{background:rgba(251,176,52,.6)}.bh-switch.on .k::after{left:14px;background:#fff}',
    '#bhQuality{margin-top:8px;padding:6px 8px;border-radius:5px;background:rgba(12,9,7,.55);font-size:12px}',
    '.bh-qrow{display:flex;gap:8px;align-items:baseline}.bh-qrow>span:first-child{width:48px;color:var(--text-dark)}',
    '.bh-qrow .dim{margin-left:auto;font-size:11px}',
    '.bh-waste{font-size:11px;color:#e0a070;margin-top:3px}',
    '.qlabels .bh-band{display:block;font-style:normal;font-size:9px;opacity:.8}',
    '#bhFerment:empty{display:none}#bhFerment{padding:6px;border-radius:5px;margin-bottom:8px}',
    '.bh-clock{font-size:12px;color:var(--text-main)}.bh-clock b{color:var(--warm-gold)}',
    '.bh-nudges{display:flex;flex-direction:column;gap:4px;margin-top:6px}',
    '.bh-nudge{display:flex;align-items:center;gap:5px;font-size:11px;padding:4px 5px;border-radius:5px;cursor:pointer;background:rgba(251,176,52,.08);border:1px solid rgba(251,176,52,.3);color:var(--text-main)}',
    '.bh-nudge:hover{background:rgba(251,176,52,.2)}',
    '.bh-nudge img{width:18px;height:18px;object-fit:contain;flex-shrink:0}',
    '.bh-nudge span:nth-child(2){flex:1}.bh-nudge .from{font-size:10px;color:var(--text-dark);white-space:nowrap}',
    '#bhButtons{display:flex;flex-direction:column;gap:4px;margin-top:8px}#bhButtons:empty{display:none}',
    '.bh-btn2{font:inherit;font-size:12px;font-weight:bold;padding:7px;border-radius:6px;cursor:pointer;color:#10200a;background:#7fae62;border:0}',
    '.bh-btn2.ghost{background:transparent;color:var(--text-dark);border:1px solid rgba(255,255,255,.15);font-weight:normal}',
    '.item.bh-poor::before{background:#c98a6a!important}.item.bh-avg::before{background:var(--warm-gold)!important}.item.bh-strong::before{background:#e07a8e!important}',
    '#itemTooltip{white-space:pre-line}'
  ].join('\n');

  function draw(doc, win) {
    if (!st.show) {
      clearPage(doc);

      return;
    }

    var system = doc.querySelector('.brew-system');
    var vat = doc.querySelector('.vat-body');
    var tsec = doc.querySelector('.ferm-col .tsec');

    if (!system || !vat || !tsec) return;

    addPageStyle(doc, STYLE_ID, PAGE_CSS);

    var col = ensureColumn(doc, win, system);
    var quality = ensureNode(doc, vat, 'bhQuality', null);
    var ferment = ensureNode(doc, tsec, 'bhFerment', tsec.firstChild);
    var buttons = ensureNode(doc, tsec, 'bhButtons', null);

    setHtml(quality, qualityHtml());
    setHtml(ferment, fermentHtml());
    setHtml(buttons, buttonsHtml());
    drawBands(doc);
    drawColumn(doc, col);
    tintItems(doc, win);
    extendTooltip(doc, win);
  }

  function clearPage(doc) {
    if (!doc.querySelector('.' + OURS)) return;

    doc.querySelectorAll('.' + OURS).forEach(function (node) { node.remove(); });
    doc.querySelectorAll('.bh-poor,.bh-avg,.bh-strong').forEach(function (node) { node.classList.remove('bh-poor', 'bh-avg', 'bh-strong'); });
  }

  // Only a changed drawing touches the page: the game watches the page for changes to learn where its buttons are,
  // and a redraw every check would also drop the hover under the mouse.
  function setHtml(node, html) {
    if (node.__bhHtml === html) return;

    node.__bhHtml = html;
    node.innerHTML = html;
  }

  function ensureNode(doc, parent, id, before) {
    var node = doc.getElementById(id);

    if (node && node.parentNode === parent) return node;
    if (node) node.remove();

    node = doc.createElement('div');
    node.id = id;
    node.className = OURS;
    node.addEventListener('click', onClick);

    if (before) parent.insertBefore(node, before);
    else parent.appendChild(node);

    return node;
  }

  function ensureColumn(doc, win, system) {
    var col = doc.getElementById('bhCol');

    if (col && col.parentNode === system) return col;
    if (col) col.remove();

    col = doc.createElement('div');
    col.id = 'bhCol';
    col.className = 'col bh-col ' + OURS;
    col.innerHTML = '<div class="col-t"><span>' + esc(say('title')) + '</span></div>'
      + '<div class="bh-tabs">' + TABS.map(function (t) { return '<div' + act('tab', t[0], 'bh-tab') + '>' + esc(say(t[1])) + '</div>'; }).join('') + '</div>'
      + '<div class="bh-tools"><input class="bh-search" id="bhSearch" data-interactive>'
      + '<span' + act('filter', 'rawOnly', 'bh-fchip') + '>' + esc(say('rawOnly')) + '</span>'
      + '<span' + act('filter', 'haveOnly', 'bh-fchip') + '>' + esc(say('haveOnly')) + '</span></div>'
      + '<div class="bh-view" id="bhView"></div>';
    col.addEventListener('click', onClick);
    system.appendChild(col);

    var search = col.querySelector('#bhSearch');

    search.placeholder = say('search');
    search.value = ui.search;
    search.addEventListener('focusin', function () { send('focus|1'); });
    search.addEventListener('focusout', function () { send('focus|0'); });
    search.addEventListener('input', function () {
      ui.search = search.value;
      patchPagesSafely();
    });

    // The game learns where its buttons are on a page change or a window scroll; a scroll inside the list is neither.
    col.querySelector('#bhView').addEventListener('scroll', function () {
      try {
        win.dispatchEvent(new win.Event('scroll'));
      } catch (e) {}
    });

    return col;
  }

  function drawColumn(doc, col) {
    col.querySelectorAll('.bh-tab').forEach(function (tab) { toggle(tab, 'on', tab.getAttribute('data-arg') === ui.tab); });

    var tools = col.querySelector('.bh-tools');

    toggle(tools, 'on', ui.tab === 'ings');
    tools.querySelectorAll('.bh-fchip').forEach(function (chip) { toggle(chip, 'on', !!ui[chip.getAttribute('data-arg')]); });

    var views = { now: viewNow, recipes: viewRecipes, ings: viewIngredients, barrels: viewBarrels };

    setHtml(col.querySelector('#bhView'), views[ui.tab]());
  }

  function toggle(node, name, isOn) {
    if (node.classList.contains(name) !== isOn) node.classList.toggle(name, isOn);
  }

  function onClick(e) {
    var node = e.target && e.target.closest ? e.target.closest('[data-act]') : null;

    if (!node || node.disabled) return;

    e.stopPropagation();

    var arg = node.getAttribute('data-arg');

    switch (node.getAttribute('data-act')) {
      case 'tab': ui.tab = arg; break;
      case 'filter': ui[arg] = !ui[arg]; break;
      case 'sort': ui.sort = arg; break;
      case 'load': sendAction('load|' + arg); return;
      case 'add': sendAction('add|' + arg); return;
      case 'remove': sendAction('remove|' + arg); return;
      case 'empty': sendAction('empty'); return;
      case 'collect': sendAction('collect|' + arg); return;
      case 'again': sendAction('collectagain'); return;
      case 'rebrew': sendAction('rebrew|' + arg + '|' + (node.getAttribute('data-on') === '1' ? '0' : '1')); return;
      default: return;
    }

    patchPagesSafely();
  }

  // ---------- additions to the game's own columns ----------

  function barrel() { return st.barrel || { id: '0', state: IDLE, locked: false, again: false }; }

  function mix() { return st.mix || { count: 0, sat: 0, mood: 0, q: 0, tier: 0, bottles: 0, canStart: false, hours: 0, wasted: 0, next: 0 }; }

  function openBarrel() {
    var all = st.barrels || [];

    for (var i = 0; i < all.length; i++) if (all[i].open) return all[i];

    return null;
  }

  // Under the satiety bar: the mix's quality, the next line up, morale over satiety, and the satiety no bottle takes.
  function qualityHtml() {
    var m = mix();
    var r = rules();

    if (!m.count) return '<div class="bh-qrow"><span>' + esc(say('quality')) + '</span><b>-</b><span class="dim">' + esc(say('addFood')) + '</span></div>';

    var next = m.tier < 2 ? '<span class="dim">' + esc(say('nextAt', tierName(m.tier + 1), fmt(m.tier === 0 ? r.low : r.high))) + '</span>' : '<span class="dim">' + esc(say('best')) + '</span>';
    var html = '<div class="bh-qrow"><span>' + esc(say('quality')) + '</span><b class="' + TIER_CLASS[m.tier] + '">' + fmt(m.q) + '</b>' + next + '</div>'
      + '<div class="bh-qrow"><span>' + esc(say('morale')) + '</span><b>' + num(m.mood) + '</b><span class="dim">' + esc(say('over', num(m.sat))) + '</span></div>';

    if (barrel().state !== IDLE) return html;

    if (!m.canStart) return html + '<div class="bh-waste">' + esc(say('tooLittle', num(r.min))) + '</div>';

    if (m.wasted > 0) {
      html += '<div class="bh-waste">' + esc(say('wasted', num(m.wasted))) + ', '
        + esc(m.bottles < r.max ? say('moreFills', num(m.next), m.bottles + 1) : say('full', r.max)) + '</div>';
    }

    return html;
  }

  // The quality line each drink starts at, under the game's own three drink names; put back when the game redraws them.
  function drawBands(doc) {
    for (var i = 0; i < 3; i++) {
      var label = doc.getElementById('ql' + i);

      if (!label) continue;

      var text = bandText(i);
      var band = label.querySelector('.bh-band');

      if (band && band.textContent === text) continue;
      if (band) band.remove();

      band = doc.createElement('i');
      band.className = 'bh-band ' + OURS;
      band.textContent = text;
      label.appendChild(band);
    }
  }

  // Above the timer: when it will be ready, and what would make it better while it can still change.
  function fermentHtml() {
    var b = barrel();
    var m = mix();

    if (b.state === BREWING) {
      var open = openBarrel();
      var html = open ? '<div class="bh-clock">' + esc(say('readyIn', '\u0001')).replace('\u0001', '<b>' + esc(hoursText(open.remain)) + '</b>') + '</div>' : '';
      var inside = (st.contents || []).map(function (c) { return c.n + ' ' + thing(c.id).name; }).join(', ');

      return html + (inside ? '<div class="bh-sub">' + esc(say('startedWith', inside)) + '</div>' : '');
    }

    if (b.state !== IDLE) return '';

    var out = m.canStart ? '<div class="bh-clock">' + esc(say('startNow', '\u0001')).replace('\u0001', '<b>' + esc(hoursText(m.hours)) + '</b>') + '</div>' : '';
    var nudges = st.nudges || [];

    if (nudges.length && !b.locked) {
      out += '<div class="bh-nudges">' + nudges.map(function (n) {
        var food = thing(n.id);
        var text = say(n.kind === 'add' ? 'nAdd' : 'nRemove', n.n, food.name, tierName(n.tier), n.bottles);
        var from = n.kind === 'add' ? n.where : say('toBag');

        return '<div' + act(n.kind === 'add' ? 'add' : 'remove', n.id, 'bh-nudge') + '>' + img(food.icon) + '<span>' + esc(text) + '</span><span class="from">' + esc(from) + '</span></div>';
      }).join('') + '</div>';
    }

    return out;
  }

  function switchHtml(id, isOn, label) {
    return '<label' + act('rebrew', id, 'bh-switch' + (isOn ? ' on' : '')) + ' data-on="' + (isOn ? '1' : '0') + '"><span class="k"></span>' + esc(label) + '</label>';
  }

  // Under the game's start button: collect and brew again, the brew-again switch, or empty the barrel.
  function buttonsHtml() {
    var b = barrel();

    if (b.state === DONE) return '<button' + act('again', null, 'bh-btn2') + '>' + esc(say('again')) + '</button>' + switchHtml(b.id, b.again, say('againWhen'));
    if (b.state === BREWING) return switchHtml(b.id, b.again, say('againWhen'));
    if ((st.contents || []).length && !b.locked) return '<button' + act('empty', null, 'bh-btn2 ghost') + '>' + esc(say('empty')) + '</button>';

    return '';
  }

  // The dot on each brewable item takes the colour of the drink it makes alone.
  function tintItems(doc, win) {
    var state = win && win.State;

    if (!state) return;

    doc.querySelectorAll('#bagGrid .item.brewable, #vatGrid .item').forEach(function (el) {
      var item = findItem(state.bagItems, el.id) || findItem(state.barrelItems, el.id);
      var food = item && cat.ings[item.configId];
      var want = food && food.sat > 0 ? 'bh-' + TIER_CLASS[tierOf(food.mood / food.sat)] : null;

      ['bh-poor', 'bh-avg', 'bh-strong'].forEach(function (name) { toggle(el, name, name === want); });
    });
  }

  function findItem(list, itemId) {
    list = list || [];

    for (var i = 0; i < list.length; i++) if (String(list[i].itemId) === String(itemId)) return list[i];

    return null;
  }

  // The item tip gets the food's morale, its quality, and the drink it makes alone.
  function extendTooltip(doc, win) {
    if (!win || win.__bhTip || typeof win.showItemTip !== 'function') return;

    var original = win.showItemTip;

    win.__bhTip = true;
    win.showItemTip = function (it) {
      original.apply(this, arguments);

      try {
        var food = st.show && it ? cat.ings[it.configId] : null;
        var tip = doc.getElementById('itemTooltip');

        if (!food || !food.sat || !tip) return;

        var q = food.mood / food.sat;

        tip.textContent += '\n' + say('tipMorale', num(food.mood), fmt(q)) + '\n' + say('tipAlone', drinkOf(tierOf(q)).name, perBottle(food.sat));
      } catch (e) {}
    };
  }

  // ---------- the fourth column ----------

  function chip(t) {
    var food = thing(t.id);

    return '<span class="bh-chip' + (t.urgent ? ' urgent' : '') + '" title="' + esc(t.where) + '">' + img(food.icon) + esc(t.n + ' ' + food.name) + '<i>' + esc(t.where) + '</i></span>';
  }

  function head(drink, tier, second, right) {
    return '<div class="bh-head">' + img(drink.icon) + '<div><b class="' + TIER_CLASS[tier] + '">' + esc(drink.name) + '</b><div class="bh-sub">' + second + '</div></div>' + (right || '') + '</div>';
  }

  // The best barrel of each drink from the food in reach, with where each part comes from.
  function viewNow() {
    var plans = st.plans || [];
    var canLoad = barrel().state === IDLE && !barrel().locked;
    var html = '<div class="bh-sub">' + esc(say('nowNote')) + ' ' + esc(say(st.spoilingFirst ? 'spoilFirst' : 'freshFirst')) + '</div>';

    [2, 1, 0].forEach(function (tier) {
      var drink = drinkOf(tier);
      var p = plans[tier];

      if (!p) {
        html += '<div class="bh-card dim">' + head(drink, tier, esc(say('noPlan'))) + '</div>';

        return;
      }

      var line = esc(say('bottles', p.bottles)) + ' &middot; ' + esc(say('band', fmt(p.q))) + ' &middot; ' + esc(hoursText(p.hours));
      var load = '<button' + act('load', tier, 'bh-load') + (canLoad ? '' : ' disabled') + '>' + esc(say('load')) + '</button>';

      html += '<div class="bh-card">' + head(drink, tier, line, load)
        + '<div class="bh-chips">' + (p.takes || []).map(chip).join('') + '</div>'
        + (p.wasted > 0 ? '<div class="bh-sub warn">' + esc(say('wasted', num(p.wasted))) + '</div>' : '') + '</div>';
    });

    return html + '<div class="bh-sub">' + esc(say('loadNote')) + '</div>';
  }

  // The foods that make a drink best: the most satiety, and for the better drinks the most morale on top.
  function topFor(tier) {
    var list = cat.ingList.filter(function (i) { return !i.dish && i.sat > 0 && tierOf(i.mood / i.sat) === tier; });
    var score = tier === 0 ? function (i) { return -i.sat; } : function (i) { return -(i.sat * (1 + Math.max(0, i.mood / i.sat))); };

    return list.sort(function (a, b) { return score(a) - score(b); }).slice(0, 6);
  }

  function viewRecipes() {
    var r = rules();
    var plans = st.plans || [];
    var html = '<div class="bh-sub">' + esc(say('recipesNote', num(r.per), r.max)) + '</div>';

    [2, 1, 0].forEach(function (tier) {
      var drink = drinkOf(tier);
      var p = plans[tier];
      var can = '<span class="bh-can' + (p ? ' ok' : '') + '">' + esc(p ? say('canMake', p.bottles) : say('noneNow')) + '</span>';
      var best = topFor(tier);
      var used = drink.used || [];

      html += '<div class="bh-card">' + head(drink, tier, esc(tierName(tier) + ' \u00b7 ' + say('band', bandText(tier))), can)
        + (drink.des ? '<div class="bh-desc">' + esc(drink.des) + '</div>' : '');

      if (best.length) {
        html += '<div class="bh-lbl">' + esc(say('goodFor')) + '</div><div class="bh-chips">'
          + best.map(function (i) { return '<span class="bh-chip">' + img(i.icon) + esc(i.name) + '<i>' + fmt(i.mood / i.sat) + '</i></span>'; }).join('') + '</div>';
      }

      if (used.length || drink.asks) {
        html += '<div class="bh-lbl">' + esc(say('usedIn')) + '</div><div class="bh-uses">'
          + used.map(function (u) {
            var make = thing(u.make);
            var others = (u['with'] || []).map(function (id) { return thing(id).name; }).join(', ');

            return '<span>' + img(make.icon) + esc(make.name) + (others ? ' <i>+ ' + esc(others) + '</i>' : '') + '</span>';
          }).join('')
          + (drink.asks ? '<span class="ask">' + esc(say('asks', drink.asks)) + '</span>' : '') + '</div>';
      }

      html += '</div>';
    });

    var talents = [];

    if (r.speed > 0) talents.push(say('speedCut', Math.round(r.speed * 100)));

    talents.push(say('perBottle', num(r.per)));
    talents.push(say('lines', fmt(r.low), fmt(r.high)));

    return html + '<div class="bh-lbl">' + esc(say('talents')) + '</div><div class="bh-sub">' + talents.map(esc).join('<br>') + '</div>';
  }

  var SORTS = {
    quality: function (a, b) { return b.mood / b.sat - a.mood / a.sat; },
    sat: function (a, b) { return b.sat - a.sat; },
    have: function (a, b) { return have(b.id) - have(a.id); }
  };

  function have(id) { return (st.have && st.have[id]) || 0; }

  // Every brewable food: its numbers, how many are in reach, and how many lift the barrel to the next drink.
  function viewIngredients() {
    var words = ui.search.toLowerCase();
    var all = cat.ingList.filter(function (i) { return i.sat > 0; });
    var rows = all.filter(function (i) {
      return (!ui.rawOnly || !i.dish) && (!ui.haveOnly || have(i.id) > 0) && (!words || String(i.name).toLowerCase().indexOf(words) >= 0);
    });
    var sort = SORTS[ui.sort] || SORTS.quality;

    rows.sort(function (a, b) { return sort(a, b) || (a.name < b.name ? -1 : a.name > b.name ? 1 : 0); });

    var reachHead = st.reachKeeps ? say('colKeeps', tierName(2)) : say('colTo', tierName(st.reachTier == null ? 2 : st.reachTier));
    var sortHead = function (key, word) { return '<span' + act('sort', key, ui.sort === key ? 'on' : '') + '>' + esc(say(word)) + '</span>'; };
    var html = '<div class="bh-trow bh-th"><span></span><span>' + esc(say('colIng')) + '</span>' + sortHead('sat', 'colSat')
      + '<span>' + esc(say('colMood')) + '</span>' + sortHead('quality', 'colQual') + '<span>' + esc(say('colPer')) + '</span>'
      + sortHead('have', 'colHave') + '<span>' + esc(reachHead) + '</span></div>';

    rows.forEach(function (i) {
      var q = i.mood / i.sat;
      var h = have(i.id);
      var reach = st.reach ? st.reach[i.id] : null;
      var reachText = reach == null ? '-' : st.reachKeeps ? (reach >= 9 ? '9+' : String(reach)) : '+' + reach;

      html += '<div' + act('add', i.id, 'bh-trow') + '>' + img(i.icon) + '<span class="nm">' + esc(i.name) + '</span><span>' + num(i.sat) + '</span>'
        + '<span class="' + (i.mood < 0 ? 'neg' : '') + '">' + num(i.mood) + '</span><span class="' + TIER_CLASS[tierOf(q)] + '">' + fmt(q) + '</span>'
        + '<span>' + perBottle(i.sat) + '</span><span class="' + (h ? 'have' : 'dim') + '">' + (h || '-') + '</span><span class="dim">' + reachText + '</span></div>';
    });

    return html + '<div class="bh-sub">' + esc(say('ingsNote', rows.length, all.length)) + '</div>';
  }

  // Every brewing barrel in the base: what it brews, how long is left, and its own buttons.
  function viewBarrels() {
    var html = '<div class="bh-sub">' + esc(say('barrelsNote')) + '</div>';

    (st.barrels || []).forEach(function (b) {
      var drink = b.tier >= 0 ? drinkOf(b.tier) : thing(b.result);
      var name = b.name + (b.open ? ' ' + say('thisOne') : '');
      var what = b.state !== IDLE && b.bottles > 0 ? b.bottles + ' x ' + drink.name : say('nothing');
      var right = '<span class="bh-can">' + esc(say('emptyBarrel')) + '</span>';
      var body = '';

      if (b.state === BREWING) {
        var done = b.total > 0 ? Math.round(100 * (1 - b.remain / b.total)) : 0;

        right = '<span class="bh-can">' + esc(say('hoursLeft', hoursText(b.remain))) + '</span>';
        body = '<div class="bh-prog"><i style="width:' + Math.max(0, Math.min(100, done)) + '%"></i></div>';
      } else if (b.state === DONE) {
        right = '<span class="bh-can ok">' + esc(say('ready')) + '</span>';
        body = '<button' + act('collect', b.id, 'bh-load go') + '>' + esc(say('collect')) + '</button>';
      }

      html += '<div class="bh-card"><div class="bh-head">' + img(b.state !== IDLE ? drink.icon : null) + '<div><b>' + esc(name) + '</b><div class="bh-sub">' + esc(what) + '</div></div>' + right + '</div>'
        + body + (b.state !== IDLE ? switchHtml(b.id, b.again, say('againWhen')) : '') + '</div>';
    });

    return html;
  }

  setInterval(patchPagesSafely, 200);

  window.__slbrewinghub = { status: status, catalog: catalog, brew: brew };
  send('ready');
})();
