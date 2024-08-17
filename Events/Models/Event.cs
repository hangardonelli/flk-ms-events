namespace Events.Models
{
    namespace ClickHouseMinimalApi.Models
    {
        public record Event
        {
            public uint Id { get; set; }
            public uint Type { get; set; }
            public float Lat { get; set; }
            public float Lon { get; set; }
            public string? DescriptionEs { get; set; }
            public string? DescriptionEn { get; set; }
            public DateTime Date { get; set; }
            public List<string>? MediaUrl { get; set; }
            public List<string>? MediaType { get; set; }
            public List<string>? MediaDescriptionEs { get; set; }
            public List<string>? MediaDescriptionEn { get; set; }
        }
    }

}