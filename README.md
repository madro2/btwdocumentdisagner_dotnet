# BTW Document Designer API

API .NET 10 para almacenar contratos de diseño y generar PDF dinámicos desde
payload XML o JSON.

## Contrato dinámico

El motor soporta el contrato 3.0 definido en
`../UtilFiles/design-contract-btw-v3.0.json`:

- tamaño físico y fondo de página;
- XML/JSON, runtime, lookups y computed fields;
- filtros de fecha, número, moneda, porcentaje y mayúsculas;
- texto, link, imagen, QR, línea, rectángulo, barcode, contenedor, tabla,
  salto y número de página;
- tablas fijas, de registro y de colección;
- anchors, encabezado/pie repetido y paginación;
- validación estructural antes de persistir o generar.

## Base de datos

Configure PostgreSQL en `BtwDocumentDesigner.Api/appsettings.json`. Para una
base existente aplique:

```bash
psql "$CONNECTION_STRING" \
  -f Database/Migrations/20260718_add_image_resource_key.sql
```

La clave opcional `ResourceKey` permite que el contrato use IDs estables como
`company-logo` sin acoplarse al GUID de la fila.

## Ejecutar y verificar

```bash
dotnet run --project BtwDocumentDesigner.Api
dotnet test BtwDocumentDesigner.Tests -m:1
```

Swagger está disponible en desarrollo. El endpoint recomendado es:

```text
POST /DocumentDesignerApi/pdf/generate-v3
Content-Type: application/json
```

Envelope:

```json
{
  "designName": "Factura BTW",
  "version": 1,
  "payloadType": "application/xml",
  "payload": "<NewDataSet>...</NewDataSet>",
  "runtime": {
    "Cufe": "valor-cufe",
    "QrImage": "base64-jpeg-o-png",
    "DianValidationDateTime": "2026-07-16T08:31:24-05:00",
    "CurrentYear": 2026
  }
}
```
