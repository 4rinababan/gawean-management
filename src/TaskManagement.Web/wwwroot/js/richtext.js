// Quill wrapper for the RichTextEditor component.
// Image insertion is deliberately *not* handled here: the toolbar's image button asks .NET to open a
// Blazor file input, so uploads go through the existing authenticated AttachmentService rather than a
// separate JS endpoint that would need its own auth and antiforgery handling.
const editors = new Map();

export function create(element, dotNetRef, initialHtml, readOnly) {
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
    quill.on('text-change', (_delta, _old, source) => {
        if (source !== 'user') return;
        clearTimeout(pending);
        pending = setTimeout(() => dotNetRef.invokeMethodAsync('OnContentChangedAsync', getHtml(quill)), 300);
    });

    editors.set(element, quill);
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
