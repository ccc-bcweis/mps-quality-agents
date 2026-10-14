// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Api.Hosting;

var builder = WebApplication.CreateBuilder(args);
builder.AddQualityApi();
var app = builder.Build();
app.UseQualityApi();
app.Run();

public partial class Program
{
}
