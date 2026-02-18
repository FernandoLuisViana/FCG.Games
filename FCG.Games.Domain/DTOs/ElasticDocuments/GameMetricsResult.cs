namespace FCG.Games.Domain.DTOs.ElasticDocuments;

public class GameMetricsResult
{
    public Dictionary<string, long> GamesByGenre { get; set; } = [];
    public Dictionary<string, long> GamesByPlatform { get; set; } = [];
    public decimal AvgPrice { get; set; }
    public decimal MinPrice { get; set; }
    public decimal MaxPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal CountPrice { get; set; }
}
