# Aerial view (live fleet map)

Sidebar: **Vehicles → Aerial view** (`/aerial`). A live map of every active vehicle (Inactive vehicles are not tracked). Clicking a vehicle opens a side panel with:
- current status: Moving / Idle (engine on) / Parked / In workshop
- speed against the category speed limit, and whether the engine is on
- distance, fuel (or energy for EVs) and idle time today, plus the odometer
- address and coordinates, with a live "updated x s ago"
- route: origin → destination, progress, remaining km and ETA (or the last trip when parked)
- the current driver, with a call button
- maintenance due per service item (oil, tyres, brake pads…)
- **View details** opens the vehicle profile; **Schedule maintenance** opens a prefilled maintenance request.

## Architecture
```
AerialViewPage (polls every 4 s) ──► GET /api/tracking/vehicles[/{id}]
                                        TrackingController
                                        IFleetTrackingService  (Axpense.Service/Tracking)
                                          ├─ ITelematicsProvider  (Infrastructure/Telematics)
                                          │    └─ SimulatedTelematicsProvider  ← swap for a real GPS adapter
                                          ├─ FleetQueries.CurrentDriversAsync  (assignments)
                                          └─ ServiceItemCalculator             (maintenance due)
```
- **Telemetry today is simulated.** It is deterministic per vehicle and follows real Greater Cairo corridors, and the page shows "Live · simulated telemetry". To go live, implement `ITelematicsProvider` for the vendor (Teltonika, Traccar, Samsara…) and register it in `InfrastructureDependencies` instead of the simulator. No other change is needed.
- **Business overrides telemetry** where it knows better: a vehicle with status `Maintenance` is shown "In workshop" (speed 0, no trip).
- **Speed limit per category:** Truck 80, Bus 70, Sedan 90, Machinery 20, others 60 km/h (`VehicleConstants.SpeedLimitFor`).

## Map
Leaflet (`leaflet` npm package) with CARTO Voyager tiles (OpenStreetMap data), plus Esri World Imagery for Satellite.
Override the tile servers with `VITE_MAP_TILE_URL` / `VITE_MAP_SATELLITE_URL`. The free CARTO/Esri endpoints have fair-use limits; use a paid key (MapTiler, Mapbox, Esri) for production traffic.

## Vehicle changes
- `Vehicle.Category` (Truck, Van, Bus, Sedan, Pickup, Machinery) and `Vehicle.FuelType` (Diesel, Petrol, Electric, Hybrid, CNG), selectable in the Add Vehicle form.
- `VehicleServiceItem`: name, interval km and/or days, last-service odometer and date.
  - Due from 85% used, overdue at 100%. The limit hit first leads ("whichever comes first").
  - "Add standard items" adds Engine oil & filter, Tyres, Brake pads, Air filter, Battery and Coolant, skipping oil and air filter for EVs.
- Vehicle profile, first version (`/vehicles/:id`): identity, current driver and service items management.
- Fixed tenant checks on `GET/PUT/DELETE /api/vehicles/{id}` (they previously didn't verify the organization).

## API
| Method | Route |
|---|---|
| GET | `/api/tracking/vehicles` — positions, motion, speed, driver + counts |
| GET | `/api/tracking/vehicles/{id}` — full panel data |
| GET | `/api/vehicles/{id}/profile` |
| POST | `/api/vehicles/{id}/service-items`, `/api/vehicles/{id}/service-items/standard` |
| PUT / DELETE | `/api/vehicles/{id}/service-items/{itemId}` |

## Database
New table `VehicleServiceItems`; new columns `Vehicles.Category`, `Vehicles.FuelType`. The API still uses `EnsureCreated`, so the dev database must be recreated.
