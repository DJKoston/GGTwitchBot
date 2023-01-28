#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["GGTwitchBot.Bot/GGTwitchBot.Bot.csproj", "GGTwitchBot.Bot/"]
COPY ["GGTwitchBot.Core/GGTwitchBot.Core.csproj", "GGTwitchBot.Core/"]
COPY ["GGTwitchBot.DAL/GGTwitchBot.DAL.csproj", "GGTwitchBot.DAL/"]
COPY ["GGTwitchBot.DAL.Migrations/GGTwitchBot.DAL.Migrations.csproj", "GGTwitchBot.DAL.Migrations/"]
RUN dotnet restore "GGTwitchBot.Bot/GGTwitchBot.Bot.csproj"
COPY . .
WORKDIR "/src/GGTwitchBot.Bot"
RUN dotnet build "GGTwitchBot.Bot.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "GGTwitchBot.Bot.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TwitchBot.dll"]