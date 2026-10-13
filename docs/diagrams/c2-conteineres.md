# C2: Contêineres

Processos e armazenamentos que compõem o CaixaDiario. Público: TI. Convenções em [README](README.md). Decisões em [ADR-0001](../adr/ADR-0001-estilo-arquitetural.md) e [ADR-0002](../adr/ADR-0002-plataforma-armazenamento-e-integracao.md).

```mermaid
flowchart LR
    comerciante["Comerciante<br/>[Pessoa]"]:::externo

    subgraph critico["Zona crítica: Controle de Lançamentos"]
        lapi["API Lancamentos<br/>[ASP.NET Core 10, :8080]<br/>Valida, registra com idempotência,<br/>lista o dia"]:::impl
        desp["Despachante da outbox<br/>[BackgroundService, no processo<br/>da API Lancamentos]<br/>Publica eventos pendentes com retentativa"]:::impl
        ldb[("Banco de lançamentos<br/>[PostgreSQL 17]<br/>lancamentos append-only<br/>e outbox, mesma transação")]:::impl
    end

    broker[["Broker<br/>[RabbitMQ]<br/>Fila durável<br/>lancamento-registrado"]]:::impl

    subgraph sacrificavel["Zona sacrificável: Consolidado Diário"]
        proj["Projetor<br/>[BackgroundService, no processo<br/>da API Consolidado]<br/>Aplica cada evento uma vez"]:::impl
        capi["API Consolidado<br/>[ASP.NET Core 10, :8081]<br/>Saldo do dia, cache curto,<br/>limitador com 429"]:::impl
        cdb[("Banco do consolidado<br/>[PostgreSQL 17]<br/>saldo_diario e<br/>lancamentos_aplicados")]:::impl
    end

    painel["Painel de observabilidade<br/>[Aspire Dashboard, :18888]<br/>Traços, métricas e logs por OTLP"]:::impl

    comerciante -->|"registra<br/>HTTP/JSON"| lapi
    comerciante -->|"consulta saldo<br/>HTTP/JSON"| capi
    lapi -->|"INSERT lançamento + evento<br/>uma transação"| ldb
    desp -->|"lê e marca a outbox"| ldb
    desp -->|"publica<br/>AMQP, confirmação"| broker
    broker -->|"entrega ao menos uma vez<br/>AMQP, ack manual"| proj
    proj -->|"aplica idempotente"| cdb
    capi -->|"SELECT por chave"| cdb
    lapi -.->|"OTLP, assíncrono,<br/>descarta se cair"| painel
    capi -.->|"OTLP"| painel

    classDef externo fill:#999999,stroke:#6b6b6b,color:#fff
    classDef impl fill:#1168bd,stroke:#0b4884,color:#fff
    classDef espec fill:#ffffff,stroke:#6b6b6b,color:#333,stroke-dasharray:5 5
```

## Por que Lancamentos sobrevive à queda do Consolidado

Siga as setas: **nenhuma sai da zona sacrificável em direção à zona crítica.** A API Lancamentos só fala com o próprio banco; o despachante só fala com o próprio banco e com o broker. Nada na zona crítica chama, lê ou espera o Consolidado ou o banco dele.

| Falha | Efeito na zona crítica | Efeito na zona sacrificável |
|---|---|---|
| API Consolidado fora | Nenhum | Consulta indisponível; a fila retém os eventos |
| Banco do consolidado fora | Nenhum | Consulta e projeção param; a fila retém |
| Consolidado sobrecarregado | Nenhum | Excedente recebe `429`, dentro do orçamento de 5% |
| Broker fora | Registro segue com `201`; a outbox acumula no banco de lançamentos | Saldo fica defasado e sinaliza `defasado: true` |
| Banco de lançamentos fora | Registro indisponível | Consulta segue servindo o último saldo projetado |

A prontidão (`/health/ready`) da API Lancamentos verifica **só** o banco de lançamentos. Nunca o broker, nunca o Consolidado. A seta para o painel é telemetria: exportação assíncrona, que descarta se o painel cair; com o painel parado, o `POST` responde `201`.

## Estado de cada elemento

| Elemento | Estado | Evidência |
|---|---|---|
| API Lancamentos | Implementado | `src/CaixaDiario.Lancamentos.Api`; `RegistroDeLancamentoTests` |
| Despachante da outbox | Implementado | `DespachanteOutbox.cs`; `Rnf01IsolamentoTests` (outbox acumula sem broker e esvazia quando ele volta) |
| Banco de lançamentos | Implementado | `db/lancamentos/0001_esquema.sql`; `ImutabilidadeTests` (UPDATE e DELETE recusados pelo banco) |
| Broker | Implementado | Serviço `broker` do `docker-compose.yml`, RabbitMQ 4.1.8 |
| Projetor | Implementado | `ProjetorRabbitMq.cs`; `ProjetorTests`, `FluxoCompletoTests` |
| API Consolidado | Implementado | `src/CaixaDiario.Consolidado.Api`; teste de carga a 50 req/s com 0% de falha |
| Banco do consolidado | Implementado | `db/consolidado/0001_esquema.sql` |
| Painel de observabilidade | Implementado | Serviço `painel` do compose; ADR-0003; traço do POST à projeção conferido pela API do painel. Setas tracejadas: telemetria, fora do caminho de negócio, não implementação pendente |
| Credencial de broker por serviço, TLS | Especificado | Ambiente local usa um usuário só no broker ([Arquitetura Alvo](../04-arquitetura-alvo.md) §4.8) |

## Decisões que o diagrama torna visíveis

- **Dependência unidirecional:** Lancamentos → broker → Consolidado. É ela que entrega o RNF-01 (ADR-0001).
- **Um banco por serviço:** derrubar o Consolidado e o banco dele não toca o banco de lançamentos (ADR-0002).
- **Lançamento e evento na mesma transação:** a outbox impede lançamento sem evento e evento sem lançamento (ADR-0002, tema 4).
- **Despachante e projetor no processo da própria API:** menos executáveis a operar; o custo é que reiniciar a API reinicia o despacho ou a projeção, o que a outbox e a fila durável tornam inofensivo.
