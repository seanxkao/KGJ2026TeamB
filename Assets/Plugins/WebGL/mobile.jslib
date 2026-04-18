mergeInto(LibraryManager.library, {

  InitPeerReceiver: function () {
    var roomID = 'kuso-game-jam-room-' + Math.floor(100000 + Math.random() * 900000);
    var peer = new Peer(roomID);

    peer.on('open', function(id) {
      console.log('【JS】Unity 接收端已就緒，ID: ' + id);
      SendMessage("DataReceiver", "SetRoomID", id);
    });

    // Peer 與 PeerJS server 斷線時自動重連 server
    peer.on('disconnected', function() {
      console.warn('【JS】與 PeerJS server 斷線，嘗試重連...');
      peer.reconnect();
    });

    peer.on('error', function(err) {
      console.error('【JS】Peer 錯誤:', err);
      SendMessage("DataReceiver", "SetSensorValue", "ERROR");
    });

    peer.on('connection', function(conn) {
      console.log('【JS】手機已成功連線！');
      SendMessage("DataReceiver", "SetSensorValue", "CONNECTED");

      conn.on('data', function(data) {
        console.log('【JS】收到資料:', data);
        try {
          var parsed = (typeof data === 'string') ? JSON.parse(data) : data;
          SendMessage("DataReceiver", "SetAccValue", parsed.acc.toString());
          SendMessage("DataReceiver", "SetPeakValue", parsed.peak.toString());
        } catch(e) {
          SendMessage("DataReceiver", "SetSensorValue", data.toString());
        }
      });

      conn.on('close', function() {
        console.warn('【JS】手機端連線已斷開');
        SendMessage("DataReceiver", "SetSensorValue", "DISCONNECTED");
      });

      conn.on('error', function(err) {
        console.error('【JS】conn 錯誤:', err);
        SendMessage("DataReceiver", "SetSensorValue", "ERROR");
      });
    });
  }

});