FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY PclModpackUpdater.Server.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV PCLUPDATER_DATA=/data
VOLUME /data
EXPOSE 8080
ENTRYPOINT ["dotnet", "PclModpackUpdater.Server.dll"]
