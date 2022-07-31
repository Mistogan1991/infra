﻿using Confluent.Kafka;
using Domain;
using Infra.Eevents;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Infra.Events.Kafka
{
    public class KafkaEventBus : IEventBus
    {
        private readonly IProducer<Null, string> _producer;
        private readonly ILogger<KafkaEventBus> _logger;
        private static JsonSerializerSettings _jsonSerializerSettings = new JsonSerializerSettings 
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        };

        public KafkaEventBus(KafkaProducerConfig config) : this(Options.Create(config), null)
        {
        }

        public KafkaEventBus(IOptions<KafkaProducerConfig> config, ILogger<KafkaEventBus> logger)
        {
            Guard.NotNull(config.Value, nameof(config));
            var producerConfig = config.Value;

            _logger = logger;
            _producer = new ProducerBuilder<Null, string>(new ProducerConfig
            {
                BootstrapServers = producerConfig.BootstrapServers,
                MessageMaxBytes = producerConfig.MaxMessageBytes
            }).Build();
        }

        public Task Execute(string topic, Event @event, Dictionary<string, string> headers = null, CancellationToken cancellationToken = default)
        {
            Guard.NotNull(topic, nameof(topic));
            Guard.NotNull(@event, nameof(@event));

            var message = new Message<Null, string>
            {
                Value = JsonConvert.SerializeObject(@event, _jsonSerializerSettings)
            };
            
            AddHeaders(headers, message);

            _logger?.LogInformation($"Pushing to ({topic}): {message.Value}");

            return _producer.ProduceAsync(topic, message, cancellationToken);
        }

        public Task Execute<TEvent>(TEvent @event, Dictionary<string, string> headers = null, CancellationToken cancellationToken = default) where TEvent : Event
        {
            Guard.NotNull(@event, nameof(@event));

            var eventData = JsonConvert.SerializeObject(@event, _jsonSerializerSettings);
            var message = new Message<Null, string>
            {
                Value = eventData
            };

            AddHeaders(headers, message);


            var topicName = GetTopicName(@event);
            _logger?.LogInformation($"Pushing to ({topicName}): {message.Value}");

            return _producer.ProduceAsync(topicName, message, cancellationToken);
        }

        private string GetTopicName<TEvent>(TEvent @event) where TEvent : Event
        {
            var topicInfo = @event.GetType()
                .GetCustomAttribute<TopicAttribute>();

            if (topicInfo != null)
                return topicInfo.Name;

            return @event.EventName;
        }

        private static void AddHeaders(Dictionary<string, string> headers, Message<Null, string> message)
        {
            if (headers != null)
            {
                var headerValues = new Headers();

                foreach (var item in headers)
                {
                    headerValues.Add(item.Key, Encoding.UTF8.GetBytes(item.Value));
                }

                message.Headers = headerValues;
            }
        }
    }
}
