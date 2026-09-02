// Bridges SortableJS drag-and-drop on the Kanban board to a .NET component.
// Each column element carries data-status; each card carries data-issue-id.
const instances = new Map();

export function init(boardElement, dotNetRef) {
    if (!boardElement || instances.has(boardElement)) return;

    const columns = boardElement.querySelectorAll('[data-kanban-column]');
    const sortables = [];

    columns.forEach(column => {
        sortables.push(new Sortable(column, {
            group: 'kanban',
            animation: 150,
            ghostClass: 'kanban-drag-ghost',
            chosenClass: 'kanban-drag-chosen',
            onEnd: async evt => {
                const card = evt.item;
                const list = evt.to;
                const issueId = card.getAttribute('data-issue-id');
                const targetStatus = list.getAttribute('data-status');

                const siblings = Array.from(list.querySelectorAll('[data-issue-id]'));
                const index = siblings.indexOf(card);
                const beforeId = index > 0 ? siblings[index - 1].getAttribute('data-issue-id') : null;
                const afterId = index < siblings.length - 1 ? siblings[index + 1].getAttribute('data-issue-id') : null;

                await dotNetRef.invokeMethodAsync('OnCardMoved', issueId, targetStatus, beforeId, afterId);
            },
        }));
    });

    instances.set(boardElement, sortables);
}

export function dispose(boardElement) {
    const sortables = instances.get(boardElement);
    if (sortables) {
        sortables.forEach(s => s.destroy());
        instances.delete(boardElement);
    }
}
