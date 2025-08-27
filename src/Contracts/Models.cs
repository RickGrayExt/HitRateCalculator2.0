namespace Contracts;

public record SalesRecord(DateOnly OrderDate, TimeOnly Time, string CustomerId, string ProductCategory,
                          string Product, decimal Sales, int Qty, string Priority);

public record SkuDemand(string SkuId, string Product, string Category, int TotalUnits, int OrderCount, double Velocity, bool Seasonal);
public record SkuGroup(string GroupId, List<string> SkuIds);
public record ShelfLocation(string SkuId, string RackId, string SlotId, int Rank);

public record Rack(string RackId, int LevelCount, int SlotPerLevel, double MaxWeightKg);

public record OrderLine(string OrderId, string SkuId, int Qty);
public record Batch(string BatchId, string Mode, List<OrderLine> Lines);

public record StationAssignment(string StationId, List<string> BatchIds);

public record HitRateResult(string Mode, double HitRate, int TotalItemsPicked, int TotalRackPresentations,
                            Dictionary<string, double> ByRack, int TotalRacks);
