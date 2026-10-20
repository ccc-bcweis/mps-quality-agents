// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text.Json;

using Mps.Api.Auth;

namespace Mps.Api.Access;

public sealed record CreateJoinLinkBody(double? Hours, int? MaxUses);

public sealed record RestoreJoinLinkBody(string? Token, double? Hours, int? MaxUses);

public sealed record RevokeMemberBody(string? MemberKey);

public sealed record JoinLinkResponse(Guid Id, string Url, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, int MaxUses, int Uses, bool Revoked, string Status, string Label);

public sealed record JoinMemberResponse(
    string? Email,
    string? DisplayName,
    DateTimeOffset JoinedAt,
    string LinkLabel,
    string Status,
    string MemberKey,
    string? TenantId,
    string? ObjectId,
    DateTimeOffset? RevokedAt);

public sealed record JoinLinksResponse(IReadOnlyList<JoinLinkResponse> Links, IReadOnlyList<JoinMemberResponse> Members);

public sealed record RedeemResponse(string Result, string Message);

/// <summary>
/// Join links: the admin makes, lists, revokes, restores and reactivates them. A signed-in caller redeems one to join.
/// </summary>
public static class AccessEndpoints
{
    public static void MapAccess(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapGet("/api/admin/join-links", async (HttpContext http, IConfiguration configuration, IJoinLinkStore store, TimeProvider time, CancellationToken cancellationToken) =>
        {
            if (!AccessGate.IsAdmin(http, configuration))
            {
                return Forbidden();
            }

            return Results.Ok(await ListAsync(http, store, time, cancellationToken).ConfigureAwait(false));
        });
        app.MapPost("/api/admin/join-links", async (CreateJoinLinkBody? body, HttpContext http, IConfiguration configuration, IJoinLinkStore store, TimeProvider time, CancellationToken cancellationToken) =>
        {
            if (!AccessGate.IsAdmin(http, configuration))
            {
                return Forbidden();
            }

            if (!TryWindow(body?.Hours, body?.MaxUses, out var lifetime, out var uses))
            {
                return WindowProblem();
            }

            var by = Caller(http);
            var link = await store.CreateAsync(lifetime, uses, by, cancellationToken).ConfigureAwait(false);
            return Results.Ok(ToResponse(http, link, time.GetUtcNow()));
        });
        app.MapPost("/api/admin/join-links/restore", async (HttpContext http, IConfiguration configuration, IJoinLinkStore store, TimeProvider time, CancellationToken cancellationToken) =>
        {
            if (!AccessGate.IsAdmin(http, configuration))
            {
                return Forbidden();
            }

            RestoreJoinLinkBody? body;
            try
            {
                body = await http.Request.ReadFromJsonAsync<RestoreJoinLinkBody>(cancellationToken).ConfigureAwait(false);
            }
            catch (JsonException)
            {
                return NotALink();
            }

            if (!JoinToken.TryParse(body?.Token, out var token))
            {
                return NotALink();
            }

            if (!TryWindow(body?.Hours, body?.MaxUses, out var lifetime, out var uses))
            {
                return WindowProblem();
            }

            var restored = await store.RestoreAsync(token, lifetime, uses, Caller(http), cancellationToken).ConfigureAwait(false);
            if (restored.Outcome == RestoreOutcome.AlreadyActive)
            {
                return Results.Problem(title: "That join link is already active.", statusCode: StatusCodes.Status409Conflict);
            }

            return restored.Link is { } link
                ? Results.Ok(ToResponse(http, link, time.GetUtcNow()))
                : Results.Problem(title: "That join link was not found.", statusCode: StatusCodes.Status404NotFound);
        });
        app.MapPost("/api/admin/join-links/{id:guid}/reactivate", async (Guid id, HttpContext http, IConfiguration configuration, IJoinLinkStore store, CancellationToken cancellationToken) =>
        {
            if (!AccessGate.IsAdmin(http, configuration))
            {
                return Forbidden();
            }

            return await store.ReactivateLinkAsync(id, cancellationToken).ConfigureAwait(false)
                ? Results.NoContent()
                : Results.Problem(title: "That join link was not found.", statusCode: StatusCodes.Status404NotFound);
        });
        app.MapDelete("/api/admin/join-links/{id:guid}", async (Guid id, HttpContext http, IConfiguration configuration, IJoinLinkStore store, CancellationToken cancellationToken) =>
        {
            if (!AccessGate.IsAdmin(http, configuration))
            {
                return Forbidden();
            }

            return await store.RevokeLinkAsync(id, cancellationToken).ConfigureAwait(false)
                ? Results.NoContent()
                : Results.Problem(title: "That join link was not found.", statusCode: StatusCodes.Status404NotFound);
        });
        app.MapPost("/api/admin/members/revoke", async (HttpContext http, IConfiguration configuration, IJoinLinkStore store, CancellationToken cancellationToken) =>
        {
            if (!AccessGate.IsAdmin(http, configuration))
            {
                return Forbidden();
            }

            RevokeMemberBody? body;
            try
            {
                body = await http.Request.ReadFromJsonAsync<RevokeMemberBody>(cancellationToken).ConfigureAwait(false);
            }
            catch (JsonException)
            {
                return Results.Problem(title: "Choose a member to revoke.", statusCode: StatusCodes.Status400BadRequest);
            }

            if (string.IsNullOrWhiteSpace(body?.MemberKey))
            {
                return Results.Problem(title: "Choose a member to revoke.", statusCode: StatusCodes.Status400BadRequest);
            }

            return await store.RevokeMemberAsync(body.MemberKey, cancellationToken).ConfigureAwait(false)
                ? Results.NoContent()
                : Results.Problem(title: "That member was not found.", statusCode: StatusCodes.Status404NotFound);
        });
        app.MapPost("/api/join/{token}", async (string token, HttpContext http, IJoinLinkStore store, CancellationToken cancellationToken) =>
        {
            var identity = CallerIdentity.Read(http.Request.Headers[EasyAuthPrincipal.HeaderName].ToString());
            if (identity.MemberKey is null)
            {
                return Results.Problem(title: "Sign in with a Microsoft account first.", statusCode: StatusCodes.Status401Unauthorized);
            }

            var result = await store.RedeemAsync(token, identity, cancellationToken).ConfigureAwait(false);
            return result switch
            {
                RedeemResult.Joined => Results.Ok(new RedeemResponse("joined", "You have joined.")),
                RedeemResult.AlreadyMember => Results.Ok(new RedeemResponse("alreadyMember", "You are already a member.")),
                RedeemResult.Expired => Results.Problem(title: "This join link has expired. Ask for a new one.", statusCode: StatusCodes.Status410Gone),
                RedeemResult.UsedUp => Results.Problem(title: "This join link has been used the maximum number of times. Ask for a new one.", statusCode: StatusCodes.Status410Gone),
                RedeemResult.Revoked => Results.Problem(title: "This join link was revoked. Ask for a new one.", statusCode: StatusCodes.Status410Gone),
                _ => Results.Problem(title: "This join link is not valid.", statusCode: StatusCodes.Status404NotFound),
            };
        });
    }

    private static async Task<JoinLinksResponse> ListAsync(HttpContext http, IJoinLinkStore store, TimeProvider time, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var links = await store.ListLinksAsync(cancellationToken).ConfigureAwait(false);
        var members = await store.ListMembersAsync(cancellationToken).ConfigureAwait(false);
        return new JoinLinksResponse(
            links.Select(link => ToResponse(http, link, now)).ToArray(),
            members.Select(member => new JoinMemberResponse(
                member.Email,
                member.DisplayName,
                member.JoinedAt,
                member.LinkLabel,
                member.Status,
                member.MemberKey,
                member.TenantId,
                member.ObjectId,
                member.RevokedAt)).ToArray());
    }

    private static JoinLinkResponse ToResponse(HttpContext http, JoinLink link, DateTimeOffset now)
    {
        var url = http.Request.Scheme + "://" + http.Request.Host + "/join/" + link.Token;
        return new JoinLinkResponse(link.Id, url, link.CreatedAt, link.ExpiresAt, link.MaxUses, link.Uses, link.Revoked, link.StatusAt(now), link.Label);
    }

    private static string Caller(HttpContext http) =>
        CallerIdentity.Read(http.Request.Headers[EasyAuthPrincipal.HeaderName].ToString()).Email ?? "admin";

    private static IResult Forbidden() =>
        Results.Problem(title: "Only the admin can manage join links.", statusCode: StatusCodes.Status403Forbidden);

    private static IResult NotALink() =>
        Results.Problem(title: "That is not a join link. Paste the link or the token.", statusCode: StatusCodes.Status400BadRequest);

    private static IResult WindowProblem() =>
        Results.Problem(title: "Choose an expiry between 1 minute and 30 days and 1 to 500 uses.", statusCode: StatusCodes.Status400BadRequest);

    private static bool TryWindow(double? hours, int? maxUses, out TimeSpan lifetime, out int uses)
    {
        var chosen = hours ?? 72d;
        uses = maxUses ?? 5;
        if (!double.IsFinite(chosen) || chosen < (1d / 60d) || chosen > (24d * 30d) || uses is < 1 or > 500)
        {
            lifetime = default;
            return false;
        }

        lifetime = TimeSpan.FromHours(chosen);
        if (lifetime < TimeSpan.FromMinutes(1))
        {
            lifetime = TimeSpan.FromMinutes(1);
        }

        if (lifetime > TimeSpan.FromDays(30))
        {
            lifetime = TimeSpan.FromDays(30);
        }

        return true;
    }
}
