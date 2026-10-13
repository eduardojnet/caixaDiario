# Arquitetura Alvo

**Público:** misto, em camadas: negócio (1 e 2), backoffice (3), TI (4 a 6).
**Requisito atendido:** desenho da solução completo (O3).
**Data:** 2026-10-12. **Estado de implementação:** [ESTADO.md](ESTADO.md) §4 e §5.

## 1. Resumo executivo

O comerciante precisa registrar cada entrada e saída de dinheiro durante o expediente e conferir o resultado do dia. A solução separa as duas atividades: o registro é protegido como operação crítica e nunca depende do relatório de saldo; o relatório é calculado automaticamente em segundo plano e pode atrasar ou ficar fora do ar sem que nenhum lançamento se perca. O benefício é que a operação que gera receita não para por causa da conferência. O custo aceito é que o saldo consultado pode estar alguns segundos atrás do último lançamento, sempre com a indicação de até quando está atualizado.

## 2. Visão de negócio

- **Valor criado:** registro do caixa disponível o tempo todo; saldo do dia sem cálculo manual.
- **Risco reduzido:** um defeito ou sobrecarga no relatório não impede o comerciante de registrar o que vende e paga; nenhum lançamento é perdido nem registrado em dobro.
- **Indicadores a acompanhar:** disponibilidade do registro de lançamentos (RNF-01); atraso do saldo em relação ao último lançamento (RNF-05); taxa de erro da consulta de saldo em pico, com teto de 5% (RNF-02, RNF-03).

## 3. Visão operacional

| Situação | O que acontece | Automático ou manual |
|---|---|---|
| Lançamento normal | Registrado na hora; entra no saldo do dia em segundos | Automático |
| Envio repetido por falha de conexão | O sistema reconhece o reenvio e devolve o lançamento original, sem duplicar | Automático |
| Saldo ainda não atualizado | A consulta informa até que horário o saldo está atualizado e marca `defasado` | Automático; o comerciante decide se espera |
| Lançamento errado | Corrigido por estorno: novo lançamento de tipo oposto, referenciando o original. Nada é apagado | Manual, pelo comerciante |
| Relatório de saldo fora do ar | O comerciante continua registrando; o saldo se atualiza sozinho quando o relatório volta | Automático |
| Lançamento de dia anterior | Aceito com a data do fato; o saldo daquele dia é recalculado (QA-03) | Automático |

Rastreabilidade: todo lançamento fica no histórico com data do fato, data do registro e, no caso de estorno, a referência ao original. A conferência do contador parte da lista do dia (RF-06), não do saldo.

## 4. Visão técnica

### 4.1. Componentes

Diagramas: [C1 contexto](diagrams/c1-contexto.md), [C2 contêineres](diagrams/c2-conteineres.md), [sequência](diagrams/sequencia-registro-e-projecao.md).

| Componente | Responsabilidade | Tecnologia |
|---|---|---|
| API Lancamentos | Valida e registra lançamentos com idempotência; lista o dia | ASP.NET Core 10, Dapper |
| Despachante da outbox | Publica eventos pendentes no broker, com retentativa | `BackgroundService` no processo da API Lancamentos |
| Banco de lançamentos | Registro append-only e outbox | PostgreSQL 17 |
| Broker | Transporta `LancamentoRegistrado` v1 com fila durável | RabbitMQ |
| Projetor | Aplica cada evento uma vez ao modelo de leitura | `BackgroundService` no processo da API Consolidado |
| API Consolidado | Responde o saldo do dia com cache curto e limitador de taxa | ASP.NET Core 10, Dapper |
| Banco do consolidado | `saldo_diario` e `lancamentos_aplicados` | PostgreSQL 17 |

### 4.2. Comunicação

| De | Para | Protocolo | Natureza |
|---|---|---|---|
| Cliente | API Lancamentos | HTTP/JSON | Síncrona |
| Cliente | API Consolidado | HTTP/JSON | Síncrona |
| Despachante | Broker | AMQP 0-9-1, confirmação de publicação | Assíncrona |
| Broker | Projetor | AMQP 0-9-1, `ack` manual após `COMMIT` | Assíncrona, ao menos uma vez |

Nenhuma comunicação parte do Consolidado em direção a Lancamentos. Contrato do evento: [lancamento-registrado.v1.md](contratos/lancamento-registrado.v1.md).

### 4.3. API

| Serviço | Rota | Comportamento |
|---|---|---|
| Lancamentos | `POST /v1/caixas/{caixaId}/lancamentos` | `Idempotency-Key` obrigatório. `201` novo; `200` com `Idempotency-Replayed: true` no reenvio idêntico; `422` mesma chave com corpo diferente; `400` dado inválido |
| Lancamentos | `GET /v1/caixas/{caixaId}/lancamentos?data=AAAA-MM-DD` | Lista do dia, do próprio banco |
| Consolidado | `GET /v1/caixas/{caixaId}/consolidado/{data}` | `totalCreditos`, `totalDebitos`, `saldoDoDia`, `quantidade`, `atualizadoAte`, `defasado`; `429` quando o limitador recusa |
| Ambos | `GET /health/live`, `GET /health/ready` | `ready` de Lancamentos verifica só o próprio banco |

Não há rota de alteração nem de exclusão de lançamento.

### 4.4. Dados

- **Um banco por serviço.** Nenhum serviço lê o banco do outro.
- **Lancamentos:** tabela `lancamentos` append-only, com chave única `(caixa_id, chave_idempotencia)`; tabela `outbox` gravada na mesma transação. O papel da aplicação tem só `SELECT` e `INSERT` em `lancamentos` (RNF-06); `UPDATE` apenas na coluna de publicação da `outbox`.
- **Consolidado:** `lancamentos_aplicados` (chave primária `lancamento_id`) e `saldo_diario` (chave `caixa_id, data`). Ambos reconstruíveis reprocessando os eventos.
- **Valor monetário:** `numeric(14,2)` no banco, `decimal` no código. Nunca ponto flutuante.

### 4.5. Escala

50 req/s em pico na consulta é carga baixa para uma instância (ADR-0001, a conta dos 4,3 milhões por dia). A consulta é leitura por chave primária num modelo pequeno, com cache de curta duração. A capacidade real será validada pelo teste de carga do RNF-02. Escala horizontal do Consolidado, se necessária, é acrescentar réplicas: o projetor concorre pela mesma fila e a aplicação idempotente impede soma dupla.

### 4.6. Estratégia de degradação do Consolidado

Como a consulta do saldo do dia é frequente em pico e muda pouco entre lançamentos, o Consolidado protege a si mesmo em vez de repassar a pressão:

1. **Cache em memória de curta duração** (poucos segundos) por `(caixaId, data)`: reduz leituras repetidas no banco. O custo é servir um saldo de instantes atrás; `atualizadoAte` torna isso visível.
2. **Limitador de taxa com fila curta:** acima do limite configurado, a requisição excedente recebe `429` imediatamente, em vez de acumular e derrubar o serviço. Cada `429` conta como falha no orçamento de 5% (RNF-03); o limite é dimensionado acima de 50 req/s para que o orçamento não seja consumido em operação normal.
3. **Marca de defasagem:** se o último evento aplicado tiver mais que o limite do RNF-05, a resposta traz `defasado: true`. A consulta não falha por atraso; informa.
4. **Lancamentos não usa cache nem limitador** neste escopo: sua carga não foi declarada, e o registro precisa ser imediato.

### 4.7. Observabilidade

Logs estruturados em JSON nos dois serviços, com identificador de correlação por requisição e o `eventoId` propagado como propriedade da mensagem AMQP, de forma que um lançamento seja rastreável do `POST` à projeção. Métricas que importam para os RNF: tamanho da outbox pendente e idade do evento mais antigo não publicado (sinal de broker fora), defasagem do projetor (`agora − atualizadoAte`), taxa de respostas por código no Consolidado (orçamento de 5%) e latência p95 da consulta. Alvo: traço distribuído com OpenTelemetry, do `POST` ao `COMMIT` da projeção, exportado por OTLP. Implementado neste escopo: logs estruturados e verificações de saúde; métricas e traço são especificados.

### 4.8. Segurança de integração

Credencial de broker por serviço, com privilégio mínimo: Lancamentos só publica na exchange, Consolidado só consome da sua fila; nenhum dos dois administra o broker. Credencial de banco por serviço, sem acesso cruzado; o papel da aplicação de Lancamentos não altera nem apaga lançamentos. Alvo: TLS no broker e nos bancos, identidade de serviço, e OAuth2 client credentials na API, com autorização por `caixaId`. Ambiente local, declarado: sem TLS e sem autenticação na API, senhas por variável de ambiente (`.env`, fora do repositório).

### 4.9. Implantação

Local: Docker Compose com cinco contêineres (duas APIs, dois bancos, broker), nome de projeto fixo e verificações de saúde. Em nuvem, especificado: as duas APIs em serviço gerenciado de contêiner, bancos gerenciados e broker gerenciado, mantidos contrato e topologia. Sem orquestrador de cluster: a carga declarada não o justifica (regra de proporcionalidade, ADR-0001).

## 5. Trade-offs e riscos

| Decisão | Vantagem | Desvantagem | Risco | Mitigação |
|---|---|---|---|---|
| Dois serviços | Isolamento de falha estrutural (RNF-01) | Mais peças; consistência eventual | Dependência acidental de Lancamentos a Consolidado | Teste de arquitetura; teste de integração sem Consolidado; prontidão só do próprio banco |
| Outbox + broker | Broker fora não afeta o registro | Despachante a operar; entrega ao menos uma vez | Outbox crescendo com broker fora por muito tempo | Métrica de tamanho e idade da outbox |
| Projeção assíncrona | Consulta barata e isolada | Saldo pode atrasar | Comerciante decidir com saldo defasado | `atualizadoAte` e `defasado` na resposta |
| Projetor idempotente | Reentrega inofensiva | Tabela extra de aplicados | Soma dupla | Chave primária em `lancamento_id`; teste de reconciliação |
| Limitador com `429` | Consolidado não cai em pico | Parte das consultas recusada | Orçamento de 5% estourar | Limite acima de 50 req/s; teste de carga |
| Imutabilidade por privilégio | Garantia no banco, não em código | Correção só por estorno | | Estorno como fluxo previsto (RF-05) |
| Scripts de inicialização no lugar de migração | Simples no prazo | Mudança de esquema exige recriar volume | Divergência de esquema após a entrega | Adotar migrador na primeira mudança (ADR-0002, gatilho) |

**Dependências externas:** nenhuma obrigatória. Maquininha e banco são evolução (C1).

## 6. Recomendação

A solução adota dois serviços com integração assíncrona por outbox e RabbitMQ, um banco PostgreSQL por serviço, e um modelo de leitura idempotente no Consolidado. A decisão atende ao RNF-01 por isolamento de falha, não por volume ([ADR-0001](adr/ADR-0001-estilo-arquitetural.md)); as escolhas de plataforma e transporte estão em [ADR-0002](adr/ADR-0002-plataforma-armazenamento-e-integracao.md). O principal trade-off é a consistência eventual do saldo, mitigada pela sinalização de defasagem.
