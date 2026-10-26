// Just enough of Root.html and the game's brewing barrel page (UI/BrewPanel/BrewPanel.html) for page.js to run in Jint: the root
// document with its page iframes, the vuplex bridge, and clicks and other events that bubble. Test code only; nothing
// here ships.
var window = this;

var location = { protocol: 'https:', href: 'https://game.local/Root.html' };

var intervals = [];

function setInterval(callback) {
  intervals.push(callback);

  return intervals.length;
}

// One run of every interval the script set, which is how its 400 ms page check runs.
function poll() {
  intervals.slice().forEach(function (callback) { callback(); });
}

// Held and never fired: the tests answer every press themselves, as the plugin does.
var timers = {};
var nextTimer = 1;

function setTimeout(callback) {
  timers[nextTimer] = callback;

  return nextTimer++;
}

function clearTimeout(id) {
  delete timers[id];
}

var FakeElement = class {
  constructor(tagName) {
    var element = this;

    // Browsers report HTML tag names in capitals, and page.js checks them that way.
    this.tagName = tagName.charAt(0) === '#' ? tagName : tagName.toUpperCase();
    this.id = '';
    this.parentElement = null;
    this.children = [];
    this.text = '';
    this.attrs = {};
    this.classes = [];
    this.listeners = {};
    this.style = {};
    this.value = '';
    this.disabled = false;
    this.scrollTop = 0;
    this.classList = {
      contains: function (name) { return element.classes.indexOf(name) >= 0; },
      add: function () {
        for (var i = 0; i < arguments.length; i++) if (element.classes.indexOf(arguments[i]) < 0) element.classes.push(arguments[i]);
      },
      remove: function () {
        var names = Array.prototype.slice.call(arguments);

        element.classes = element.classes.filter(function (c) { return names.indexOf(c) < 0; });
      },
      toggle: function (name, isOn) {
        var has = element.classes.indexOf(name) >= 0;

        if (isOn === undefined) isOn = !has;
        if (isOn && !has) element.classes.push(name);
        if (!isOn && has) element.classes.splice(element.classes.indexOf(name), 1);

        return isOn;
      }
    };
  }

  get parentNode() { return this.parentElement; }

  get firstChild() { return this.children.length ? this.children[0] : null; }

  get lastElementChild() {
    for (var i = this.children.length - 1; i >= 0; i--) if (this.children[i].tagName !== '#text') return this.children[i];

    return null;
  }

  get childElementCount() { return this.children.filter(function (child) { return child.tagName !== '#text'; }).length; }

  // On a page: under a document's head or body, which are the roots here.
  get isConnected() {
    var top = this;

    while (top.parentElement) top = top.parentElement;

    return top.isRoot === true;
  }

  get nextSibling() { return this.sibling(1); }

  get previousSibling() { return this.sibling(-1); }

  sibling(step) {
    if (!this.parentElement) return null;

    return this.parentElement.children[this.parentElement.children.indexOf(this) + step] || null;
  }

  get className() { return this.classes.join(' '); }

  set className(value) { this.classes = String(value).split(' ').filter(function (name) { return name !== ''; }); }

  // As in a browser: an element's text is all of its descendants' text, and setting it replaces the children.
  get textContent() {
    return this.text + this.children.map(function (child) { return child.textContent; }).join('');
  }

  set textContent(value) {
    this.children.slice().forEach(function (child) { this.removeChild(child); }, this);
    this.text = value == null ? '' : String(value);
  }

  // The page draws with innerHTML; this parses the little HTML it writes: tags, quoted attributes, text and the
  // entities esc() makes. Reading it back gives nothing useful, and the script never does.
  get innerHTML() { return ''; }

  set innerHTML(html) {
    this.textContent = '';

    var open = [this];
    var tokens = String(html).match(/<\/?[a-zA-Z0-9]+[^>]*>|[^<]+/g) || [];

    tokens.forEach(function (token) {
      var top = open[open.length - 1];

      if (token.charAt(0) !== '<') {
        var text = new FakeElement('#text');

        text.text = decodeEntities(token);
        top.appendChild(text);

        return;
      }

      var closing = /^<\/([a-zA-Z0-9]+)/.exec(token);

      if (closing) {
        for (var i = open.length - 1; i > 0; i--) {
          if (open[i].tagName === closing[1].toUpperCase()) {
            open.length = i;
            break;
          }
        }

        return;
      }

      var tag = /^<([a-zA-Z0-9]+)/.exec(token)[1];
      var element = new FakeElement(tag);
      var attrPattern = /([\w-]+)(?:="([^"]*)")?/g;
      var rest = token.slice(tag.length + 1, -1);
      var match;

      while ((match = attrPattern.exec(rest))) {
        var value = match[2] == null ? '' : decodeEntities(match[2]);

        if (match[1] === 'class') element.className = value;
        else if (match[1] === 'id') element.id = value;
        else if (match[1] === 'disabled') element.disabled = true;
        else element.attrs[match[1]] = value;
      }

      top.appendChild(element);

      if (!/^(img|input|br)$/i.test(tag)) open.push(element);
    });
  }

  hasAttribute(name) { return Object.prototype.hasOwnProperty.call(this.attrs, name); }

  getAttribute(name) { return this.hasAttribute(name) ? this.attrs[name] : null; }

  setAttribute(name, value) { this.attrs[name] = String(value); }

  addEventListener(type, listener) {
    (this.listeners[type] = this.listeners[type] || []).push(listener);
  }

  // A fragment's children move into the parent, as in a browser.
  appendChild(child) {
    if (child.tagName === '#fragment') {
      child.children.slice().forEach(function (moved) { this.appendChild(moved); }, this);

      return child;
    }

    if (child.parentElement) child.parentElement.removeChild(child);

    child.parentElement = this;
    this.children.push(child);

    return child;
  }

  insertBefore(child, before) {
    if (!before) return this.appendChild(child);

    if (child.parentElement) child.parentElement.removeChild(child);

    child.parentElement = this;
    this.children.splice(this.children.indexOf(before), 0, child);

    return child;
  }

  replaceChild(child, old) {
    if (child.parentElement) child.parentElement.removeChild(child);

    this.children[this.children.indexOf(old)] = child;
    child.parentElement = this;
    old.parentElement = null;

    return old;
  }

  removeChild(child) {
    this.children = this.children.filter(function (c) { return c !== child; });
    child.parentElement = null;

    return child;
  }

  remove() {
    if (this.parentElement) this.parentElement.removeChild(this);
  }

  contains(other) {
    for (var element = other; element; element = element.parentElement) {
      if (element === this) return true;
    }

    return false;
  }

  // Selector lists and the descendant combinator: ".ferm-col .tsec", "#bagGrid .item.brewable, #vatGrid .item".
  matches(selector) {
    var element = this;

    return String(selector).split(',').some(function (one) {
      var parts = one.trim().split(/\s+/);

      if (!matchesCompound(element, parts[parts.length - 1])) return false;

      var at = element.parentElement;

      for (var i = parts.length - 2; i >= 0; i--) {
        while (at && !matchesCompound(at, parts[i])) at = at.parentElement;

        if (!at) return false;

        at = at.parentElement;
      }

      return true;
    });
  }

  closest(selector) {
    for (var element = this; element; element = element.parentElement) {
      if (element.matches(selector)) return element;
    }

    return null;
  }

  querySelectorAll(selector) {
    searches++;

    return descendantsOf([this]).filter(function (element) { return element.matches(selector); });
  }

  querySelector(selector) {
    return this.querySelectorAll(selector)[0] || null;
  }
};

// Every search of a page (querySelector, querySelectorAll, getElementById), so a test can see a check that searched
// nothing.
var searches = 0;

// Compound selectors of a tag, classes, ids and attributes ("iframe", ".ch-hud-btn", "[data-act]"); the script uses
// nothing else.
function selectorSteps(selector) {
  var steps = selector.match(/^[a-z]+|\.[\w-]+|#[\w-]+|\[[\w-]+\]/g) || [];

  if (steps.join('') !== selector) throw new Error('the fake page does not know the selector "' + selector + '"');

  return steps;
}

function matchesCompound(element, compound) {
  return selectorSteps(compound).every(function (step) { return matchesStep(element, step); });
}

function decodeEntities(text) {
  return text.replace(/&(amp|lt|gt|quot|middot);/g, function (all, name) {
    return { amp: '&', lt: '<', gt: '>', quot: '"', middot: '\u00b7' }[name];
  });
}

function matchesStep(element, step) {
  switch (step.charAt(0)) {
    case '.':
      return element.classList.contains(step.slice(1));
    case '#':
      return element.id === step.slice(1);
    case '[':
      return element.hasAttribute(step.slice(1, -1));
    default:
      return element.tagName === step.toUpperCase();
  }
}

function descendantsOf(roots) {
  var found = [];

  roots.forEach(function (root) {
    root.children.forEach(function walk(child) {
      found.push(child);
      child.children.forEach(walk);
    });
  });

  return found;
}

var FakeDocument = class {
  constructor() {
    this.head = new FakeElement('head');
    this.body = new FakeElement('body');
    this.head.isRoot = true;
    this.body.isRoot = true;
  }

  createElement(tagName) { return new FakeElement(tagName); }

  createElementNS(namespace, tagName) { return new FakeElement(tagName); }

  createTextNode(text) {
    var node = new FakeElement('#text');

    node.textContent = text;

    return node;
  }

  createDocumentFragment() { return new FakeElement('#fragment'); }

  getElementById(id) {
    searches++;

    return descendantsOf([this.head, this.body]).filter(function (element) { return element.id === id; })[0] || null;
  }

  querySelectorAll(selector) {
    searches++;

    return descendantsOf([this.head, this.body]).filter(function (element) { return element.matches(selector); });
  }

  querySelector(selector) {
    return this.querySelectorAll(selector)[0] || null;
  }
};

// Root.html's own document, which holds the page iframes.
var document = new FakeDocument();

// What the script posts to the plugin.
var posted = [];

window.vuplex = {
  postMessage: function (message) { posted.push(message); }
};

// The game's brewing barrel screen (UI/BrewPanel/BrewPanel.html), as far as page.js reaches into it: the three columns
// in .brew-system (bag grid, barrel grid, the fermenting column with its three drink names and the timer section with
// the start button), the item tooltip, and the page's globals State and showItemTip. Window scroll events the script
// raises are counted in scrolls.
function openBrewing() {
  var frame = document.body.appendChild(new FakeElement('iframe'));
  var doc = new FakeDocument();

  frame.src = 'https://game.local/UI/BrewPanel/BrewPanel.html';
  frame.contentDocument = doc;
  doc.body.innerHTML = '<div class="brew-system">'
    + '<div class="col"><div class="col-t">Bag</div><div class="grid" id="bagGrid"></div></div>'
    + '<div class="col"><div class="vat-body"><div class="vat-wrap"><div class="grid" id="vatGrid"></div></div><div id="satZone"></div></div></div>'
    + '<div class="col ferm-col"><div id="bStage"></div><div class="qlabels"><span id="ql0">Poor</span><span id="ql1">Average</span><span id="ql2">Strong</span></div>'
    + '<div class="tsec"><div id="timeNum">0</div><div id="warnBox"></div><div id="takeHint"></div><button id="brewBtn">Start</button></div></div>'
    + '</div><div id="itemTooltip"></div>';
  frame.contentWindow = {
    State: { bagItems: [], barrelItems: [] },
    scrolls: 0,
    Event: function (type) { this.type = type; },
    dispatchEvent: function (event) { if (event.type === 'scroll') frame.contentWindow.scrolls++; },
    showItemTip: function (item) { doc.getElementById('itemTooltip').textContent = item.name; }
  };

  return frame;
}

function brewFrame() {
  return document.querySelectorAll('iframe').filter(function (frame) { return /BrewPanel\.html/.test(frame.src); })[0];
}

function brewPage() {
  return brewFrame().contentDocument;
}

// One item in the bag grid (or the barrel grid), as renderGrid draws it: the element's id is the item's instance id,
// and the page's State lists the same item with its item table id.
function addItem(gridId, instanceId, configId, isBrewable) {
  var frame = brewFrame();
  var el = new FakeElement('div');
  var list = gridId === 'vatGrid' ? frame.contentWindow.State.barrelItems : frame.contentWindow.State.bagItems;

  el.id = String(instanceId);
  el.className = 'item' + (isBrewable ? ' brewable' : '');
  frame.contentDocument.getElementById(gridId).appendChild(el);
  list.push({ itemId: instanceId, configId: configId, name: 'item ' + configId, count: 1, canBrew: !!isBrewable });

  return el;
}

// The text a node shows, every descendant included.
function shown(selector) {
  var node = brewPage().querySelector(selector);

  return node ? node.textContent : '';
}

// An event of any type, bubbling from the target as focusin, focusout and input do in a browser.
function fire(target, type, extra) {
  var isStopped = false;
  var event = { type: type, target: target, stopPropagation: function () { isStopped = true; } };

  Object.keys(extra || {}).forEach(function (key) { event[key] = extra[key]; });

  for (var node = target; node && !isStopped; node = node.parentElement) {
    (node.listeners[type] || []).slice().forEach(function (listener) { listener(event); });
  }
}

// Typing into a text box: it takes focus, its value changes, and the page hears the input.
function type(input, text) {
  fire(input, 'focusin');
  input.value = text;
  fire(input, 'input');
}

// A click as the game's click-through delivers one: dispatched on whatever is under the pointer, greyed buttons
// included, bubbling up until a listener stops it (webui-core.js:33-36).
function click(target) {
  var isStopped = false;
  var event = { target: target, stopPropagation: function () { isStopped = true; } };

  for (var node = target; node && !isStopped; node = node.parentElement) {
    (node.listeners.click || []).slice().forEach(function (listener) { listener(event); });
  }
}

// The first part of the brewing screen with this data-act (and data-arg, when given).
function act(name, arg) {
  return brewPage().querySelectorAll('[data-act]').filter(function (node) {
    return node.getAttribute('data-act') === name && (arg === undefined || node.getAttribute('data-arg') === String(arg));
  })[0] || null;
}
