# Verificación de BonusTest

Revisión del 29 de octubre de 2026. Alcance: requisitos obligatorios, ambos puntos extra y ajustes solicitados de ancho, densidad móvil, confirmaciones y notificaciones.

## Resultados

| Comprobación | Resultado |
| --- | --- |
| Compilación .NET 10 | Sin errores ni advertencias |
| Compilación de producción Angular | Sin errores ni advertencias, dentro de los presupuestos configurados |
| xUnit | 43 pruebas aprobadas |
| API y SQL Server real | 63 comprobaciones aprobadas |
| Anchos revisados | 390, 870, 1280, 1440 y 1920 px |
| Desbordamiento horizontal con el registro revisado | Ninguno; los 11 campos permanecen dentro de la pantalla |

## Cálculos y reglas

Las pruebas cubren monto fijo, porcentaje, límites, mitades hacia arriba, retención y neto. Incluyen el ejemplo de bruto 500, retención 40 y neto 460, empleados cesados, motivos inactivos, periodos inválidos y futuros, límite del mes en Lima, UserCode y conservación de la auditoría.

Se verificaron duplicados, filtrado combinado, paginación desde SQL, exclusión de eliminados y reutilización del periodo después de anular o eliminar. Dos registros simultáneos producen un único bono activo.

## Pago y consistencia

- Reparto proporcional, un único proyecto y ajuste del último proyecto.
- Ejemplo de dos proyectos: 500 se reparte en 375 y 125 para 120 y 40 horas.
- Ejemplo de tres proyectos iguales: 1 se reparte en 0.33, 0.33 y 0.34.
- Dos pagos simultáneos: uno se completa y el otro devuelve un conflicto de estado o concurrencia; el detalle se guarda una sola vez.
- No se permite pagar otra vez, anular ni eliminar un bono Pagado. Tampoco se pagan bonos anulados o eliminados.
- Un trigger temporal provoca un error de SQL al insertar el detalle. Se comprobó que tanto el estado como todas las líneas se revierten; al retirar el trigger, el pago se completa correctamente.
- Sin horas válidas, el bono conserva su estado y no se inserta detalle.

## Interfaz

Se verificaron los modales de pago, anulación y eliminación, el foco inicial en Cancelar, la salida con Escape y la devolución del foco al botón de origen. Cancelar no ejecuta la operación. Se probaron desde la pantalla el pago y la eliminación de registros creados exclusivamente para QA.

El pago de prueba de bruto 1 produjo tres líneas de 0.30, 0.50 y 0.20, estado Pagado y acciones deshabilitadas; se contrastó con SQL Server. Los registros creados para esta comprobación se retiraron al terminar, conservando los registros previos del usuario.

Las notificaciones se muestran una sola vez: dentro del modal activo o flotando en la página. Para el aviso flotante se midió la posición vertical de la tarjeta antes y después: 72.5 px en ambos casos. La notificación usa posición fija y puede cerrarse.

La tabla aprovecha el ancho disponible en escritorio. Por debajo de 1200 px, conserva los campos en filas con etiquetas. En móvil, los filtros usan dos columnas y los tres importes comparten una fila. Se revisaron capturas y dimensiones del contenido en los cinco anchos indicados.

## Repetir las comprobaciones

```bash
dotnet build backend/BonusTest.slnx
dotnet test backend/BonusTest.slnx
npm --prefix frontend run build
```

Para integración, seguir el comando con `SQLCMDPASSWORD` de README y ejecutar `python3 scripts/verify_api.py`. El script crea una base temporal con nombre único e inicia una API en otro puerto; al finalizar elimina únicamente esa base.

## Límites de esta revisión

La interfaz se comprobó manualmente en el navegador disponible, con datos semilla y registros temporales; no es una certificación de todos los navegadores ni una auditoría completa de accesibilidad. Los estados de carga, confirmación, error, éxito y acciones deshabilitadas están implementados. No se añade un sistema de autenticación ni transferencias bancarias.

El caso extremo de muchos proyectos y un bruto muy pequeño puede generar un ajuste final negativo al seguir el redondeo especificado. Se rechaza sin persistir cambios y se documenta en README; no se altera silenciosamente el algoritmo de reparto.

## Ajuste posterior: iconos y tooltips

Las acciones usan iconos SVG con fondos pastel (verde, ámbar y rosado), nombres accesibles y tooltips al pasar el puntero o recibir foco. Las acciones no disponibles conservan `aria-disabled` y la protección en el manejador; su explicación sigue accesible con teclado. Se comprobó el tooltip con Tab, su cierre con Escape y la apertura/cancelación del modal desde el icono de pago. Los tres iconos comparten la misma fila y la pantalla no desborda horizontalmente en la vista revisada. La compilación de producción pasó sin advertencias. Se retiró el registro temporal de esta comprobación.
