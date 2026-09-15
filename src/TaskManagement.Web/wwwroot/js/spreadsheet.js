// Excel-style paste for SpreadsheetEditor. Blazor's own paste event binding can't read clipboard
// contents (ClipboardEventArgs carries no data), so reading the actual pasted grid needs a native
// listener here, which then hands the parsed rows back to .NET.
const listeners = new WeakMap();

/// Attaches a paste listener to the currently-active cell's <input>. A fresh input element is
/// rendered each time a different cell is activated, so no explicit detach is needed — the old
/// element (and its listener) is simply discarded.
export function attachPasteListener(inputEl, dotNetRef) {
    if (!inputEl || listeners.has(inputEl)) return;

    const handler = (e) => {
        const html = e.clipboardData?.getData('text/html');
        const text = e.clipboardData?.getData('text/plain');
        const grid = (html && html.includes('<table')) ? parseHtmlGrid(html) : parsePlainGrid(text);
        if (!grid) return;

        // A single plain value (no tab, no newline, no rich table) is an ordinary paste into one
        // field — let the browser handle it normally rather than intercepting every paste.
        if (grid.length === 1 && grid[0].length === 1 && !html) return;

        e.preventDefault();
        dotNetRef.invokeMethodAsync('OnGridPasted', grid);
    };

    inputEl.addEventListener('paste', handler);
    listeners.set(inputEl, handler);
}

/// Reads the clipboard via the async Clipboard API for the toolbar's Paste button (native Ctrl+V
/// while a cell is focused goes through attachPasteListener instead). Requires a secure context and
/// clipboard-read permission; returns null if the browser denies it or the clipboard is empty.
export async function readClipboardGrid() {
    try {
        const items = await navigator.clipboard.read();
        for (const item of items) {
            if (item.types.includes('text/html')) {
                const html = await (await item.getType('text/html')).text();
                if (html.includes('<table')) return parseHtmlGrid(html);
            }
        }
        for (const item of items) {
            if (item.types.includes('text/plain')) {
                const text = await (await item.getType('text/plain')).text();
                return parsePlainGrid(text);
            }
        }
    } catch {
        // Permission denied or nothing readable — fall back to the plain-text-only API.
        try {
            const text = await navigator.clipboard.readText();
            return text ? parsePlainGrid(text) : null;
        } catch {
            return null;
        }
    }
    return null;
}

export async function writeClipboardText(text) {
    try {
        await navigator.clipboard.writeText(text ?? '');
        return true;
    } catch {
        return false;
    }
}

function parsePlainGrid(text) {
    if (!text) return null;
    const normalised = text.replace(/\r/g, '');
    const lines = normalised.split('\n');
    // Excel's clipboard text ends with a trailing newline — drop the resulting empty last row.
    if (lines.length > 1 && lines[lines.length - 1] === '') lines.pop();
    return lines.map((line) => line.split('\t').map((value) => ({ value })));
}

// Excel's HTML clipboard payload carries real layout/formatting (colours, bold, merged header
// cells via colspan/rowspan) that a plain tab-separated paste throws away entirely. Reading it
// back reliably means letting the browser's own CSS engine resolve whatever mix of inline styles
// and Excel's exported <style> classes were used — which means briefly attaching the pasted markup
// to the live (but hidden) DOM and reading getComputedStyle, the same off-screen-container technique
// already used for real pagination in pagination.js.
function parseHtmlGrid(html) {
    const container = document.createElement('div');
    container.style.position = 'fixed';
    container.style.left = '-99999px';
    container.style.top = '0';
    container.innerHTML = html;
    document.body.appendChild(container);

    try {
        const table = container.querySelector('table');
        if (!table) return null;
        return expandTableToGrid(table);
    } finally {
        container.remove();
    }
}

// HTML tables are row-major with holes where a previous row's rowspan still applies — this expands
// that into a full rectangular grid, one entry per logical (row, col) position. A merged source cell
// isn't merged in the target sheet (the grid here has no concept of spans), so its value lands only
// in the top-left position of the span and its formatting is copied across the rest of the span —
// close enough to read as a single coloured band even though the cells stay independent.
function expandTableToGrid(table) {
    const rows = Array.from(table.rows);
    const grid = [];
    const rowSpanFill = []; // rowSpanFill[col] = { cell, remaining } while a previous row's rowspan still covers this column

    for (let r = 0; r < rows.length; r++) {
        grid[r] = [];
        let col = 0;
        const htmlCells = Array.from(rows[r].cells);
        let cellIndex = 0;

        while (cellIndex < htmlCells.length || (rowSpanFill[col] && rowSpanFill[col].remaining > 0)) {
            while (rowSpanFill[col] && rowSpanFill[col].remaining > 0) {
                grid[r][col] = { ...rowSpanFill[col].cell, value: '' };
                rowSpanFill[col].remaining--;
                col++;
            }
            if (cellIndex >= htmlCells.length) break;

            const cellEl = htmlCells[cellIndex++];
            const cellData = readCellFormat(cellEl);
            const colSpan = cellEl.colSpan || 1;
            const rowSpan = cellEl.rowSpan || 1;

            for (let k = 0; k < colSpan; k++) {
                grid[r][col] = k === 0 ? cellData : { ...cellData, value: '' };
                if (rowSpan > 1) {
                    rowSpanFill[col] = { cell: { ...cellData, value: '' }, remaining: rowSpan - 1 };
                }
                col++;
            }
        }
    }

    // Ragged rows (short rows at the end of a spanned table) — pad so every row is the same length.
    const width = Math.max(...grid.map((row) => row.length), 0);
    for (const row of grid) {
        while (row.length < width) row.push({ value: '' });
    }
    return grid;
}

function readCellFormat(cellEl) {
    const style = getComputedStyle(cellEl);
    const weight = parseInt(style.fontWeight, 10);
    return {
        value: cellEl.textContent.replace(/\s+/g, ' ').trim(),
        bold: style.fontWeight === 'bold' || (!Number.isNaN(weight) && weight >= 600),
        italic: style.fontStyle === 'italic',
        underline: (style.textDecorationLine || style.textDecoration || '').includes('underline'),
        backgroundColor: normaliseColor(style.backgroundColor),
        textColor: normaliseColor(style.color),
        align: ['left', 'center', 'right'].includes(style.textAlign) ? style.textAlign : null,
    };
}

// getComputedStyle reports colours as "rgb(r, g, b)"/"rgba(r, g, b, a)" — convert to hex, and treat
// a transparent or plain-white fill as "no fill" so ordinary cells don't all end up with an explicit
// white background (which would also defeat the app's dark theme).
function normaliseColor(rgbString) {
    const m = rgbString && rgbString.match(/rgba?\((\d+),\s*(\d+),\s*(\d+)(?:,\s*([\d.]+))?\)/);
    if (!m) return null;
    const [, r, g, b, a] = m;
    if (a !== undefined && parseFloat(a) === 0) return null;
    if (r === '255' && g === '255' && b === '255') return null;
    const hex = (n) => Number(n).toString(16).padStart(2, '0');
    return `#${hex(r)}${hex(g)}${hex(b)}`;
}
