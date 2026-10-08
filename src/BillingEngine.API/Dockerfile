FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY src/BillingEngine.Domain/BillingEngine.Domain.csproj src/BillingEngine.Domain/
COPY src/BillingEngine.Application/BillingEngine.Application.csproj src/BillingEngine.Application/
COPY src/BillingEngine.Infrastructure/BillingEngine.Infrastructure.csproj src/BillingEngine.Infrastructure/
COPY src/BillingEngine.API/BillingEngine.API.csproj src/BillingEngine.API/
RUN dotnet restore src/BillingEngine.API/BillingEngine.API.csproj

COPY src/ src/
RUN dotnet publish src/BillingEngine.API/BillingEngine.API.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
RUN addgroup --system app && adduser --system --ingroup app app
USER app
COPY --from=build /app/publish .
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --retries=3 CMD curl -f http://localhost:8080/health/live || exit 1
ENTRYPOINT ["dotnet", "BillingEngine.API.dll"]
