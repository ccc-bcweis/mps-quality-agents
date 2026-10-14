// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = Mps.Api.LayerAnchor.Name }));

app.Run();
