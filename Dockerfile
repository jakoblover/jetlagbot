FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/JetlagBot.App/JetlagBot.App.csproj", "src/JetlagBot.App/"]
RUN dotnet restore "src/JetlagBot.App/JetlagBot.App.csproj"

COPY . .
WORKDIR "/src/src/JetlagBot.App"
RUN dotnet publish "JetlagBot.App.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "JetlagBot.App.dll"]
