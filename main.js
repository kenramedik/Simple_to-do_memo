const { app, BrowserWindow, Menu, Tray, nativeImage, nativeTheme, dialog, ipcMain, shell } = require('electron');
const path = require('path');
const fs = require('fs');

const AUTHOR = 'goni';

const T = {
  ko: {
    appTitle: '간단한 할일 메모',
    open: '열기',
    alwaysOnTop: '항상 위에 표시',
    exit: '종료',
    about: '정보',
    createdBy: '제작자',
    aboutBody: '하루 단위로 메모를 남기고 완료 표시를 할 수 있습니다.',
    linksTitle: '링크 모음',
    ok: '확인',
  },
  en: {
    appTitle: 'Simple To-Do Memo',
    open: 'Open',
    alwaysOnTop: 'Always on Top',
    exit: 'Exit',
    about: 'About',
    createdBy: 'Created by',
    aboutBody: 'Keep a to-do list one day at a time.',
    linksTitle: 'Links',
    ok: 'OK',
  },
};

let win = null;
let linksWin = null;
let tray = null;

// link: 괄호 안 숫자에 달 링크. url 이 비어 있으면 링크를 달지 않는다.
const settings = {
  lang: null, alwaysOnTop: false, minimizeToTray: true, deleteLock: true, bounds: null, linksBounds: null,
  link: { url: '', param: '' },
};
const t = key => T[settings.lang === 'en' ? 'en' : 'ko'][key];
const settingsFile = () => path.join(app.getPath('userData'), 'settings.json');

function loadSettings() {
  try { Object.assign(settings, JSON.parse(fs.readFileSync(settingsFile(), 'utf8'))); } catch {}
}
function saveSettings() {
  try { fs.writeFileSync(settingsFile(), JSON.stringify(settings, null, 2)); } catch {}
}

function loadIcon(file) {
  return nativeImage.createFromBuffer(fs.readFileSync(path.join(__dirname, 'assets', file)));
}

const titleText = () => `${t('appTitle')}  v${app.getVersion()}`;

function setLang(value) {
  settings.lang = value;
  saveSettings();
  if (win && !win.isDestroyed()) win.setTitle(titleText());
  if (linksWin) linksWin.setTitle(t('linksTitle'));
  if (tray) tray.setToolTip(titleText());
  applyMenus();
  pushState();
}

/* ── 설정 토글 ── */
function setAlwaysOnTop(value) {
  settings.alwaysOnTop = value;
  win.setAlwaysOnTop(value);
  if (linksWin) linksWin.setAlwaysOnTop(value);
  saveSettings();
  applyMenus();
  pushState();
}
function setMinimizeToTray(value) {
  settings.minimizeToTray = value;
  saveSettings();
  applyMenus();
  pushState();
}
function setDeleteLock(value) {
  settings.deleteLock = value;
  saveSettings();
  pushState();
}
// 렌더러가 이미 검사하지만, 설정 파일에 들어가는 값이라 여기서 한 번 더 거른다
function setLink(value) {
  const url = String(value?.url ?? '').trim();
  const param = String(value?.param ?? '').trim();
  if (url && !/^https?:\/\/\S+$/i.test(url)) return;
  if (param && !/^[\w.~-]+$/.test(param)) return;
  settings.link = { url, param };
  saveSettings();
  pushState();
}

// 메뉴바는 HTML로 그리므로, 렌더러가 체크 상태를 알아야 한다.
const menuState = () => ({
  lang: settings.lang === 'en' ? 'en' : 'ko',
  alwaysOnTop: settings.alwaysOnTop,
  minimizeToTray: settings.minimizeToTray,
  deleteLock: settings.deleteLock,
  link: { url: settings.link?.url || '', param: settings.link?.param || '' },
});
function pushState() {
  const s = menuState();
  if (win && !win.isDestroyed()) win.webContents.send('menu:state', s);
  if (linksWin) linksWin.webContents.send('menu:state', s);
}

/* ── 트레이 ── */
function hideToTray() {
  if (!tray) {
    tray = new Tray(loadIcon('tray.png'));
    tray.setToolTip(titleText());
    tray.on('click', showWindow);
    tray.on('double-click', showWindow);
  }
  applyMenus();
  win.hide();
}

// 숨긴 창은 isMinimized()가 false를 돌려주므로 show() 뒤에 restore()를 무조건 부른다.
// 트레이는 창을 되살린 뒤에 없앤다 - 먼저 지우면 복원 실패 시 앱에 접근할 길이 사라진다.
function showWindow() {
  win.show();
  win.restore();
  win.focus();
  if (tray) { tray.destroy(); tray = null; }
}

/* ── 메뉴 ── */
// 메뉴바는 렌더러가 HTML로 그린다. 네이티브 메뉴를 남겨두면 setApplicationMenu를
// 다시 부를 때마다 숨겨둔 메뉴바가 되살아나므로, 단축키는 before-input-event로 받는다.
function applyMenus() {
  if (tray) {
    tray.setContextMenu(Menu.buildFromTemplate([
      { label: t('open'), click: showWindow },
      {
        label: t('alwaysOnTop'),
        type: 'checkbox',
        checked: settings.alwaysOnTop,
        click: item => setAlwaysOnTop(item.checked),
      },
      { type: 'separator' },
      { label: t('exit'), role: 'quit' },
    ]));
  }
}

function showAbout() {
  dialog.showMessageBox(win, {
    type: 'info',
    title: t('about'),
    message: `${t('appTitle')}   v${app.getVersion()}`,
    detail: `${t('createdBy')}: ${AUTHOR}\nElectron ${process.versions.electron}\n\n${t('aboutBody')}`,
    buttons: [t('ok')],
  });
}

// 앱 이름이 DateMemo 에서 바뀌면서 userData 경로도 바뀌었다.
// 창을 만들기 전에 예전 폴더를 옮겨와야 기존 메모가 그대로 보인다.
function migrateUserData() {
  const oldDir = path.join(app.getPath('appData'), 'DateMemo');
  const newDir = app.getPath('userData');
  if (oldDir === newDir) return;
  if (fs.existsSync(path.join(newDir, 'settings.json'))) return;
  if (!fs.existsSync(path.join(oldDir, 'settings.json'))) return;
  try {
    fs.mkdirSync(newDir, { recursive: true });
    for (const entry of fs.readdirSync(oldDir)) {
      fs.cpSync(path.join(oldDir, entry), path.join(newDir, entry), { recursive: true });
    }
  } catch (e) {
    // 옮기지 못해도 예전 폴더는 그대로 남으므로 손으로 복구할 수 있다
    console.error('userData 이전 실패:', e);
  }
}

// 최초 실행 - 어느 언어인지 모르니 양쪽 언어로 묻는다.
function askLanguage() {
  const i = dialog.showMessageBoxSync({
    type: 'question',
    title: 'Simple To-Do Memo',
    message: '언어를 선택하세요 / Choose a language',
    buttons: ['한국어', 'English'],
    defaultId: 0,
    cancelId: 0,
    noLink: true,
  });
  settings.lang = i === 1 ? 'en' : 'ko';
  saveSettings();
}

/* ── 창 ── */
// 두 창 모두 링크는 앱 안에 새 창을 띄우지 않고 기본 브라우저로 열고,
// 링크가 앱 화면 자체를 바꿔 버리지 않게 막는다.
function guardContents(wc) {
  wc.setWindowOpenHandler(({ url }) => {
    if (url.startsWith('https://') || url.startsWith('http://')) shell.openExternal(url);
    return { action: 'deny' };
  });
  wc.on('will-navigate', e => e.preventDefault());
  wc.on('before-input-event', (e, input) => {
    if (input.type !== 'keyDown' || !input.control || input.alt || input.meta) return;
    const key = (input.key || '').toLowerCase();
    if (key === 't') { e.preventDefault(); setAlwaysOnTop(!settings.alwaysOnTop); }
    else if (key === 'q') { e.preventDefault(); app.quit(); }
    else if (key === 'l') { e.preventDefault(); openLinksWindow(); }
  });
}

function createWindow() {
  win = new BrowserWindow({
    width: 460,
    height: 740,
    minWidth: 360,
    minHeight: 440,
    ...(settings.bounds || {}),
    title: titleText(),
    icon: loadIcon('icon.png'),
    backgroundColor: '#f5f5f3',
    alwaysOnTop: settings.alwaysOnTop,
    show: false,
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
    },
  });

  guardContents(win.webContents);

  win.loadFile('index.html');
  win.once('ready-to-show', () => win.show());
  win.on('page-title-updated', e => e.preventDefault());

  win.on('minimize', e => {
    if (settings.minimizeToTray) {
      e.preventDefault();
      hideToTray();
    }
  });

  win.on('close', () => {
    if (!win.isMinimized() && !win.isFullScreen()) settings.bounds = win.getBounds();
    saveSettings();
  });
  // 링크 창만 남으면 메인 창을 되살릴 길이 없다 - 같이 닫는다
  win.on('closed', () => { if (linksWin) linksWin.close(); });
}

// 링크 모음 창 - 이미 떠 있으면 앞으로 가져온다
function openLinksWindow() {
  if (linksWin) {
    if (linksWin.isMinimized()) linksWin.restore();
    linksWin.show();
    linksWin.focus();
    return;
  }
  linksWin = new BrowserWindow({
    width: 420,
    height: 640,
    minWidth: 340,
    minHeight: 420,
    ...(settings.linksBounds || {}),
    title: t('linksTitle'),
    icon: loadIcon('icon.png'),
    backgroundColor: '#f5f5f3',
    alwaysOnTop: settings.alwaysOnTop,
    show: false,
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
    },
  });
  guardContents(linksWin.webContents);
  linksWin.loadFile('links.html');
  linksWin.once('ready-to-show', () => linksWin.show());
  linksWin.on('page-title-updated', e => e.preventDefault());
  linksWin.on('close', () => {
    if (!linksWin.isMinimized() && !linksWin.isFullScreen()) settings.linksBounds = linksWin.getBounds();
    saveSettings();
  });
  linksWin.on('closed', () => { linksWin = null; });
}

/* ── 자료 내보내기 ──
   할 일과 링크는 브라우저 저장소(localStorage)에 있어 다른 프로그램이 읽기 어렵다.
   네이티브(WPF) 판이 가져갈 수 있도록 바뀔 때마다 userData/export.json 에 사본을 적는다. */
const exportFile = () => path.join(app.getPath('userData'), 'export.json');
let exportData = null;
let exportTimer = null;
function setExport(key, value) {
  if (!['memos', 'links', 'newestFirst'].includes(key)) return;
  if (!exportData) {
    try { exportData = JSON.parse(fs.readFileSync(exportFile(), 'utf8')); } catch { exportData = {}; }
  }
  exportData[key] = value;
  // 입력할 때마다 쓰지 않도록 잠깐 모았다가 한 번에 쓴다
  clearTimeout(exportTimer);
  exportTimer = setTimeout(flushExport, 400);
}
function flushExport() {
  clearTimeout(exportTimer);
  if (!exportData) return;
  try {
    const tmp = exportFile() + '.tmp';
    fs.writeFileSync(tmp, JSON.stringify(exportData, null, 2));
    fs.renameSync(tmp, exportFile());
  } catch {}
}

/* ── 앱 수명주기 ── */
if (!app.requestSingleInstanceLock()) {
  app.quit();
} else {
  app.on('second-instance', () => { if (win) showWindow(); });

  app.whenReady().then(() => {
    nativeTheme.themeSource = 'light';
    Menu.setApplicationMenu(null);
    migrateUserData();
    loadSettings();
    if (settings.lang !== 'ko' && settings.lang !== 'en') askLanguage();
    createWindow();
  });

  app.on('window-all-closed', () => app.quit());
  app.on('before-quit', flushExport);
  ipcMain.on('data:export', (_e, key, value) => setExport(key, value));

  ipcMain.handle('menu:state', () => menuState());
  ipcMain.handle('link:set', (_e, value) => { setLink(value); return menuState(); });
  ipcMain.handle('menu:action', (_e, name) => {
    switch (name) {
      case 'quit': app.quit(); break;
      case 'toggleAlwaysOnTop': setAlwaysOnTop(!settings.alwaysOnTop); break;
      case 'toggleMinimizeToTray': setMinimizeToTray(!settings.minimizeToTray); break;
      case 'toggleDeleteLock': setDeleteLock(!settings.deleteLock); break;
      case 'langKo': setLang('ko'); break;
      case 'langEn': setLang('en'); break;
      case 'about': showAbout(); break;
      case 'openLinks': openLinksWindow(); break;
    }
    return menuState();
  });
}
