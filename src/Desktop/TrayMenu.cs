// AC Server Manager — https://github.com/ (see README)
// Copyright (C) 2026 AC Server Manager contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Drawing;
using System.Windows.Forms;

namespace AcServerManager.Desktop;

/// <summary>
/// Windows system-tray icon with Open / Quit. Kept in its own type so the
/// headless <c>--smoke</c> path never loads WinForms assemblies.
/// </summary>
internal sealed class TrayMenu : IDisposable
{
    private readonly NotifyIcon _icon;
    private bool _disposed;

    public TrayMenu(string title, Action onOpen, Action onQuit)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem("Open", null, (_, _) => onOpen()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Quit", null, (_, _) => onQuit()));

        _icon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = title,
            Visible = true,
            ContextMenuStrip = menu,
        };
        _icon.DoubleClick += (_, _) => onOpen();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _icon.Visible = false;
        _icon.Dispose();
    }
}
