// Wraps Paged.js (vendored in wwwroot/lib/pagedjs) to give wiki Documents real, paper-accurate
// pagination — the same chunking engine a print/PDF layout would use, including mid-paragraph
// splits — rather than an estimated page count from a character total.
let pagedReadyPromise = null;

function ensurePagedLoaded() {
    if (pagedReadyPromise) return pagedReadyPromise;

    pagedReadyPromise = new Promise((resolve, reject) => {
        if (window.Paged) { resolve(window.Paged); return; }

        // Must be set before the script loads: without it the polyfill auto-chunks document.body,
        // which would blow away the whole app shell instead of just the content we hand it.
        window.PagedConfig = { auto: false };

        const script = document.createElement('script');
        script.src = 'lib/pagedjs/paged.polyfill.min.js';
        script.onload = () => resolve(window.Paged);
        script.onerror = () => reject(new Error('Failed to load paged.polyfill.min.js'));
        document.head.appendChild(script);
    });

    return pagedReadyPromise;
}

// A4 with ~2cm margins is the default paper size this app paginates against — matches the size
// most users printing or exporting a wiki document from Indonesia would expect.
const PAGE_STYLESHEET = `
@page {
    size: A4;
    margin: 20mm 18mm 16mm 18mm;
    @bottom-center {
        content: counter(page) " / " counter(pages);
        font-size: 9pt;
        color: #94a3b8;
    }
}
body {
    font-family: ui-sans-serif, system-ui, -apple-system, sans-serif;
    font-size: 11pt;
    line-height: 1.55;
    color: #1e293b;
}
h1 { font-size: 20pt; font-weight: 600; margin: 0 0 0.5em; }
h2 { font-size: 15pt; font-weight: 600; margin: 1em 0 0.5em; }
h3 { font-size: 12.5pt; font-weight: 600; margin: 1em 0 0.5em; }
p { margin: 0 0 0.75em; orphans: 2; widows: 2; }
ul, ol { margin: 0 0 0.75em; padding-left: 1.5em; }
table { border-collapse: collapse; width: 100%; margin: 0 0 0.75em; }
table td, table th { border: 1px solid #cbd5e1; padding: 4pt 6pt; text-align: left; }
img { max-width: 100%; }
blockquote { border-left: 3px solid #cbd5e1; margin: 0 0 0.75em; padding-left: 1em; color: #475569; }
pre { background: #f1f5f9; padding: 8pt; border-radius: 4px; overflow-wrap: break-word; white-space: pre-wrap; font-size: 9.5pt; }
code { font-family: ui-monospace, monospace; }
`;

// Paged.js's Polisher.add() treats a plain string argument as a URL to fetch — passing the CSS text
// itself that way makes it try to XHR-fetch the stylesheet's own contents as a path (404s, and the
// SPA's fallback HTML gets parsed as CSS). An inline sheet has to be wrapped as { key: cssText },
// mirroring how the library treats an existing <style> tag when it harvests page styles itself.
const PAGE_STYLESHEETS = [{ 'tm-pagination-stylesheet': PAGE_STYLESHEET }];

function toFragment(html) {
    const template = document.createElement('template');
    template.innerHTML = html;
    return template.content;
}

/// Runs the real pagination engine against `html` off-screen and returns how many pages it produced,
/// without touching anything the user can see. Used for the live "N pages" estimate while editing.
export async function countPages(html) {
    if (!html || !html.trim()) return 0;

    const Paged = await ensurePagedLoaded();

    const container = document.createElement('div');
    container.style.position = 'fixed';
    container.style.left = '-99999px';
    container.style.top = '0';
    container.setAttribute('aria-hidden', 'true');
    document.body.appendChild(container);

    const stylesBefore = new Set(document.querySelectorAll('style[data-pagedjs-inserted-styles]'));
    try {
        const previewer = new Paged.Previewer();
        const flow = await previewer.preview(toFragment(html), PAGE_STYLESHEETS, container);
        return flow.pages.length;
    } finally {
        container.remove();
        // Each preview() call leaves a <style> tag behind in <head>; a throwaway off-screen count
        // doesn't need to keep it, and this runs on every debounced keystroke while editing.
        document.querySelectorAll('style[data-pagedjs-inserted-styles]').forEach((el) => {
            if (!stylesBefore.has(el)) el.remove();
        });
    }
}

/// Renders `html` as real paginated pages into `targetElement` (visible, read-only view) and
/// returns the page count.
export async function renderPaginated(html, targetElement) {
    targetElement.innerHTML = '';
    if (!html || !html.trim()) return 0;

    const Paged = await ensurePagedLoaded();
    const stylesBefore = new Set(document.querySelectorAll('style[data-pagedjs-inserted-styles]'));
    const previewer = new Paged.Previewer();
    const flow = await previewer.preview(toFragment(html), PAGE_STYLESHEETS, targetElement);
    // Clean up any predecessor's <style> tag now that this one has rendered successfully — otherwise
    // navigating between wiki Documents in the same session leaks one per page visited.
    stylesBefore.forEach((el) => el.remove());
    return flow.pages.length;
}
