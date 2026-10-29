# BonusTest · Bonos de desempeño

Módulo de Recursos Humanos para buscar empleados activos, registrar bonos de monto fijo o porcentaje del sueldo, consultar un listado paginado, anular y eliminar de forma lógica. Implementado sobre el proyecto base de la prueba técnica, manteniendo .NET 10, Angular 22 y SQL Server.

## Requisitos

- SDK de .NET 10 (`dotnet --version`).
- Node.js compatible con Angular 22 y npm. En este entorno se utiliza Node 24.
- SQL Server 2019 o superior accesible; el entorno local utiliza SQL Server 2022 en Docker mediante Colima.

## Base de datos

Ejecutar en orden en SQL Server, por ejemplo desde DBeaver:

1. `database/01_schema_seed.sql`: tablas existentes y datos semilla. Se conserva sin modificaciones; ejecutar una sola vez en una base nueva.
2. `database/02_bonus.sql`: tabla Bonus, restricciones e índices. Se puede ejecutar nuevamente sin borrar datos.
3. `database/03_bonus_project_allocation.sql`: detalle del reparto por proyecto para el pago. También es reejecutable.

Con el contenedor local `bonustest-sql` ya configurado:

```bash
colima start
docker start bonustest-sql
```

Para ejecutar los scripts desde terminal, sin escribir la contraseña en el historial:

```bash
read -s SQLCMDPASSWORD
export SQLCMDPASSWORD
docker exec -i -e SQLCMDPASSWORD bonustest-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b < database/01_schema_seed.sql
docker exec -i -e SQLCMDPASSWORD bonustest-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b < database/02_bonus.sql
docker exec -i -e SQLCMDPASSWORD bonustest-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b < database/03_bonus_project_allocation.sql
unset SQLCMDPASSWORD
```

Si `01_schema_seed.sql` ya fue ejecutado, ejecutar `02_bonus.sql` y después `03_bonus_project_allocation.sql`. La aplicación no crea ni modifica el esquema al arrancar.

## Backend

La conexión de desarrollo está en `backend/src/BonusTest.Api/appsettings.json`. Ajustar host, usuario y contraseña para tu SQL Server, o sobrescribirla sin editar archivos:

```bash
export ConnectionStrings__BonusTest='Server=localhost,1433;Database=BonusTest;User Id=sa;Password=TU_CLAVE;TrustServerCertificate=True;'
cd backend
dotnet build
dotnet run --project src/BonusTest.Api
```

API: http://localhost:5106. Swagger en desarrollo: http://localhost:5106/swagger.

Para recarga durante desarrollo, desde `backend/`:

```bash
dotnet watch run --project src/BonusTest.Api
```

## Frontend

En otra terminal, desde la raíz del repositorio:

```bash
cd frontend
npm ci
npm start
```

Abrir http://localhost:4200/bonuses. La URL de la API y el código de usuario se configuran en `frontend/src/environments/environment.ts`. El interceptor existente envía `UserCode` y centraliza los errores HTTP, incluidos los de negocio (409). No se incorpora autenticación: el usuario simulado del proyecto base se utiliza para auditoría, como pide la prueba.

## Operaciones

| Método | Ruta | Función |
| --- | --- | --- |
| GET | `/api/v1/Bonus` | Listado paginado |
| GET | `/api/v1/Bonus/employee/{departmentCode}/{employeeNumber}` | Buscar empleado activo |
| POST | `/api/v1/Bonus` | Registrar bono |
| POST | `/api/v1/Bonus/{bonusId}/pay` | Pagar y distribuir el bruto por proyecto |
| PUT | `/api/v1/Bonus/{bonusId}/cancel` | Anular bono registrado |
| DELETE | `/api/v1/Bonus/{bonusId}` | Eliminar lógicamente un bono registrado |

El listado acepta `departmentCode`, `employeeNumber`, `period` (`YYYY-MM`), `stateCode`, `pageIndex` y `pageSize`. La pantalla solicita 10 registros por página; la API admite hasta 50. La búsqueda cuenta, ordena y pagina en SQL Server con `Count`, `OrderBy`, `Skip` y `Take`; solo después da formato al periodo de las filas obtenidas.

Ejemplo de cuerpo para POST (enviar también el header `UserCode`):

```json
{
  "departmentCode": "TECH1",
  "employeeNumber": 101,
  "period": "2026-10",
  "bonusTypeId": 2,
  "value": 10,
  "reasonCode": "000001"
}
```

Con el empleado semilla TECH1-101: bruto 500.00, retención 40.00 y neto 460.00. El periodo debe ser igual o anterior al mes actual y no debe existir otro bono activo del empleado en ese periodo.

## Decisiones técnicas

- **Capas:** Domain define la entidad, sus cambios de estado y el cálculo; Application coordina validaciones y operaciones; Infrastructure implementa EF Core y SQL Server; Api expone HTTP sin reglas de negocio en el controlador.
- **Identidad del empleado:** se conserva la clave compuesta `(DepartmentCode, EmployeeNumber)` en la relación de Bonus.
- **Dinero:** C# `decimal`, con redondeo `MidpointRounding.AwayFromZero` a dos decimales. Primero se redondea el bruto, luego la retención y finalmente el neto. El valor ingresado admite cuatro decimales (`DECIMAL(18,4)`); los resultados se almacenan con dos. La vista previa usa enteros escalados y el servidor recalcula al guardar.
- **Histórico:** cada bono guarda el sueldo y porcentaje de retención utilizados, además de bruto, retención y neto.
- **Periodo:** se almacena como fecha del primer día del mes. La validación del mes actual usa `America/Lima`; la auditoría se guarda como `DateTimeOffset` en UTC. El reloj es inyectable para probar límites de mes.
- **Duplicados:** validación de aplicación más un índice único filtrado por empleado y periodo, excluyendo anulados y eliminados. El índice protege también ante solicitudes simultáneas.
- **Concurrencia:** `rowversion` evita sobrescribir un cambio concurrente al anular o eliminar. Los conflictos devuelven 409 con `{ code, description }`.
- **Eliminación:** `IsDeleted` y filtro global de EF Core. Se conservan los datos y la auditoría; el periodo vuelve a estar disponible.
- **Auditoría:** `CreatedBy`, `CreatedAt`, `ModifiedBy` y `ModifiedAt`. Las escrituras requieren un `UserCode` no vacío.
- **Índices:** el índice único también facilita la búsqueda por empleado; un segundo índice filtrado por periodo/estado apoya los filtros y el orden del listado. El orden tiene desempate por ID para paginar de forma estable.
- **Interfaz:** se mantiene la presentación del proyecto base, con formularios Angular y un diálogo nativo que gestiona foco, teclado y Escape. Pago, anulación y eliminación tienen un modal de confirmación propio con los datos del bono antes de llamar a la API. Las notificaciones se muestran una sola vez: dentro del diálogo activo o como avisos flotantes que no desplazan el contenido. Al cerrar un diálogo se limpian sus mensajes. El listado aprovecha el ancho del escritorio y, en pantallas estrechas, reorganiza las filas con etiquetas para mantener todos los campos visibles.

## Pago y distribución por proyecto

Solo un bono Registrado se puede pagar. `POST /api/v1/Bonus/{bonusId}/pay` devuelve el estado Pagado y las líneas distribuidas. No realiza transferencias bancarias: registra el pago previsto en la prueba.

- El reparto usa el bruto guardado del bono y las horas de `EmployeeProjectHours`.
- Los proyectos se ordenan por `EmployeeProjectHoursId`. Cada línea anterior a la última se redondea a dos decimales hacia arriba en las mitades; el último recibe exactamente el saldo. Un único proyecto recibe todo.
- Se conserva en `BonusProjectAllocation` el proyecto de origen, su nombre, horas y bruto asignado para mantener el histórico.
- El detalle, estado Pagado y auditoría se guardan con una transacción explícita de EF Core. Si una inserción o la comprobación de `rowversion` falla, se revierte toda la operación.
- Se rechaza el pago si no hay proyectos, existen horas negativas o el total de horas es cero. Si, con importes muy pequeños y muchos proyectos, el redondeo exigido produce un saldo final negativo, se devuelve `PROJECT_ROUNDING` y el bono sigue Registrado; no se guarda una distribución negativa.
- Una vez Pagado, el bono no puede pagarse otra vez, anularse ni eliminarse.

## Validación

Desde la raíz:

```bash
dotnet build backend/BonusTest.slnx
dotnet test backend/BonusTest.slnx
npm --prefix frontend run build
```

Las pruebas xUnit cubren cálculo fijo/porcentual, redondeo de mitades, límites, estados, empleados cesados, motivos inactivos, duplicados, auditoría y el límite mensual en Lima. Se comprobó además la API con SQL Server en una base temporal separada: registro, filtros, paginación, cancelación, eliminación lógica, reutilización del periodo y dos registros simultáneos. La base temporal se eliminó al terminar.

La revisión de interfaz incluye búsqueda del empleado, vista previa, confirmaciones y pago desde la pantalla. No se añade infraestructura de pruebas automatizadas de Angular.

Para repetir las comprobaciones de integración contra SQL Server local (necesita Docker, el contenedor y Python 3):

```bash
dotnet build backend/BonusTest.slnx
read -s SQLCMDPASSWORD
export SQLCMDPASSWORD
python3 scripts/verify_api.py
unset SQLCMDPASSWORD
```

El script crea una base temporal con nombre único, arranca otra instancia de la API en un puerto libre y elimina únicamente esa base al terminar. No modifica los datos de `BonusTest`. Se puede indicar otro nombre de contenedor mediante `SQL_CONTAINER`. El resumen y alcance de la revisión están en `QA.md`.

## Alcance y pendientes

Se implementan los requisitos obligatorios y ambos puntos extra: pruebas unitarias y pago con distribución por proyecto. Se conservan los estados y tablas del proyecto base. No quedan funcionalidades del enunciado pendientes; la puntuación corresponde al evaluador.

No se agregan funcionalidades de producto fuera del enunciado. El historial de commits está disponible localmente; no se ha configurado ni publicado un repositorio remoto.
