window.keyboardVoting = {
    _handler: null,
    init(dotNetRef) {
        this._handler = (e) => {
            if (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA' || e.target.tagName === 'SELECT') return;
            if (e.key >= '1' && e.key <= '9') {
                dotNetRef.invokeMethodAsync('HandleVoteKey', e.key);
            }
        };
        document.addEventListener('keydown', this._handler);
    },
    dispose() {
        if (this._handler) {
            document.removeEventListener('keydown', this._handler);
            this._handler = null;
        }
    }
};

window.columnResizer = {
    _cleanups: [],
    init(containerSelector) {
        this.dispose();
        const container = document.querySelector(containerSelector);
        if (!container) return;
        const handles = container.querySelectorAll('.resize-handle');
        handles.forEach(handle => {
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
                    const newWidth = Math.max(150, isRight ? startWidth - delta : startWidth + delta);
                    panel.style.width = newWidth + 'px';
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
    dispose() {
        this._cleanups.forEach(fn => fn());
        this._cleanups = [];
    }
};

window.raiseHandSound = {
    play() {
        try {
            const ctx = new (window.AudioContext || window.webkitAudioContext)();
            const notes = [660, 880, 1046];
            notes.forEach((freq, i) => {
                const osc = ctx.createOscillator();
                const gain = ctx.createGain();
                osc.connect(gain);
                gain.connect(ctx.destination);
                osc.type = 'triangle';
                osc.frequency.value = freq;
                const start = ctx.currentTime + i * 0.13;
                gain.gain.setValueAtTime(0, start);
                gain.gain.linearRampToValueAtTime(0.25, start + 0.02);
                gain.gain.exponentialRampToValueAtTime(0.001, start + 0.28);
                osc.start(start);
                osc.stop(start + 0.3);
            });
        } catch (_) {}
    }
};

window.nudgeSound = {
    play() {
        try {
            const ctx = new (window.AudioContext || window.webkitAudioContext)();
            const notes = [880, 1100];
            notes.forEach((freq, i) => {
                const osc = ctx.createOscillator();
                const gain = ctx.createGain();
                osc.connect(gain);
                gain.connect(ctx.destination);
                osc.type = 'sine';
                osc.frequency.value = freq;
                const start = ctx.currentTime + i * 0.18;
                gain.gain.setValueAtTime(0, start);
                gain.gain.linearRampToValueAtTime(0.35, start + 0.02);
                gain.gain.exponentialRampToValueAtTime(0.001, start + 0.32);
                osc.start(start);
                osc.stop(start + 0.35);
            });
        } catch (_) {}
    }
};

window.themeManager = {
    init() {
        const saved = localStorage.getItem('pp-theme') || 'dark';
        document.documentElement.setAttribute('data-theme', saved);
        return saved;
    },
    toggle() {
        const next = document.documentElement.getAttribute('data-theme') === 'light' ? 'dark' : 'light';
        document.documentElement.setAttribute('data-theme', next);
        localStorage.setItem('pp-theme', next);
        return next;
    }
};
