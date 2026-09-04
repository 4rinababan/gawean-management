(function () {
    const COOKIE = 'tm-tour';

    window.tm = window.tm || {};
    window.tm.tour = {
        read() {
            const match = document.cookie.match(new RegExp('(?:^|; )' + COOKIE + '=([^;]*)'));
            if (!match) return null;
            try {
                return JSON.parse(decodeURIComponent(match[1]));
            } catch (e) {
                return null;
            }
        },
        write(json) {
            const value = encodeURIComponent(JSON.stringify(json));
            document.cookie = `${COOKIE}=${value}; Path=/; Max-Age=15552000; SameSite=Lax`;
        },
        // Finds the target element for a tour step, scrolls it into view, and reports its on-screen
        // rect so Blazor can position the spotlight cutout. Retries for a bit since a step can become
        // current right after a Blazor navigation, before the target page has finished rendering.
        locate(selector) {
            return new Promise((resolve) => {
                if (!selector) { resolve(null); return; }
                let tries = 0;
                (function attempt() {
                    const el = document.querySelector(selector);
                    if (el) {
                        el.scrollIntoView({ block: 'center', behavior: 'smooth' });
                        setTimeout(() => {
                            const r = el.getBoundingClientRect();
                            resolve({ top: r.top, left: r.left, width: r.width, height: r.height });
                        }, 300);
                    } else if (++tries < 20) {
                        setTimeout(attempt, 150);
                    } else {
                        resolve(null);
                    }
                })();
            });
        },
    };
})();
