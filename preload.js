const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('appMenu', {
  state: () => ipcRenderer.invoke('menu:state'),
  run: name => ipcRenderer.invoke('menu:action', name),
  setLink: value => ipcRenderer.invoke('link:set', value),
  onState: cb => ipcRenderer.on('menu:state', (_e, s) => cb(s)),
  // 네이티브(WPF) 판이 가져갈 수 있도록 자료 사본을 파일로 남긴다
  exportData: (key, value) => ipcRenderer.send('data:export', key, value),
});
