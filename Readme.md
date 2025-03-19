# Официальные примеры были скопированы из официальной документации
Источники:  
https://www.rabbitmq.com/tutorials/tutorial-seven-dotnet    
https://github.com/rabbitmq/rabbitmq-tutorials/blob/main/dotnet/PublisherConfirms/PublisherConfirms.cs

# Проекты разделены по типам очередей
> Для анализа потребления памяти в Rider и Visual Studio тесты оформлены в виде консольных приложений  
> Для сравнения потребления памяти необходимо каждый publish-метод запускать в отдельном процессе  
> Уровень параллелизма и количество сообщений в каждом примере настроены согласно конфигурации тестовой машины (16 виртуальных ядер и 32Гб ОЗУ)
## RabbitMqStreamLoadTests
Тесты для проверки очередей типа Stream.   
Тесты упорядочены по скорости публикации
Проверена публикация в очередь из Channel и стримминг из продюсеров разного типа.
## PublisherConfirmsOfficial
2 официальных теста из документации. Метод HandlePublishConfirmsAsynchronously на основе коллбэков потребляет в разы больше памяти
И ActionClassicQueue - мой пример для сравнения производительности и потребления памяти с использованием Action-блоков для распараллеливания задач
Тесты упорядочены по потреблению памяти

# Конфигурация RabbitMq
В файле compose.yaml сконфигурирован RabbitMq с плагином для стримминга и дополнительным портом 5552 для стримминга

# Результаты тестов
## RabbitMqStreamLoadTests
```shell
2025-03-18T12:08:58.3945088+02:00 [INFO] Start ActionStreamQueueChannelBasicProducer. Publishing 1 000 000 messages to StreamQueue and handling confirms by Action blocks
2025-03-18T12:09:09.6234209+02:00 [INFO] published to StreamQueue 1 000 000 messages in action blocks. Total time: 11 167 ms
2025-03-18T12:09:09.6235592+02:00 [INFO] Speed 89549,42 msg/s
2025-03-18T12:09:12.5533132+02:00 [INFO] Start ActionStreamProducerCallbacks. Publishing 3 000 000 messages to Stream and handling confirms by producer callbacks
2025-03-18T12:09:23.5856072+02:00 [INFO] produced 3 000 000 messages to Stream in action blocks. Total time: 10 798 ms
2025-03-18T12:09:23.5857034+02:00 [INFO] Speed 277812,26 msg/s
2025-03-18T12:09:23.5871520+02:00 [INFO] Start ActionStreamRawProducer. Publishing 3 000 000 messages to Stream by Action blocks
2025-03-18T12:09:32.3286194+02:00 [INFO] produced 3 000 000 messages to Stream in action blocks. Total time: 8 624 ms
2025-03-18T12:09:32.3287182+02:00 [INFO] Speed 347845,19 msg/s
```

## PublisherConfirmsOfficial
```shell
2025-03-18T12:01:38.1082598+02:00 [INFO] publishing 2 000 000 messages and handling confirms in batches
2025-03-18T12:02:10.1404934+02:00 [INFO] published 2 000 000 messages in batch in 31 963 ms
Speed 62571,66 msg/s
2025-03-18T12:02:10.1458426+02:00 [INFO] publishing 2 000 000 messages and handling confirms in Action blocks
2025-03-18T12:02:38.9742340+02:00 [INFO] published 2 000 000 messages in action blocks 28 775 ms
Speed 69503,75 msg/s
2025-03-18T12:02:38.9778822+02:00 [INFO] publishing 2 000 000 messages and handling confirms asynchronously
2025-03-18T12:03:05.5019684+02:00 [INFO] published 2 000 000 messages and handled confirm asynchronously 26 467 ms
Speed 75565,32 msg/s
```