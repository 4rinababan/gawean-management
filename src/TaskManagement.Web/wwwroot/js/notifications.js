// Thin wrapper around the SignalR notification hub. Calls back into .NET when a "notify" bump arrives.
export function connect(hubUrl, dotNetRef) {
    const connection = new signalR.HubConnectionBuilder()
        .withUrl(hubUrl)
        .withAutomaticReconnect()
        .build();

    connection.on('notify', () => dotNetRef.invokeMethodAsync('OnNotified'));
    connection.start().catch(err => console.error('notification hub:', err));

    return {
        stop: () => connection.stop(),
    };
}
