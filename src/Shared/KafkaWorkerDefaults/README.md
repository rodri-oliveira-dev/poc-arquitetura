# KafkaWorkerDefaults

Defaults tecnicos compartilhados para consumers Kafka dos workers.

Este pacote nao contem contratos de dominio, nomes de topicos, mappers ou
processors de bounded contexts. Ele concentra apenas montagem de configuracao,
seguranca, commit condicional e fechamento tolerante a erro para adapters Kafka.
