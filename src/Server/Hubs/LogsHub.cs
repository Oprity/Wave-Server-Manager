// AC Server Manager — https://github.com/ (see README)
// Copyright (C) 2026 AC Server Manager contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.AspNetCore.SignalR;

namespace AcServerManager.Hubs;

/// <summary>
/// Streams acServer stdout lines to the web UI. Clients join group
/// <c>profile-{id}</c> and receive <c>LogReceived</c> events.
/// </summary>
public sealed class LogsHub : Hub
{
    public static string GroupName(string profileId) => $"profile-{profileId.ToLowerInvariant()}";

    public Task JoinProfile(string profileId)
    {
        RequireGuid(profileId);
        return Groups.AddToGroupAsync(Context.ConnectionId, GroupName(profileId));
    }

    public Task LeaveProfile(string profileId)
    {
        RequireGuid(profileId);
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(profileId));
    }

    private static void RequireGuid(string profileId)
    {
        if (!Guid.TryParse(profileId, out _))
        {
            throw new HubException("Invalid profile id.");
        }
    }
}
