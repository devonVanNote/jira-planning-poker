window.keyboardVoting = {
    _handler: null,
    _bindings: [],
    _dotNetRef: null,

    loadBindings(n) {
        try {
            const stored = JSON.parse(localStorage.getItem('pp-keybindings') || 'null');
            if (Array.isArray(stored) && stored.length >= n) {
                this._bindings = stored.slice(0, n);
                return this._bindings;
            }
        } catch (_) {}
        this._bindings = Array.from({ length: n }, (_, i) => String(i + 1));
        return this._bindings;
    },

    saveBindings(bindings) {
        this._bindings = Array.from(bindings);
        localStorage.setItem('pp-keybindings', JSON.stringify(this._bindings));
        this._rebuildHandler();
    },

    setBindings(bindings) {
        this._bindings = Array.from(bindings);
        this._rebuildHandler();
    },

    _rebuildHandler() {
        if (this._handler) document.removeEventListener('keydown', this._handler);
        const self = this;
        this._handler = (e) => {
            if (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA' || e.target.tagName === 'SELECT') return;
            const idx = self._bindings.findIndex(b => b.toUpperCase() === e.key.toUpperCase());
            if (idx >= 0 && self._dotNetRef) {
                self._dotNetRef.invokeMethodAsync('HandleVoteKey', String(idx + 1));
            }
        };
        document.addEventListener('keydown', this._handler);
    },

    init(dotNetRef) {
        this._dotNetRef = dotNetRef;
        if (this._bindings.length > 0) this._rebuildHandler();
    },

    dispose() {
        if (this._handler) { document.removeEventListener('keydown', this._handler); this._handler = null; }
        this._dotNetRef = null;
    }
};

window.columnResizer = {
    _cleanups: [],
    init(containerSelector) {
        this.dispose();
        const container = document.querySelector(containerSelector);
        if (!container) return;
        container.querySelectorAll('.resize-handle').forEach(handle => {
            const isRight = handle.dataset.side === 'right';
            const onMouseDown = (e) => {
                e.preventDefault();
                const panel = isRight ? handle.nextElementSibling : handle.previousElementSibling;
                if (!panel) return;
                const startX = e.clientX;
                const startWidth = panel.getBoundingClientRect().width;
                panel.style.flex = '0 0 auto';
                panel.style.width = startWidth + 'px';
                document.body.style.cursor = 'col-resize';
                document.body.style.userSelect = 'none';
                const onMouseMove = (ev) => {
                    const delta = ev.clientX - startX;
                    panel.style.width = Math.max(150, isRight ? startWidth - delta : startWidth + delta) + 'px';
                };
                const onMouseUp = () => {
                    document.removeEventListener('mousemove', onMouseMove);
                    document.removeEventListener('mouseup', onMouseUp);
                    document.body.style.cursor = '';
                    document.body.style.userSelect = '';
                };
                document.addEventListener('mousemove', onMouseMove);
                document.addEventListener('mouseup', onMouseUp);
            };
            handle.addEventListener('mousedown', onMouseDown);
            this._cleanups.push(() => handle.removeEventListener('mousedown', onMouseDown));
        });
    },
    dispose() { this._cleanups.forEach(fn => fn()); this._cleanups = []; }
};

window.raiseHandSound = {
    play() {
        try {
            const ctx = new (window.AudioContext || window.webkitAudioContext)();
            [660, 880, 1046].forEach((freq, i) => {
                const osc = ctx.createOscillator(), gain = ctx.createGain();
                osc.connect(gain); gain.connect(ctx.destination);
                osc.type = 'triangle'; osc.frequency.value = freq;
                const t = ctx.currentTime + i * 0.13;
                gain.gain.setValueAtTime(0, t);
                gain.gain.linearRampToValueAtTime(0.25, t + 0.02);
                gain.gain.exponentialRampToValueAtTime(0.001, t + 0.28);
                osc.start(t); osc.stop(t + 0.3);
            });
        } catch (_) {}
    }
};

window.nudgeSound = {
    play() {
        try {
            const ctx = new (window.AudioContext || window.webkitAudioContext)();
            [880, 1100].forEach((freq, i) => {
                const osc = ctx.createOscillator(), gain = ctx.createGain();
                osc.connect(gain); gain.connect(ctx.destination);
                osc.type = 'sine'; osc.frequency.value = freq;
                const t = ctx.currentTime + i * 0.18;
                gain.gain.setValueAtTime(0, t);
                gain.gain.linearRampToValueAtTime(0.35, t + 0.02);
                gain.gain.exponentialRampToValueAtTime(0.001, t + 0.32);
                osc.start(t); osc.stop(t + 0.35);
            });
        } catch (_) {}
    }
};

window.celebrateConsensus = function () {
    // Fanfare: C-E-G-C
    try {
        const ctx = new (window.AudioContext || window.webkitAudioContext)();
        [261.63, 329.63, 392, 523.25].forEach((freq, i) => {
            const osc = ctx.createOscillator(), gain = ctx.createGain();
            osc.connect(gain); gain.connect(ctx.destination);
            osc.type = 'triangle'; osc.frequency.value = freq;
            const t = ctx.currentTime + i * 0.14;
            gain.gain.setValueAtTime(0, t);
            gain.gain.linearRampToValueAtTime(0.3, t + 0.02);
            gain.gain.exponentialRampToValueAtTime(0.001, t + 0.35);
            osc.start(t); osc.stop(t + 0.4);
        });
    } catch (_) {}

    // Confetti via Web Animations API
    const colors = ['#f59e0b','#ef4444','#3b82f6','#22c55e','#a855f7','#ec4899','#06b6d4'];
    for (let i = 0; i < 90; i++) {
        const el = document.createElement('div');
        el.style.cssText = `position:fixed;top:-10px;left:${Math.random()*100}vw;
            width:${6+Math.random()*6}px;height:${6+Math.random()*10}px;
            background:${colors[i % colors.length]};border-radius:2px;
            pointer-events:none;z-index:9999;opacity:1`;
        document.body.appendChild(el);
        el.animate([
            { transform: `translateY(0) rotate(0deg)`, opacity: 1 },
            { transform: `translateY(100vh) rotate(${360 + Math.random()*360}deg)`, opacity: 0 }
        ], {
            duration: 1200 + Math.random() * 1200,
            delay: Math.random() * 400,
            easing: 'ease-in',
            fill: 'forwards'
        }).onfinish = () => el.remove();
    }
};

window.themeManager = {
    _custom: {},

    init() {
        const saved = localStorage.getItem('pp-theme') || 'light';
        document.documentElement.setAttribute('data-theme', saved);
        if (saved === 'custom') this._applyCustomVars(this._loadCustom());
        return saved;
    },

    setTheme(name) {
        document.documentElement.setAttribute('data-theme', name);
        localStorage.setItem('pp-theme', name);
        // Clear any inline custom vars when switching away from custom
        if (name !== 'custom') {
            const vars = ['--primary','--primary-dark','--primary-light','--bg','--card-bg',
                          '--surface','--text','--text-muted','--border','--nav-bg',
                          '--shadow-sm','--shadow','--shadow-lg'];
            vars.forEach(v => document.documentElement.style.removeProperty(v));
        } else {
            this._applyCustomVars(this._loadCustom());
        }
    },

    applyCustom(accent, bg, card, text, border, nav) {
        const c = { accent, bg, card, text, border, nav };
        this._custom = c;
        localStorage.setItem('pp-custom-theme', JSON.stringify(c));
        this._applyCustomVars(c);
    },

    getCustomColors() {
        const c = this._loadCustom();
        return [c.accent || '#c9a84c', c.bg || '#0c1c2d', c.card || '#112233',
                c.text || '#dde9f2', c.border || '#254d72', c.nav || '#081524'];
    },

    _loadCustom() {
        try { return JSON.parse(localStorage.getItem('pp-custom-theme') || '{}'); } catch (_) { return {}; }
    },

    _applyCustomVars(c) {
        const r = document.documentElement;
        if (c.accent) {
            r.style.setProperty('--primary', c.accent);
            r.style.setProperty('--primary-dark', this._darken(c.accent, 0.15));
            r.style.setProperty('--primary-light', this._alpha(c.accent, 0.12));
        }
        if (c.bg) {
            r.style.setProperty('--bg', c.bg);
            r.style.setProperty('--surface', this._lighten(c.bg, 0.08));
        }
        if (c.card) r.style.setProperty('--card-bg', c.card);
        if (c.text) {
            r.style.setProperty('--text', c.text);
            r.style.setProperty('--text-muted', this._alpha(c.text, 0.55));
        }
        if (c.border) r.style.setProperty('--border', c.border);
        if (c.nav)   r.style.setProperty('--nav-bg', c.nav);
    },

    _darken(hex, amt) {
        const [r, g, b] = this._parseHex(hex);
        return `rgb(${Math.max(0,Math.round(r*(1-amt)))},${Math.max(0,Math.round(g*(1-amt)))},${Math.max(0,Math.round(b*(1-amt)))})`;
    },

    _lighten(hex, amt) {
        const [r, g, b] = this._parseHex(hex);
        return `rgb(${Math.min(255,Math.round(r+(255-r)*amt))},${Math.min(255,Math.round(g+(255-g)*amt))},${Math.min(255,Math.round(b+(255-b)*amt))})`;
    },

    _alpha(hex, a) {
        const [r, g, b] = this._parseHex(hex);
        return `rgba(${r},${g},${b},${a})`;
    },

    _parseHex(hex) {
        const h = hex.replace('#', '');
        return [parseInt(h.slice(0,2),16), parseInt(h.slice(2,4),16), parseInt(h.slice(4,6),16)];
    }
};
