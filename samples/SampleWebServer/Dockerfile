#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["./SampleWebServer.csproj", "./"]
RUN dotnet restore "./SampleWebServer.csproj"
COPY . .
WORKDIR "."
RUN dotnet build "SampleWebServer.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "SampleWebServer.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

COPY ./certs/server.pfx /app/server.pfx

COPY ./certs/ca.crt /usr/local/share/ca-certificates/ca.crt
COPY ./certs/server.crt /usr/local/share/ca-certificates/server.crt
COPY ./certs/server.key /usr/local/share/ca-certificates/server.key
RUN update-ca-certificates

ENTRYPOINT ["dotnet", "SampleWebServer.dll"]
