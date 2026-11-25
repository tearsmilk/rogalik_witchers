'use strict';
const { app, BrowserWindow, Menu } = require('electron');
const path = require('path');

function createWindow() {
  const win = new BrowserWindow({
    width: 1280,
    height: 800,
    minWidth: 900,
    minHeight: 600,
    backgroundColor: '#0b120e',
    title: 'Ведьма-ученица',
    webPreferences: { contextIsolation: true, nodeIntegration: false, sandbox: true },
  });
  win.loadFile(path.join(__dirname, 'src', 'index.html'));
}

// Минимальное меню macOS: Выход, Полный экран, копирование.
Menu.setApplicationMenu(Menu.buildFromTemplate([
  { role: 'appMenu' },
  { label: 'Вид', submenu: [{ role: 'togglefullscreen', label: 'Полный экран' }] },
  { role: 'windowMenu' },
]));

app.whenReady().then(() => {
  createWindow();
  app.on('activate', () => { if (BrowserWindow.getAllWindows().length === 0) createWindow(); });
});
app.on('window-all-closed', () => app.quit());
