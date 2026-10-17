# Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props global.json nuget.config MPS.sln ./
COPY src/ src/
COPY prompts/ prompts/
COPY eval/results/ eval/results/
RUN dotnet restore src/Mps.Api/Mps.Api.csproj
RUN dotnet publish src/Mps.Api/Mps.Api.csproj --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Mps.Api.dll"]
