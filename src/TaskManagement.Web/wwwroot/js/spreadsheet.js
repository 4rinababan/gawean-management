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
        const text = e.clipboardData?.getData('text/plain');
        if (!text) return;

        // A single value (no tab, no newline) is an ordinary paste into one field — let the browser
        // handle it normally rather than intercepting every keystroke-equivalent paste.
        if (!text.includes('\t') && !text.includes('\n')) return;

        e.preventDefault();
        dotNetRef.invokeMethodAsync('OnGridPasted', parseClipboardGrid(text));
    };

    inputEl.addEventListener('paste', handler);
    listeners.set(inputEl, handler);
}

/// Reads the clipboard via the async Clipboard API for the toolbar's Paste button (native Ctrl+V
/// while a cell is focused goes through attachPasteListener instead). Requires a secure context;
/// falls back to a no-op (returns null) if the browser denies clipboard-read permission.
export async function readClipboardGrid() {
    try {
        const text = await navigator.clipboard.readText();
        return text ? parseClipboardGrid(text) : null;
    } catch {
        return null;
    }
}

export async function writeClipboardText(text) {
    try {
        await navigator.clipboard.writeText(text ?? '');
        return true;
    } catch {
        return false;
    }
}

function parseClipboardGrid(text) {
    const normalised = text.replace(/\r/g, '');
    const lines = normalised.split('\n');
    // Excel's clipboard text ends with a trailing newline — drop the resulting empty last row.
    if (lines.length > 1 && lines[lines.length - 1] === '') lines.pop();
    return lines.map((line) => line.split('\t'));
}
