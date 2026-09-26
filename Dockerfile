# syntax=docker/dockerfile:1

# ---------- Etapa 1: restauracion de paquetes ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore
WORKDIR /src

# Solo el manifiesto del proyecto: esta capa se reutiliza mientras no cambien
# las dependencias, sin copiar todavia el codigo fuente.
COPY ExamenParcial.csproj ./
RUN dotnet restore

# ---------- Etapa 2: compilacion y publicacion ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Se copia el codigo fuente. bin/ y obj/ del equipo local quedan fuera por
# .dockerignore y, ademas, se excluyen de forma explicita en este COPY.
COPY --exclude=**/bin --exclude=**/obj . .

# De la etapa de restore solo se hereda la cache de paquetes NuGet (~/.nuget/packages),
# NUNCA su carpeta obj/: aqui el obj/ se genera dentro del contenedor, de forma
# coherente con este sistema de archivos y sin rutas de la maquina local.
COPY --from=restore /root/.nuget/packages /root/.nuget/packages

# publish ejecuta su propio restore (idempotente y ya servido por la cache anterior).
RUN dotnet publish ExamenParcial.csproj -c Release -o /app/publish /p:UseAppHost=false

# ---------- Etapa 3: imagen final (solo ASP.NET Core Runtime) ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# APP_UID viene definido en la imagen: la app corre como usuario sin privilegios.
COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .
# /app debe ser escribible porque SQLite crea ExamenParcial.db en el directorio de trabajo.
RUN chown $APP_UID:$APP_UID /app
USER $APP_UID

ENV PORT=8080
ENV ASPNETCORE_ENVIRONMENT=Production
# Puerto fijo de escucha: define en Render la variable de entorno PORT=8080
# para que coincida con el puerto que Render enruta al contenedor.
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "ExamenParcial.dll"]
