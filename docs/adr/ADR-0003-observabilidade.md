# ADR-0003: Instrumentar com OpenTelemetry e propagar o traço pela outbox

- **Status:** Aceito em 2026-10-12 (Eduardo determinou OpenTelemetry; painel e propagação decididos neste ADR)
- **Data:** 2026-10-12
- **Decisor:** Eduardo J. G. do Carmo (Arquiteto de Soluções)
- **Consultados:** nenhum
- **Requisitos dirigentes:** RNF-01, RNF-03, RNF-05; Arquitetura Alvo §4.7

## Contexto e problema

Um lançamento atravessa dois processos e um intervalo de tempo arbitrário: o `POST` termina quando o lançamento e o evento são gravados; o despachante publica depois, às vezes horas depois, se o broker estava fora; o projetor aplica em outro serviço. Sem correlação, responder "por que este lançamento ainda não aparece no saldo" exige ler logs de três lugares e casar horários. Os RNF também pedem números que só existem com medição contínua: tamanho da outbox (sinal de broker fora, RNF-01), defasagem do projetor (RNF-05), taxa de respostas não 2xx no Consolidado (orçamento de 5%, RNF-03).

## Critérios de decisão

Fixados em 2026-10-12, antes das opções:

1. **Não cria dependência do registro com a telemetria.** Painel ou coletor fora do ar não pode afetar o `POST` (RNF-01).
2. **Um traço do `POST` à projeção**, atravessando a outbox e o broker.
3. **Padrão aberto**, sem amarrar a um fornecedor de monitoramento.
4. **Roda localmente** no mesmo `docker compose`, sem conta em serviço externo.

## Opções consideradas

1. OpenTelemetry com exportação OTLP para o Aspire Dashboard.
2. OpenTelemetry com OTLP para Jaeger (traços) e Prometheus com Grafana (métricas).
3. Agente de APM de um fornecedor comercial.

Para a propagação do traço:

- A. Gravar o `traceparent` da requisição na outbox, na mesma transação do lançamento, e enviá-lo no cabeçalho da mensagem.
- B. Iniciar um traço novo no despachante, ligado ao original só por atributo.

## Decisão

**A solução utiliza OpenTelemetry (.NET SDK 1.19), exportação OTLP só quando `OTEL_EXPORTER_OTLP_ENDPOINT` está definido, e o Aspire Dashboard como painel local. O contexto do traço é gravado na outbox (`outbox.traceparent`) e propagado no cabeçalho `traceparent` da mensagem AMQP (opção A).**

- **Critério 1:** o exportador OTLP é assíncrono, em lote, e descarta quando o destino falha; nenhum serviço tem `depends_on` do painel. Verificado: com o painel parado, o `POST` responde `201`.
- **Critério 2:** a opção A faz o despachante continuar o mesmo traço, mesmo horas depois; a B quebraria o traço exatamente no ponto em que a investigação mais importa (evento parado na outbox). Verificado por teste e no painel: `POST` → `outbox publicar` → `projetor aplicar`, com os comandos do PostgreSQL de cada lado, num traço só.
- **Critério 3:** OpenTelemetry e OTLP são padrões abertos; trocar o painel é trocar uma variável de ambiente.
- **Critério 4:** o Aspire Dashboard é um contêiner único que recebe traços, métricas e logs por OTLP.

**Instrumentos:**

| Serviço | Traço | Métricas próprias | Métricas da plataforma |
|---|---|---|---|
| Lancamentos | `POST`, comandos do PostgreSQL, `outbox publicar` | `caixadiario.lancamentos.registrados` (por tipo), `caixadiario.outbox.publicados`, `caixadiario.outbox.pendentes`, `caixadiario.outbox.idade_mais_antiga` | HTTP do ASP.NET Core |
| Consolidado | `projetor aplicar`, comandos do PostgreSQL, `GET` | `caixadiario.projetor.aplicados`, `caixadiario.projetor.reentregas`, `caixadiario.projetor.defasagem`, `caixadiario.projetor.fila_pendente`, `caixadiario.projetor.conectado` | HTTP por código de resposta (orçamento de 5%), limitador de taxa |

Logs saem por OTLP também, com identificador de traço; o console JSON continua.

## Consequências

**Positivas**

- Um lançamento é rastreável do registro à projeção por um identificador.
- O acúmulo da outbox e a defasagem do projetor viram números, com histórico no painel.

**Negativas**

- **Mudança de esquema:** a coluna `outbox.traceparent` é a primeira mudança de esquema depois da entrega. Sem migrador versionado, só é aplicada em volume novo (`docker compose down -v`). Isso cumpre o gatilho do cartão 39 (migração versionada).
- Mais um contêiner local e quatro pacotes por serviço.
- O painel guarda tudo em memória: reiniciá-lo apaga a telemetria. Adequado para o ambiente local, não para produção.
- O medidor da outbox faz uma consulta ao banco a cada 5 s.

**Neutras**

- Sem `OTEL_EXPORTER_OTLP_ENDPOINT`, a instrumentação continua ativa em processo, mas nada é exportado.

## Análise das opções rejeitadas

**Jaeger, Prometheus e Grafana.** Mais robusto para produção (retenção, alertas, painéis versionados), mas são três contêineres e configuração de painéis para o ambiente local. *Voltaria a ser a melhor escolha* para um ambiente compartilhado ou de produção, mantida a instrumentação: só o destino OTLP muda.

**APM comercial.** Menor esforço de painel, mas amarra a instrumentação ao fornecedor e exige conta externa. *Voltaria* se a organização já tiver contrato e o agente aceitar OTLP.

**Opção B de propagação.** Mais simples (sem coluna nova), mas perde a continuidade do traço. *Voltaria* se a mudança de esquema fosse proibida.

## Validação

- Teste `Traco_do_POST_continua_no_despacho_e_na_projecao`: o mesmo `TraceId` enviado no `POST` aparece em `outbox publicar` e em `projetor aplicar`.
- Teste de RNF-01: a métrica `caixadiario.outbox.pendentes` mostra o acúmulo com o broker fora.
- No compose: `GET http://localhost:18888/api/telemetry/traces/<traceId>` devolve os spans dos dois serviços.

## Gatilho de revisão

- Ambiente compartilhado ou de produção (troca o destino OTLP, opção 2).
- Volume de traços que exija amostragem (hoje, 100% dos traços são registrados).
