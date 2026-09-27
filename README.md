# osm2mssql

Importador de datos **OpenStreetMap** a **Microsoft SQL Server** con tipos espaciales nativos (`geography`), más una extensión CLR y una aplicación web de demostración para geocodificación inversa.

> Basado en el proyecto original **osm2mssql** de [Christian Giesswein](https://sessionize.com/christian-giesswein/), publicado en CodePlex (~2011–2012, `osm2mssql.codeplex.com`). Este repositorio mantiene viva esa base, la ha modernizado a .NET Framework 4.8/4.8.1 y la evoluciona como ecosistema de importación geoespacial para SQL Server.

---

## ¿Qué hace?

Convierte extractos de OpenStreetMap (`.osm` XML o `.osm.pbf` binario) en una base de datos SQL Server espacialmente consultable, en una sola operación:

1. **Crea/recrea** la base de datos `[OSM]` (la importación es destructiva: pregunta antes de reemplazar).
2. **Despliega automáticamente** el ensamblado CLR correcto según la versión de SQL Server (2008 o 2012+).
3. **Importa en paralelo** nodos, ways y relations con `SqlBulkCopy` (lotes de 50 000 filas, hasta 12 escrituras concurrentes).
4. **Construye las geometrías dentro del motor SQL** mediante agregados CLR propios (`CreateLineString`, `GeographyUnion`, `ConvertToPolygon`): los ways se convierten en `LineString` y las relations en multipolígonos.
5. **Genera índices espaciales** y el esquema `info.AdminLevels`, listo para geocodificación administrativa (ciudad, código postal, niveles admin 4–10).

## Ecosistema actual

| Proyecto | Tipo | Target | Rol |
|---|---|---|---|
| `osm2mssql.Importer` | WPF (WinExe) | .NET 4.8.1 | Aplicación principal de importación |
| `osm2mssql.OsmDb2008` | Library (SQL CLR) | .NET 4.8 | Extensión CLR para SQL Server 2008 |
| `osm2mssql.OsmDb2012` | Library (SQL CLR) | .NET 4.8.1 | Mismo código compilado contra tipos espaciales v11 (2012+) |
| `osm2mssql.InfoDAL` | Library | .NET 4.8.1 | Capa de datos EF6 + geocodificación inversa (`OsmAccess`) |
| `osm2mssql.WebApp` | ASP.NET MVC 5 + Web API 2 | .NET 4.8.1 | Demo web de búsqueda de POIs y geocodificación inversa |

### Arquitectura del importador

Pipeline de **13 tareas** seleccionables, organizadas en fases (`Tasks/TaskRunner.cs`):

```
Initialize (síncronas)  →  CreateDatabase · InstallDbExtension
Paralela                →  NodeReader · WayReader · RelationReader
Paralela (fin)          →  NodeIndices · WayIndices · RelationIndices
Finish (síncronas)      →  AttributeWriter · BuildLineInDB · CreateRelationInDB · CreateSpatialIndices · ExecuteSqlCommands
```

La UI es una ventana única con: configuración de conexión con prueba en vivo, lista de tareas con estado/duración/velocidad (nodos/seg), barra de progreso y panel de log en vivo. Credenciales persistidas en `osm2mssql.xml` junto al ejecutable.

### Esquema de base de datos

- **Tablas crudas**: `tNode`, `tNodeTag`, `tWay`, `tWayTag`, `tWayCreation`, `tRelation`, `tRelationTag`, `tRelationCreation`
- **Diccionarios**: `tTagType`, `tMemberType`, `tMemberRole`
- **Esquema `info.`**: `info.AdminLevels` con índice espacial
- **Ensamblado CLR** `osm2mssqlSqlExtension` con funciones y agregados espaciales (SRID 4326)

### Aplicación web de demostración

Demo de mapa interactivo con **OpenLayers 10** (vendido localmente, sin CDNs de runtime): busca nodos por tipo de tag y texto (`Contains`, top 20 ordenados por nombre) y vuela al punto seleccionado con marcador. Al hacer clic en cualquier punto del mapa responde con **geocodificación inversa real** sobre `info.AdminLevels` (la zona administrativa más específica, mayor `admin_level`). Conecta contra la misma BD `[OSM]` generada por el importador; interfaz en español.

## Requisitos

- Windows + **.NET Framework 4.8/4.8.1**
- **SQL Server 2008 o superior** con CLR habilitado (`sp_configure 'clr enabled', 1`)
- Visual Studio 2019+ (compilación) o MSBuild
- Extracto OSM: descarga regional desde [Geofabrik](https://download.geofabrik.de/) (p. ej. [`ecuador-latest.osm.pbf`](https://download.geofabrik.de/south-america/ecuador-latest.osm.pbf))

## Compilar

```bat
msbuild osm2mssql.sln /p:Configuration=Release /p:Platform="Mixed Platforms" /m
```

Los paquetes NuGet ya están en `packages/`; si se borran, el restore usa `.nuget/NuGet.exe`.

## Uso

1. Ejecuta `osm2mssql.exe`, configura host/BD/usuario/contraseña y pulsa *Try to connect*.
2. Pulsa *Start import* y selecciona el archivo `.osm` / `.osm.pbf`.
3. **Ojo**: la BD existente se elimina y recrea.
4. Logs rotados en `Logfiles\OsmServiceLog_*.txt`.

### Modo línea de comandos

El mismo `osm2mssql.exe` funciona **headless**: si se lanza con argumentos no
abre ventana y ejecuta la importación en consola, logueando siempre a archivo.
Ejemplo mínimo:

```bat
osm2mssql.exe --file ecuador-latest.osm.pbf --host localhost --database OSM --trusted --replace
```

Sin argumentos, se abre la interfaz gráfica como siempre. Sintaxis completa,
tabla de códigos de salida, selección de tareas y ejemplos de tarea programada
(Windows Scheduler) en [docs/CLI.md](docs/CLI.md).

Antes de usar la WebApp, ajusta los connection strings en `osm2mssql.Importer/app.config` y `osm2mssql.WebApp/Web.config` (por defecto apuntan a `(localdb)\v11.0`).

## Limitaciones conocidas

- Importación destructiva: sin reanudación ni modo incremental.
- UI solo en inglés (mecanismo de localización preparado, un solo diccionario).
- WebApp de demostración con mapa interactivo Leaflet y geocodificación inversa; requiere la BD importada con el esquema `info.` para la función inversa.
- Sin suite de tests.

---

## Hoja de ruta

### UX / IX

- [ ] **Español + localización completa**: añadir diccionarios `Languages.*.xaml` (ES primero — la pestaña principal ya está en español), selector de idioma en la UI.
- [ ] **Ventana de importación rediseñada**: asistente por pasos (conexión → archivo → tareas → resumen), progreso determinado (MB leídos / registros, no solo indeterminado), estimación de tiempo restante.
- [ ] **Modo no destructivo**: opción "importar en BD nueva" / "solo regenerar geometrías", con aviso claro de pérdida de datos.
- [ ] **Reanudación tras error**: reintentar tarea fallida sin reiniciar todo el pipeline.
- [x] **WebApp**: mapa interactivo (Leaflet/OpenLayers) en lugar del mapa estático caído; resultados en lista con zoom al punto; búsqueda inversa real (coordenada → ciudad/admin level) usando `info.AdminLevels`.
- [ ] **Accesibilidad y consistencia visual**: temas claros/oscuros, iconografía vectorial, teclado.

### Optimización y mejora técnica

- [ ] **Migrar a .NET 8 (LTS)**: los tres proyectos a SDK-style + `PackageReference`; el ensamblado SQL CLR es el único punto delicado (SQL CLR requiere .NET Framework — evaluar migrar la lógica espacial a funciones T-SQL nativas `STGeomFromText`/spatial aggregates o mantener el CLR en net48 como proyecto hermano).
- [ ] **Reemplazar geometrías CLR por T-SQL nativo** en SQL Server 2012+: `STRING_AGG` + `geography::STGeomFromText` eliminaría el ensamblado UNSAFE y el despliegue por bytes hex.
- [ ] **PbfOsmReader con spans/`System.IO.Pipelines`** y lectura asíncrona para mayor throughput; batching adaptativo según memoria.
- [ ] **Tablas staging temporales** para `tWayCreation`/`tRelationCreation` y `TABLOCK` en bulk copy.
- [ ] **Modo incremental** (diff/updates con `osmosis`/`osmium` replication) — convierte el importador en un servicio de sincronización.
- [x] **CLI headless**: mismas tareas ejecutables por línea de comandos para automatizar/CI (`osm2mssql --file x.pbf --db OSM --tasks all`). Ver [docs/CLI.md](docs/CLI.md).
- [ ] **Tests**: suite sobre `OsmDb` (agregados espaciales) y readers con extractos pequeños de prueba; CI con GitHub Actions + SQL Server container.
- [ ] **Modernizar WebApp**: ASP.NET Core + minimal API + front SPA, o integrar la demo como vista del propio importador.
- [ ] **Documentar el despliegue**: script de creación de BD, permisos CLR, y diagrama del esquema.

## Créditos

- **Autor original**: Christian Giesswein — [osm2mssql en CodePlex](https://archive.codeplex.com/?p=osm2mssql) (proyecto archivado, ~2011–2012).
- **Mantenimiento actual**: [gmolina75](https://github.com/gmolina75/osm2mssql) — actualización a VS 2019/.NET 4.8, adaptación regional (Ecuador) y evolución del ecosistema.

Los datos © colaboradores de [OpenStreetMap](https://www.openstreetmap.org), licencia ODbL.
