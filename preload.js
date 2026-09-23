const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('appMenu', {
  state: () => ipcRenderer.invoke('menu:state'),
  run: name => ipcRenderer.invoke('menu:action', name),
  setLink: value => ipcRenderer.invoke('link:set', value),
  onState: cb => ipcRenderer.on('menu:state', (_e, s) => cb(s)),
});
