mergeInto(LibraryManager.library, {

  // 啟動心跳包，模擬數據傳入
  StartHeartbeat: function () {
    console.log("JS: 心跳計時器已啟動");
    
    // 每 1000 毫秒執行一次
    setInterval(function() {
      // 產生一個 0~100 的隨機字串
      var mockValue = Math.floor(Math.random() * 100).toString();
      
      // 注意：這裡的 "DataReceiver" 必須跟你 Unity 裡的物件名字完全一致
      // 如果你之前拼錯成 DataReceiver，這裡就要跟著拼錯
      SendMessage("DataReceiver", "SetSensorValue", mockValue);
      
    }, 1000);
  }

});