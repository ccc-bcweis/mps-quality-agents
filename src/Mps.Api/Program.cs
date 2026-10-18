// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Api.Hosting;

// `dotnet run` reads the repo's untracked .env (model mode, keys, sign-in); the environment wins over the file.
DotEnvFile.LoadFromRepo(Directory.GetCurrentDirectory());
var builder = WebApplication.CreateBuilder(args);
builder.AddQualityApi();
var app = builder.Build();
app.UseQualityApi();
app.Run();

public partial class Program
{
}
