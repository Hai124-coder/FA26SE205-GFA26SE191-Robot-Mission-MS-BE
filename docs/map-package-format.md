# Map package format

`POST /api/v1/floors/{floorCode}/map-versions` accepts a multipart form with one field named `file`. The file must be JSON and no larger than 2 MB.

```json
{
  "widthM": 40,
  "heightM": 24,
  "backgroundImagePath": "/assets/maps/floor-01.png",
  "nodes": [
    { "code": "WP-01", "nodeType": "WAYPOINT", "x": 2.5, "y": 3.0 },
    { "code": "STOP-01", "nodeType": "STOP", "x": 12.0, "y": 3.0 },
    { "code": "CHG-01", "nodeType": "CHARGER", "x": 30.0, "y": 18.0 }
  ],
  "edges": [
    { "from": "WP-01", "to": "STOP-01", "distanceMeters": 9.5, "isBidirectional": true },
    { "from": "STOP-01", "to": "CHG-01", "distanceMeters": 23.4, "isBidirectional": false }
  ]
}
```

`nodeType` is one of `WAYPOINT`, `STOP`, or `CHARGER`. Node coordinates must be within the declared map dimensions. Every edge must reference existing, different node codes. The import creates a draft version. Its response includes `affectedLocationCodes` when the package omits nodes used by active locations; that draft cannot be published until the nodes are restored or the locations are disabled.
