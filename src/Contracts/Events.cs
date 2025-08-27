namespace Contracts;
public record StartRunCommand(Guid RunId, string DatasetPath, RunConfig Config);
public record SalesPatternsIdentified(Guid RunId, RunConfig Config, List<SkuDemand> Demand, List<SalesRecord> Records);
public record SkuGroupsCreated(Guid RunId, RunConfig Config, List<SkuGroup> Groups, List<SkuDemand> Demand, List<SalesRecord> Records);
public record ShelfLocationsAssigned(Guid RunId, RunConfig Config, List<ShelfLocation> Locations, List<SkuDemand> Demand, List<SalesRecord> Records);
public record RackLayoutCalculated(Guid RunId, RunConfig Config, List<Rack> Racks, List<ShelfLocation> Locations, List<SkuDemand> Demand, List<SalesRecord> Records);
public record BatchesCreated(Guid RunId, RunConfig Config, List<Batch> Batches, List<Rack> Racks, List<ShelfLocation> Locations, List<SkuDemand> Demand, List<SalesRecord> Records);
public record StationsAllocated(Guid RunId, RunConfig Config, List<StationAssignment> Assignments, List<Batch> Batches, List<Rack> Racks, List<ShelfLocation> Locations, List<SkuDemand> Demand, List<SalesRecord> Records);
public record HitRateCalculated(Guid RunId, RunConfig Config, HitRateResult Result);