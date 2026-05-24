const WebSocket = require('ws');
const wss = new WebSocket.Server({ port: 8081 });

console.log("チャットサーバー起動中...");

wss.on('connection', (ws) => {
    console.log("新しいプレイヤーが接続しました");
    ws.on('message', (message) => {
        console.log("メッセージ受信: " + message);

        // 接続している全員にメッセージを転送する
        wss.clients.forEach((client) => {
            // ★ここに「client !== ws」を追加（送ってきた本人には返さない設定）
            if (client !== ws && client.readyState === WebSocket.OPEN) {
                client.send(message.toString());
            }
        });
    });
});