namespace Contracts;

public record StartRunCommand(Guid RunId, string DataUrl, RunParams Params);
public record SalesPatternsIdentified(Guid RunId, List<SkuDemand> Demand);
public record SkuGroupsCreated(Guid RunId, List<SkuGroup> Groups);
public record ShelfLocationsAssigned(Guid RunId, List<ShelfLocation> Locations, int TotalRacks);
public record RackLayoutCalculated(Guid RunId, List<Rack> Racks);
public record BatchesCreated(Guid RunId, List<Batch> Batches, string Mode);
public record StationsAllocated(Guid RunId, List<StationAssignment> Assignments);
public record HitRateCalculated(Guid RunId, HitRateResult Result);

public record RunParams(
    bool UsePickToLine,
    int StationCapacity,
    int NumberOfStations,
    int WaveSize,
    bool EnableSeasonality,
    int SkusPerRack,
    int MaxOrdersPerBatch,
    int MaxStationsOpen
);
