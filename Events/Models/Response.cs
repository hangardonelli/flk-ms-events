namespace Events.Models
{
    public record Response<T>
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public string? Message { get; init; }
        public T? Data { get; init; }

        public Response() { }
        public Response(string? message, T? data)
        {
            Message = message;
            Data = data;
        }
        public Response(string? message)
        {
            Message = message;
        }
        public Response(T? data)
        {
            Data = data;
        }
    }

}
