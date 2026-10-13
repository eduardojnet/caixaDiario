# Estado do Projeto

**Documento vivo.** Atualizado ao fim de cada bloco de trabalho. Descreve o que existe, o que falta e o que está decidido, sem otimismo.

**Última atualização:** 2026-10-12 (cartões 42 e 43 concluídos; backlog vazio)
**Build:** `dotnet build CaixaDiario.sln`: 0 avisos, 0 erros (`TreatWarningsAsErrors`, `AnalysisMode=Recommended`)
**Testes:** 65 passando, 0 falhas (domínio 25, arquitetura 4, integração 36 com PostgreSQL e RabbitMQ reais via Testcontainers); carga k6 a 50 req/s com 0,00% de falha (com autenticação, p95 3,3 ms). CI verde no GitHub Actions; cobertura de linha do domínio 96,36%; mutação do domínio 98,21% (Stryker.NET 5.0.0), ambos com limite de 85%

---

## 1. Identificação

| | |
|---|---|
| Produto | **CaixaDiario** |
| Repositório | https://github.com/eduardojnet/caixaDiario (público) |
| Propósito | Desafio técnico de Arquiteto de Soluções |
| Domínio | Fluxo de caixa diário de um comerciante: lançamentos de débito e crédito e saldo diário consolidado. Não é conta corrente bancária |
| Teto de tempo | 5 horas de trabalho efetivo (300 min) |
| Plataforma prevista | .NET 10, PostgreSQL 17, broker a decidir no ADR-0002 |
| Decisor | Eduardo J. G. do Carmo |

---

## 2. Requisitos obrigatórios do enunciado

Conferidos contra o repositório publicado em 2026-10-12 21:37 (cartão 33): cada link responde 200.

| # | Requisito | Estado | Evidência publicada |
|---|---|---|---|
| O1 | Mapeamento de domínios funcionais e capacidades de negócio | Atendido | [01-capacidades-de-negocio.md](https://github.com/eduardojnet/caixaDiario/blob/main/docs/01-capacidades-de-negocio.md), [02-dominios-funcionais.md](https://github.com/eduardojnet/caixaDiario/blob/main/docs/02-dominios-funcionais.md) |
| O2 | Refinamento de requisitos funcionais e não funcionais | Atendido | [03-requisitos.md](https://github.com/eduardojnet/caixaDiario/blob/main/docs/03-requisitos.md): RF-01 a RF-10, RNF-01 a RNF-07 com métrica, meta e medição, QA-01 a QA-05 |
| O3 | Desenho da solução completo (Arquitetura Alvo) | Atendido | [04-arquitetura-alvo.md](https://github.com/eduardojnet/caixaDiario/blob/main/docs/04-arquitetura-alvo.md), [C2](https://github.com/eduardojnet/caixaDiario/blob/main/docs/diagrams/c2-conteineres.md), [contrato](https://github.com/eduardojnet/caixaDiario/blob/main/docs/contratos/lancamento-registrado.v1.md) |
| O4 | Justificativa de ferramentas, tecnologias e tipo de arquitetura | Atendido | [ADR-0001](https://github.com/eduardojnet/caixaDiario/blob/main/docs/adr/ADR-0001-estilo-arquitetural.md), [ADR-0002](https://github.com/eduardojnet/caixaDiario/blob/main/docs/adr/ADR-0002-plataforma-armazenamento-e-integracao.md), aceitos no G2 |
| O5 | Linguagem dominada | Atendido | C# / .NET 10: [src/](https://github.com/eduardojnet/caixaDiario/blob/main/src) |
| O6 | Testes | Atendido | [tests/](https://github.com/eduardojnet/caixaDiario/blob/main/tests): 51 verdes, CI verde, inclusive [RNF-01](https://github.com/eduardojnet/caixaDiario/blob/main/tests/CaixaDiario.Integracao.Tests/Rnf01IsolamentoTests.cs); carga k6 executada |
| O7 | README: como funciona e como rodar localmente | Atendido | [README.md](https://github.com/eduardojnet/caixaDiario/blob/main/README.md), verificado por clone limpo |
| O8 | Repositório público no GitHub | Atendido | https://github.com/eduardojnet/caixaDiario |
| O9 | Toda documentação no repositório | Atendido | [docs/](https://github.com/eduardojnet/caixaDiario/blob/main/docs) e README |

> O enunciado descarta o teste se algum requisito obrigatório não for minimamente atendido. Nenhum item desta tabela pode terminar como "Pendente".

---

## 3. Decisões e portões

| Item | Estado |
|---|---|
| ADR-0001 estilo arquitetural | Aceito: dois serviços, isolamento de falha e não volume |
| ADR-0002 plataforma, armazenamento e integração | Aceito: .NET 10, PostgreSQL 17 por serviço, Dapper, outbox + RabbitMQ |
| G1 pré-voo | Não acionado: as cinco verificações responderam (2026-10-12 19:55) |
| G2 aprovação dos ADRs e autorização de push | G2 aprovado em 2026-10-12 21:06 (Eduardo: "Considere todos os ADR aprovados") |

---

## 4. Implementado

- Esqueleto do repositório: `Directory.Build.props` (.NET 10, `TreatWarningsAsErrors`, `AnalysisMode=Recommended`, `NuGetAudit`), `global.json` (Microsoft Testing Platform), `.editorconfig`, `.gitignore`.
- Documentação: `docs/01` a `docs/04`, ADR-0001 e ADR-0002 (aceitos), C1, C2, sequência, contrato `LancamentoRegistrado` v1 com exemplo JSON.
- `CaixaDiario.Lancamentos.Dominio`: `Lancamento`, `TipoLancamento`, validação (valor positivo com duas casas, tipo, data futura recusada, estorno de tipo oposto e mesmo valor). Sem dependência externa.
- `CaixaDiario.Lancamentos.Api`: `POST` com `Idempotency-Key` obrigatório (201, 200 com `Idempotency-Replayed`, 422), estorno, `GET` do dia, outbox na mesma transação, despachante com espera crescente, publicação com confirmação e `mandatory`, `/health/live` e `/health/ready` (só o próprio banco), logs JSON.
- `CaixaDiario.Consolidado.Api`: projetor idempotente, consulta com `atualizadoAte` e `defasado`, cache de 2 s, limitador token bucket (200/s, fila 50, excedente 429), estado do projetor medido pela própria fila.
- Esquemas: `db/lancamentos` (ledger append-only, chave única de idempotência, estorno único, outbox, papel com `SELECT`/`INSERT` e `UPDATE` só em `outbox.publicado_em`) e `db/consolidado` (`saldo_diario`, `lancamentos_aplicados`, `estado_projecao`).
- `docker-compose.yml`: 2 APIs, 2 PostgreSQL 17, RabbitMQ 4.1.8; `lancamentos` depende só do próprio banco.
- Testes: domínio, arquitetura (NetArchTest: Lancamentos não referencia Consolidado; sem assembly compartilhado), RNF-01 sem Consolidado e sem broker, idempotência sequencial e concorrente, estorno, imutabilidade por privilégio, projetor idempotente, reconciliação ponta a ponta, contrato nos dois lados.
- C3 componentes (`docs/diagrams/c3-componentes.md`), desenhado a partir do código.
- Integração contínua (`.github/workflows/ci.yml`): build sem avisos, testes, cobertura do domínio ≥ 85% e mutação do domínio ≥ 85%, ações fixadas por SHA.
- Observabilidade (ADR-0003): OpenTelemetry nos dois serviços, traço único do `POST` à projeção via `outbox.traceparent` e cabeçalho AMQP, métricas de outbox e projetor, logs por OTLP, painel Aspire Dashboard em `:18888`.
- Segurança de integração (ADR-0004): OAuth2 client credentials com Keycloak, escopo por operação; usuário de broker por serviço com privilégio mínimo; TLS obrigatório no broker (5671), certificados gerados no primeiro `up`.
- Migração versionada (ADR-0005): um migrador DbUp por serviço, executado antes da API; segunda subida sobre o mesmo volume não reaplica nada; senha do papel da aplicação redefinida a cada execução.
- Saldo acumulado até a data (cartão 42) e batimento do despachante (cartão 43), que fecha a lacuna L-05.
- RNF medidos: RNF-01 (teste e demonstração no compose), RNF-02 e RNF-03 (k6: 3001 req a 50/s, 0,00% de falha, p95 1,56 ms), RNF-04 (reconciliação), RNF-06 (privilégio), RNF-07 (idempotência).

## 5. Especificado, não implementado

- RNF-05: meta de 5 s mantida como proposta `[NVI]`. A marca `defasado` está implementada; a defasagem em operação normal não foi medida com instrumento (observada abaixo de 1 s nas demonstrações, sem registro sistemático).
- Segurança, limites declarados (ADR-0004): servidor de autorização em HTTP e com segredos padrão; sem autorização por caixa (QA-04); usuário `consolidado` pode apagar a exchange que declara.
- Integrações com maquininha e banco (C1).
- Arquitetura de Transição (`docs/05-arquitetura-de-transicao.md`): fachada, adaptador do legado e adaptador reverso especificados sobre legado hipotético (H-01 a H-05); nenhuma peça implementada.
- Estimativa de custos (`docs/06-estimativa-de-custos.md`): nada implantado em nuvem; tamanho do armazenamento do Amazon MQ `mq.t3.micro` (20 GB) e valor em reais com impostos `[NVI]`.

## 6. Questões abertas e lacunas

| ID | Questão | Conduta provisória | Origem |
|---|---|---|---|
| L-01 | Números do PacioliBank no texto de citação divergiam do repositório (95% e 87% declarados; 94,11% e 86,96% medidos) | Resolvida: reconferidos no README publicado no GitHub em 2026-10-12 (237 testes, 94,11%, 86,96%, estado de 2026-10-06). A cópia local do PacioliBank está atrás do remoto (230, 95,41%, 87,06%); vale o publicado, que é o que o avaliador abre | Verificação de 2026-10-12 |
| L-02 | Plano original não tinha ADR de tecnologia (requisito O4) | ADR-0002 incluído no Bloco 2 | Verificação de 2026-10-12 |
| QA-01 a QA-06 | Questões de negócio do enunciado | Condutas provisórias em `docs/03-requisitos.md` §7; QA-01 segue aberta mesmo com os dois saldos expostos; QA-06 (saldo de abertura) registrada no cartão 42 | Bloco 1 |
| PR-01 a PR-04 | Premissas com impacto declarado | `docs/03-requisitos.md` §5 | Bloco 1 |
| L-03 | Eventos publicados antes de existir a fila do Consolidado seriam descartados pela exchange fanout | Corrigido: publicação com `mandatory`; sem fila, o evento fica na outbox. Achado pelo teste de reconciliação | Bloco 4 |
| L-04 | `defasado` baseado só no atraso do último evento ficaria verdadeiro para sempre depois de recuperar um acúmulo | Corrigido: estado do projetor (conexão e fila) entra no cálculo. Achado na demonstração do RNF-01 | Bloco 4 |
| L-06 | Gatilho do cartão 39 (migração versionada) cumprido: `db/lancamentos/0003_rastreamento.sql` é a primeira mudança de esquema depois da entrega; só se aplica em volume novo (`docker compose down -v`) | Resolvida no cartão 39: migrador DbUp por serviço aplica scripts novos em volume existente (ADR-0005) | Cartão 36 |
| L-05 | O Consolidado não enxerga a outbox de Lancamentos: com o broker fora, ele sabe que está desconectado, mas não quantos eventos esperam | Resolvida no cartão 43: batimento do despachante com o estado da outbox; sem ele por 15 s, `defasado` | Bloco 4 |

---

## 7. Fila de execução

| Bloco | Teto (min) | Início | Fim | Gasto | Estado | Critério de conclusão |
|---|---|---|---|---|---|---|
| 0. Pré-voo e repositório | 10 | 19:55 | 19:57 | 2 | Concluído | Ferramentas respondem; remoto configurado |
| 1. Fronteira e requisitos | 65 | 21:00 | 21:03 | 3 | Concluído | Dois serviços nominais no mapa; RNF-01 e RNF-02 com meta e medição |
| 2. Decisões | 40 | 21:04 | 21:06 | 2 | Concluído | Frase volume x isolamento no ADR-0001; G2 aprovado |
| 3. Arquitetura Alvo | 35 | 21:07 | 21:11 | 4 | Concluído | C2 renderiza; sobrevivência de Lancamentos visível |
| 4. Código e testes | 80 | 21:12 | 21:31 | 19 | Concluído | Compose sobe; POST 201; saldo confere; testes passam; POST 201 com Consolidado parado |
| 5. README e fechamento | 40 | 21:32 | 21:35 | 3 | Concluído | Clone limpo sobe só com o README |
| Reserva | 30 | 21:36 | 21:37 | 1 | Revisão final (cartão 33) | Consumida apenas pelo Bloco 4 ou por revisão final |
| Backlog (38, 40, 41) | (reserva) | 21:48 | 21:52 | 4 | Concluído | Autorizado por Eduardo; gatilho de sobra de reserva cumprido |
| **Total** | **300** | | | **38** | | |

---

## 8. Histórico

| Data | Evento |
|---|---|
| 2026-10-12 | Acessos verificados: repositório GitHub público e vazio com permissão de escrita; PacioliBank legível; pasta local com leitura e escrita |
| 2026-10-12 | Instruções de execução geradas (CLAUDE.md local, skill `/proximo`) e estado inicial registrado |
| 2026-10-12 | Bloco 0, pré-voo: `dotnet --list-sdks` → 10.0.401; `docker version` → cliente e servidor 29.8.2; `docker compose version` → v5.6.0; `git --version` → 2.43.0; `gh` ausente, acesso ao GitHub verificado por SSH (`ssh -T git@github.com` autentica `eduardojnet`) e `git ls-remote` HTTPS no remoto vazio retorna sem erro |
| 2026-10-12 | Bloco 0: remoto `origin` em HTTPS, conforme o plano; se o push HTTPS falhar por credencial após o G2, trocar para `git@github.com:eduardojnet/caixaDiario.git` (SSH verificado). Decisão reversível |
| 2026-10-12 | Bloco 0: `Directory.Build.props` e `template.md` adaptados do PacioliBank, com comentários e IDs de requisito daquele projeto removidos (regra 13); `global.json` idêntico |
| 2026-10-12 | Bloco 0: identidade git local do repositório igual à do PacioliBank (`Eduardo J. Net <eduardojnet@outlook.com>`), pois `user.name` global não está definido |
| 2026-10-12 | 2026-10-12 21:02 cartão 07 concluído: docs/01-capacidades-de-negocio.md com cadeia de valor de 5 etapas, 4 capacidades nível 1 e 11 nível 2, todas com criticidade e domínio |
| 2026-10-12 | 2026-10-12 21:02 cartão 08 concluído: docs/02-dominios-funcionais.md nomeia Controle de Lançamentos (núcleo, crítico) e Consolidado Diário (suporte, sacrificável); fronteiras, assimetria, dependência unidirecional e glossário de 13 termos |
| 2026-10-12 | 2026-10-12 21:02 cartão 09 concluído: docs/03-requisitos.md §2 com RF-01 a RF-10, cada um com domínio e capacidade |
| 2026-10-12 | 2026-10-12 21:02 cartão 10 concluído: docs/03-requisitos.md §3 com RNF-01 a RNF-07, cada um com métrica, meta e medição; RNF-05 marcado [NVI] |
| 2026-10-12 | 2026-10-12 21:02 cartão 11 concluído: docs/03-requisitos.md §7 com QA-01 a QA-05, conduta provisória e o que muda se respondida; restrições, premissas e drivers em seções próprias |
| 2026-10-12 | Bloco 1: critério verificado: `docs/02` nomeia Controle de Lançamentos e Consolidado Diário (8 ocorrências); RNF-01 e RNF-02 com métrica, meta e medição em `docs/03` §3; §11 aplicada (01 em N1 sem tecnologia, 02 em N2-N3, 03 separa requisito, restrição, premissa, driver e QA) |
| 2026-10-12 | 2026-10-12 21:04 cartão 12 concluído: ADR-0001 com critérios datados 2026-10-12 antes das opções, conta 50 x 86.400 ≈ 4,3 milhões, frase literal 'O volume não justifica a separação; o isolamento de falha justifica' (linha encontrada por grep), comparação com o PacioliBank, consequências negativas e gatilho |
| 2026-10-12 | 2026-10-12 21:04 cartão 13 concluído: ADR-0002 com cinco temas, cada um com alternativa rejeitada e gatilho; transporte A/B/C decidido pelo critério 1; docs/adr/README.md com índice |
| 2026-10-12 | 2026-10-12 21:06 cartão 14 concluído: G2 aprovado por Eduardo; ADR-0001 e ADR-0002 passam a Aceito com redação de fato; nenhuma marca da avaliadora em docs/; primeiro push de main |
| 2026-10-12 | Push HTTPS falhou por credencial (`could not read Username`); remoto trocado para `git@github.com:eduardojnet/caixaDiario.git` e push concluído (origin/main = 36474e7) |
| 2026-10-12 | TickTick sincronizado: cartões 04 a 14 movidos para Concluído, checklists marcados |
| 2026-10-12 | 2026-10-12 21:10 cartão 15 concluído: docs/diagrams/c1-contexto.md e README.md da convenção; mermaid-cli 11.4.2 renderiza (rc=0) |
| 2026-10-12 | 2026-10-12 21:10 cartão 16 concluído: docs/diagrams/c2-conteineres.md renderiza (rc=0, PNG inspecionado); nenhuma seta da zona sacrificável para a crítica; tabela de falhas explica a sobrevivência |
| 2026-10-12 | 2026-10-12 21:10 cartão 17 concluído: docs/diagrams/sequencia-registro-e-projecao.md renderiza (rc=0); passos 4 a 6 mostram lançamento e evento na mesma transação |
| 2026-10-12 | 2026-10-12 21:10 cartão 18 concluído: docs/contratos/lancamento-registrado.v1.md com 8 campos, exemplo JSON versionado em arquivo próprio, regra de versão e semântica ao menos uma vez |
| 2026-10-12 | 2026-10-12 21:10 cartão 19 concluído: docs/04-arquitetura-alvo.md nas seis camadas da §11.5, degradação, um parágrafo de observabilidade e um de segurança |
| 2026-10-12 | Bloco 3: validação Mermaid com `minlag/mermaid-cli:11.4.2` em contêiner (imagem baixada para isso); C1, C2 e sequência renderizam com rc=0. Exemplo do contrato em arquivo JSON próprio para os testes de contrato dos dois lados lerem o mesmo arquivo |
| 2026-10-12 | 2026-10-12 21:30 cartão 20 concluído: CaixaDiario.sln com 3 projetos de src e 3 de teste; dotnet build: 0 avisos, 0 erros; docker compose config -q válido (5 serviços, name: caixadiario, healthchecks em bancos e broker); Dockerfiles multi-stage com USER $APP_UID; /health/live e /health/ready |
| 2026-10-12 | 2026-10-12 21:30 cartão 21 concluído: db/lancamentos e db/consolidado aplicados pelo initdb no compose e pela fixture; lancamentos_app com SELECT, INSERT e UPDATE só em outbox.publicado_em; ImutabilidadeTests verde |
| 2026-10-12 | 2026-10-12 21:30 cartão 22 concluído: POST 201, reenvio 200 com Idempotency-Replayed: true, mesma chave com corpo diferente 422, 10 envios simultâneos geram 1 lançamento; GET do dia lista; estorno validado |
| 2026-10-12 | 2026-10-12 21:30 cartão 23 concluído: DespachanteOutbox com espera crescente até 10 s, publicação com confirmação e mandatory; ready só verifica o banco; Rnf01IsolamentoTests: outbox acumula 5 sem broker e esvazia com 5 mensagens na fila |
| 2026-10-12 | 2026-10-12 21:30 cartão 24 concluído: GET consolidado devolve totalCreditos, totalDebitos, saldoDoDia, quantidade, atualizadoAte, defasado; reentrega não soma (ProjetorTests); cache 2 s; limitador token bucket 200/s com fila 50 (rajada: 462 x 200, 1038 x 429) |
| 2026-10-12 | 2026-10-12 21:30 cartão 25 concluído: Rnf01IsolamentoTests verde (Lancamentos + PostgreSQL real, sem Consolidado e sem broker: 201, outbox acumula, broker volta, outbox esvazia); RegrasDeDependenciaTests verde (4 regras NetArchTest) |
| 2026-10-12 | 2026-10-12 21:30 cartão 26 concluído: dotnet test: 46 testes, 0 falhas, 0 avisos (domínio, arquitetura, idempotência, reconciliação ponta a ponta, projetor idempotente, imutabilidade, contrato) |
| 2026-10-12 | 2026-10-12 21:30 cartão 27 concluído: compose up --build do zero; POST 201 e 201; GET saldo 100,50 conferido; stop consolidado consolidado-db; POST 201; volta: saldo 120,50 com 3 lançamentos; nenhum log Error |
| 2026-10-12 | 2026-10-12 21:30 cartão 28 concluído: k6 grafana/k6:1.8.2 na rede do compose: 3001 requisições a 50,01/s por 60 s, http_req_failed 0,00%, p95 1,56 ms |
| 2026-10-12 | Bloco 4: versões novas conferidas no nuget.org e no Docker Hub em 2026-10-12 e validadas pelo restore e pela execução: RabbitMQ.Client 7.2.2, Testcontainers.RabbitMq 4.15.0 (mesma do PostgreSql), rabbitmq:4.1.8-management-alpine, grafana/k6:1.8.2 |
| 2026-10-12 | Bloco 4: despachante e projetor no processo das próprias APIs (`BackgroundService`), sem terceiro executável; senha do papel da aplicação por script `0002_senha_aplicacao.sh` no initdb, fora do SQL versionado |
| 2026-10-12 | Bloco 4: logs JSON pelo provedor nativo (`AddJsonConsole`), sem Serilog: um pacote a menos, mesmo efeito para o escopo |
| 2026-10-12 | Bloco 4: teste de RNF-01 com banco exclusivo; com banco compartilhado, o despachante do teste ponta a ponta esvaziava a outbox contada (achado na segunda execução) |
| 2026-10-12 | Bloco 4: `GSS Encryption Mode=Disable` nas conexões do compose, mesma correção do PacioliBank, para o Npgsql não tentar carregar libgssapi |
| 2026-10-12 | 2026-10-12 21:34 cartão 29 concluído: README §Como a aplicação funciona: resumo sem tecnologia em 5 linhas, o que muda para quem confere o caixa, dois serviços e frase volume x isolamento, fluxo em 4 passos, links para C2, sequência e ADRs |
| 2026-10-12 | 2026-10-12 21:34 cartão 30 concluído: README §Como rodar localmente: pré-requisitos com versão, 7 passos com resultado esperado, RNF-01 em três comandos, recusas, Se algo der errado, Parar e começar do zero, testes e carga |
| 2026-10-12 | 2026-10-12 21:34 cartão 31 concluído: README com índice, Estado atual coerente com ESTADO §4/§5, citação com números reconferidos no GitHub em 2026-10-12 (237 testes, 94,11%, 86,96%, estado de 2026-10-06), O que faria com mais tempo, LICENSE MIT |
| 2026-10-12 | 2026-10-12 21:35 cartão 32 concluído: clone limpo de github.com/eduardojnet/caixaDiario em pasta temporária: passos 1 a 7 e RNF-01 do README reproduzidos (ready 200/200, saldo 100,50, 201 com Consolidado parado, saldo 120,50); dotnet test no clone: 46/0; passo 6 corrigido (head -5 escondia Idempotency-Replayed) |
| 2026-10-12 | Bloco 5: clone limpo revelou passo 6 do README sem o resultado prometido (`head -5` cortava o cabeçalho `Idempotency-Replayed`); trocado por `grep` dos cabeçalhos e do id |
| 2026-10-12 | Bloco 5: item extra em "O que faria com mais tempo" (batimento do despachante, L-05) registrado como cartão 43 no backlog |
| 2026-10-12 | Tempos da §7 são relógio de parede desta sessão, do primeiro ao último comando de cada bloco; a revisão de Eduardo fora da sessão não está contada |
| 2026-10-12 | 2026-10-12 21:37 cartão 33 concluído: O1 a O9 Atendido no ESTADO §2 com link publicado (todos 200); marca da avaliadora: 0 arquivos versionados e 0 mensagens de commit; §7 com tempos reais; espelho KANBAN atualizado |
| 2026-10-12 | Backlog: Eduardo autorizou 40, 41 e 38; recusou abrir 34 a 37 (regra 15) e 39, 42, 43 (gatilho não cumprido) |
| 2026-10-12 | Cartão 41: mutação inicial 83,93%, abaixo do limite; cinco testes de limite acrescentados (tamanhos, valor máximo, descrição ausente, tipo desconhecido) levaram a 98,21%. Limite mantido em 85% |
| 2026-10-12 | 2026-10-12 21:53 cartão 40 concluído: .github/workflows/ci.yml verde no push f8cdee6 (run 38329130875), ações fixadas por SHA |
| 2026-10-12 | 2026-10-12 21:53 cartão 41 concluído: Stryker.NET 5.0.0: 98,21% (55 mortos, 1 sobrevivente), limite 85% no CI; job verde no push 2482861 (run 38329246089) |
| 2026-10-12 | 2026-10-12 21:53 cartão 38 concluído: docs/diagrams/c3-componentes.md com os dois serviços, desenhado a partir das classes de src/; mermaid-cli renderiza (rc=0) |
| 2026-10-12 | Cartão 34: Eduardo autorizou explicitamente ("34"), abrindo exceção à regra 15 só para a Arquitetura de Transição; a regra segue valendo para 35, 36 e 37 |
| 2026-10-12 | 2026-10-12 21:59 cartão 34 concluído: docs/05-arquitetura-de-transicao.md com hipóteses H-01 a H-05, estrangulamento em 5 fases, critério de avanço C1 a C4 e caminho de volta por fase, diagrama de fases e de arquitetura transitória (mermaid-cli rc=0) |
| 2026-10-12 | Cartão 35: Eduardo autorizou explicitamente ("Siga para 35"), exceção à regra 15 só para a estimativa de custos; segue valendo para 36 e 37 |
| 2026-10-12 | 2026-10-12 22:11 cartão 35 concluído: docs/06-estimativa-de-custos.md: opção A gerenciada ≈ US$ 195,63/mês, opção B VM única ≈ US$ 51,84/mês, cada preço com serviço, código de uso, versão do arquivo e data (AWS Price List Bulk API, sa-east-1, 2026-10-12); 13 licenças conferidas, nenhuma paga |
| 2026-10-12 | Cartão 36: Eduardo autorizou explicitamente ("Completa com OpenTelemetry. Siga para o 36"), exceção à regra 15 só para a observabilidade; segue valendo para o 37 |
| 2026-10-12 | Cartão 36: API de leitura do painel aberta sem autenticação (`DASHBOARD__API__AUTHMODE: Unsecured`), só no ambiente local, para conferir o traço por linha de comando |
| 2026-10-12 | 2026-10-12 22:25 cartão 36 concluído: ADR-0003; OpenTelemetry nos dois serviços com traceparent gravado na outbox e propagado no cabeçalho AMQP; métricas de outbox, projetor, HTTP e limitador; painel Aspire no compose; traço único POST → outbox publicar → projetor aplicar conferido pela API do painel (8 spans, 2 serviços) e por teste; 52 testes verdes; POST 201 com o painel parado |
| 2026-10-12 | Cartão 37: Eduardo autorizou explicitamente ("Siga para o card 37"); com isso, a regra 15 foi excedida nos quatro diferenciais, sempre por ordem explícita |
| 2026-10-12 | Cartão 37: entrypoint do broker espera o cookie do Erlang antes da CLI e roda a CLI como usuário rabbitmq (a primeira versão derrubou o broker com eacces) |
| 2026-10-12 | 2026-10-12 22:48 cartão 37 concluído: ADR-0004; OAuth2 client credentials com Keycloak e escopo por operação; usuário de broker por serviço (permissoes.sh no compose e nos testes); TLS na 5671 com CA gerada no up; SegurancaDeIntegracaoTests verde (401 sem token e com token inválido, 403 sem escopo, ACCESS_REFUSED do Consolidado ao publicar); compose: 401/403/201, 5672 fechada, certificado validado; k6 com token 0,00% p95 3,3 ms; 56 testes verdes |
| 2026-10-12 | Cartão 37: clone limpo em pasta temporária; blocos do README executados em sequência: 200/200, token, 201/201, idempotência 200 com Idempotency-Replayed, lista, RNF-01 201, 401/403, traço completo no painel; nota acrescentada para o 000 logo após a demonstração do RNF-01 |
| 2026-10-12 | Cartão 39: Eduardo autorizou explicitamente ("Siga para o 39"); gatilho já cumprido no cartão 36 |
| 2026-10-12 | Cartão 39: scripts copiados para scripts/<serviço>/ na saída, para os dois migradores não se misturarem quando referenciados pelo mesmo projeto de teste; 0002_senha_aplicacao.sh removidos, a senha passa a ser aplicada pelo migrador |
| 2026-10-12 | 2026-10-12 23:05 cartão 39 concluído: ADR-0005; CaixaDiario.Lancamentos.Migracoes e CaixaDiario.Consolidado.Migracoes (DbUp 7.0.1, diário versoes_do_esquema); compose: primeira subida aplicou 0001 e 0003 / 0001, segunda subida sobre o mesmo volume "Nenhum script novo" nos dois; MigracaoTests e fixture com o mesmo migrador; 60 testes verdes |
| 2026-10-12 | Cartões 42 e 43: Eduardo autorizou explicitamente ("Siga para 42 e 43") com gatilhos não cumpridos; 42 implementado sem responder a QA-01 (os dois saldos expostos), 43 implementado sem requisito formal, como fechamento da L-05 |
| 2026-10-12 | 2026-10-12 23:18 cartão 42 concluído: saldoAcumulado no GET do consolidado, soma dos dias até a data na consulta; teste Saldo_acumulado_reconcilia_entre_dias_inclusive_com_retroativo verde; compose: 1000 ontem e -250,40 hoje → saldoAcumulado 749,60; QA-01 mantida aberta e QA-06 registrada |
| 2026-10-12 | 2026-10-12 23:18 cartão 43 concluído: BatimentoDoDespachante v1 (contrato e exemplo) publicado a cada 5 s com o estado da outbox; Consolidado marca defasado sem batimento por 15 s ou com outbox atrasada na origem; testes de estado, de contrato e ponta a ponta verdes; compose: stop lancamentos → defasado true em 18 s, start → false; 65 testes verdes |
| 2026-10-12 | README: "O que faria com mais tempo" recomposta com as evoluções já registradas nos ADRs e na Arquitetura Alvo, cada uma com gatilho, depois de esvaziado o backlog |
| 2026-10-12 | CI vermelho no push c815cc6 (passo de testes; log do Actions exige autenticação): causa provável, o teste de RNF-01 contava 5 mensagens exatas na fila e o batimento do cartão 43 também chega a ela. Corrigido para contar só eventos LancamentoRegistrado, com batimento de 300 ms no teste para o caso ocorrer em toda execução |
