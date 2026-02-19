using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace FCG.Games.API.Messaging
{
    public sealed class RabbitPublisher
    {
        private readonly string _uri;
        private readonly string _queue;

        public RabbitPublisher(IConfiguration cfg)
        {
            _uri = cfg["RabbitMq:Uri"]!;
            _queue = cfg["RabbitMq:Queue"]!;
        }

        public void PublishOrderCreated(object payload)
        {
            var factory = new ConnectionFactory
            {
                Uri = new Uri(_uri),
                Ssl = new SslOption
                {
                    Enabled = true,
                    ServerName = "fly.rmq.cloudamqp.com"
                },
                AutomaticRecoveryEnabled = true
            };

            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            channel.QueueDeclare(queue: _queue, durable: true, exclusive: false, autoDelete: false);

            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));

            var props = channel.CreateBasicProperties();
            props.Persistent = true;

            channel.BasicPublish(exchange: "", routingKey: _queue, basicProperties: props, body: body);
        }
    }
}
