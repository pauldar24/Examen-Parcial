# syntax=docker/dockerfile:1

# ---------- Etapa 1: restauracion de paquetes (cacheable) ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore
WORKDIR /src

# Solo se copian los manifiestos: esta capa se reutiliza mientras no cambien.
COPY ExamenParcial.csproj ./
RUN dotnet restore

# ---------- Etapa 2: compilacion y publicacion ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY --from=restore /src/obj ./obj
COPY . .
RUN dotnet publish ExamenParcial.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------- Etapa 3: imagen final (solo runtime) ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# APP_UID viene definido en la imagen: la app corre como usuario sin privilegios.
COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .
# /app debe ser escribible porque SQLite crea ExamenParcial.db en el directorio de trabajo.
RUN chown $APP_UID:$APP_UID /app
USER $APP_UID

# Render asigna el puerto dinamico en la variable de entorno PORT;
# en local se usa 8080, que es el puerto por defecto de la imagen aspnet.
ENV PORT=8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

# El puerto real se resuelve en tiempo de ejecucion a partir de $PORT.
CMD ["sh", "-c", "dotnet ExamenParcial.dll --urls http://0.0.0.0:${PORT:-8080}"]
