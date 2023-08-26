#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS base
WORKDIR /app
EXPOSE 8197

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS restore
WORKDIR /src
COPY ["nuget.config", "."]
COPY ["./src/ZiraLink.Client/ZiraLink.Client.csproj", "./src/ZiraLink.Client/"]
RUN dotnet restore "./src/ZiraLink.Client/ZiraLink.Client.csproj"

FROM restore AS build
WORKDIR /src
COPY ./src/ ./src/
WORKDIR "./src/ZiraLink.Client"
RUN dotnet build "ZiraLink.Client.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ZiraLink.Client.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

RUN apt-get update && apt-get install -y ca-certificates grep netcat-openbsd iputils-ping iproute2

COPY ./certs/server.pfx /app/server.pfx

COPY ./certs/ca.crt /usr/local/share/ca-certificates/ca.crt
COPY ./certs/server.crt /usr/local/share/ca-certificates/server.crt
COPY ./certs/server.key /usr/local/share/ca-certificates/server.key
RUN update-ca-certificates

ENTRYPOINT ["dotnet", "ZiraLink.Client.dll"]
