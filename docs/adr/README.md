# Registros de Decisão Arquitetural (ADR)

Decisões arquiteturais do CaixaDiario, em formato [MADR](https://adr.github.io/madr/), em português. Cada decisão é um arquivo. Decisão superada não é editada: cria-se um novo ADR que a substitui, e o anterior passa a `Superado por ADR-NNNN`.

## Como ler

Comece pelo [ADR-0001](ADR-0001-estilo-arquitetural.md); o ADR-0002 depende dele.

| ADR | Decisão | Status | Dirigido por |
|---|---|---|---|
| [0001](ADR-0001-estilo-arquitetural.md) | Dois serviços com integração assíncrona, justificados por isolamento de falha, não por volume | Aceito | RNF-01, RNF-02, RE-01 |
| [0002](ADR-0002-plataforma-armazenamento-e-integracao.md) | .NET 10 LTS, PostgreSQL 17 por serviço, Dapper, outbox com RabbitMQ, Testcontainers, NetArchTest, k6 | Aceito | RNF-01, RNF-06, RNF-07, RE-04 |
| [0003](ADR-0003-observabilidade.md) | OpenTelemetry com OTLP para o Aspire Dashboard; traço propagado pela outbox e pelo cabeçalho da mensagem | Aceito | RNF-01, RNF-03, RNF-05 |
| [0004](ADR-0004-seguranca-de-integracao.md) | OAuth2 client credentials com JWT e escopo por operação; usuário de broker por serviço com privilégio mínimo; TLS no broker | Aceito | RNF-01, RNF-02, QA-04 |
| [0005](ADR-0005-migracao-versionada.md) | Migração versionada com DbUp, um migrador por serviço, executado antes da API | Aceito; supera um trecho do ADR-0002 | RNF-06, gatilho do ADR-0002 |

## Regras deste diretório

1. **Critérios antes das opções.** Os critérios são fixados e datados antes da comparação.
2. **Consequência negativa obrigatória.** ADR sem consequência negativa não registra um trade-off.
3. **Alternativa rejeitada com gatilho.** Toda opção descartada registra a condição que a tornaria a melhor escolha.
4. **Toda decisão rastreia a um requisito** de [03-requisitos.md](../03-requisitos.md).

Template em [template.md](template.md).
