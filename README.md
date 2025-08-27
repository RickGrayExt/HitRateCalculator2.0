# Hit-Rate Microservices (with UI)

## Run
```bash
docker compose up --build
```
Open UI at http://localhost:3000  
UI proxies API calls to the Gateway at http://gateway:8080 (container) / http://localhost:8000 (host).

### Starting a run
- In the UI, keep Data URL as `http://ui/sample.csv` (reachable from the services).
- Set parameters and press **Start**.
- The UI polls for results (GET /api/runs/{id}).

### Services
- Orchestrator (exposes /runs, stores results)
- SalesDataAnalysis (reads CSV, computes demand + seasonality)
- SkuGrouping (category + velocity tiers)
- ShelfLocation (assigns ranks; racks = ceil(unique SKUs / SKUsPerRack))
- RackCalculation (creates rack objects)
- OrderBatching (toy batches from rack count; honors PTL/PTO flag & max orders)
- PickingStationAllocation (round-robin to stations)
- EfficiencyAnalysis (computes hit-rate, rack presentations)

### RabbitMQ UI
http://localhost:15672 (guest/guest)
