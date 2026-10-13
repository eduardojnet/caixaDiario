# C3: Componentes

Componentes dentro de cada API, desenhados a partir do código em `src/`, não do desejo. Público: engenharia. Convenções em [README](README.md). Cada caixa nomeia a classe que a implementa.

## API Lancamentos

```mermaid
flowchart LR
    cliente["Cliente HTTP"]:::externo
    ldb[("Banco de lançamentos<br/>[PostgreSQL 17]")]:::impl
    broker[["Broker<br/>[RabbitMQ]"]]:::impl

    subgraph api["API Lancamentos [ASP.NET Core 10]"]
        ep["EndpointsDeLancamentos<br/>Rotas POST e GET; exige Idempotency-Key;<br/>impressão SHA-256 do conteúdo"]:::impl
        dom["Lancamento, TipoLancamento<br/>[CaixaDiario.Lancamentos.Dominio]<br/>Validação e regra do estorno, sem I/O"]:::impl
        repo["RepositorioLancamentos<br/>[Dapper]<br/>INSERT do lançamento e da outbox<br/>numa transação; repetição pela chave única"]:::impl
        desp["DespachanteOutbox<br/>[BackgroundService]<br/>Lote com SKIP LOCKED, espera crescente"]:::impl
        pub["PublicadorRabbitMq<br/>Conexão sob demanda; confirmação<br/>e mandatory"]:::impl
        saude["VerificacaoBanco<br/>[IHealthCheck]<br/>/health/ready: só o próprio banco"]:::impl
    end

    cliente -->|"HTTP/JSON"| ep
    ep -->|"Criar"| dom
    ep -->|"RegistrarAsync, ListarDoDiaAsync"| repo
    repo -->|"SQL"| ldb
    desp -->|"lê e marca a outbox"| ldb
    desp -->|"PublicarAsync"| pub
    pub -->|"AMQP"| broker
    saude -->|"SELECT 1"| ldb

    classDef externo fill:#999999,stroke:#6b6b6b,color:#fff
    classDef impl fill:#1168bd,stroke:#0b4884,color:#fff
```

## API Consolidado

```mermaid
flowchart LR
    cliente["Cliente HTTP"]:::externo
    broker[["Broker<br/>[RabbitMQ]"]]:::impl
    cdb[("Banco do consolidado<br/>[PostgreSQL 17]")]:::impl

    subgraph api["API Consolidado [ASP.NET Core 10]"]
        lim["Limitador de taxa<br/>[RateLimiter, token bucket]<br/>Excedente: 429"]:::impl
        ep["Endpoint de consulta<br/>[Program.cs, MapGet]<br/>Cache em memória de 2 s"]:::impl
        proj["ProjetorRabbitMq<br/>[BackgroundService]<br/>Consome, aplica, ack após COMMIT;<br/>mede a fila a cada segundo"]:::impl
        evento["EventoLancamentoRegistradoV1<br/>Tipo próprio do consumidor;<br/>recusa versão ou dado inválido"]:::impl
        estado["EstadoDoProjetor<br/>Conectado e pendentes;<br/>decide 'defasado'"]:::impl
        repo["RepositorioConsolidado<br/>[Dapper]<br/>Aplicação idempotente pela chave<br/>do lançamento; consulta do dia"]:::impl
        saude["VerificacaoBanco<br/>[IHealthCheck]"]:::impl
    end

    cliente -->|"HTTP/JSON"| lim
    lim --> ep
    ep -->|"ConsultarAsync"| repo
    ep -->|"Defasado"| estado
    broker -->|"AMQP"| proj
    proj -->|"Ler"| evento
    proj -->|"AplicarAsync"| repo
    proj -->|"Conectou, AtualizarPendentes"| estado
    repo -->|"SQL"| cdb
    saude -->|"SELECT 1"| cdb

    classDef externo fill:#999999,stroke:#6b6b6b,color:#fff
    classDef impl fill:#1168bd,stroke:#0b4884,color:#fff
```

## Onde cada regra vive

| Regra | Componente | Garantia final |
|---|---|---|
| Valor positivo, duas casas, data não futura, estorno coerente (RF-03, RF-05) | `Lancamento` | Domínio; o banco repete `valor > 0` e o estorno único por constraint |
| Idempotência (RNF-07) | `RepositorioLancamentos` | Chave única `(caixa_id, chave_idempotencia)` no banco |
| Lançamento e evento juntos (RNF-04) | `RepositorioLancamentos` | Transação local |
| Imutabilidade (RNF-06) | Nenhum componente: privilégio do papel no banco | `GRANT SELECT, INSERT` |
| Registro não depende de Consolidado nem de broker (RNF-01) | `VerificacaoBanco`, `PublicadorRabbitMq` sob demanda, `DespachanteOutbox` fora do caminho do POST | Teste de arquitetura e teste de integração sem Consolidado |
| Evento sem fila não é descartado | `PublicadorRabbitMq` (`mandatory`) | Evento fica na outbox |
| Aplicação única por lançamento (RF-10) | `RepositorioConsolidado` | Chave primária de `lancamentos_aplicados` |
| Degradação em pico (RNF-02, RNF-03) | Limitador e cache | Teste de carga e rajada |
| Sinalização de atraso (RNF-05) | `EstadoDoProjetor` | Teste de unidade do estado |

## Estado

Todos os componentes: **implementado**, com testes (ver [ESTADO.md](../ESTADO.md) §4).
