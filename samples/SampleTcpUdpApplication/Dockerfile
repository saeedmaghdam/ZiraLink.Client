#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app
EXPOSE 5555/tcp
EXPOSE 5555/udp

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["./samples/SampleTcpUdpApplication/SampleTcpUdpApplication.csproj", "./"]
RUN dotnet restore "./SampleTcpUdpApplication.csproj"
COPY ./samples/SampleTcpUdpApplication .
RUN dotnet build "SampleTcpUdpApplication.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "SampleTcpUdpApplication.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

COPY ./certs/*.pfx /app/certs/
COPY ./certs/*.crt /usr/local/share/ca-certificates/
COPY ./certs/*.key /usr/local/share/ca-certificates/
RUN update-ca-certificates

ENV PROTOCOL=TCP
CMD ["sh", "-c", "dotnet SampleTcpUdpApplication.dll server --port 5555 --protocol $PROTOCOL"]
