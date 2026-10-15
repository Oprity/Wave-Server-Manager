// Wave Server Manager — https://github.com/ (see README)
// Copyright (C) 2026 Wave Server Manager contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using WaveServerManager.Hosting;

var app = AppHost.Create(args);
app.Run();

// Visible to integration tests (WebApplicationFactory<Program>).
public partial class Program
{
}
