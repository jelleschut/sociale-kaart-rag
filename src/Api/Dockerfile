FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props ./
COPY src/Core/SocialeKaartRag.Core.csproj src/Core/
COPY src/Api/SocialeKaartRag.Api.csproj src/Api/
RUN dotnet restore src/Api/SocialeKaartRag.Api.csproj
COPY src/Core src/Core
COPY src/Api src/Api
RUN dotnet publish src/Api/SocialeKaartRag.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "SocialeKaartRag.Api.dll"]
