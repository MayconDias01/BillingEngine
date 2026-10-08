FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY src/BillingEngine.Domain/BillingEngine.Domain.csproj src/BillingEngine.Domain/
COPY src/BillingEngine.Application/BillingEngine.Application.csproj src/BillingEngine.Application/
COPY src/BillingEngine.Infrastructure/BillingEngine.Infrastructure.csproj src/BillingEngine.Infrastructure/
COPY src/BillingEngine.Worker/BillingEngine.Worker.csproj src/BillingEngine.Worker/
RUN dotnet restore src/BillingEngine.Worker/BillingEngine.Worker.csproj

COPY src/ src/
RUN dotnet publish src/BillingEngine.Worker/BillingEngine.Worker.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
RUN addgroup --system app && adduser --system --ingroup app app
USER app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "BillingEngine.Worker.dll"]
