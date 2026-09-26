# Modo línea de comandos (headless)

El mismo `osm2mssql.exe` puede ejecutarse **sin abrir la ventana WPF**: si se
lanza con argumentos, la aplicación ejecuta la importación en consola, muestra
el progreso por pantalla (si hay terminal) y **siempre** escribe un log en
archivo. Si se lanza sin argumentos, el comportamiento es el de siempre:
se abre la interfaz gráfica.

Esto permite automatizar la importación (tareas programadas, scripts, CI)
usando exactamente el mismo pipeline de 13 tareas que la UI.

## Sintaxis

```
osm2mssql --file <archivo.osm|.osm.pbf> --host <servidor> --database <base>
          [--user <usuario> --password <clave>] [--trusted] [--replace]
          [--tasks <all|init,reader,...>] [--logfile <ruta>] [--help]
```

También se acepta el archivo como primer argumento posicional:

```
osm2mssql ecuador-latest.osm.pbf --database OSM --replace
```

Los nombres de opción no distinguen mayúsculas de minúsculas y admiten tanto
`--opcion valor` como `--opcion=valor`. La ayuda completa está disponible con
`osm2mssql --help`.

## Parámetros

| Parámetro | Obligatorio | Descripción | Ejemplo |
|---|---|---|---|
| `--file` | Sí | Ruta del extracto OSM (`.osm` o `.osm.pbf`). Se valida que exista y tenga extensión soportada. También como argumento posicional. | `--file ecuador-latest.osm.pbf` |
| `--host` | Sí¹ | Servidor SQL Server. | `--host localhost` o `--host srv01\SQLEXPRESS` |
| `--database` | Sí¹ | Base de datos de destino. | `--database OSM` |
| `--user` | No² | Usuario de autenticación SQL. Requiere `--password`. | `--user osmimport` |
| `--password` | No² | Contraseña del usuario SQL. | `--password "Clave!"` |
| `--trusted` | No² | Autenticación integrada de Windows (la cuenta que ejecuta el proceso). Excluyente con `--user`/`--password`. | `--trusted` |
| `--replace` | No | **Elimina y recrea** la base de datos si ya existe. Es obligatorio cuando la BD existe: sin él, la ejecución falla con el código de salida 3. Nunca pregunta interactivamente. | `--replace` |
| `--tasks` | No | Subconjunto de tareas (por defecto `all`). Ver tabla siguiente. | `--tasks init,reader` |
| `--logfile` | No | Ruta del archivo de log. Por defecto `Logfiles\OsmCliLog_yyyyMMdd.txt` **relativo al directorio actual**. | `--logfile D:\logs\osm.txt` |
| `--help` | No | Muestra la ayuda en español y termina (código de salida 1). | `--help` |

¹ Puede omitirse si el valor está guardado en `osm2mssql.xml` (ver siguiente sección).
² Si no se indica `--trusted` ni `--user`/`--password`, se usan las credenciales
  guardadas en `osm2mssql.xml`.

### Configuración por defecto desde la UI (`osm2mssql.xml`)

Si no se pasa `--trusted` ni `--user`/`--password`, el importador lee
`osm2mssql.xml` (la configuración que guarda la interfaz gráfica junto al
ejecutable) y la usa como **origen de valores por defecto** para host, base de
datos y credenciales. Los argumentos explícitos siempre tienen prioridad.

Esto permite, por ejemplo, guardar la conexión una vez desde la UI y después
lanzar la tarea programada solo con:

```
osm2mssql --file ecuador-latest.osm.pbf --database OSM --trusted --replace
```

Ten en cuenta que `osm2mssql.xml` se busca en el **directorio de trabajo
actual**, no junto al exe. En tareas programadas, fija el directorio de
trabajo (ver más abajo) o pasa `--host`/`--database` explícitos.

> En modo CLI el archivo `osm2mssql.xml` **nunca se sobrescribe**: sigue siendo
> propiedad exclusiva de la interfaz gráfica. La contraseña se descifra en
> memoria (DPAPI) solo para usarla en la conexión.

### Selección de tareas (`--tasks`)

Por defecto se ejecutan todas (`all`). Los grupos disponibles:

| Clave | Tareas que activa |
|---|---|
| `init` | CreateDatabase, InstallDbExtension |
| `reader` | NodeReader, WayReader, RelationReader |
| `indices` | NodeIndices, WayIndices, RelationIndices |
| `attribute` | AttributeWriter |
| `line` | CreateLineInDB |
| `relation` | CreateRelationInDB |
| `spatial` | CreateSpatialIndices |
| `sql` | ExecuteSqlCommands |

También se admiten nombres individuales de tarea (`CreateDatabase`,
`NodeReader`, ...) y cualquier combinación separada por comas:

```
--tasks init,reader
--tasks CreateDatabase,NodeReader
```

## Códigos de salida

El proceso devuelve siempre un código de salida que puede comprobarse con
`%ERRORLEVEL%` (cmd), `$LASTEXITCODE` (PowerShell) o `$?` (bash):

| Código | Significado |
|---|---|
| `0` | Importación completada con éxito. |
| `1` | Argumentos inválidos (o `--help` mostrado). El mensaje de error indica el problema. |
| `2` | No se pudo conectar al servidor SQL (host inaccesible, credenciales incorrectas, etc.). |
| `3` | La base de datos ya existe y no se indicó `--replace`. No se ha tocado la BD. |
| `4` | Falló una tarea durante la importación. El mensaje indica qué tarea falló y por qué. |
| `5` | Archivo de entrada inválido: no existe o la extensión no es `.osm`/`.pbf`. |

## Salida: consola y log

- **Consola**: por cada tarea se imprime una línea
  `[hh:mm:ss] NOMBRE ... iniciada / completada (duración) / ERROR (duración)`,
  más un resumen final con la duración total.
- **Archivo**: el log **siempre** se escribe, esté o no visible una consola.
  Por defecto va a `Logfiles\OsmCliLog_yyyyMMdd.txt` (con rotación diaria,
  mismo mecanismo que el log de la UI) **relativo al directorio de trabajo
  actual**. Usa `--logfile <ruta absoluta>` para fijar una ubicación concreta.

> El ejecutable es un WinExe: si se lanza desde una consola existente la
> reutiliza (`AttachConsole`), si la salida está redirigida (p. ej.
> `> salida.txt` en una tarea programada) escribe directamente al flujo, y si
> se lanza sin consola crea una propia. Todo ello es transparente y nunca
> interrumpe la ejecución.

## Advertencia: la importación es destructiva

La importación **elimina y recrea** la base de datos de destino. El modo CLI
hereda el espíritu no destructivo de la UI, que pregunta antes de reemplazar:

- Si la BD **no existe**, se crea (necesitas permisos de `CREATE DATABASE`).
- Si la BD **existe**, debes pasar `--replace` explícitamente; en caso contrario
  el proceso falla con el código `3` **sin tocar la base de datos**.

No hay preguntas interactivas: `--replace` es tu confirmación.

## Ejemplos

### (a) Importación local simple

```
osm2mssql.exe --file ecuador-latest.osm.pbf --host localhost --database OSM --trusted --replace
```

Autenticación integrada de Windows contra el SQL Server local, recreando la BD
`OSM` si existe.

### (b) Servidor remoto con autenticación SQL

```
osm2mssql.exe --file ecuador-latest.osm.pbf --host 192.168.1.10 --database OSM --user osmimport --password "Clave!" --replace
```

### (c) Tarea programada: importación semanal de Ecuador

Script PowerShell `Importar-Ecuador.ps1` (descarga el extracto de Geofabrik y
lanza la importación):

```powershell
$ErrorActionPreference = 'Stop'

# Rutas ABSOLUTAS: en una tarea programada el directorio de trabajo puede ser
# System32 si no se configura, asi que no uses rutas relativas.
$Exe      = 'C:\osm2mssql\osm2mssql.exe'
$Dir      = 'C:\osm2mssql\datos'
$Pbf      = Join-Path $Dir 'ecuador-latest.osm.pbf'
$Url      = 'https://download.geofabrik.de/south-america/ecuador-latest.osm.pbf'

New-Item -ItemType Directory -Force -Path $Dir | Out-Null

# Descarga con reintento (Invoke-WebRequest); con curl.exe seria:
#   curl.exe -fSL --retry 3 -o $Pbf $Url
Invoke-WebRequest -Uri $Url -OutFile $Pbf -UseBasicParsing

& $Exe --file $Pbf --host localhost --database OSM --trusted --replace `
       --logfile "C:\osm2mssql\logs\import.txt"

$code = $LASTEXITCODE
if ($code -ne 0) {
    throw "La importacion fallo con codigo de salida $code (ver C:\osm2mssql\logs y Logfiles)"
}
```

Creación con `schtasks` (semanal, domingos a las 03:30, con el usuario
`SERVIDOR\osmimport` que tiene acceso a SQL Server):

```bat
schtasks /Create /TN "OSM\Importar Ecuador" /SC WEEKLY /D SUN /ST 03:30 ^
  /RU "SERVIDOR\osmimport" /RP ^
  /TR "powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"C:\osm2mssql\Importar-Ecuador.ps1\"" ^
  /RL HIGHEST /F
```

**Importante — directorio de trabajo:** el Programador de tareas arranca los
procesos con el directorio de trabajo que se indique (o `System32` si no se
indica). Como `osm2mssql.xml` y el log por defecto `Logfiles\` se resuelven
respecto al directorio de trabajo, en el script anterior se usan **rutas
absolutas** (`--file`, `--logfile`) y no se depende de `osm2mssql.xml`.
Alternativamente, añade `Set-Location 'C:\osm2mssql'` al principio del script.

Para ejecutarlo ahora mismo y comprobarlo: `schtasks /Run /TN "OSM\Importar Ecuador"`
y consulta el resultado con `schtasks /Query /TN "OSM\Importar Ecuador" /V /FO LIST`.

### (d) Integración en CI / batch con chequeo de exit code

En un `.bat`:

```bat
osm2mssql.exe --file data\prueba.osm.pbf --host localhost --database OSM_CI --trusted --replace
if errorlevel 1 (
    echo La importacion fallo con codigo %ERRORLEVEL% >> build-errors.log
    exit /b %ERRORLEVEL%
)
```

En PowerShell se usa `$LASTEXITCODE`; en un pipeline de CI basta con ejecutar el
exe directamente: cualquier código distinto de 0 marca el paso como fallido.

## Notas técnicas

- Sin argumentos → la app abre la interfaz gráfica exactamente igual que antes
  (`App.xaml.cs` decide en `OnStartup` según `e.Args`).
- El pipeline, el orden de las fases y las conexiones son los mismos que usa la
  UI (`TaskRunner.RunTasks`); el CLI solo selecciona qué tareas están activas.
- El progreso por tarea se obtiene suscribiéndose a `PropertyChanged` de cada
  `TaskBase`; el detalle interno llega a través del `System.Diagnostics.Trace`
  existente (`CliConsoleTraceListener` a consola y `OsmTextWriterTraceListener`
  a archivo, con la misma rotación diaria que el log de servicio).
