// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Microsoft.Extensions.DependencyInjection;

using Mps.MockEnterprise;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<MockOptions>(builder.Configuration.GetSection(MockOptions.SectionName));
builder.Services.ConfigureHttpJsonOptions(EnterpriseJson.Configure);
builder.Services.AddSingleton<EnterpriseStore>();

var app = builder.Build();
app.MapEnterpriseEndpoints();
app.Run();

public partial class Program
{
}
