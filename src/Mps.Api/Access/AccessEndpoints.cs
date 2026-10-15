// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Api.Auth;

namespace Mps.Api.Access;

public sealed record CreateJoinLinkBody(double? Hours, int? MaxUses);

public sealed record JoinLinkResponse(Guid Id, string Url, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, int MaxUses, int Uses, bool Revoked, string Status);

public sealed record JoinMemberResponse(string? Email, string? DisplayName, DateTimeOffset JoinedAt);

public sealed record JoinLinksResponse(IReadOnlyList<JoinLinkResponse> Links, IReadOnlyList<JoinMemberResponse> Members);

public sealed record RedeemResponse(string Result, string Message);

/// <summary>
/// Join links: the admin makes, lists and revokes them; anyone signed in with a Microsoft account redeems one to join the demo.
/// </summary>
public static class AccessEndpoints
{
    public static void MapAccess(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapGet("/api/admin/join-links", async (HttpContext http, IConfiguration configuration, JoinLinkStore store, CancellationToken cancellationToken) =>
        {
            if (!AccessGate.IsAdmin(http, configuration))
            {
                return Results.Problem(title: "Only the demo admin can manage join links.", statusCode: StatusCodes.Status403Forbidden);
            }

            return Results.Ok(await ListAsync(http, store, cancellationToken).ConfigureAwait(false));
        });
        app.MapPost("/api/admin/join-links", async (CreateJoinLinkBody? body, HttpContext http, IConfiguration configuration, JoinLinkStore store, CancellationToken cancellationToken) =>
        {
            if (!AccessGate.IsAdmin(http, configuration))
            {
                return Results.Problem(title: "Only the demo admin can manage join links.", statusCode: StatusCodes.Status403Forbidden);
            }

            var hours = body?.Hours ?? 72d;
            var uses = body?.MaxUses ?? 5;
            if (!double.IsFinite(hours) || hours < 1d / 60d || hours > 24d * 30d || uses is < 1 or > 500)
            {
                return Results.Problem(title: "Choose an expiry between 1 minute and 30 days and 1 to 500 uses.", statusCode: StatusCodes.Status400BadRequest);
            }

            var by = CallerIdentity.Read(http.Request.Headers[EasyAuthPrincipal.HeaderName].ToString()).Email ?? "admin";
            var link = await store.CreateAsync(TimeSpan.FromHours(hours), uses, by, cancellationToken).ConfigureAwait(false);
            return Results.Ok(ToResponse(http, link, DateTimeOffset.UtcNow));
        });
        app.MapDelete("/api/admin/join-links/{id:guid}", async (Guid id, HttpContext http, IConfiguration configuration, JoinLinkStore store, CancellationToken cancellationToken) =>
        {
            if (!AccessGate.IsAdmin(http, configuration))
            {
                return Results.Problem(title: "Only the demo admin can manage join links.", statusCode: StatusCodes.Status403Forbidden);
            }

            return await store.RevokeAsync(id, cancellationToken).ConfigureAwait(false)
                ? Results.NoContent()
                : Results.Problem(title: "That join link was not found.", statusCode: StatusCodes.Status404NotFound);
        });
        app.MapPost("/api/join/{token}", async (string token, HttpContext http, JoinLinkStore store, CancellationToken cancellationToken) =>
        {
            var identity = CallerIdentity.Read(http.Request.Headers[EasyAuthPrincipal.HeaderName].ToString());
            if (identity.MemberKey is null)
            {
                return Results.Problem(title: "Sign in with a Microsoft account first.", statusCode: StatusCodes.Status401Unauthorized);
            }

            var result = await store.RedeemAsync(token, identity, cancellationToken).ConfigureAwait(false);
            return result switch
            {
                RedeemResult.Joined => Results.Ok(new RedeemResponse("joined", "You have joined the demo.")),
                RedeemResult.AlreadyMember => Results.Ok(new RedeemResponse("alreadyMember", "You are already in the demo.")),
                RedeemResult.Expired => Results.Problem(title: "This join link has expired. Ask for a new one.", statusCode: StatusCodes.Status410Gone),
                RedeemResult.UsedUp => Results.Problem(title: "This join link has been used the maximum number of times. Ask for a new one.", statusCode: StatusCodes.Status410Gone),
                RedeemResult.Revoked => Results.Problem(title: "This join link was revoked. Ask for a new one.", statusCode: StatusCodes.Status410Gone),
                _ => Results.Problem(title: "This join link is not valid.", statusCode: StatusCodes.Status404NotFound),
            };
        });
    }

    private static async Task<JoinLinksResponse> ListAsync(HttpContext http, JoinLinkStore store, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var links = await store.ListAsync(cancellationToken).ConfigureAwait(false);
        var members = await store.ListMembersAsync(cancellationToken).ConfigureAwait(false);
        return new JoinLinksResponse(
            links.Select(link => ToResponse(http, link, now)).ToArray(),
            members.Select(member => new JoinMemberResponse(member.Email, member.DisplayName, member.JoinedAt)).ToArray());
    }

    private static JoinLinkResponse ToResponse(HttpContext http, JoinLink link, DateTimeOffset now)
    {
        var status = link.Revoked ? "revoked" : now >= link.ExpiresAt ? "expired" : link.Uses >= link.MaxUses ? "usedUp" : "active";
        var url = http.Request.Scheme + "://" + http.Request.Host + "/join/" + link.Token;
        return new JoinLinkResponse(link.Id, url, link.CreatedAt, link.ExpiresAt, link.MaxUses, link.Uses, link.Revoked, status);
    }
}
