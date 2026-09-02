// Post-processes a rendered issue description: syntax highlighting for code blocks and Mermaid
// rendering for diagram blocks. Operates on markup the server already sanitised.
let mermaidLoader;

// Mermaid is ~2.5 MB, so it is fetched only when a description actually contains a diagram.
function loadMermaid() {
    mermaidLoader ??= new Promise((resolve, reject) => {
        const script = document.createElement('script');
        script.src = '/lib/mermaid/mermaid.min.js';
        script.onload = () => resolve(window.mermaid);
        script.onerror = () => reject(new Error('Could not load the diagram renderer.'));
        document.head.appendChild(script);
    });
    return mermaidLoader;
}

// Quill writes code blocks either as <pre>line<br>line</pre> or as nested <div> lines, depending on
// version and how the content was pasted. Both must come back as plain text with real newlines,
// because Mermaid is whitespace-significant.
function sourceText(pre) {
    const clone = pre.cloneNode(true);
    clone.querySelectorAll('br').forEach(br => br.replaceWith('\n'));
    clone.querySelectorAll('div, p').forEach(line => line.after('\n'));
    return clone.textContent.replace(/\n{3,}/g, '\n\n').trim();
}

function isDiagram(pre, text) {
    return pre.querySelector('code')?.classList.contains('language-mermaid')
        || /^(erDiagram|graph|flowchart|sequenceDiagram|classDiagram|stateDiagram)\b/.test(text);
}

export async function render(container) {
    if (!container) return;

    const blocks = [...container.querySelectorAll('pre')];
    const diagrams = [];

    for (const pre of blocks) {
        const text = sourceText(pre);

        if (isDiagram(pre, text)) {
            diagrams.push({ pre, text });
            continue;
        }

        if (!window.hljs || pre.dataset.highlighted) continue;

        // Normalise to <pre><code> so the hljs theme's selectors apply.
        let code = pre.querySelector('code');
        if (!code) {
            code = document.createElement('code');
            code.textContent = text;
            pre.replaceChildren(code);
        }
        window.hljs.highlightElement(code);
        pre.dataset.highlighted = 'true';
    }

    if (diagrams.length === 0) return;

    let mermaid;
    try {
        mermaid = await loadMermaid();
    } catch {
        return; // leave the source visible rather than showing nothing
    }

    mermaid.initialize({
        startOnLoad: false,
        securityLevel: 'strict',
        theme: document.documentElement.classList.contains('dark') ? 'dark' : 'default',
        fontFamily: 'Inter, ui-sans-serif, system-ui, sans-serif',
    });

    for (const [index, { pre, text }] of diagrams.entries()) {
        const host = document.createElement('div');
        host.className = 'tm-diagram';

        try {
            const { svg } = await mermaid.render(`tm-mermaid-${Date.now()}-${index}`, text);
            host.innerHTML = svg;
            pre.replaceWith(host);
        } catch (err) {
            // A half-finished diagram is normal while drafting: keep the source and explain.
            host.className = 'tm-diagram tm-diagram--error';
            host.textContent = `Diagram error: ${err?.message ?? err}`;
            pre.after(host);
        }
    }
}
