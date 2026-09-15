// Quill wrapper for the RichTextEditor component.
// Image insertion is deliberately *not* handled here: the toolbar's image button asks .NET to open a
// Blazor file input, so uploads go through the existing authenticated AttachmentService rather than a
// separate JS endpoint that would need its own auth and antiforgery handling.
const editors = new Map();

export function create(element, dotNetRef, initialHtml, readOnly, showPageCount, pageCountEl, pageNavEl) {
    if (!element || editors.has(element)) return;

    const quill = new Quill(element, {
        theme: 'snow',
        readOnly: readOnly,
        placeholder: 'Describe the work…',
        modules: {
            toolbar: readOnly ? false : {
                container: [
                    [{ header: [1, 2, 3, false] }],
                    ['bold', 'italic', 'underline', 'strike'],
                    [{ color: [] }, { background: [] }],
                    [{ list: 'ordered' }, { list: 'bullet' }],
                    [{ align: [] }],
                    ['blockquote', 'code-block', 'link', 'image'],
                    ['clean'],
                ],
                handlers: {
                    image: () => dotNetRef.invokeMethodAsync('PickImageAsync'),
                },
            },
        },
    });

    if (initialHtml) {
        quill.clipboard.dangerouslyPasteHTML(initialHtml, 'silent');
    }

    // Debounced: on Blazor Server every callback is a network round trip, and firing one per
    // keystroke makes typing feel like it is dropping characters.
    let pending;
    let pageCountToken = 0;
    let paginationModule = null;
    let totalPages = 1;

    quill.on('text-change', (_delta, _old, source) => {
        if (source !== 'user') return;
        clearTimeout(pending);
        pending = setTimeout(() => {
            const html = getHtml(quill);
            dotNetRef.invokeMethodAsync('OnContentChangedAsync', html);
            if (showPageCount) schedulePageCount(html);
        }, 300);
    });

    // Real pagination is real work (it lays the content out the same way a print/PDF engine
    // would), so only the most recent request's result is allowed to win — otherwise a burst of
    // keystrokes could resolve out of order and flash a stale count. Written straight to the DOM
    // rather than round-tripped through Blazor: this component intentionally never re-renders
    // after its first paint (see the ShouldRender override in the .razor file), since letting
    // Blazor diff over the div Quill owns steals focus and eats input.
    function schedulePageCount(html) {
        if (!pageCountEl) return;
        const token = ++pageCountToken;
        pageCountEl.textContent = 'Calculating pages…';
        import('./pagination.js').then((mod) => {
            paginationModule = mod;
            return mod.countPages(html);
        }).then((count) => {
            if (token !== pageCountToken) return;
            totalPages = Math.max(1, count);
            pageCountEl.textContent = count <= 1 ? '1 page' : `${count} pages`;
            if (navTotalEl) navTotalEl.textContent = String(totalPages);
            refreshNavButtons();
        });
    }

    // "Paginated editing": rather than trying to make a contenteditable Quill instance literally
    // reflow into separate page boxes (Paged.js is a read-only layout engine, not something a live
    // editor can reflow against on every keystroke), the .ql-editor surface is sized to one page's
    // content area (see the .tm-richtext--paginated CSS) with its own internal scroll, and guide
    // lines are drawn at each real page-height interval. Prev/Next jump that internal scroll by
    // exactly one page height, so reading/typing through a long Document is "flip through pages"
    // rather than one continuous scroll down the browser window. This is an approximation — Quill's
    // line-wrapping won't always land on the exact same break as the real Paged.js output on save —
    // but the width/font match the print stylesheet closely enough that it's a close guide.
    let navTotalEl, navCurrentEl, navFirstBtn, navPrevBtn, navNextBtn, navLastBtn, scroller;

    function currentPageIndex() {
        if (!scroller) return 1;
        const pageHeight = paginationModule?.PAGE_CONTENT_HEIGHT_PX ?? 986.2;
        return Math.min(totalPages, Math.max(1, Math.round(scroller.scrollTop / pageHeight) + 1));
    }

    function refreshNavButtons() {
        const page = currentPageIndex();
        if (navCurrentEl) navCurrentEl.textContent = String(page);
        if (navFirstBtn) navFirstBtn.disabled = page <= 1;
        if (navPrevBtn) navPrevBtn.disabled = page <= 1;
        if (navNextBtn) navNextBtn.disabled = page >= totalPages;
        if (navLastBtn) navLastBtn.disabled = page >= totalPages;
    }

    function goToPage(page) {
        if (!scroller) return;
        const pageHeight = paginationModule?.PAGE_CONTENT_HEIGHT_PX ?? 986.2;
        page = Math.min(totalPages, Math.max(1, page));
        scroller.scrollTo({ top: (page - 1) * pageHeight, behavior: 'smooth' });
    }

    if (showPageCount && pageNavEl) {
        scroller = element.querySelector('.ql-editor');
        navTotalEl = pageNavEl.querySelector('[data-page-nav="total"]');
        navCurrentEl = pageNavEl.querySelector('[data-page-nav="current"]');
        navFirstBtn = pageNavEl.querySelector('[data-page-nav="first"]');
        navPrevBtn = pageNavEl.querySelector('[data-page-nav="prev"]');
        navNextBtn = pageNavEl.querySelector('[data-page-nav="next"]');
        navLastBtn = pageNavEl.querySelector('[data-page-nav="last"]');

        navFirstBtn?.addEventListener('click', () => goToPage(1));
        navPrevBtn?.addEventListener('click', () => goToPage(currentPageIndex() - 1));
        navNextBtn?.addEventListener('click', () => goToPage(currentPageIndex() + 1));
        navLastBtn?.addEventListener('click', () => goToPage(totalPages));

        let scrollTicking = false;
        scroller?.addEventListener('scroll', () => {
            if (scrollTicking) return;
            scrollTicking = true;
            requestAnimationFrame(() => { refreshNavButtons(); scrollTicking = false; });
        });
    }

    editors.set(element, quill);

    if (showPageCount) schedulePageCount(getHtml(quill));
}

function getHtml(quill) {
    // Quill leaves an empty paragraph behind; treat that as "no description".
    const html = quill.getSemanticHTML();
    return quill.getText().trim().length === 0 && !html.includes('<img') ? '' : html;
}

export function setContent(element, html) {
    const quill = editors.get(element);
    if (!quill) return;
    quill.setContents([], 'silent');
    if (html) quill.clipboard.dangerouslyPasteHTML(html, 'silent');
}

/// Inserts a fenced block of text as a Quill code-block, one line per block line.
export function insertCodeBlock(element, text) {
    const quill = editors.get(element);
    if (!quill) return;

    const range = quill.getSelection(true);
    const at = range ? range.index : quill.getLength();

    quill.insertText(at, text.endsWith('\n') ? text : text + '\n', 'user');
    quill.formatLine(at, text.length, 'code-block', true, 'user');
    quill.setSelection(at + text.length, 0);
}

export function insertImage(element, url) {
    const quill = editors.get(element);
    if (!quill) return;
    const range = quill.getSelection(true);
    quill.insertEmbed(range ? range.index : quill.getLength(), 'image', url, 'user');
    quill.setSelection((range ? range.index : quill.getLength()) + 1, 0);
}

export function dispose(element) {
    editors.delete(element);
}
